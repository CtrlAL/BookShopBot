using ChatFSM.Fsm;
using S3Bot.TelegramBot.Enums;
using S3Bot.TelegramBot.Services.Implementations;
using Telegram.Bot.Types;

namespace S3Bot.TelegramBot.Interfaces;

public interface ITelegramChatContext : IChatFsmContext<TelegramChatSession, Trigger, Update>
{
}
