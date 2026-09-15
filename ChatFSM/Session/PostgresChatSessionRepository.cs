using System.Reflection;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Fsm.Session;

/// <summary>
/// PostgreSQL store over table <c>chat_sessions(chat_id bigint PK, payload
/// jsonb, updated_at timestamptz)</c>. Reads and upserts use JSONB payloads
/// serialized with <c>System.Text.Json</c> and always parameterized SQL.
/// </summary>
public sealed class PostgresChatSessionRepository<TSession> : IChatSessionRepository<TSession>
    where TSession : class
{
    private const string UpsertSql = """
        INSERT INTO chat_sessions (chat_id, payload, updated_at)
        VALUES (@chat_id, @payload, @updated_at)
        ON CONFLICT (chat_id) DO UPDATE
        SET payload = EXCLUDED.payload,
            updated_at = EXCLUDED.updated_at
        """;

    private const string SelectSql = "SELECT payload FROM chat_sessions WHERE chat_id = @chat_id";

    private const string DeleteSql = "DELETE FROM chat_sessions WHERE chat_id = @chat_id";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly PropertyInfo? ChatIdProperty =
        typeof(TSession).GetProperty("ChatId", BindingFlags.Instance | BindingFlags.Public);

    private readonly NpgsqlDataSource _dataSource;

    public PostgresChatSessionRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<TSession> GetOrCreateAsync(long chatId)
    {
        TSession? existing = await ReadAsync(chatId).ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        TSession created = (TSession)Activator.CreateInstance(typeof(TSession))!;
        SetChatId(created, chatId);

        return created;
    }

    public async Task SaveAsync(TSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        long chatId = GetChatId(session);
        await using NpgsqlCommand command = _dataSource.CreateCommand(UpsertSql);
        command.Parameters.AddWithValue("chat_id", chatId);
        command.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb) { Value = Serialize(session) });
        command.Parameters.Add(new NpgsqlParameter("updated_at", NpgsqlDbType.TimestampTz) { Value = DateTime.UtcNow });

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task RemoveAsync(long chatId)
    {
        await using NpgsqlCommand command = _dataSource.CreateCommand(DeleteSql);
        command.Parameters.AddWithValue("chat_id", chatId);

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(long chatId)
    {
        await using NpgsqlCommand command = _dataSource.CreateCommand(SelectSql);
        command.Parameters.AddWithValue("chat_id", chatId);

        object? payload = await command.ExecuteScalarAsync().ConfigureAwait(false);

        return payload is not null;
    }

    public static string Serialize(TSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return JsonSerializer.Serialize(session, SerializerOptions);
    }

    public static TSession Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonSerializer.Deserialize<TSession>(json, SerializerOptions)
            ?? throw new JsonException("Unable to deserialize session from JSON payload.");
    }

    private async Task<TSession?> ReadAsync(long chatId)
    {
        await using NpgsqlCommand command = _dataSource.CreateCommand(SelectSql);
        command.Parameters.AddWithValue("chat_id", chatId);

        object? payload = await command.ExecuteScalarAsync().ConfigureAwait(false);

        return payload is string json ? Deserialize(json) : null;
    }

    private static void SetChatId(TSession session, long chatId)
    {
        if (ChatIdProperty?.PropertyType == typeof(long))
        {
            ChatIdProperty.SetValue(session, chatId);
        }
    }

    private static long GetChatId(TSession session)
    {
        if (ChatIdProperty?.PropertyType == typeof(long))
        {
            return (long)ChatIdProperty.GetValue(session)!;
        }

        throw new InvalidOperationException(
            $"Type '{typeof(TSession)}' must expose a public 'long ChatId' property.");
    }
}