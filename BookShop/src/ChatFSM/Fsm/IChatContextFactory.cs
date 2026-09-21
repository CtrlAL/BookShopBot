namespace Fsm.Interfaces;

public interface IChatContextFactory<TContext>
    where TContext : class
{
    Task<TContext> CreateContextAsync(params object[] attributes);
}

public interface IChatContextFactory<TContext, in TSession> : IChatContextFactory<TContext>
    where TContext : class
    where TSession : class
{
    Task<TContext> CreateContextAsync(TSession session);
}