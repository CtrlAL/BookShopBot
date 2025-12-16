using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using S3Bot.TelegramBot.Configs;
using S3Bot.TelegramBot.Services.Implementations;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace S3Bot.TelegramBot.Services.HostedServices
{
    public class TelegramBotHostedService : IHostedService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<TelegramBotHostedService> _logger;

        public TelegramBotHostedService(IOptions<BotConfig> botConfig,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<TelegramBotHostedService> logger)
        {
            var token = botConfig.Value.Token;
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Токен бота не задан в BotConfig:Token");

            _serviceScopeFactory = serviceScopeFactory;
            _botClient = new TelegramBotClient(token);
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            var me = await _botClient.GetMe(ct);
            _logger.LogInformation("Бот @{Username} запущен", me.Username);

            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: new()
                {
                    AllowedUpdates = Array.Empty<UpdateType>()
                },
                cancellationToken: ct
            );
        }

        public Task StopAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        private Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            var scope = _serviceScopeFactory.CreateScope();
            var updateHadnler = scope.ServiceProvider.GetRequiredService<UpdateHandler>();
            return updateHadnler.HandleUpdateAsync(bot, update, ct);
        }

        private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
        {
            _logger.LogError(exception, "Ошибка в Telegram-боте");
            return Task.CompletedTask;
        }
    }
}