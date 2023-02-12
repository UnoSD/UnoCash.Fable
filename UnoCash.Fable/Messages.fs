module UnoCash.Fulma.Messages

open Browser.Types
open UnoCash.Fulma.Models

type Message =
    | ChangeToTab of Tab
    | ChangeAmount of string
    | ChangePayee of string
    | TagsKeyDown of string * string
    | TagsTextChanged of string
    | TagDelete of string
    | ChangeDate of string
    | ShowAccountsLoaded of Account[]
    | ShowExpensesLoaded of Expense[]
    | ExchangeRatesLoaded of CurrencyExchangeData list
    | FileSelected of string
    | FileUpload of (Blob * string * int)
    | ReceiptUploaded of string
    | ShowParsedExpense of ExpenseModel
    | AddNewExpense
    | ChangeAccount of string
    | ChangeStatus of string
    | ChangeType of string
    | ChangeShowAccount of string
    | ChangeDescription of string
    | DeleteExpense of string
    | EditExpense of Expense
    | SetApiBaseUrl of string
    // We need to split the app as this is growing
    | ChangePieChartIndex of int
    | ExportExpenses
    
    | AccountNameChanged of string
    | AddAccount of string
    | AccountAdded of string