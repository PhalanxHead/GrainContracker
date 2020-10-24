namespace Company.Function

open System
open System.IO
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.AspNetCore.Http
open Newtonsoft.Json
open Microsoft.Extensions.Logging
open FSharp.CosmosDb
open FSharp.Control
open System.Text.Json
open System.Text.Json.Serialization
open Azure.Cosmos.Serialization
open System.Text
open Microsoft.FSharpLu.Json
open Shared.Domain


module Serializer =

    let JsonFSharpCon =
        JsonFSharpConverter
            (unionEncoding =
                (JsonUnionEncoding.ExternalTag
                 ||| JsonUnionEncoding.UnwrapFieldlessTags))

    let options = JsonSerializerOptions()

    JsonFSharpCon |> options.Converters.Add

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
    // open Shared.Domain

    // Define a nullable container to deserialize into.
    [<AllowNullLiteral>]
    type NameContainer() =
        member val Name = "" with get, set

    // For convenience, it's better to have a central place for the literal.
    [<Literal>]
    let Name = "name"

    [<FunctionName("HttpTrigger")>]
    let run ([<HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)>] req: HttpRequest) (log: ILogger) =
        async {
            log.LogInformation("F# HTTP trigger function processed a request.")

            let nameOpt =
                if req.Query.ContainsKey(Name) then Some(req.Query.[Name].[0]) else None

            use stream = new StreamReader(req.Body)
            let! reqBody = stream.ReadToEndAsync() |> Async.AwaitTask

            let data =
                JsonConvert.DeserializeObject<NameContainer>(reqBody)

            let name =
                match nameOpt with
                | Some n -> n
                | None ->
                    match data with
                    | null -> ""
                    | nc -> nc.Name

            let pdfString = PdfHelper.readSamplePdfPig

            let responseMessage =
                PdfParser.GrainCorpBarleyParser pdfString
            // if (String.IsNullOrWhiteSpace(name)) then
            // "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
            // else
            // "Hello, " +  name + ". This HTTP triggered function executed successfully."


            let connString =
                System.Environment.GetEnvironmentVariable("ConnectionStrings:" + "CosmosDbConnectionString")

            printfn "%s" connString

            let op: Azure.Cosmos.CosmosClientOptions = Azure.Cosmos.CosmosClientOptions()

            op.Serializer <- Serializer.CompactCosmosSerializer()

            let insertPriceSheet =
                Cosmos.fromConnectionStringWithOptions connString op
                |> Cosmos.database "Contracker_primary"
                |> Cosmos.container "Prices"
                |> Cosmos.insertMany<SitePrice> responseMessage.Prices
                |> Cosmos.execAsync

            let users = insertPriceSheet

            do! users |> AsyncSeq.iter (fun u -> printfn "%A" u)

            return OkObjectResult(responseMessage) :> IActionResult
        }
        |> Async.StartAsTask
