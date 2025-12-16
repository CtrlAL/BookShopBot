namespace ChatFSM.States;

public interface IStateFactory<in TState, in TContext, TInput>
    where TState : Enum
    where TContext : class
{
    IState<TContext, TInput> Create(TState state);
}