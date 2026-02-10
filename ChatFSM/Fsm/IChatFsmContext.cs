using Fsm.Interfaces;

namespace Fsm.Fsm;

public interface IChatFsmContext<in TTrigger, in TInput> : IInitializeble
{
    Task FireTriggerAsync(TTrigger trigger);
    Task HandleInputAsync(TInput input);
}