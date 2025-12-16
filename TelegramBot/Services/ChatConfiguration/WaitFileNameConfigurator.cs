using S3Bot.TelegramBot.Enums;
using S3Bot.TelegramBot.Interfaces;
using S3Bot.TelegramBot.Interfaces.ChatConfiguration;
using Stateless;

namespace S3Bot.TelegramBot.Services.ChatConfiguration
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
            stateConfig.OnExitAsync(() => stateHandler.OnStateEnterAsync(context));

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
