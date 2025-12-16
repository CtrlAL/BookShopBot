namespace ChatFSM.Configuration;

public interface IFsmConfigurator<in TStateMachine, in TContext>
    where TContext : class
    where TStateMachine : class
{
    void Configure(TStateMachine stateMachine, TContext context);
}