using System.Linq;
using BookShop.TelegramBot.Extensions;
using BookShop.TelegramBot.Interfaces;
using BookShop.TelegramBot.Services.HostedServices;
using BookShop.TelegramBot.Services.Implementations;
using Fsm.Session;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Npgsql;
using Xunit;

namespace BookShopBot.Tests.TelegramBot;

public sealed class TelegramBotSessionIntegrationTests
{
    [Fact]
    public async Task AddChatSessionPersistence_registers_full_session_stack()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServices();

        var configuration = new StubConfiguration(
            ("ConnectionStrings:ChatSession", "Host=localhost;Database=bookshop_bot"));

        services.AddChatSessionPersistence(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        var repo = provider.GetRequiredService<IMemoryCacheSessionRepository>();
        Assert.NotNull(repo);
        Assert.IsNotType<MemoryCacheSessionRepository>(repo);

        var cached = provider.GetRequiredService<CachedChatSessionRepository<TelegramChatSession>>();
        Assert.NotNull(cached);

        var store = provider.GetRequiredService<PostgresChatSessionRepository<TelegramChatSession>>();
        Assert.NotNull(store);

        var dataSource = provider.GetRequiredService<NpgsqlDataSource>();
        Assert.NotNull(dataSource);

        var storageOptions = provider.GetRequiredService<IOptions<SessionStorageOptions>>().Value;
        Assert.Equal("chatfsm", storageOptions.Schema);
        Assert.Equal("chat_session", storageOptions.Table);
        Assert.Equal(TimeSpan.FromDays(30), storageOptions.BaseLifetime);
        Assert.Equal(TimeSpan.FromHours(1), storageOptions.CleanupInterval);

        var hosted = provider.GetServices<IHostedService>().ToList();
        Assert.Contains(hosted, x => x.GetType().Name == nameof(SchemaInitializationService));
        Assert.Contains(hosted, x => x.GetType().Name == nameof(ChatSessionCleanupService));

        var cache = provider.GetRequiredService<IMemoryCache>();
        TelegramChatSession seeded = new(12345);
        cache.Set("chat_session:12345", seeded);

        var session = await repo.GetOrCreateAsync(12345);

        Assert.NotNull(session);
        Assert.Same(seeded, session);
        Assert.Equal(12345L, session.ChatId);

        var fromCache = await cached.GetOrCreateAsync(12345);
        Assert.Same(seeded, fromCache);
    }

    [Fact]
    public void AddServices_alone_does_not_register_memory_session_repository()
    {
        var services = new ServiceCollection();
        services.AddServices();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IMemoryCacheSessionRepository>());
    }

    private sealed class StubConfiguration : IConfiguration
    {
        private readonly IReadOnlyDictionary<string, string> _values;

        public StubConfiguration(params (string Key, string Value)[] values)
        {
            _values = values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        }

        public string? this[string key]
        {
            get => _values.TryGetValue(key, out string? value) ? value : null;
            set => throw new NotSupportedException("Stub configuration is read-only.");
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            yield break;
        }

        public IChangeToken GetReloadToken()
        {
            return StubChangeToken.Instance;
        }

        public IConfigurationSection GetSection(string key)
        {
            return new StubSection(this, key);
        }

        private sealed class StubSection : IConfigurationSection
        {
            private readonly StubConfiguration _configuration;
            private readonly string _key;

            public StubSection(StubConfiguration configuration, string key)
            {
                _configuration = configuration;
                _key = key;
            }

            public string? this[string key]
            {
                get => _configuration[Combine(_key, key)];
                set => throw new NotSupportedException("Stub configuration is read-only.");
            }

            public string Key => _key;

            public string Path => _key;

            public string? Value
            {
                get => _configuration[_key];
                set => throw new NotSupportedException("Stub configuration is read-only.");
            }

            public IEnumerable<IConfigurationSection> GetChildren()
            {
                return _configuration.GetChildren();
            }

            public IChangeToken GetReloadToken()
            {
                return StubChangeToken.Instance;
            }

            public IConfigurationSection GetSection(string key)
            {
                return _configuration.GetSection(Combine(_key, key));
            }

            private static string Combine(string parent, string child)
            {
                return parent + ConfigurationPath.KeyDelimiter + child;
            }
        }
    }

    private sealed class StubChangeToken : IChangeToken
    {
        public static readonly StubChangeToken Instance = new();

        public bool HasChanged => false;

        public bool ActiveChangeCallbacks => false;

        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
        {
            return StubDisposable.Instance;
        }

        private sealed class StubDisposable : IDisposable
        {
            public static readonly StubDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}