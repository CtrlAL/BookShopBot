# Task: Собрать TelegramBot — восстановить .csproj

## Context & Objective

`TelegramBot/` содержит все .cs-исходники, но `.csproj` был удалён в ходе
реорганизации; объектные кэши (`obj/`, `bin/`)аются в gitignore, артефакты
/proj.nuget пакетов устарели (концы путей). Задача: восстановить `TelegramBot.csproj`
по точным зависимостям из артефактов (`TelegramBot.csproj.nuget.dgspec.json` в
корне репо), добавить проект в `BookShop/BookShop.sln`, убедиться, что
`dotnet build` (с `-p:WarningsAsErrors=CS*`) проходит чисто. Тесты пока
отсутствуют — достаточно компиляции + отсутствия CS-предупреждений.

> Источник правды для зависимостей и версий: файл
> `TelegramBot.csproj.nuget.dgspec.json` (см. Notes).

## Interface Contract

Публичное API решения не меняется — добавляется только новый бинарник
(TelegramBot.dll) в конвейере.

## Acceptance Criteria

- [ ] Критерий 1: в `TelegramBot/TelegramBot.csproj` существует и
  инкапсулирует правильный `TargetFramework` (`net8.0`), все прямые
  `PackageReference` из Notes и `ProjectReference` на `ChatFSM/Fsm.csproj`.
- [ ] Критерий 2: `dotnet build BookShop/BookShop.sln --no-restore
  -p:WarningsAsErrors=CS*` завершается 0 ошибок, **0 CS-предупреждений**
  в `TelegramBot` (независимо от Nullable-режима — можно
  `<Nullable>disable</Nullable>`, если проще).
- [ ] Критерий 3: `TelegramBot` включён в `BookShop/BookShop.sln` и
  попадает в решётку `dotnet test` (build-only, тестов от него нет).
- [ ] Критерий 4: `dotnet format --verify-no-changes` проходит.
- [ ] Критерий 5: существующие тесты (52 в проекте BookShopBot.Tests)
  не падают — регрессий нет.

## Affected Files

| Файл | Действие |
|------|----------|
| `TelegramBot/TelegramBot.csproj` | создание (уже выполнено) |
| `BookShop/BookShop.sln` | изменение (включение TelegramBot, уже выполнено) |
| `TelegramBot/Extensions/DiExtensions.cs` | изменение (namespace fix) |
| `TelegramBot/Interfaces/ITelegramChatContext.cs` | изменение (namespace fix + член `Session`) |
| `TelegramBot/Interfaces/ITelegramStateFactory.cs` | изменение (namespace fix + декларация `Create`) |
| `TelegramBot/Interfaces/IMemoryCacheSessionRepository.cs` | изменение (namespace fix) |
| `TelegramBot/Interfaces/ChatConfiguration/ITelegramChatFsmConfigurator.cs` | изменение (namespace fix) |
| `TelegramBot/Interfaces/ChatConfiguration/ITelegramChatStateConfigurator.cs` | изменение (namespace fix) |
| `TelegramBot/Interfaces/IS3Service.cs` | создание (недостающий legacy-контракт) |
| `TelegramBot/Services/Implementations/UpdateHandler.cs` | изменение (namespace fix) |
| `TelegramBot/Services/Implementations/StateFactory.cs` | изменение (namespace fix) |
| `TelegramBot/Services/Implementations/TelegramChatSession.cs` | изменение (namespace fix + nullable) |
| `TelegramBot/Services/Implementations/ChatContext.cs` | изменение (nullable + `IInitializable`) |
| `TelegramBot/Services/States/IdleState.cs` | изменение (namespace fix) |
| `TelegramBot/Services/States/WaitFile.cs` | изменение (namespace fix) |
| `TelegramBot/Services/States/WaitFileName.cs` | изменение (namespace fix) |
| `TelegramBot/Services/ChatConfiguration/WaitFileConfigurator.cs` | изменение (namespace fix) |

> Запрещено изменять `.cs`-файлы TelegramBot, ChatFSM, сервисов, `.gitignore`,
> `scripts/tdd_loop.py`, `.tasks/`. Если для прохождения сборки необходимы
> изменения в `.cs` — добавьте файл в белый список ДО запуска.

## Validation Commands

```bash
dotnet format BookShop/BookShop.sln --verify-no-changes --verbosity diagnostic
dotnet build BookShop/BookShop.sln --no-restore -p:WarningsAsErrors=CS*
dotnet test BookShop/BookShop.sln --no-build --verbosity normal
```

## Notes

### Прямые зависимости TelegramBot (из дgspec-кэша)

