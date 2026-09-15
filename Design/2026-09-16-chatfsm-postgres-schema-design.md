# Дизайн: дефолтная схема сессий в PostgreSQL (ChatFSM)

- **Дата:** 2026-09-16
- **Статус:** approved (уточнения приняты)
- **Ветка плана:** отдельная ADD-задача поверх `main` после мержа Task 2

## Контекст и цель

Библиотека ChatFSM предоставляет дефолтное хранилище сессий в PostgreSQL
(`ChatFSM/Session/PostgresChatSessionRepository.cs`, введено в Task 2). Сегодня
таблица жёстко зашита (`chat_sessions` в `public`) и никем не создаётся — схема
не инициализируется ни при старте, ни миграцией.

Цель: библиотека «выдаёт» дефолтную схему в духе системных таблиц фреймворков
(аналог `__EFMigrationsHistory`), безопасно изолированную от реальных сущностей
БД, с разумным сроком жизни строк и фоновой очисткой.

## Принятые решения (по итогам брифа)

1. **Без EF Core и без версионирования/истории.** Инициализация —
   идемпотентная (`CREATE SCHEMA IF NOT EXISTS` / `CREATE TABLE IF NOT EXISTS`),
   вызываемая при старте приложения. Ро́ли миграций в библиотеке нет.
2. **Изоляция имени.** Отдельная схема `chatfsm` + таблица `chat_session`.
   Имя и схема конфигурируются через опции; дефолт — как выше.
3. **JSONB payload.** Схема фиксированная и не зависит от `TSession`:
   сессия сериализуется целиком в колонку `payload` (`System.Text.Json`,
   camelCase). Enum состояния — внутри JSON. Поэтому версии/откаты схемы
   не нужны.
4. **Срок жизни строки в БД ≠ TTL кэша.** TTL кэша (20 мин sliding / 2 ч
   absolute) — только горячий путь и не влияет на БД. Срок жизни в БД —
   **30 дней + случайный джиттер 0..7 дней** (декорреляция очистки против
   «thundering herd»), конфигурируемо.
5. **Фоновая очистка.** `BackgroundService` в библиотеке, по умолчанию раз в
   час: `DELETE ... WHERE expires_at < now()`.
6. **Явная инвалидация** (`RemoveAsync`, завершение/отмена флоу) удаляет
   строку из кэша и БД сразу.
7. **Исправление пробела Task 2.** `CachedChatSessionRepository` возвращает
   обработку выселения `ActionCts` (отмена/диспоз как в старом
   `MemoryCacheSessionRepository`), чтобы не текли таймеры фоновых операций FSM.

## Схема

```sql
CREATE SCHEMA IF NOT EXISTS chatfsm;

CREATE TABLE IF NOT EXISTS chatfsm.chat_session (
  chat_id    bigint      PRIMARY KEY,
  payload    jsonb       NOT NULL,
  expires_at timestamptz NOT NULL,
  updated_at timestamptz NOT NULL
);

-- фоновая очистка (интервал из опций, по умолчанию 1 ч):
DELETE FROM chatfsm.chat_session WHERE expires_at < now();
```

## Компоненты

Все новые файлы — в `ChatFSM/Session/`.

### `SessionStorageOptions`

```csharp
public sealed class SessionStorageOptions
{
    public string Schema { get; set; } = "chatfsm";
    public string Table  { get; set; } = "chat_session";
    public TimeSpan BaseLifetime { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan LifetimeJitter { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
}
```

### Инициализатор схемы

```csharp
public static class ChatSessionSchema
{
    // CREATE SCHEMA IF NOT EXISTS + CREATE TABLE IF NOT EXISTS (схема/имя из опций)
    public static Task EnsureCreatedAsync(NpgsqlDataSource dataSource,
        IOptions<SessionStorageOptions> options, ...);
}
```

Idempotent; пробрасывает исключения при неудаче (fail-fast на старте).

### `PostgresChatSessionRepository<TSession>` (изменение)

