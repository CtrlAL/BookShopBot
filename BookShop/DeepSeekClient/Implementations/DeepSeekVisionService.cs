using DeepSeekClient.Configs;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeepSeek.Implementations
{
    public class DeepSeekVisionService
    {
        private readonly HttpClient _httpClient;
        private readonly DeepSeekConfig _config;

        public DeepSeekVisionService(IOptions<DeepSeekConfig> options)
        {
            _config = options.Value;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<string> AnalyzeImageAsync(byte[] imageBytes, string prompt = "Опиши это изображение подробно")
        {
            try
            {
                string base64Image = Convert.ToBase64String(imageBytes);
                string mimeType = GetMimeType(imageBytes);

                var requestData = new
                {
                    model = _config.VisionModel,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = prompt },
                                new {
                                    type = "image_url",
                                    image_url = new {
                                        url = $"data:{mimeType};base64,{base64Image}"
                                    }
                                }
                            }
                        }
                    },
                    max_tokens = _config.MaxTokens,
                    temperature = _config.Temperature
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_config.BaseUrl}/chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var responseObj = JsonSerializer.Deserialize<VisionApiResponse>(responseJson);

                return responseObj?.Choices?[0]?.Message?.Content ?? "Не удалось получить описание";
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка Vision API: {ex.Message}", ex);
            }
        }

        public async Task<string> AnalyzeBookCoverAsync(byte[] imageBytes)
        {
            string prompt = "Опиши обложку этой книги максимально подробно. Укажи: " +
                           "1. Весь текст на обложке (название, автор, издательство, год) " +
                           "2. Внешний вид обложки (цвета, дизайн, иллюстрации) " +
                           "3. Язык текста " +
                           "4. Любые другие заметные детали";

            return await AnalyzeImageAsync(imageBytes, prompt);
        }

        private string GetMimeType(byte[] imageBytes)
        {
            // Простая проверка типа изображения
            if (imageBytes.Length > 1)
            {
                if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8) return "image/jpeg";
                if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50) return "image/png";
                if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49) return "image/gif";
                if (imageBytes[0] == 0x42 && imageBytes[1] == 0x4D) return "image/bmp";
            }
            return "image/jpeg"; // По умолчанию
        }
    }
}
