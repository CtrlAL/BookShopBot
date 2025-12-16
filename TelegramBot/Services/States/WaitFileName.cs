using ChatFSM.States;
using Microsoft.Extensions.Logging;
using S3Bot.S3Tool.Interfaces;
using S3Bot.TelegramBot.Enums;
using S3Bot.TelegramBot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace S3Bot.TelegramBot.Services.States;

public class WaitFileName : BaseState<ITelegramChatContext, Update>
{
    private readonly ITelegramBotClient _botClient;
    private readonly IS3Service _s3Service;
    private readonly ILogger<WaitFileName> _logger;
    private const string _uploadFolder = "S3BotUploads";

    public WaitFileName(ITelegramBotClient botClient, IS3Service s3Service, ILogger<WaitFileName> logger)
    {
        _botClient = botClient;
        _s3Service = s3Service;
        _logger = logger;
    }

    public override async Task HandleInputAsync(ITelegramChatContext chatContext, Update update, CancellationToken ct = default)
    {
        if (update.Message is not { } message)
        {
            return;
        }

        if (chatContext.Session is not { } session)
        {
            return;
        }

        var userId = message.From?.Id ?? 0;
        var chatId = message.Chat.Id;

        if (message.Text?.ToLowerInvariant() == "/cancel")
        {
            await chatContext.FireTriggerAsync(Trigger.Cancel);
            await _botClient.SendMessage(
                chatId: chatId,
                text: "✅ Название файла не сохранено.",
                cancellationToken: ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            var userFileName = message.Text.Trim();

            if (!IsValidFileName(userFileName, out var errorMessage))
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: $"❌ {errorMessage}\nПопробуйте еще раз:",
                    cancellationToken: ct);
                return;
            }

            session.FileName = userFileName;

            try
            {
                await ProcessFileUploadAsync(chatId, chatContext, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file for user {UserId}", userId);
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "❌ Ошибка при загрузке файла. Попробуйте снова.",
                    cancellationToken: ct);

                await chatContext.FireTriggerAsync(Trigger.Cancel);
            }
        }
        else
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: "📝 Пожалуйста, введите название файла или отправьте /cancel для отмены.",
                cancellationToken: ct);
        }
    }

    private async Task ProcessFileUploadAsync(long chatId, ITelegramChatContext chatContext, CancellationToken ct)
    {
        if (chatContext.Session is not { } session)
        {
            return;
        }

        var filename = session.FileName + session.Extension;

        await _botClient.SendMessage(
            chatId: chatId,
            text: "⏳ Загружаю файл в облако...",
            cancellationToken: ct);

        await using var memoryStream = new MemoryStream();
        await _botClient.GetInfoAndDownloadFile(session.FileId, memoryStream);
        memoryStream.Position = 0;

        var result = await _s3Service.CreateFile(
            memoryStream,
            _uploadFolder,
            filename,
            session.FileType);

        if (result)
        {
            var fileUrl = _s3Service.GetPublicUrl(_uploadFolder, filename);

            await chatContext.FireTriggerAsync(Trigger.FileNameReceived);

            await _botClient.SendMessage(
                chatId: chatId,
                text: $"✅ Файл успешно загружен!\n\n" +
                      $"📁 Имя: {session.FileName}\n" +
                      $"🔗 Ссылка: {fileUrl}\n\n" +
                      $"⚠️ Ссылка действительна очень долго",
                cancellationToken: ct);
        }
        else
        {
            await _botClient.SendMessage(
            chatId: chatId,
            text: $"✅ Ошибка во время загрузки файла!\n\n" +
                  $"📁 Имя: {session.FileName}\n",
            cancellationToken: ct);

            await chatContext.FireTriggerAsync(Trigger.UploadError);
        }
    }

    private static bool IsValidFileName(string fileName, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            errorMessage = "Имя файла не может быть пустым.";
            return false;
        }

        if (fileName.Length > 255)
        {
            errorMessage = "Имя файла слишком длинное.";
            return false;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        if (fileName.Any(c => invalidChars.Contains(c)))
        {
            errorMessage = "Имя файла содержит недопустимые символы.";
            return false;
        }

        if (fileName.Equals(".") || fileName.Equals(".."))
        {
            errorMessage = "Имя файла не может быть '.' или '..'";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}