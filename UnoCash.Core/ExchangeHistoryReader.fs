module UnoCash.Core.ExchangeHistoryReader

open System
open System.Reflection
open UnoCash.Dto
open FSharp.Data
open System.IO

let get from ``to`` =
    async {
        // Using CSV for test purposes, to replace with API
        let! csv =
            let csvPath =
                Assembly.GetExecutingAssembly().Location
                |> Path.GetDirectoryName
                |> fun x -> $"{x}/.."
                
            CsvFile.AsyncLoad $"{csvPath}/{from.Code}-{``to``.Code}.csv"
        
        return
            csv.Rows
            |> Seq.map (fun row -> { Date = row["Date"].AsDateTime().ToString("yyyy-MM-dd")
                                     Rate = Double.Parse(row["Price"]) })
    }