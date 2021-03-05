module UnoCash.Core.ExpenseReader

open System
open UnoCash.Dto
open Microsoft.Azure.Documents
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

let private partitionKey upn account =
    upn + account |>
    formatTableKey

let private getAll' upn account filter =
    let getSetting name =
        Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)                |> Option.ofObj  |>
        Option.orElse (Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)    |> Option.ofObj) |>
        Option.orElse (Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine) |> Option.ofObj) |>
        Option.defaultWith (fun () -> failwith "")
    
    let csa = getSetting "StorageAccountConnectionString" |> CloudStorageAccount.Parse
    let client = csa.CreateCloudTableClient()
    let fromExpensesTable = fromTableAsync client (nameof Expense)

    async {
        // Possibly this needs to be set once per type at the start of the app rather than every time
        EntityIdentiferReader.GetIdentifier <- (fun x -> { PartitionKey = partitionKey upn account; RowKey = x.Id.ToString() })
        
        let! expenses =
            Query.all<Expense> |>
            filter |>
            fromExpensesTable
        
        return expenses |> Seq.map fst
    }

let getAll upn account =
    getAll' upn account id
    
let get upn account id =
    let partitionKey = partitionKey upn account
    
    Query.where <@ fun g s -> s.PartitionKey = partitionKey && g.Id = id @> |>
    getAll' upn account