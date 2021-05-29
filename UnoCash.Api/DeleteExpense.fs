module UnoCash.Api.DeleteExpense

open Microsoft.Azure.Functions.Worker
open Microsoft.Azure.Functions.Worker.Http
open UnoCash.Api.HttpRequest
open UnoCash.Api.Function
open UnoCash.Core

[<Function("DeleteExpense")>]
let run ([<HttpTrigger(AuthorizationLevel.Function, "delete")>]req: HttpRequestData) =
    result {
        let! upn = 
            JwtToken.tryGetUpn req.Cookies
        and! account =
            ExpenseRequest.tryGetAccount req.Url.Query
        and! guid =
            {
                Key      = "id"
                Value    = tryParseRequiredGuid
                Empty    = Error "Empty id value"
                Missing  = Error "Missing id value"
                Multiple = Error "Multiple ids not supported" |> ignoreSnd
            } |>
            getQueryStringResult req.Url.Query
        
        return ExpenseWriter.deleteAsync upn account guid |> 
               mapHttpResult (sprintf "%s" >> Ok)
                             (sprintf "Error %i occurred while deleting the expense" >> Error)
    } |>
    runFlattenAsync req