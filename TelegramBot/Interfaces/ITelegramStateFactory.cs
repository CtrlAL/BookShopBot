using ChatFSM.States;
using BookShop.TelegramBot.Enums;
using Telegram.Bot.Types;

namespace BookShop.TelegramBot.Interfaces
{
    public interface ITelegramStateFactory : IStateFactory<State, ITelegramChatContext, Update>
    {
    }
}