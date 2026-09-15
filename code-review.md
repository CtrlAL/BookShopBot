# Code Review: BookShopBot

> Дата: 2026-06-16 | Всего файлов: ~80 | Язык: C# .NET 8

---

## CRITICAL — не компилируется / упадёт в рантайме

### 1. `ITelegramChatContext` — несовместимое количество generic-параметров
**Файл:** `TelegramBot/Interfaces/ITelegramChatContext.cs:8`
```csharp
public interface ITelegramChatContext : IChatFsmContext<TelegramChatSession, Trigger, Update>
```
`IChatFsmContext` объявлен как `<in TTrigger, in TInput>` — 2 параметра. Передаётся 3 (`TelegramChatSession, Trigger, Update`). **Код не соберётся.**

### 2. Все `using ChatFSM.*` указывают на несуществующий namespace
**Файлы:** `TelegramBot/*.cs` — ~15 файлов
```csharp
using ChatFSM.Fsm;       // реально: Fsm.Fsm
using ChatFSM.Configuration; // реально: Fsm.Configuration
using ChatFSM.Session;   // реально: Fsm.Session
using ChatFSM.States;    // реально: Fsm.States
```
ChatFSM — название папки, а namespace везде `Fsm.*`. `using ChatFSM.Fsm` — CS0234. **Весь TelegramBot не компилируется.**

### 3. Копипаста маппинга полей в `RecognizeBook`
**Файл:** `BookShop/GrpcBookRecognitionService/Features/BookRecognition/Services/BookRecognitionGrpcService.cs:34-37`
```csharp
Title = result.Payload.Title,
Isbn = result.Payload.Title,   // должно быть result.Payload.ISBN
Author = result.Payload.Title, // должно быть result.Payload.Author
```
ISBN и Author получают значение Title. В базу уйдёт мусор.

### 4. `RecognizeBooks()` отправляет пустые запросы в каталог
**Файл:** `BookShop/GrpcBookRecognitionService/Features/BookRecognition/Services/BookRecognitionGrpcService.cs:57-65`
```csharp
var createBookRequest = new CreateBookRequest { };  // пусто!
var createBookResponse = await _bookCatalogServiceClient.CreateBookAsync(createBookRequest);
```
Результаты `_deepSeekClient.AnalyzeBookImageAsync` собраны в `responses`, но проигнорированы. В цикле создаются пустые `CreateBookRequest`. Весь batch-метод — мёртвый код.

### 5. `AnalyzeImageAsync` возвращает null как `string` (non-nullable)
**Файл:** `BookShop/DeepSeekClient/Implementations/AiBookRecognitionService.cs:72-77`
```csharp
catch (ClientResultException ex)
{
    return null; // string? -> string — NRE в вызывающем коде
}
```
`null` придёт в `ExtractBookInfoFromDescriptionAsync`, где будет использован в интерполяции строки. `NullReferenceException` гарантирован.

---

## HIGH — логические ошибки

### 6. OnExit везде вызывает OnStateEnter
**Файлы:** `TelegramBot/Services/ChatConfiguration/IdleStateConfigurator.cs:23`, `WaitFileConfigurator.cs:23`, `WaitFileNameConfigurator.cs:23`
```csharp
stateConfig.OnExitAsync(() => stateHandler.OnStateEnterAsync(context));
//                                 ^^^^^^^^^^^^^^ должно быть OnStateExitAsync
```
Копипаста во всех трёх конфигураторах. OnExit никогда не вызывается корректно.

### 7. Scope не диспозится
**Файл:** `TelegramBot/Services/HostedServices/TelegramBotHostedService.cs:55`
```csharp
var scope = _serviceScopeFactory.CreateScope();
// scope не обёрнут в using — утечка ресурсов при каждом апдейте
```

### 8. `BookGrpcService` — заглушка, БД не используется
**Файл:** `BookShop/GrpcBookService/Features/BooksManagment/Services/BookGrpcService.cs:16-20`
```csharp
return Task.FromResult(new CreateBookResponse { BookId = 1, CreatedBook = book });
```
PostgreSQL + EF Core настроены, но сервис всегда возвращает `BookId = 1`. `GetBook`, `SearchBooks`, `UpdateBook`, `DeleteBook` не реализованы.

