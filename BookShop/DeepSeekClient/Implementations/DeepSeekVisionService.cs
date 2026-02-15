using DeepSeek.Configs;
using DeepSeek.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

#pragma warning disable OPENAI001
namespace DeepSeek.Implementations
{
    public class DeepSeekVisionService
    {
        private readonly ChatClient _chatClient;

        public DeepSeekVisionService(IOptions<DeepSeekConfig> options)
        {
            var config = options.Value ?? throw new ArgumentNullException(nameof(options));

            OpenAIClient openAiClient;

            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                var clientOptions = new OpenAIClientOptions()
                {
                    Endpoint = new Uri(config.BaseUrl),
                };

                var credential = new ApiKeyCredential(config.ApiKey);
                openAiClient = new OpenAIClient(credential, clientOptions);
            }
            else
            {
                openAiClient = new OpenAIClient(config.BaseUrl);
            }

            _chatClient = openAiClient.GetChatClient(config.VisionModel);
        }

        public async Task<string> AnalyzeImageAsync(byte[] imageBytes, string prompt = "Опиши это изображение подробно")
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
                };

                var completion = await _chatClient.CompleteChatAsync(message);

                return completion.Value.Content[0].Text;
            }
            catch (ClientResultException ex)
            {
                return null;
            }
        }

        public async Task<string> AnalyzeBookCoverAsync(byte[] imageBytes)
        {
            const string prompt = """
                Опиши обложку этой книги максимально подробно. Укажи:
                1. Весь текст на обложке (название, автор, издательство, год)
                2. Внешний вид обложки (цвета, дизайн, иллюстрации)
                3. Язык текста
                4. Любые другие заметные детали
                """;

            return await AnalyzeImageAsync(imageBytes, prompt);
        }

        private string GetMimeType(byte[] imageBytes)
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
    }
}