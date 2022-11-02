# Sitecore.StorageContainerFillerAndMediaDownloader
The sample code for filling storage container in blob storage with dummy images, queueing their names and Sitecore task to process that queue and download images locally

## Known Problems:

1. MediaDownloader does not work, there are some conflicting dependencies between of `Azure.Storage.Blobs` and `Azure.Storage.Queues` and Sitecore bin.
