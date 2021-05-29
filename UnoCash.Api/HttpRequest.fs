module UnoCash.Api.HttpRequest

open System
open Microsoft.Azure.Functions.Worker.Http

let getQueryStringMap (query : string) =
    query.Substring(1).Split('&') |>
    Array.filter (fun x -> not <| String.IsNullOrEmpty(x)) |>
    Array.map (fun x -> match x.Split('=') with
                        | [| k     |]
                        | [| k; "" |] -> (k, None)
                        | [| k; v  |] -> (k, Some v)
                        | a           -> (Array.head a, Some <| String.Join('=', Array.tail a))) |>
    Map.ofArray

let tryGetValues key (query : string) =
    match getQueryStringMap query |> Map.tryFind key with
    | Some (Some a) -> Some a |> Option.map Array.singleton
    | _             -> None
    
let tryGetSingleValue key (query : string) =
    tryGetValues key query |>
    Option.bind (function
                 | [| value |] -> Some value
                 | _           -> None)
    
type QueryStringValidationSettings<'a, 'b> =
    {
        Key: string
        Value: 'a -> 'b
        Empty: 'b
        Multiple: 'a array -> 'b
        Missing: 'b
    }

let getQueryStringResult query config =
    match query |> tryGetValues config.Key with
    | Some [| "" |]    -> config.Empty
    | Some [| value |] -> config.Value value
    | Some values      -> config.Multiple values
    | _                -> config.Missing
    
let tryGetCookie key cookies =
    Seq.tryFind (fun (c : IHttpCookie) -> c.Name = key) cookies |>
    Option.map (fun c -> c.Value)