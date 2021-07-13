module Server

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Saturn

open Shared
open Shared.SampleData


open Shared.Domain
open Shared.Units
open System
open System.Threading.Tasks

let graincontrackerApi =
    { getDayPrices =
          fun () ->
              async {
                  let! t = Task.Delay(1000) |> Async.AwaitTask
                  return List.sortByDescending (fun x -> x.PriceSheetDate) (SampleDayPrices_All())
              } }


let webApp =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue graincontrackerApi
    |> Remoting.buildHttpHandler

let app =
    application {
        url "http://0.0.0.0:8085"
        use_router webApp
        memory_cache
        use_static "public"
        use_gzip
    }

run app
