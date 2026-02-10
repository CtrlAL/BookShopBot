namespace Fsm.Configuration;

public interface IStateConfigurator<in TStateMachine, in TContext>
    where TContext : class
    where TStateMachine : class
{
    void ConfigureState(TStateMachine stateMachine, TContext context);
}