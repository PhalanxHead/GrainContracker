namespace Graincontracker.PriceAutomata

open Microsoft.Azure
open Microsoft.Azure.WebJobs
open FSharp.CosmosDb
open FSharp.Control
open System.IO
open Microsoft.Extensions.Configuration
open NLog

module GraincorpVicBarleyTrigger =
    open GrainContracker.Common
    open Shared.Domain
    open Shared.Configuration.Constants

    [<FunctionName("GraincorpVicBarleyTimerTrigger")>]
    let run
        ([<TimerTrigger(FinalTimerString)>] graincorpVicBarleyTimer: TimerInfo)
        (azureILogger: Microsoft.Extensions.Logging.ILogger)
        (context: ExecutionContext)
        =
        let config =
            ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", true, true)
                .AddEnvironmentVariables()
                .Build()

        let log =
            Shared.Configuration.Logging.getLogger azureILogger "GrainCorpLogger" (config.GetSection("NLog"))

        let cosmosConnString =
            config.GetConnectionString EnvVariable_CosmosDbConnString

        let storageConnString =
            config.GetConnectionString EnvVariable_AzStorageConnString

        async {

            log.Info "Starting Vic Barley Download at %s" (System.DateTime.Now.ToString())

            use a =
                NestedDiagnosticsLogicalContext.Push("GC_VIC_Barley")

            let! pdfBytes = PriceSheetDownloader.DownloadPricesheetBytes Barley VIC

            use pdfStream = new MemoryStream(pdfBytes)

            let pdfString =
                PdfHelper.readPdfToStringPdfPig pdfStream

            let prices =
                PdfParser.GrainCorpBarleyParser pdfString

            use b =
                NestedDiagnosticsLogicalContext.Push(
                    sprintf "PdfDate_%s" (prices.Head.PriceSheetDate.ToString("yyyy-MMM-dd"))
                )

            do!
                StorageHelper.UploadStreamToBlob
                    pdfStream
                    (DayPrice.GeneratePricesheetNameFromPrice prices.Head)
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

            log.Info "Found %i of these prices already in CosmosDB! They won't be updated." (existingPIDS.Length)

            let cleanprices =
                prices
                |> List.filter (fun p -> existingPIDS |> List.contains { id = p.id } |> not)

            let insertSitePrices =
                cosmosConnection
                |> Cosmos.insertMany<DayPrice> cleanprices
                |> Cosmos.execAsync

            let mutable count = 0

            do!
                insertSitePrices
                |> AsyncSeq.iter (fun _ -> count <- count + 1)

            log.Info "Wrote %i new prices to the CosmosDB" count

        }
        |> Async.StartAsTask
