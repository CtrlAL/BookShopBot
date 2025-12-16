using Microsoft.Extensions.DependencyInjection;

namespace ChatFSM.Fsm;

public class ChatContextFactory<TContext> : IChatContextFactory<TContext>
    where TContext : class, IInitializeble
{
    private readonly IServiceProvider _serviceProvider;

    public ChatContextFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TContext> CreateContextAsync(long chatId)
    {
        var context = _serviceProvider.GetRequiredService<TContext>();
        await context.InitializeAsync(chatId);

        return context;
    }
}
