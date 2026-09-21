using Fsm.Configuration;
using BookShop.TelegramBot.Enums;
using BookShop.TelegramBot.Interfaces;
using Stateless;

namespace BookShop.TelegramBot.Interfaces.ChatConfiguration
{
    internal interface ITelegramChatStateConfigurator : IStateConfigurator<StateMachine<State, Trigger>, ITelegramChatContext>
    {
    }
}
