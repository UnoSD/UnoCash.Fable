module UnoCash.Api.AddExpenses

open System.IO
open UnoCash.Core
open Microsoft.Azure.WebJobs
open UnoCash.Api.Function
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http

[<FunctionName("AddExpense")>]
let run ([<HttpTrigger(AuthorizationLevel.Function, "post")>]req: HttpRequest) =
    use reader = new StreamReader(req.Body)
    
    result {
        let! upn = 
            JwtToken.tryGetUpn req.Cookies
        and! expense =            
            match Json.tryParse<Expense> (reader.ReadToEnd()) with
            | Some e -> Ok e
            | None   -> Error "Unable to parse expense JSON body"
        
        return ExpenseWriter.WriteAsync(expense, upn) |> Async.AwaitTask
    } |>
    runAsync