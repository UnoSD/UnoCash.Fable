module UnoCash.Fulma.Models

open Fable
open System
open Microsoft.FSharp.Reflection
open UnoCash.Fulma.Helpers
open UnoCash.Fulma.Config

type Expense =
    JsonProvider.Generator<"GetExpenses.json">

type Account =
    JsonProvider.Generator<"GetAccounts.json">

type Tab =
    | AddExpense
    | SplitExpense
    | EditExpense
    | ShowExpenses
    | ShowStatistics
    | Settings
    | About

type AlertType =
    | NoAlert
    | DuplicateTag

type Receipt =
    {
        Payee : string option
        Date : DateTime option
        Method : string option
        Amount : float option
    }

type ExpenseModel =
    {
        Amount : decimal
        Tags : string list
        Date : DateTime
        Payee : string
        Account : string
        Status : string
        Type : string
        Description : string
    }

type ReceiptAnalysisStatus =
    | NotStarted
    | InProgress
    | Completed

type ReceiptAnalysis =
    {
        Status : ReceiptAnalysisStatus
    }
    
// Duplicate in Currency.fs, share the code
type CurrencyExchangeData =
    {
        Date : string
        Rate : float
    }

type StatisticsTimeRange =
    | Last7Days  
    | Last30Days 
    | Last365Days
    | AllRange
    
let toTimeRangeDisplayString timeRange =
    match timeRange with
    | Last7Days   -> "Last 7 days"
    | Last30Days  -> "Last 30 days"
    | Last365Days -> "Last 365 days"
    | AllRange    -> "All range"

type Model =
    {
        ApiBaseUrl : string
        CurrentTab : Tab
        TagsText : string
        Alert : AlertType
        Expenses : Expense[]
        SelectedFile : string option
        ExpensesLoaded : bool
        Expense : ExpenseModel
        ShowAccount : string
        SelectedExpenseId : string
        Accounts : string list
        ReceiptAnalysis : ReceiptAnalysis
        PieChartIndex : int
        
        AccountName : string
        
        GbpToEurData : CurrencyExchangeData list
        StatisticsSelectedTimeRange : StatisticsTimeRange
    }
    
let emptyModel = 
    {
        ApiBaseUrl = ""
        CurrentTab = AddExpense
        TagsText = ""
        Alert = NoAlert
        Expenses = [||]
        SelectedFile = Option.None
        ExpensesLoaded = false
        ShowAccount = initialAccount
        SelectedExpenseId = String.Empty
        Accounts = accounts
        ReceiptAnalysis = { Status = NotStarted }
        PieChartIndex = 0
        AccountName = ""
        // CurrencyExchangeData : (Currency, Currency, CurrencyExchangeData)[]
        //                        Map<Currency, Map<Currency, CurrencyExchangeData>>
        GbpToEurData = []
        StatisticsSelectedTimeRange = AllRange
        Expense =
        {
            Date = DateTime.Today
            Tags = []
            Amount = 0m
            Payee = String.Empty
            Account = initialAccount
            Status = "New"
            Type = "Regular"
            Description = String.Empty
        }
    }

type ExpenseType =
    | New
    | Pending
    | Scheduled

type ExpenseStatus =
    | Regular
    | InternalTransfer
    | Scheduled

let inline enumerateCases<'a> =
    FSharpType.GetUnionCases typeof<'a> |>
    Array.map (fun case -> pascalCaseToDisplay case.Name)

let expenseTypes =
    enumerateCases<ExpenseType>
    
let expenseStatus =
    enumerateCases<ExpenseStatus>