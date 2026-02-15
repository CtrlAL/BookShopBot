using DeepSeek.Configs;
using DeepSeek.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace DeepSeek
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDeepSeekClient(this IServiceCollection services, IConfigurationSection section)
        {
            services.AddScoped<OpenAIClient>(services =>
            {
                var options = services.GetRequiredService<IOptions<AiConfig>>().Value;

                ArgumentNullException.ThrowIfNull(options);

                var clientOptions = new OpenAIClientOptions()
                {
                    Endpoint = new Uri(options.BaseUrl),
                };

                var credential = new ApiKeyCredential(options.ApiKey);

                return new OpenAIClient(credential, clientOptions);
            });

            services.AddScoped<ChatClient>(services =>
            {
                var aIClient = services.GetRequiredService<OpenAIClient>();
                var options = services.GetRequiredService<IOptions<AiConfig>>().Value;

                return aIClient.GetChatClient(options.Model);
            });

            services.Configure<AiConfig>(section);
            services.AddScoped<AiBookRecognitionService>();

            return services;
        }
    }
}
