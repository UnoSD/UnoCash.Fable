module UnoCash.Api.AddExpenses

open System.IO
open Microsoft.Azure.Functions.Worker
open Microsoft.Azure.Functions.Worker.Http
open UnoCash.Core
open UnoCash.Dto
open UnoCash.Api.Function

[<Function("AddExpense")>]
let run ([<HttpTrigger(AuthorizationLevel.Function, "post")>]req: HttpRequestData) =
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
    runFlattenAsync req