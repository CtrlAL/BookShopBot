namespace Fsm.Session;

public interface ISessionProvider<out TSession>
    where TSession : class
{
    TSession? Session { get; }
}