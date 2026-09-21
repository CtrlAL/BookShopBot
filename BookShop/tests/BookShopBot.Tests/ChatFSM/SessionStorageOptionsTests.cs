using Fsm.Session;
using Xunit;

namespace BookShopBot.Tests.ChatFSM;

public sealed class SessionStorageOptionsTests
{
    [Fact]
    public void Defaults_schema_is_chatfsm()
    {
        var options = new SessionStorageOptions();
        Assert.Equal("chatfsm", options.Schema);
    }

    [Fact]
    public void Defaults_table_is_chat_session()
    {
        var options = new SessionStorageOptions();
        Assert.Equal("chat_session", options.Table);
    }

    [Fact]
    public void Defaults_BaseLifetime_is_30_days()
    {
        var options = new SessionStorageOptions();
        Assert.Equal(TimeSpan.FromDays(30), options.BaseLifetime);
    }

    [Fact]
    public void Defaults_LifetimeJitter_is_7_days()
    {
        var options = new SessionStorageOptions();
        Assert.Equal(TimeSpan.FromDays(7), options.LifetimeJitter);
    }

    [Fact]
    public void Defaults_CleanupInterval_is_1_hour()
    {
        var options = new SessionStorageOptions();
        Assert.Equal(TimeSpan.FromHours(1), options.CleanupInterval);
    }

    [Fact]
    public void Properties_are_settable()
    {
        var options = new SessionStorageOptions
        {
            Schema = "custom_schema",
            Table = "custom_table",
            BaseLifetime = TimeSpan.FromDays(10),
            LifetimeJitter = TimeSpan.FromDays(3),
            CleanupInterval = TimeSpan.FromMinutes(30),
        };

        Assert.Equal("custom_schema", options.Schema);
        Assert.Equal("custom_table", options.Table);
        Assert.Equal(TimeSpan.FromDays(10), options.BaseLifetime);
        Assert.Equal(TimeSpan.FromDays(3), options.LifetimeJitter);
        Assert.Equal(TimeSpan.FromMinutes(30), options.CleanupInterval);
    }
}
