namespace Company.Function

open FSharp.CosmosDb
open FSharp.Control
//open Logary
//open Logary.Message
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open System.Net
open System
open Microsoft.Extensions.Configuration
open System.IO
open NLog
open NLog.FSharp
open NLog.Extensions.Logging

module HttpTrigger =
    open GrainContracker.Common
    open Shared.Domain
    open Shared.Configuration.Constants


    // let private gCBarleyLogger =
    // Shared.Configuration.Logging.getLogger "GrainContracker.PriceAutomata" "PriceAutomata.GraincorpBarley"

    let private generatePricesheetNameFromPrice (price: DayPrice) =
        (sprintf
            "%s_%s_%s.pdf"
             (price.Pool.ToString())
             (price.Grain.ToString())
             (price.PriceSheetDate.ToString("yyyy-MMM-dd")))

    [<FunctionName("HttpTrigger")>]
    let run ([<HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)>] req: HttpRequest)
            (azureILogger: Microsoft.Extensions.Logging.ILogger)
            (context: ExecutionContext)
            =
        let config =
            ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", true, true).AddEnvironmentVariables().Build()

        let loggerTarget =
            new NLog.Extensions.Logging.MicrosoftILoggerTarget(azureILogger)

        loggerTarget.Layout <-
            Layouts.Layout.FromString
                ("${longdate}|${event-properties:item=EventId_Id:whenEmpty=0}|${uppercase:${level}}| ${logger} | ${message} ${exception:format=tostring} | ${ndlc:topFrames=3}")

        let nlogConfig = NLog.Config.LoggingConfiguration()
        nlogConfig.AddRuleForAllLevels(loggerTarget, "*")

        NLog.LogManager.Configuration <- nlogConfig

        let log =
            NLog.FSharp.Logger("GrainCorpHTTPLogger")

        let cosmosConnString =
            config.GetConnectionString EnvVariable_CosmosDbConnString

        let storageConnString =
            config.GetConnectionString EnvVariable_AzStorageConnString

        async {

            log.Info "F# HTTP trigger function processed a request."

            let! pdfBytes = PriceSheetDownloader.DownloadPricesheetBytes Barley VIC

            use a =
                NestedDiagnosticsLogicalContext.Push("GC_VIC_Barley_HTTP")

            use pdfStream = new MemoryStream(pdfBytes)

            let pdfString =
                PdfHelper.readPdfToStringPdfPig pdfStream

            let prices =
                PdfParser.GrainCorpBarleyParser pdfString

            use b =
                NestedDiagnosticsLogicalContext.Push
                    (sprintf "PdfDate_%s" (prices.[0].PriceSheetDate.ToString("yyyy-MMM-dd")))

            do! StorageHelper.UploadStreamToBlob
                    pdfStream
                    (generatePricesheetNameFromPrice prices.[0])
                    storageConnString

            let priceIdsString = prices |> List.map (fun p -> p.id)

            let op: Azure.Cosmos.CosmosClientOptions = Azure.Cosmos.CosmosClientOptions()

            op.Serializer <- Shared.Serializer.CompactCosmosSerializer()

            log.Debug "Connection String: %s" cosmosConnString

            log.Info "Found %i prices to write to the CosmosDB" prices.Length

            log.Debug "Top 10 new Price IDs: %A" (List.take 10 priceIdsString)

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

            log.Info "Found %i of these prices already in CosmosDB!" (existingPIDS.Length)

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

            log.Info "Wrote %i new prices to the CosmosDB" count

            return OkObjectResult(prices) :> IActionResult
        }
        |> Async.StartAsTask
