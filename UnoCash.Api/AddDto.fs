module UnoCash.Api.AddDto

open System.IO
open UnoCash.Api.Function
open Microsoft.AspNetCore.Http

let add<'a> (req: HttpRequest) writeAsync =
    use reader = new StreamReader(req.Body)

    result {
        let! upn = 
            JwtToken.tryGetUpn req.Cookies
        and! dto =
            Json.tryResult<'a> (reader.ReadToEnd())
                
        return writeAsync upn dto |> 
               mapHttpResult (ignoreSnd "" >> Ok)
                             (sprintf "Error %i occurred while writing the object" >> Error)
    } |>
    runFlattenAsync 