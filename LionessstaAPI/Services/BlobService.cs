using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LionessstaAPI.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace LionessstaAPI.Services
{
    public class BlobService : IBlobService
    {
        // Longest side, in pixels. Photos keep their own shape (no cropping) and
        // are only ever scaled down, never up.
        private const int FullMaxSide = 1600;
        private const int ThumbMaxSide = 600;
        private const int FullQuality = 82;
        private const int ThumbQuality = 78;

        // Every upload gets a brand-new unique name, so a blob's content never
        // changes -- browsers can safely keep it for a year without re-checking.
        private const string CacheControl = "public, max-age=31536000, immutable";

        private readonly string _connectionString;
        private readonly string _containerName;

        public BlobService(IConfiguration config)
        {
            _connectionString = config["AzureBlob:ConnectionString"]
                ?? throw new ArgumentNullException("AzureBlob:ConnectionString is missing in appsettings.json");
            _containerName = config["AzureBlob:ContainerName"]
                ?? throw new ArgumentNullException("AzureBlob:ContainerName is missing in appsettings.json");
        }

        public async Task<string> UploadImageAsync(Stream stream, string fileName, string contentType)
        {
            using var image = await Image.LoadAsync(stream);

            // Phone photos are often stored sideways with a "rotate me" note in
            // their metadata -- apply that rotation for real, then drop the
            // metadata (it can include the GPS location where the photo was taken).
            image.Mutate(x => x.AutoOrient());
            image.Metadata.ExifProfile = null;
            image.Metadata.XmpProfile = null;
            image.Metadata.IptcProfile = null;

            var fullSize = FitWithin(image.Width, image.Height, FullMaxSide);
            if (fullSize.Width != image.Width || fullSize.Height != image.Height)
                image.Mutate(x => x.Resize(fullSize.Width, fullSize.Height));

            var thumbSize = FitWithin(image.Width, image.Height, ThumbMaxSide);
            using var thumb = image.Clone(x => x.Resize(thumbSize.Width, thumbSize.Height));

            var baseName = $"p_{Guid.NewGuid():N}";
            var container = new BlobContainerClient(_connectionString, _containerName);
            var fullBlob = container.GetBlobClient(baseName + ImageUrlHelper.FullSuffix);
            var thumbBlob = container.GetBlobClient(baseName + ImageUrlHelper.ThumbSuffix);

            await UploadWebpAsync(fullBlob, image, FullQuality);
            try
            {
                await UploadWebpAsync(thumbBlob, thumb, ThumbQuality);
            }
            catch
            {
                // Don't leave a full-size photo behind with no thumbnail.
                await fullBlob.DeleteIfExistsAsync();
                throw;
            }

            return fullBlob.Uri.ToString();
        }

        public async Task DeleteImageAsync(string imageUrl)
        {
            var container = new BlobContainerClient(_connectionString, _containerName);

            await container.GetBlobClient(BlobNameFromUrl(imageUrl)).DeleteIfExistsAsync();

            var thumbUrl = ImageUrlHelper.ThumbnailFor(imageUrl);
            if (thumbUrl != imageUrl)
                await container.GetBlobClient(BlobNameFromUrl(thumbUrl)).DeleteIfExistsAsync();
        }

        private static async Task UploadWebpAsync(BlobClient blob, Image image, int quality)
        {
            using var output = new MemoryStream();
            await image.SaveAsync(output, new WebpEncoder { Quality = quality });
            output.Position = 0;

            await blob.UploadAsync(output, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "image/webp",
                    CacheControl = CacheControl
                }
            });
        }

        private static string BlobNameFromUrl(string imageUrl)
        {
            return Path.GetFileName(new Uri(imageUrl).LocalPath);
        }

        private static Size FitWithin(int width, int height, int maxSide)
        {
            var longest = Math.Max(width, height);
            if (longest <= maxSide) return new Size(width, height);

            var scale = (double)maxSide / longest;
            return new Size(
                Math.Max(1, (int)Math.Round(width * scale)),
                Math.Max(1, (int)Math.Round(height * scale)));
        }
    }
}
