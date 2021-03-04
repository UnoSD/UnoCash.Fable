module UnoCash.Api.AddExpenses

open System.IO
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
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
            match Json.tryParse<Expense> (reader.ReadToEnd()) with
            | Some e -> Ok e
            | None   -> Error "Unable to parse expense JSON body"
        
        return (upn, expense)
    } |>
    function
    | Ok (upn, expense)  -> ExpenseWriter.WriteAsync(expense, upn) |>
                            toActionResult "Error occurred while writing the expense" |>
                            Async.StartAsTask
    | Error errors       -> errors |>
                            BadRequestObjectResult :>
                            IActionResult |>
                            Task.FromResult