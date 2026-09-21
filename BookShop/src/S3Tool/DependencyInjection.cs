using Amazon.S3;
using BookShop.S3Tool.Configs;
using BookShop.S3Tool.Implementations;
using BookShop.S3Tool.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookShop.S3Tool;

public static class DependencyInjection
{
    public static IServiceCollection AddS3Client(this IServiceCollection services, IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        services.Configure<S3Config>(section);
        services.AddSingleton<IAmazonS3>(serviceProvider =>
        {
            var config = serviceProvider.GetRequiredService<IOptions<S3Config>>().Value;
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("BookShop.S3Tool");
            var environmentName = serviceProvider.GetService<IHostEnvironment>()?.EnvironmentName;
            var isDevelopment = environmentName == Environments.Development;

            if (string.IsNullOrWhiteSpace(config.AccessKey) || string.IsNullOrWhiteSpace(config.SecretKey))
            {
                if (!isDevelopment)
                {
                    throw new InvalidOperationException(
                        "S3 AccessKey/SecretKey не заданы в конфигурации (секция 'S3').");
                }

                logger.LogWarning("S3 AccessKey/SecretKey пусты. CreateFile/GetPresignedUrl упадут при первом обращении к MinIO.");
            }

            var amazonConfig = new AmazonS3Config
            {
                ServiceURL = config.Endpoint,
                ForcePathStyle = true,
            };
            return new AmazonS3Client(config.AccessKey, config.SecretKey, amazonConfig);
        });
        services.AddSingleton<S3Service>();
        services.AddSingleton<IS3Service>(sp => sp.GetRequiredService<S3Service>());

        return services;
    }
}