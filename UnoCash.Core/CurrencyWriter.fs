module UnoCash.Core.CurrencyWriter

open UnoCash.Dto
open UnoCash.Core.Table

let writeAsync upn account =
    writeAsync upn account noPartitionKey (fun a -> a.Id.ToString())