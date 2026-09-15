# Task: Test Infrastructure — зелёный baseline для TDD

## Context & Objective

Репозиторий `BookShopBot` (решение `BookShop/BookShop.sln`, .NET 8) не имеет ни одного
тестового проекта: `dotnet test` выполняет 0 тестов. Правило TDD Invariant
(`.opencode/rules.md`) требует падающего теста до кода реализации, но без тест-инфраструктуры
это невозможно. Задача — создать тестовый проект `tests/BookShopBot.Tests` (xUnit, net8.0),
добавить его в решение и написать структурный smoke-тест, который падает при нарушении
состава решения (ключевые проекты обязаны присутствовать). Цель — зелёный `dotnet test`
с реально выполняющимися тестами и готовый каркас для последующих ADD-задач.

## Interface Contract

`N/A` — инфраструктурная задача, публичное API приложений не меняется. Изменяется только
состав решения (`BookShop/BookShop.sln`) и добавляются тестовые файлы.

Ожидаемые открытые типы тестового проекта:

```csharp
// tests/BookShopBot.Tests/SmokeTests.cs
public sealed class SolutionStructureTests
{
    // Читает BookShop/BookShop.sln (путь задаётся относительно корня репозитория)
    // и утверждает, что решение содержит Project-записи для csproj:
    //   - GrpcBookService.csproj
    //   - GrpcBookRecognitionService.csproj
    //   - TelegramBot.csproj
    //   - ChatFSM\Fsm.csproj
    //   - BookShopBot.Tests.csproj
    [Fact]
    public void Solution_contains_core_projects(); // при их отсутствии — падает
}
```

## Acceptance Criteria

- [ ] 1. Тест-проект `tests/BookShopBot.Tests/BookShopBot.Tests.csproj` создан
      (xUnit, net8.0, `IsPackable=false`).
- [ ] 2. Структурный smoke-тест `SolutionStructureTests.Solution_contains_core_projects`
      написан ПЕРВЫМ (до добавления тестового проекта в решение) и «падает» —
      TDD Invariant соблюдён: проверка на наличие проекта в решении не проходит,
      пока проекта нет в `.sln`.
- [ ] 3. Тестовый проект добавлен в `BookShop/BookShop.sln` (и только он; существующие
      прочие записи решения не удаляются и не переименовываются).
- [ ] 4. `dotnet test BookShop/BookShop.sln --no-build` обнаруживает и выполняет
      >= 1 тест; smoke-тест проходит после добавления проекта в решение.
- [ ] 5. `dotnet build BookShop/BookShop.sln --no-restore -p:WarningsAsErrors=CS*`
      завершается без ошибок (0 CS-warnings; известные NU1902 / Grpc.Net.ClientFactory
      предупреждения не трогать и не чинить — они задокументированы в `roadmap.md`).
- [ ] 6. `dotnet format BookShop/BookShop.sln --verify-no-changes` проходит
      (новые файлы отформатированы).
- [ ] 7. Нет регрессий: файлы с кодом приложений (`BookShop/`, `ChatFSM/`, `TelegramBot/`)
      НЕ изменяются этой задачей.

## Affected Files

Строгий белый список файлов, доступных для редактирования:

| Файл | Действие |
|------|----------|
| `tests/BookShopBot.Tests/BookShopBot.Tests.csproj` | создание |
| `tests/BookShopBot.Tests/SmokeTests.cs` | создание |
| `tests/BookShopBot.Tests/GlobalUsings.cs` | создание (опционально) |
| `BookShop/BookShop.sln` | изменение (добавить `..\tests\BookShopBot.Tests\BookShopBot.Tests.csproj`) |

> **Важно:** Разрешено добавлять NuGet-пакеты **только** из списка ниже (см. Notes).
> Файлы реализации приложений и `.editorconfig` изменять запрещено.

## Validation Commands

```bash
# Линтер / форматирование (порядок фиксирован в .opencode/rules.md)
dotnet format BookShop/BookShop.sln --verify-no-changes --verbosity diagnostic

# Статический анализ + компиляция
dotnet build BookShop/BookShop.sln --no-restore -p:WarningsAsErrors=CS*

# Тесты
dotnet test BookShop/BookShop.sln --no-build --verbosity normal
```

## Notes

- **Разрешённые новые NuGet-пакеты** (только они; versions — последние стабильные,
  совместимые с net8.0):
  - `Microsoft.NET.Test.Sdk`
  - `xunit`
  - `xunit.runner.visualstudio`
- Тест-проект должен размещаться в `tests/BookShopBot.Tests/` (папки `tests/` пока нет —
  создать). Относительный путь из `BookShop/BookShop.sln` — `..\tests\BookShopBot.Tests\BookShopBot.Tests.csproj`
  (аналогично уже существующим относительным путям `..\..\ChatFSM` / `..\..\TelegramBot`,
  проверить актуальную схему в `.sln`).
- Smoke-тест ищет решение относительно корня репозитория (переходить от `AppContext.BaseDirectory`
  вверх на 2 уровня, либо использовать `Directory.GetCurrentDirectory()` — выбрать надёжный
  способ и зафиксировать маршрут до корня `BookShop/BookShop.sln`).
- Известные предупреждения NU1902 (OpenTelemetry) и Grpc.Net.ClientFactory 2.63
  считаются приемлемыми (см. `roadmap.md`) — НЕ «чинить».
- Структура файлов должна соответствовать стилю репозитория (вкладки, file-scoped
  namespace, `GlobalUsings` при необходимости).