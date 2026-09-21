using BookAi.Abstractions;
using BookAi.Configs;
using BookAi.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace BookAi
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddBookAiClient(this IServiceCollection services, IConfigurationSection section)
        {
            var useMock = section.GetValue<bool>("UseMockAiService");

            services.Configure<AiConfig>(section);

            if (useMock)
            {
                services.AddScoped<IAiBookRecognitionService, MockAiBookRecognitionService>();
            }
            else
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

                services.AddScoped<IAiBookRecognitionService, AiBookRecognitionService>();
            }

            return services;
        }
    }
}
