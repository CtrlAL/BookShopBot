var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.BookCatalogService>("bookcatalogservice")
    .WithReference(postgres);

var minio = builder.AddContainer("minio", "minio/minio", "RELEASE.2024-08-03T04-33-23Z")
    .WithEndpoint(port: 9000, targetPort: 9000, name: "s3")
    .WithEndpoint(port: 9001, targetPort: 9001, name: "console")
    .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
    .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
    .WithVolume("minio-data", "/data")
    .WithVolume("minio-config", "/root/.minio")
    .WithArgs(new[] { "server", "/data", "--console-address", ":9001" })
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.BookRecognitionService>("bookrecognitionservice");

builder.AddProject<Projects.BotApi>("botapi")
    .WithReference(postgres)
    .WithReference(minio.GetEndpoint("s3"))
    .WithEnvironment("S3__Endpoint", "http://localhost:9000")
    .WithEnvironment("S3__AccessKey", "minioadmin")
    .WithEnvironment("S3__SecretKey", "minioadmin");

builder.AddProject<Projects.ChatApi>("chatapi");

await builder.Build().RunAsync();
