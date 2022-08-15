module UnoCash.Api.AddDto

open System.IO
open UnoCash.Api.Function
open Microsoft.AspNetCore.Http

let add (req: HttpRequest) writeAsync =
    use reader = new StreamReader(req.Body)

    result {
        let! upn = 
            JwtToken.tryGetUpn req.Cookies
        and! account =
            Json.tryResult (reader.ReadToEnd())
                
        return writeAsync upn account |> 
               mapHttpResult (ignoreSnd "" >> Ok)
                             (sprintf "Error %i occurred while writing the object" >> Error)
    } |>
    runFlattenAsync 