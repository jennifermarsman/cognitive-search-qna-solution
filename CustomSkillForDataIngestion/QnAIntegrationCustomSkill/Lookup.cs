using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Common;
using Azure.Search.Documents.Indexes;
using Azure;
using Azure.Search.Documents;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Azure.Search.Documents.Models;
using System.Linq;
using Azure.Storage.Blobs;
using Azure.Storage;
using Azure.Storage.Sas;
using System.Net;

namespace QnAIntegrationCustomSkill
{
    public static class Lookup
    {
        private static string searchApiKey = Environment.GetEnvironmentVariable("SearchServiceApiKey", EnvironmentVariableTarget.Process);
        private static string searchServiceName = Environment.GetEnvironmentVariable("SearchServiceName", EnvironmentVariableTarget.Process);
        private static string searchIndexName = Constants.indexName;
        //private static string searchIndexName = "qna-index";

        private static string storageAccountName = Environment.GetEnvironmentVariable("StorageAccountName", EnvironmentVariableTarget.Process);
        private static string storageAccountKey = Environment.GetEnvironmentVariable("StorageAccountKey", EnvironmentVariableTarget.Process);
        private static string storageContainerName = Constants.containerName;
        //private static string storageContainerName = "covid-docs";

        private static StorageSharedKeyCredential sharedStorageCredentials = new StorageSharedKeyCredential(storageAccountName, storageAccountKey);
        private static BlobContainerClient blobContainerClient = new BlobContainerClient(new Uri($"https://{storageAccountName}.blob.core.windows.net/{storageContainerName}"), sharedStorageCredentials);

        // Create a SearchIndexClient to send create/delete index commands
        private static Uri serviceEndpoint = new Uri($"https://{searchServiceName}.search.windows.net/");
        private static AzureKeyCredential credential = new AzureKeyCredential(searchApiKey);
        private static SearchIndexClient adminClient = new SearchIndexClient(serviceEndpoint, credential);

        // Create a SearchClient to load and query documents
        private static SearchClient searchClient = new SearchClient(serviceEndpoint, searchIndexName, credential);

        [FunctionName("Lookup")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string id = req.Query["id"];
            string top = req.Query["top"];
            string suggester = req.Query["suggester"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            id = id ?? data?.id;


            var response = await searchClient.GetDocumentAsync<SearchDocument>(id);

            var policy = new BlobSasBuilder
            {
                Protocol = SasProtocol.HttpsAndHttp,
                BlobContainerName = storageContainerName,
                Resource = "c",
                StartsOn = DateTimeOffset.UtcNow,
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                IPRange = new SasIPRange(IPAddress.None, IPAddress.None)
            };

            policy.SetPermissions(BlobSasPermissions.Read);
            var sas = policy.ToSasQueryParameters(sharedStorageCredentials);

            LookupOutput output = new LookupOutput();
            output.document = response.Value;
            output.sasToken = sas.ToString();

            return new OkObjectResult(output);
        }
    }
}
