module UnoCash.Api.AddAccount

open UnoCash.Core
open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http
open UnoCash.Dto

[<FunctionName("AddAccount")>]
let run ([<HttpTrigger(AuthorizationLevel.Anonymous, "post")>]req: HttpRequest) =
    // Put Currency Guid on DTO and validate account
    AddDto.add<Account> req AccountWriter.writeAsync