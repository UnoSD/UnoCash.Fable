module UnoCash.Core.AzureTableHelpers

open System
open UnoCash.Dto
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
    
let getConnectionString () =
    let getSetting name =
        Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)                |> Option.ofObj  |>
        Option.orElse (Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)    |> Option.ofObj) |>
        Option.orElse (Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine) |> Option.ofObj) |>
        Option.defaultWith (fun () -> failwith "")
        
    getSetting "StorageAccountConnectionString"

let getCloudTable () =
    let csa = getConnectionString () |> CloudStorageAccount.Parse
    csa.CreateCloudTableClient()

let getTable<'a, 'b> (operation : CloudTableClient -> string -> Operation<'b> -> Async<OperationResult>) =
    operation (getCloudTable()) (typeof<'a>.Name)

let getTableEntityId upn (expense : Expense) =
    { 
        PartitionKey = partitionKey upn expense.Account
        RowKey       = expense.Id.ToString()
    }

let inExpensesTable<'a> upn =
    EntityIdentiferReader<Expense>.GetIdentifier <- (getTableEntityId upn)

    getTable<Expense, 'a> inTableAsync