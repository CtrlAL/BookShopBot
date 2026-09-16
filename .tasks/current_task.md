# Task: Дефолтная схема сессий в PostgreSQL (chatfsm.chat_session + expires_at + фоновая очистка)

## Context & Objective

Библиотека ChatFSM уже даёт `PostgresChatSessionRepository<TSession>` (Task 2), но:
(1) таблица жёстко зашита и никем не создаётся; (2) у строк нет срока жизни, и они
накапливаются; (3) `CachedChatSessionRepository` теряет отмену `ActionCts` при
выселении из кэша (было в старом TelegramBot-коде). По утверждённому дизайну
`Design/2026-09-16-chatfsm-postgres-schema-design.md` нужно:

- идемпотентная инициализация схемы (без EF, без версий/истории, в духе
  системных таблиц: схема `chatfsm`, таблица `chat_session`);
- `expires_at`: срок жизни БД-строки `30 дней + джиттер 0..7 дней` (НЕ равен TTL
  кэша), джиттер — против «thundering herd» очистки;
- фоновая очистка протухших строк `WHERE expires_at < now()` через
  `BackgroundService` (интервал по умолчанию 1 ч);
- конфигурируемость схемы/имени/сроков через `SessionStorageOptions`;
- у `CachedChatSessionRepository` при выселении сессии из `IMemoryCache` —
  `Cancel()` + `Dispose()` её `ActionCts` (reflection, как для `ChatId`).

`TelegramBot` не трогаем (вне решения). `.tasks/template.md` — шаблон, файлы
вне белого списка запрещено изменять.

## Interface Contract

Существующие контракты (`IChatSessionRepository<TSession>`,
`CachedChatSessionRepository`, `SessionCacheOptions`, `Session<TState>`,
`IState` и т.д.) НЕ меняются. Новые/изменяемые типы в `ChatFSM/Session`:

```csharp
namespace Fsm.Session;

// Опции БД-слоя сессий (новое).
public sealed class SessionStorageOptions
{
    public string Schema { get; set; } = "chatfsm";
    public string Table  { get; set; } = "chat_session";
    public TimeSpan BaseLifetime   { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan LifetimeJitter { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
}

// Инициализатор схемы (новое): CREATE SCHEMA IF NOT EXISTS + CREATE TABLE
// IF NOT EXISTS с именем из опций; idempotent; fail-fast на ошибке.
public static class ChatSessionSchema
{
    public static Task EnsureCreatedAsync(
        NpgsqlDataSource dataSource,
        IOptions<SessionStorageOptions> options,
        CancellationToken cancellationToken = default);
}

// Изменение: новый ctor; upsert записывает expires_at = now + BaseLifetime +
// jitter (jitter = Random.Shared.NextDouble() * LifetimeJitter); GetOrCreateAsync
// и ExistsAsync считают протухшую строку (expires_at <= now) отсутствующей.
public sealed class PostgresChatSessionRepository<TSession> : IChatSessionRepository<TSession>
    where TSession : class
{
    public PostgresChatSessionRepository(
        NpgsqlDataSource dataSource,
        IOptions<SessionStorageOptions> options);
}

// Фоновая очистка (новое): PeriodTimer(CleanupInterval), каждый тик DELETE ...
// WHERE expires_at < now() на таблице из опций; исключения логируются, следующий
// тик повторяет.
public sealed class ChatSessionCleanupService : BackgroundService
{
    public ChatSessionCleanupService(
        NpgsqlDataSource dataSource,
        IOptions<SessionStorageOptions> options,
        ILogger<ChatSessionCleanupService> logger);
}

// Изменение: добавление регистрации options + hosted-сервиса удобным хелпером.
public static class ChatSessionRepositoryServiceCollectionExtensions
{
    // существующий AddCachedChatSessionRepository<TSession,TStore> остаётся
    public static IServiceCollection AddChatSessionCleanup(
        this IServiceCollection services,
        string connectionString,
        Action<SessionStorageOptions>? configure = null); // регистрирует NpgsqlDataSource,
                                                          // options и ChatSessionCleanupService
}

// Изменение CachedChatSessionRepository: в MemoryCacheEntryOptions при SetCache
// регистрировать post-eviction callback: если value — TSession с свойством
// "ActionCts" типа CancellationTokenSource и оно не null -> Cancel() + Dispose().
// ВАЖНО: в Microsoft.Extensions.Caching.Memory 8.x вызывать
// entryOptions.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration {
//     EvictionCallback = (k, v, reason, state) => {...} });
// (экземплярного метода RegisterPostEvictionCallback в 8.x нет — он добавлен в 9.x).
```

## Acceptance Criteria

- [ ] Критерий 1 (TDD первым): до реализации каждого из новых типов в `tests/`
  существуют падающие тесты (см. пункты ниже).
- [ ] Критерий 2: `SessionStorageOptions` дефолты: schema=`chatfsm`,
  table=`chat_session`, BaseLifetime=30d, LifetimeJitter=7d, CleanupInterval=1h.
