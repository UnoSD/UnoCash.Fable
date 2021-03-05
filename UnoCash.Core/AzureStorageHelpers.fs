module UnoCash.Core.AzureStorageHelpers

open Microsoft.Azure.Cosmos.Table

let cloudStorageAccount =
    lazy(
        Configuration.tryGetSetting Configuration.Keys.StorageAccountConnectionString |>
        Option.defaultWith (fun _ -> failwith "Missing connection string in configuration") |>
        CloudStorageAccount.Parse
    )