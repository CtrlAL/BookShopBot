using System.Text.Json.Serialization;

namespace DeepSeek.Domain
{
    public class VisionApiResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("choices")]
        public List<VisionChoice> Choices { get; set; } = new();
    }

    public class VisionChoice
    {
        [JsonPropertyName("message")]
        public VisionMessage Message { get; set; } = new();
    }

    public class VisionMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
