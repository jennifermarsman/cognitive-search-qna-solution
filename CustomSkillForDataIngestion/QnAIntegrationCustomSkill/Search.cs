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
using Azure;
using Azure.Search.Documents;
using System.Collections.Generic;
using Azure.Search.Documents.Models;
using System.Linq;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker;
using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker.Models;
using Microsoft.Azure.Documents.SystemFunctions;

namespace QnAIntegrationCustomSkill
{
    public static class Search
    {
        private static string searchApiKey = Environment.GetEnvironmentVariable("SearchServiceApiKey", EnvironmentVariableTarget.Process);
        private static string searchServiceName = Environment.GetEnvironmentVariable("SearchServiceName", EnvironmentVariableTarget.Process);
        private static string searchIndexName = Constants.indexName;
       
        private static string storageAccountName = Environment.GetEnvironmentVariable("StorageAccountName", EnvironmentVariableTarget.Process);
        private static string storageAccountKey = Environment.GetEnvironmentVariable("StorageAccountKey", EnvironmentVariableTarget.Process);

        private static BlobServiceClient blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process));

        // Create a SearchIndexClient to send create/delete index commands
        private static Uri serviceEndpoint = new Uri($"https://{searchServiceName}.search.windows.net/");
        private static AzureKeyCredential credential = new AzureKeyCredential(searchApiKey);

        // Create a SearchClient to load and query documents
        private static SearchClient searchClient = new SearchClient(serviceEndpoint, searchIndexName, credential);

        private static string kbId;
        private static string qnaRuntimeKey;
        private static QnAMakerRuntimeClient runtimeClient;
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
            string getAnswer = req.Query["getAnswer"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            q = q ?? data?.q;
            top = top ?? data?.top;
            skip = skip ?? data?.skip;
            getAnswer = getAnswer ?? data?.getAnswer;

            QnASearchResultList qnaResponse = null;
            if (getAnswer.ToLower() == "true")
            {
                if (string.IsNullOrEmpty(kbId))
                {
                    kbId = await GetKbId(log);
                }

                if (string.IsNullOrEmpty(qnaRuntimeKey))
                {
                    qnaRuntimeKey = await GetRuntimeKey(log);
                }

                if (runtimeClient == null)
                {
                    runtimeClient = new QnAMakerRuntimeClient(new EndpointKeyServiceClientCredentials(qnaRuntimeKey))
                    {
                        RuntimeEndpoint = qnaMakerEndpoint
                    };
                }

                var qnaOptions = new QueryDTO
                {
                    Question = q,
                    Top = 1,
                    ScoreThreshold = 30
                };
                qnaResponse = await runtimeClient.Runtime.GenerateAnswerAsync(kbId, qnaOptions);

            }


            SearchOptions options = new SearchOptions()
            {
                Size = int.Parse(top),
                Skip = int.Parse(skip),
                IncludeTotalCount = true,
            };

            options.Facets.Add("keyPhrases");
            options.Facets.Add("fileType");
            options.HighlightFields.Add("content");
            options.Select.Add("metadata_storage_name");
            options.Select.Add("metadata_storage_path");
            options.Select.Add("id");

            var response = await searchClient.SearchAsync<SearchDocument>(q, options);

            Dictionary<string, IList<FacetValue>> facets = new Dictionary<string, IList<FacetValue>>();

            foreach (KeyValuePair<string, IList<FacetResult>> facet in response.Value.Facets)
            {
                //KeyValuePair<string, IList<FacetValue>> f = new KeyValuePair<string, IList<FacetValue>>(facet.Key, new List<FacetValue>());

                var values = new List<FacetValue>();
                foreach (FacetResult result in facet.Value)
                {
                    FacetValue value = new FacetValue() { count = result.Count, value = result.Value.ToString() };
                    values.Add(value);
                }

                facets[facet.Key] = values;
            }

            SearchOutput output = new SearchOutput();
            output.count = response.Value.TotalCount;
            output.results = response.Value.GetResults().ToList();
            output.facets = facets;

            if (qnaResponse != null)
            {
                output.answers = qnaResponse.Answers.First();
            }

            return new OkObjectResult(output);
        }

        private static async Task<string> GetKbId(ILogger log)
        {
            string kbID = string.Empty;
            var path = Path.Join(Environment.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Process), Constants.qnamakerFolderPath);
            var filePath = Path.Join(path, Constants.kbIdBlobName + ".txt");
            // Check for kbid in local file system
            if (File.Exists(filePath))
            {
                kbID = File.ReadAllText(filePath);
            }
            else
            {
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(Constants.kbContainerName);
                BlobClient kbidBlobClient = containerClient.GetBlobClient(Constants.kbIdBlobName);
                // Check blob for kbid 
                if (await kbidBlobClient.ExistsAsync())
                {
                    BlobDownloadInfo download = await kbidBlobClient.DownloadAsync();
                    using (var streamReader = new StreamReader(download.Content))
                    {
                        while (!streamReader.EndOfStream)
                        {
                            kbID = await streamReader.ReadLineAsync();
                        }
                    }
                }

            }
            return kbID;
        }

        private static async Task<string> GetRuntimeKey(ILogger log)
        {
            string runtimeKey = string.Empty;
            var path = Path.Join(Environment.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Process), Constants.qnamakerFolderPath);
            var filePath = Path.Join(path, Constants.keyBlobName + ".txt");
            // Check for kbid in local file system
            if (File.Exists(filePath))
            {
                runtimeKey = File.ReadAllText(filePath);
            }
            else
            {
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(Constants.kbContainerName);
                BlobClient keyBlobClient = containerClient.GetBlobClient(Constants.keyBlobName);
                // Check blob for kbid 
                if (await keyBlobClient.ExistsAsync())
                {
                    BlobDownloadInfo download = await keyBlobClient.DownloadAsync();
                    using (var streamReader = new StreamReader(download.Content))
                    {
                        while (!streamReader.EndOfStream)
                        {
                            runtimeKey = await streamReader.ReadLineAsync();
                        }
                    }
                }

            }
            return runtimeKey;
        }
    }
}
