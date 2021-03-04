namespace UnoCash.Dto

open System

type Receipt =
    {
        Payee  : string  
        Date   : DateTime
        Method : string  
        Amount : decimal 
    }