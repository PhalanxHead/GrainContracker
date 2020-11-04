namespace Shared

open Hopac
open Logary
open Logary.Message
open Logary.Configuration
open Logary.Targets

module Configuration =
    module Logging =

        let getLogger (hostName: string) (loggerName: string) =
            let logary =
                Config.create hostName "Azure Funcs"
                |> Config.target (LiterateConsole.create LiterateConsole.empty "litconsole")
                |> Config.target (Console.create Console.empty "console")
                |> Config.loggerMinLevel ".*" LogLevel.Debug
                |> Config.ilogger (ILogger.Console Debug)
                |> Config.build
                |> run

            logary.getLogger loggerName

    module Constants =
        [<Literal>]
        let Database_Name = "Contracker_primary"

        [<Literal>]
        let Container_Name = "Prices"

        [<Literal>]
        let BlobPriceSheetContainerName = "pricesheets"

        [<Literal>]
        let EnvVariable_ConnStringsPrefix = "ConnectionStrings"

        [<Literal>]
        let EnvVariable_CosmosDbConnString = "CosmosDbConnectionString"

        [<Literal>]
        let EnvVariable_AzStorageConnString = "AzureStorage"
