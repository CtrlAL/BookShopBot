namespace BookShop.S3Tool.Interfaces;

public interface IS3Service
{
    Task<bool> CreateFile(Stream file, string folder, string fileName, string fileType);
    string GetPresignedUrl(string folder, string fileName, TimeSpan? lifetime = null);
}