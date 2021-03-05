module UnoCash.Core.ExpenseWriter

open System
open UnoCash.Dto
open FSharp.Azure.Storage.Table
open UnoCash.Core.AzureTableHelpers

let writeAsync upn (expense : Expense) =
    Insert expense |>
    inExpensesTable upn
    
let deleteAsync upn account (id : Guid) =
    {
        PartitionKey = partitionKey upn account
        RowKey = id.ToString()
    } |>
    ForceDelete |>
    inExpensesTable upn