### 9. `ChatApi` — мёртвый шаблон
**Файлы:** `BookShop/ChatApi/Controllers/WeatherForecastController.cs`, `BookShop/ChatApi/WeatherForecast.cs`
Стандартный WeatherForecast из шаблона WebAPI. Не имеет отношения к Chat API. `ChatHostedService` бросает `NotImplementedException` и не зарегистрирован в DI.

---

## MEDIUM — качество кода

### 10. `ITelegramChatFsmConfigurator` internal-зависимость
`ITelegramChatFsmConfigurator` — `internal`, но `ChatFsmConfigurator` — `public`. Конфигураторы используют `internal interface` — странная видимость.

### 11. Транзит `DangerousAcceptAnyServerCertificateValidator`
**Файл:** `BookShop/GrpcBookRecognitionService/Program.cs:22-23`
Production-код отключает проверку SSL-сертификатов. Если это нужно для dev — должно быть под флагом конфигурации.

### 12. `DefaultDbContext.Users` — неправильное имя
**Файл:** `BookShop/GrpcBookService/Shared/DefaultDbContext.cs:8`
```csharp
public DbSet<Book> Users { get; set; } // должно быть Books
```
Таблица `Users` для книг — дезинформация для следующего разработчика.

### 13. Дублирование `ChatContextFactory`
Два класса с одинаковой логикой:
- `Fsm/Abstractions/ChatContextFactory.cs` (generic `<TContext, TSession>`)
- `Fsm/Fsm/ChatContextFactory.cs` (generic `<TContext>`, живёт в namespace `Fsm.Interfaces`)

Второй — лишний.

### 14. Хардкод `AllowedUsers`
**Файл:** `TelegramBot/Constants/AllowedUsers.cs`
ID `123456789`, `987654321` в коде — должен быть в конфигурации.

### 15. `GrpcHendWriteReader` — опечатка в названии
`Hend` → `Hand`. Проект — пустой шаблон Greeter, к HandWrite не имеет отношения.

### 16. `OCR/Class1.cs` — пустой проект
WPF проект без единой строчки логики, без `App.xaml` и `MainWindow`.

### 17. Пустой `.github/workflows/`
Папка есть, yml-файлов нет. CI/CD нет.

### 18. Нет тестов
Ни одного тестового проекта — ни unit, ни integration.

### 19. Нет README
Нет ни одного `.md`-файла с документацией.

### 20. Сломанная кодировка в proto
**Файл:** `BookShop/GrpcBookRecognitionService/Features/BookRecognition/Protos/book.recognition.proto:63`
```
// ���������
```
Комментарий потерял кодировку.

### 21. Несоответствие версий пакетов
`Microsoft.Extensions.DependencyInjection.Abstractions` — версия **10.0.0** (net8 не поддерживает DI 10.0), при этом остальные пакеты `Microsoft.Extensions.*` на версии 8.0.x.

### 22. Unused `OllamaSharp`
Добавлен в два `.csproj`, но нигде не используется — код работает через `OpenAI` SDK с эндпоинтом Ollama.

### 23. Отсутствие `ILogger` в `AiBookRecognitionService`
Ручная обработка ошибок в `AnalyzeBookImageAsync` не логируется.

### 24. Нелогичная структура namespace `Fsm.Fsm`
```csharp
namespace Fsm.Fsm; // "Fsm.Fsm" — дублирование
```

### 25. Пустой OnTransitionedAsync
**Файл:** `TelegramBot/Services/ChatConfiguration/ChatFsmConfigurator.cs:19-22`
```csharp
stateMachine.OnTransitionedAsync(async t => { await Task.CompletedTask; });
```
Мёртвый код.

---

## Итоговая оценка

| Метрика | Значение |
|---------|----------|
| Компилируемость | **НЕТ** — 2+ критических ошибки (generic mismatch, namespace) |
| Тесты | 0% |
| Документация | 0% |
| Production-ready | НЕТ |
| Мёртвый код | 4 проекта/файла (OCR, HandWriteReader, ChatApi WeatherForecast, Empty workflow) |
| Утечки ресурсов | 1 (scope), потенциально CancellationTokenSource в сессиях |
| Security | Dangerous SSL, пустые `CreateBookRequest`, хардкод user IDs |
