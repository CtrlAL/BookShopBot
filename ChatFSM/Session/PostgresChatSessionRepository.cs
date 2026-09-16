using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Fsm.Session;

/// <summary>
/// PostgreSQL store over the table named in
/// <see cref="SessionStorageOptions"/> (default <c>chatfsm.chat_session</c>
/// with columns chat_id bigint PK, payload jsonb, expires_at timestamptz,
/// updated_at timestamptz). Reads and upserts use JSONB payloads serialized
/// with <c>System.Text.Json</c> and always parameterized SQL. Rows whose
/// <c>expires_at</c> has passed are treated as absent.
/// </summary>
public sealed class PostgresChatSessionRepository<TSession> : IChatSessionRepository<TSession>
    where TSession : class
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly PropertyInfo? ChatIdProperty =
        typeof(TSession).GetProperty("ChatId", BindingFlags.Instance | BindingFlags.Public);

    private readonly NpgsqlDataSource _dataSource;
    private readonly SessionStorageOptions _options;

    public PostgresChatSessionRepository(NpgsqlDataSource dataSource)
        : this(dataSource, Options.Create(new SessionStorageOptions()))
    {
    }

    public PostgresChatSessionRepository(NpgsqlDataSource dataSource, IOptions<SessionStorageOptions> options)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
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
        await using NpgsqlCommand command = _dataSource.CreateCommand(BuildUpsertSql(_options));
        command.Parameters.AddWithValue("chat_id", chatId);
        command.Parameters.Add(new NpgsqlParameter("payload", NpgsqlDbType.Jsonb) { Value = Serialize(session) });
        command.Parameters.Add(new NpgsqlParameter("expires_at", NpgsqlDbType.TimestampTz) { Value = BuildExpiresAt(_options) });
        command.Parameters.Add(new NpgsqlParameter("updated_at", NpgsqlDbType.TimestampTz) { Value = DateTime.UtcNow });

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task RemoveAsync(long chatId)
    {
        await using NpgsqlCommand command = _dataSource.CreateCommand(BuildDeleteSql(_options));
        command.Parameters.AddWithValue("chat_id", chatId);

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(long chatId)
    {
        TSession? existing = await ReadAsync(chatId).ConfigureAwait(false);

        return existing is not null;
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

    /// <summary>
    /// Computes the row <c>expires_at</c>: <c>now + BaseLifetime + jitter</c>
    /// where jitter is a uniform value in <c>[0, LifetimeJitter)</c>. A zero
    /// jitter yields exactly <c>now + BaseLifetime</c>. Exposed as a pure
    /// function so the bounds can be verified in tests without a database.
    /// </summary>
    public static DateTime BuildExpiresAt(SessionStorageOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        double jitterSeconds = options.LifetimeJitter.TotalSeconds * Random.Shared.NextDouble();

        return DateTime.UtcNow
            .AddSeconds(options.BaseLifetime.TotalSeconds)
            .AddSeconds(jitterSeconds);
    }

    /// <summary>
    /// Builds the upsert statement using the configured schema/table.
    /// Exposed as a pure function so SQL text can be verified in tests.
    /// </summary>
    public static string BuildUpsertSql(SessionStorageOptions options)
    {
        return $"""
            INSERT INTO {options.Schema}.{options.Table} (chat_id, payload, expires_at, updated_at)
            VALUES (@chat_id, @payload, @expires_at, @updated_at)
            ON CONFLICT (chat_id) DO UPDATE
            SET payload = EXCLUDED.payload,
                expires_at = EXCLUDED.expires_at,
                updated_at = EXCLUDED.updated_at
            """;
    }

    /// <summary>
    /// Builds the select statement: only rows whose <c>expires_at</c> is in
    /// the future are considered present.
    /// </summary>
    public static string BuildSelectSql(SessionStorageOptions options)
    {
        return $"SELECT payload FROM {options.Schema}.{options.Table} WHERE chat_id = @chat_id AND expires_at > now()";
    }

    /// <summary>
    /// Builds the delete-by-chat-id statement.
    /// </summary>
    public static string BuildDeleteSql(SessionStorageOptions options)
    {
        return $"DELETE FROM {options.Schema}.{options.Table} WHERE chat_id = @chat_id";
    }

    private async Task<TSession?> ReadAsync(long chatId)
    {
        await using NpgsqlCommand command = _dataSource.CreateCommand(BuildSelectSql(_options));
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