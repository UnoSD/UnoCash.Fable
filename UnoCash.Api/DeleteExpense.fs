module UnoCash.Api.DeleteExpense

open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open UnoCash.Api.HttpRequest
open UnoCash.Api.Function
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http
open UnoCash.Core

let private tryParseRequiredGuid =
    tryParseGuid >>
    Result.bind (function
                 | Some guid -> Ok guid
                 | None      -> Error "Invalid guid")

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
        
        return ExpenseWriter.DeleteAsync(account, upn, guid) |>
               toActionResultWithError "Error occurred while deleting the expense"
    } |>
    runAsync''