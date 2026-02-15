namespace DeepSeek.Configs
{
    public class AiConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.deepseek.com";
        public string Model { get; set; } = "deepseek-chat";
        public int MaxTokens { get; set; } = 2000;
        public float Temperature { get; set; } = 0.1f;
    }
}
