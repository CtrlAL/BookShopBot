using DeepSeek.Configs;
using DeepSeek.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeepSeek
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDeepSeekClient(this IServiceCollection services, IConfigurationSection section)
        {
            services.Configure<DeepSeekConfig>(section);
            services.AddSingleton<DeepSeekVisionService>();
            services.AddSingleton<DeepSeekClient>();

            return services;
        }
    }
}
