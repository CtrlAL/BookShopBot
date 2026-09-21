using System.Reflection;
using Fsm.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace BookShopBot.Tests.ChatFSM;

public sealed class ChatSessionCleanupServiceTests
{
    [Fact]
    public void Default_cleanup_SQL_contains_DELETE_with_expires_at()
    {
        var options = new SessionStorageOptions
        {
            Schema = "chatfsm",
            Table = "chat_session",
        };

        string sql = ChatSessionCleanupService.BuildDeleteSql(options);

        Assert.Contains("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires_at", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("now()", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chatfsm.chat_session", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cleanup_SQL_interpolates_custom_schema_and_table()
    {
        var options = new SessionStorageOptions
        {
            Schema = "myschema",
            Table = "mysessions",
        };

        string sql = ChatSessionCleanupService.BuildDeleteSql(options);

        Assert.Contains("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("myschema.mysessions", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires_at", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("now()", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cleanup_SQL_is_parameterless_and_safe()
    {
        var options = new SessionStorageOptions();
        string sql = ChatSessionCleanupService.BuildDeleteSql(options);

        Assert.DoesNotContain("@", sql);
        Assert.DoesNotContain("{", sql);
        Assert.DoesNotContain("=", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cleanup_SQL_contains_EXPIRES_AT_less_than_now()
    {
        var options = new SessionStorageOptions();
        string sql = ChatSessionCleanupService.BuildDeleteSql(options);

        Assert.Contains("expires_at < now()", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Service_can_be_constructed_with_valid_arguments()
    {
        using var dataSource = NpgsqlDataSource.Create(
            "Host=localhost;Port=5432;Database=none;Username=user;Password=pass");
        var options = Options.Create(new SessionStorageOptions());
        var logger = NullLogger<ChatSessionCleanupService>.Instance;

        var service = new ChatSessionCleanupService(dataSource, options, logger);

        Assert.NotNull(service);
    }
}
