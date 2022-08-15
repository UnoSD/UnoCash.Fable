module UnoCash.Core.Table

open System
open UnoCash.Core.Storage
open FSharp.Azure.Storage.Table
open Microsoft.Azure.Cosmos.Table

let private disallowedKeyFieldsChars =
    lazy([|
        '/'; '\\'; '#'; '?'
        yield! [ 0x0 .. 0x1F ] @ [ 0x7F .. 0x9F ] |> List.map Convert.ToChar
    |])

let private formatTableKey text =
    String.split disallowedKeyFieldsChars.Value text |>
    String.join

let partitionKey upn account =
    upn + account |>
    formatTableKey

let private getTable<'a, 'b, 'c> (operation : CloudTableClient -> string -> 'b -> Async<'c>) =
    operation tableClient.Value (typeof<'a>.Name)

let private getTableEntityId upn getPartition getId (entity : 'a) =
    { 
        PartitionKey = partitionKey upn (getPartition entity)
        RowKey       = getId entity
    }

let inEntityTable upn (getPartition : 'a -> string) getId =
    EntityIdentiferReader.GetIdentifier <- (getTableEntityId upn getPartition getId)

    getTable<'a, Operation<'b>, OperationResult> inTableAsync
    
let fromTable upn getPartition getId =
    EntityIdentiferReader.GetIdentifier <- (getTableEntityId upn getPartition getId)

    getTable<'a, EntityQuery<'a>, seq<'a * EntityMetadata>> fromTableAsync >>
    Async.map (Seq.map fst)