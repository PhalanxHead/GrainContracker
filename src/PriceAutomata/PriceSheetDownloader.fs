namespace GrainContracker.Common

open System.Net
open System

module PriceSheetDownloader =

    open Shared.Domain

    let DownloadPricesheetBytes (grain: GrainType) (pool: SalesPool) =

        let dlUri =
            match grain, pool with
            | Barley, VIC -> Uri("http://www.graincorp.com.au/daily-contract-prices/VIC-Barley.pdf")
            | _ -> Uri("http://www.graincorp.com.au/daily-contract-prices/VIC-Barley.pdf")

        async {
            use wc = new WebClient()

            let! pdfBytes = wc.AsyncDownloadData(dlUri)

            return pdfBytes
        }
