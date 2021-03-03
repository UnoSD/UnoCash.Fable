module UnoCash.Api.GetExpenses

open System
open UnoCash.Core
open Microsoft.Azure.WebJobs
open UnoCash.Api.HttpRequest
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Azure.WebJobs.Extensions.Http

let private parseGuid (value : string) =
    match Guid.TryParse(value) with
    | Value x -> Some x |> Ok
    | _       -> Error "Invalid expense guid"

let private ignoreArg y _ =
    y

[<FunctionName("GetExpenses")>]
let run ([<HttpTrigger(AuthorizationLevel.Function, "get")>]req: HttpRequest) (log: ILogger) =
    async {
        log.LogInformation("Get expenses called")
        
        let accountResult =
            {
                Key      = "account"
                Value    = Ok
                Empty    = Error "Missing account name"
                Missing  = Error "Missing account name"
                Multiple = Error "Multiple accounts not supported" |> ignoreArg
            } |> getQueryStringResult req.Query
        
        log.LogInformation("Get expenses for account: {account}", accountResult)
            
        let upnResult =
            match req.Cookies.TryGetValue "jwtToken" with
            | Value t -> JwtToken.getClaim "upn" t |>
                         Option.map Ok |>
                         Option.defaultValue (Error "Missing upn claim")                      
            | _       -> Error "Missing jwtToken cookie"
        
        log.LogInformation("Get expenses for upn: {upn}", upnResult)
        
        let guidResult =
            {
                Key      = "id"
                Value    = parseGuid
                Empty    = Ok None
                Missing  = Ok None
                Multiple = Error "Multiple ids not supported" |> ignoreArg
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
                | Ok expensesAsync -> async {
                                          let! expenses = expensesAsync
                                          return OkObjectResult expenses :> IActionResult
                                      } 
                | Error errors     -> async { return BadRequestObjectResult errors :> IActionResult }
    } |>
    Async.StartAsTask