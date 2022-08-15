module UnoCash.Core.AccountWriter

open FSharp.Azure.Storage.Table
open UnoCash.Dto

let writeAsync upn (account : Account) =
    Insert account |>
    Table.inEntityTable upn (fun (_ : Account) -> "") (fun a -> a.Id.ToString())