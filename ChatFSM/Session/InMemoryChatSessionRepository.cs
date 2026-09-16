using System.Collections.Concurrent;
using System.Reflection;

namespace Fsm.Session;

/// <summary>
/// Thread-safe in-memory store used for dev/test. Acts as the store
/// (source of truth) and is swappable for a Redis-based implementation
/// behind the same contract.
/// </summary>
public sealed class InMemoryChatSessionRepository<TSession> : IChatSessionRepository<TSession>
    where TSession : class
{
    private static readonly PropertyInfo? ChatIdProperty =
        typeof(TSession).GetProperty("ChatId", BindingFlags.Instance | BindingFlags.Public);

    private readonly ConcurrentDictionary<long, TSession> _sessions = new();

    public Task<TSession> GetOrCreateAsync(long chatId)
    {
        TSession session = _sessions.GetOrAdd(chatId, _ => CreateNew(chatId));
        return Task.FromResult(session);
    }

    public Task SaveAsync(TSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        long chatId = GetChatId(session);
        _sessions[chatId] = session;

        return Task.CompletedTask;
    }

    public Task RemoveAsync(long chatId)
    {
        _sessions.TryRemove(chatId, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(long chatId)
    {
        return Task.FromResult(_sessions.ContainsKey(chatId));
    }

    private static TSession CreateNew(long chatId)
    {
        TSession session = (TSession)Activator.CreateInstance(typeof(TSession))!;

        if (ChatIdProperty?.PropertyType == typeof(long))
        {
            ChatIdProperty.SetValue(session, chatId);
        }

        return session;
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