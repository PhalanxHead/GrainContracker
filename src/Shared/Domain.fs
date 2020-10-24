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

    type SalesPool =
        | QLD
        | VIC
        | WA
        | NSW
        | SA

    type Season = Season of string
    type Grade = Grade of string
    type Site = Site of string

    [<CLIMutable>]
    type SitePrice =
        { id: Guid
          SheetId: Guid option
          PriceSheetDate: DateTimeOffset
          Season: Season
          Grade: Grade
          Grain: GrainType
          Site: Site
          Price: Currency }

    [<CLIMutable>]
    type PriceSheet =
        { id: Guid
          SheetDate: DateTimeOffset
          Pool: SalesPool
          Buyer: Buyers
          SaleType: PriceType }
