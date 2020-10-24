namespace Company.Function

open Azure.Cosmos.Serialization
open FSharp.CosmosDb
open FSharp.Control
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.FSharpLu.Json
open Newtonsoft.Json
open System.Text
open System.IO

module Serializer =

    type CompactCosmosSerializer() =
        inherit CosmosSerializer()

        let defaultEncoding = new UTF8Encoding(false, true)
        let jsonSerializerSettings = new JsonSerializerSettings()
        do jsonSerializerSettings.Converters <- [| CompactUnionJsonConverter(true) |]

        let serializer =
            JsonSerializer.Create(jsonSerializerSettings)

        override u.FromStream<'T>(stream: Stream): 'T =
            let returnCastedStream () = (box stream) :?> 'T

            let readStreamAndConvert () =
                use streamReader = new StreamReader(stream)
                use jsonTextReader = new JsonTextReader(streamReader)
                serializer.Deserialize<'T>(jsonTextReader)

            try
                if typeof<Stream>.IsAssignableFrom(typeof<'T>)
                then returnCastedStream ()
                else readStreamAndConvert ()
            finally
                stream.Dispose()

        override u.ToStream<'T>(input: 'T): Stream =
            let streamPayload = new MemoryStream()

            use streamWriter =
                new StreamWriter(streamPayload, encoding = defaultEncoding, bufferSize = 1024, leaveOpen = true)

            use writer = new JsonTextWriter(streamWriter)
            writer.Formatting <- Newtonsoft.Json.Formatting.None
            serializer.Serialize(writer, input)
            writer.Flush()
            streamWriter.Flush()

            streamPayload.Position <- 0L
            streamPayload :> Stream


module HttpTrigger =
    open GrainContracker.Common
    open Shared.Domain

    // For convenience, it's better to have a central place for the literal.
    [<Literal>]
    let Name = "name"

    [<FunctionName("HttpTrigger")>]
    let run ([<HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)>] req: HttpRequest) (log: ILogger) =
        async {
            log.LogInformation("F# HTTP trigger function processed a request.")
            let pdfString = PdfHelper.readSamplePdfPig

            let priceSheet, prices =
                PdfParser.GrainCorpBarleyParser pdfString

            let cosmosConnString =
                System.Environment.GetEnvironmentVariable("ConnectionStrings:" + "CosmosDbConnectionString")

            let op: Azure.Cosmos.CosmosClientOptions = Azure.Cosmos.CosmosClientOptions()

            op.Serializer <- Serializer.CompactCosmosSerializer()

            let cosmosConnection =
                Cosmos.fromConnectionStringWithOptions cosmosConnString op
                |> Cosmos.database "Contracker_primary"
                |> Cosmos.container "Prices"

            let insertPriceSheet =
                cosmosConnection
                |> Cosmos.insert<PriceSheet> priceSheet
                |> Cosmos.execAsync

            let priceSheet = insertPriceSheet
            do! priceSheet
                |> AsyncSeq.iter (fun u -> printfn "%A" u)

            let insertSitePrices =
                cosmosConnection
                |> Cosmos.insertMany<SitePrice> prices
                |> Cosmos.execAsync

            let sitePrices = insertSitePrices
            do! sitePrices
                |> AsyncSeq.iter (fun u -> printfn "%A" u)

            return OkObjectResult(priceSheet, prices) :> IActionResult
        }
        |> Async.StartAsTask
