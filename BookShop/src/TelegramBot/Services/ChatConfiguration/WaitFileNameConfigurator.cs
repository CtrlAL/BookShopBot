using BookShop.TelegramBot.Enums;
using BookShop.TelegramBot.Interfaces;
using BookShop.TelegramBot.Interfaces.ChatConfiguration;
using Stateless;

namespace BookShop.TelegramBot.Services.ChatConfiguration
{
    internal class WaitFileNameConfigurator : ITelegramChatStateConfigurator
    {
        private readonly ITelegramStateFactory _stateFactory;

        public WaitFileNameConfigurator(ITelegramStateFactory stateFactory)
        {
            _stateFactory = stateFactory;
        }

        public void ConfigureState(StateMachine<State, Trigger> stateMachine, ITelegramChatContext context)
        {
            var stateConfig = stateMachine.Configure(State.WaitFileName);
            var stateHandler = _stateFactory.Create(State.WaitFileName);

            stateConfig.OnEntryAsync(() => stateHandler.OnStateEnterAsync(context));
            stateConfig.OnExitAsync(() => stateHandler.OnStateExitAsync(context));

            ConfigureTransitions(stateConfig);
        }

        private static void ConfigureTransitions(StateMachine<State, Trigger>.StateConfiguration stateConfig)
        {
            stateConfig.PermitReentry(Trigger.UploadError);
            stateConfig.Permit(Trigger.FileNameReceived, State.Idle);
            stateConfig.Permit(Trigger.Cancel, State.Idle);
        }
    }
}
