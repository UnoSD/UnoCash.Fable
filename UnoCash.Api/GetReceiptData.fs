module UnoCash.Api.GetReceiptData

open UnoCash.Core
open Microsoft.Azure.WebJobs
open UnoCash.Api.HttpRequest
open UnoCash.Api.Function
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http

// Constrain UPN also for requests such as GetReceipt* as a
// cross-user request could happen even if all calls are authenticated

[<FunctionName("GetReceiptData")>]
let run ([<HttpTrigger(AuthorizationLevel.Function, "get")>]req: HttpRequest) =
    result {
        let! blobName =
            {
                Key      = "blobName"
                Value    = Ok
                Empty    = Error [ "Empty blob name" ]
                Missing  = Error [ "Missing blob name" ]
                Multiple = Error [ "Multiple blob names not supported" ] |> ignoreSnd
            } |> getQueryStringResult req.Query 
        
        return ReceiptParser.ParseAsync blobName |> Async.AwaitTask
    } |>
    runAsync