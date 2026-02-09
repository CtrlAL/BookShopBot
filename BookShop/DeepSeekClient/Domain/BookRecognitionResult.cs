namespace DeepSeek.Domain
{
    public class BookRecognitionResult
    {
        public bool Success { get; set; }
        public BookInfo? Payload { get; set; }
        public string? Error { get; set; }
        public string? RawDescription { get; set; }
        public string? ImageDescription { get; set; }
        public bool HasImageData { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}
