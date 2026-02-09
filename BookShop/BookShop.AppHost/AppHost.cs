var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.GrpcBookService>("grpcbookservice");

builder.Build().Run();
