namespace UnoCash.Dto

open System

type Account =
    {
        CurrencyId     : Guid
        Name           : string
        InitialBalance : float
        Id             : Guid
    }