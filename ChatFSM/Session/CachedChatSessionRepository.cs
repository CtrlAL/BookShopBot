using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fsm.Session;

/// <summary>
/// Cache-aside decorator over <see cref="IChatSessionRepository{TSession}"/>:
/// GetOrCreateAsync is read-through, SaveAsync is write-through, RemoveAsync
/// clears both layers and ExistsAsync is served from the cache when warm.
/// A post-eviction IMemoryCache callback performs no persistence (SaveAsync
/// already keeps the store in sync) but cancels and disposes the session's
/// <c>ActionCts</c> so background operation timers do not leak.
/// </summary>
public sealed class CachedChatSessionRepository<TSession> : IChatSessionRepository<TSession>
    where TSession : class
{
    private static readonly PropertyInfo? ChatIdProperty =
        typeof(TSession).GetProperty("ChatId", BindingFlags.Instance | BindingFlags.Public);

    private static readonly PropertyInfo? ActionCtsProperty =
        typeof(TSession).GetProperty("ActionCts", BindingFlags.Instance | BindingFlags.Public);

    private readonly IChatSessionRepository<TSession> _store;
    private readonly IMemoryCache _cache;
    private readonly SessionCacheOptions _options;
    private readonly ILogger<CachedChatSessionRepository<TSession>> _logger;

    public CachedChatSessionRepository(
        IChatSessionRepository<TSession> store,
        IMemoryCache cache,
        IOptions<SessionCacheOptions> options,
        ILogger<CachedChatSessionRepository<TSession>> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TSession> GetOrCreateAsync(long chatId)
    {
        string key = BuildCacheKey(chatId);

        if (_cache.TryGetValue(key, out object? cached) && cached is TSession session)
        {
            _logger.LogTrace("Session cache hit for chat {ChatId}.", chatId);
            return session;
        }

        TSession created = await _store.GetOrCreateAsync(chatId).ConfigureAwait(false);
        SetCache(key, created);

        return created;
    }

    public async Task SaveAsync(TSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        await _store.SaveAsync(session).ConfigureAwait(false);
        SetCache(BuildCacheKey(GetChatId(session)), session);
    }

    public async Task RemoveAsync(long chatId)
    {
        string key = BuildCacheKey(chatId);
        _cache.Remove(key);

        await _store.RemoveAsync(chatId).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(long chatId)
    {
        string key = BuildCacheKey(chatId);

        if (_cache.TryGetValue(key, out object? cached))
        {
            switch (cached)
            {
                case TSession:
                    return true;
                case bool exists:
                    return exists;
            }
        }

        bool existsInStore = await _store.ExistsAsync(chatId).ConfigureAwait(false);
        SetCache(key, existsInStore);

        return existsInStore;
    }

    private static string BuildCacheKey(long chatId)
    {
        return $"chat_session:{chatId}";
    }

    private void SetCache(string key, object value)
    {
        var entryOptions = new MemoryCacheEntryOptions();

        if (_options.SlidingExpiration > TimeSpan.Zero)
        {
            entryOptions.SlidingExpiration = _options.SlidingExpiration;
        }

        if (_options.AbsoluteExpiration > TimeSpan.Zero)
        {
            entryOptions.AbsoluteExpirationRelativeToNow = _options.AbsoluteExpiration;
        }

        entryOptions.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
        {
            EvictionCallback = (_, evicted, _, _) =>
            {
                if (evicted is TSession session)
                {
                    CancelActionCts(session);
                }
            },
        });

        _cache.Set(key, value, entryOptions);
    }

    private static void CancelActionCts(TSession session)
    {
        if (ActionCtsProperty?.PropertyType == typeof(CancellationTokenSource)
            && ActionCtsProperty.GetValue(session) is CancellationTokenSource cts)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // CTS was already disposed by another path; nothing to cancel.
            }

            cts.Dispose();
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