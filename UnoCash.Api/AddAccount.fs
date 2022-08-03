module UnoCash.Api.AddAccount

open System.IO
open UnoCash.Core
open UnoCash.Dto
open UnoCash.Api.Function
open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http

let private validateAccountName name =
    Result<string, string>.Ok name

[<FunctionName("AddAccount")>]
let run ([<HttpTrigger(AuthorizationLevel.Anonymous, "post")>]req: HttpRequest) =
    use reader = new StreamReader(req.Body)

    result {
        let! upn = 
            JwtToken.tryGetUpn req.Cookies
        and! accountName =            
            validateAccountName (reader.ReadToEnd())
                
        return AccountWriter.writeAsync upn accountName |> 
               mapHttpResult (ignoreSnd "" >> Ok)
                             (sprintf "Error %i occurred while writing the account" >> Error)
    } |>
    runFlattenAsync 