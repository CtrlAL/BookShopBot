using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Fsm.Session;

/// <summary>
/// Registers the cache-aside stack: <see cref="IMemoryCache"/>, the store
/// (<typeparamref name="TStore"/>) in scoped lifetime and the
/// <see cref="CachedChatSessionRepository{TBase}"/> decorator on top of it.
/// Also provides <see cref="AddChatSessionCleanup"/> to wire up the
/// <see cref="SessionStorageOptions"/>, <see cref="NpgsqlDataSource"/> and
/// hosted <see cref="ChatSessionCleanupService"/>.
/// </summary>
public static class ChatSessionRepositoryServiceCollectionExtensions
{
    public static IServiceCollection AddCachedChatSessionRepository<TSession, TStore>(
        this IServiceCollection services)
        where TSession : class
        where TStore : class, IChatSessionRepository<TSession>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddMemoryCache();
        services.AddOptions<SessionCacheOptions>();
        services.AddScoped<TStore>();
        services.AddScoped<IChatSessionRepository<TSession>>(serviceProvider =>
            new CachedChatSessionRepository<TSession>(
                serviceProvider.GetRequiredService<TStore>(),
                serviceProvider.GetRequiredService<IMemoryCache>(),
                serviceProvider.GetRequiredService<IOptions<SessionCacheOptions>>(),
                serviceProvider.GetRequiredService<ILogger<CachedChatSessionRepository<TSession>>>()));

        return services;
    }

    /// <summary>
    /// Registers a singleton <see cref="NpgsqlDataSource"/>,
    /// <see cref="SessionStorageOptions"/> and the hosted
    /// <see cref="ChatSessionCleanupService"/> that expires session rows.
    /// </summary>
    public static IServiceCollection AddChatSessionCleanup(
        this IServiceCollection services,
        string connectionString,
        Action<SessionStorageOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<NpgsqlDataSource>(_ => NpgsqlDataSource.Create(connectionString));
        services.AddOptions<SessionStorageOptions>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddHostedService<ChatSessionCleanupService>();

        return services;
    }
}