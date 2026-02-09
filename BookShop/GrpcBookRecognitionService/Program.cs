using DeepSeek;
using GrpcBookRecognitionService.Features.BookRecognition.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDeepSeekClient(builder.Configuration.GetSection("DeepSeekConfig"));
builder.Services.AddGrpc();
builder.Services.AddGrpcClient<BookService.V1.BookCatalogService.BookCatalogServiceClient>(options =>
{
    options.Address = new Uri("http://book-catalog-service:5000");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return handler;
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGrpcService<BookRecognitionGrpcService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
