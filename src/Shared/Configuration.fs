namespace Shared

module Configuration =
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
