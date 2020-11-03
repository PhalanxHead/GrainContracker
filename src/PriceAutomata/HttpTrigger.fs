namespace Company.Function

open FSharp.CosmosDb
open FSharp.Control
open Logary
open Logary.Message
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.Extensions.Logging
open System.Net
open System
open System.IO

module HttpTrigger =
    open GrainContracker.Common
    open Shared.Domain
    open Shared.Configuration.Constants

    let cosmosConnString =
        System.Environment.GetEnvironmentVariable
            (sprintf "%s:%s" EnvVariable_ConnStringsPrefix EnvVariable_CosmosDbConnString)

    let GCBarleyLogger =
        Shared.Configuration.Logging.getLogger "GrainContracker.PriceAutomata" "PriceAutomata.GraincorpBarley"

    let GeneratePricesheetNameFromPrice (price: DayPrice) =
        (sprintf
            "%s_%s_%s.pdf"
             (price.Pool.ToString())
             (price.Grain.ToString())
             (price.PriceSheetDate.ToString("yyyy-MMM-dd")))

    [<FunctionName("HttpTrigger")>]
    let run ([<HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)>] req: HttpRequest) (log: ILogger) =
        async {

            log.LogInformation("F# HTTP trigger function processed a request.")

            let! pdfBytes = PriceSheetDownloader.DownloadPricesheetBytes Barley VIC

            use pdfStream = new MemoryStream(pdfBytes)

            let pdfString =
                PdfHelper.readPdfToStringPdfPig pdfStream

            let prices =
                PdfParser.GrainCorpBarleyParser pdfString

            do! StorageHelper.UploadStreamToBlob pdfStream (GeneratePricesheetNameFromPrice prices.[0])

            let priceIdsString = prices |> List.map (fun p -> p.id)

            let op: Azure.Cosmos.CosmosClientOptions = Azure.Cosmos.CosmosClientOptions()

            op.Serializer <- Shared.Serializer.CompactCosmosSerializer()

            event Debug "Connection String: {connstring}"
            |> setField "connstring" cosmosConnString
            |> GCBarleyLogger.logSimple

            event Info "Found {priceCount} prices to write to the CosmosDB"
            |> setField "priceCount" prices.Length
            |> setField "connstring" cosmosConnString
            |> GCBarleyLogger.logSimple

            event Debug "New Price IDs: {priceIds}"
            |> setField "priceIds" priceIdsString
            |> GCBarleyLogger.logSimple

            let cosmosConnection =
                Cosmos.fromConnectionStringWithOptions cosmosConnString op
                |> Cosmos.database Database_Name
                |> Cosmos.container Container_Name

            let getExistingSitePrices =
                cosmosConnection
                |> Cosmos.query "SELECT c.id FROM c WHERE ARRAY_CONTAINS (@newIds, c.id)"
                |> Cosmos.parameters [ "@newIds", box priceIdsString ]
                |> Cosmos.execAsync<RecordId>

            let! existingPIDS = getExistingSitePrices |> AsyncSeq.toListAsync

            event Info "Found {priceCount} of these prices already in the CosmosDB!"
            |> setField "priceCount" existingPIDS.Length
            |> setField "connstring" cosmosConnString
            |> GCBarleyLogger.logSimple

            let cleanprices =
                prices
                |> List.filter (fun p -> existingPIDS |> List.contains { id = p.id } |> not)

            let insertSitePrices =
                cosmosConnection
                |> Cosmos.insertMany<DayPrice> cleanprices
                |> Cosmos.execAsync

            let mutable count = 0

            do! insertSitePrices
                |> AsyncSeq.iter (fun _ -> count <- count + 1)

            event Info "Wrote {priceCount} new prices to the CosmosDB"
            |> setField "priceCount" count
            |> setField "connstring" cosmosConnString
            |> GCBarleyLogger.logSimple

            return OkObjectResult(prices) :> IActionResult
        }
        |> Async.StartAsTask
