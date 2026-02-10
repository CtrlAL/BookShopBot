namespace Fsm.States;

public abstract class BaseState<TContext, TInput> : IState<TContext, TInput>
    where TContext : class
{
    public virtual Task OnStateEnterAsync(TContext chatContext, CancellationToken ct = default) => Task.CompletedTask;
    public virtual Task OnStateExitAsync(TContext chatContext, CancellationToken ct = default) => Task.CompletedTask;
    public abstract Task HandleInputAsync(TContext chatContext, TInput input, CancellationToken ct = default);
}