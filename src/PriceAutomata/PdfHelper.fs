namespace GrainContracker.Common

open System
open System.Text
open Shared.Domain

module String =
    let contains x = String.exists ((=) x)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Array =
    let inline last (arr: _ []) = arr.[arr.Length - 1]

module PdfHelper =
    open UglyToad.PdfPig

    let readPdfToStringPdfPig (pdfPath: string) =
        let mutable pdfString = ""
        use doc = PdfDocument.Open(pdfPath)
        for page in doc.GetPages() do
            for word in page.GetWords() do
                pdfString <- pdfString + " " + word.Text

        pdfString

    let readSamplePdfPig =
        readPdfToStringPdfPig @"../../../../../../VIC-Barley_Oct13.pdf"

module PdfParser =
    open Shared.Units

    [<Literal>]
    let DateIndex = 8

    [<Literal>]
    let DateLen = 3

    type SiteRow =
        { SiteName: string
          SitePrices: string [] }

    /// <summary>
    /// Works out the date from the beginning of the PDF String,
    /// </summary>
    /// <param name="pdfArr">
    /// The String Array containing the text of the pdf document
    /// </param>
    /// <returns>
    /// The date of the price sheet, tupled with the pdf string array that begins after the date
    /// </returns>
    let extractDateFromGCBarley (pdfArr: string []) =
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
    let extractSeasonsFromGCBarley (pdfArrAfterDate: string []) =
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
    let extractSitePriceRowFromGCBarley (pdfArrAfterGrades: string []) =
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
    /// <returns>List of Site Prices for the primary grain grade</returns>
    let extractSitePricesFromSiteRow (siteRow: SiteRow) (seasons: Season []) sheetDate =
        let relevantPriceCount =
            min siteRow.SitePrices.Length seasons.Length

        let relevantPrices =
            match relevantPriceCount with
            | 0 -> nullArg "There's no prices in this row!"
            | 1 -> [| siteRow.SitePrices.[0] |]
            | 2 ->
                [| siteRow.SitePrices.[0]
                   (Array.last siteRow.SitePrices) |]
            | _ ->
                [| siteRow.SitePrices.[0]
                   siteRow.SitePrices.[1]
                   (Array.last siteRow.SitePrices) |]

        Array.zip relevantPrices seasons
        |> Array.map (fun price_season ->
            let priceDec = Decimal.Parse(fst price_season)
            let priceAsCurrency = AUD(aud.lift priceDec)
            { PriceSheetDate = sheetDate
              Season = (snd price_season)
              Grade = Grade "BAR1"
              Grain = Barley
              Site = Site siteRow.SiteName
              Price = priceAsCurrency })
        |> Array.toList

    /// <summary>
    /// Recursively extracts the next site row from the PDF String array into an accumulator until the remaining depth = 0
    /// </summary>
    /// <param name="rowAccum">The array of already extracted rows to append to</param>
    /// <param name="remainingDepth">The number of rows left to extract</param>
    /// <param name="pdfArray">The remaining PDF Array</param>
    /// <returns>An array of the Site Rows in the given PDF array</returns>
    let rec extractNextSiteRow (rowAccum: SiteRow []) (remainingDepth: int) (pdfArray: string []) =
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

        printfn "%A" pdfDate

        let pdfSeasons, pdfArrAfterSeasons =
            extractSeasonsFromGCBarley pdfArrAfterDate

        printfn "%A" pdfSeasons
        printfn "%A" pdfArrAfterSeasons

        let pdfArrAfterGradeHeaders =
            (pdfArrAfterSeasons
             |> Array.skip 1
             |> Array.skipWhile (String.exists Char.IsDigit))

        let priceRows =
            extractNextSiteRow [||] 37 pdfArrAfterGradeHeaders

        let mutable priceList2 = []

        for row in priceRows do
            priceList2 <-
                priceList2
                @ (extractSitePricesFromSiteRow row pdfSeasons)

        let priceList: SitePrice list =
            [ { PriceSheetDate = DateTimeOffset.Now
                Season = pdfSeasons.[0]
                Grade = Grade "BAR1"
                Grain = Barley
                Site = Site "Murtoa Sub"
                Price = AUD 200.00m<aud> } ]

        let thisPriceSheet =
            { SheetDate = pdfDate
              Pool = VIC
              SaleType = Contract
              Buyer = GrainCorp
              Prices = priceList2 }

        printfn "%A" thisPriceSheet

        thisPriceSheet


(*
module PdfSharpHelper =
    open PdfSharp.Pdf.IO
    open PdfSharp.Pdf.Content
    open PdfSharp.Pdf.Content.Objects

    let rec extractText (content: CObject, sb: StringBuilder) =
        match content with
        | :? CArray as xs ->
            for x in xs do
                extractText (x, sb)
        | :? CComment -> ()
        | :? CInteger -> ()
        | :? CName -> ()
        | :? CNumber -> ()
        // Tj/TJ = Show text
        | :? COperator as op when op.OpCode.OpCodeName = OpCodeName.Tj
                                  || op.OpCode.OpCodeName = OpCodeName.TJ ->
            for element in op.Operands do
                extractText (element, sb)

            sb.Append(" ") |> ignore

        | :? COperator -> ()
        | :? CSequence as xs ->
            for x in xs do
                extractText (x, sb)
        | :? CString as s -> sb.Append(s.Value) |> ignore
        | x ->
            raise
            <| System.NotImplementedException(x.ToString())

    let readAllText (pdfPath: string) =
        use document =
            PdfReader.Open(pdfPath, PdfDocumentOpenMode.ReadOnly)

        let result = StringBuilder()
        for page in document.Pages do
            let content = ContentReader.ReadContent(page)
            extractText (content, result)
            result.AppendLine() |> ignore
        result.ToString()

    let readSamplePdfSharp =
        readAllText @"../../../../../../VIC-Barley_Oct13.pdf"

*)
