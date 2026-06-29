using BookAi.Domain;

namespace BookAi.Abstractions
{
    public interface IAiBookRecognitionService
    {
        Task<BookRecognitionResult> AnalyzeBookImageAsync(byte[] imageBytes);
    }
}
