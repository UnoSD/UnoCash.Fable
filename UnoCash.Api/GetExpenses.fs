namespace UnoCash.Api

open System
open UnoCash.Core
open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Azure.WebJobs.Extensions.Http

module GetExpenses =
    [<FunctionName("GetExpenses")>]
    let run ([<HttpTrigger(AuthorizationLevel.Function, "get")>]req: HttpRequest) (log: ILogger) =
        async {
            log.LogInformation("Get expenses called")
            
            let accountResult =
                match req.Query |> HttpRequest.tryGetValues "account" with
                | Some [| "" |]    -> Error "Missing account name"
                | Some [| value |] -> Ok value
                | Some _           -> Error "Multiple accounts not supported"
                | _                -> Error "Missing account name"

            log.LogInformation("Get expenses for account: {account}", accountResult)
                
            let upnResult =
                match req.Cookies.TryGetValue "jwtToken" with
                | Value t -> JwtToken.getClaim "upn" t |>
                             Option.map Ok |>
                             Option.defaultValue (Error "Missing upn claim")                      
                | _       -> Error "Missing jwtToken cookie"
            
            log.LogInformation("Get expenses for upn: {upn}", upnResult)
            
            let guidResult =
                match req.Query |> HttpRequest.tryGetValues "id" with
                | Some [| value |] -> match Guid.TryParse(value) with
                                      | Value x -> Some x |> Ok
                                      | _       -> Error "Invalid expense guid"
                | Some _           -> Error "Multiple ids not supported"
                | _                -> Ok None

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