module Experimental

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open UnoCash.Api

type Task with
    static member map f (t : Task<'a>) =
        t.AsAsync() |> Async.map f |> Async.StartAsTask
        
    static member bind f (t : Task<'a>) =
        async.Bind(t.AsAsync(), f >> Async.AwaitTask) |> Async.StartAsTask

type AzureFunctionBuilder() =
    member __.Bind(v : 'a option, f : 'a -> (Task<IActionResult> * 'b)) =
        match v with
        | Some x -> f x
        | None   -> (BadRequestObjectResult("Option was None") :>
                     IActionResult |>
                     Task.FromResult,
                     Unchecked.defaultof<'b>)
        
    member __.Bind(v : Result<'a, string>, f : 'a -> (Task<IActionResult> * 'b)) =
        match v with
        | Ok x    -> f x
        | Error x -> (BadRequestObjectResult(x) :>
                      IActionResult |>
                      Task.FromResult,
                      Unchecked.defaultof<'b>)
        
    member __.Bind(v : Task<'a>, f : 'a -> (Task<IActionResult> * 'b)) =
        (Task.bind (f >> fst) v, Unchecked.defaultof<'b>)

    member __.Bind(v : Async<'a>, f : 'a -> (Task<IActionResult> * 'b)) =
        async.Bind(v, (f >> fst >> Async.AwaitTask)) |> Async.StartAsTask, Unchecked.defaultof<'b>
    
    member __.Run(v : Task<IActionResult>, _) = v

    member __.Return (v : #IActionResult          ) = (v :> IActionResult |> Task.FromResult                            , ())
    member __.Return (v : (unit -> #IActionResult)) = (v() :> IActionResult |> Task.FromResult                          , ())
    member __.Return (v : Task<#IActionResult>    ) = (v |> Task.map (fun x -> x :> IActionResult)                      , ())
    member __.Return (v : Async<#IActionResult>   ) = (v |> Async.map (fun x -> x :> IActionResult) |> Async.StartAsTask, ())
        
let azureFunction = AzureFunctionBuilder()
let azureFunction' (_ : HttpRequest) = AzureFunctionBuilder() // Set the request and use for custom operations such as setCookie

let cookie key (req: HttpRequest) =
    HttpRequest.tryGetCookie key req.Cookies 

let run ()(*(req: HttpRequest)*) =
    azureFunction {
        let! v1 = Some 1
        //let! myCookie = cookie "upn" req
        //and! myQs = query "account" req
        
        let! v2 = Task.FromResult 2
        let! v3 = Task.FromResult 3
        
        let! v4 = Some 4
        //setCookie "" "" req
        
        let! v5 = async.Return 5
        
        let! v6 = Ok 6
        
        let res = 
            [ v1; v2; v3; v4; v5; v6 ] |>
            OkObjectResult
        
        return res
    }

let run'    = azureFunction { return OkResult() |> Task.FromResult }
let run''   = azureFunction { return OkResult() }
let run'''  = azureFunction { return OkResult }
let run'''' = azureFunction { return async.Return(OkResult()) }

[<EntryPoint>]
let main _ =
    (match (run()).Result with
     | :? OkObjectResult as r -> r.Value
     | :? BadRequestObjectResult as b -> b.Value
     | _ -> failwith "Unsupported") |>
    printfn "%A"
    
    0