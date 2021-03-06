module UnoCash.Fulma.Export

open System
open Fable.Core
open UnoCash.Fulma.Models

let exportExpenses expenses =
    let row (expense : Expense) =
        let date =
            DateTime.Parse(expense.date).ToString("MM-dd-yyyy")
        
        let allTags =
            expense.tags.Split(',')
        
        let sharedTag =
            allTags |>
            Array.tryFind (fun s -> s.StartsWith("Shared")) |>
            Option.defaultValue ""
            
        let category =
            allTags |>
            Array.filter (fun s -> s.StartsWith("c:")) |>
            Array.map (fun s -> s.Substring(2)) |>
            Array.tryExactlyOne |>
            Option.defaultValue ""
            
        let decimalAmount =
            Convert.ToDecimal(expense.amount)
            
        sprintf "%s;0;;%s;%s;-%M;%s;%s" date expense.payee expense.description decimalAmount category sharedTag
    
    promise {
        let encodedContent =
            expenses |>
            Array.map row |>
            Array.reduce (fun l r -> l + "\n" + r) |>
            sprintf "data:text/plain;charset=utf-8,%s" |>
            JS.encodeURI
            
        let anchor = Browser.Dom.document.createElement "a"
        anchor.setAttribute("href", encodedContent)
        anchor.setAttribute("download", "export.csv")
        anchor.click()
    }