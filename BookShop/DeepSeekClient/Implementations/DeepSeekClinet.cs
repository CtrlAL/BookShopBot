using DeepSeekClient.Configs;
using DeepSeekClient.Domain;
using Microsoft.Extensions.Options;
using OpenAI.Images;
using System.Text;
using System.Text.Json;

namespace DeepSeekClient.Implementations
{
    public class DeepSeekHttpClient
    {
        private readonly HttpClient _httpClient;
        private DeepSeekConfig _seekConfig;

        public DeepSeekHttpClient(IOptions<DeepSeekConfig> options)
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(options.Value.BaseUrl);
            _seekConfig = options.Value;
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_seekConfig.ApiKey}");
        }

        public async Task<string> GetChatCompletionAsync(string prompt)
        {
            var request = new
            {
                model = "deepseek-chat",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                max_tokens = 1024,
                temperature = 0.7
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                "https://api.deepseek.com/chat/completions",
                content);

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseObj = JsonSerializer.Deserialize<ChatResponse>(responseJson);

            return responseObj?.Choices[0]?.Message?.Content;
        }
    }
}