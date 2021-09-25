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
open System.Linq

let GetAllDayPrices =
    fun () ->
        async {
            let! t = Task.Delay(1000) |> Async.AwaitTask
            return List.sortByDescending (fun x -> x.PriceSheetDate) (SampleDayPrices_All())
        }

let GetDayPricesBySite (sites: Site list) =
    async {
        let! t = Task.Delay(1000) |> Async.AwaitTask

        return
            (SampleDayPrices_All())
            |> List.filter (fun x -> sites.Contains(x.Site))
            |> List.sortByDescending (fun x -> x.PriceSheetDate)
    }

let GetSiteList =
    fun () ->
        async {
            let! t = Task.Delay(1000) |> Async.AwaitTask

            return
                List.map (fun x -> x.Site) (SampleDayPrices_All())
                |> List.distinct
                |> List.sortBy (fun x -> x.ToString())
        }

let graincontrackerApi: IGrainConTrackerApi =
    { GetSiteList = GetSiteList
      GetDayPrices = GetAllDayPrices
      GetDayPricesBySite = GetDayPricesBySite }


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
