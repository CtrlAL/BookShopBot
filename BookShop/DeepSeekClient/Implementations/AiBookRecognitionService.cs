using DeepSeek.Domain;
using DeepSeek.ToolBuilder;using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;

namespace DeepSeek.Implementations
{
    public class AiBookRecognitionService
    {
        private readonly ChatClient _chatClient;

        private const string _prompt = """
                Опиши обложку этой книги максимально подробно. Укажи:
                1. Весь текст на обложке (название, автор, издательство, год)
                2. Внешний вид обложки (цвета, дизайн, иллюстрации)
                3. Язык текста
                4. Любые другие заметные детали
                """;

        public AiBookRecognitionService(ChatClient chatClient)
        {
            _chatClient = chatClient;
        }

        public async Task<BookRecognitionResult> AnalyzeBookImageAsync(byte[] imageBytes)
        {
            try
            {
                string imageDescription = await AnalyzeImageAsync(imageBytes);

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

        private async Task<string> AnalyzeImageAsync(byte[] imageBytes, string prompt = _prompt)
        {
            ArgumentNullException.ThrowIfNull(imageBytes);

            try
            {
                var message = new UserChatMessage(
                    ChatMessageContentPart.CreateTextPart(prompt),
                    ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(imageBytes), GetMimeType(imageBytes))
                );

                var options = new ChatCompletionOptions()
                {
                    ResponseFormat = ChatResponseFormat.CreateTextFormat(),
                    MaxOutputTokenCount = 1000,
                    Temperature = 0.7f,
                };

                var completion = await _chatClient.CompleteChatAsync(message);

                return completion.Value.Content[0].Text;
            }
            catch (ClientResultException ex)
            {
                return null;
            }
        }

        private static string GetMimeType(byte[] imageBytes)
        {
            if (imageBytes.Length > 1)
            {
                if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8) return "image/jpeg";
                if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50) return "image/png";
                if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49) return "image/gif";
                if (imageBytes[0] == 0x42 && imageBytes[1] == 0x4D) return "image/bmp";
                if (imageBytes[0] == 0x52 && imageBytes[1] == 0x49) return "image/webp";
            }
            return "image/jpeg";
        }

        private async Task<BookRecognitionResult> ExtractBookInfoFromDescriptionAsync(string imageDescription)
        {
            try
            {
                var bookExtractionTool = BookRecognitionToolBuilder.Build();

                var options = new ChatCompletionOptions
                {
                    Tools = { bookExtractionTool },
                };

                var messages = new ChatMessage[]
                {
                    new UserChatMessage($"Извлеки информацию о книге из этого описания: '{imageDescription}'. " +
                                       "Если какое-то поле невозможно определить, используй null. " +
                                       "Определи язык книги. " +
                                       "Оцени уверенность от 0.0 до 1.0 на основе качества описания.")
                };

                ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, options);

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