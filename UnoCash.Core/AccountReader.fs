module UnoCash.Core.AccountReader

open System
open UnoCash.Dto
open Microsoft.Azure.Documents
open FSharp.Azure.Storage.Table
open UnoCash.Core.Table

let getAll upn =
    Query.all<Account> |>
    Query.where <@ fun _ s -> s.PartitionKey = partitionKey upn String.Empty @> |>
    fromTable upn noPartitionKey (fun (x : Account) -> x.Id.ToString())