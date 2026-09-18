# S3 (MinIO) + BotApi + Deploy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Реализовать `IS3Service` на MinIO (AWSSDK.S3, presigned URL), создать хост бота `BotApi`, ввести локальные креды (`appsettings.Local.json`, gitignored) и настроить деплой (единый Dockerfile + docker-compose с Postgres/MinIO/4 сервисами + Aspire AppHost с Postgres/MinIO/BotApi) + `docs/deployment.md`.

**Architecture:** Новая библиотека `BookShop/S3Tool/` реализует `IS3Service` поверх `AWSSDK.S3` (ForcePathStyle endpoint на MinIO, presigned URL с TTL по умолчанию 1 год). Новый Web-проект `BookShop/BotApi/` собирает DI-граф бота (Telegram long-polling через существующий `TelegramBotHostedService`), session-persistence (Postgres), S3 и gRPC-клиент к catalog. AppHost дополняется Postgres/MinIO контейнерами и BotApi. Compose использует единый мультистейджинг Dockerfile с `ARG SERVICE`.

**Tech Stack:** .NET 8, Aspire 9.5.0, AWSSDK.S3, Npgsql/EF Core, Telegram.Bot 22.9.0, Grpc.AspNetCore 2.65.0, xunit 2.9.2, Moq, Docker compose, MinIO (`minio/minio`), PostgreSQL (`postgres:16-alpine`).

## Global Constraints

- Работаем в **новом worktree** от `origin/main`: `.worktrees/task-8-s3-botapi-deploy`, ветка `task/8-s3-botapi-deploy`. Пуш и PR — в origin.
- TDD invariant (`.opencode/rules.md`): тест пишется до реализации; сначала тест падает, потом реализация, потом зелёный тест. Исключения (без тестов): `.gitignore`, `appsettings`, Dockerfile, compose, `docs/*.md`.
- Scope guard: изменения только по плану. Новые NuGet-пакеты разрешены только те, что в плане: `AWSSDK.S3`, `Moq`, `Grpc.Net.Client`, `Grpc.Tools`, `Microsoft.Extensions.Hosting.Abstractions`, `Aspire.Hosting.PostgreSQL`.
- Детерминированная валидация (порядок строгий):
  1. `dotnet restore BookShop/BookShop.sln`
  2. `dotnet build BookShop/BookShop.sln --no-restore -p:WarningsAsErrors=CS*`
  3. `dotnet format BookShop/BookShop.sln --verify-no-changes --verbosity diagnostic`
  4. `dotnet test BookShop/BookShop.sln --no-build --verbosity normal`
- Все сборки и тесты запускать из корня worktree (`F:\SpaceApp\BookShopBot\.worktrees\task-8-s3-botapi-deploy`).
- Стиль: nullable enable, явные namespace-длоки не требуются, продолжать существующие паттерны проекта.
- `WarningsAsErrors` из корневого `Directory.Build.props` применяется автоматически.

---

### Task 1: S3Tool — интерфейс, конфиг, DI и S3Service на AWSSDK.S3

**Files:**
- Create: `BookShop/S3Tool/S3Tool.csproj`
- Create: `BookShop/S3Tool/Interfaces/IS3Service.cs`
- Create: `BookShop/S3Tool/Configs/S3Config.cs`
- Create: `BookShop/S3Tool/Implementations/S3Service.cs`
- Create: `BookShop/S3Tool/DependencyInjection.cs`
- Test: `tests/BookShopBot.Tests/S3/S3ServiceTests.cs`

**Interfaces:**
- Consumes: `BookShop.S3Tool.Interfaces.IS3Service` — до этой задачи интерфейс живёт в `TelegramBot/Interfaces/IS3Service.cs` (namespace `BookShop.S3Tool.Interfaces`). После Task 1 место жительства — S3Tool (Task 2 удаляет старый файл).
- Produces:
  - `S3Config { string Endpoint; string AccessKey; string SecretKey; string Bucket; TimeSpan PresignedLifetime; }` (namespace `BookShop.S3Tool.Configs`).
  - `IS3Service.CreateFile(Stream file, string folder, string fileName, string fileType)` → `Task<bool>`.
  - `IS3Service.GetPresignedUrl(string folder, string fileName, TimeSpan? lifetime = null)` → `string`.
  - `DependencyInjection.AddS3Client(this IServiceCollection services, IConfigurationSection section)` (namespace `BookShop.S3Tool`).

- [ ] **Step 1: Создать проект S3Tool**

`BookShop/S3Tool/S3Tool.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="AWSSDK.S3" Version="3.7.411.5" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.2" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="8.0.2" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Создать интерфейс и конфиг**

`BookShop/S3Tool/Interfaces/IS3Service.cs`:
```csharp
namespace BookShop.S3Tool.Interfaces;

