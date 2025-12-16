namespace ChatFSM.States;

public interface IStateFactory<in TStateDescriptor, in TContext, TInput>
    where TContext : class
{
    IState<TContext, TInput> Create(TStateDescriptor state);
}