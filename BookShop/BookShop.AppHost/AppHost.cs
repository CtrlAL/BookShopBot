var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.GrpcBookService>("grpcbookservice");

builder.AddProject<Projects.GrpcBookRecognitionService>("grpcbookrecognitionservice");

builder.Build().Run();
