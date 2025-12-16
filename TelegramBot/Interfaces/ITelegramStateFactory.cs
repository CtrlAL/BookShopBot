using ChatFSM.States;
using S3Bot.TelegramBot.Enums;
using Telegram.Bot.Types;

namespace S3Bot.TelegramBot.Interfaces
{
    public interface ITelegramStateFactory : IStateFactory<State, ITelegramChatContext, Update>
    {
    }
}