using ChatFSM.Fsm;
using Fsm.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Fsm.Abstractions;

public class ChatContextFactory<TContext, TSession> : IChatContextFactory<TContext, TSession>
    where TContext : class, IInitializeble
    where TSession : class
{
    private readonly IServiceProvider _serviceProvider;

    public ChatContextFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TContext> CreateContextAsync(params object[] attributes)
    {
        var context = _serviceProvider.GetRequiredService<TContext>();
        await context.InitializeAsync(attributes);

        return context;
    }

    public async Task<TContext> CreateContextAsync(TSession session)
    {
        var context = _serviceProvider.GetRequiredService<TContext>();
        await context.InitializeAsync(session);

        return context;
    }
}