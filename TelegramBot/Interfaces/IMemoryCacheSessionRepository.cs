using ChatFSM.Session;
using BookShop.TelegramBot.Services.Implementations;

namespace BookShop.TelegramBot.Interfaces
{
    public interface IMemoryCacheSessionRepository : IChatSessionRepository<TelegramChatSession>
    {
    }
}
