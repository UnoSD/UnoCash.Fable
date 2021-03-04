module UnoCash.Api.ExpenseRequest

open UnoCash.Api.HttpRequest

let tryGetAccount query =
    {
        Key      = "account"
        Value    = Ok
        Empty    = Error "Missing account name"
        Missing  = Error "Missing account name"
        Multiple = Error "Multiple accounts not supported" |> ignoreSnd
    } |> getQueryStringResult query