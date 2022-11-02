using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Sitecore.Data.Items;
using Sitecore.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MediaDownloader
{
  public class MediaDownloaderCommand
  {
    private const string ConnStr = "connection-string";

    private const string ContainerName = "media";

    private const string MediaQueueName = "mediaqueue";

    private const int batchNumber = 20;

    private static BlobServiceClient BlobSvcClient
    {
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

    public void Execute(Item[] items, CommandItem command, ScheduleItem schedule)
    {
      //one-by-one example
      //this can be improved to perform downloads in parallel threads
      for (int i = 0; i < batchNumber; i++)
      {
        //run in several threads (configurable)
        //run a configurable amount of items
        QueueMessage msg = Queue.ReceiveMessage(TimeSpan.FromSeconds(60));


        //download media to the temp folder       

        string filePath = $"{Sitecore.IO.FileUtil.MapPath(Sitecore.Configuration.Settings.TempFolderPath)}/{msg.Body.ToString()}";

        Sitecore.Diagnostics.Log.Info($"MediaDownloader: downloading file {filePath}", this);
        var client = BlobContainer.GetBlobClient(msg.Body.ToString());
        client.DownloadTo(filePath);

        //remove message from queue
        Queue.DeleteMessage(msg.MessageId, msg.PopReceipt);
      }
    }
  }
}