 
namespace LionessstaAPI.Services
{
 
        public interface IBlobService
        {
            Task<string> UploadImageAsync(Stream stream, string fileName, string contentType);
            Task DeleteImageAsync(string imageUrl);
        }
 

}
