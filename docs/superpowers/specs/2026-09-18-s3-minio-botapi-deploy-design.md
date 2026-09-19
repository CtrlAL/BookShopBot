# Design: S3/MinIO интеграция, хост бота (BotApi), креды и деплой

Дата: 2026-09-18
Статус: approved (брейншторм завершён)

## Проблема

1. `IS3Service` (namespace `BookShop.S3Tool.Interfaces`) — интерфейс без реализации. Вызывается из
   `TelegramBot/Services/States/WaitFileName.cs` (`CreateFile`, `GetPublicUrl`). Реального хранилища нет.
2. Telegram-бот — библиотека (`TelegramBot/`) без `Program.cs`, нигде не хостится. AppHost запускает
   только catalog/recognition/chatapi; бот не работает.
3. Секреты (Telegram token, MinIO/DeepSeek/Postgres креды) нигде не хранятся локально, нет схемы.
4. Нет деплоя: нет Dockerfile, docker-compose, тасок для Aspire. K8s не требуется.

## Цель

- Реализовать `IS3Service` на MinIO (S3-совместимый локальный стэк) через AWSSDK.S3 с presigned URL.
- Создать хост бота `BotApi` (Web SDK) и подключить в AppHost.
- Ввести локальный секретный файл `appsettings.Local.json` (в `.gitignore`) с разделом кредов.
- Настроить деплой: единый Dockerfile + docker-compose (Postgres + MinIO + 4 сервиса) + Aspire
  (AppHost с Postgres/MinIO-контейнерами). README со сравнением стратегий.

## Решения (одобрены)

| Вопрос | Решение |
|---|---|
| Где живёт S3 | Новый проект-библиотека `BookShop/S3Tool/`, `IS3Service` переносится из `TelegramBot/Interfaces`. |
| Клиент MinIO | `AWSSDK.S3` с кастомным endpoint (ForcePathStyle=true), переиспользуем для облачного S3. |
| Публичность | Только presigned URL, бакет не публичный. |
| TTL presigned | 1 год (default), настраивается в `S3:PresignedLifetime`. |
| Имя метода | `GetPublicUrl` → `GetPresignedUrl(folder, fileName, TimeSpan? lifetime = null)`. |
| Хост бота | Новый `BookShop/BotApi/` (Microsoft.NET.Sdk.Web), long-polling через существующий `TelegramBotHostedService`. |
| Креды | `appsettings.Local.json` (`.gitignore`), значения пустые, вписывает человек. |
| Мусор ChatApi | Не трогаем (вне задачи). |
| Деплой | Единый `BookShop/Dockerfile` (ARG SERVICE), корневой `docker-compose.yml`, AppHost дополняется Postgres+MinIO+BotApi. |

## Архитектура

### Новые/изменённые проекты

```
BookShop/
  S3Tool/                          # LIB net8.0, nullable enable
    Interfaces/IS3Service.cs       # перенесён + GetPresignedUrl
    Configs/S3Config.cs
    Implementations/S3Service.cs
    DependencyInjection.cs         # AddS3Client(IConfigurationSection)
  BotApi/                          # WEB net8.0, nullable enable
    Program.cs
    Properties/launchSettings.json
    appsettings.json               # без секретов
    appsettings.Local.json         # секреты, gitignored (шаблон с пустыми значениями)
TelegramBot/
  Interfaces/IS3Service.cs         # УДАЛЯЕТСЯ (перенесён в S3Tool)
  Services/States/WaitFileName.cs  # GetPublicUrl → GetPresignedUrl
  TelegramBot.csproj               # + ProjectReference S3Tool
BookShop/BookShop.AppHost/AppHost.cs  # + postgres, minio, botapi
```

### IS3Service

```csharp
namespace BookShop.S3Tool.Interfaces;

public interface IS3Service
{
    Task<bool> CreateFile(Stream file, string folder, string fileName, string fileType);
    string GetPresignedUrl(string folder, string fileName, TimeSpan? lifetime = null);
}
```

### S3Config

```csharp
public sealed class S3Config
{
    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "bookshop";
    public TimeSpan PresignedLifetime { get; set; } = TimeSpan.FromDays(365);
}
```

### S3Service (AWSSDK.S3)

- `CreateFile`: `PutObjectAsync`, key=`folder/fileName`, ContentType=fileType. Возвращает успешность.
- `GetPresignedUrl`: `GetPreSignedURLRequest` (Expires из параметра или `PresignedLifetime`).
- Bucket создаётся при первом старте: `BucketExistsAsync` → `PutBucketAsync` (lazy/при инициализации DI-синглтона).
- `AmazonS3Client` конфигурируется с `ForcePathStyle=true` (требование MinIO), endpoint строка.

### DI (S3Tool)

```csharp
services.AddS3Client(IConfigurationSection section)
// - Configure<S3Config>(section)
// - AddSingleton<S3Service>, AddSingleton<IS3Service>
// - AddSingleton<IAmazonS3>(sp => ... ForcePathStyle)
// Валидация: AccessKey/SecretKey пустые в Development → log warning (fail при первом вызове),
// в Production → throw на старте.
```

