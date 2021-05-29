module UnoCash.Api.Function

open System.Net
open System.Threading.Tasks
open FSharp.Azure.Storage.Table
open Microsoft.Azure.Functions.Worker.Http

let private response code (req : HttpRequestData) body =
    async {
        let response = req.CreateResponse(code)
        do! response.WriteAsJsonAsync(body)
        return response                
    }

let runAsync req (result : Result<Async<'a>,string list>) =
   let asyncResponse =
       match result with
       | Ok expensesAsync -> expensesAsync |> Async.bind (response HttpStatusCode.OK req)
       | Error errors     -> errors |> response HttpStatusCode.BadRequest req
   
   asyncResponse |> Async.StartAsTask
    
let runAsync' req result =
    async.Bind(result, (Result.map async.Return) >>
                       runAsync req >>
                       (fun x -> x.AsAsync())) |>
    Async.StartAsTask
    
let mapHttpResult success error (resultAsync : Async<OperationResult>) =
    async {
        let! operationResult =
            resultAsync
        
        let requestResult =
            match operationResult.HttpStatusCode with
            | c when  c > 200 && c < 300 -> success operationResult.Etag
            | _                          -> error operationResult.HttpStatusCode
        
        return requestResult
    }
    
let runFlattenAsync req (result : Result<Async<Result<'a, string>>,string list>) =
    let outerResult =
        match result with
        | Ok async    -> async |> Async.map (Result.mapError List.singleton)
        | Error errors -> async.Return(Error errors)

    outerResult |>
    runAsync' req