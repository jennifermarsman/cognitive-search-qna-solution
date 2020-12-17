using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using Common;
using Azure.Storage.Blobs.Models;

namespace QnAIntegrationCustomSkill
{
    

    public static class GetKnowledgeBase
    {
        public static string kbId;

        [FunctionName("GetKb")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            BlobServiceClient blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process));
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(Constants.kbContainerName);

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

            GetKbOutput output = new GetKbOutput()
            {
                QnAMakerKnowledgeBaseID = kbId
            };

            return new OkObjectResult(output);
        }
    }
}
