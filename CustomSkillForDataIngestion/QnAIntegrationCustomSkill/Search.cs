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
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker;
using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker.Models;

namespace QnAIntegrationCustomSkill
{
    public static class Search
    {
        private static string searchApiKey = Environment.GetEnvironmentVariable("SearchServiceApiKey", EnvironmentVariableTarget.Process);
        private static string searchServiceName = Environment.GetEnvironmentVariable("SearchServiceName", EnvironmentVariableTarget.Process);
        private static string searchIndexName = Constants.indexName;
       
        private static string storageAccountName = Environment.GetEnvironmentVariable("StorageAccountName", EnvironmentVariableTarget.Process);
        private static string storageAccountKey = Environment.GetEnvironmentVariable("StorageAccountKey", EnvironmentVariableTarget.Process);

        private static StorageSharedKeyCredential sharedStorageCredentials = new StorageSharedKeyCredential(storageAccountName, storageAccountKey);

        // Create a SearchIndexClient to send create/delete index commands
        private static Uri serviceEndpoint = new Uri($"https://{searchServiceName}.search.windows.net/");
        private static AzureKeyCredential credential = new AzureKeyCredential(searchApiKey);
        private static SearchIndexClient adminClient = new SearchIndexClient(serviceEndpoint, credential);

        // Create a SearchClient to load and query documents
        private static SearchClient searchClient = new SearchClient(serviceEndpoint, searchIndexName, credential);

        private static string kbId;
        private static string qnaRuntimeKey;
        private static string qnaMakerEndpoint = Environment.GetEnvironmentVariable("QnAMakerEndpoint", EnvironmentVariableTarget.Process);

        [FunctionName("Search")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string q = req.Query["q"];
            string top = req.Query["top"];
            string skip = req.Query["skip"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            q = q ?? data?.q;
            top = top ?? data?.top;
            skip = skip ?? data?.skip;

            var containerUrl = new Uri($"https://{storageAccountName}.blob.core.windows.net/{Constants.kbContainerName}");
            BlobContainerClient containerClient = new BlobContainerClient(containerUrl, sharedStorageCredentials);

            if (string.IsNullOrEmpty(kbId))
            {
                BlobClient kbidBlobClient = containerClient.GetBlobClient(Constants.kbIdBlobName);
                // Check blob for kbid 
                if (await kbidBlobClient.ExistsAsync())
                {
                    BlobDownloadInfo download = await kbidBlobClient.DownloadAsync();
                    using (var streamReader = new StreamReader(download.Content))
                    {
                        while (!streamReader.EndOfStream)
                        {
                            kbId = await streamReader.ReadLineAsync();
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(qnaRuntimeKey))
            {
                BlobClient keyBlobClient = containerClient.GetBlobClient(Constants.keyBlobName);
                // Check blob for kbid 
                if (await keyBlobClient.ExistsAsync())
                {
                    BlobDownloadInfo download = await keyBlobClient.DownloadAsync();
                    using (var streamReader = new StreamReader(download.Content))
                    {
                        while (!streamReader.EndOfStream)
                        {
                            qnaRuntimeKey = await streamReader.ReadLineAsync();
                        }
                    }
                }
            }

            var runtimeClient = new QnAMakerRuntimeClient(new EndpointKeyServiceClientCredentials(qnaRuntimeKey))
            {
                RuntimeEndpoint = qnaMakerEndpoint
            };

            var qnaOptions = new QueryDTO
            {
                Question = q,
                Top = 1,
                ScoreThreshold = 30
            };
            QnASearchResultList qnaResponse = await runtimeClient.Runtime.GenerateAnswerAsync(kbId, qnaOptions);


            SearchOptions options = new SearchOptions()
            {
                Size = int.Parse(top),
                Skip = int.Parse(skip),
                IncludeTotalCount = true,
            };

            options.HighlightFields.Add("content");

            var response = await searchClient.SearchAsync<SearchDocument>(q, options);

            SearchOutput output = new SearchOutput();
            output.count = response.Value.TotalCount;
            output.results = response.Value.GetResults().ToList();
            output.answers = qnaResponse.Answers.First();

            return new OkObjectResult(output);
        }
    }
}
