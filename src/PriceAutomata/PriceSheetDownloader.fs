namespace GrainContracker.Common

open System.Net
open System

module PriceSheetDownloader =

    open Shared.Domain

    let log = NLog.FSharp.Logger()

    let DownloadPricesheetBytes (grain: GrainType) (pool: SalesPool) =

        let dlUri =
            match grain, pool with
            | Barley, VIC -> Uri("http://www.graincorp.com.au/daily-contract-prices/VIC-Barley.pdf")
            | _ -> Uri("http://www.graincorp.com.au/daily-contract-prices/VIC-Barley.pdf")

        log.Debug "Downloading Pricesheet from %s" dlUri.AbsoluteUri

        async {
            use wc = new WebClient()

            let! pdfBytes = wc.AsyncDownloadData(dlUri)

            return pdfBytes
        }
