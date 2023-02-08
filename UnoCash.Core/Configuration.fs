module UnoCash.Core.Configuration

open System

module Keys =
    [<Literal>]
    let StorageAccountConnectionString =
        "StorageAccountConnectionString"
        
    [<Literal>]
    let XhbFilePath =
        "XhbFilePath"

let tryGetSetting name =
    Environment.GetEnvironmentVariable(name) |> Option.ofObj