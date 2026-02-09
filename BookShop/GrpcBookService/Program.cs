using GrpcBookService.Features.BooksManagment.Services;
using GrpcBookService.Shared;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<DefaultDbContext>(config 
    => config.UseNpgsql(builder.Configuration["ConnectionStrings:DefaultConnectionString"]));


// Add services to the container.
builder.Services.AddGrpc();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
app.MapGrpcService<GreeterService>();
//app.MapGrpcService<BookGrpcService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
