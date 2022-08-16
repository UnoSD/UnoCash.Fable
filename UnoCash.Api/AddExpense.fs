module UnoCash.Api.AddExpenses

open Microsoft.Extensions.Logging
open UnoCash.Core
open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http

[<FunctionName("AddExpense")>]
let run ([<HttpTrigger(AuthorizationLevel.Anonymous, "post")>]req: HttpRequest, logger : ILogger) =
    // Validate Currency
    AddDto.add req (ExpenseWriter.writeAsync (fun exn msg args -> logger.LogError(exn, msg, args)))