module UnoCash.Core.ExpenseWriter

open UnoCash.Core.Table
open UnoCash.Dto
open System
open FSharp.Azure.Storage.Table
open UnoCash.Core.ExpenseTable

let writeAsync upn (expense : Expense) =
    writeAsync upn expense getPartitionKey getRowKey
    
let deleteAsync upn account (id : Guid) =
    {
        PartitionKey = partitionKey upn account
        RowKey = id.ToString()
    } |>
    ForceDelete |>
    inExpensesTable upn