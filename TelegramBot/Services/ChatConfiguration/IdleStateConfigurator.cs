using BookShop.TelegramBot.Enums;
using BookShop.TelegramBot.Interfaces;
using BookShop.TelegramBot.Interfaces.ChatConfiguration;
using Stateless;

namespace BookShop.TelegramBot.Services.ChatConfiguration
{
    internal class IdleStateConfigurator : ITelegramChatStateConfigurator
    {
        private readonly ITelegramStateFactory _stateFactory;

        public IdleStateConfigurator(ITelegramStateFactory stateFactory)
        {
            _stateFactory = stateFactory;
        }

        public void ConfigureState(StateMachine<State, Trigger> stateMachine, ITelegramChatContext context)
        {
            var stateConfig = stateMachine.Configure(State.Idle);
            var stateHandler = _stateFactory.Create(State.Idle);

            stateConfig.OnEntryAsync(() => stateHandler.OnStateEnterAsync(context));
            stateConfig.OnExitAsync(() => stateHandler.OnStateExitAsync(context));

            ConfigureTransitions(stateConfig);
        }

        private static void ConfigureTransitions(StateMachine<State, Trigger>.StateConfiguration stateConfig)
        {
            stateConfig.PermitReentry(Trigger.StartBot);
            stateConfig.PermitReentry(Trigger.Cancel);

            stateConfig.Permit(Trigger.UploadFile, State.WaitFile);
        }
    }
}