public interface IS3Service
{
    Task<bool> CreateFile(Stream file, string folder, string fileName, string fileType);
    string GetPresignedUrl(string folder, string fileName, TimeSpan? lifetime = null);
}
```

`BookShop/S3Tool/Configs/S3Config.cs`:
```csharp
namespace BookShop.S3Tool.Configs;

public sealed class S3Config
{
    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "bookshop";
    public TimeSpan PresignedLifetime { get; set; } = TimeSpan.FromDays(365);
}
```

- [ ] **Step 3: Написать падающий тест S3Service**

Добавить в `tests/BookShopBot.Tests/BookShopBot.Tests.csproj` ProjectReference на S3Tool и пакет Moq:
```xml
  <ItemGroup>
    <PackageReference Include="Moq" Version="4.20.72" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\BookShop\S3Tool\S3Tool.csproj" />
    <ProjectReference Include="..\..\ChatFSM\Fsm.csproj" />
    <ProjectReference Include="..\..\TelegramBot\TelegramBot.csproj" />
  </ItemGroup>
```

`tests/BookShopBot.Tests/S3/S3ServiceTests.cs`:
```csharp
using Amazon.S3;
using Amazon.S3.Model;
using BookShop.S3Tool.Configs;
using BookShop.S3Tool.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BookShopBot.Tests.S3;

public sealed class S3ServiceTests
{
    private static S3Service BuildService(
        Mock<IAmazonS3> client,
        S3Config? config = null,
        bool bucketExists = true)
    {
        client.Setup(c => c.ListBucketsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(bucketExists
                ? new ListBucketsResponse
                {
                    Buckets = new List<S3Bucket> { new S3Bucket { BucketName = "bookshop" } },
                }
                : new ListBucketsResponse());
        var options = Options.Create(config ?? new S3Config());
        return new S3Service(client.Object, options, NullLogger<S3Service>.Instance);
    }

    [Fact]
    public async Task CreateFile_calls_PutObject_with_expected_key_and_content_type()
    {
        var client = new Mock<IAmazonS3>();
        var service = BuildService(client);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await service.CreateFile(stream, "BookShopUploads", "book.pdf", "application/pdf");

        Assert.True(result);
        client.Verify(c => c.PutObjectAsync(
            It.Is<PutObjectRequest>(r =>
                r.BucketName == "bookshop" &&
                r.Key == "BookShopUploads/book.pdf" &&
                r.ContentType == "application/pdf"),
            default), Times.Once);
    }

    [Fact]
    public async Task CreateFile_creates_bucket_when_missing()
    {
        var client = new Mock<IAmazonS3>();
        var service = BuildService(client, bucketExists: false);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        await service.CreateFile(stream, "f", "n.txt", "text/plain");

        client.Verify(c => c.PutBucketAsync("bookshop", default), Times.Once);
        client.Verify(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Once);
    }

    [Fact]
    public void GetPresignedUrl_uses_default_lifetime_when_null()
    {
        var client = new Mock<IAmazonS3>();
        client.Setup(c => c.GetPreSignedURL(It.IsAny<GetPreSignedURLRequest>()))
            .Returns((GetPreSignedURLRequest r) => r.Key);
        var service = BuildService(client);

        var url = service.GetPresignedUrl("BookShopUploads", "book.pdf");

        Assert.Contains("BookShopUploads/book.pdf", url);
        client.Verify(c => c.GetPreSignedURL(It.Is<GetPreSignedUrlRequest>(r =>
            r.Expires - DateTime.UtcNow >= TimeSpan.FromDays(364))), Times.Once);
    }

    [Fact]
    public void GetPresignedUrl_uses_explicit_lifetime()
    {
        var client = new Mock<IAmazonS3>();
        client.Setup(c => c.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Returns("http://presigned");
        var service = BuildService(client);

        var url = service.GetPresignedUrl("f", "n.png", TimeSpan.FromDays(2));

        Assert.Equal("http://presigned", url);
        client.Verify(c => c.GetPreSignedURL(It.Is<GetPreSignedUrlRequest>(r =>
            r.Expires - DateTime.UtcNow <= TimeSpan.FromDays(3))), Times.Once);
    }

    [Fact]
    public async Task CreateFile_returns_false_on_amazon_exception()
    {
        var client = new Mock<IAmazonS3>();
        client.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ThrowsAsync(new AmazonS3Exception("boom"));
        var service = BuildService(client);

        using var stream = new MemoryStream(new byte[] { 1 });
        var result = await service.CreateFile(stream, "f", "n", "text/plain");

        Assert.False(result);
    }
}
```

- [ ] **Step 4: Убедиться, что тесты падают**

Run: `dotnet test tests/BookShopBot.Tests/BookShopBot.Tests.csproj --filter FullyQualifiedName~S3.S3ServiceTests --verbosity minimal`
Expected: не компилируется — `S3Service`/`S3Config` не существуют (CS0246).

- [ ] **Step 5: Реализовать S3Service**

`BookShop/S3Tool/Implementations/S3Service.cs`:
```csharp
using Amazon.S3;
using Amazon.S3.Model;
using BookShop.S3Tool.Configs;
using BookShop.S3Tool.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookShop.S3Tool.Implementations;

public sealed class S3Service : IS3Service
{
    private readonly IAmazonS3 _client;
    private readonly S3Config _config;
    private readonly ILogger<S3Service> _logger;
    private readonly SemaphoreSlim _ensureLock = new(1, 1);
    private bool _bucketEnsured;

    public S3Service(IAmazonS3 client, IOptions<S3Config> options, ILogger<S3Service> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _config = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> CreateFile(Stream file, string folder, string fileName, string fileType)
    {
        try
        {
            await EnsureBucketAsync().ConfigureAwait(false);
            string key = BuildKey(folder, fileName);
            var request = new PutObjectRequest
            {
                BucketName = _config.Bucket,
                Key = key,
                ContentType = fileType,
                InputStream = file,
                AutoCloseStream = false,
            };
            await _client.PutObjectAsync(request).ConfigureAwait(false);
            return true;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "S3 upload failed for key {Key}", BuildKey(folder, fileName));
            return false;
        }
    }

    public string GetPresignedUrl(string folder, string fileName, TimeSpan? lifetime = null)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _config.Bucket,
            Key = BuildKey(folder, fileName),
            Expires = DateTime.UtcNow.Add(lifetime ?? _config.PresignedLifetime),
            Verb = HttpVerb.GET,
        };
        return _client.GetPreSignedURL(request);
    }

