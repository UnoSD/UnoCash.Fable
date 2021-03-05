module UnoCash.Core.Configuration

open System

module Keys =
    [<Literal>]
    let StorageAccountConnectionString =
        "StorageAccountConnectionString"

let tryGetSetting name =
    Environment.GetEnvironmentVariable(name) |> Option.ofObj