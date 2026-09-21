using Fsm.Session;
using Xunit;

namespace BookShopBot.Tests.ChatFSM;

public sealed class SessionCacheOptionsTests
{
    [Fact]
    public void Defaults_match_optimal_ttl_for_marketplace_workflow()
    {
        var options = new SessionCacheOptions();

        Assert.Equal(TimeSpan.FromMinutes(20), options.SlidingExpiration);
        Assert.Equal(TimeSpan.FromHours(2), options.AbsoluteExpiration);
    }
}