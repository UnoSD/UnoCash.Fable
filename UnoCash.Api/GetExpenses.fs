module UnoCash.Api.GetExpenses

open UnoCash.Core
open Microsoft.Azure.WebJobs
open UnoCash.Api.HttpRequest
open UnoCash.Api.Function
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http

[<FunctionName("GetExpenses")>]
let run ([<HttpTrigger(AuthorizationLevel.Anonymous, "get")>]req: HttpRequest) =
    result {
        let! upn = 
            JwtToken.tryGetUpn req.Cookies
        and! account =
            ExpenseRequest.tryGetAccount req.Query
        and! guidOption =
            {
                Key      = "id"
                Value    = tryParseGuid
                Empty    = Error "Empty id value"
                Missing  = Ok None
                Multiple = Error "Multiple ids not supported" |> ignoreSnd
            } |> getQueryStringResult req.Query 
        
        let expensesAsync =
            match guidOption with
            | Some guid -> ExpenseReader.get upn account guid
            | None      -> ExpenseReader.getAll upn account
        
        return expensesAsync
    } |>
    runAsync