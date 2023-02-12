namespace UnoCash.Dto

open System

type CurrencySymbolPosition =
    | Pre
    | Post

type Currency =
    {
        Code               : string
        Symbol             : char
        SymbolPosition     : CurrencySymbolPosition
        Name               : string
        DecimalSeparator   : char
        ThousandsSeparator : char
        DecimalDigits      : int
        Id                 : Guid
    }
    
type CurrencyExchangeData =
    {
        Date : string
        Rate : float
    }
