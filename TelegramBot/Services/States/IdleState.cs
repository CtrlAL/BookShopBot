using ChatFSM.States;
using S3Bot.TelegramBot.Enums;
using S3Bot.TelegramBot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace S3Bot.TelegramBot.Services.States
{
    public class IdleState : BaseState<ITelegramChatContext, Update>
    {
        private readonly ITelegramBotClient _botClient;

        public IdleState(ITelegramBotClient botClient)
        {
            _botClient = botClient;
        }

        public override async Task HandleInputAsync(ITelegramChatContext chatContext, Update update, CancellationToken ct = default)
        {
            if (update.Message is not { } message)
            {
                return;
            }

            var chatId = message.Chat.Id;

            switch (message?.Text?.ToLowerInvariant() ?? string.Empty)
            {
                case "/start":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "🤖 Бот для загрузки файлов в S3\n\n" +
                              "Доступные команды:\n" +
                              "/upload - Загрузить файл\n" +
                              "/help - Помощь",
                        cancellationToken: ct);
                    break;

                case "/upload":
                case "/загрузить":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "📁 Пожалуйста, отправьте файл или фото для загрузки.",
                        cancellationToken: ct);

                    await chatContext.FireTriggerAsync(Trigger.UploadFile);
                    break;

                case "/help":
                case "/помощь":
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "📋 Помощь:\n" +
                              "/upload - Начать загрузку файла\n" +
                              "/cancel - Отменить текущую операцию\n" +
                              "/help - Показать это сообщение",
                        cancellationToken: ct);
                    break;

                default:
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "❓ Неизвестная команда. Используйте /help для списка команд.",
                        cancellationToken: ct);
                    break;
            }
        }

    }
}