using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    public class BookInfo
    {
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? PublishingHouse { get; set; }
        public int? Year { get; set; }
        public string? ISBN { get; set; }
        public string? Language { get; set; }
        public double Confidence { get; set; }
    }

}
