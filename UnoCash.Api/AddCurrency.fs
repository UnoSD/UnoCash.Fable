module UnoCash.Api.AddCurrency

open UnoCash.Core
open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http

[<FunctionName("AddCurrency")>]
let run ([<HttpTrigger(AuthorizationLevel.Anonymous, "post")>]req: HttpRequest) =
    AddDto.add req CurrencyWriter.writeAsync