- [ ] Критерий 3: `ChatSessionSchema.EnsureCreatedAsync` формирует корректный
  SQL с `CREATE SCHEMA IF NOT EXISTS {schema}` и `CREATE TABLE IF NOT EXISTS
  {schema}.{table}` (проверка на построенном тексте / через парсинг команды);
  при переопределении схемы/имени в опциях SQL использует их.
- [ ] Критерий 4: `PostgresChatSessionRepository.SaveAsync` upsert задаёт
  `expires_at` в границах `now + BaseLifetime ≤ expires_at ≤ now + BaseLifetime +
  LifetimeJitter`; при `LifetimeJitter = TimeSpan.Zero` — ровно `now + Base`.
- [ ] Критерий 5: `GetOrCreateAsync`/`ExistsAsync` не возвращают протухшую
  строку (проверяется через unit-сеам со «протухшим» значением expires_at).
- [ ] Критерий 6: `ChatSessionCleanupService` за тик выполняет `DELETE ... WHERE
  expires_at < now()` на таблице из опций; ошибка выполнения не роняет сервис
  (следующий тик продолжает).
- [ ] Критерий 7: `CachedChatSessionRepository` при выселении сессии из
  `IMemoryCache` (`cache.Remove`/`Compact`) вызывает `Cancel()`+`Dispose()`
  на свойстве `ActionCts` (проверка: `token.IsCancellationRequested == true`).
- [ ] Критерий 8: `dotnet build BookShop/BookShop.sln --no-restore
  -p:WarningsAsErrors=CS*` — 0 ошибок, без новых CS-предупреждений в добавленных
  файлах.
- [ ] Критерий 9: `dotnet format BookShop/BookShop.sln --verify-no-changes`
  проходит.
- [ ] Критерий 10: нет регрессий (существующие тесты, включая SmokeTests и
  тесты Task 2, зелёные).

## Affected Files

Строгий белый список файлов, доступных для редактирования:

| Файл | Действие |
|------|----------|
| `ChatFSM/Session/SessionStorageOptions.cs` | создание |
| `ChatFSM/Session/ChatSessionSchema.cs` | создание |
| `ChatFSM/Session/ChatSessionCleanupService.cs` | создание |
| `ChatFSM/Session/PostgresChatSessionRepository.cs` | изменение (новый ctor, expires_at, протухшие) |
| `ChatFSM/Session/ChatSessionRepositoryServiceCollectionExtensions.cs` | изменение (AddChatSessionCleanup) |
| `ChatFSM/Session/CachedChatSessionRepository.cs` | изменение (post-eviction: отмена ActionCts) |
| `ChatFSM/Fsm.csproj` | изменение (пакет Microsoft.Extensions.Hosting.Abstractions 8.x) |
| `tests/BookShopBot.Tests/ChatFSM/SessionStorageOptionsTests.cs` | создание |
| `tests/BookShopBot.Tests/ChatFSM/ChatSessionSchemaTests.cs` | создание |
| `tests/BookShopBot.Tests/ChatFSM/PostgresChatSessionRepositoryTests.cs` | изменение (новый ctor + expires/expired) |
| `tests/BookShopBot.Tests/ChatFSM/CachedChatSessionRepositoryTests.cs` | изменение (тест на отмену ActionCts при eviction) |
| `tests/BookShopBot.Tests/ChatFSM/ChatSessionCleanupServiceTests.cs` | создание |

> **Важно:** Запрещено изменять файлы, не указанные в списке (в т.ч.
> `Session<TState>.cs`, `IChatSessionRepository.cs`, `SessionCacheOptions.cs`,
> `Design/*`, `scripts/*`). Если для выполнения задачи нужен доступ к другим
> файлам — добавьте их в список ДО начала работы.

## Validation Commands

```bash
dotnet format BookShop/BookShop.sln --verify-no-changes --verbosity diagnostic
dotnet build BookShop/BookShop.sln --no-restore -p:WarningsAsErrors=CS*
dotnet test BookShop/BookShop.sln --no-build --verbosity normal
```

## Notes

- TDD Invariant: тесты пишутся раньше реализации (критерии 2–7 = тесты).
- Разрешённые новые зависимости: только `Microsoft.Extensions.Hosting.Abstractions`
  (8.x) в `ChatFSM/Fsm.csproj`. Никаких EF Core, миграций, истории версий.
- Схема/имя таблицы интерполируются в SQL из опций; допустимы только символы
  `[A-Za-z0-9_]` (валидация в коде и/или тесте) — защита от injection в DDL.
- Реальные PostgreSQL-интеграционные тесты (Testcontainers) НЕ включать:
  docker-демон выключен.
- НЕ чинить известные предсуществующие warnings (CS8618 в Session.cs,
  CS8603/CS0168 в AiBookRecognitionService.cs, CS8625/CS8629 в BookMapper.cs,
  NU1902/Grpc 2.63).
- Для post-eviction в CachedChatSessionRepository использовать
  `entryOptions.PostEvictionCallbacks` (API 8.x), см. Interface Contract.
- Фоновая очистка не обязана удалять мгновенно: «протухшую, но ещё не
  удалённую» строку GetOrCreate/Exists считают отсутствующей (Критерий 5).
- После цикла изменения нужно закоммитить (pre-commit хук прогонит тот же
  пайплайн); обход — только осознанный `git commit -n`.