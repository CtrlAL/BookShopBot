using S3Bot.TelegramBot.Enums;
using S3Bot.TelegramBot.Interfaces;
using S3Bot.TelegramBot.Interfaces.ChatConfiguration;
using Stateless;
using Telegram.Bot.Types;

namespace S3Bot.TelegramBot.Services.Implementations;

public class ChatContext : ITelegramChatContext
{
    private readonly ITelegramStateFactory _stateFactory;
    private readonly IMemoryCacheSessionRepository _memoryCacheSessionRepository;
    private readonly StateMachine<State, Trigger> _stateMachine;

    private bool _isInitialized = false;

    public TelegramChatSession? Session { get ; private set; }

    public ChatContext(ITelegramChatFsmConfigurator fsmConfigurator,
        ITelegramStateFactory stateFactory,
        IMemoryCacheSessionRepository memoryCacheSessionRepository)
    {
        _stateFactory = stateFactory;
        _memoryCacheSessionRepository = memoryCacheSessionRepository;

        _stateMachine = new StateMachine<State, Trigger>(
            () => Session?.CurrentState ?? default, 
            async newState => await OnStateChangedAsync(newState));

        fsmConfigurator.Configure(_stateMachine, this);
    }

    public async Task InitializeAsync(long chatId)
    {
        if (_isInitialized) return;

        Session = await _memoryCacheSessionRepository.GetOrCreateAsync(chatId);

        if (Session.CurrentState.Equals(default(State)))
        {
            Session.CurrentState = State.Idle;
            await _memoryCacheSessionRepository.SaveAsync(Session);
        }

        _isInitialized = true;
    }

    private async Task OnStateChangedAsync(State newState)
    {
        if (!_isInitialized || Session == null)
        {
            return;
        }

        Session.PreviousState = Session.CurrentState;
        Session.CurrentState = newState;

        await _memoryCacheSessionRepository.SaveAsync(Session);
    }

    public async Task FireTriggerAsync(Trigger trigger) => await _stateMachine.FireAsync(trigger);

    public Task HandleInputAsync(Update input)
    {
        if (!_isInitialized || Session == null)
        {
            return Task.CompletedTask;
        }

        var state = _stateFactory.Create(Session.CurrentState);
        return state.HandleInputAsync(this, input);
    }
}