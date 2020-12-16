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
using System.Net.Http;

namespace QnAIntegrationCustomSkill
{
    public static class UploadDocument
    {
        private static string storageAccountName = Environment.GetEnvironmentVariable("StorageAccountName", EnvironmentVariableTarget.Process);
        private static string storageAccountKey = Environment.GetEnvironmentVariable("StorageAccountKey", EnvironmentVariableTarget.Process);
        private static string storageContainerName = Constants.containerName;
        //private static string storageContainerName = "covid-docs";

        private static StorageSharedKeyCredential sharedStorageCredentials = new StorageSharedKeyCredential(storageAccountName, storageAccountKey);
        private static BlobContainerClient blobContainerClient = new BlobContainerClient(new Uri($"https://{storageAccountName}.blob.core.windows.net/{storageContainerName}"), sharedStorageCredentials);


        [FunctionName("UploadDocument")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string fileName = req.Query["fileName"];
            string file = req.Query["file"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            fileName = fileName ?? data?.fileName;
            file = file ?? data?.file;

            var bytes = Convert.FromBase64String(file);
            var contents = new MemoryStream(bytes);

            var response = await blobContainerClient.UploadBlobAsync(fileName, contents);


            return new OkObjectResult(response.Value);
        }
    }
}
