module UnoCash.Core.ExpenseTable

open UnoCash.Core.Table
open UnoCash.Dto

let private getPartitionKey expense =
    expense.Account

let private getRowKey expense =
    expense.Id.ToString()

let inExpensesTable upn =
    inEntityTable upn getPartitionKey getRowKey

let fromExpensesTable upn =
    fromTable upn getPartitionKey getRowKey