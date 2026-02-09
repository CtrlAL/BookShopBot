using DeepSeekClient.Configs;
using DeepSeekClient.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeepSeekClient
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDeepSeekCline(IServiceCollection services, IConfigurationSection section)
        {
            services.Configure<DeepSeekConfig>(section);

            services.AddSingleton<DeepSeekVisionService>();
            services.AddSingleton<DeepSeekClient>();

            return services;
        }
    }
}
