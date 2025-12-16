namespace ChatFSM.Session;

public interface IChatSessionRepository<TSession>
    where TSession : class
{
    Task<TSession> GetOrCreateAsync(long chatId);
    Task SaveAsync(TSession session);
    Task RemoveAsync(long chatId);
    Task<bool> ExistsAsync(long chatId);
}
