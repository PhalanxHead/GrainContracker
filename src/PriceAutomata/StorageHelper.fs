namespace GrainContracker.Common

open Shared.Configuration.Constants

module StorageHelper =
    open Microsoft.WindowsAzure.Storage
    open Microsoft.WindowsAzure.Storage.Blob
    open System.IO

    let log = NLog.FSharp.Logger()

    let UploadStreamToBlob (stream: Stream) (blobName: string) storageConnString =
        async {
            stream.Position <- int64 (0)

            let cloudStorageAcc =
                CloudStorageAccount.Parse(storageConnString)

            log.Debug "Uploading PDF to Storage Account at %s" cloudStorageAcc.BlobEndpoint.OriginalString

            stream.Position <- int64 (0)

            let blobClient = cloudStorageAcc.CreateCloudBlobClient()

            let container =
                blobClient.GetContainerReference(BlobPriceSheetContainerName)

            container.CreateIfNotExistsAsync()
            |> Async.AwaitTask
            |> ignore

            let permissions =
                BlobContainerPermissions(PublicAccess = BlobContainerPublicAccessType.Blob)

            container.SetPermissionsAsync(permissions)
            |> Async.AwaitTask
            |> ignore

            let blockBlob = container.GetBlockBlobReference blobName
            blockBlob.Properties.ContentType <- "application/pdf"

            do! blockBlob.UploadFromStreamAsync(stream)
                |> Async.AwaitTask

            log.Debug
                "Uploaded %i bytes to File %s in Storage Account at %s"
                blockBlob.StreamMinimumReadSizeInBytes
                blockBlob.Name
                cloudStorageAcc.BlobEndpoint.OriginalString

        }
