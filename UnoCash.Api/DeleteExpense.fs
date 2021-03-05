module UnoCash.Api.DeleteExpense

open Microsoft.Azure.WebJobs
open UnoCash.Api.HttpRequest
open UnoCash.Api.Function
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http
open UnoCash.Core

[<FunctionName("DeleteExpense")>]
let run ([<HttpTrigger(AuthorizationLevel.Function, "delete")>]req: HttpRequest) =
    result {
        let! upn = 
            JwtToken.tryGetUpn req.Cookies
        and! account =
            ExpenseRequest.tryGetAccount req.Query
        and! guid =
            {
                Key      = "id"
                Value    = tryParseRequiredGuid
                Empty    = Error "Empty id value"
                Missing  = Error "Missing id value"
                Multiple = Error "Multiple ids not supported" |> ignoreSnd
            } |>
            getQueryStringResult req.Query
        
        return ExpenseWriter.deleteAsync upn account guid |> 
               mapHttpResult (sprintf "%s" >> Ok)
                             (sprintf "Error %i occurred while deleting the expense" >> Error)
    } |>
    runFlattenAsync