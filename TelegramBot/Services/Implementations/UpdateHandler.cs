using ChatFSM.Fsm;
using Microsoft.Extensions.Logging;
using S3Bot.TelegramBot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace S3Bot.TelegramBot.Services.Implementations
{
    public class UpdateHandler
    {
        private readonly ILogger<UpdateHandler> _logger;
        private readonly IChatContextFactory<ITelegramChatContext> _chatContextFactory;

        public UpdateHandler(ILogger<UpdateHandler> logger, IChatContextFactory<ITelegramChatContext> chatContextFactory)
        {
            _logger = logger;
            _chatContextFactory = chatContextFactory;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            try
            {
                if (update.Message?.Chat?.Id is not { } chatId)
                {
                    return;
                }

                var context = await _chatContextFactory.CreateContextAsync(chatId);
                await context.HandleInputAsync(update);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Произошла ошибка при обработке обновления {UpdateId} {UserId}.", update.Id, update?.Message?.From?.Id);
            }
        }
    }
}
