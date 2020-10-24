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
        private
        | AUD of decimal<aud>
        | USD of decimal<usd>

    module Currency =
        let create (str: string) =
            match str.[0..2].ToUpper() with
            | "AUD" ->
                str.[4..]
                |> Decimal.Parse
                |> aud.lift
                |> AUD
                |> Ok

            | "USD" ->
                str.[4..]
                |> Decimal.Parse
                |> usd.lift
                |> USD
                |> Ok
            | _ -> Error "Currency doesn't match format USD$250.00"

        let value curr =
            match curr with
            | AUD c -> "AUD$" + c.ToString()
            | USD c -> "USD$" + c.ToString()

    type PriceType =
        | Contract
        | Cash

    type Buyers =
        | GrainCorp
        | AustralianWheatBoard
        | Viterra
        | Other of string

    type GrainType =
        private
        | Wheat
        | Barley
        | Canola
        | Oats
        | Lentils
        | Chickpeas
        | Sorghum
        | Beans
        | Lupins

    module GrainType =
        let create (str: string) =
            match str.ToLower() with
            | "wheat" -> Ok Wheat
            | "barley" -> Ok Barley
            | "canola" -> Ok Canola
            | "oats" -> Ok Oats
            | "lentils" -> Ok Lentils
            | "chickpeas" -> Ok Chickpeas
            | "sorghum" -> Ok Sorghum
            | "beans" -> Ok Beans
            | "lupins" -> Ok Lupins
            | _ -> Error "Grain type not recognised"

        let value gt = gt.ToString()

    type SalesPool =
        | QLD
        | VIC
        | WA
        | NSW
        | SA

    type Season = private Season of string

    module Season =
        let create str =
            if (String.IsNullOrWhiteSpace(str)) then Error "Season must be non-Empty" else Ok(Season str)

        let value (Season season) = season

    type Grade = private Grade of string

    module Grade =
        let create str =
            if (String.IsNullOrWhiteSpace(str)) then Error "Grade must be non-Empty" else Ok(Grade str)

        let value (Grade grade) = grade

    type Site = private Site of string

    module Site =
        let create str =
            if (String.IsNullOrWhiteSpace(str)) then Error "Site must be non-Empty" else Ok(Site str)

        let value (Site site) = site

    type SitePrice =
        { Id: Guid
          PriceSheetDate: DateTimeOffset
          Season: Season
          Grade: Grade
          Grain: GrainType
          Site: Site
          Price: Currency }

    type PriceSheet =
        { Id: Guid
          SheetDate: DateTimeOffset
          Pool: SalesPool
          Buyer: Buyers
          SaleType: PriceType
          Prices: SitePrice list }

module Dto =
    type SitePrice =
        { id: Guid
          PriceSheetDate: DateTimeOffset
          Season: string
          Grade: string
          Grain: string
          Site: string
          Price: string }

    type PriceSheet =
        { id: Guid
          SheetDate: DateTimeOffset
          Pool: string
          Buyer: string
          SaleType: string
          Prices: SitePrice list }

    module SitePrice =
        open ResultBuilder

        let fromDomain (domainSitePrice: Domain.SitePrice): SitePrice =
            let thisSeason =
                domainSitePrice.Season |> Domain.Season.value

            let thisPrice =
                domainSitePrice.Price |> Domain.Currency.value

            let thisGrade =
                domainSitePrice.Grade |> Domain.Grade.value

            let thisSite =
                domainSitePrice.Site |> Domain.Site.value

            { id = domainSitePrice.Id
              PriceSheetDate = domainSitePrice.PriceSheetDate
              Grain = domainSitePrice.Grain.ToString()
              Season = thisSeason
              Grade = thisGrade
              Site = thisSite
              Price = thisPrice }

        let toDomain (dto: SitePrice): Result<Domain.SitePrice, string> =
            result {
                // get each (validated) simple type from the DTO as a success or failure
                let! thisSeason = dto.Season |> Domain.Season.create
                let! thisPrice = dto.Price |> Domain.Currency.create
                let! thisGrade = dto.Grade |> Domain.Grade.create
                let! thisSite = dto.Site |> Domain.Site.create
                let! thisGrain = dto.Grain |> Domain.GrainType.create

                // combine the components to create the domain object

                return { Id = dto.id
                         PriceSheetDate = dto.PriceSheetDate
                         Grain = thisGrain
                         Season = thisSeason
                         Grade = thisGrade
                         Site = thisSite
                         Price = thisPrice }
            }
