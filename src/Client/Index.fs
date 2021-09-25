module Index

open Elmish
open Fable.Remoting.Client
open Thoth.Fetch
open Shared
open Shared.SampleData
open System

type Model =
    {

      /// <summary>
      /// True if we are waiting for day prices to load
      /// </summary>
      LoadingDayPrices: bool
      /// <summary>
      /// True if we are waiting for site names to load
      /// </summary>
      LoadingSiteNames: bool
      /// <summary>
      /// List of sites we have data stored for - Note: Currently not used
      /// </summary>
      SitesWithLoadedData: Domain.Site list
      /// <summary>
      /// The list of all the site names we know about, and if they're being displayed or not.
      /// Plan is to check if a site has already been loaded before requesting again
      /// </summary>
      KnownSiteNamesPlusDisplayStatus: (Domain.Site * bool) list
      /// <summary>
      /// The list of DayPrice records that have been loaded
      /// </summary>
      LoadedDayPrices: Domain.DayPrice list }


type Msg =
    | GettingDayPrices of Domain.Site list
    | GotDayPrices of Domain.DayPrice list
    | GotSiteNames of Domain.Site list

let grainContrackerApi =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<IGrainConTrackerApi>


// Load the site names from the BE, and then load the dayprices for the selected (read First) checkbox in dropdown
let init () : Model * Cmd<Msg> =
    let model =
        { LoadingDayPrices = false
          LoadingSiteNames = true
          KnownSiteNamesPlusDisplayStatus = []
          SitesWithLoadedData = []
          LoadedDayPrices = [] }

    let cmd =
        Cmd.OfAsync.perform grainContrackerApi.GetSiteList () GotSiteNames

    model, cmd

/// <summary>
/// Given a set of site names with their current display status, give a list of all the sites that are being displayed
/// </summary>
/// <param name="sites"></param>
/// <returns></returns>
let KnownSelectedSiteNames (sites: (Domain.Site * bool) list) =
    (sites)
    |> List.filter (fun x -> snd x)
    |> List.map (fun x -> fst x)

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | GotSiteNames siteNames ->
        // Select the first site name in the list
        let allSiteNamesUnselected = List.map (fun x -> x, false) siteNames

        let firstSelectedSiteNames =
            (allSiteNamesUnselected.Head |> fst, true)
            :: List.tail allSiteNamesUnselected

        { model with
              LoadingSiteNames = false
              LoadingDayPrices = true
              SitesWithLoadedData = []
              KnownSiteNamesPlusDisplayStatus = firstSelectedSiteNames },
        // Go get the list of DayPrices for all selected sites
        // Note that there's a bug where the first site will always be selected
        Cmd.OfAsync.perform
            grainContrackerApi.GetDayPricesBySite
            (KnownSelectedSiteNames firstSelectedSiteNames)
            GotDayPrices
    | GettingDayPrices siteNames ->
        // Select the first site name in the list
        let allSiteNamesUnselected = List.map (fun x -> x, false) siteNames

        let firstSelectedSiteNames =
            (allSiteNamesUnselected.Head |> fst, true)
            :: List.tail allSiteNamesUnselected

        { model with
              LoadingDayPrices = true
              SitesWithLoadedData = siteNames @ model.SitesWithLoadedData },
        // Go get the list of DayPrices for all selected sites
        // Note that there's a bug where the first site will always be selected
        Cmd.OfAsync.perform
            grainContrackerApi.GetDayPricesBySite
            (KnownSelectedSiteNames firstSelectedSiteNames)
            GotDayPrices
    | GotDayPrices dayPrices ->
        { model with
              LoadedDayPrices = dayPrices
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
open Fable.FontAwesome
open Fulma.Extensions.Wikiki



let tableContainer (model: Model) (dispatch: Msg -> unit) =
    Box.box' [] [

        Content.content [] [
            Dropdown.dropdown [ if not model.LoadingSiteNames then
                                    Dropdown.IsHoverable ] [
                Dropdown.trigger [] [
                    Button.button [] [
                        span [] [ str "Select Sites" ]
                        Icon.icon [ Icon.Size IsSmall ] [
                            if model.LoadingSiteNames then
                                Fa.i [ Fa.Solid.Spinner; Fa.Pulse ] []
                            else
                                Fa.i [ Fa.Solid.AngleDown ] []
                        ]
                    ]
                ]
                Dropdown.menu [] [
                    Dropdown.content [] [
                        // Create dropdown items for each siteName in the model, and tick them if they are selected
                        for site, siteSelected in model.KnownSiteNamesPlusDisplayStatus do
                            Dropdown.Item.div [] [
                                div [] [
                                    match site with
                                    | Domain.Site siteName ->
                                        Checkradio.checkbox [ Checkradio.IsCircle
                                                              Checkradio.Checked siteSelected
                                                              Checkradio.Id(sprintf "%s" (siteName)) ] [
                                            str (sprintf " %s" (siteName))
                                        ]
                                ]
                            ]
                    ]
                ]
            ]
            // Show a loading bar if awaiting data
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
            // Show the data in a table, where the headers are the price sheet dates, the first column is the site name, and the data is the price on that day
            else
                div [ Class "table-container" ] [
                    let sheetDates =
                        List.sortDescending (
                            List.distinct (
                                List.map (fun (x: Domain.DayPrice) -> x.PriceSheetDate) model.LoadedDayPrices
                            )
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
                            for siteprice in
                                (List.groupBy (fun (x: Domain.DayPrice) -> x.Site) model.LoadedDayPrices
                                 |> List.sortBy (fun x -> fst x)) do
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

                                            // If there's no price for the date, show a '-'
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
                                            // Otherwise show the first listed price
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
                    tableContainer model dispatch
                ]
            ]
        ]
    ]
