using System.Text.Json;
using Fsm.Session;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace BookShopBot.Tests.ChatFSM;

public sealed class PostgresChatSessionRepositoryTests
{
    [Fact]
    public void Serialize_roundtrips_core_properties_and_ignores_action_cts()
    {
        var session = new Session<TestState>
        {
            ChatId = 777,
            CurrentState = TestState.Checkout,
            PreviousState = TestState.Menu,
            CurrentPage = 4,
            PagedMessageId = 123,
            ActionCts = new CancellationTokenSource(),
        };

        string json = PostgresChatSessionRepository<Session<TestState>>.Serialize(session);

        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.Equal(777, root.GetProperty("chatId").GetInt64());
        Assert.Equal((int)TestState.Checkout, root.GetProperty("currentState").GetInt32());
        Assert.Equal((int)TestState.Menu, root.GetProperty("previousState").GetInt32());
        Assert.Equal(4, root.GetProperty("currentPage").GetInt32());
        Assert.Equal(123, root.GetProperty("pagedMessageId").GetInt32());
        Assert.False(root.TryGetProperty("actionCts", out _));

        var roundtrip = PostgresChatSessionRepository<Session<TestState>>.Deserialize(json);

        Assert.Equal(777, roundtrip.ChatId);
        Assert.Equal(TestState.Checkout, roundtrip.CurrentState);
        Assert.Equal(TestState.Menu, roundtrip.PreviousState);
        Assert.Equal(4, roundtrip.CurrentPage);
        Assert.Equal(123, roundtrip.PagedMessageId);
        Assert.Null(roundtrip.ActionCts);
    }

    [Fact]
    public void Serialize_omits_unsupported_action_cts_from_jsonb_payload()
    {
        var session = new Session<TestState>
        {
            ChatId = 778,
            CurrentState = TestState.Idle,
            PreviousState = TestState.Idle,
            ActionCts = new CancellationTokenSource(),
        };

        string json = PostgresChatSessionRepository<Session<TestState>>.Serialize(session);

        Assert.DoesNotContain("ActionCts", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sql_texts_use_parameters_and_no_injection_formattings()
    {
        var options = new SessionStorageOptions();
        var sqlTexts = new Dictionary<string, string>
        {
            ["UpsertSql"] = PostgresChatSessionRepository<Session<TestState>>.BuildUpsertSql(options),
            ["SelectSql"] = PostgresChatSessionRepository<Session<TestState>>.BuildSelectSql(options),
            ["DeleteSql"] = PostgresChatSessionRepository<Session<TestState>>.BuildDeleteSql(options),
        };

        Assert.Equal(3, sqlTexts.Count);

        Assert.All(sqlTexts, pair =>
        {
            Assert.NotEmpty(pair.Value);
            Assert.Contains("@", pair.Value);
            Assert.DoesNotContain("{", pair.Value);
        });

        Assert.Contains("INSERT INTO", sqlTexts["UpsertSql"]);
        Assert.Contains("ON CONFLICT (chat_id) DO UPDATE", sqlTexts["UpsertSql"]);
        Assert.Contains("@chat_id", sqlTexts["UpsertSql"]);
        Assert.Contains("@payload", sqlTexts["UpsertSql"]);
        Assert.Contains("@updated_at", sqlTexts["UpsertSql"]);
        Assert.Contains("@expires_at", sqlTexts["UpsertSql"]);

        Assert.Contains("SELECT", sqlTexts["SelectSql"]);
        Assert.Contains("@chat_id", sqlTexts["SelectSql"]);
        Assert.Contains("expires_at", sqlTexts["SelectSql"]);
        Assert.Contains("now()", sqlTexts["SelectSql"]);

        Assert.Contains("DELETE FROM", sqlTexts["DeleteSql"]);
        Assert.Contains("@chat_id", sqlTexts["DeleteSql"]);
    }

    [Fact]
    public void Repository_accepts_npgsql_data_source()
    {
        using var dataSource = NpgsqlDataSource.Create(
            "Host=localhost;Port=5432;Database=none;Username=user;Password=pass");

        var repository = new PostgresChatSessionRepository<Session<TestState>>(dataSource);

        Assert.NotNull(repository);
    }

    [Fact]
    public void Repository_accepts_npgsql_data_source_and_options()
    {
        using var dataSource = NpgsqlDataSource.Create(
            "Host=localhost;Port=5432;Database=none;Username=user;Password=pass");
        var options = Options.Create(new SessionStorageOptions());

        var repository = new PostgresChatSessionRepository<Session<TestState>>(dataSource, options);

        Assert.NotNull(repository);
    }

    [Fact]
    public void BuildExpiresAt_with_zero_jitter_returns_exact_base_lifetime()
    {
        var options = new SessionStorageOptions
        {
            BaseLifetime = TimeSpan.FromDays(30),
            LifetimeJitter = TimeSpan.Zero,
        };

        DateTime before = DateTime.UtcNow + options.BaseLifetime;
        DateTime expiresAt = PostgresChatSessionRepository<Session<TestState>>.BuildExpiresAt(options);
        DateTime after = before + TimeSpan.FromMilliseconds(100);

        Assert.Equal(before, expiresAt, TimeSpan.FromMilliseconds(50));
        Assert.InRange(expiresAt, before, after);
    }

    [Fact]
    public void BuildExpiresAt_with_jitter_returns_value_in_bounds()
    {
        var options = new SessionStorageOptions
        {
            BaseLifetime = TimeSpan.FromDays(30),
            LifetimeJitter = TimeSpan.FromDays(7),
        };

        DateTime minExpected = DateTime.UtcNow + options.BaseLifetime;
        DateTime maxExpected = minExpected + options.LifetimeJitter;
        DateTime expiresAt = PostgresChatSessionRepository<Session<TestState>>.BuildExpiresAt(options);

        Assert.InRange(expiresAt, minExpected, maxExpected);
    }

    [Fact]
    public void UpsertSql_contains_expires_at_parameter()
    {
        var options = new SessionStorageOptions();
        string sql = PostgresChatSessionRepository<Session<TestState>>.BuildUpsertSql(options);

        Assert.Contains("@expires_at", sql);
        Assert.Contains("expires_at", sql);
    }

    [Fact]
    public void SelectSql_filters_out_expired_rows()
    {
        var options = new SessionStorageOptions();
        string sql = PostgresChatSessionRepository<Session<TestState>>.BuildSelectSql(options);

        Assert.Contains("expires_at", sql);
        Assert.Contains("now()", sql);
    }

    [Fact]
    public void SelectSql_uses_options_schema_and_table()
    {
        string sql = PostgresChatSessionRepository<Session<TestState>>.BuildSelectSql(
            new SessionStorageOptions { Schema = "chatfsm", Table = "chat_session" });

        Assert.Contains("chatfsm.chat_session", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires_at > now()", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpsertSql_uses_options_schema_and_table()
    {
        string sql = PostgresChatSessionRepository<Session<TestState>>.BuildUpsertSql(
            new SessionStorageOptions { Schema = "chatfsm", Table = "chat_session" });

        Assert.Contains("chatfsm.chat_session", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@expires_at", sql);
    }

    [Fact]
    public void DeleteSql_uses_options_schema_and_table()
    {
        string sql = PostgresChatSessionRepository<Session<TestState>>.BuildDeleteSql(
            new SessionStorageOptions { Schema = "chatfsm", Table = "chat_session" });

        Assert.Contains("chatfsm.chat_session", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@chat_id", sql);
    }
}