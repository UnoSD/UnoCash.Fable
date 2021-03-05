module UnoCash.Core.ExpenseWriter

open System
open UnoCash.Dto
open FSharp.Azure.Storage.Table
open Microsoft.Azure.Cosmos.Table
open UnoCash.Core.AzureTableHelpers

let writeAsync upn expense =
    let getSetting name =
        Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)                |> Option.ofObj  |>
        Option.orElse (Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)    |> Option.ofObj) |>
        Option.orElse (Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine) |> Option.ofObj) |>
        Option.defaultWith (fun () -> failwith "")
    
    let csa = getSetting "StorageAccountConnectionString" |> CloudStorageAccount.Parse
    let client = csa.CreateCloudTableClient()
    let inExpensesTable = inTableAsync client (nameof Expense)

    // Possibly this needs to be set once per type at the start of the app rather than every time        
    EntityIdentiferReader.GetIdentifier <- (fun x -> { PartitionKey = partitionKey upn x.Account; RowKey = x.Id.ToString() })

    expense |>
    Insert |>
    inExpensesTable
    
let deleteAsync upn account (id : Guid) =
    let getSetting name =
        Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)                |> Option.ofObj  |>
        Option.orElse (Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)    |> Option.ofObj) |>
        Option.orElse (Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine) |> Option.ofObj) |>
        Option.defaultWith (fun () -> failwith "")
    
    let csa = getSetting "StorageAccountConnectionString" |> CloudStorageAccount.Parse
    let client = csa.CreateCloudTableClient()
    let inExpensesTable = inTableAsync client (nameof Expense)

    { EntityIdentifier.PartitionKey = partitionKey upn account; RowKey = id.ToString() } |>
    ForceDelete |>
    inExpensesTable