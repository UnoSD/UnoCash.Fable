module UnoCash.Api.Function

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