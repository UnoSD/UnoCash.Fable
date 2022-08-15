module UnoCash.Api.AddExpenses

open UnoCash.Core
open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http

[<FunctionName("AddExpense")>]
let run ([<HttpTrigger(AuthorizationLevel.Anonymous, "post")>]req: HttpRequest) =
    // Validate Currency
    AddDto.add req ExpenseWriter.writeAsync