    private static string BuildKey(string folder, string fileName) => $"{folder}/{fileName}";

    private async Task EnsureBucketAsync()
    {
        if (_bucketEnsured)
        {
            return;
        }

        await _ensureLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_bucketEnsured)
            {
                return;
            }

            var listResponse = await _client.ListBucketsAsync().ConfigureAwait(false);
            bool exists = listResponse.Buckets.Any(b =>
                string.Equals(b.BucketName, _config.Bucket, StringComparison.Ordinal));
            if (!exists)
            {
                await _client.PutBucketAsync(_config.Bucket).ConfigureAwait(false);
            }

            _bucketEnsured = true;
        }
        finally
        {
            _ensureLock.Release();
        }
    }
}
```

- [ ] **Step 6: Реализовать DI-extension**

`BookShop/S3Tool/DependencyInjection.cs`:
```csharp
using Amazon.S3;
using BookShop.S3Tool.Configs;
using BookShop.S3Tool.Implementations;
using BookShop.S3Tool.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookShop.S3Tool;

public static class DependencyInjection
{
    public static IServiceCollection AddS3Client(this IServiceCollection services, IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        services.Configure<S3Config>(section);
        services.AddSingleton<IAmazonS3>(serviceProvider =>
        {
            var config = serviceProvider.GetRequiredService<IOptions<S3Config>>().Value;
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("BookShop.S3Tool");
            var environmentName = serviceProvider.GetService<IHostEnvironment>()?.EnvironmentName;
            var isDevelopment = environmentName == Environments.Development;

            if (string.IsNullOrWhiteSpace(config.AccessKey) || string.IsNullOrWhiteSpace(config.SecretKey))
            {
                if (!isDevelopment)
                {
                    throw new InvalidOperationException(
                        "S3 AccessKey/SecretKey не заданы в конфигурации (секция 'S3').");
                }

                logger.LogWarning("S3 AccessKey/SecretKey пусты. CreateFile/GetPresignedUrl упадут при первом обращении к MinIO.");
            }

            var amazonConfig = new AmazonS3Config
            {
                ServiceURL = config.Endpoint,
                ForcePathStyle = true,
            };
            return new AmazonS3Client(config.AccessKey, config.SecretKey, amazonConfig);
        });
        services.AddSingleton<S3Service>();
        services.AddSingleton<IS3Service>(sp => sp.GetRequiredService<S3Service>());

        return services;
    }
}
```

Примечание: `DoesS3BucketExistAsync`/`DoesS3BucketExistV2Async` — это extension-методы из `AmazonS3Util`, их нельзя замокать через Moq. Поэтому проверка существования бакета в `S3Service` реализована через интерфейсный метод `IAmazonS3.ListBucketsAsync` (мокается) — этот же метод сетапится в тестах.

- [ ] **Step 7: Запустить TDD-цикл и исправить нюансы моков**

Run: `dotnet test tests/BookShopBot.Tests/BookShopBot.Tests.csproj --filter FullyQualifiedName~S3.S3ServiceTests --verbosity minimal`
Expected: PASS (5/5). Если падают pre-signed-замеры (~1s погрешности) — ослабить окна в тестах, не меняя прод-логику.

- [ ] **Step 8: Полная валидация**

Register in solution (иначе `dotnet build`/`dotnet format`/`dotnet test` на слаке не покроют проект):
```
dotnet sln BookShop/BookShop.sln add BookShop/S3Tool/S3Tool.csproj --solution-folder CoreApi
```

Run (из корня worktree):
```
dotnet restore BookShop/BookShop.sln
dotnet build BookShop/BookShop.sln --no-restore -p:WarningsAsErrors=CS*
dotnet format BookShop/BookShop.sln --verify-no-changes --verbosity diagnostic
dotnet test BookShop/BookShop.sln --no-build --verbosity normal
```
Expected: build 0/0, format без правок, все тесты зелёные.

- [ ] **Step 9: Commit**

```bash
git add BookShop/S3Tool BookShop/BookShop.sln tests/BookShopBot.Tests/S3 tests/BookShopBot.Tests/BookShopBot.Tests.csproj
git commit -m "feat(s3): implement IS3Service on AWSSDK.S3 with presigned urls (MinIO-ready)"
```

---

### Task 2: Миграция TelegramBot на IS3Service из S3Tool

**Files:**
- Delete: `TelegramBot/Interfaces/IS3Service.cs`
- Modify: `TelegramBot/Services/States/WaitFileName.cs` (вызов `GetPublicUrl` → `GetPresignedUrl`)
- Modify: `TelegramBot/TelegramBot.csproj` (ProjectReference на S3Tool)

**Interfaces:**
- Consumes: `IS3Service.GetPresignedUrl(string folder, string fileName, TimeSpan? lifetime = null)` (Task 1).
- Produces: обновлённая зависимость бота от S3Tool — `IS3Service` больше не существует в `TelegramBot/Interfaces`.

- [ ] **Step 1: Обновить csproj**

`TelegramBot/TelegramBot.csproj` — в `ItemGroup` с ProjectReference добавить:
```xml
    <ProjectReference Include="..\BookShop\S3Tool\S3Tool.csproj" />
