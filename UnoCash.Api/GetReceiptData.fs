module UnoCash.Api.GetReceiptData

open Microsoft.Azure.Functions.Worker
open Microsoft.Azure.Functions.Worker.Http
open UnoCash.Core
open UnoCash.Api.HttpRequest
open UnoCash.Api.Function

// Constrain UPN also for requests such as GetReceipt* as a
// cross-user request could happen even if all calls are authenticated

[<Function("GetReceiptData")>]
let run ([<HttpTrigger(AuthorizationLevel.Function, "get")>]req: HttpRequestData) =
    result {
        let! blobName =
            {
                Key      = "blobName"
                Value    = Ok
                Empty    = Error [ "Empty blob name" ]
                Missing  = Error [ "Missing blob name" ]
                Multiple = Error [ "Multiple blob names not supported" ] |> ignoreSnd
            } |> getQueryStringResult req.Url.Query 
        
        return ReceiptParser.parseAsync blobName
    } |>
    runAsync req