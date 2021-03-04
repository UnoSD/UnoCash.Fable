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
    
let runAsync' result =
    async.Bind(result, (Result.map (async.Return)) >>
                       runAsync >>
                       (fun x -> x.AsAsync())) |>
    Async.StartAsTask
    
let toActionResultWithError falseMessage (boolTask : Task<bool>) =
    async {
        let! success = boolTask
        
        let result =
            match success with
            | true  -> OkResult() :> IActionResult
            | false -> [ falseMessage ] |> BadRequestObjectResult :> IActionResult
            
        return result
    }
    
let runAsync'' result =
    result |>
    Result.mapError (box >> BadRequestObjectResult >> unbox<IActionResult> >> async.Return) |>
    (function | Ok x | Error x -> x) |>
    Async.StartAsTask