```

- [ ] **Step 2: Обновить WaitFileName**

В `TelegramBot/Services/States/WaitFileName.cs` заменить `var fileUrl = _s3Service.GetPublicUrl(_uploadFolder, filename);`
на `var fileUrl = _s3Service.GetPresignedUrl(_uploadFolder, filename);`.

- [ ] **Step 3: Удалить старый интерфейс**

Удалить `TelegramBot/Interfaces/IS3Service.cs`.

- [ ] **Step 4: Валидация**

Run (из корня worktree):
```
dotnet restore BookShop/BookShop.sln
dotnet build BookShop/BookShop.sln --no-restore -p:WarningsAsErrors=CS*
dotnet test BookShop/BookShop.sln --no-build --verbosity normal
```
Expected: build 0/0 (включая `TelegramBotSessionIntegrationTests`), тесты зелёные.

- [ ] **Step 5: Commit**

```bash
git add -A TelegramBot
git commit -m "refactor(telegrambot): use S3Tool IS3Service (GetPresignedUrl)"
```

---

### Task 3: BotApi — хост бота

**Files:**
- Create: `BookShop/BotApi/BotApi.csproj`
- Create: `BookShop/BotApi/Program.cs`
- Create: `BookShop/BotApi/Properties/launchSettings.json`
- Create: `BookShop/BotApi/appsettings.json`
- Create: `BookShop/BotApi/appsettings.Local.json` (gitignored, пустые значения)
- Test: `tests/BookShopBot.Tests/S3/BotApiSmokeTests.cs`
- Modify: `BookShop/BookShop.sln` (регистрация BotApi)

**Interfaces:**
- Consumes: `AddTelegramBotClient(IConfigurationSection)` (`TelegramBot/Extensions/DiExtensions.cs`), `AddChatSessionPersistence(IConfiguration)` (`TelegramBot/Extensions/ChatSessionPersistenceExtensions.cs`), `AddServices()` (`DiExtensions`), `AddS3Client(IConfigurationSection)` (Task 1), gRPC proto `book_service.v1.Book`.

- [ ] **Step 1: Создать csproj (Web SDK + gRPC client на каталог)**

`BookShop/BotApi/BotApi.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Grpc.Net.Client" Version="2.65.0" />
    <PackageReference Include="Google.Protobuf" Version="3.27.1" />
    <PackageReference Include="Grpc.Tools" Version="2.65.0" PrivateAssets="All" />
    <PackageReference Include="Telegram.Bot" Version="22.9.0" />
  </ItemGroup>

  <ItemGroup>
    <Protobuf Include="..\BookCatalogService\Features\BooksManagement\Protos\book_service.proto" GrpcServices="Client" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\TelegramBot\TelegramBot.csproj" />
    <ProjectReference Include="..\S3Tool\S3Tool.csproj" />
    <ProjectReference Include="..\BookShop.ServiceDefaults\BookShop.ServiceDefaults.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Создать launchSettings**

