module UnoCash.Api.GetExpenses

open UnoCash.Core
open Microsoft.Azure.WebJobs
open UnoCash.Api.HttpRequest
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http

[<FunctionName("GetExpenses")>]
let run ([<HttpTrigger(AuthorizationLevel.Function, "get")>]req: HttpRequest) =
    async {
        let response =
            result {
                let! upn = 
                    JwtToken.tryGetUpn req.Cookies
                and! account =
                    {
                        Key      = "account"
                        Value    = Ok
                        Empty    = Error "Missing account name"
                        Missing  = Error "Missing account name"
                        Multiple = Error "Multiple accounts not supported" |> ignoreSnd
                    } |> getQueryStringResult req.Query
                and! guidOption =
                    {
                        Key      = "id"
                        Value    = tryParseGuid
                        Empty    = Error "Empty id value"
                        Missing  = Ok None
                        Multiple = Error "Multiple ids not supported" |> ignoreSnd
                    } |> getQueryStringResult req.Query 
                
                let task =
                    match guidOption with
                    | Some guid -> ExpenseReader.GetAsync(account, upn, guid) |> Async.AwaitTask
                    | None      -> ExpenseReader.GetAllAsync(account, upn)    |> Async.AwaitTask
                
                return task
            } |>
            function
            | Ok expensesAsync -> expensesAsync |> Async.map (OkObjectResult >> unbox)
            | Error errors     -> errors |> BadRequestObjectResult :> IActionResult |> async.Return
        
        return! response
    } |>
    Async.StartAsTask