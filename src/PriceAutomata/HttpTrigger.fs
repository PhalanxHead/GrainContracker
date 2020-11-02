namespace Company.Function

open FSharp.CosmosDb
open FSharp.Control
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Logary
open Logary.Message

module HttpTrigger =
    open GrainContracker.Common
    open Shared.Domain

    type RecordId = { id: string }

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

    let GCBarleyLogger =
        Shared.Configuration.Logging.getLogger "GrainContracker.PriceAutomata" "PriceAutomata.GraincorpBarley"

    [<FunctionName("HttpTrigger")>]
    let run ([<HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)>] req: HttpRequest) (log: ILogger) =
        async {
            log.LogInformation("F# HTTP trigger function processed a request.")
            let pdfString = PdfHelper.readSamplePdfPig

            let prices =
                PdfParser.GrainCorpBarleyParser pdfString

            let priceIdsString = prices |> List.map (fun p -> p.id)

            let op: Azure.Cosmos.CosmosClientOptions = Azure.Cosmos.CosmosClientOptions()

            op.Serializer <- Shared.Serializer.CompactCosmosSerializer()

            GCBarleyLogger.debug
                (eventX "Connection String: {connstring}"
                 >> setField "connstring" cosmosConnString)

            GCBarleyLogger.info
                (eventX "Found {priceCount} prices to write to the CosmosDB"
                 >> setField "priceCount" prices.Length
                 >> setField "connstring" cosmosConnString)

            GCBarleyLogger.debug
                (eventX "New Price IDs: {priceIds}"
                 >> setField "priceIds" priceIdsString)

            let cosmosConnection =
                Cosmos.fromConnectionStringWithOptions cosmosConnString op
                |> Cosmos.database Database_Name
                |> Cosmos.container Container_Name

            let getExistingSitePrices =
                cosmosConnection
                |> Cosmos.query "SELECT c.id FROM c WHERE ARRAY_CONTAINS (@newIds, c.id)"
                |> Cosmos.parameters [ "@newIds", box priceIdsString ]
                |> Cosmos.execAsync<RecordId>

            let! existingPIDS = getExistingSitePrices |> AsyncSeq.toListAsync

            GCBarleyLogger.info
                (eventX "Found {priceCount} of these prices already in the CosmosDB!"
                 >> setField "priceCount" existingPIDS.Length
                 >> setField "connstring" cosmosConnString)

            let cleanprices =
                prices
                |> List.filter (fun p -> existingPIDS |> List.contains { id = p.id } |> not)

            let insertSitePrices =
                cosmosConnection
                |> Cosmos.insertMany<DayPrice> cleanprices
                |> Cosmos.execAsync

            let mutable count = 0

            do! insertSitePrices
                |> AsyncSeq.iter (fun _ -> count <- count + 1)

            GCBarleyLogger.info
                (eventX "Wrote {priceCount} new prices to the CosmosDB"
                 >> setField "priceCount" count
                 >> setField "connstring" cosmosConnString)

            return OkObjectResult(prices) :> IActionResult
        }
        |> Async.StartAsTask
