module UnoCash.Core.AzureTableHelpers

open System
open UnoCash.Dto
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

let private getTableEntityId upn (expense : Expense) =
    { 
        PartitionKey = partitionKey upn expense.Account
        RowKey       = expense.Id.ToString()
    }

let inExpensesTable<'a> upn =
    EntityIdentiferReader<Expense>.GetIdentifier <- (getTableEntityId upn)

    getTable<Expense, Operation<'a>, OperationResult> inTableAsync
    
let fromExpensesTable upn =
    EntityIdentiferReader.GetIdentifier <- (getTableEntityId upn)

    getTable<Expense, EntityQuery<Expense>, seq<Expense * EntityMetadata>> fromTableAsync >>
    Async.map (Seq.map fst)