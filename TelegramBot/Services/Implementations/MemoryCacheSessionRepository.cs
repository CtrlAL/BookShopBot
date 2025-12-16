using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using S3Bot.TelegramBot.Interfaces;

namespace S3Bot.TelegramBot.Services.Implementations
{
    public class MemoryCacheSessionRepository : IMemoryCacheSessionRepository
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<MemoryCacheSessionRepository> _logger;
        private readonly string _cachePrefix = $"chat_session_{typeof(TelegramChatSession).Name}_";

        public MemoryCacheSessionRepository(
            IMemoryCache cache,
            ILogger<MemoryCacheSessionRepository> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        private string GetCacheKey(long chatId) => $"{_cachePrefix}{chatId}";

        public Task<TelegramChatSession> GetOrCreateAsync(long chatId)
        {
            var cacheKey = GetCacheKey(chatId);

            if (_cache.TryGetValue<TelegramChatSession>(cacheKey, out var session) && session != null)
            {
                _logger.LogDebug("Session found in cache for chat {ChatId}", chatId);
                return Task.FromResult(session);
            }

            session = new TelegramChatSession(chatId);

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(30))
                .SetAbsoluteExpiration(TimeSpan.FromHours(2))
                .RegisterPostEvictionCallback(OnSessionEvicted);

            _cache.Set(cacheKey, session, cacheOptions);

            _logger.LogInformation("Created new session for chat {ChatId}", chatId);

            return Task.FromResult(session);
        }

        public Task SaveAsync(TelegramChatSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var cacheKey = GetCacheKey(session.ChatId);

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(30))
                .SetAbsoluteExpiration(TimeSpan.FromHours(2))
                .SetPriority(CacheItemPriority.Normal)
                .RegisterPostEvictionCallback(OnSessionEvicted);

            _cache.Set(cacheKey, session, cacheOptions);

            _logger.LogDebug("Session saved for chat {ChatId}", session.ChatId);

            return Task.CompletedTask;
        }

        public Task RemoveAsync(long chatId)
        {
            var cacheKey = GetCacheKey(chatId);
            _cache.Remove(cacheKey);

            _logger.LogInformation("Session removed for chat {ChatId}", chatId);

            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(long chatId)
        {
            var cacheKey = GetCacheKey(chatId);
            var exists = _cache.TryGetValue<TelegramChatSession>(cacheKey, out _);
            return Task.FromResult(exists);
        }

        private void OnSessionEvicted(object key, object value, EvictionReason reason, object state)
        {
            if (value is TelegramChatSession session)
            {
                _logger.LogInformation(
                    "Session evicted for chat {ChatId}. Reason: {Reason}",
                    session.ChatId,
                    reason);

                session.ActionCts?.Cancel();
                session.ActionCts?.Dispose();
            }
        }
    }
}
