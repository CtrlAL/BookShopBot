# Task: Cache-aside сессии (IChatSessionRepository + InMemoryCache, Redis-ready)

## Context & Objective

По дизайн-решению текущий FSM-бот держит состояние сессий в `IMemoryCache` (TelegramBot/MemoryCacheSessionRepository). Нужна архитектура cache-aside:

- **Store (источник правды)**: PostgreSQL (JSONB-таблица `chat_sessions`) + заглушка `InMemoryChatSessionRepository` для dev/тестов.
- **Cache (горячий путь)**: `IMemoryCache` перед store, никакого запроса в БД при каждом удержании сессии; оптимальный TTL; API готов к замене `IMemoryCache` -> Redis.
- Уже существующий контракт `Fsm.Session.IChatSessionRepository<TSession>` (GetOrCreateAsync / SaveAsync / RemoveAsync / ExistsAsync) не меняется — добавляются реализации и декоратор.

Важное ограничение окружения: `TelegramBot` больше не имеет `.csproj` и не входит в `BookShop.sln`, поэтому его **не трогаем**. Весь новый код размещается в `ChatFSM/Fsm.csproj` (проект входит в решение и валидируется пайплайном). Docker-демон на этой машине не запущен (`docker info` -> exit 1), поэтому функциональный интеграционный тест постгрэса переносится в отдельную задачу с Testcontainers; в этой задаче PostgreSQL-репозиторий покрывается unit-тестами сериализации и проверяется компиляцией.

## Interface Contract

Существующий контракт (НЕ меняется):

```csharp
public interface IChatSessionRepository<TSession> where TSession : class
{
    Task<TSession> GetOrCreateAsync(long chatId);
    Task SaveAsync(TSession session);
    Task RemoveAsync(long chatId);
    Task<bool> ExistsAsync(long chatId);
}
```

Новые типы в `ChatFSM/Session`:

```csharp
namespace Fsm.Session;

// Опции кэша. Дефолты = "оптимальный TTL" для длинного маркетплейс-воркфлоу:
// сессия жива при активности (sliding 20 мин), жёсткий предел 2 часа.
public sealed class SessionCacheOptions
{
    public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromMinutes(20);
    public TimeSpan AbsoluteExpiration { get; set; } = TimeSpan.FromHours(2);
}

// Thread-safe in-memory store (dev/test минима; Redis-заменитель на этом же контракте).
public sealed class InMemoryChatSessionRepository<TSession> : IChatSessionRepository<TSession> where TSession : class
{
    public InMemoryChatSessionRepository(); // создаёт TSession через Activator.CreateInstance на miss
}

// Декоратор cache-aside:
//   GetOrCreateAsync  — read-through: cache hit => вернуть; miss => store.GetOrCreateAsync + Set в cache;
//   SaveAsync         — write-through: store + (пере)запись cache;
//   RemoveAsync       — cache.Remove + store.RemoveAsync;
//   ExistsAsync       — cache-проверка, при miss — store.
// Пост-выселение из IMemoryCache никаких persist-операций не делает (SaveAsync уже write-through).
public sealed class CachedChatSessionRepository<TSession> : IChatSessionRepository<TSession> where TSession : class
{
    public CachedChatSessionRepository(
        IChatSessionRepository<TSession> store,
        Microsoft.Extensions.Caching.Memory.IMemoryCache cache,
        Microsoft.Extensions.Options.IOptions<SessionCacheOptions> options,
        Microsoft.Extensions.Logging.ILogger<CachedChatSessionRepository<TSession>> logger);
}

// PostgreSQL store: таблица chat_sessions(chat_id bigint PK, payload jsonb, updated_at timestamptz),
// upsert (INSERT ... ON CONFLICT (chat_id) DO UPDATE). Сериализация через System.Text.Json.
public sealed class PostgresChatSessionRepository<TSession> : IChatSessionRepository<TSession> where TSession : class
{
    public PostgresChatSessionRepository(NpgsqlDataSource dataSource);
}

// DI-хелпер: регистрирует IMemoryCache, store в Scoped и декоратор поверх него.
public static class ChatSessionRepositoryServiceCollectionExtensions
{
    public static IServiceCollection AddCachedChatSessionRepository<TSession, TStore>(this IServiceCollection services)
        where TSession : class
        where TStore : class, IChatSessionRepository<TSession>;
}
```

## Acceptance Criteria

