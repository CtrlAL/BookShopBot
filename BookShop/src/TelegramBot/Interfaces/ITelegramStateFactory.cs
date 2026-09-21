using Fsm.States;
using BookShop.TelegramBot.Enums;
using Telegram.Bot.Types;

namespace BookShop.TelegramBot.Interfaces
{
    public interface ITelegramStateFactory
    {
        IState<ITelegramChatContext, Update> Create(State state);
    }
}