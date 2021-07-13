module Index

open Elmish
open Fable.Remoting.Client
open Thoth.Fetch
open Shared
open Shared.SampleData
open System

type Model =
    { LoadingDayPrices: bool
      DayPrices_All: Domain.DayPrice list }


type Msg = GotDayPrices of Domain.DayPrice list

let todosApi =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<IGrainConTrackerApi>


let init () : Model * Cmd<Msg> =
    let model =
        { LoadingDayPrices = true
          DayPrices_All = [] }

    let cmd =
        Cmd.OfAsync.perform todosApi.getDayPrices () GotDayPrices

    model, cmd

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | GotDayPrices dayPrices ->
        { model with
              DayPrices_All = dayPrices
              LoadingDayPrices = false },
        Cmd.none

open Fable.React
open Fable.React.Props
open Fulma

let pickBackground =
    let rnd = System.Random()

    let backgrounds =
        [| "/Backgrounds/a.jpg"
           "/Backgrounds/b.jpg"
           "/Backgrounds/c.jpg"
           "/Backgrounds/d.jpg"
           "/Backgrounds/e.png" |]

    backgrounds.[rnd.Next(backgrounds.Length)]

let navBrand =
    Navbar.Brand.div [] [
        Navbar.Item.a [ Navbar.Item.IsActive true ] [
            str "Hello John"
        ]
    ]

open Fable.DateFunctions
open System.Linq

let containerBox (model: Model) (dispatch: Msg -> unit) =
    Box.box' [] [

        Content.content [] [
            if model.LoadingDayPrices then
                div [] [
                    Heading.p [ Heading.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered)
                                                    Modifier.TextColor IsBlack
                                                    Modifier.TextSize(Screen.All, TextSize.Is5) ] ] [
                        str "Grain Prices Loading"
                        Progress.progress [ Progress.Option.Size IsSmall
                                            Progress.Option.Color IsPrimary ] []
                    ]
                ]
            else
                div [ Class "table-container" ] [
                    let sheetDates =
                        List.sortDescending (
                            List.distinct (List.map (fun (x: Domain.DayPrice) -> x.PriceSheetDate) model.DayPrices_All)
                        )

                    let dateList = sheetDates

                    Table.table [ Table.IsStriped ] [
                        thead [] [
                            tr [] [
                                th [] [ str "Site" ]
                                for sheetDate in dateList do
                                    th [] [
                                        str (sheetDate.Format("dd/MM/yy"))
                                    ]
                            ]
                        ]
                        tbody [] [
                            for siteprice in List.groupBy (fun (x: Domain.DayPrice) -> x.Site) model.DayPrices_All do
                                tr [] [
                                    match (fst siteprice) with
                                    | Domain.Site site -> td [] [ str site ]

                                    let sitePriceSheets = snd siteprice

                                    for date in dateList do
                                        td [] [
                                            let dayPriceForDate =
                                                List.filter
                                                    (fun (x: Domain.DayPrice) ->
                                                        x.PriceSheetDate.DifferenceInDays(date) = 0)
                                                    sitePriceSheets

                                            if (dayPriceForDate.IsEmpty) then
                                                Heading.p [ Heading.Modifiers [ Modifier.TextAlignment(
                                                                                    Screen.All,
                                                                                    TextAlignment.Centered
                                                                                )
                                                                                Modifier.TextColor IsBlack
                                                                                Modifier.TextSize(
                                                                                    Screen.All,
                                                                                    TextSize.Is5
                                                                                ) ] ] [
                                                    str "-"
                                                ]
                                            else
                                                    str (
                                                        (List.filter
                                                            (fun (x: Domain.DayPrice) ->
                                                                x.PriceSheetDate.DifferenceInDays(date) < 1)
                                                            sitePriceSheets)
                                                            .Head.Price.Head.Price.ToString()
                                                    )
                                        ]
                                ]
                        ]
                    ]
                ]
        ]
    ]

let view (model: Model) (dispatch: Msg -> unit) =
    Hero.hero [ Hero.Color IsPrimary
                Hero.IsFullHeight
                Hero.Props [ Style [ Background(
                                         sprintf
                                             """linear-gradient(rgba(0, 0, 0, 0.5), rgba(0, 0, 0, 0.5)), url("%s") no-repeat center center fixed"""
                                             pickBackground
                                     )
                                     BackgroundSize "cover" ] ] ] [
        Hero.head [] [
            Navbar.navbar [ Navbar.Color IsInfo ] [
                Navbar.Brand.div [] [
                    Navbar.Item.a [ Navbar.Item.Props [ Href "#" ] ] [
                        img [ Src "/favicon.png" ]
                    ]
                ]
                Navbar.Item.a [ Navbar.Item.HasDropdown
                                Navbar.Item.IsHoverable ] [
                    Navbar.Link.a [] [ str "Docs" ]
                    Navbar.Dropdown.div [ Navbar.Dropdown.Modifiers [ Modifier.TextColor IsBlack ] ] [
                        Navbar.Item.a [] [ str "Overwiew" ]
                        Navbar.Item.a [] [ str "Elements" ]
                        Navbar.divider [] []
                        Navbar.Item.a [] [ str "Components" ]
                    ]
                ]
                Navbar.End.div [] [
                    Navbar.Item.div [] [
                        Button.button [ Button.Color IsSuccess ] [
                            str "Demo"
                        ]
                    ]
                ]
            ]
        ]

        Hero.body [] [
            Container.container [] [
                Column.column [ Column.Width(Screen.All, Column.Is6)
                                Column.Offset(Screen.All, Column.Is3) ] [
                    Heading.h2 [ Heading.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [
                        str "GrainContracker"
                    ]
                    containerBox model dispatch
                ]
            ]
        ]
    ]
