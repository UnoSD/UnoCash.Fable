module UnoCash.Core.ReceiptParser

open System
open System.Collections.Generic
open System.IO
open System.Text.Json
open System.Threading.Tasks
open Azure
open Azure.AI.FormRecognizer
open UnoCash.Dto
open UnoCash.Core.Storage
open UnoCash.Core.UnoCashFormField
open Microsoft.Azure.Storage.Blob

let changeTypeOrDefault (field : UnoCashFormField) (default' : 'a) =
    try
        Convert.ChangeType(field.ValueData.Text, typeof<'a>) :?> 'a
    with
    | _ -> default'

let getOrDefault key (dict : IReadOnlyDictionary<string, UnoCashFormField>) (default' : 'a) : 'a =
    match dict.TryGetValue key with
    | Value v -> changeTypeOrDefault v default'
    | _       -> default'

let getReceiptDataFromCache (streamTask : Task<Stream>) =
    async {
        use! stream = 
            streamTask
        
        printfn "Found receipt analysis in cache"
        
        let! fields =
            JsonSerializer.DeserializeAsync<Dictionary<string, UnoCashFormField>>(stream)
        
        return {
            Payee  = getOrDefault "MerchantName" fields ""
            Date   = getOrDefault "TransactionDate" fields DateTime.Today
            Method = "Cash"
            Amount = getOrDefault "Total" fields 0m
        }
    }

let analyseReceipt (container : CloudBlobContainer) blobName =
    async {
        printfn "Analysing receipt"
        
        let blob = container.GetBlobReference(blobName)
        
        let endpoint =
            Configuration.tryGetSetting "FormRecognizerEndpoint" |> Option.defaultValue null

        let formRecognizerKey =
            Configuration.tryGetSetting "FormRecognizerKey" |> Option.defaultValue null
        
        let sas = blob.GetSharedAccessSignature(SharedAccessBlobPolicy
                            (
                                Permissions            = SharedAccessBlobPermissions.Read,
                                SharedAccessExpiryTime = DateTimeOffset.Now.AddMinutes(5.)
                            ))

        let blobUrl = blob.Uri.ToString() + sas

        let credential = AzureKeyCredential(formRecognizerKey)
        let formRecognizerClient = FormRecognizerClient(Uri(endpoint), credential)

        let! request =
            formRecognizerClient.StartRecognizeReceiptsFromUriAsync(Uri(blobUrl))

        let! response =
            request.WaitForCompletionAsync()

        let recognizedForm = response.Value |> Seq.exactlyOne

        let json = JsonSerializer.Serialize(recognizedForm.Fields,
                                            JsonSerializerOptions (WriteIndented = true));

        do! container.GetBlockBlobReference(blobName + ".json")
                     .UploadTextAsync(json)

        printfn "%s" json

        let fields = JsonSerializer.Deserialize<Dictionary<string, UnoCashFormField>>(json)
        
        return {
           Payee  = getOrDefault "MerchantName" fields ""
           Date   = getOrDefault "TransactionDate" fields DateTime.Today
           Method = "Cash"
           Amount = getOrDefault "Total" fields 0m
       }
    }

let parseAsync blobName =
    async {    
        let container =
            blobClient.Value.GetContainerReference "receipts"
            
        let cachedResultsBlob =
            container.GetBlobReference(blobName + ".json")
            
        let! exists =   
            cachedResultsBlob.ExistsAsync()
          
        let receipt =
            match exists with
            | true  -> cachedResultsBlob.OpenReadAsync() |> getReceiptDataFromCache
            | false -> analyseReceipt container blobName
            
        return! receipt
    }