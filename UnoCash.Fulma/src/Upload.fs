module UnoCash.Fulma.Upload

open Fable.Core
open Fetch
open UnoCash.Fulma.Config
open UnoCash.Fulma.Models
open Thoth.Json
open System

let receiptParse (blobName, apiBaseUrl) =
    promise {
        let url =
            sprintf "%s?blobName=%s" (getReceiptDataUrl apiBaseUrl) blobName
            
        let! response =
            fetch url []
        
        let! json =
            response.text()
            
        let receiptData =
            match Decode.Auto.fromString<Receipt>(json, caseStrategy=CamelCase) with
            | Ok r    -> r
            | Error e -> printfn "Error parsing receipt json: %s" e
                         {
                             Method = None
                             Payee = None
                             Amount = None
                             Date = None
                         }
        
        return {
            Date = Option.defaultValue DateTime.Today receiptData.Date
            Tags = []
            Amount = Option.defaultValue 0. receiptData.Amount |> decimal
            Payee = Option.defaultValue String.Empty receiptData.Payee
            Account = initialAccount
            Status = "New"
            Type = "Regular"
            Description = Option.defaultValue String.Empty receiptData.Method
        }
    }


let fileUpload (blob, name, contentLength, apiBaseUrl) =
    promise {
        let! response =
            fetch (receiptUploadSasTokenUrl apiBaseUrl) []
        
        let! url =
            response.text()
        
        let! _ =
            fetch url
                  [ Method HttpMethod.PUT
                    Credentials RequestCredentials.Include
                    requestHeaders [ HttpRequestHeaders.Custom ("x-ms-content-length", contentLength)
                                     HttpRequestHeaders.Custom ("x-ms-blob-content-disposition", sprintf "attachment; filename=\"%s\"" name)
                                     HttpRequestHeaders.Custom ("x-ms-blob-type", "BlockBlob") ]
                    Body <| U3.Case1 blob ]
        
        return name
    } 
