module UnoCash.Api.Json

open Newtonsoft.Json

let tryParse<'a> text =
    let mutable success = true
    
    let settings =
        JsonSerializerSettings(
            Error = (fun _ args -> args.ErrorContext.Handled <- true; success <- false),
            MissingMemberHandling = MissingMemberHandling.Error
        )
    
    let result =
        JsonConvert.DeserializeObject<'a>(text, settings)
    
    match success with
    | true  -> Some result
    | false -> None