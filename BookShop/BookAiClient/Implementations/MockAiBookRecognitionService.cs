using BookAi.Abstractions;
using BookAi.Domain;
using Microsoft.Extensions.Logging;

namespace BookAi.Implementations
{
    public class MockAiBookRecognitionService : IAiBookRecognitionService
    {
        private readonly ILogger<MockAiBookRecognitionService> _logger;

        public MockAiBookRecognitionService(ILogger<MockAiBookRecognitionService> logger)
        {
            _logger = logger;
        }

        public Task<BookRecognitionResult> AnalyzeBookImageAsync(byte[] imageBytes)
        {
            _logger.LogInformation("MockAiBookRecognitionService: returning mock book recognition result");

            var result = new BookRecognitionResult
            {
                Success = true,
                Payload = new BookInfo
                {
                    Title = "Mock Book Title",
                    Author = "Mock Author",
                    PublishingHouse = "Mock Publishing House",
                    Year = 2024,
                    ISBN = "978-0-00-000000-0",
                    Language = "Russian",
                    Confidence = 0.95
                },
                RawDescription = "Mock description of a book cover with title and author",
                ImageDescription = "Mock visual description of the book cover",
                HasImageData = true,
                ProcessedAt = DateTime.UtcNow
            };

            return Task.FromResult(result);
        }
    }
}
