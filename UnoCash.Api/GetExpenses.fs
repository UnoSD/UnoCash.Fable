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

            let account =
                match req.Query |> HttpRequest.tryGetValues "account" with
                | Some [| value |] -> value
                | Some _           -> failwith "Multiple accounts not supported"
                | _                -> failwith "Missing account name"

            log.LogInformation("Get expenses for account: {account}", account)
                
            let upn =
                match req.Cookies.TryGetValue "jwtToken" with
                | Value t -> JwtToken.getClaim "upn" t |>
                             Option.defaultWith (fun () -> failwith "Missing upn claim")                      
                | _       -> failwith "Missing jwtToken cookie"
            
            log.LogInformation("Get expenses for upn: {upn}", upn)
            
            let guid =
                match req.Query |> HttpRequest.tryGetValues "id" with
                | Some [| value |] -> match Guid.TryParse(value) with
                                      | Value x -> Some x
                                      | _       -> failwith "Invalid expense guid"
                | Some _           -> failwith "Multiple ids not supported"
                | _                -> None

            log.LogInformation("Get expense for id: {guid}", guid)
            
            let! resultTask =
                match guid with
                | Some idGuid -> ExpenseReader.GetAsync(account, upn, idGuid)
                | None        -> ExpenseReader.GetAllAsync(account, upn)
            
            return resultTask |> OkObjectResult :> IActionResult
        } |>
        Async.StartAsTask