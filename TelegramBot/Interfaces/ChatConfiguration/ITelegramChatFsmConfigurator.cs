using ChatFSM.Configuration;
using S3Bot.TelegramBot.Enums;
using S3Bot.TelegramBot.Interfaces;
using Stateless;

namespace S3Bot.TelegramBot.Interfaces.ChatConfiguration
{
    public interface ITelegramChatFsmConfigurator : IFsmConfigurator<StateMachine<State, Trigger>, ITelegramChatContext>
    {
    }
}
