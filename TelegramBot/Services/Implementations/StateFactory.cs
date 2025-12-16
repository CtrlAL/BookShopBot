using ChatFSM.States;
using Microsoft.Extensions.DependencyInjection;
using S3Bot.TelegramBot.Enums;
using S3Bot.TelegramBot.Interfaces;
using S3Bot.TelegramBot.Services.States;
using Telegram.Bot.Types;

namespace S3Bot.TelegramBot.Services.Implementations
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
