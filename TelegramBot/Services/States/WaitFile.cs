using ChatFSM.States;
using S3Bot.TelegramBot.Enums;
using S3Bot.TelegramBot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace S3Bot.TelegramBot.Services.States
{
    public class WaitFile : BaseState<ITelegramChatContext, Update>
    {
        private readonly ITelegramBotClient _botClient;

        public WaitFile(ITelegramBotClient botClient)
        {
            _botClient = botClient;
        }

        public override async Task HandleInputAsync(ITelegramChatContext chatContext, Update update, CancellationToken ct = default)
        {
            if (update.Message is not { } message)
                return;

            if (chatContext.Session is not { } session)
                return;

            var chatId = message.Chat.Id;

            if (message.Document != null || message.Photo?.Length > 0 || message.Video != null)
            {
                string fileId;
                string fileType;
                string extension;

                if (message.Document != null)
                {
                    fileId = message.Document.FileId;
                    var originalFileName = message.Document.FileName ?? "document.bin";
                    extension = Path.GetExtension(originalFileName).ToLowerInvariant();
                    fileType = message.Document.MimeType ?? "document";
                }
                else if (message.Video != null)
                {
                    fileId = message.Video.FileId;
                    var originalFileName = message.Video.FileName ?? "document.mp4";
                    extension = Path.GetExtension(originalFileName).ToLowerInvariant();
                    fileType = message.Video.MimeType ?? "video/mp4";
                }
                else
                {
                    extension = ".jpg";
                    var largestPhoto = message.Photo!.MaxBy(p => p.Width * p.Height)!;
                    fileId = largestPhoto.FileId;
                    fileType = "photo";
                }

                session.FileId = fileId;
                session.FileType = fileType;
                session.Extension = extension;

                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"📎 Файл получен. Теперь укажите имя для файла (без расширения, оно добавится автоматически):\n\n",
                    cancellationToken: ct);

                await chatContext.FireTriggerAsync(Trigger.FileReceived);
            }
            else if (!string.IsNullOrWhiteSpace(message.Text))
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "📁 Пожалуйста, отправьте файл или фото.",
                    cancellationToken: ct);
            }
        }
    }
}