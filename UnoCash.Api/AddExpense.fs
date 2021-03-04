module UnoCash.Api.AddExpenses

open System.IO
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open UnoCash.Core
open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http

let writeExpense expense upn =
    async {
        let! success = ExpenseWriter.WriteAsync(expense, upn)
        
        let result =
            match success with
            | true  -> OkResult() :> IActionResult
            | false -> [ "Error occurred while writing the expense" ] |> BadRequestObjectResult :> IActionResult
            
        return result
    }

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
    | Ok (upn, expense)  -> writeExpense expense upn |> Async.StartAsTask
    | Error errors       -> errors |> BadRequestObjectResult :> IActionResult |> Task.FromResult