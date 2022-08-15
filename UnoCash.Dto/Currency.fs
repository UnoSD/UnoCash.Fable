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
    
// let embedded =
//     [
//         {
//             Code               = "USD"
//             Symbol             = '$'
//             SymbolPosition     = CurrencySymbolPosition.Pre
//             Name               = "Dollar"
//             DecimalSeparator   = '.'
//             ThousandsSeparator = ','
//             DecimalDigits      = 2
//             Id                 = Guid.Empty
//         }        
//     ]