`BookShop/BotApi/Properties/launchSettings.json`:
```json
{
  "$schema": "http://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5200",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

- [ ] **Step 3: Создать appsettings**

`BookShop/BotApi/appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "S3": {
    "Endpoint": "http://localhost:9000",
    "Bucket": "bookshop",
    "PresignedLifetime": "365.00:00:00"
  }
}
```

`BookShop/BotApi/appsettings.Local.json`:
```json
{
  "BotConfig": {
    "Token": ""
  },
  "S3": {
    "AccessKey": "",
    "SecretKey": ""
  },
  "ConnectionStrings": {
    "ChatSession": ""
  }
}
```

- [ ] **Step 4: Написать DI smoke-тест (падающий на отсутствии Program-звеньев НЕ нужно — это интеграция)**

`tests/BookShopBot.Tests/S3/BotApiSmokeTests.cs`:
```csharp
using BookShop.S3Tool.Interfaces;
using BookShop.TelegramBot.Extensions;
using BookShop.TelegramBot.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookShopBot.Tests.S3;

public sealed class BotApiSmokeTests
{
    private sealed class StubConfiguration : IConfiguration, IConfigurationRoot
    {
        private readonly IConfigurationRoot _inner;
        public StubConfiguration() => _inner = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BotConfig:Token"] = "TEST-TOKEN",
            ["S3:Endpoint"] = "http://localhost:9000",
            ["S3:Bucket"] = "bookshop",
            ["S3:AccessKey"] = "minioadmin",
            ["S3:SecretKey"] = "minioadmin",
            ["ConnectionStrings:ChatSession"] = "Host=localhost;Database=bookshop_bot",
        }).Build();
        public string? this[string key] { get => _inner[key]; set => _inner[key] = value; }
        public IEnumerable<IConfigurationSection> GetChildren() => _inner.GetChildren();
        public IChangeToken GetReloadToken() => _inner.GetReloadToken();
        public IConfigurationSection GetSection(string key) => _inner.GetSection(key);
        public void Reload() => _inner.Reload();
        public IReadOnlyList<IConfigurationProvider> Providers => _inner.Providers;
    }

    [Fact]
    public void Full_bot_di_graph_resolves_without_external_services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new StubConfiguration();

        services.AddTelegramBotClient(configuration.GetSection("BotConfig"));
        services.AddChatSessionPersistence(configuration);
        services.AddS3Client(configuration.GetSection("S3"));
        services.AddServices();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ITelegramBotClient>());
        Assert.NotNull(provider.GetRequiredService<IS3Service>());
        Assert.NotNull(provider.GetRequiredService<IMemoryCacheSessionRepository>());
    }
}
```

Примечание: в тестовый csproj добавить пакет `Microsoft.Extensions.Configuration` версии 8.0.0 — `AddInMemoryCollection` (namespace `Microsoft.Extensions.Configuration.Memory`) входит в этот пакет, отдельного пакета `Microsoft.Extensions.Configuration.Memory` не существует.

- [ ] **Step 5: Создать Program.cs**

`BookShop/BotApi/Program.cs`:
```csharp
using BookShop.TelegramBot.Extensions;
using BookShop.TelegramBot.Services.HostedServices;
using BookService.V1;
using Grpc.Net.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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
```

Примечание: сгенерированный gRPC-клиент — вложенный тип `BookService.V1.BookCatalogService.BookCatalogServiceClient` (namespace из proto `package book_service.v1`). Статического `Create` нет — только конструкторы (от `GrpcChannel`/`CallInvoker`), используем `new BookCatalogService.BookCatalogServiceClient(channel)` (см. шаблон в `BookRecognitionService/Program.cs`).

- [ ] **Step 6: Зарегистрировать BotApi в решении**

Run (из корня worktree):
```
dotnet sln BookShop/BookShop.sln add BookShop/BotApi/BotApi.csproj --solution-folder CoreApi
```

- [ ] **Step 7: Валидация**

Run:
```
dotnet restore BookShop/BookShop.sln
dotnet build BookShop/BookShop.sln --no-restore -p:WarningsAsErrors=CS*
dotnet format BookShop/BookShop.sln --verify-no-changes --verbosity diagnostic
dotnet test BookShop/BookShop.sln --no-build --verbosity normal
```
Expected: build 0/0, включая новый `BotApiSmokeTests` (зелёный), все тесты проходят.

- [ ] **Step 8: Commit**

```bash
git add BookShop/BotApi BookShop/BookShop.sln tests/BookShopBot.Tests/S3/BotApiSmokeTests.cs tests/BookShopBot.Tests/BookShopBot.Tests.csproj
git commit -m "feat(botapi): add Telegram bot host with S3, sessions, gRPC catalog client"
```

---

### Task 4: Aspire AppHost — Postgres, MinIO, BotApi

**Files:**
- Modify: `BookShop/BookShop.AppHost/BookShop.AppHost.csproj` (+ `Aspire.Hosting.PostgreSQL`)
- Modify: `BookShop/BookShop.AppHost/AppHost.cs`
- Modify: `.gitignore` (паттерны Local)

**Interfaces:**
- Consumes: BotApi project (Task 3), существующие AddProject для catalog/recognition/chatapi.
- Produces: dev-запуск всего стека одним F5: Postgres (порт 5432), MinIO (9000/9001), 4 сервиса.

- [ ] **Step 1: Обновить .gitignore**

Добавить в конец `.gitignore`:
```gitignore
# Локальные секреты (никогда в git)
**/appsettings.Local.json
**/appsettings.*.Local.json
```

- [ ] **Step 2: Добавить пакет Aspire.Hosting.PostgreSQL**

`BookShop/BookShop.AppHost/BookShop.AppHost.csproj` — в `ItemGroup` с PackageReference добавить:
```xml
    <PackageReference Include="Aspire.Hosting.PostgreSQL" Version="9.5.0" />
