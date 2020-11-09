namespace Graincontracker.PriceAutomata

open Microsoft.Azure
open Microsoft.Azure.WebJobs
open FSharp.CosmosDb
open FSharp.Control
open System.IO
open Microsoft.Extensions.Configuration
open NLog

module GraincorpVicWheatTrigger =

    open GrainContracker.Common
    open Shared.Domain
    open Shared.Configuration.Constants

    [<FunctionName("GraincorpVicWheatTimerTrigger")>]
    let run ([<TimerTrigger(FinalTimerString)>] graincorpVicWheatTimer: TimerInfo)
            (azureILogger: Microsoft.Extensions.Logging.ILogger)
            (context: ExecutionContext)
            =
        let config =
            ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", true, true).AddEnvironmentVariables().Build()

        let log =
            Shared.Configuration.Logging.getLogger azureILogger "GrainCorpLogger" (config.GetSection("NLog"))

        let cosmosConnString =
            config.GetConnectionString EnvVariable_CosmosDbConnString

        let storageConnString =
            config.GetConnectionString EnvVariable_AzStorageConnString

        async {

            log.Info "Starting Vic Wheat Download at %s" (System.DateTime.Now.ToString())

            use a =
                NestedDiagnosticsLogicalContext.Push("GC_VIC_Wheat")

            let! pdfBytes = PriceSheetDownloader.DownloadPricesheetBytes Wheat VIC

            use pdfStream = new MemoryStream(pdfBytes)

            let pdfString =
                PdfHelper.readPdfToStringPdfPig pdfStream

            let prices = PdfParser.GrainCorpWheatParser pdfString

            use b =
                NestedDiagnosticsLogicalContext.Push
                    (sprintf "PdfDate_%s" (prices.Head.PriceSheetDate.ToString("yyyy-MMM-dd")))

            do! StorageHelper.UploadStreamToBlob
                    pdfStream
                    (DayPrice.GeneratePricesheetNameFromPrice prices.Head)
                    storageConnString
        }
        |> Async.StartAsTask
