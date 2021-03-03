module UnoCash.Api.GetExpenses

open UnoCash.Core
open Microsoft.Azure.WebJobs
open UnoCash.Api.HttpRequest
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Azure.WebJobs.Extensions.Http

[<FunctionName("GetExpenses")>]
let run ([<HttpTrigger(AuthorizationLevel.Function, "get")>]req: HttpRequest) (log: ILogger) =
    async {
        log.LogInformation("Get expenses called")
        
        let upnResult =
            JwtToken.tryGetUpn req.Cookies
        
        log.LogInformation("Get expenses for upn: {upn}", upnResult)
        
        let accountResult =
            {
                Key      = "account"
                Value    = Ok
                Empty    = Error "Missing account name"
                Missing  = Error "Missing account name"
                Multiple = Error "Multiple accounts not supported" |> ignoreSnd
            } |> getQueryStringResult req.Query
        
        log.LogInformation("Get expenses for account: {account}", accountResult)
        
        let guidResult =
            {
                Key      = "id"
                Value    = tryParseGuid
                Empty    = Error "Empty id value"
                Missing  = Ok None
                Multiple = Error "Multiple ids not supported" |> ignoreSnd
            } |> getQueryStringResult req.Query

        log.LogInformation("Get expense for id: {guid}", guidResult)

        let result =
            result {
                let! account = accountResult
                and! upn = upnResult
                and! guidResult = guidResult 
                
                return match guidResult with
                       | Some guid -> ExpenseReader.GetAsync(account, upn, guid) |> Async.AwaitTask
                       | None      -> ExpenseReader.GetAllAsync(account, upn)    |> Async.AwaitTask
            }
        
        return! match result with
                | Ok expensesAsync -> expensesAsync |> Async.map (OkObjectResult >> unbox)
                | Error errors     -> errors |> BadRequestObjectResult :> IActionResult |> async.Return
    } |>
    Async.StartAsTask