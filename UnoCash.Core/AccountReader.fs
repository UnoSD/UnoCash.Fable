module UnoCash.Core.AccountReader

open System
open UnoCash.Dto
open Microsoft.Azure.Documents
open FSharp.Azure.Storage.Table
open UnoCash.Core.Table
open XhbProvider

let private getAllFromAzureTables upn =
    Query.all<Account> |>
    Query.where <@ fun _ s -> s.PartitionKey = partitionKey upn String.Empty @> |>
    fromTable upn noPartitionKey (fun (x : Account) -> x.Id.ToString())

let private getAllFromXhb path =
    let toGuid id =
        Guid.Parse($"00000000-0000-0000-0000-{id:d12}")
    
    let toUnoCashAccount (xhbAccount : Xhb.Account) =
        { CurrencyId     = toGuid xhbAccount.Curr
          Name           = xhbAccount.Name
          InitialBalance = float xhbAccount.Initial
          Id             = toGuid xhbAccount.Key }
    
    async {
        let! hb = Xhb.AsyncLoad path
        
        return hb.Accounts |> Seq.map toUnoCashAccount
    }

let getAll upn =
    Configuration.tryGetSetting Configuration.Keys.XhbFilePath
    |> Option.map getAllFromXhb
    |> Option.defaultWith (fun () -> getAllFromAzureTables upn)