- Логика NewSession (GetOrCreateAsync): если строка найдена и
  `expires_at > now()` — вернуть; если протухла — считать отсутствующей
  (лениво удаляется cleanup'ом; допускается и немедленное удаление).
- `SaveAsync` (write-through): upsert с `expires_at = now() + BaseLifetime +
  jitter`, где jitter = `Random.Shared.NextDouble() * LifetimeJitter`.
- `ExistsAsync`: учитывает `expires_at`.
- Схема/имя таблицы берутся из опций (не hard-coded).

### `ChatSessionCleanupService` — фоновая очистка

```csharp
public sealed class ChatSessionCleanupService : BackgroundService
{
    public ChatSessionCleanupService(NpgsqlDataSource dataSource,
        IOptions<SessionStorageOptions> options, ILogger<...> logger);
}
```

- Цикл: `PeriodicTimer(CleanupInterval)`; каждый тик — `DELETE ... WHERE
  expires_at < now()` на таблице из опций, с `try/catch` (ошибка логируется,
  следующий тик повторяет).
- DI: `services.AddChatSessionCleanup(config)` (регистрирует и `NpgsqlDataSource`
  при необходимости). Пакет `Microsoft.Extensions.Hosting.Abstractions`.

### `CachedChatSessionRepository` (исправление пробела)

- В `SetCache` добавить `RegisterPostEvictionCallback`: при `EvictionReason !=
  None` найти `ActionCts` у выселенной сессии, `Cancel()` + `Dispose()`.
  Записи в БД не делает (со store синхронизирует write-through SaveAsync).

## Потоки данных

1. **Горячий путь:** `FSM → CachedChatSessionRepository` (`IMemoryCache`,
   TTL 20m/2h). При hit — без БД.
2. **Miss:** `→ PostgresChatSessionRepository.GetOrCreateAsync` → при пусто/протухло
   создаётся свежая сессия (фиксация в БД — только в SaveAsync).
3. **SaveAsync:** store (upsert, `expires_at` = now + 30d + jitter) → кэш.
4. **RemoveAsync:** кэш + delete из БД (мгновенная инвалидация).
5. **Background:** раз в час удаляет протухшие строки.

## Error handling

- Инициализация схемы: исключения наружу, сервис не стартует (fail-fast).
- Cleanup: ошибка логируется, не роняет приложение; следующий тик повторяет.
- Рандом-джиттер — через `Random.Shared` (без расходов на свой RNG).

## Тестирование (юнит, без Docker)

- `expires_at` в границах: `base ≤ delta ≤ base+jitter`; при `LifetimeJitter=0`
  точно `base`.
- SQL инициализатора/cleanup содержит корректные schema.table (и не ломается
  при override схемы/имени).
- `GetOrCreateAsync`/`ExistsAsync` считают протухшую строку отсутствующей.
- Плюс: `CachedChatSessionRepository` при eviction отменяет `ActionCts`
  (проверка `token.IsCancellationRequested` после принудительного испускания
  выселения).

Интеграционный PostgreSQL-тест (Testcontainers) — отдельной задачей, т.к. на
этой машине docker-демон выключен.

## Затронутые файлы (белый список будущей ADD-задачи)

| Файл | Действие |
|------|----------|
| `ChatFSM/Session/SessionStorageOptions.cs` | создание |
| `ChatFSM/Session/ChatSessionSchema.cs` | создание |
| `ChatFSM/Session/PostgresChatSessionRepository.cs` | изменение (опции, expires_at, протухшие) |
| `ChatFSM/Session/ChatSessionCleanupService.cs` | создание |
| `ChatFSM/Session/ChatSessionRepositoryServiceCollectionExtensions.cs` | изменение (EnsureCreated + AddChatSessionCleanup) |
| `ChatFSM/Session/CachedChatSessionRepository.cs` | изменение (post-eviction ActionCts) |
| `ChatFSM/Session/SessionCacheOptions.cs` | без изменений (кэш-слой отдельно) |
| `ChatFSM/Fsm.csproj` | изменение (пакет Hosting.Abstractions) |
| `tests/BookShopBot.Tests/ChatFSM/*` | добавление/изменение |

## Открытые вопросы

Закрытых нет. Значения по умолчанию (30d / 7d / 1h) — стартовые, меняются через
опции без изменения кода.