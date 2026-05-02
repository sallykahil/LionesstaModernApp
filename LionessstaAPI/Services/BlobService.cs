using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace LionessstaAPI.Services
{
    public class BlobService : IBlobService
    {
        private readonly string _connectionString;
        private readonly string _containerName;

        //public BlobService(IConfiguration config)
        //{
        //    _connectionString = config["AzureBlob:ConnectionString"]!;
        //    _containerName = config["AzureBlob:ContainerName"]!;
        //}
        public BlobService(IConfiguration config)
        {
            _connectionString = config["AzureBlob:ConnectionString"]
                ?? throw new ArgumentNullException("AzureBlob:ConnectionString is missing in appsettings.json");
            _containerName = config["AzureBlob:ContainerName"]
                ?? throw new ArgumentNullException("AzureBlob:ContainerName is missing in appsettings.json");
        }
        public async Task<string> UploadImageAsync(Stream stream, string fileName, string contentType)
        {
            // Resize image to 800x1000 using ImageSharp
            using var image = await Image.LoadAsync(stream);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(800, 1000),
                Mode = ResizeMode.Crop
            }));

            using var outputStream = new MemoryStream();
            await image.SaveAsJpegAsync(outputStream);
            outputStream.Position = 0;

            // Upload to Azure Blob
            var uniqueName = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.UtcNow:yyyyMMddHHmmss}.jpg";
            var containerClient = new BlobContainerClient(_connectionString, _containerName);
            var blobClient = containerClient.GetBlobClient(uniqueName);

            await blobClient.UploadAsync(outputStream, new BlobHttpHeaders { ContentType = "image/jpeg" });

            return blobClient.Uri.ToString();
        }

        public async Task DeleteImageAsync(string imageUrl)
        {
            var uri = new Uri(imageUrl);
            var blobName = Path.GetFileName(uri.LocalPath);
            var container = new BlobContainerClient(_connectionString, _containerName);
            var blobClient = container.GetBlobClient(blobName);

            await blobClient.DeleteIfExistsAsync();
        }
    }
}