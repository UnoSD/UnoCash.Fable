namespace UnoCash.Api

open System
open Microsoft.AspNetCore.Mvc
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open UnoCash.Core

module GetExpenses =
    [<FunctionName("GetExpenses")>]
    let run ([<HttpTrigger(AuthorizationLevel.Function, "get", Route = null)>]req: HttpRequest) (log: ILogger) =
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
            
            let toOkResult x =
                async.Bind(x |> Async.AwaitTask, fun y -> y |> OkObjectResult :> IActionResult |> async.Return)
            
            let toBadRequestResult (account, upn, id) =
                [
                    match account with | Error e -> yield e | _ -> ()
                    match upn     with | Error e -> yield e | _ -> ()
                    match id      with | Error e -> yield e | _ -> ()
                ] |>
                BadRequestObjectResult :>
                IActionResult |>
                async.Return
            
            let! resultTask =
                match accountResult, upnResult, guidResult with
                | Ok account, Ok upn , Ok (Some idGuid) -> ExpenseReader.GetAsync(account, upn, idGuid) |> toOkResult
                | Ok account, Ok upn , Ok None          -> ExpenseReader.GetAllAsync(account, upn)      |> toOkResult
                | results                               -> results                                      |> toBadRequestResult
            
            return resultTask
        } |>
        Async.StartAsTask