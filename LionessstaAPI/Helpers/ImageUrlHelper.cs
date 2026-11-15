namespace LionessstaAPI.Helpers
{
    // Photos uploaded through the current pipeline are stored as two WebP blobs:
    //   p_<id>.webp        full size (shown in the lightbox)
    //   p_<id>_thumb.webp  small version (shown in grids and the cart)
    // The thumbnail name is derived from the full-size name, so no extra
    // database column is needed. Older photos (.jpg, uploaded before this
    // pipeline) have no thumbnail, so they fall back to the full-size URL.
    public static class ImageUrlHelper
    {
        public const string FullSuffix = ".webp";
        public const string ThumbSuffix = "_thumb.webp";

        public static string ThumbnailFor(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return imageUrl;
            if (!imageUrl.EndsWith(FullSuffix, StringComparison.OrdinalIgnoreCase)) return imageUrl;
            if (imageUrl.EndsWith(ThumbSuffix, StringComparison.OrdinalIgnoreCase)) return imageUrl;

            return imageUrl[..^FullSuffix.Length] + ThumbSuffix;
        }
    }
}
