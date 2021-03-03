namespace UnoCash.Api

open System
open System.IdentityModel.Tokens.Jwt
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
                match req.Query.TryGetValue "account" with
                | true, a when a.Count = 1 -> a.[0]
                | true, a when a.Count > 1 -> failwith "Multiple accounts not supported"
                | _                        -> failwith "Missing account name"

            log.LogInformation("Get expenses for account: {account}", account)
            
            let upn =
                match req.Cookies.TryGetValue "jwtToken" with
                | true, t -> JwtSecurityTokenHandler().ReadJwtToken(t).Claims |>
                             Seq.filter (fun c -> c.Type = "upn") |>
                             Seq.tryExactlyOne |>
                             Option.map (fun u -> u.Value) |>
                             Option.defaultWith (fun () -> failwith "Missing upn claim")                      
                | _       -> failwith "Missing jwtToken cookie"
            
            log.LogInformation("Get expenses for upn: {upn}", upn)
            
            let id =
                match req.Query.TryGetValue "id" with
                | true, a when a.Count = 1 -> Some a.[0]
                | true, a when a.Count > 1 -> failwith "Multiple ids not supported"
                | _                        -> None
            
            let idGuid =
                id |>
                Option.bind (fun id -> match Guid.TryParse(id) with | true, x -> Some x | _ -> None)

            log.LogInformation("Get expense for id: {id}", idGuid)
            
            let! resultTask =
                match idGuid with
                | Some idGuid -> ExpenseReader.GetAsync(account, upn, idGuid)
                | None        -> ExpenseReader.GetAllAsync(account, upn)
            
            return resultTask |> OkObjectResult :> IActionResult
        } |>
        Async.StartAsTask