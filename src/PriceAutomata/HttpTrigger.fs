namespace Company.Function

open FSharp.CosmosDb
open FSharp.Control
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging

module HttpTrigger =
    open GrainContracker.Common
    open Shared.Domain

    [<Literal>]
    let Database_Name = "Contracker_primary"

    [<Literal>]
    let Container_Name = "Prices"

    [<Literal>]
    let EnvVariable_ConnStringsPrefix = "ConnectionStrings"

    [<Literal>]
    let EnvVariable_CosmosDbConnString = "CosmosDbConnectionString"

    let cosmosConnString =
        System.Environment.GetEnvironmentVariable
            (EnvVariable_ConnStringsPrefix
             + ":"
             + EnvVariable_CosmosDbConnString)

    [<FunctionName("HttpTrigger")>]
    let run ([<HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)>] req: HttpRequest) (log: ILogger) =
        async {
            log.LogInformation("F# HTTP trigger function processed a request.")
            let pdfString = PdfHelper.readSamplePdfPig

            let prices =
                PdfParser.GrainCorpBarleyParser pdfString

            let op: Azure.Cosmos.CosmosClientOptions = Azure.Cosmos.CosmosClientOptions()

            op.Serializer <- Shared.Serializer.CompactCosmosSerializer()

            printfn "%s" cosmosConnString

            let cosmosConnection =
                Cosmos.fromConnectionStringWithOptions cosmosConnString op
                |> Cosmos.database Database_Name
                |> Cosmos.container Container_Name

            let insertSitePrices =
                cosmosConnection
                |> Cosmos.insertMany<DayPrice> prices
                |> Cosmos.execAsync

            let sitePrices = insertSitePrices
            do! sitePrices
                |> AsyncSeq.iter (fun u -> printfn "%A" u)

            return OkObjectResult(prices) :> IActionResult
        }
        |> Async.StartAsTask
