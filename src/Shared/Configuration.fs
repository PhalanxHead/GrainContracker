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
                |> Config.target (LiterateConsole.create LiterateConsole.empty "console")
                |> Config.loggerMinLevel ".*" LogLevel.Debug
                |> Config.ilogger (ILogger.Console Debug)
                |> Config.build
                |> run

            logary.getLogger loggerName