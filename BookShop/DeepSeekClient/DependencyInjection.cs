using DeepSeek.Configs;
using DeepSeek.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeepSeek
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDeepSeekCline(IServiceCollection services, IConfigurationSection section)
        {
            //services.Configure<DeepSeekConfig>();
            services.Configure<DeepSeekConfig>(dwd);


            //services.Configure<DeepSeekConfig>();
            services.AddSingleton<DeepSeekVisionService>();
            services.AddSingleton<DeepSeekClient>();

            return services;
        }
    }
}
