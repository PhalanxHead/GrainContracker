namespace GrainContracker.Common

open System.Net
open System
open Logary
open Logary.Message

module PriceSheetDownloader =

    open Shared.Domain

    let private pSDownloaderLogger =
        Shared.Configuration.Logging.getLogger "GrainContracker.PriceAutomata" "PriceAutomata.PriceSheetDownloader"

    let DownloadPricesheetBytes (grain: GrainType) (pool: SalesPool) =

        let dlUri =
            match grain, pool with
            | Barley, VIC -> Uri("http://www.graincorp.com.au/daily-contract-prices/VIC-Barley.pdf")
            | _ -> Uri("http://www.graincorp.com.au/daily-contract-prices/VIC-Barley.pdf")


        event Debug "Downloading Pricesheet from {uri}"
        |> setField "uri" dlUri.OriginalString
        |> pSDownloaderLogger.logSimple

        async {
            use wc = new WebClient()

            let! pdfBytes = wc.AsyncDownloadData(dlUri)

            return pdfBytes
        }
