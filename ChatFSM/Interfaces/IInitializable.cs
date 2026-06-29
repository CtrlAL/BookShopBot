namespace Fsm.Interfaces;

public interface IInitializable
{
    Task InitializeAsync(params object[] attributes);
}

public interface IInitializable<in TSession>
    where TSession : class
{
    Task InitializeSessionAsync(TSession session);
}