Восстановлены из `TelegramBot.csproj.nuget.dgspec.json` (路径 в дgspec:
`F:\SpaceApp\BookShopBot\TelegramBot\TelegramBot.csproj`). Versions = минимальные
constraint-based (freeze actual used in `PackageReference`):

```xml
<TargetFramework>net8.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings>
<Nullable>disable</Nullable> <!-- без новых CS86xx при переносе legacy DTO -->

<!-- библиотека ChatFSM (AssemblyName = FSM) -->
<ProjectReference Include="..\ChatFSM\Fsm.csproj" />

<!-- Прямые NuGet-пакеты (точные версии из cache) -->
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="8.0.1" />
<PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.1" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.1" />
<PackageReference Include="Microsoft.Extensions.Options" Version="8.0.2" />
<PackageReference Include="Stateless" Version="5.20.0" />
<PackageReference Include="Telegram.Bot" Version="22.9.0" />
```

> В нижеследующих командах `dotnet restore` от restore убирает transient-пакеты
> автоматически; приведённые `PackageReference` — прямые; максимальная
> совместимость версий сохранена из исходного проекта.

### Процесс работы

1. `TelegramBot/TelegramBot.csproj` — создан (текущее состояние, проверить).
2. `BookShop/BookShop.sln` — TelegramBot добавлен (текущее состояние, проверить).
3. Выполнить namespace-миграцию `ChatFSM.*` → `Fsm.*` по карте выше.
4. Выполнить API-правки из п. «Исправление API-несовместимостей».
5. Создать `TelegramBot/Interfaces/IS3Service.cs`.
6. `dotnet restore BookShop/BookShop.sln`.
7. `dotnet build --no-restore -p:WarningsAsErrors=CS*` — исправлять ошибки CS.
8. `dotnet format --verify-no-changes` — исправить стиль; повторить.
9. `dotnet test` — 52 теста зелёные.

### Семантика агента

TelegramBot.csproj и sln-включение УЖЕ выполнены. Основная работа — адаптация
TelegramBot `.cs`-файлов к реорганизованной библиотеке ChatFSM (namespace `Fsm.*`).
Точка навигации — см. следующий раздел.

Не трогать `.cs`-файлы ChatFSM, сервисов.

### Миграция namespace-ов (ОБЯЗАТЕЛЬНО — по файлам)

ChatFSM был реорганизован: все namespace `ChatFSM.*` → `Fsm.*`. В исходниках
TelegramBot directives `using ChatFSM.X;` нужно заменить на `using Fsm.X;`.

**Точная карта замены (namespace source → target):**

| Файл (относительно `TelegramBot/`) | `using` до | `using` после |
|---|---|---|
| `Extensions/DiExtensions.cs` | `ChatFSM.Fsm` | `Fsm.Interfaces` |
| `Interfaces/ITelegramChatContext.cs` | `ChatFSM.Fsm` | `Fsm.Fsm` |
| `Interfaces/ITelegramStateFactory.cs` | `ChatFSM.States` | `Fsm.States` |
| `Interfaces/IMemoryCacheSessionRepository.cs` | `ChatFSM.Session` | `Fsm.Session` |
| `Interfaces/ChatConfiguration/ITelegramChatFsmConfigurator.cs` | `ChatFSM.Configuration` | `Fsm.Configuration` |
| `Interfaces/ChatConfiguration/ITelegramChatStateConfigurator.cs` | `ChatFSM.Configuration` | `Fsm.Configuration` |
| `Services/Implementations/UpdateHandler.cs` | `ChatFSM.Fsm` | `Fsm.Interfaces` |
| `Services/Implementations/StateFactory.cs` | `ChatFSM.States` | `Fsm.States` |
| `Services/Implementations/TelegramChatSession.cs` | `ChatFSM.Session` | `Fsm.Session` |
| `Services/States/IdleState.cs` | `ChatFSM.States` | `Fsm.States` |
| `Services/States/WaitFile.cs` | `ChatFSM.States` | `Fsm.States` |
| `Services/States/WaitFileName.cs` | `ChatFSM.States` | `Fsm.States` |
| `Services/ChatConfiguration/WaitFileConfigurator.cs` | `ChatFSM.Configuration` | `Fsm.Configuration` |

> `using` namespace alias (`global using ChatFSM = Fsm;`) НЕ работает для
> вложенных `using ChatFSM.X;`. Нужна ТОЛЬКО явная замена в каждом файле.

### Исправление API-несовместимостей (помимо namespace)

Помимо namespace, ChatFSM API изменился. Конкретные правки:

#### 1. `Interfaces/ITelegramStateFactory.cs` — удалить наследование