```

- [ ] **Step 3: Переписать AppHost.cs**

`BookShop/BookShop.AppHost/AppHost.cs`:
```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.BookCatalogService>("bookcatalogservice")
    .WithReference(postgres);

var minio = builder.AddContainer("minio", "minio/minio", "RELEASE.2024-08-03T04-33-23Z")
    .WithContainerPort(9000, name: "s3")
    .WithContainerPort(9001, name: "console")
    .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
    .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
    .WithVolume("minio-data", "/data")
    .WithVolume("minio-config", "/root/.minio")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.BookRecognitionService>("bookrecognitionservice");

builder.AddProject<Projects.BotApi>("botapi")
    .WithReference(postgres)
    .WithReference(minio)
    .WithEnvironment("S3__Endpoint", "http://localhost:9000")
    .WithEnvironment("S3__AccessKey", "minioadmin")
    .WithEnvironment("S3__SecretKey", "minioadmin");

builder.AddProject<Projects.ChatApi>("chatapi");

await builder.Build().RunAsync();
```

Примечание: если `WithContainerPort`/`WithVolume`/`WithArgs` недоступны в Aspire 9.5.0 API — собрать контейнер максимально простым способом:
```csharp
var minio = builder.AddContainer("minio", "minio/minio", "latest")
    .WithEndpoint(port: 9000, targetPort: 9000, name: "s3")
    .WithEndpoint(port: 9001, targetPort: 9001, name: "console")
    .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
    .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
    .WithVolume("minio-data", "/data")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithLifetime(ContainerLifetime.Persistent);
```
Использовать тот вариант, который компилируется под Aspire 9.5.0. Поиск API при необходимости: `dotnet aspire docs` (см. `https://learn.microsoft.com/en-us/dotnet/aspire/container-runtime`).

- [ ] **Step 4: Добавить BotApi в ProjectReference AppHost**

`BookShop/BookShop.AppHost/BookShop.AppHost.csproj` — в ItemGroup с ProjectReference добавить:
```xml
    <ProjectReference Include="..\BotApi\BotApi.csproj" />
```

- [ ] **Step 5: Валидация**

Run:
```
dotnet restore BookShop/BookShop.sln
dotnet build BookShop/BookShop.sln --no-restore -p:WarningsAsErrors=CS*
dotnet format BookShop/BookShop.sln --verify-no-changes --verbosity diagnostic
dotnet test BookShop/BookShop.sln --no-build --verbosity normal
```
Expected: сборка AppHost успешна (без запуска), тесты зелёные.

- [ ] **Step 6: Commit**

```bash
git add BookShop/BookShop.AppHost .gitignore
git commit -m "feat(apphost): add Postgres, MinIO and BotApi to Aspire orchestration"
```

---

### Task 5: Dockerfile + docker-compose + .env

**Files:**
- Create: `Dockerfile` (единый multi-stage, `ARG SERVICE`, в корне репо — чтобы build-context включал `ChatFSM/`, `TelegramBot/`, `tests/`, `Directory.Build.props`)
- Create: `.dockerignore`
- Create: `docker-compose.yml`
- Create: `.env.example`
- Create: локальный `.env` (gitignored уже через `*.env`), если нужен для локального `docker compose up`

