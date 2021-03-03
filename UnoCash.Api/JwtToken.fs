module UnoCash.Api.JwtToken

open System.IdentityModel.Tokens.Jwt

let tryGetClaim type' token =
    JwtSecurityTokenHandler().ReadJwtToken(token).Claims |>
    Seq.filter (fun c -> c.Type = type') |>
    Seq.tryExactlyOne |>
    Option.map (fun u -> u.Value)