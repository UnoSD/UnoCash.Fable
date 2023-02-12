module UnoCash.Fulma.Updates

open System
open Elmish
open UnoCash.Fulma.Models
open UnoCash.Fulma.Messages
open UnoCash.Fulma.Helpers
open UnoCash.Fulma.Config
open UnoCash.Fulma.Upload
open UnoCash.Fulma.Export
open Fetch
open Fable.Core
open Fable.SimpleJson

let private loadConfig () =
    fetch "/apibaseurl" [] |>
    Promise.bind (fun x -> x.text())

let init _ =
    emptyModel, Cmd.OfPromise.perform loadConfig () SetApiBaseUrl

let private loadAccounts apiBaseUrl =
    promise {
        let url =
            apiBaseUrl |>
            getAccountsUrl
        
        let! response =
            fetch url [ Credentials RequestCredentials.Include ]
        
        let! text =
            response.text()
        
        return text |> Account.ParseArray
    }

let private loadAccountsCmd apiBaseUrl =
    Cmd.OfPromise.perform loadAccounts apiBaseUrl ShowAccountsLoaded

let private loadExchangeRates apiBaseUrl =
    promise {
        let url =
            apiBaseUrl |>
            getExchangeRatesUrl |>
            (fun url -> $"{url}?from=GBP&to=EUR")
        
        let! response =
            fetch url [ Credentials RequestCredentials.Include ]
        
        let! text =
            response.text()
        
        return text
               |> SimpleJson.parse
               |> SimpleJson.mapKeys (fun key -> $"{Char.ToUpper(key[0])}{key[1..]}")
               |> Json.convertFromJsonAs<CurrencyExchangeData list>
    }

let private loadExchangeRatesCmd apiBaseUrl =
    Cmd.OfPromise.perform loadExchangeRates apiBaseUrl ExchangeRatesLoaded

let private loadExpenses (account, apiBaseUrl) =
    promise {
        let url =
            apiBaseUrl |>
            getExpensesUrl
        
        let urlWithAccount =
            sprintf "%s?account=%s" url account
        
        let! response =
            fetch urlWithAccount [ Credentials RequestCredentials.Include ]
        
        let! text =
            response.text()
        
        return text |> Expense.ParseArray
    }

let private loadExpensesCmd account apiBaseUrl =
    Cmd.OfPromise.perform loadExpenses (account, apiBaseUrl) ShowExpensesLoaded

let private addExpense (model : ExpenseModel, apiBaseUrl) =
    let dateString =
        model.Date.ToString "O"
        
    let tagsCsv =
        model.Tags |> String.concat ","
        
    let newId =
        Guid.NewGuid() |> string
        
    let jsonComposer = sprintf """{
    "date": "%s",
    "payee": "%s",
    "amount": %f,
    "status": "%s",
    "type": "%s",
    "description": "%s",
    "account": "%s",
    "tags": "%s",
    "id": "%s"
}"""
   
    let body =
        jsonComposer dateString
                     model.Payee
                     model.Amount
                     model.Status
                     model.Type
                     model.Description
                     model.Account
                     tagsCsv
                     newId
    
    fetch (addExpenseUrl apiBaseUrl)
          [ Credentials RequestCredentials.Include
            Method HttpMethod.POST
            Body <| U3.Case3 body ]

let private removeExpense (id, account, apiBaseUrl) =
    fetch (sprintf "%s?id=%s&account=%s" (deleteExpenseUrl apiBaseUrl) id account)
          [ Method HttpMethod.DELETE
            Credentials RequestCredentials.Include ]
    
let private toModel (expense : Expense) =
    {
        Amount = decimal expense.amount
        Tags = expense.tags.Split(',') |> List.ofArray
        Date = DateTime.Parse expense.date
        Payee = expense.payee
        Account = expense.account
        Status = expense.status
        Type = expense.``type``
        Description = expense.description
    }

let private changeTabTo tab model =
    match tab with
    | ShowExpenses
    | ShowStatistics -> { model with CurrentTab = tab }, Cmd.batch [ loadExpensesCmd model.ShowAccount model.ApiBaseUrl
                                                                     loadAccountsCmd model.ApiBaseUrl
                                                                     loadExchangeRatesCmd model.ApiBaseUrl ]
    | AddExpense     -> { model with ApiBaseUrl = model.ApiBaseUrl; CurrentTab = tab }, Cmd.none
    | _              -> { model with CurrentTab = tab }, Cmd.none

let private addTagOnEnter key tag model =
    match key with
    | "Enter" -> { model with Expense = { model.Expense with Tags = tag :: model.Expense.Tags |> List.distinct }
                              TagsText = String.Empty
                              Alert = match model.Expense.Tags |> List.exists ((=)tag) with
                                      | true  -> DuplicateTag
                                      | false -> NoAlert
                 }, Cmd.none
    | _       -> model, Cmd.none

