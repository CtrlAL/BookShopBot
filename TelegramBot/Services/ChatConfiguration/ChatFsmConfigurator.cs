using BookShop.TelegramBot.Enums;
using BookShop.TelegramBot.Interfaces;
using BookShop.TelegramBot.Interfaces.ChatConfiguration;
using Stateless;

namespace BookShop.TelegramBot.Services.ChatConfiguration
{
    public class ChatFsmConfigurator : ITelegramChatFsmConfigurator
    {
        private readonly ITelegramStateFactory _stateFactory;

        public ChatFsmConfigurator(ITelegramStateFactory stateFactory)
        {
            _stateFactory = stateFactory;
        }

        public void Configure(StateMachine<State, Trigger> stateMachine, ITelegramChatContext context)
        {
            stateMachine.OnTransitionedAsync(async t =>
            {
                await Task.CompletedTask;
            });

            new IdleStateConfigurator(_stateFactory).ConfigureState(stateMachine, context);
            new WaitFileConfigurator(_stateFactory).ConfigureState(stateMachine, context);
            new WaitFileNameConfigurator(_stateFactory).ConfigureState(stateMachine, context);
        }
    }
}