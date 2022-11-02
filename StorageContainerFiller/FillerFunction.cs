using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Mvc;

namespace StorageContainerFiller
{
  public static class FillerFunction
  {
    private const string ConnStr = "<connection-string>";

    private const string ContainerName = "media";

    private const string MediaQueueName = "mediaqueue";

    private static BlobServiceClient BlobSvcClient {
      get
      {        
        return new BlobServiceClient(ConnStr);
      }
    }

    private static BlobContainerClient BlobContainer
    {
      get
      {
        return BlobSvcClient.GetBlobContainerClient("media");
      }
    }

    private static QueueClient Queue
    {
      get
      {
        return new QueueClient(ConnStr, MediaQueueName);

      }
    }

    [FunctionName("FillerOrchestrator")]
    public static async Task<List<string>> RunOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext context)
    {      
      var outputs = new List<string>();
      var outputTasks = new List<Task<string>>();
      int imagesCount = 100;

      for (int i = 0; i < imagesCount; i++)
      {
        var randomString = $"{Guid.NewGuid().ToString()}.jpg";
        outputTasks.Add(context.CallActivityAsync<string>("FillerWorker", randomString));
      }

      await Task.WhenAll(outputTasks);

      // merge all the available dates
      foreach (var task in outputTasks)
      {
        outputs.Add(task.Result);
      }

      // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
      return outputs;
    }

    [FunctionName("FillerWorker")]
    public static string SayHello([ActivityTrigger] string name, ILogger log)
    {
      log.LogInformation($"Working on Media item '{name}'.");
      log.LogInformation($"Downloading '{name}'.");
      //var url = "https://picsum.photos/1000";
      var url = "https://source.unsplash.com/collection/928423/480x480";

      var handler = new HttpClientHandler()
      {
        AllowAutoRedirect = true
      };

      HttpClient client = new HttpClient(handler);
      var webRequest = new HttpRequestMessage(HttpMethod.Get, url);

      HttpResponseMessage response = client.Send(webRequest);
      var type = response.Content.Headers.ContentType.MediaType;      

      //response.EnsureSuccessStatusCode();

      using var reader = new StreamReader(response.Content.ReadAsStream());
      log.LogInformation($"Downloading '{name}' completed.");

      log.LogInformation($"Uploading '{name}'.");
      UploadStream(BlobContainer, response.Content.ReadAsStream(), name);

      //BlobContainer.GetBlobClient("test").Exist
      //BlobContainer.GetBlobClient("test").DownloadTo

      log.LogInformation($"Uploading '{name}' completed.");

      return $"Done for {name}";
    }

    [FunctionName("FillerHttpStart")]
    public static async Task<HttpResponseMessage> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient starter,
        ILogger log)
    {
      // Function input comes from the request content.
      string instanceId = await starter.StartNewAsync("FillerOrchestrator", null);

      log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

      return starter.CreateCheckStatusResponse(req, instanceId);
    }

    [FunctionName("QueuerHttpStart")]
    public static async Task<IActionResult> QueuerHttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
        [DurableClient] IDurableOrchestrationClient starter,
        ILogger log)
    {
      log.LogInformation($"Started adding filenames to the queue");

      var blobs = BlobContainer.GetBlobsAsync().AsPages();
      int counter = 0;

      // Enumerate the blobs returned for each page.
      await foreach (Azure.Page<BlobItem> blobPage in blobs)
      {
        foreach (BlobItem blobItem in blobPage.Values)
        {
          Queue.SendMessage(blobItem.Name);
          counter++;
          Console.WriteLine("Blob name: {0} added to the Queue", blobItem.Name);
        }

        Console.WriteLine();
      }

      //return starter.CreateCheckStatusResponse(req);
      return new OkObjectResult($"Queued {counter} filenames");
    }

    public static void UploadStream(BlobContainerClient containerClient, Stream streamToUpload, string fileName)
    {
      BlobClient blobClient = containerClient.GetBlobClient(fileName);

      blobClient.Upload(streamToUpload, true);
    }
  }
}