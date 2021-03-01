using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.AI.FormRecognizer;
using Microsoft.Azure.Storage.Blob;
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
                using (var stream = await cachedResultsBlob.OpenReadAsync())
                {
                    Console.WriteLine("Found receipt analysis in cache");

                    var formFields =
                        await JsonSerializer.DeserializeAsync<Dictionary<string, UnoCashFormField>>(stream)
                                            .ConfigureAwait(false);

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

            var json = JsonSerializer.Serialize(recognizedForm.Fields,
                                                new JsonSerializerOptions {WriteIndented = true});

            await container.GetBlockBlobReference(blobName + ".json")
                           .UploadTextAsync(json)
                           .ConfigureAwait(false);

            Console.WriteLine(json);

            return JsonSerializer.Deserialize<Dictionary<string, UnoCashFormField>>(json).ToReceipt();
        }

        static Task<CloudBlobContainer> GetReceiptsContainer() =>
            ConfigurationReader.GetAsync(StorageAccountConnectionString)
                               .Map(CloudStorageAccount.Parse)
                               .Map(client => client.CreateCloudBlobClient()
                                                    .GetContainerReference("receipts"));

        static Receipt ToReceipt(this IReadOnlyDictionary<string, UnoCashFormField> fields) =>
            new Receipt
            {
                Payee  = fields.GetOrDefault<string>("MerchantName"),
                Date   = fields.GetOrDefault<DateTime>("TransactionDate"),
                Amount = fields.GetOrDefault<decimal>("Total")
            };

        static T GetOrDefault<T>(
            this IReadOnlyDictionary<string, UnoCashFormField> dict,
            string key,
            T defaultValue = default) =>
            dict.TryGetValue(key, out var field) ?
                ChangeTypeOrDefault(field, defaultValue) :
                defaultValue;

        static T ChangeTypeOrDefault<T>(UnoCashFormField field, T defaultValue)
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