module UnoCash.Api.GetReceiptUploadSasToken

open System
open System.Net
open Microsoft.Azure.Functions.Worker
open Microsoft.Azure.Functions.Worker.Http
open Microsoft.Azure.Storage
open Microsoft.Azure.Storage.Blob
open UnoCash.Api.Function

[<Function("GetReceiptUploadSasToken")>]
let run ([<HttpTrigger(AuthorizationLevel.Function, "get")>]req: HttpRequestData) =
    async {
        let connectionString =
            Environment.GetEnvironmentVariable("StorageAccountConnectionString")
            
        let cloudBlob =
            Guid.NewGuid().ToString("N") |>
            CloudStorageAccount.Parse(connectionString)
                               .CreateCloudBlobClient()
                               .GetContainerReference("receipts")
                               .GetBlobReference
        
        let sas =
            SharedAccessBlobPolicy(
                // SharedAccessProtocol.HttpsOnly, req.HttpContext.Connection.RemoteIpAddress.ToString() |> IPAddressOrRange
                Permissions = SharedAccessBlobPermissions.Create,
                SharedAccessExpiryTime = DateTimeOffset.Now.AddMinutes(2.)) |>
            cloudBlob.GetSharedAccessSignature
            
        let response =
            req.CreateResponse(HttpStatusCode.OK)
            
        response.Headers.Add("blobName", cloudBlob.Name)
        
        do! response.WriteStringAsync($"{cloudBlob.Uri}{sas}")
            
        return response
    } |>
    Async.StartAsTask