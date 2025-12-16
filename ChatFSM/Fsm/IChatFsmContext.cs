namespace ChatFSM.Fsm;

public interface IChatFsmContext<out TSession, in TTrigger, in TInput> : IInitializeble
    where TSession : class
    where TTrigger : Enum
{
    TSession? Session { get; }
    Task FireTriggerAsync(TTrigger trigger);
    Task HandleInputAsync(TInput input);
}