let private withoutTag tagName expense =
    { expense with Tags = expense.Tags |> List.except [ tagName ] }

let addExpenseCmd expense apiBaseUrl =
    Cmd.OfPromise.perform addExpense (expense, apiBaseUrl) (fun _ -> ChangeToTab AddExpense)

let removeExpenseCmd expId account apiBaseUrl =
    Cmd.OfPromise.perform removeExpense (expId, account, apiBaseUrl) (fun _ -> ChangeToTab ShowExpenses)

let fileUploadCmd blob name length apiBaseUrl =
    Cmd.OfPromise.perform fileUpload (blob, name, length, apiBaseUrl) (fun blobName -> ReceiptUploaded blobName)

let receiptParseCmd blobName apiBaseUrl =
    Cmd.OfPromise.perform receiptParse (blobName, apiBaseUrl) (fun result -> ShowParsedExpense result)

let expensesExportCmd expenses =
    Cmd.OfPromise.perform exportExpenses expenses (fun _ -> ChangeToTab ShowExpenses)

let private addAccount apiBaseUrl name =
    let body = name
    
    promise {
        let! response =
            fetch (addAccountUrl apiBaseUrl)
                  [ Credentials RequestCredentials.Include
                    Method HttpMethod.POST
                    Body <| U3.Case3 body ]
                  
        return response.Ok
    }

let update message model =
    match message with
    | SetApiBaseUrl apiHost  -> { model with ApiBaseUrl = apiHost }, Cmd.none
    
    | ChangeToTab newTab     -> changeTabTo newTab model
                                
    | TagsKeyDown (key, tag) -> addTagOnEnter key tag model
    
    | TagsTextChanged text   -> { model with TagsText = text }, Cmd.none
    
    | TagDelete tagName      -> { model with Expense = model.Expense |> withoutTag tagName }, Cmd.none
                             
    | ShowExpensesLoaded exs -> { model with Expenses = exs
                                             ExpensesLoaded = true }, Cmd.none
    
    | ShowAccountsLoaded acs -> { model with Accounts = acs |> Array.map (fun a -> a.name) |> List.ofArray }, Cmd.none
    
    | ExchangeRatesLoaded er -> { model with GbpToEurData = er }, Cmd.none
    
    | FileSelected fileName  -> { model with SelectedFile = match fileName with
                                                            | "" | null -> Option.None
                                                            | fileName  -> Some fileName }, Cmd.none
    | FileUpload (b, n, l)   -> { model with ReceiptAnalysis = { model.ReceiptAnalysis with Status = InProgress } },
                                fileUploadCmd b n l model.ApiBaseUrl
    | ReceiptUploaded blob   -> model, receiptParseCmd blob model.ApiBaseUrl
    | ShowParsedExpense exp  -> { model with Expense = exp
                                             ReceiptAnalysis = { model.ReceiptAnalysis with Status = Completed } },
                                Cmd.none
    
    | AddNewExpense          -> { emptyModel with ApiBaseUrl = model.ApiBaseUrl }, addExpenseCmd model.Expense model.ApiBaseUrl
                             
    | ChangePayee text       -> { model with Expense = { model.Expense with Payee = text } }, Cmd.none
    | ChangeDate newDate     -> { model with Expense = { model.Expense with Date = DateTime.Parse(newDate) } },
                                Cmd.none
    | ChangeAmount newValue  -> { model with Expense = { model.Expense with Amount = toDecimal newValue 2 } },
                                Cmd.none
    | ChangeAccount text     -> { model with Expense = { model.Expense with Account = text } }, Cmd.none
    | ChangeStatus text      -> { model with Expense = { model.Expense with Status = text } }, Cmd.none
    | ChangeType text        -> { model with Expense = { model.Expense with Type = text } }, Cmd.none
    | ChangeDescription txt  -> { model with Expense = { model.Expense with Description = txt } }, Cmd.none
                             
    | ChangeShowAccount acc  -> match model.ShowAccount = acc with
                                | true  -> model, Cmd.none
                                | false -> { model with ShowAccount = acc }, loadExpensesCmd acc model.ApiBaseUrl
                                
    | DeleteExpense expId    -> model, removeExpenseCmd expId model.ShowAccount model.ApiBaseUrl
    | EditExpense expense    -> { model with Expense = expense |> toModel
                                             CurrentTab = Tab.EditExpense }, Cmd.none
    
    | ChangePieChartIndex ix -> { model with PieChartIndex = ix }, Cmd.none
    
    | ExportExpenses         -> model, expensesExportCmd model.Expenses
    
    | AccountNameChanged txt -> { model with AccountName = txt }, Cmd.none
    | AddAccount name        -> model, Cmd.OfPromise.perform (addAccount model.ApiBaseUrl) name (fun _ -> AccountAdded name)
    | AccountAdded name      -> { model with Accounts = name :: model.Accounts; AccountName = "" }, Cmd.none