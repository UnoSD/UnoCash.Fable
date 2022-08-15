module UnoCash.Core.AccountWriter

open UnoCash.Dto
open UnoCash.Core.Table

let writeAsync upn account =
    Table.writeAsync upn account noPartitionKey (fun a -> a.Id.ToString())