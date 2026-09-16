# Task: Интеграция session-слоя в TelegramBot + smoke-тесты

## Context & Objective

Бот (`TelegramBot/`) сейчас использует in-memory `MemoryCacheSessionRepository`
(без персистентности). `ChatFSM` уже имеет `PostgresChatSessionRepository`,
`CachedChatSessionRepository`, `SessionStorageOptions` и
`ChatSessionCleanupService` (Tasks 2–3). Задача — подключить их к боту
и проверить smoke-тестом.

Бот — библиотека без `Program.cs`; хостинг недоступен. Интеграция =
расширение для DI, которое потребитель (Aspire AppHost/внешний хост) вызывает
явно. `SchemaInitializationService` и `ChatSessionCleanupService` работают
только при наличии `IHostedService`-хоста; smoke-тест валидирует DI без реальной
БД.

## Interface Contract

```csharp
// TelegramBot/Extensions/ChatSessionPersistenceExtensions.cs
public static IServiceCollection AddChatSessionPersistence(
    this IServiceCollection services,
    IConfiguration configuration);

// Регистрирует:
// - NpgsqlDataSource (ConnectionStrings:ChatSession)
// - SessionStorageOptions + SessionCacheOptions (defaults)
// - PostgresChatSessionRepository<TelegramChatSession> (scoped)
// - IMemoryCacheSessionRepository → CachedChatSessionRepository<TelegramChatSession>
// - SchemaInitializationService (IHostedService)
// - ChatSessionCleanupService (IHostedService)
```

```csharp
// TelegramBot/Services/HostedServices/SchemaInitializationService.cs
public sealed class SchemaInitializationService : IHostedService
{
    public SchemaInitializationService(
        NpgsqlDataSource dataSource,
        IOptions<SessionStorageOptions> options,
        ILogger<SchemaInitializationService> logger);

    // StartAsync: ChatSessionSchema.EnsureCreatedAsync(...).
    // При ошибке — логирует и продолжает (без падения хоста).
    public Task StartAsync(CancellationToken ct);
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

`IMemoryCacheSessionRepository` создаётся как `CachedChatSessionRepository`
обёрткой над `PostgresChatSessionRepository`. Все вызовы (`GetOrCreateAsync`,
`SaveAsync`, `RemoveAsync`, `ExistsAsync`) проходят кэш → Postgres.
При выселении из `IMemoryCache` отменяется `ActionCts` (Task 3).

## Acceptance Criteria

- [ ] Критерий 1: `TelegramBot/Extensions/ChatSessionPersistenceExtensions.cs`
  существует; `AddChatSessionPersistence` корректно регистрирует все компоненты
  (NpgsqlDataSource, options, repositories, hosted-сервисы).
- [ ] Критерий 2: `TelegramBot/Services/HostedServices/SchemaInitializationService.cs`
  существует; вызывает `ChatSessionSchema.EnsureCreatedAsync` в `StartAsync`;
  при исключении логирует и не падает.
- [ ] Критерий 3: `DiExtensions.AddServices()` **не содержит** регистрации
  `IMemoryCacheSessionRepository` (перенесена в `AddChatSessionPersistence`).
- [ ] Критерий 4: smoke-тест (`TelegramBotSessionIntegrationTests.cs`):
  строит `ServiceCollection`, вызывает `AddServices()` + `AddChatSessionPersistence`
  (с `IConfiguration`-заглушкой), резолвит `IMemoryCacheSessionRepository` →
  `CachedChatSessionRepository<TelegramChatSession>`, проверяет `GetOrCreateAsync`
  возвращает не-`null`.
- [ ] Критерий 5: smoke-тест проверяет, что в `IHostedService`-коллекции
  присутствуют `SchemaInitializationService` и `ChatSessionCleanupService`.
- [ ] Критерий 6: `dotnet build --no-restore -p:WarningsAsErrors=CS*`
  завершается 0 ошибок, 0 CS-предупреждений.
- [ ] Критерий 7: `dotnet format --verify-no-changes` проходит.
- [ ] Критерий 8: все существующие 53 теста не падают (нет регрессий).

## Affected Files

| Файл | Действие |
|------|----------|
| `TelegramBot/Extensions/ChatSessionPersistenceExtensions.cs` | создание |
| `TelegramBot/Services/HostedServices/SchemaInitializationService.cs` | создание |
| `TelegramBot/Extensions/DiExtensions.cs` | изменение (удалить строку регистрации `IMemoryCacheSessionRepository`) |
| `tests/BookShopBot.Tests/TelegramBot/TelegramBotSessionIntegrationTests.cs` | создание |

> Запрещено изменять `.csproj` TelegramBot/ChatFSM, `.sln`, `.gitignore`,
> `scripts/tdd_loop.py`, `.tasks/`. Если для прохождения сборки необходимы
> изменения в других файлах — добавьте в белый список ДО запуска.

## Validation Commands

```bash
dotnet format BookShop/BookShop.sln --verify-no-changes --verbosity diagnostic
dotnet build BookShop/BookShop.sln --no-restore -p:WarningsAsErrors=CS*
dotnet test BookShop/BookShop.sln --no-build --verbosity normal
```

## Notes

### Откуда подключается connection string

Потребитель `AddChatSessionPersistence(IConfiguration config)` читает
`config.GetConnectionString("ChatSession")` (стандартная .NET-конвенция;
`ConnectionStrings:ChatSession`). De-fault (если ключ отсутствует):
`"Host=localhost;Database=bookshop_bot"` — позволяет запустить smoke-тест
без реального значения.

### Структура smoke-теста

```csharp
// Arrange
var services = new ServiceCollection();
services.AddLogging(); // ILogger-based резолвы
services.AddServices(); // существующие регистрации
var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new[] { new KeyValuePair<string,string>("ConnectionStrings:ChatSession", "Host=localhost;Database=bookshop_bot") })
    .Build();
services.AddChatSessionPersistence(config);
var provider = services.BuildServiceProvider();

// Act
var repo = provider.GetRequiredService<IMemoryCacheSessionRepository>();
var session = await repo.GetOrCreateAsync(12345);

// Assert
session.Should().NotBeNull();
session.ChatId.Should().Be(12345);
repo.Should().BeOfType<CachedChatSessionRepository<TelegramChatSession>>();

var hosted = provider.GetServices<IHostedService>();
hosted.Should().Contain(x => x.GetType().Name == "SchemaInitializationService");
hosted.Should().Contain(x => x.GetType().Name == "ChatSessionCleanupService");
```

> Тест не требует реального PostgreSQL — проверяет только DI-граф и базовое
> поведение кэша. Дополнительные assertions: `PostgresChatSessionRepository`
  резолвится как зависимость, `SessionStorageOptions` содержит дефолты.

### Нужна ли ссылка на Npgsql?

`NpgsqlDataSource` находится в пакете `Npgsql` (8.0.6), который уже
транзитивно доступен через `ChatFSM/Fsm.csproj` (`ProjectReference`).
Если компилятор выдаст `CS0246` — добавить явную `PackageReference` в
`TelegramBot/TelegramBot.csproj` с версией `8.0.6`, подтвердив необходимость
в отчёте (файл разрешён для внесения только в рамках данного исключения).

### Ожидаемое поведение после интеграции (Anthropic Deployment)

- Если `ConnectionStrings:ChatSession` не задан → smoke-тест использует
  default; реальный запуск бота без настройки даст `NpgsqlException` при
  `StartAsync` → логируется, хост продолжает (инициализация схемы — best effort).
- Нет реального деплоя — smoke-тест заменяет сквозную проверку.
