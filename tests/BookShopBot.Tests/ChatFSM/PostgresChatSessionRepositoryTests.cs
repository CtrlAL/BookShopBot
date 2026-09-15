using System.Reflection;
using System.Text.Json;
using Fsm.Session;
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
        Dictionary<string, string> sqlTexts = GetSqlConstants();

        Assert.Equal(3, sqlTexts.Count);

        Assert.All(sqlTexts, pair =>
        {
            Assert.NotEmpty(pair.Value);
            Assert.Contains("@", pair.Value);
            Assert.DoesNotContain("{", pair.Value);
        });

        Assert.Contains("INSERT INTO chat_sessions", sqlTexts["UpsertSql"]);
        Assert.Contains("(chat_id, payload, updated_at)", sqlTexts["UpsertSql"]);
        Assert.Contains("ON CONFLICT (chat_id) DO UPDATE", sqlTexts["UpsertSql"]);
        Assert.Contains("@chat_id", sqlTexts["UpsertSql"]);
        Assert.Contains("@payload", sqlTexts["UpsertSql"]);
        Assert.Contains("@updated_at", sqlTexts["UpsertSql"]);

        Assert.Contains("SELECT payload FROM chat_sessions", sqlTexts["SelectSql"]);
        Assert.Contains("@chat_id", sqlTexts["SelectSql"]);

        Assert.Contains("DELETE FROM chat_sessions", sqlTexts["DeleteSql"]);
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

    private static Dictionary<string, string> GetSqlConstants()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        Type repositoryType = typeof(PostgresChatSessionRepository<Session<TestState>>);

        return repositoryType
            .GetFields(flags)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .ToDictionary(field => field.Name, field => (string)field.GetValue(null)!);
    }
}