namespace Fsm.States;

public interface IStateFactory<in TContext, TInput>
    where TContext : class
{
    IState<TContext, TInput> Create();
}