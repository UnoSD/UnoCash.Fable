module UnoCash.Core.AccountWriter

open UnoCash.Dto

let writeAsync upn account =
    Table.writeAsync upn account (fun (_ : Account) -> "") (fun a -> a.Id.ToString())