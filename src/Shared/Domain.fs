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

    [<Measure>]
    type usd // Currency, US Dollar

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

    type SitePrice =
        { PriceSheetDate: DateTimeOffset
          Season: Season
          Grade: Grade
          Grain: GrainType
          Site: Site
          Price: Currency }

    type PriceSheet =
        { SheetDate: DateTimeOffset
          Pool: SalesPool
          Buyer: Buyers
          SaleType: PriceType
          Prices: SitePrice list }