**Interfaces:**
- Consumes: BotApi (Task 3), существующие сервисы; протоколы: gRPC `book_service.v1`, health `/health`.
- Produces: `docker compose up -d` поднимает Postgres + MinIO + 4 сервиса.

- [ ] **Step 1: Создать Dockerfile (в корне репо)**

`Dockerfile` (в корне worktree):
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG SERVICE=BotApi
WORKDIR /src

COPY Directory.Build.props ./
COPY BookShop/BookShop.sln ./BookShop/
COPY ChatFSM/ ./ChatFSM/
COPY TelegramBot/ ./TelegramBot/
COPY BookShop/ ./BookShop/
COPY tests/ ./tests/

RUN dotnet restore BookShop/BookShop.sln

RUN dotnet publish BookShop/${SERVICE}/${SERVICE}.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ARG SERVICE
COPY --from=build /app/publish .
ENV SERVICE=${SERVICE}
ENTRYPOINT ["sh", "-c", "dotnet /app/${SERVICE}.dll"]
```

Примечание: `SERVICE` принимает имена `BotApi | BookCatalogService | BookRecognitionService | ChatApi` (совпадает с именами csproj). Для S3Tool/ServiceDefaults Dockerfile не нужен (библиотеки). Dockerfile **обязательно в корне репозитория**, т.к. `dotnet restore` решения требует `ChatFSM/`, `TelegramBot/`, `tests/` и корневой `Directory.Build.props`, а build-context не может выходить за свою директорию.

- [ ] **Step 1b: Создать .dockerignore**

`.dockerignore` (в корне worktree):
```
**/bin/
**/obj/
**/.vs/
**/.worktrees/
**/node_modules/
*.user
Dockerfile
docker-compose.yml
.env
.env.*
!.env.example
```

- [ ] **Step 2: Создать docker-compose.yml**

`docker-compose.yml`:
```yaml
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: ${POSTGRES_USER:-bookshop}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-bookshop}
      POSTGRES_DB: ${POSTGRES_DB:-bookshop}
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB}"]
      interval: 5s
      timeout: 5s
      retries: 10

  minio:
    image: minio/minio:latest
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: ${MINIO_ROOT_USER:-minioadmin}
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD:-minioadmin}
    ports:
      - "9000:9000"
      - "9001:9001"
    volumes:
      - minio-data:/data

  catalog:
    build:
      context: .
      dockerfile: Dockerfile
      args:
        SERVICE: BookCatalogService
    environment:
      ConnectionStrings__DefaultConnectionString: "Host=postgres;Port=5432;Database=${POSTGRES_DB:-bookshop};Username=${POSTGRES_USER:-bookshop};Password=${POSTGRES_PASSWORD:-bookshop}"
    depends_on:
      postgres:
        condition: service_healthy

  recognition:
    build:
      context: .
      dockerfile: Dockerfile
      args:
        SERVICE: BookRecognitionService
    environment:
      DeepSeekConfig__ApiKey: ${DEEPSEEK_API_KEY:-}
      BookCatalogService__Url: http://catalog:5000
    depends_on:
      - catalog

  botapi:
    build:
      context: .
      dockerfile: Dockerfile
      args:
        SERVICE: BotApi
    environment:
      BotConfig__Token: ${BOT_TOKEN:-}
      S3__Endpoint: http://minio:9000
      S3__AccessKey: ${MINIO_ROOT_USER:-minioadmin}
      S3__SecretKey: ${MINIO_ROOT_PASSWORD:-minioadmin}
      S3__Bucket: bookshop
      ConnectionStrings__ChatSession: "Host=postgres;Port=5432;Database=${POSTGRES_DB:-bookshop};Username=${POSTGRES_USER:-bookshop};Password=${POSTGRES_PASSWORD:-bookshop}"
      BookCatalogService__Url: http://catalog:5000
    depends_on:
      postgres:
        condition: service_healthy
      minio:
        condition: service_started

  chatapi:
    build:
      context: .
      dockerfile: Dockerfile
      args:
        SERVICE: ChatApi
    depends_on:
      - catalog

volumes:
  postgres-data:
  minio-data:
```

- [ ] **Step 3: Создать .env.example**

`.env.example`:
```
# Telegram
BOT_TOKEN=

# MinIO (root пользователь хранилища)
MINIO_ROOT_USER=minioadmin
MINIO_ROOT_PASSWORD=minioadmin

# PostgreSQL
POSTGRES_USER=bookshop
POSTGRES_PASSWORD=bookshop
POSTGRES_DB=bookshop

