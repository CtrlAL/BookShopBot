using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fsm.Session;

/// <summary>
/// Registers the cache-aside stack: <see cref="IMemoryCache"/>, the store
/// (<typeparamref name="TStore"/>) in scoped lifetime and the
/// <see cref="CachedChatSessionRepository{TBase}"/> decorator on top of it.
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
}