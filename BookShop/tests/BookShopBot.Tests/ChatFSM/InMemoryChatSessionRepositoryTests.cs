using Fsm.Session;
using Xunit;

namespace BookShopBot.Tests.ChatFSM;

public enum TestState
{
    Idle = 0,
    Menu = 1,
    Checkout = 2,
}

public sealed class InMemoryChatSessionRepositoryTests
{
    private static Session<TestState> CreateSession(long chatId)
    {
        return new Session<TestState>
        {
            ChatId = chatId,
            CurrentState = TestState.Menu,
            PreviousState = TestState.Idle,
            CurrentPage = 3,
            PagedMessageId = 17,
        };
    }

    [Fact]
    public async Task GetOrCreate_creates_single_instance_per_chatId_and_returns_same_instance()
    {
        var repository = new InMemoryChatSessionRepository<Session<TestState>>();

        var first = await repository.GetOrCreateAsync(42);
        var second = await repository.GetOrCreateAsync(42);

        Assert.Same(first, second);
        Assert.Equal(42, first.ChatId);
    }

    [Fact]
    public async Task GetOrCreate_returns_distinct_instances_for_distinct_chatIds()
    {
        var repository = new InMemoryChatSessionRepository<Session<TestState>>();

        var first = await repository.GetOrCreateAsync(1);
        var second = await repository.GetOrCreateAsync(2);

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task Save_then_Get_roundtrips_the_saved_instance()
    {
        var repository = new InMemoryChatSessionRepository<Session<TestState>>();
        var session = CreateSession(7);

        await repository.SaveAsync(session);

        var roundtrip = await repository.GetOrCreateAsync(session.ChatId);

        Assert.Same(session, roundtrip);
        Assert.Equal(TestState.Menu, roundtrip.CurrentState);
        Assert.Equal(TestState.Idle, roundtrip.PreviousState);
        Assert.Equal(3, roundtrip.CurrentPage);
        Assert.Equal(17, roundtrip.PagedMessageId);
    }

    [Fact]
    public async Task Exists_reflects_current_store_state()
    {
        var repository = new InMemoryChatSessionRepository<Session<TestState>>();

        Assert.False(await repository.ExistsAsync(9));

        await repository.GetOrCreateAsync(9);

        Assert.True(await repository.ExistsAsync(9));

        await repository.RemoveAsync(9);

        Assert.False(await repository.ExistsAsync(9));
    }

    [Fact]
    public async Task Remove_clears_entry_so_next_GetOrCreate_builds_a_new_instance()
    {
        var repository = new InMemoryChatSessionRepository<Session<TestState>>();
        var original = await repository.GetOrCreateAsync(5);

        await repository.RemoveAsync(5);

        var recreated = await repository.GetOrCreateAsync(5);

        Assert.NotSame(original, recreated);
    }
}