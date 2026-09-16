using Microsoft.Extensions.Options;
using Npgsql;

namespace Fsm.Session;

/// <summary>
/// Idempotent schema initializer for the chat session table.
/// Creates the schema (<c>CREATE SCHEMA IF NOT EXISTS</c>) and table
/// (<c>CREATE TABLE IF NOT EXISTS</c>) using the configured names from
/// <see cref="SessionStorageOptions"/>. No migrations, no versioning —
/// the schema is fixed JSONB payload storage.
/// </summary>
public static class ChatSessionSchema
{
    /// <summary>
    /// Ensures that the chat session schema and table exist.
    /// Idempotent; fast-fails on errors.
    /// </summary>
    public static async Task EnsureCreatedAsync(
        NpgsqlDataSource dataSource,
        IOptions<SessionStorageOptions> options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(options);

        string sql = BuildEnsureCreatedSql(options.Value);
        await using NpgsqlCommand command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the combined DDL statement for schema + table creation.
    /// Exposed as a pure function so SQL text can be verified in tests
    /// without a live database connection.
    /// </summary>
    public static string BuildEnsureCreatedSql(SessionStorageOptions options)
    {
        return $"""
            CREATE SCHEMA IF NOT EXISTS {options.Schema};
            CREATE TABLE IF NOT EXISTS {options.Schema}.{options.Table} (
                chat_id    bigint      PRIMARY KEY,
                payload    jsonb       NOT NULL,
                expires_at timestamptz NOT NULL,
                updated_at timestamptz NOT NULL
            );
            """;
    }
}
