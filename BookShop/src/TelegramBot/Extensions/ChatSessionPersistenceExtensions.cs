using BookShop.TelegramBot.Interfaces;
using BookShop.TelegramBot.Services.HostedServices;
using BookShop.TelegramBot.Services.Implementations;
using Fsm.Session;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace BookShop.TelegramBot.Extensions
{
    public static class ChatSessionPersistenceExtensions
    {
        public static IServiceCollection AddChatSessionPersistence(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            string connectionString = configuration.GetConnectionString("ChatSession")
                ?? "Host=localhost;Database=bookshop_bot";

            services.AddSingleton<NpgsqlDataSource>(_ => NpgsqlDataSource.Create(connectionString));

            services.AddOptions<SessionStorageOptions>();
            services.AddOptions<SessionCacheOptions>();

            services.AddMemoryCache();

            services.AddScoped<PostgresChatSessionRepository<TelegramChatSession>>();
            services.AddScoped<CachedChatSessionRepository<TelegramChatSession>>(serviceProvider =>
                new CachedChatSessionRepository<TelegramChatSession>(
                    serviceProvider.GetRequiredService<PostgresChatSessionRepository<TelegramChatSession>>(),
                    serviceProvider.GetRequiredService<IMemoryCache>(),
                    serviceProvider.GetRequiredService<IOptions<SessionCacheOptions>>(),
                    serviceProvider.GetRequiredService<ILogger<CachedChatSessionRepository<TelegramChatSession>>>()));
            services.AddScoped<IMemoryCacheSessionRepository>(serviceProvider =>
                new CachedChatSessionRepositoryAdapter(
                    serviceProvider.GetRequiredService<CachedChatSessionRepository<TelegramChatSession>>()));

            services.AddHostedService<SchemaInitializationService>();
            services.AddHostedService<ChatSessionCleanupService>();

            return services;
        }

        /// <summary>
        /// Exposes the ChatFSM <see cref="CachedChatSessionRepository{TSession}"/>
        /// cache-aside wrapper through the bot's
        /// <see cref="IMemoryCacheSessionRepository"/> contract.
        /// </summary>
        private sealed class CachedChatSessionRepositoryAdapter : IMemoryCacheSessionRepository
        {
            private readonly CachedChatSessionRepository<TelegramChatSession> _inner;

            public CachedChatSessionRepositoryAdapter(
                CachedChatSessionRepository<TelegramChatSession> inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public Task<TelegramChatSession> GetOrCreateAsync(long chatId) => _inner.GetOrCreateAsync(chatId);

            public Task SaveAsync(TelegramChatSession session) => _inner.SaveAsync(session);

            public Task RemoveAsync(long chatId) => _inner.RemoveAsync(chatId);

            public Task<bool> ExistsAsync(long chatId) => _inner.ExistsAsync(chatId);
        }
    }
}