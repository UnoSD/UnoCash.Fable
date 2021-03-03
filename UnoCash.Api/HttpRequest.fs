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