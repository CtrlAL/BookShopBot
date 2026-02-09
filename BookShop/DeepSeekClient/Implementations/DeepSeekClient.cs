using DeepSeekClient.Configs;
using DeepSeekClient.Domain;
using DeepSeekClient.Implementations;
using DeepSeekClient.ToolBuilder;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;

namespace DeepSeek.Implementations
{
    public class DeepSeekClient
    {
        private readonly OpenAIClient _openAIClient;
        private readonly DeepSeekVisionService _visionService;
        private readonly DeepSeekConfig _config;

        public DeepSeekClient(
            IOptions<DeepSeekConfig> options,
            DeepSeekVisionService visionService)
        {
            _config = options.Value;
            _visionService = visionService;

            var clientOptions = new OpenAIClientOptions()
            {
                Endpoint = new Uri(_config.BaseUrl),
            };
            var credential = new ApiKeyCredential(_config.ApiKey);
            _openAIClient = new OpenAIClient(credential, clientOptions);
        }

        public async Task<BookRecognitionResult> AnalyzeBookImageAsync(byte[] imageBytes)
        {
            try
            {
                string imageDescription = await _visionService.AnalyzeBookCoverAsync(imageBytes);

                var result = await ExtractBookInfoFromDescriptionAsync(imageDescription);

                if (result.Success)
                {
                    result.ImageDescription = imageDescription;
                    result.HasImageData = true;
                }

                return result;
            }
            catch (Exception ex)
            {
                return new BookRecognitionResult
                {
                    Success = false,
                    Error = $"Ошибка анализа изображения: {ex.Message}"
                };
            }
        }

        private async Task<BookRecognitionResult> ExtractBookInfoFromDescriptionAsync(string imageDescription)
        {
            try
            {
                ChatClient chatClient = _openAIClient.GetChatClient(_config.Model);

                var bookExtractionTool = BookRecognitionToolBuilder.Build();

                var options = new ChatCompletionOptions
                {
                    Tools = { bookExtractionTool },
                    Temperature = _config.Temperature,
                };

                var messages = new ChatMessage[]
                {
                    new UserChatMessage($"Извлеки информацию о книге из этого описания: '{imageDescription}'. " +
                                       "Если какое-то поле невозможно определить, используй null. " +
                                       "Определи язык книги. " +
                                       "Оцени уверенность от 0.0 до 1.0 на основе качества описания.")
                };

                ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options);

                if (completion.FinishReason == ChatFinishReason.ToolCalls)
                {
                    foreach (var toolCall in completion.ToolCalls)
                    {
                        if (toolCall.FunctionName == "ExtractBookInfo")
                        {
                            using JsonDocument doc = JsonDocument.Parse(toolCall.FunctionArguments);
                            var root = doc.RootElement;

                            return new BookRecognitionResult
                            {
                                Success = true,
                                Payload = new BookInfo
                                {
                                    Title = GetStringProperty(root, "title"),
                                    Author = GetStringProperty(root, "author"),
                                    PublishingHouse = GetStringProperty(root, "publishingHouse"),
                                    Year = GetIntProperty(root, "year"),
                                    ISBN = GetStringProperty(root, "isbn"),
                                    Language = GetStringProperty(root, "language"),
                                    Confidence = GetDoubleProperty(root, "confidence") ?? 0.5
                                },
                                RawDescription = imageDescription,
                                ProcessedAt = DateTime.UtcNow
                            };
                        }
                    }
                }

                return new BookRecognitionResult
                {
                    Success = false,
                    Error = "Не удалось извлечь структурированные данные: " + completion.Content[0].Text,
                    RawDescription = imageDescription
                };
            }
            catch (Exception ex)
            {
                return new BookRecognitionResult
                {
                    Success = false,
                    Error = $"Ошибка извлечения информации: {ex.Message}",
                    RawDescription = imageDescription
                };
            }
        }

        private static string? GetStringProperty(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var prop) &&
                   prop.ValueKind != JsonValueKind.Null ?
                   prop.GetString() : null;
        }

        private static int? GetIntProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) &&
                prop.ValueKind != JsonValueKind.Null &&
                prop.TryGetInt32(out int value))
            {
                return value;
            }
            return null;
        }

        private static double? GetDoubleProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) &&
                prop.ValueKind != JsonValueKind.Null &&
                prop.TryGetDouble(out double value))
            {
                return value;
            }
            return null;
        }
    }
}