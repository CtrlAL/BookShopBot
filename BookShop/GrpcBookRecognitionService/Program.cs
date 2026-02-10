using BookService.V1;
using DeepSeek;
using Grpc.Net.Client;
using GrpcBookRecognitionService.Features.BookRecognition.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDeepSeekClient(builder.Configuration.GetSection("DeepSeekConfig"));
builder.Services.AddGrpc();

builder.Services.AddSingleton(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();

    var address = configuration["GrpcBookService:Url"] ??
                  configuration["Services:GrpcBookService:Url"] ??
                  "http://book-catalog-service:5000";

    var handler = new HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

    var httpClient = new HttpClient(handler);

    var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
    {
        HttpClient = httpClient,
        MaxReceiveMessageSize = 1024 * 1024 * 100,
        MaxSendMessageSize = 1024 * 1024 * 100,
    });

    return new BookCatalogService.BookCatalogServiceClient(channel);
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGrpcService<BookRecognitionGrpcService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
