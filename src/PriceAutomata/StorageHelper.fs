namespace GrainContracker.Common

open Shared.Configuration.Constants
open Logary
open Logary.Message

module StorageHelper =
    open Microsoft.WindowsAzure.Storage
    open Microsoft.WindowsAzure.Storage.Blob
    open System.IO

    // let private storageConnString =
    // System.Environment.GetEnvironmentVariable
    // (sprintf "%s:%s" EnvVariable_ConnStringsPrefix EnvVariable_AzStorageConnString)

    let private blobUploadLogger =
        Shared.Configuration.Logging.getLogger "GrainContracker.PriceAutomata" "PriceAutomata.StorageHelper"

    let UploadStreamToBlob (stream: Stream) (blobName: string) storageConnString =
        async {
            stream.Position <- int64 (0)

            let cloudStorageAcc =
                CloudStorageAccount.Parse(storageConnString)

            event Debug "Uploading {byteCount} bytes to Storage Account at {cloudStorage}"
            |> setField "byteCount" stream.Length
            |> setField "cloudStorage" cloudStorageAcc.BlobEndpoint.OriginalString
            |> blobUploadLogger.logSimple

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

            event Debug "Uploaded {byteCount} bytes to File {fileName} in Storage Account at {cloudStorage}"
            |> setField "byteCount" blockBlob.StreamMinimumReadSizeInBytes
            |> setField "cloudStorage" cloudStorageAcc.BlobEndpoint
            |> setField "fileName" blockBlob.Name
            |> blobUploadLogger.logSimple

        }
