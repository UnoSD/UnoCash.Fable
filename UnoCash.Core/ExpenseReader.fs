module UnoCash.Core.ExpenseReader

open UnoCash.Dto
open Microsoft.Azure.Documents
open FSharp.Azure.Storage.Table
open UnoCash.Core.AzureTableHelpers

let private getAll' upn account filter =
    Query.all<Expense> |>
    Query.where <@ fun _ s -> s.PartitionKey = partitionKey upn account @> |>
    filter |>
    fromExpensesTable upn

let getAll upn account =
    getAll' upn account id
    
let get upn account guid =
    getAll' upn account <|
    Query.where <@ fun g _ -> g.Id = guid @>