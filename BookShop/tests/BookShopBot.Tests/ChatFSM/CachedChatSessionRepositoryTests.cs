using Fsm.Session;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BookShopBot.Tests.ChatFSM;

public sealed class CachedChatSessionRepositoryTests
{
    private sealed class CountingStore : IChatSessionRepository<Session<TestState>>
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<long, Session<TestState>> _sessions = new();

        public int GetOrCreateCalls { get; private set; }

        public int SaveCalls { get; private set; }

        public int RemoveCalls { get; private set; }

        public int ExistsCalls { get; private set; }

        public List<long> RemovedChatIds { get; } = [];

        public List<Session<TestState>> SavedSessions { get; } = [];

        public Task<Session<TestState>> GetOrCreateAsync(long chatId)
        {
            GetOrCreateCalls++;
            Session<TestState> session = _sessions.GetOrAdd(chatId, id => new Session<TestState>
            {
                ChatId = id,
                CurrentState = TestState.Menu,
            });
            return Task.FromResult(session);
        }

        public Task SaveAsync(Session<TestState> session)
        {
            SaveCalls++;
            SavedSessions.Add(session);
            _sessions[session.ChatId] = session;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(long chatId)
        {
            RemoveCalls++;
            RemovedChatIds.Add(chatId);
            _sessions.TryRemove(chatId, out _);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(long chatId)
        {
            ExistsCalls++;
            return Task.FromResult(_sessions.ContainsKey(chatId));
        }
    }

    private static MemoryCache CreateCache()
    {
        return new MemoryCache(new MemoryCacheOptions());
    }

    private static CachedChatSessionRepository<Session<TestState>> CreateRepository(
        CountingStore store,
        IMemoryCache cache,
        SessionCacheOptions? options = null)
    {
        return new CachedChatSessionRepository<Session<TestState>>(
            store,
            cache,
            Options.Create(options ?? new SessionCacheOptions()),
            NullLogger<CachedChatSessionRepository<Session<TestState>>>.Instance);
    }

    [Fact]
    public async Task GetOrCreate_with_warm_cache_does_not_hit_store()
    {
        using MemoryCache cache = CreateCache();
        var store = new CountingStore();
        var repository = CreateRepository(store, cache);

        var first = await repository.GetOrCreateAsync(100);
        var second = await repository.GetOrCreateAsync(100);

        Assert.Same(first, second);
        Assert.Equal(1, store.GetOrCreateCalls);
    }

    [Fact]
    public async Task GetOrCreate_on_miss_calls_store_exactly_once_and_populates_cache()
    {
        using MemoryCache cache = CreateCache();
        var store = new CountingStore();
        var repository = CreateRepository(store, cache);

        await repository.GetOrCreateAsync(100);
        await repository.GetOrCreateAsync(100);
        await repository.GetOrCreateAsync(100);

        Assert.Equal(1, store.GetOrCreateCalls);
    }

    [Fact]
    public async Task SaveAsync_writes_through_to_store_and_cache()
    {
        using MemoryCache cache = CreateCache();
        var store = new CountingStore();
        var repository = CreateRepository(store, cache);
        var session = new Session<TestState>
        {
            ChatId = 5,
            CurrentState = TestState.Checkout,
            CurrentPage = 3,
        };

        await repository.SaveAsync(session);

        Assert.Equal(1, store.SaveCalls);
        Assert.Contains(session, store.SavedSessions);

        var cached = await repository.GetOrCreateAsync(5);

        Assert.Same(session, cached);
        Assert.Equal(0, store.GetOrCreateCalls);
    }

    [Fact]
    public async Task RemoveAsync_clears_store_and_cache()
    {
        using MemoryCache cache = CreateCache();
        var store = new CountingStore();
        var repository = CreateRepository(store, cache);

        await repository.GetOrCreateAsync(9);
        await repository.RemoveAsync(9);

        Assert.Equal(1, store.RemoveCalls);
        Assert.Contains(9L, store.RemovedChatIds);

        await repository.GetOrCreateAsync(9);

        Assert.Equal(2, store.GetOrCreateCalls);
    }

    [Fact]
    public async Task ExistsAsync_with_warm_cache_does_not_hit_store()
    {
        using MemoryCache cache = CreateCache();
        var store = new CountingStore();
        var repository = CreateRepository(store, cache);

        await repository.GetOrCreateAsync(11);

        var exists = await repository.ExistsAsync(11);

        Assert.True(exists);
        Assert.Equal(0, store.ExistsCalls);
    }

    [Fact]
    public async Task ExistsAsync_on_miss_defers_to_store_and_caches_result()
    {
        using MemoryCache cache = CreateCache();
        var store = new CountingStore();
        var repository = CreateRepository(store, cache);

        var first = await repository.ExistsAsync(22);
        var second = await repository.ExistsAsync(22);

        Assert.False(first);
        Assert.False(second);
        Assert.Equal(1, store.ExistsCalls);
    }

    [Fact]
    public async Task Eviction_from_memory_cache_triggers_store_reload_on_next_get()
    {
        using MemoryCache cache = CreateCache();
        var store = new CountingStore();
        var repository = CreateRepository(store, cache);

        await repository.GetOrCreateAsync(33);
        Assert.Equal(1, store.GetOrCreateCalls);

        cache.Compact(1.0);

        await repository.GetOrCreateAsync(33);

        Assert.Equal(2, store.GetOrCreateCalls);
    }

    [Fact]
    public async Task Manual_remove_of_cache_entry_triggers_store_reload_on_next_get()
    {
        using MemoryCache cache = CreateCache();
        var store = new CountingStore();
        var repository = CreateRepository(store, cache);

        await repository.GetOrCreateAsync(44);
        Assert.Equal(1, store.GetOrCreateCalls);

        cache.Remove($"chat_session:{44}");

        await repository.GetOrCreateAsync(44);

        Assert.Equal(2, store.GetOrCreateCalls);
    }

    [Fact]
    public async Task Ttl_from_options_is_applied_and_entry_stays_evictable()
    {
        using MemoryCache cache = CreateCache();
        var store = new CountingStore();
        var options = new SessionCacheOptions
        {
            SlidingExpiration = TimeSpan.FromSeconds(30),
            AbsoluteExpiration = TimeSpan.FromSeconds(30),
        };
        var repository = CreateRepository(store, cache, options);

        await repository.GetOrCreateAsync(55);
        Assert.Equal(1, store.GetOrCreateCalls);

        cache.Compact(1.0);

        await repository.GetOrCreateAsync(55);

        Assert.Equal(2, store.GetOrCreateCalls);
    }

    [Fact]
    public async Task AbsoluteExpiration_expires_entry_and_forces_store_reload()
    {
        using MemoryCache cache = CreateCache();
        var store = new CountingStore();
        var options = new SessionCacheOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(20),
            AbsoluteExpiration = TimeSpan.FromMilliseconds(100),
        };
        var repository = CreateRepository(store, cache, options);

        await repository.GetOrCreateAsync(66);
        Assert.Equal(1, store.GetOrCreateCalls);

        await Task.Delay(TimeSpan.FromMilliseconds(250));

        await repository.GetOrCreateAsync(66);

        Assert.Equal(2, store.GetOrCreateCalls);
    }

