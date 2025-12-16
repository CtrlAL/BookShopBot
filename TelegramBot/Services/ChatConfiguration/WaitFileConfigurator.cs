using ChatFSM.Configuration;
using S3Bot.TelegramBot.Enums;
using S3Bot.TelegramBot.Interfaces;
using S3Bot.TelegramBot.Interfaces.ChatConfiguration;
using Stateless;

namespace S3Bot.TelegramBot.Services.ChatConfiguration
{
    internal class WaitFileConfigurator : ITelegramChatStateConfigurator
    {
        private readonly ITelegramStateFactory _stateFactory;

        public WaitFileConfigurator(ITelegramStateFactory stateFactory)
        {
            _stateFactory = stateFactory;
        }

        public void ConfigureState(StateMachine<State, Trigger> stateMachine, ITelegramChatContext context)
        {
            var stateConfig = stateMachine.Configure(State.WaitFile);
            var stateHandler = _stateFactory.Create(State.WaitFile);

            stateConfig.OnEntryAsync(() => stateHandler.OnStateEnterAsync(context));
            stateConfig.OnExitAsync(() => stateHandler.OnStateEnterAsync(context));

            ConfigureTransitions(stateConfig);
        }

        private static void ConfigureTransitions(StateMachine<State, Trigger>.StateConfiguration stateConfig)
        {
            stateConfig.Permit(Trigger.FileReceived, State.WaitFileName);
            stateConfig.Permit(Trigger.Cancel, State.Idle);
        }
    }
}
