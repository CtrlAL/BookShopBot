namespace BookShop.S3Tool.Configs;

public sealed class S3Config
{
    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "bookshop";
    public TimeSpan PresignedLifetime { get; set; } = TimeSpan.FromDays(365);
}