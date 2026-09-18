using BookShop.S3Tool;
using BookShop.TelegramBot.Extensions;
using BookShop.TelegramBot.Services.HostedServices;
using BookService.V1;
using Grpc.Net.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Локальные секреты (gitignored): BotConfig:Token, S3:AccessKey/SecretKey, ConnectionStrings.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddTelegramBotClient(builder.Configuration.GetSection("BotConfig"));
builder.Services.AddChatSessionPersistence(builder.Configuration);
builder.Services.AddS3Client(builder.Configuration.GetSection("S3"));
builder.Services.AddServices();

builder.Services.AddSingleton(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();

    var address = configuration["BookCatalogService:Url"] ??
                  configuration["Services:BookCatalogService:Url"] ??
                  "http://book-catalog-service:5000";

    var handler = new HttpClientHandler();
    if (bool.TryParse(configuration["GrpcClient:IgnoreSslErrors"], out var ignore) && ignore)
    {
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }

    var httpClient = new HttpClient(handler);

    var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
    {
        HttpClient = httpClient,
        MaxReceiveMessageSize = 10 * 1024 * 1024,
        MaxSendMessageSize = 10 * 1024 * 1024,
    });

    return new BookCatalogService.BookCatalogServiceClient(channel);
});

builder.Services.AddHostedService<TelegramBotHostedService>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/", () => "BookShop Bot is running");

await app.RunAsync();