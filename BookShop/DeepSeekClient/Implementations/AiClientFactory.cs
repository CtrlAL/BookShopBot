using DeepSeek.Configs;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;

namespace DeepSeek.Implementations
{
    public class AiClientFactory
    {
        private readonly DeepSeekVisionService _visionService;
        private readonly DeepSeekConfig _config;

        public AiClientFactory(
            IOptions<DeepSeekConfig> options,
            DeepSeekVisionService visionService)
        {
            _config = options.Value;
            _visionService = visionService;
        }

        public OpenAIClient Create()
        {
            var clientOptions = new OpenAIClientOptions()
            {
                Endpoint = new Uri(_config.BaseUrl),
            };
            var credential = new ApiKeyCredential(_config.ApiKey);

            return new OpenAIClient(credential, clientOptions);
        }
    }
}
