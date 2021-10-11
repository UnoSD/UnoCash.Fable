module UnoCash.Core.AccountWriter

open FSharp.Azure.Storage.Table

let writeAsync upn (account : string) =
    {
        HttpStatusCode = 202
        Etag = $"{upn} {account}"
    } |> async.Return