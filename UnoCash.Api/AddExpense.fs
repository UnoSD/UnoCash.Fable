module UnoCash.Api.AddExpenses

open System.IO
open UnoCash.Core
open UnoCash.Api.Function
open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http

[<FunctionName("AddExpense")>]
let run ([<HttpTrigger(AuthorizationLevel.Function, "post")>]req: HttpRequest) =
    use reader = new StreamReader(req.Body)

    result {
        let! upn = 
            JwtToken.tryGetUpn req.Cookies
        and! expense =            
            Json.tryResult<Expense> (reader.ReadToEnd())
        
        return ExpenseWriter.WriteAsync(expense, upn) |>
               toActionResultWithError "Error occurred while writing the expense"
    } |>
    runAsync''