    [Fact]
    public async Task Eviction_of_session_with_ActionCts_cancels_and_disposes_it()
    {
        using MemoryCache cache = CreateCache();
        var store = new CountingStore();
        var repository = CreateRepository(store, cache);

        var session = await repository.GetOrCreateAsync(77);
        var cts = new CancellationTokenSource();
        session.ActionCts = cts;

        await repository.SaveAsync(session);

        cache.Compact(1.0);

        Assert.True(SpinWait.SpinUntil(() => cts.IsCancellationRequested, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Manual_remove_of_session_with_ActionCts_cancels_and_disposes_it()
    {
        using MemoryCache cache = CreateCache();
        var store = new CountingStore();
        var repository = CreateRepository(store, cache);

        var session = await repository.GetOrCreateAsync(88);
        var cts = new CancellationTokenSource();
        session.ActionCts = cts;

        await repository.SaveAsync(session);

        cache.Remove("chat_session:88");

        Assert.True(SpinWait.SpinUntil(() => cts.IsCancellationRequested, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Eviction_of_session_with_null_ActionCts_does_not_throw()
    {
        using MemoryCache cache = CreateCache();
        var store = new CountingStore();
        var repository = CreateRepository(store, cache);

        await repository.GetOrCreateAsync(99);

        cache.Compact(1.0);
    }
}