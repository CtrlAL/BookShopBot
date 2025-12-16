using Fsm.Interfaces;

namespace ChatFSM.Fsm;

public interface IChatFsmContext<in TTrigger, in TInput> : IInitializeble
{
    Task FireTriggerAsync(TTrigger trigger);
    Task HandleInputAsync(TInput input);
}