namespace UnoCash.Dto

open System

type Expense =
    {
        Amount      : float 
        Payee       : string  
        Description : string  
        Date        : DateTime
        Account     : string  
        Type        : string  
        Status      : string  
        Tags        : string  
        Id          : Guid    
    }