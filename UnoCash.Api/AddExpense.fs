module UnoCash.Api.AddExpenses

open System.IO
open Newtonsoft.Json
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
        let expense =
            JsonConvert.DeserializeObject<Expense>(reader.ReadToEnd())
        
        return ExpenseWriter.WriteAsync(expense, upn) |> Async.AwaitTask
    } |>
    Result.mapError List.singleton |>
    runAsync