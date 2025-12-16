using S3Bot.TelegramBot.Enums;
using S3Bot.TelegramBot.Interfaces;
using S3Bot.TelegramBot.Interfaces.ChatConfiguration;
using Stateless;

namespace S3Bot.TelegramBot.Services.ChatConfiguration
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