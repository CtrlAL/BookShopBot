namespace ChatFSM.Fsm;

public interface IChatContextFactory<TContext>
    where TContext : class, IInitializeble
{
    Task<TContext> CreateContextAsync(long chatId);
}
