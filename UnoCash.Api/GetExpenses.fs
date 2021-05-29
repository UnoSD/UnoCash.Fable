module UnoCash.Api.GetExpenses

open Microsoft.Azure.Functions.Worker
open Microsoft.Azure.Functions.Worker.Http
open UnoCash.Core
open UnoCash.Api.HttpRequest
open UnoCash.Api.Function

let getCookiesFromHeaders (headers : HttpHeadersCollection) =
    [ for c in headers do
          if c.Key = "Cookie" then
              yield HttpCookie(c.Value |> Seq.head |> (fun x -> x.Split('=').[0]),
                               c.Value |> Seq.head |> (fun x -> x.Split('=').[1])) |> unbox<IHttpCookie> ]

[<Function("GetExpenses")>]
let run ([<HttpTrigger(AuthorizationLevel.Function, "get")>]req: HttpRequestData) =
    result {
        //req.Identities
        let! upn = 
            JwtToken.tryGetUpn (getCookiesFromHeaders req.Headers) // req.Cookies not working, it's empty
        and! account =
            ExpenseRequest.tryGetAccount req.Url.Query
        and! guidOption =
            {
                Key      = "id"
                Value    = tryParseGuid
                Empty    = Error "Empty id value"
                Missing  = Ok None
                Multiple = Error "Multiple ids not supported" |> ignoreSnd
            } |> getQueryStringResult req.Url.Query
        
        let expensesAsync =
            match guidOption with
            | Some guid -> ExpenseReader.get upn account guid
            | None      -> ExpenseReader.getAll upn account
        
        return expensesAsync
    } |>
    runAsync req