# DeepSeek AI
DEEPSEEK_API_KEY=
```

- [ ] **Step 4: Проверить конфигурацию compose**

Run (из корня worktree): `docker compose config --quiet`
Expected: без ошибок (валидный синтаксис). Если Docker недоступен/не установлен — выполнить сухой просмотр синтаксиса YAML любым способом и отметить в self-review.

- [ ] **Step 5: Commit**

```bash
git add BookShop/Dockerfile docker-compose.yml .env.example
git commit -m "feat(deploy): add docker-compose with Postgres, MinIO and 4 services"
```

---

### Task 6: docs/deployment.md — стратегии деплоя

**Files:**
- Create: `docs/deployment.md`

**Interfaces:**
- Consumes: конфигурация из Task 3–5.

- [ ] **Step 1: Создать документ**

`docs/deployment.md` со следующими обязательными разделами:

1. **Стратегии одним экраном** — таблица: Docker Compose vs Aspire (dev) vs Kubernetes (почему нет).
2. **Быстрый старт (Docker Compose)** — `cp .env.example .env`, вписать креды, `docker compose up -d`, проверить `docker compose ps`; где логи (`docker compose logs -f botapi`).
3. **Креды** — таблица всех переменных: `BOT_TOKEN`, `MINIO_ROOT_USER/PASSWORD`, `POSTGRES_USER/PASSWORD/DB`, `DEEPSEEK_API_KEY`; где они применяются (botapi/catalog/recognition) и что будет при пустых значениях.
4. **Локальный деплой через Aspire** — `dotnet run --project BookShop/BookShop.AppHost`; F5 в VS; что поднимет: Postgres, MinIO (консоль на http://localhost:9001), dashbord otel.
5. **Локальные секреты** — как работают `appsettings.Local.json` + `.gitignore`; пример заполнения.
6. **Почему не Kubernetes** — когда K8s/Aspirate становится нужен (маcштабирование, автоскейлинг, 100M DAU цель из design-doc) и как перейти (Aspire manifest → Aspirate → K8s).
7. **Сборка образа вручную** — `docker build -f Dockerfile --build-arg SERVICE=BotApi -t bookshop-botapi .` (из корня репо; DNS: `dotnet build . -p:...` изнутри).

- [ ] **Step 2: Commit**

```bash
git add docs/deployment.md
git commit -m "docs(deploy): document docker-compose, Aspire and credentials strategies"
```

---

### Task 7: Финальная проверка и PR

**Files:**
- All files above.

- [ ] **Step 1: Полная валидация из чистого состояния**

Run (из корня worktree):
```
dotnet restore BookShop/BookShop.sln
dotnet build BookShop/BookShop.sln --no-restore -p:WarningsAsErrors=CS*
dotnet format BookShop/BookShop.sln --verify-no-changes --verbosity diagnostic
dotnet test BookShop/BookShop.sln --no-build --verbosity normal
```
Expected: 0 предупреждений, 0 ошибок, все тесты (включая package advisory) зелёные.

- [ ] **Step 2: Проверить git status — в git не попали секреты**

Run: `git status --short` и `git check-ignore BookShop/BotApi/appsettings.Local.json`
Expected: Local-файл игнорируется; в индексе нет `appsettings.Local.json`.

- [ ] **Step 3: Push и PR**

```bash
git push -u origin task/8-s3-botapi-deploy
```
Затем создать PR в `main` (через `gh` или веб), title: `feat: MinIO S3, BotApi host, local credentials and docker deploy`.

- [ ] **Step 4: (По чек-листу reviewer'а) Проверить PR**

Review: S3Service presigned-логика, отсутствие секретов в diff, Dockerfile/docker-compose согласованы с конфигами, `docs/deployment.md` покрывает стратегии, тесты не запускают реальные контейнеры.

---

## Self-Review (проверено вручную)

1. **Spec coverage:** S3Tool (Task 1) ✓, TelegramBot миграция (Task 2) ✓, BotApi (Task 3) ✓, AppHost Postgres/MinIO/BotApi (Task 4) ✓, .gitignore+Local.json (Task 4.1 + Task 3.3) ✓, Dockerfile+compose+.env (Task 5) ✓, deployment.md сравнение стратегий (Task 6) ✓. Все секции дизайн-спеки покрыты.
2. **Placeholder scan:** без TBD/TODO; коды реальные; API-нюансы (вёрстка `GetPreSignedURL` Moq, Aspire container API, `BookCatalogServiceClient`) оговорены конкретными fallback-вариантами.
3. **Type consistency:** `IS3Service.CreateFile`/`GetPresignedUrl` сигнатуры совпадают через все задачи; `AddS3Client(IConfigurationSection)` единообразен; `SERVICE` в Docker = имя csproj (BotApi/BookCatalogService/BookRecognitionService/ChatApi).