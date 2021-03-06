[<AutoOpen>]
module UnoCash.Api.Core

open System

let ignoreSnd y _ =
    y
    
let tryParseGuid (value : string) =
    match Guid.TryParse(value) with
    | Value x -> Some x |> Ok
    | _       -> Error "Invalid guid"
    
let tryParseRequiredGuid =
    tryParseGuid >>
    Result.bind (function
                 | Some guid -> Ok guid
                 | None      -> Error "Invalid guid")