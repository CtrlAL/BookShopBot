using DeepSeek.Domain;

namespace DeepSeek.Abstractions
{
    public interface IAiBookRecognitionService
    {
        Task<BookRecognitionResult> AnalyzeBookImageAsync(byte[] imageBytes);
    }
}
