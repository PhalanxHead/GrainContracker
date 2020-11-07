namespace Shared


module Configuration =
    module Constants =
        [<Literal>]
        let FinalTimerString = "0 30 2,7,22 * * *"

        [<Literal>]
        let TestingTimerString = "0 0/1 * * * *"

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

        [<Literal>]
        let DefaultLoggingLayout =
            "${longdate}|${event-properties:item=EventId_Id:whenEmpty=0}|${uppercase:${level}}| ${logger} | ${message} ${exception:format=tostring} | ${ndlc:topFrames=3}"

    module Logging =
        open NLog
        open NLog.FSharp
        open NLog.Extensions.Logging

        let getLogger (azureILogger: Microsoft.Extensions.Logging.ILogger) (loggerName: string) (configString) =

            let loggerTarget = new MicrosoftILoggerTarget(azureILogger)

            loggerTarget.Layout <- Layouts.Layout.FromString(Constants.DefaultLoggingLayout)

            let nlogConfig = NLogLoggingConfiguration(configString)
            nlogConfig.AddRuleForAllLevels(loggerTarget, "*")
            LogManager.Configuration <- nlogConfig

            Logger(loggerName)
