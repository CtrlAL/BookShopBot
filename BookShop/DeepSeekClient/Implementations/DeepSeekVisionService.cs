using DeepSeek.Configs;
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

            var openAiClient = new OpenAIClient(config.ApiKey);

            var deepSeekOptions = new OpenAIClientOptions()
            {
                Endpoint = new Uri(config.BaseUrl)
            };

            var credential = new ApiKeyCredential(config.ApiKey);
            var deepSeekClient = new OpenAIClient(credential, deepSeekOptions);
            _chatClient = deepSeekClient.GetChatClient(config.VisionModel);
        }

        public async Task<string> AnalyzeImageAsync(byte[] imageBytes, string prompt = "Опиши это изображение подробно")
        {
            ArgumentNullException.ThrowIfNull(imageBytes);

            try
            {
                var binaryData = new BinaryData(imageBytes);

                var message = new UserChatMessage(
                    ChatMessageContentPart.CreateTextPart(prompt),
                    ChatMessageContentPart.CreateImagePart(binaryData, GetMimeType(imageBytes))
                );

                var response = await _chatClient.CompleteChatAsync(message);

                return response.Value.Content[0].Text;
            }
            catch(ClientResultException ex)
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