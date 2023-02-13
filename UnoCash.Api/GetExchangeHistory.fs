module UnoCash.Api.GetExchangeHistory

open System
open Microsoft.Azure.WebJobs
open UnoCash.Api.HttpRequest
open UnoCash.Api.Function
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http
open UnoCash.Dto
open UnoCash.Core

let private usd, gbp, eur =
    {
        Code               = "USD"
        Symbol             = '$'
        SymbolPosition     = CurrencySymbolPosition.Pre
        Name               = "Dollar"
        DecimalSeparator   = '.'
        ThousandsSeparator = ','
        DecimalDigits      = 4
        Id                 = Guid.Empty
    },
    {
        Code               = "GBP"
        Symbol             = '£'
        SymbolPosition     = CurrencySymbolPosition.Pre
        Name               = "Sterling"
        DecimalSeparator   = '.'
        ThousandsSeparator = ','
        DecimalDigits      = 4
        Id                 = Guid.Empty
    },
    {
        Code               = "EUR"
        Symbol             = '€'
        SymbolPosition     = CurrencySymbolPosition.Post
        Name               = "Dollar"
        DecimalSeparator   = ','
        ThousandsSeparator = '.'
        DecimalDigits      = 4
        Id                 = Guid.Empty
    }

let private tryParseCurrency (text : string) : Result<Currency option, string> =
    match text.ToUpper() with
    | "EUR" -> eur |> Some |> Ok
    | "GBP" -> gbp |> Some |> Ok
    | "USD" -> usd |> Some |> Ok
    | _     -> Error $"Unsupported currency: {text}"

let private tryParseCurrencyQueryString key query =
    {
        Key      = key
        Value    = tryParseCurrency
        Empty    = Error $"Empty {key} currency value"
        Missing  = Error $"Missing {key} currency"
        Multiple = Error $"Multiple {key} currency not supported" |> ignoreSnd
    } |> getQueryStringResult query

[<FunctionName("GetExchangeHistory")>]
let run ([<HttpTrigger(AuthorizationLevel.Anonymous, "get")>]req: HttpRequest) =
    result {
        let! fromCurrency =
            tryParseCurrencyQueryString "from" req.Query
        and! toCurrency =
            tryParseCurrencyQueryString "to" req.Query
        
        return
            match fromCurrency, toCurrency with
            | Some from, Some ``to`` -> ExchangeHistoryReader.get from ``to``
            | _                      -> Seq.empty |> async.Return // Return meaningful error instead
    } |>
    runAsync