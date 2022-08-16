module UnoCash.Core.ExpenseWriter

open Microsoft.Azure.Cosmos.Table
open UnoCash.Core.Table
open UnoCash.Dto
open System
open FSharp.Azure.Storage.Table
open UnoCash.Core.ExpenseTable

// Use CLI_DEBUG=1 as env var to get the exception logged in the func console

let writeAsync (log : exn -> string -> obj[] -> unit) upn (expense : Expense) =
    async {
        let! result = 
            writeAsync upn expense getPartitionKey getRowKey
            |> Async.Catch
            
        let errorOperationResult =
            {
                HttpStatusCode = 500
                Etag = String.Empty
            }
        
        let formatExtendedErrorInformation (sexn : StorageException) =
            sexn.RequestInformation.ExtendedErrorInformation
            |> Option.ofObj
            |> Option.map (fun x -> x.ToString())
            |> Option.defaultValue "null"
        
        let logStorageException (sexn : StorageException) =
            log sexn
                ("Message: {Message}" +
                 "StatusCode: {HttpStatusCode}" +
                 "ExtendedInfo: {ExtendedErrorInformation}" +
                 "Code: {ErrorCode}")
                [| sexn.Message
                   sexn.RequestInformation.HttpStatusCode                   
                   formatExtendedErrorInformation sexn                   
                   sexn.RequestInformation.ErrorCode |]
        
        let logAggregateException (aexn : AggregateException) =
            match aexn.Flatten().InnerExceptions |> List.ofSeq with
            | [ :? StorageException as sexn ] -> logStorageException sexn
            | _                               -> log aexn "Error creating expense {Expense}" [| expense |]
        
        return match result with
               | Choice1Of2 operationResult                 -> operationResult
               | Choice2Of2 (:? AggregateException as aexn) -> logAggregateException aexn
                                                               errorOperationResult
               | Choice2Of2 _                               -> errorOperationResult
    }
    
let deleteAsync upn account (id : Guid) =
    {
        PartitionKey = partitionKey upn account
        RowKey = id.ToString()
    } |>
    ForceDelete |>
    inExpensesTable upn