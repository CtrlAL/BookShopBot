using Fsm.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Fsm.Interfaces;

public class ChatContextFactory<TContext> : IChatContextFactory<TContext>
    where TContext : class, IInitializeble
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
}
