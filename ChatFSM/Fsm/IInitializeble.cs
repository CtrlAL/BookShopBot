namespace ChatFSM.Fsm;

public interface IInitializeble
{
    Task InitializeAsync(long chatId);
}