Старый ChatFSM определял `IStateFactory<TState, TContext, TInput>` (3 generic params).
В текущем ChatFSM: `IStateFactory<TContext, TInput>` (2 params, `Create()` без аргумента).

TelegramBot использует `Create(State state)` — метод, которого нет в новом интерфейсе.
**Fix:** `ITelegramStateFactory` больше НЕ наследует `IStateFactory`, а объявляет
свой контракт:

```csharp
using Fsm.States;
using BookShop.TelegramBot.Enums;
using Telegram.Bot.Types;

namespace BookShop.TelegramBot.Interfaces
{
    public interface ITelegramStateFactory
    {
        IState<ITelegramChatContext, Update> Create(State state);
    }
}
```

#### 2. `Interfaces/ITelegramChatContext.cs` — добавить член `Session`

Старый `IChatFsmContext<,>` мог иметь свойство `Session`. В текущем ChatFSM
`IChatFsmContext<,>` (namespace `Fsm.Fsm`) объявляет только `FireTriggerAsync` и
`HandleInputAsync` — член `Session` отсутствует.

TelegramBot-код в `WaitFile.cs` и `WaitFileName.cs` обращается к `chatContext.Session`.
Fix: добавить в `ITelegramChatContext`:

```csharp
using Fsm.Fsm;
using BookShop.TelegramBot.Enums;
using BookShop.TelegramBot.Services.Implementations;
using Telegram.Bot.Types;

namespace BookShop.TelegramBot.Interfaces;

public interface ITelegramChatContext : IChatFsmContext<Trigger, Update>
{
    TelegramChatSession Session { get; set; }
}
```

#### 3. `Services/Implementations/ChatContext.cs` — `IInitializable` + nullable

Текущий ChatFSM: `ChatContextFactory<TContext> where TContext : class, IInitializable`.
`IInitializable.InitializeAsync(params object[] attributes)`.
`ChatContext` имеет `InitializeAsync(long chatId)` — signature mismatch.

Fix:
- Удалить `TelegramChatSession?` → `TelegramChatSession` (remove `?`).
- Заменить `public async Task InitializeAsync(long chatId)` на:

```csharp
public Task InitializeAsync(params object[] attributes)
{
    long chatId = attributes.Length > 0 && attributes[0] is long id ? id : 0;
    return InitializeCoreAsync(chatId);
}

private async Task InitializeCoreAsync(long chatId)
{
    if (_isInitialized) return;
    Session = await _memoryCacheSessionRepository.GetOrCreateAsync(chatId);
    if (Session.CurrentState.Equals(default(State)))
    {
        Session.CurrentState = State.Idle;
        await _memoryCacheSessionRepository.SaveAsync(Session);
    }
    _isInitialized = true;
}
```

Убрать все `Session?.CurrentState` → `Session.CurrentState` (TelegramChatSession не nullable).

#### 4. `Services/Implementations/TelegramChatSession.cs` — nullable

Убрать `?` у `Extension`: `public string Extension { get; set; }`.

#### 5. Создать `TelegramBot/Interfaces/IS3Service.cs`

`WaitFileName.cs` использует `BookShop.S3Tool.Interfaces.IS3Service`, который
отсутствует в дереве. Создать интерфейс-заглушку ровно по signatures из кода:

```csharp
namespace BookShop.S3Tool.Interfaces;

public interface IS3Service
{
    bool CreateFile(Stream file, string folder, string fileName, string fileType);
    string GetPublicUrl(string folder, string fileName);
}
```

Файл добавить в список Affected Files как `TelegramBot/Interfaces/IS3Service.cs`.

### Telegram.Bot 22.9.0 API-факты (проверено)

- `TelegramBotClient(string token)` ctor — **NOT obsolete**, optional httpClient + ct.
- `GetInfoAndDownloadFile(ITelegramBotClient, string, Stream, CancellationToken)` — ct optional.
- `SendMessage(chatId, text, cancellationToken:)` — OK.
- `StartReceiving(updateHandler, errorHandler, receiverOptions, cancellationToken)` — OK.
- `GetMe(CancellationToken)` — OK.

### Валидация после каждого цикла

После всех правок выполнить (порядок строгий!):

```bash
dotnet restore BookShop/BookShop.sln
dotnet format BookShop/BookShop.sln --verify-no-changes --verbosity diagnostic
dotnet build BookShop/BookShop.sln --no-restore -p:WarningsAsErrors=CS*
dotnet test BookShop/BookShop.sln --no-build --verbosity normal
```

`dotnet test` — 52 теста (BookShopBot.Tests) должны остаться зелёными.
TelegramBot не содержит тестов — достаточно успешного build-only.
