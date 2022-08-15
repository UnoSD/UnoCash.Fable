module UnoCash.Api.AddAccount

open UnoCash.Core
open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http

[<FunctionName("AddAccount")>]
let run ([<HttpTrigger(AuthorizationLevel.Anonymous, "post")>]req: HttpRequest) =
    // Put Account Guid on DTO and validate account
    AddDto.add req AccountWriter.writeAsync