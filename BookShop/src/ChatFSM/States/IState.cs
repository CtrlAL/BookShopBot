namespace Fsm.States;

public interface IState<in TContext, in TInput>
    where TContext : class
{
    public virtual Task OnStateEnterAsync(TContext chatContext, CancellationToken ct = default) => Task.CompletedTask;
    public virtual Task OnStateExitAsync(TContext chatContext, CancellationToken ct = default) => Task.CompletedTask;
    public abstract Task HandleInputAsync(TContext chatContext, TInput input, CancellationToken ct = default);
}