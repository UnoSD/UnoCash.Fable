module UnoCash.Core.ExpenseReader

open UnoCash.Dto
open Microsoft.Azure.Documents
open FSharp.Azure.Storage.Table
open UnoCash.Core.Table
open UnoCash.Core.ExpenseTable
open XhbProvider
open System

let private getAll' upn account filter =
    Query.all<Expense> |>
    Query.where <@ fun _ s -> s.PartitionKey = partitionKey upn account @> |>
    filter |>
    fromExpensesTable upn

let private toUnoCashExpense (accounts : Map<int, string>) (payees : Map<int, string>) (ope : Xhb.Ope) =
    {
        Amount      = float ope.Amount 
        Payee       = ope.Payee |> Option.bind (fun p -> Map.tryFind p payees) |> Option.defaultValue ""
        Description = ope.Wording |> Option.defaultValue ""
        Date        = DateTime(1, 1, 1, 0, 0, 0).AddDays(ope.Date)
        Account     = Map.tryFind ope.Account accounts |> Option.defaultValue ""
        Type        = "Regular"
        Status      = match ope.St with | Some 2 -> "Reconciled" | _ -> "New"
        Tags        = ope.Tags |> Option.defaultValue ""
        Id          = Guid.Empty
    }

let private getAllFromXhb account path =
    async {
        let! hb = Xhb.AsyncLoad(path)
        
        let payees =
            hb.Pays
            |> Array.map (fun p -> p.Key, p.Name)
            |> Map.ofArray
        
        let accounts =
            hb.Accounts
            |> Array.map (fun p -> p.Key, p.Name)
            |> Map.ofArray
        
        let accountId =
            hb.Accounts
            |> Array.filter (fun a -> a.Name = account)
            |> Array.tryExactlyOne
            |> Option.map (fun a -> a.Key)
            |> Option.defaultValue 0
        
        return hb.Opes
               |> Array.filter (fun ope -> ope.Account = accountId)
               |> Array.map (toUnoCashExpense accounts payees)
               |> Seq.ofArray
    }

let private getFromXhb account guid path =
    async {
        let! expenses = getAllFromXhb account path
        
        return expenses |> Seq.filter (fun e -> e.Id = guid)
    }

let getAll upn account =
    Configuration.tryGetSetting Configuration.Keys.XhbFilePath
    |> Option.map (getAllFromXhb account)
    |> Option.defaultWith (fun () -> getAll' upn account id)
    
let get upn account guid =
    Configuration.tryGetSetting Configuration.Keys.XhbFilePath
    |> Option.map (getFromXhb account guid)
    |> Option.defaultWith (fun () -> getAll' upn account <|
                                     Query.where <@ fun g _ -> g.Id = guid @>)