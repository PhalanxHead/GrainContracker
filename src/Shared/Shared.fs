namespace Shared

open System
open Domain

module Route =
    let builder typeName methodName =
        sprintf "/api/%s/%s" typeName methodName

type IGrainConTrackerApi =
    { GetSiteList: unit -> Async<Site list>
      GetDayPrices: unit -> Async<DayPrice list>
      GetDayPricesBySite: Site list -> Async<DayPrice list> }
