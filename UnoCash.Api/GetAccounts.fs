module UnoCash.Api.GetAccounts

open UnoCash.Core
open Microsoft.Azure.WebJobs
open UnoCash.Api.Function
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http

[<FunctionName("GetAccounts")>]
let run ([<HttpTrigger(AuthorizationLevel.Anonymous, "get")>]req: HttpRequest) =
    result {
        let! upn = 
            JwtToken.tryGetUpn req.Cookies
        and! _ =
            Result<unit, string>.Ok ()
        
        return AccountReader.getAll upn
    } |>
    runAsync