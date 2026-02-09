namespace DeepSeek.Configs
{
    public class DeepSeekConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.deepseek.com";
        public string Model { get; set; } = "deepseek-chat";
        public string VisionModel { get; set; } = "deepseek-vision";
        public int MaxTokens { get; set; } = 2000;
        public float Temperature { get; set; } = 0.1f;
    }
}
