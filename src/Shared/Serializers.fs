namespace Shared

module Serializer  = 

    open Azure.Cosmos.Serialization
    open Microsoft.FSharpLu.Json
    open Newtonsoft.Json
    open System.Text
    open System.IO

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


