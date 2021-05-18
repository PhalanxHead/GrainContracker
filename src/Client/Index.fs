module Index

open Elmish
open Fable.Remoting.Client
open Thoth.Fetch
open Shared
open Shared.SampleData

type Model = { Todos: Todo list; Input: string; DayPrices_All: Domain.DayPrice list }

type Msg =
    | GotTodos of Todo list
    | SetInput of string
    | AddTodo
    | AddedTodo of Todo

let todosApi =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<IGrainConTrackerApi>

let init (): Model * Cmd<Msg> =
    let model = { Todos = []; Input = ""; DayPrices_All = List.sortBy (fun x -> x.Site, x.PriceSheetDate) (SampleData.SampleDayPrices_All()) }

    let cmd =
        Cmd.OfAsync.perform todosApi.getTodos () GotTodos

    model, cmd

let update (msg: Msg) (model: Model): Model * Cmd<Msg> =
    match msg with
    | GotTodos todos -> { model with Todos = todos }, Cmd.none
    | SetInput value -> { model with Input = value }, Cmd.none
    | AddTodo ->
        let todo = Todo.create model.Input

        let cmd =
            Cmd.OfAsync.perform todosApi.addTodo todo AddedTodo

        { model with Input = "" }, cmd
    | AddedTodo todo ->
        { model with
              Todos = model.Todos @ [ todo ] },
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

let containerBox (model: Model) (dispatch: Msg -> unit) =
    Box.box' [] [

        Content.content [] [
            (*
            Content.Ol.ol [] [
                for todo in model.Todos do
                    li [] [ str todo.Description ]
            ]
            *)
            div [Class "table-container"] [
                let sheetDates = List.distinct (List.map (fun (x:Domain.DayPrice) -> x.PriceSheetDate) model.DayPrices_All)
                Table.table [Table.IsStriped] [
                    thead [] [
                        tr [] [
                            th [] [str "Site"]
                            for sheetDate in sheetDates do
                                th [] [str (sheetDate.Format("dd/MM/yy"))]
                        ]
                    ]
                    tbody [] [
                        for siteprice in List.groupBy (fun (x:Domain.DayPrice) -> x.Site) model.DayPrices_All do
                            tr [] [
                                match (fst siteprice) with
                                | Domain.Site site -> td [] [str site]

                                let sitePriceSheets = snd siteprice
                                for date in sitePriceSheets do 
                                    td [] [str (date.Price.Head.Price.ToString())]
                            ]
                    ]
                ]
            ]
        ]
        (*
        Field.div [ Field.IsGrouped ] [
            Control.p [ Control.IsExpanded ] [
                Input.text [ Input.Value model.Input
                             Input.Placeholder "What needs to be done?"
                             Input.OnChange(fun x -> SetInput x.Value |> dispatch) ]
            ]
            Control.p [] [
                Button.a [ Button.Color IsPrimary
                           Button.Disabled(Todo.isValid model.Input |> not)
                           Button.OnClick(fun _ -> dispatch AddTodo) ] [
                    str "Add"
                ]
            ]
        ]
        *)
    ]

let view (model: Model) (dispatch: Msg -> unit) =
    Hero.hero [ Hero.Color IsPrimary
                Hero.IsFullHeight
                Hero.Props [ Style [ Background
                                         (sprintf
                                             """linear-gradient(rgba(0, 0, 0, 0.5), rgba(0, 0, 0, 0.5)), url("%s") no-repeat center center fixed"""
                                              pickBackground)
                                     BackgroundSize "cover" ] ] ] [
        Hero.head [] [
            (*
            Navbar.navbar [] [
                Container.container [] [ navBrand ]
            ]
            *)
            Navbar.navbar [ Navbar.Color IsInfo ] [
                Navbar.Brand.div [] [
                    Navbar.Item.a [ Navbar.Item.Props [ Href "#" ] ] [
                        img [ Src "/favicon.png" ]
                    ]
                ]
                Navbar.Item.a [ Navbar.Item.HasDropdown
                                Navbar.Item.IsHoverable ] [
                    Navbar.Link.a [] [ str "Docs" ]
                    Navbar.Dropdown.div [] [
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
                    Heading.p [ Heading.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [
                        str "GrainContracker"
                    ]
                    containerBox model dispatch
                ]
            ]
        ]
    ]