### BotApi/Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();                                     // otel, health, discovery
builder.Services.AddTelegramBotClient(builder.Configuration.GetSection("BotConfig"));
builder.Services.AddChatSessionPersistence(builder.Configuration);
builder.Services.AddS3Client(builder.Configuration.GetSection("S3"));
builder.Services.AddServices();                                    // FSM, UpdateHandler, ChatContext, states
builder.Services.AddSingleton(provider => /* gRPC BookCatalogServiceClient, fallback Url */);
builder.Services.AddHostedService<TelegramBotHostedService>();   // long-polling

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapGet("/", () => "BookShop Bot is running");
app.Run();
```

gRPC-клиент копирует паттерн `BookRecognitionService/Program.cs` (fallback
`BookCatalogService:Url` → `Services:BookCatalogService:Url` → `http://book-catalog-service:5000`,
DangerousAcceptAnyServerCertificateValidator под флаг `GrpcClient:IgnoreSslErrors`).

### Бот и текст

`WaitFileNameProcessFileUploadAsync` вызывает `_s3Service.GetPresignedUrl(...)`.
Текст «Ссылка действительна очень долго» остаётся: presigned на 1 год корректен.

## Креды

### appsettings.json (в git) — BotApi

```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "S3": {
    "Endpoint": "http://localhost:9000",
    "Bucket": "bookshop",
    "PresignedLifetime": "365.00:00:00"
  }
}
```

### appsettings.Local.json (gitignored) — шаблон с пустыми значениями

```json
{
  "BotConfig": { "Token": "" },
  "S3": { "AccessKey": "", "SecretKey": "" },
  "ConnectionStrings": { "ChatSession": "" }
}
```

Аналогичные `appsettings.Local.json` создаются для Catalog (`ConnectionStrings:DefaultConnectionString`)
и Recognition (`DeepSeekConfig:ApiKey`), где применяются.

### .gitignore

```
# Локальные секреты (никогда в git)
**/appsettings.Local.json
**/appsettings.*.Local.json
```

## Деплой

### Единый Dockerfile — `BookShop/Dockerfile`

Multi-stage: `sdk:8.0` build → `aspnet:8.0` runtime. `ARG SERVICE` определяет проект
(напр. `botapi`, `bookcatalogservice`, `bookrecognitionservice`, `chatapi`). Копирует
`sln` + `Directory.Build.props` + проекты по списку.

### docker-compose.yml (корень)

```yaml
services:
  postgres: image postgres:16-alpine; POSTGRES_USER/PASSWORD/DB из .env
  minio:    image minio/minio; ports 9000/9001; volume; cmd server /data --console-address :9001
  botapi:      build { dockerfile: BookShop/Dockerfile, args: { SERVICE: botapi } };
               env: ConnectionStrings__ChatSession, S3:AccessKey/SecretKey; depends_on postgres, minio
  catalog:     build SERVICE=bookcatalogservice; env ConnectionStrings__DefaultConnectionString
  recognition: build SERVICE=bookrecognitionservice; env DeepSeekConfig__ApiKey
  chatapi:     build SERVICE=chatapi
```

Секреты из `.env` (gitignored): `BOT_TOKEN`, `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`,
`POSTGRES_PASSWORD`, `DEEPSEEK_API_KEY`. В git кладётся `.env.example` без значений.

### Aspire AppHost

- Пакеты: `Aspire.Hosting.PostgreSQL`, `Aspire.Hosting.MinIO`.
- `AddPostgres("postgres")`, `AddMinio("minio")`.
- `AddProject<Projects.BotApi>("botapi")` с `WithReference(postgres)`, `WithReference(minio)`,
  `WithEnvironment("ConnectionStrings__ChatSession", ...)`.
- Catalog — `WithReference(postgres)`.
- Recognition/ChatApi — AddProject как сейчас.

### docs/deployment.md

Сравнение стратегий:
1. **Docker Compose** — один хост/VPS, `docker compose up -d`, `.env`, где креды.
2. **Aspire в dev** — F5, otel-дашборд, контейнеры Postgres/MinIO автоматически.
3. **Почему не K8s** — Aspirate/manifest как будущий мост, сейчас overkill.

## Ошибки/Edge cases

- MinIO доступен по `http://localhost:9000` локально; в compose — сервис `minio:9000`.
- `PresignedLifetime` конфигурируется как строкой (json), так и через env `S3__PresignedLifetime`.
- S3 без кредов в Development: warning, не падение (бот при первом upload→CreateFile получит
  `AmazonS3Exception` — логируем в `WaitFileName` как сейчас).
- Bucket создаётся лениво; если MinIO недоступен на старте — retry не добавляем (YAGNI),
  первая реальная загрузка упадёт с понятной ошибкой.

## Тестирование

- Юнит-тесты `S3Service` с замоканным `IAmazonS3` (PutObjectAsync, presign-логика ключа/срока,
  валидация кредов, bucket-ensure).
- Интеграционный smoke: `AddChatSessionPersistence` + `AddS3Client` DI-граф резолвится без MinIO.
- Build/lint/test по правилам проекта (см. `.opencode/rules.md`).

## Вне зоны

- Реальный облачный S3 (Yandex/AWS) — только окружение/конфиг.
- Вебхуки Telegram (заменяют long-polling) — не сейчас.
- K8s/манифесты/Aspirate.
- Удаление мусора ChatApi (WeatherForecast и т.п.).