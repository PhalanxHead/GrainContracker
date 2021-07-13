namespace GrainContracker.Common

open System.Net.Http
open System.Net
open System

module PriceSheetDownloader =

    open Shared.Domain

    let log = NLog.FSharp.Logger()

    let DownloadPricesheetBytes (grain: GrainType) (pool: SalesPool) =

        let dlUri =
            match grain, pool with
            | Barley, VIC -> Uri(String.Format(@"https://season-update-uat.app.baqend.com/v1/file/www/uploads/{0}%20Barley%20Daily%20Grower%20Bids%20App%20Data%20VIC.pdf?BCB", DateTime.Now.ToString("yyMMdd")))
            | Wheat, VIC -> Uri(String.Format(@"https://season-update-uat.app.baqend.com/v1/file/www/uploads/{0}%20Wheat%20Bid%20Sheet%20VIC.pdf", DateTime.Now.ToString("yyMMdd")))
            | _ -> Uri("http://www.graincorp.com.au/daily-contract-prices/VIC-Barley.pdf")

        log.Info "Downloading Pricesheet from %s" dlUri.AbsoluteUri

        async {
            use httpHandler = new HttpClientHandler()
            httpHandler.AutomaticDecompression <- DecompressionMethods.GZip ||| DecompressionMethods.None
            use httpClient = new HttpClient(httpHandler)

            let! response = httpClient.GetAsync(dlUri) |> Async.AwaitTask
            response.EnsureSuccessStatusCode () |> ignore
            let! pdfBytes = response.Content.ReadAsByteArrayAsync() |> Async.AwaitTask

            return pdfBytes
        }
