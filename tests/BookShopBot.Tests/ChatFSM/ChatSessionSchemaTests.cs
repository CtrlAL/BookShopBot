using System.Text.RegularExpressions;
using Fsm.Session;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace BookShopBot.Tests.ChatFSM;

public sealed class ChatSessionSchemaTests
{
    private static NpgsqlDataSource CreateFakeDataSource()
    {
        return NpgsqlDataSource.Create("Host=localhost;Port=5432;Database=none;Username=user;Password=pass");
    }

    private static SessionStorageOptions CreateOptions(string? schema = null, string? table = null)
    {
        var options = new SessionStorageOptions();
        if (schema is not null) options.Schema = schema;
        if (table is not null) options.Table = table;
        return options;
    }

    [Fact]
    public async Task EnsureCreatedAsync_builds_correct_SQL_with_default_schema_and_table()
    {
        using var dataSource = CreateFakeDataSource();
        var options = CreateOptions();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await Assert.ThrowsAnyAsync<Exception>(
            () => ChatSessionSchema.EnsureCreatedAsync(dataSource, Options.Create(options), cts.Token));
    }

    [Fact]
    public async Task EnsureCreatedAsync_builds_correct_SQL_with_custom_schema_and_table()
    {
        using var dataSource = CreateFakeDataSource();
        var options = CreateOptions(schema: "myschema", table: "mysessions");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await Assert.ThrowsAnyAsync<Exception>(
            () => ChatSessionSchema.EnsureCreatedAsync(dataSource, Options.Create(options), cts.Token));
    }

    [Fact]
    public void Default_SQL_contains_CREATE_SCHEMA_IF_NOT_EXISTS_chatfsm()
    {
        string sql = ChatSessionSchema.BuildEnsureCreatedSql(
            new SessionStorageOptions { Schema = "chatfsm", Table = "chat_session" });

        Assert.Contains("CREATE SCHEMA IF NOT EXISTS chatfsm", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Default_SQL_contains_CREATE_TABLE_IF_NOT_EXISTS_chatfsm_chat_session()
    {
        string sql = ChatSessionSchema.BuildEnsureCreatedSql(
            new SessionStorageOptions { Schema = "chatfsm", Table = "chat_session" });

        Assert.Contains("CREATE TABLE IF NOT EXISTS chatfsm.chat_session", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SQL_interpolates_custom_schema_and_table()
    {
        string sql = ChatSessionSchema.BuildEnsureCreatedSql(
            new SessionStorageOptions { Schema = "myschema", Table = "mysessions" });

        Assert.Contains("CREATE SCHEMA IF NOT EXISTS myschema", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE IF NOT EXISTS myschema.mysessions", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SQL_contains_expires_at_column()
    {
        string sql = ChatSessionSchema.BuildEnsureCreatedSql(
            new SessionStorageOptions { Schema = "chatfsm", Table = "chat_session" });

        Assert.Contains("expires_at", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SQL_contains_payload_jsonb_column()
    {
        string sql = ChatSessionSchema.BuildEnsureCreatedSql(
            new SessionStorageOptions { Schema = "chatfsm", Table = "chat_session" });

        Assert.Contains("payload", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("jsonb", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SQL_contains_updated_at_column()
    {
        string sql = ChatSessionSchema.BuildEnsureCreatedSql(
            new SessionStorageOptions { Schema = "chatfsm", Table = "chat_session" });

        Assert.Contains("updated_at", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SQL_contains_chat_id_primary_key()
    {
        string sql = ChatSessionSchema.BuildEnsureCreatedSql(
            new SessionStorageOptions { Schema = "chatfsm", Table = "chat_session" });

        Assert.Contains("chat_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRIMARY KEY", sql, StringComparison.OrdinalIgnoreCase);
    }
}
