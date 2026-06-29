using ChatFSM.Configuration;
using BookShop.TelegramBot.Enums;
using BookShop.TelegramBot.Interfaces;
using Stateless;

namespace BookShop.TelegramBot.Interfaces.ChatConfiguration
{
    public interface ITelegramChatFsmConfigurator : IFsmConfigurator<StateMachine<State, Trigger>, ITelegramChatContext>
    {
    }
}
