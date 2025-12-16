using ChatFSM.Session;
using S3Bot.TelegramBot.Services.Implementations;

namespace S3Bot.TelegramBot.Interfaces
{
    public interface IMemoryCacheSessionRepository : IChatSessionRepository<TelegramChatSession>
    {
    }
}