- [ ] Критерий 1: `CachedChatSessionRepositoryTests` — повторный `GetOrCreateAsync` с тёплым кэшем **не** вызывает store (счётчик вызовов store по нулям); на miss store вызывается ровно 1 раз и результат попадает в кэш.
- [ ] Критерий 2: `CachedChatSessionRepositoryTests` — write-through: `SaveAsync` обновляет и store, и кэш; `RemoveAsync` чистит оба; `ExistsAsync` за счёт кэша не ходит в store при тёплом кэше.
- [ ] Критерий 3: `CachedChatSessionRepositoryTests` — выселение из `IMemoryCache` (via `GetOrCreate`/Compact + EvictionCallback) приводит к повторному обращению к store на следующем Get; TTL из `SessionCacheOptions` применяется (проверяется установкой 30-сек sliding/abs и принудительным `Remove`/выселением).
- [ ] Критерий 4: `InMemoryChatSessionRepositoryTests` — GetOrCreate создаёт ровно один экземпляр на chatId, Get возвращает тот же; Save/Get round-trip; Exists; Remove.
- [ ] Критерий 5: `SessionCacheOptionsTests` — дефолты 20 мин / 2 часа.
- [ ] Критерий 6: `PostgresChatSessionRepositoryTests` — round-trip сериализации `Session<ТState>` в JSONB-совместимый json (CoreProperties, с `ActionCts`/JsonIgnore) и обратно; SQL-тексты upsert/select/delete не содержат инъекционных форматирований (используется параметризация).
- [ ] Критерий 7: `dotnet build BookShop/BookShop.sln --no-restore -p:WarningsAsErrors=CS*` — 0 ошибок, **без новых CS-предупреждений** в добавленных файлах.
- [ ] Критерий 8: `dotnet format BookShop/BookShop.sln --verify-no-changes` — проходит.
- [ ] Критерий 9: нет регрессий (существующий `SmokeTests` + все прежние тесты зелёные).

## Affected Files

Строгий белый список файлов, доступных для редактирования:

| Файл | Действие |
|------|----------|
| `ChatFSM/Session/SessionCacheOptions.cs` | создание |
| `ChatFSM/Session/InMemoryChatSessionRepository.cs` | создание |
| `ChatFSM/Session/CachedChatSessionRepository.cs` | создание |
| `ChatFSM/Session/PostgresChatSessionRepository.cs` | создание |
| `ChatFSM/Session/ChatSessionRepositoryServiceCollectionExtensions.cs` | создание |
| `ChatFSM/Fsm.csproj` | изменение (разрешённые пакеты, см. Notes) |
| `tests/BookShopBot.Tests/ChatFSM/InMemoryChatSessionRepositoryTests.cs` | создание |
| `tests/BookShopBot.Tests/ChatFSM/CachedChatSessionRepositoryTests.cs` | создание |
| `tests/BookShopBot.Tests/ChatFSM/SessionCacheOptionsTests.cs` | создание |
| `tests/BookShopBot.Tests/ChatFSM/PostgresChatSessionRepositoryTests.cs` | создание |
| `tests/BookShopBot.Tests/BookShopBot.Tests.csproj` | изменение (ProjectReference ChatFSM + пакеты) |

> **Важно:** Запрещено изменять файлы, не указанные в этом списке (включая `TelegramBot/*`, `ChatFSM`-файлы вне списка, `Session.cs`, существующие интерфейсы).
> Если для выполнения задачи необходим доступ к другим файлам — добавьте их в список ДО начала работы.

## Validation Commands

```bash
dotnet format BookShop/BookShop.sln --verify-no-changes --verbosity diagnostic
dotnet build BookShop/BookShop.sln --no-restore -p:WarningsAsErrors=CS*
dotnet test BookShop/BookShop.sln --no-build --verbosity normal
```

## Notes

- TDD Invariant: перед реализацией каждого нового класса должны падать его тесты (см. критерии 1–6). Порядок: tests -> impl.
- Разрешённые новые пакеты: `Microsoft.Extensions.Caching.Memory` (8.x), `Microsoft.Extensions.Logging.Abstractions` (8.x), `Microsoft.Extensions.Options` (8.x, если не транзитивен), `Npgsql` (8.x). Запрещены любые другие новые зависимости (Redis-cache-клиенты добавляются в задаче про Redis).
- Docker-демон выключен -> реальный интеграционный тест PostgreSQL (Testcontainers) НЕ включать в эту задачу; `PostgresChatSessionRepository` покрывается unit-тестами сериализации + компиляцией. Интеграционный тест будет в отдельной задаче.
- Существующие известные предупреждения вне белого списка (CS8603/CS0168 в `AiBookRecognitionService.cs`, CS8618 в `Session.cs`, CS8625/CS8629 в `BookMapper.cs`, NU1902/Grpc 2.63) НЕ чинить.
- Валидация ходит по `BookShop/BookShop.sln`, поэтому логика обязана быть в проектах решения (ChatFSM — один из них); TelegramBot вне решения и остаётся нетронутым.