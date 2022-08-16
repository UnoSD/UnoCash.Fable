module UnoCash.Core.AccountWriter

open UnoCash.Dto
open UnoCash.Core.Table

let writeAsync upn (account : Account) =
    writeAsync upn account noPartitionKey (fun (a : Account) -> a.Id.ToString())