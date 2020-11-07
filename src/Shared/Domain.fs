namespace Shared

open FSharp.Data.UnitSystems.SI.UnitSymbols
open System

module Units =
    [<Measure>]
    type mt // Mass, Metric Tonnes

    [<Measure>]
    type bsh // Mass, Bushels

    // Currency, Australian Dollar
    [<Measure>]
    type aud =
        static member lift(v: decimal) = v * 1.0m<aud>

    // Currency, US Dollar
    [<Measure>]
    type usd =
        static member lift(v: decimal) = v * 1.0m<usd>

module Domain =
    open Units

    type Season = Season of string
    type Grade = Grade of string

    type Site =
        | Site of string
        static member ExtractSiteName(Site site) = site

    type PriceMeasure =
        | MetricTonnes
        | Bushels

    type Currency =
        | AUD of decimal<aud>
        | USD of decimal<usd>

    type PriceType =
        | Contract
        | Cash

    type Buyers =
        | GrainCorp
        | AustralianWheatBoard
        | Viterra
        | Other of string

    type GrainType =
        | Wheat
        | Barley
        | Canola
        | Oats
        | Lentils
        | Chickpeas
        | Sorghum
        | Beans
        | Lupins
        static member DefaultGrade grain =
            match grain with
            | Wheat -> Grade "APW1"
            | Barley -> Grade "BAR1"
            | Canola -> Grade "CAN ISCC"
            | Oats -> failwith "Not Implemented"
            | Lentils -> failwith "Not Implemented"
            | Chickpeas -> Grade "CHKP"
            | Sorghum -> Grade "SOR"
            | Beans -> failwith "Not Implemented"
            | Lupins -> failwith "Not Implemented"

    type SalesPool =
        | QLD
        | VIC
        | WA
        | NSW
        | SA

    type SeasonPrice = { Season: Season; Price: Currency }

    [<CLIMutable>]
    type DayPrice =
        { id: string
          PriceSheetDate: DateTimeOffset
          Pool: SalesPool
          Buyer: Buyers
          Grade: Grade
          Grain: GrainType
          Site: Site
          SaleType: PriceType
          Price: SeasonPrice list }
        static member GenerateId(price: DayPrice) =
            price.PriceSheetDate.ToString("yy-MM-dd")
            + (Site.ExtractSiteName price.Site).Replace(" ", String.Empty)
            + price.Grain.ToString()
            + price.Buyer.ToString()
            + price.Pool.ToString()

        static member GeneratePricesheetNameFromPrice(price: DayPrice) =
            (sprintf
                "%s_%s_%s.pdf"
                 (price.Pool.ToString())
                 (price.Grain.ToString())
                 (price.PriceSheetDate.ToString("yyyy-MMM-dd")))


    type RecordId = { id: string }
