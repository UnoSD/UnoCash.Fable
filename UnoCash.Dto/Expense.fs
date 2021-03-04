namespace UnoCash.Dto

open System

type Expense =
    {
        Amount      : decimal 
        Payee       : string  
        Description : string  
        Date        : DateTime
        Account     : string  
        Type        : string  
        Status      : string  
        Tags        : string  
        Id          : Guid    
    }