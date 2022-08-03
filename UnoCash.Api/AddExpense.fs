module UnoCash.Api.AddExpenses

open System.IO
open UnoCash.Core
open UnoCash.Dto
open UnoCash.Api.Function
open Microsoft.Azure.WebJobs
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http

[<FunctionName("AddExpense")>]
let run ([<HttpTrigger(AuthorizationLevel.Anonymous, "post")>]req: HttpRequest) =
    use reader = new StreamReader(req.Body)

    result {
        let! upn = 
            JwtToken.tryGetUpn req.Cookies
        and! expense =            
            Json.tryResult<Expense> (reader.ReadToEnd())
                
        return ExpenseWriter.writeAsync upn expense |> 
               mapHttpResult (ignoreSnd "" >> Ok)
                             (sprintf "Error %i occurred while writing the expense" >> Error)
    } |>
    runFlattenAsync 