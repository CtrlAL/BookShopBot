namespace Fsm.Interfaces;

public interface IInitializeble
{
    Task InitializeAsync(params object[] attributes);
}

public interface IInitializeble<in TSession>
    where TSession : class
{
    Task InitializeSessionAsync(TSession session);
}