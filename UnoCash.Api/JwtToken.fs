module UnoCash.Api.JwtToken

open System.IdentityModel.Tokens.Jwt

let tryGetClaim type' token =
    JwtSecurityTokenHandler().ReadJwtToken(token).Claims |>
    Seq.filter (fun c -> c.Type = type') |>
    Seq.tryExactlyOne |>
    Option.map (fun u -> u.Value)
    
let tryGetUpn cookies =
    result {
        let! token =
            match HttpRequest.tryGetCookie "jwtToken" cookies with
            | Some t -> Ok t
            | None   -> Error "Missing jwtToken cookie"
            
        let! claim =
            match tryGetClaim "upn" token with
            | Some c -> Ok c
            | None   -> Error "Missing upn claim"
            
        return claim 
    }