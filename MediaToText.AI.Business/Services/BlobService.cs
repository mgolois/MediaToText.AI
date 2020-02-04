using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MediaToText.AI.Business.Services
{
    public interface IBlobService
    {
        Task<string> UploadFile(string containerName, string filename, Stream stream);
        Task<byte[]> DownloadBytes(string containerName, string fileName);
        string GetContainerUrl(string containerName);
    }
    public class BlobService : IBlobService
    {
        private CloudBlobClient cloudBlobClient;
        public BlobService(string connectionString)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            cloudBlobClient = storageAccount.CreateCloudBlobClient();
        }

        public async Task<string> UploadFile(string containerName, string filename, Stream stream)
        {
            var cloudBlobContainer = cloudBlobClient.GetContainerReference(containerName);
            var cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(filename);

            await cloudBlockBlob.UploadFromStreamAsync(stream);
            return cloudBlockBlob.Uri.ToString();
        }

        public async Task<byte[]> DownloadBytes(string containerName, string fileName)
        {
            var cloudBlobContainer = cloudBlobClient.GetContainerReference(containerName);
            var cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference(fileName);
            await cloudBlockBlob.FetchAttributesAsync();
            byte[] blobBytes = new byte[cloudBlockBlob.Properties.Length];

            await cloudBlockBlob.DownloadToByteArrayAsync(blobBytes, 0);
            return blobBytes;
        }

        public string GetContainerUrl(string containerName)
        {
            var container = cloudBlobClient.GetContainerReference(containerName);
            string containerSasToken = container.GetSharedAccessSignature(new SharedAccessBlobPolicy()
            {
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddDays(1),
                Permissions = SharedAccessBlobPermissions.Write
            });

            string containerSasUrl = container.Uri.AbsoluteUri + containerSasToken;
            return containerSasUrl;
        }
    }
}
