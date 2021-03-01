using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using UnoCash.Core;
using static UnoCash.Core.ConfigurationKeys;

namespace UnoCash.Api
{
    public static class GetReceiptUploadSasToken
    {
        [FunctionName(nameof(GetReceiptUploadSasToken))]
        public static Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]
            HttpRequest req,
            ILogger log) =>
            ConfigurationReader.GetAsync(StorageAccountConnectionString)
                               .Map(cs =>
                                {
                                    var (name, result) = GetBlobUrl(cs, log);
                                    
                                    req.HttpContext.Response.Headers.Add("blobName", name);
                                    
                                    return result;
                                });
        
        
        static (string name, IActionResult result) GetBlobUrl(string connectionString, ILogger log) =>
            Guid.NewGuid()
                .ToString("N")
                .Tap(guid => log.LogWarning($"Getting blob {guid} upload URL for receipts container"))
                .GetBlobUrl(connectionString);

        static (string name, IActionResult result) GetBlobUrl(this string blobName, string connectionString) =>
            CloudStorageAccount.Parse(connectionString)
                               .CreateCloudBlobClient()
                               .GetContainerReference("receipts")
                               .GetBlobUrl(blobName)
                               .TMap(obj => (blobName, obj));

        static IActionResult GetBlobUrl(this CloudBlobContainer cbc, string blobName) =>
            cbc.GetBlockBlobReference(blobName)
               .GetSharedAccessSignature(new SharedAccessBlobPolicy
               {
                   Permissions            = SharedAccessBlobPermissions.Create,
                   SharedAccessExpiryTime = DateTimeOffset.Now.AddMinutes(2)
                   // Add IP range limit? Other access policy?
               }/*, "", SharedAccessProtocol.HttpsOnly, new IPAddressOrRange("CALLER")*/)
               .TMap(sas => $"{cbc.Uri}/{blobName}{sas}")
               .ToOkObject();
    }
}
