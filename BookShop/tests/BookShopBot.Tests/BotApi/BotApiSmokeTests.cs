using BookShop.S3Tool;
using BookShop.S3Tool.Interfaces;
using BookShop.TelegramBot.Extensions;
using BookShop.TelegramBot.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Telegram.Bot;
using Xunit;

namespace BookShopBot.Tests.BotApi;

public sealed class BotApiSmokeTests
{
    [Fact]
    public void Full_bot_di_graph_resolves_without_external_services()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new TestConfiguration(
            ("BotConfig:Token", "110201543:AAHdqTcvCH1vGWJxfSeofSAs0K5PALDsaw"),
            ("S3:Endpoint", "http://localhost:9000"),
            ("S3:Bucket", "bookshop"),
            ("S3:AccessKey", "minioadmin"),
            ("S3:SecretKey", "minioadmin"),
            ("ConnectionStrings:ChatSession", "Host=localhost;Database=bookshop_bot"));

        services.AddTelegramBotClient(configuration.GetSection("BotConfig"));
        services.AddChatSessionPersistence(configuration);
        services.AddS3Client(configuration.GetSection("S3"));
        services.AddServices();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ITelegramBotClient>());
        Assert.NotNull(provider.GetRequiredService<IS3Service>());
        Assert.NotNull(provider.GetRequiredService<IMemoryCacheSessionRepository>());
    }

    private sealed class TestConfiguration : IConfiguration
    {
        private readonly IReadOnlyDictionary<string, string?> _values;

        public TestConfiguration(params (string Key, string? Value)[] entries)
        {
            _values = entries.ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
        }

        public string? this[string key]
        {
            get => _values.TryGetValue(key, out string? value) ? value : null;
            set => throw new NotSupportedException("Test configuration is read-only.");
        }

        public IEnumerable<IConfigurationSection> GetChildren() => GetChildren(string.Empty);

        public IChangeToken GetReloadToken() => TestChangeToken.Instance;

        public IConfigurationSection GetSection(string key) => new TestSection(this, key, key);

        private IEnumerable<IConfigurationSection> GetChildren(string parentPath)
        {
            var delimiter = ConfigurationPath.KeyDelimiter;
            var prefix = string.IsNullOrEmpty(parentPath) ? string.Empty : parentPath + delimiter;

            var segments = _values.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(k => k.Substring(prefix.Length).Split(delimiter[0], 2)[0])
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var segment in segments)
            {
                yield return new TestSection(this, prefix + segment, segment);
            }
        }

        private sealed class TestSection : IConfigurationSection
        {
            private readonly TestConfiguration _configuration;

            public TestSection(TestConfiguration configuration, string path, string key)
            {
                _configuration = configuration;
                Path = path;
                Key = key;
            }

            public string? this[string key]
            {
                get => _configuration[Combine(Path, key)];
                set => throw new NotSupportedException("Test configuration is read-only.");
            }

            public string Key { get; }

            public string Path { get; }

            public string? Value
            {
                get => _configuration[Path];
                set => throw new NotSupportedException("Test configuration is read-only.");
            }

            public IEnumerable<IConfigurationSection> GetChildren() => _configuration.GetChildren(Path);

            public IChangeToken GetReloadToken() => TestChangeToken.Instance;

            public IConfigurationSection GetSection(string key) => new TestSection(_configuration, Combine(Path, key), key);

            private static string Combine(string parentPath, string key) =>
                parentPath + ConfigurationPath.KeyDelimiter + key;
        }
    }

    private sealed class TestChangeToken : IChangeToken
    {
        public static readonly TestChangeToken Instance = new();

        public bool HasChanged => false;

        public bool ActiveChangeCallbacks => false;

        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => TestDisposable.Instance;

        private sealed class TestDisposable : IDisposable
        {
            public static readonly TestDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}