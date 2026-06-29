using ChatFSM.Session;
using BookShop.TelegramBot.Enums;

namespace BookShop.TelegramBot.Services.Implementations;

public class TelegramChatSession : Session<State>
{
    public string FileName { get; set; }
    public string FileId { get; set; }
    public string? Extension { get; set; }
    public string FileType { get; set; }

    public TelegramChatSession(long chatId)
    {
        ChatId = chatId;
        CurrentState = State.Idle;
        PreviousState = State.Idle;
    }
}