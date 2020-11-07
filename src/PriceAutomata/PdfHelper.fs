namespace GrainContracker.Common

open System
open Shared.Domain

module String =
    let contains x = String.exists ((=) x)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Array =
    let inline last (arr: _ []) = arr.[arr.Length - 1]

module PdfHelper =
    open UglyToad.PdfPig

    let log = NLog.FSharp.Logger("PdfHelper")

    let readPdfToStringPdfPig (pdfPath: IO.Stream) =
        let mutable pdfString = ""
        use doc = PdfDocument.Open(pdfPath)
        for page in doc.GetPages() do
            for word in page.GetWords() do
                pdfString <- pdfString + " " + word.Text

        log.Debug "Read %i chars from PDF" pdfString.Length

        pdfString

(*
    let readSamplePdfPig =
        readPdfToStringPdfPig @"../../../../../../VIC-Barley_Oct16.pdf"
    *)

module PdfParser =
    open Shared.Units

    [<Literal>]
    let private DateIndex = 8

    [<Literal>]
    let private DateLen = 3

    type private SiteRow =
        { SiteName: string
          SitePrices: string [] }

    type private SheetData =
        { PdfDate: DateTimeOffset
          PdfPool: SalesPool
          SaleType: PriceType
          Grain: GrainType
          Buyer: Buyers }

    let log = NLog.FSharp.Logger("PdfParser")

    /// <summary>
    /// Works out the date from the beginning of the PDF String,
    /// </summary>
    /// <param name="pdfArr">
    /// The String Array containing the text of the pdf document
    /// </param>
    /// <returns>
    /// The date of the price sheet, tupled with the pdf string array that begins after the date
    /// </returns>
    let private extractDateFromGCBarley (pdfArr: string []) =
        let remainingArr =
            pdfArr |> Array.skip (DateIndex + DateLen)

        let priceSheetDate =
            (pdfArr
             |> Array.skip DateIndex
             |> Array.take DateLen
             |> String.concat " "
             |> DateTimeOffset.Parse)

        priceSheetDate, remainingArr


    /// <summary>
    /// Works out which seasons this price sheet is interested in, and sorts them.
    /// </summary>
    /// <param name="pdfArrAfterDate"></param>
    /// <returns>
    /// The array of seasons for this price sheet, tupled with the pdf string array starting after the seasons row.
    /// </returns>
    let private extractSeasonsFromGCBarley (pdfArrAfterDate: string []) =
        let arrBeforeSeasons =
            (pdfArrAfterDate
             |> Array.skipWhile (String.contains '/' >> not))

        let seasons =
            (arrBeforeSeasons
             |> Array.takeWhile (String.contains '/')
             |> Array.map Season
             |> Array.sort)

        let remainingArrAfterSeasons =
            (arrBeforeSeasons
             |> Array.skipWhile (String.contains '/'))

        seasons, remainingArrAfterSeasons


    /// <summary>
    /// Gets a single row from the main price table
    /// </summary>
    /// <param name="pdfArrAfterGrades">
    /// The PDF string array which starts on the first site name
    /// </param>
    /// <returns>
    /// The Site Row (Site name + prices in a string array without $ signs)
    /// and the remaining PDF string after the last price in the row.
    /// </returns>
    let private extractSitePriceRowFromGCBarley (pdfArrAfterGrades: string []) =
        let siteName =
            (pdfArrAfterGrades
             |> Array.takeWhile (String.contains '$' >> not))

        let sitePrices =
            (pdfArrAfterGrades
             |> Array.skip siteName.Length
             |> Array.takeWhile (String.exists Char.IsDigit)
             |> Array.map (fun str -> str.Replace("$", String.Empty)))

        let pdfArrAfterThisSite =
            (pdfArrAfterGrades
             |> Array.skip siteName.Length
             |> Array.skipWhile (String.exists Char.IsDigit))

        { SiteName = siteName |> String.concat " "
          SitePrices = sitePrices },
        pdfArrAfterThisSite


    /// <summary>
    /// Extracts a list of Site Prices for the given Site Row.
    /// </summary>
    /// <param name="siteRow">The site row to extract from</param>
    /// <param name="seasons">The list of seasons present on this site sheet</param>
    /// <param name="staticSheetData">The obejct containing all the ambiant sheet data like date and pool</param>
    /// <returns>List of Site Prices for the primary grain grade</returns>
    let private extractBarleySitePricesFromSiteRow (siteRow: SiteRow) (seasons: Season []) (staticSheetData: SheetData) =
        let relevantPriceCount =
            min siteRow.SitePrices.Length seasons.Length

        let relevantPrices =
            match relevantPriceCount with
            | 0 ->
                log.Error "Row %A has no Prices!" siteRow.SiteName
                nullArg "There's no prices in this row!"
            | 1 -> [| siteRow.SitePrices.[0] |]
            | 2 ->
                [| siteRow.SitePrices.[0]
                   (Array.last siteRow.SitePrices) |]
            | _ ->
                [| siteRow.SitePrices.[0]
                   siteRow.SitePrices.[1]
                   (Array.last siteRow.SitePrices) |]

        let mutable prices = []

        Array.zip relevantPrices seasons
        |> Array.map (fun price_season ->
            let priceDec = Decimal.Parse(fst price_season)
            let priceAsCurrency = AUD(aud.lift priceDec)
            prices <-
                prices
                @ [ { Season = snd price_season
                      Price = priceAsCurrency } ])
        |> ignore

        log.Debug
            "Read %i seasons for Site \"%s\" from Price Sheet \"%A\":%A"
            relevantPriceCount
            siteRow.SiteName
            staticSheetData.Grain
            (staticSheetData.PdfDate.ToString("yyyy-MMM-dd"))

        let nonIdPrice =
            { id = ""
              Pool = staticSheetData.PdfPool
              Buyer = staticSheetData.Buyer
              SaleType = staticSheetData.SaleType
              PriceSheetDate = staticSheetData.PdfDate
              Grade = GrainType.DefaultGrade staticSheetData.Grain
              Grain = staticSheetData.Grain
              Site = Site siteRow.SiteName
              Price = prices }

        { nonIdPrice with
              id = DayPrice.GenerateId nonIdPrice }

    /// <summary>
    /// Recursively extracts the next site row from the PDF String array into an accumulator until the remaining depth = 0
    /// </summary>
    /// <param name="rowAccum">The array of already extracted rows to append to</param>
    /// <param name="remainingDepth">The number of rows left to extract</param>
    /// <param name="pdfArray">The remaining PDF Array</param>
    /// <returns>An array of the Site Rows in the given PDF array</returns>
    let rec private extractNextSiteRow (rowAccum: SiteRow []) (remainingDepth: int) (pdfArray: string []) =
        if remainingDepth < 0 then
            rowAccum
        else
            let thisRow, pdfArrayAfterThisRow = extractSitePriceRowFromGCBarley pdfArray
            extractNextSiteRow (Array.append rowAccum [| thisRow |]) (remainingDepth - 1) (pdfArrayAfterThisRow)


    /// <summary>
    /// Extracts the main price table from a string of the PDF Price Sheet for a Barley Price
    /// </summary>
    /// <param name="pdf"></param>
    /// <returns></returns>
    let GrainCorpBarleyParser (pdf: string) =
        let pdfArr =
            pdf.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)

        let pdfDate, pdfArrAfterDate = extractDateFromGCBarley pdfArr

        use b =
            NLog.NestedDiagnosticsLogicalContext.Push(pdfDate.ToString("yyyy-MMM-dd"))

        log.Info "Parsing Barley Prices for sheet on %A" (pdfDate.ToString("yyyy-MMM-dd"))

        let pdfSeasons, pdfArrAfterSeasons =
            extractSeasonsFromGCBarley pdfArrAfterDate

        let pdfArrAfterGradeHeaders =
            (pdfArrAfterSeasons
             |> Array.skip 1
             |> Array.skipWhile (String.exists Char.IsDigit))

        let priceRows =
            extractNextSiteRow [||] 37 pdfArrAfterGradeHeaders

        let staticSheetData =
            { PdfDate = pdfDate
              PdfPool = VIC
              SaleType = Contract
              Grain = Barley
              Buyer = GrainCorp }

        let mutable priceList = []

        for row in priceRows do
            priceList <-
                priceList
                @ [ (extractBarleySitePricesFromSiteRow row pdfSeasons staticSheetData) ]

        log.Info "Read Barley Prices for %i sites from sheet %A" priceList.Length (pdfDate.ToString("yyyy-MMM-dd"))

        priceList

    let GrainCorpWheatParser (pdf: string) =
        let pdfArr =
            pdf.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)

        let pdfDate, pdfArrAfterDate = extractDateFromGCBarley pdfArr

        use b =
            NLog.NestedDiagnosticsLogicalContext.Push(pdfDate.ToString("yyyy-MMM-dd"))

        log.Warn "Wheat Parser not Implemented! Returning Default Wheat DayPrice"

        [ { id = ""
            PriceSheetDate = pdfDate
            Pool = VIC
            Buyer = GrainCorp
            Grade = GrainType.DefaultGrade Wheat
            Grain = Wheat
            Site = Site "Test"
            SaleType = Contract
            Price = [] } ]
