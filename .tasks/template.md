# Task: [Название задачи]

## Context & Objective

Краткое описание контекста и цели задачи. Что нужно сделать и зачем.

**Пример:**
> Реализовать валидацию ISBN в `BookGrpcService` с выбросом `ArgumentException` при невалидном формате.

## Interface Contract

Сигнатуры функций/типов, которые должны быть реализованы или изменены.

```csharp
// Пример:
public static class IsbnValidator
{
    public static bool IsValid(string isbn);
    public static string Normalize(string isbn);
}
```

Если задача не затрагивает публичные интерфейсы — указать `N/A`.

## Acceptance Criteria

- [ ] Критерий 1: тест проходит
- [ ] Критерий 2: `dotnet build` без ошибок
- [ ] Критерий 3: `dotnet format --verify-no-changes` проходит
- [ ] Критерий 4: нет regressions (существующие тесты не падают)

## Affected Files

Строгий белый список файлов, доступных для редактирования:

| Файл | Действие |
|------|----------|
| `path/to/file.cs` | создание / изменение |
| `tests/path/to/file.cs` | создание |

> **Важно:** Запрещено изменять файлы, не указанные в этом списке.
> Если для выполнения задачи необходим доступ к другим файлам — добавьте их в список ДО начала работы.

## Validation Commands

```bash
# Линтер / форматирование
dotnet format BookShop/BookShop.sln --verify-no-changes --verbosity diagnostic

# Статический анализ + компиляция
dotnet build BookShop/BookShop.sln --no-restore -p:WarningsAsErrors=CS*

# Тесты
dotnet test BookShop/BookShop.sln --no-build --verbosity normal
```

## Notes

Дополнительные заметки, ссылки на документацию, известные ограничения.
