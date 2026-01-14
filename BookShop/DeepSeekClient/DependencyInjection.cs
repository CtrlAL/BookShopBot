using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeepSeekClient
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDeepSeekCline(IServiceCollection services, IConfigurationSection section)
        {
            return services;
        }
    }
}
