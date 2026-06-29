using ChatFSM.States;
using Microsoft.Extensions.DependencyInjection;
using BookShop.TelegramBot.Enums;
using BookShop.TelegramBot.Interfaces;
using BookShop.TelegramBot.Services.States;
using Telegram.Bot.Types;

namespace BookShop.TelegramBot.Services.Implementations
{
    public class StateFactory : ITelegramStateFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public StateFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IState<ITelegramChatContext, Update> Create(State state)
        {
            return state switch
            {
                State.Idle => _serviceProvider.GetRequiredService<IdleState>(),
                State.WaitFile => _serviceProvider.GetRequiredService<WaitFile>(),
                State.WaitFileName => _serviceProvider.GetRequiredService<WaitFileName>(),
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };
        }
    }
}
