var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.BookCatalogService>("bookcatalogservice");

builder.AddProject<Projects.BookRecognitionService>("bookrecognitionservice");

builder.AddProject<Projects.ChatApi>("chatapi");

await builder.Build().RunAsync();
