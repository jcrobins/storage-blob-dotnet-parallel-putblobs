//------------------------------------------------------------------------------
//MIT License

//Copyright(c) 2018 Microsoft Corporation. All rights reserved.

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.
//------------------------------------------------------------------------------

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace Sample_HighRatePutBlob
{
    /// <summary>
    /// This application uploads several blobs in parallel at a high rate.
    /// </summary>
    /// <remarks>
    /// See README.md for details on how to run this application.
    /// 
    /// NOTE: For performance reasons, this code is best run on .NET Core v2.1 or later.
    /// 
    /// Documentation References: 
    /// - What is a Storage Account - https://docs.microsoft.com/azure/storage/common/storage-create-storage-account
    /// - Getting Started with Blobs - https://docs.microsoft.com/azure/storage/blobs/storage-dotnet-how-to-use-blobs
    /// - Blob Service Concepts - https://docs.microsoft.com/rest/api/storageservices/Blob-Service-Concepts
    /// - Blob Service REST API - https://docs.microsoft.com/rest/api/storageservices/Blob-Service-REST-API
    /// - Blob Service C# API - https://docs.microsoft.com/dotnet/api/overview/azure/storage?view=azure-dotnet
    /// - Scalability and performance targets - https://docs.microsoft.com/azure/storage/common/storage-scalability-targets
    /// - Azure Storage Performance and Scalability checklist https://docs.microsoft.com/azure/storage/common/storage-performance-checklist
    /// - Storage Emulator - https://docs.microsoft.com/azure/storage/common/storage-use-emulator
    /// - Asynchronous Programming with Async and Await  - http://msdn.microsoft.com/library/hh191443.aspx
    /// </remarks>
    class Program
    {

        private static ulong nScheduledOperations = 0;
        private static ulong nCompleteOperations = 0;

        static async Task DeleteAllContainers(CloudBlobClient blobClient)
        {
            BlobContinuationToken continuationToken = null;
            do
            {
                ContainerResultSegment resultSegment = await blobClient.ListContainersSegmentedAsync(continuationToken);

                //List<Task> deletionTasks = new List<Task>();
                Console.WriteLine($"Deleting {resultSegment.Results.Count()} containers from account '{blobClient.Credentials.AccountName}'.");

                foreach (CloudBlobContainer container in resultSegment.Results)
                {
                    await container.DeleteAsync();
                    Console.WriteLine($"Deleted container {container.Name}.");
                    //deletionTasks.Add(container.DeleteAsync().ContinueWith((t)=> { Console.WriteLine($"Deleted."); }));
                }

                //await Task.WhenAll(deletionTasks);

                continuationToken = resultSegment.ContinuationToken;

            } while (continuationToken != null);
        }

        static async Task GetSize(CloudBlobClient blobClient)
        {
            Console.WriteLine("Fetching total size.");
            long totalSize = 0;
            BlobContinuationToken continuationToken = null;
            do
            {
                ContainerResultSegment resultSegment = await blobClient.ListContainersSegmentedAsync(continuationToken);

                foreach (CloudBlobContainer container in resultSegment.Results)
                {

                    BlobContinuationToken blobContinuationToken = null;
                    do
                    {
                        BlobResultSegment blobResults = await container.ListBlobsSegmentedAsync(blobContinuationToken);
                        foreach (CloudBlockBlob blob in blobResults.Results)
                        {
                            totalSize += blob.Properties.Length;
                        }
                        blobContinuationToken = blobResults.ContinuationToken;
                        Console.WriteLine($"...TotalSizeSoFar = {totalSize}");

                    } while (blobContinuationToken != null);

                }


                continuationToken = resultSegment.ContinuationToken;

            } while (continuationToken != null);

            Console.WriteLine($"Total size = {totalSize}.");
        }

        static void Main(string[] args)
        {
            string containerName = $"hightpsputblob";
            string blobPrefix = $"sampleBlob{new Random().Next(0, 1000)}";

            if (!ParseArguments(args, out ulong blobSizeBytes, out TimeSpan runTime, out uint levelOfConcurrency))
            {
                return;
            }

            try
            {
                // Load the connection string for use with the application. The storage connection string is stored
                // in an environment variable on the machine running the application called storageconnectionstring.
                // If the environment variable is created after the application is launched in a console or with Visual
                // Studio, the shell needs to be closed and reloaded to take the environment variable into account.
                string storageConnectionString = Environment.GetEnvironmentVariable("storageconnectionstring");
                if (string.IsNullOrEmpty(storageConnectionString))
                {
                    throw new Exception("Unable to connect to storage account. The environment variable 'storageconnectionstring' is not defined. See README.md for details.");
                }
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                // Create the container if it doesn't yet exist.
                CloudBlobContainer container = blobClient.GetContainerReference(containerName);
                container.CreateIfNotExistsAsync().Wait();

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Schedule cancellation of the test based on the given run time.
                CancellationTokenSource cancellationSource = new CancellationTokenSource(runTime);

                Console.WriteLine($"Begin high TPS PutBlob test for blobs prefixed with '{blobPrefix}' in container '{containerName}'.");
                Task uploadBlobTask = UploadBlobsAsync(blobClient, container, blobPrefix, blobSizeBytes, (int)levelOfConcurrency, cancellationSource.Token);

                try
                {
                    uploadBlobTask.Wait();
                }
                // Filter out TaskCancelledExceptions.
                catch (TaskCanceledException) { }
                catch (AggregateException ex)
                {
                    Exception inner = ex;
                    // SUPER HACKY - Do not consider aggregate exceptions originating from task cancellation to be exceptional.
                    while (inner.InnerException != null)
                    {
                        if (inner.InnerException is TaskCanceledException)
                        { break; }
                        inner = inner.InnerException;
                    }
                    if (inner.InnerException == null)
                    { throw; }
                }
                catch (Exception) { throw; }

                stopwatch.Stop();

                Console.WriteLine($"Test completed in {stopwatch.Elapsed.TotalSeconds} seconds.");

                // To be considerate, consider deleting the created blobs / container.
                DeleteContainerAsync(container).Wait();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed.  Details: {ex.Message}");
            }

            Console.ReadLine();

            
        }

        static async Task DeleteContainerAsync(CloudBlobContainer container)
        {
            Console.WriteLine("Press [Enter] to delete test data from the account.");
            Console.ReadLine();
            await container.DeleteAsync();
            Console.WriteLine($"Successfully deleted container '{container.Name}'");
        }

        public static async Task UploadBlobsAsync
            (CloudBlobClient blobClient,
             CloudBlobContainer container,
             string blobPrefix,
             ulong blobSizeInBytes,
             int levelOfConcurrency,
             CancellationToken cancellationToken)
        {
            // Create a buffer with the given size.
            byte[] buffer = new byte[blobSizeInBytes];
            Random rand = new Random();
            rand.NextBytes(buffer);


            // Create a task to periodically report metrics until cancellation.
            Task metricReportingTask = new Task(async () =>
            {
                TimeSpan reportPeriod = TimeSpan.FromSeconds(5);
                ulong nPreviouslyScheduledOperations = nScheduledOperations;
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(reportPeriod);

                    ulong nCurrentlyScheduledOperations = nScheduledOperations;
                    ulong deltaOperations = nCurrentlyScheduledOperations - nPreviouslyScheduledOperations;
                    double tps = deltaOperations / reportPeriod.TotalSeconds;
                    Console.WriteLine($"\t{deltaOperations} PutBlob operations issued in {reportPeriod.TotalSeconds} seconds ({tps} TPS).");
                    nPreviouslyScheduledOperations = nCurrentlyScheduledOperations;
                }
            });

            try
            {
                ConcurrentDictionary<string, Task> tasks = new ConcurrentDictionary<string, Task>();
                SemaphoreSlim semaphore = new SemaphoreSlim(levelOfConcurrency, levelOfConcurrency);

                metricReportingTask.Start();

                for (; !cancellationToken.IsCancellationRequested; ++nScheduledOperations)
                {
                    string blobName = blobPrefix + nScheduledOperations;

                    CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);
                    //CloudBlockBlob blockBlob = new CloudBlockBlob(new Uri(container.Uri.ToString().Replace(".net", ".net:1443") + "/" + blobName), blobClient.Credentials);

                    await semaphore.WaitAsync();

                    tasks[blobName] = Task.Run(async ()=>
                        {
                            try
                            {
                                await blockBlob.UploadFromByteArrayAsync(buffer, 0, buffer.Length, AccessCondition.GenerateEmptyCondition(), new BlobRequestOptions(), new OperationContext(), cancellationToken);
                                ++nCompleteOperations;
                            }
                            catch(Exception)
                            {
                                throw;
                            }
                            finally
                            {
                                tasks.Remove(blobName, out Task val);
                                semaphore.Release();
                            }
                        }, cancellationToken);
                }

                ICollection<Task> remainingTasks = tasks.Values;
                Console.WriteLine($"Cancellation received. Waiting for the remaining {remainingTasks.Count()} operations to complete.");
                remainingTasks.Append(metricReportingTask);
                await Task.WhenAll(remainingTasks);
            }
            finally
            {
                Console.WriteLine($"Issued {nScheduledOperations} operations. Successfully completed {nCompleteOperations} operations.");
            }
        }

        static bool ParseArguments
            (string[] args,
             out ulong blobSizeBytes,
             out TimeSpan runTime,
             out uint levelOfConcurrency)
        {
            bool isValid = true;

            // Enforce maximums.
            const uint  MAX_BLOCKS = 50000; // Maximum number of blocks that can be committed.
            const ulong MAX_BLOCK_SIZE = 100 * 1024 * 1024; // Maximum size accepted for a single block: 100MiB.
            const ulong MAX_BLOB_SIZE = MAX_BLOCKS * MAX_BLOCK_SIZE; // Maximum size of a block blob.

            // Establish default values.
            blobSizeBytes = 8 * 1024; // 8KB
            runTime = TimeSpan.FromSeconds(15);// TimeSpan.MaxValue;
            levelOfConcurrency = 64;
            
            try
            {
                if (args.Length > 0)
                {
                    blobSizeBytes = Convert.ToUInt32(args[0]);
                }
                if (args.Length > 1)
                {
                    runTime = TimeSpan.FromSeconds(Convert.ToDouble(args[1]));
                }
                if (args.Length > 2)
                {
                    levelOfConcurrency = Convert.ToUInt32(args[2]);
                }
            }
            catch (Exception)
            {
                isValid = false;
            }

            if (!isValid)
            {
                Console.WriteLine("Invalid Arguments Provided.  Expected Arguments:");
                Console.WriteLine("\targ0:BlobSize (Bytes)");
                Console.WriteLine("\targ1:RunTime (Seconds)");
                Console.WriteLine("\targ2:LevelOfConcurrency");
            }
            else if (blobSizeBytes > MAX_BLOB_SIZE)
            {
                Console.WriteLine($"BlockSizeBytes (arg0) cannot exceed the maximum number of bytes for a Block Blob ({MAX_BLOB_SIZE}).");
                isValid = false;
            }

            return isValid;
        }
    }

}
