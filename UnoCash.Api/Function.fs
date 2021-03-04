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
    
let mapBoolTask ifTrue ifFalse (task : Task<bool>) =
    async {
        let! success =
            task
        
        let result =
            match success with
            | true  -> ifTrue
            | false -> ifFalse
            
        return result
    }
    
let mapBoolTaskToActionResult ifTrue ifFalse =
    mapBoolTask (ifTrue :> IActionResult) (ifFalse :> IActionResult)
    
let runAsync'' result =
    result |>
    Result.mapError (box >> BadRequestObjectResult >> unbox<IActionResult> >> async.Return) |>
    (function | Ok x | Error x -> x) |>
    Async.StartAsTask