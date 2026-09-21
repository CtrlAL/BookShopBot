using BookShop.TelegramBot.Enums;
using BookShop.TelegramBot.Interfaces;
using BookShop.TelegramBot.Interfaces.ChatConfiguration;
using Stateless;
using Telegram.Bot.Types;

namespace BookShop.TelegramBot.Services.Implementations;

public class ChatContext : ITelegramChatContext
{
    private readonly ITelegramStateFactory _stateFactory;
    private readonly IMemoryCacheSessionRepository _memoryCacheSessionRepository;
    private readonly StateMachine<State, Trigger> _stateMachine;

    private bool _isInitialized = false;

    public TelegramChatSession Session { get; set; }

    public ChatContext(ITelegramChatFsmConfigurator fsmConfigurator,
        ITelegramStateFactory stateFactory,
        IMemoryCacheSessionRepository memoryCacheSessionRepository)
    {
        _stateFactory = stateFactory;
        _memoryCacheSessionRepository = memoryCacheSessionRepository;

        _stateMachine = new StateMachine<State, Trigger>(
            () => Session.CurrentState,
            async newState => await OnStateChangedAsync(newState));

        fsmConfigurator.Configure(_stateMachine, this);
    }

    public Task InitializeAsync(params object[] attributes)
    {
        long chatId = attributes.Length > 0 && attributes[0] is long id ? id : 0;
        return InitializeCoreAsync(chatId);
    }

    private async Task InitializeCoreAsync(long chatId)
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