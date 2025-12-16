using ChatFSM.Fsm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using S3Bot.TelegramBot.Configs;
using S3Bot.TelegramBot.Interfaces;
using S3Bot.TelegramBot.Interfaces.ChatConfiguration;
using S3Bot.TelegramBot.Services.ChatConfiguration;
using S3Bot.TelegramBot.Services.Implementations;
using S3Bot.TelegramBot.Services.States;
using Telegram.Bot;

namespace S3Bot.TelegramBot.Extensions
{
    public static class DiExtensions
    {
        public static void AddServices(this IServiceCollection services)
        {
            services.AddSingleton<IChatContextFactory<ITelegramChatContext>, ChatContextFactory<ITelegramChatContext>>();
            services.AddSingleton<IMemoryCacheSessionRepository, MemoryCacheSessionRepository>();
            services.AddSingleton<ITelegramStateFactory, StateFactory>();
            services.AddSingleton<ITelegramChatFsmConfigurator, ChatFsmConfigurator>();
            services.AddScoped<ITelegramChatContext, ChatContext>();
            services.AddScoped<UpdateHandler>();
            services.AddStates();
        }

        public static IServiceCollection AddTelegramBotClient(this IServiceCollection services, IConfigurationSection configurationSection)
        {
            services.Configure<BotConfig>(configurationSection);
            services.AddSingleton<ITelegramBotClient>(sp =>
            {
                var botConfig = sp.GetRequiredService<IOptions<BotConfig>>().Value;
                if (string.IsNullOrEmpty(botConfig.Token))
                {
                    throw new InvalidOperationException(
                        "Telegram Bot Token не задан в конфигурации. " +
                        "Убедитесь, что в appsettings.json указано значение для 'BotConfig:Token'.");
                }
                return new TelegramBotClient(botConfig.Token);
            });
            return services;
        }

        private static void AddStates(this IServiceCollection services)
        {
            services.AddTransient<IdleState>();
            services.AddTransient<WaitFile>();
            services.AddTransient<WaitFileName>();
        }
    }
}