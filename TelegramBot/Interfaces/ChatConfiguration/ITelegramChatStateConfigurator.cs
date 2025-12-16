using ChatFSM.Configuration;
using S3Bot.TelegramBot.Enums;
using S3Bot.TelegramBot.Interfaces;
using Stateless;

namespace S3Bot.TelegramBot.Interfaces.ChatConfiguration
{
    internal interface ITelegramChatStateConfigurator : IStateConfigurator<StateMachine<State, Trigger>, ITelegramChatContext>
    {
    }
}
