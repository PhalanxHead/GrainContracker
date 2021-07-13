namespace Shared

open System
open Domain

module Route =
    let builder typeName methodName =
        sprintf "/api/%s/%s" typeName methodName

type IGrainConTrackerApi =
    { getDayPrices: unit -> Async<DayPrice list> }

