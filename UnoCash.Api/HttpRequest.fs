module UnoCash.Api.HttpRequest

open Microsoft.AspNetCore.Http

let tryGetValues key (query : IQueryCollection) =
    match query.TryGetValue key with
    | Value a -> a.ToArray() |> Some
    | _       -> None
    
let tryGetSingleValue key (query : IQueryCollection) =
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