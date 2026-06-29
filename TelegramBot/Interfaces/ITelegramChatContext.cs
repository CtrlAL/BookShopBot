using ChatFSM.Fsm;
using BookShop.TelegramBot.Enums;
using BookShop.TelegramBot.Services.Implementations;
using Telegram.Bot.Types;

namespace BookShop.TelegramBot.Interfaces;

public interface ITelegramChatContext : IChatFsmContext<Trigger, Update>
{
}
