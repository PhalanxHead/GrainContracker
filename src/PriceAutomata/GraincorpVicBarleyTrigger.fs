namespace Graincontracker.PriceAutomata

open Microsoft.Azure
open Microsoft.Azure.WebJobs
open Microsoft.Extensions.Logging
open FSharp.CosmosDb
open FSharp.Control
open Logary
open Logary.Message
open System.IO
open Microsoft.Extensions.Configuration

module GraincorpVicBarleyTrigger =
    open GrainContracker.Common
    open Shared.Domain
    open Shared.Configuration.Constants

    // let private cosmosConnString =
    // System.Environment.GetEnvironmentVariable
    // (sprintf "%s:%s" EnvVariable_ConnStringsPrefix EnvVariable_CosmosDbConnString)

    let private gCBarleyLogger =
        Shared.Configuration.Logging.getLogger "GrainContracker.PriceAutomata" "PriceAutomata.GraincorpBarley"

    let private generatePricesheetNameFromPrice (price: DayPrice) =
        (sprintf
            "%s_%s_%s.pdf"
             (price.Pool.ToString())
             (price.Grain.ToString())
             (price.PriceSheetDate.ToString("yyyy-MMM-dd")))

    [<Literal>]
    let private FinalTimerString = "0 * 2,7,22 * * *"

    [<Literal>]
    let private TestingTimerString = "0 0/1 * * * *"

    [<FunctionName("GraincorpVicBarleyTimerTrigger")>]
    let run ([<TimerTrigger(FinalTimerString)>] myTimer: TimerInfo, log: ILogger) (context: ExecutionContext) =
        let config =
            ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", true, true).AddEnvironmentVariables().Build()

        let cosmosConnString =
            config.GetConnectionString EnvVariable_CosmosDbConnString
        // System.Environment.GetEnvironmentVariable
        //(sprintf "%s:%s" EnvVariable_ConnStringsPrefix EnvVariable_CosmosDbConnString)

        let storageConnString =
            config.GetConnectionString EnvVariable_AzStorageConnString

        async {

            event Info "Starting Vic Barley Download at {time}"
            |> setField "time" System.DateTime.Now
            |> gCBarleyLogger.logSimple

            let! pdfBytes = PriceSheetDownloader.DownloadPricesheetBytes Barley VIC

            use pdfStream = new MemoryStream(pdfBytes)

            let pdfString =
                PdfHelper.readPdfToStringPdfPig pdfStream

            let prices =
                PdfParser.GrainCorpBarleyParser pdfString

            do! StorageHelper.UploadStreamToBlob
                    pdfStream
                    (generatePricesheetNameFromPrice prices.[0])
                    storageConnString

            let priceIdsString = prices |> List.map (fun p -> p.id)

            let op: Azure.Cosmos.CosmosClientOptions = Azure.Cosmos.CosmosClientOptions()

            op.Serializer <- Shared.Serializer.CompactCosmosSerializer()

            event Debug "Connection String: {connstring}"
            |> setField "connstring" cosmosConnString
            |> gCBarleyLogger.logSimple

            event Info "Found {priceCount} prices to write to the CosmosDB"
            |> setField "priceCount" prices.Length
            |> setField "connstring" cosmosConnString
            |> gCBarleyLogger.logSimple

            event Debug "New Price IDs: {priceIds}"
            |> setField "priceIds" priceIdsString
            |> gCBarleyLogger.logSimple

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
            |> gCBarleyLogger.logSimple

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
            |> gCBarleyLogger.logSimple

        }
        |> Async.StartAsTask
