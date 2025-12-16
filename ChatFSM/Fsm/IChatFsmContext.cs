using Fsm.Interfaces;

namespace ChatFSM.Fsm;

public interface IChatFsmContext<in TTrigger, in TInput> : IInitializeble
    where TTrigger : Enum
{
    Task FireTriggerAsync(TTrigger trigger);
    Task HandleInputAsync(TInput input);
}