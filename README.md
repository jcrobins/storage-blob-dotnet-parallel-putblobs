# High-Rate PutBlobs

This application issues many concurrent PutBlobs of a given size for a given duration of time.


## Prerequisites

**NOTE** For best performance, this application should be run atop .NET Core 2.1 or later.

* Install .NET core 2.1 for [Linux](https://www.microsoft.com/net/download/linux) or [Windows](https://www.microsoft.com/net/download/windows)

If you don't have an Azure subscription, create a [free account](https://azure.microsoft.com/free/?WT.mc_id=A261C142F) before you begin.

## Create a storage account using the Azure portal

First, create a new general-purpose storage account to use for this quickstart.

1. Go to the [Azure portal](https://portal.azure.com) and log in using your Azure account. 
2. On the Hub menu, select **New** > **Storage** > **Storage account - blob, file, table, queue**. 
3. Enter a unique name for your storage account. Keep these rules in mind for naming your storage account:
    - The name must be between 3 and 24 characters in length.
    - The name may contain numbers and lowercase letters only.
4. Make sure that the following default values are set: 
    - **Deployment model** is set to **Resource manager**.
    - **Account kind** is set to **General purpose**.
    - **Performance** is set to **Standard**.
    - **Replication** is set to **Locally Redundant storage (LRS)**.
5. Select your subscription. 
6. For **Resource group**, create a new one and give it a unique name. 
7. Select the **Location** to use for your storage account.
8. Check **Pin to dashboard** and click **Create** to create your storage account. 

After your storage account is created, it is pinned to the dashboard. Click on it to open it. Under **Settings**, click **Access keys**. Select the primary key and copy the associated **Connection string** to the clipboard, then paste it into a text editor for later use.  Note that we are directly using storage account keys in this example for the sake of simplicity.  It is highly recommended to use another means to authenticate with the storage service in production environments.

## Put the connection string in an environment variable

This solution requires a connection string be stored in an environment variable securely on the machine running the sample. Follow one of the examples below depending on your Operating System to create the environment variable. If using windows close out of your open IDE or shell and restart it to be able to read the environment variable.

### Linux

```bash
export storageconnectionstring="<yourconnectionstring>"
```
### Windows

```cmd
setx storageconnectionstring "<yourconnectionstring>"
```

At this point, you can run this application. It creates its own file to upload and download, and then cleans up after itself by deleting everything at the end.

## Run the application

Navigate to the base directory of the repository and run with the following command and arguments.

### Command
dotnet run --project .\storage-blob-dotnet-parallel-putblobs\storage-blob-dotnet-parallel-putblobs.csproj

### Arguments
- `arg0: Blob Size` (Default: 8192)
Specifies the size of the payload (in bytes) for each uploaded blob.
- `arg1: Run Time` (Default: 15)
The total period of time the test should run.
- `arg2: Level of Concurrency` (Default: 64)
The maximum number of PutBlob operations to be executed at any given point of time.  

### Example
```
dotnet run --project .\storage-blob-dotnet-parallel-putblobs\storage-blob-dotnet-parallel-putblobs.csproj
```
The application will upload 8192 byte blobs for 15 seconds, with up to 64 PutBlob commands
being issued at any given time.


```
dotnet run --project .\storage-blob-dotnet-parallel-putblobs\storage-blob-dotnet-parallel-putblobs.csproj 4096 60 80
```
The application will upload 4096 byte blobs for 60 seconds, with up to 80 PutBlob commands
being issued at any given time.


## More information

The [Azure storage documentation](https://docs.microsoft.com/azure/storage/) includes a rich set of tutorials and conceptual articles, which serve as a good complement to the samples.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
