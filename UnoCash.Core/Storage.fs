module UnoCash.Core.Storage

open Microsoft.Azure.Storage.Blob
open Microsoft.Azure.Cosmos.Table
open Microsoft.Azure

type BlobCloudStorageAccount = Storage.CloudStorageAccount
type TableCloudStorageAccount = CloudStorageAccount

let inline private cloudStorageAccount< ^a when ^a : (static member Parse : string -> ^a) > =
    Configuration.tryGetSetting Configuration.Keys.StorageAccountConnectionString |>
    Option.defaultWith (fun _ -> failwith "Missing connection string in configuration") |>
    (fun x -> (^a : (static member Parse : string -> ^a) x))

let blobClient =
    lazy cloudStorageAccount<BlobCloudStorageAccount>.CreateCloudBlobClient()
    
let tableClient =
    lazy cloudStorageAccount<TableCloudStorageAccount>.CreateCloudTableClient()
    
let getContainer =
    blobClient.Value.GetContainerReference