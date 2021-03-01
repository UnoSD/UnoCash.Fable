using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.AI.FormRecognizer;
using Azure.AI.FormRecognizer.Models;
using Microsoft.Azure.Storage.Blob;
using Newtonsoft.Json;
using UnoCash.Dto;
using CloudStorageAccount = Microsoft.Azure.Storage.CloudStorageAccount;
using static UnoCash.Core.ConfigurationKeys;

namespace UnoCash.Core
{
    public static class ReceiptParser
    {
        public static async Task<Receipt> ParseAsync(string blobName)
        {
            var container =
                await GetReceiptsContainer().ConfigureAwait(false);

            var cachedResultsBlob =
                container.GetBlockBlobReference(blobName + ".json");

            if (await cachedResultsBlob.ExistsAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(await cachedResultsBlob.OpenReadAsync()))
                    using (var jsonReader = new JsonTextReader(reader))
                {
                    Console.WriteLine("Found receipt analysis in cache");
                    
                    var formFields = 
                        new JsonSerializer().Deserialize<Dictionary<string, FormField>>(jsonReader);

                    return formFields.ToReceipt();
                }

            Console.WriteLine("Analysing receipt");
            
            var blob =
                container.GetBlobReference(blobName);

            var endpoint =
                await ConfigurationReader.GetAsync("FormRecognizerEndpoint")
                                         .ConfigureAwait(false);

            var formRecognizerKey =
                await SecretReader.GetAsync("FormRecognizerKey")
                                  .ConfigureAwait(false);

            var sas = blob.GetSharedAccessSignature(new SharedAccessBlobPolicy
            {
                Permissions            = SharedAccessBlobPermissions.Read,
                SharedAccessExpiryTime = DateTimeOffset.Now.AddMinutes(5)
            });

            var blobUrl = blob.Uri + sas;

            var credential = new AzureKeyCredential(formRecognizerKey);
            var formRecognizerClient = new FormRecognizerClient(new Uri(endpoint), credential);

            var request =
                await formRecognizerClient.StartRecognizeReceiptsFromUriAsync(new Uri(blobUrl))
                                          .ConfigureAwait(false);

            var response =
                await request.WaitForCompletionAsync().ConfigureAwait(false);

            var recognizedForm = response.Value.Single();

            await container.GetBlockBlobReference(blobName + ".json")
                           .UploadTextAsync(JsonConvert.SerializeObject(recognizedForm.Fields))
                           .ConfigureAwait(false);

            Console.WriteLine(JsonConvert.SerializeObject(recognizedForm.Fields, Formatting.Indented));

            return recognizedForm.Fields.ToReceipt();
        }

        static Task<CloudBlobContainer> GetReceiptsContainer() =>
            ConfigurationReader.GetAsync(StorageAccountConnectionString)
                               .Map(CloudStorageAccount.Parse)
                               .Map(client => client.CreateCloudBlobClient()
                                                    .GetContainerReference("receipts"));

        static Receipt ToReceipt(this IReadOnlyDictionary<string, FormField> fields) =>
            new Receipt
            {
                Payee = fields.GetOrDefault<string>("MerchantName"),
                Date  = fields.GetOrDefault<DateTime>("TransactionDate"),
                //Items = fields["Items"].Value.AsString(),
                Amount = fields.GetOrDefault<decimal>("Total")
            };

        static T GetOrDefault<T>(
            this IReadOnlyDictionary<string, FormField> dict,
            string key,
            T defaultValue = default) =>
            dict.TryGetValue(key, out var field) ?
                ChangeTypeOrDefault(field, defaultValue) :
                defaultValue;

        static T ChangeTypeOrDefault<T>(FormField field, T defaultValue)
        {
            try
            {
                return (T) Convert.ChangeType(field.ValueData.Text, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}