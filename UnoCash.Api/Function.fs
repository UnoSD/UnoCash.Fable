module UnoCash.Api.Function

open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc

let runAsync (result : Result<Async<'a>,string list>) =
   async {
        let response =
            result |>
            function
            | Ok expensesAsync -> expensesAsync |> Async.map (OkObjectResult >> unbox)
            | Error errors     -> errors |> BadRequestObjectResult :> IActionResult |> async.Return
        
        return! response
    } |>
    Async.StartAsTask
    
let toActionResult falseMessage (boolTask : Task<bool>) =
    async {
        let! success = boolTask
        
        let result =
            match success with
            | true  -> OkResult() :> IActionResult
            | false -> [ falseMessage ] |> BadRequestObjectResult :> IActionResult
            
        return result
    }