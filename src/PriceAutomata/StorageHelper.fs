namespace GrainContracker.Common

open Shared.Configuration.Constants

module StorageHelper =
    open Microsoft.WindowsAzure.Storage
    open Microsoft.WindowsAzure.Storage.Blob
    open System.IO

    let private storageConnString =
        System.Environment.GetEnvironmentVariable
            (sprintf "%s:%s" EnvVariable_ConnStringsPrefix EnvVariable_AzStorageConnString)


    let UploadStreamToBlob (stream: Stream) (blobName: string) =
        async {
            stream.Position <- int64 (0)

            let cloudStorageAcc =
                CloudStorageAccount.Parse(storageConnString)

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

        }
