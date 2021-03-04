module UnoCash.Api.GetReceiptUploadSasToken

open System
open Microsoft.Extensions.Primitives
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Blob
open UnoCash.Core
open Microsoft.Azure.WebJobs
open UnoCash.Api.Function
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs.Extensions.Http

[<FunctionName("GetReceiptUploadSasToken")>]
let run ([<HttpTrigger(AuthorizationLevel.Function, "get")>]req: HttpRequest) =
    async {
        let! connectionString =
            ConfigurationKeys.StorageAccountConnectionString |>
            ConfigurationReader.GetAsync
            
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
            
        req.HttpContext.Response.Headers.Add("blobName", cloudBlob.Name |> StringValues)
            
        return $"{cloudBlob.Uri}{sas}" |> Ok
    } |>
    runAsync'