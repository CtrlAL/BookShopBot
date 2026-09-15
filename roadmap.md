# Roadmap: BookShopBot — Status Update

> 2026-06-16 | Все P0 и P1 задачи выполнены

---

## ✅ Done — Critical (P0)

| Задача | Статус |
|--------|--------|
| Generic-параметры `IChatFsmContext` (3→2) | ✅ |
| `using ChatFSM.*` → `using Fsm.*` (12 файлов) | ✅ |
| Копипаста Title/Author/ISBN в RecognizeBook | ✅ |
| RecognizeBooks игнорировал AI результаты | ✅ |
| `AnalyzeImageAsync` возвращал null (NRE) | ✅ |
| `OnExit` вызывал `OnStateEnter` в 3 конфигураторах | ✅ |
| Scope leak в `TelegramBotHostedService` | ✅ |
| `BookGrpcService` — заглушка (всегда BookId=1) | ✅ |
| `ChatContext.InitializeAsync` не совпадал с интерфейсом | ✅ |

## ✅ Done — Important (P1)

| Задача | Статус |
|--------|--------|
| `IInitializeble` → `IInitializable` | ✅ |
| `Users` → `Books` в `DefaultDbContext` | ✅ |
| `GrpcHendWriteReader` → `GrpcHandWriteReader` | ✅ |
| `OllamaSharp` — удалён (unused) | ✅ |
| `WeatherForecast`/`ChatHostedService` — удалены | ✅ |
| `Shared/ChatContextFactory` — дубликат удалён | ✅ |
| `libman.json` — удалён | ✅ |
| `AllowedUsers` — удалён (unused dead code) | ✅ |
| `OnTransitionedAsync` — пустой handler удалён | ✅ |
| `DangerousAcceptAnyServerCertificateValidator` — под флаг | ✅ |
| SSL-проверка: `GrpcClient:IgnoreSslErrors` | ✅ |
| `MaxReceiveMessageSize` — 100MB → 10MB | ✅ |
| `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.0→8.0.2 | ✅ |
| `ILogger` добавлен во все catch в AiBookRecognitionService | ✅ |
| `OCR` — удалён (пустой WPF проект) | ✅ |
| Nullable warnings (CS8618, CS8625, CS8629) — 0 | ✅ |

## ⏳ Known Warnings (pre-existing, low priority)

| Warning | Причина | Fix |
|---------|---------|-----|
| NU1902 | OpenTelemetry.Exporter.OpenTelemetryProtocol 1.9.0 уязвимость | Обновить до 1.11+ |
| Grpc.Net.ClientFactory | Несовместимость с Microsoft.Extensions.Http.Resilience 9.9.0 | Обновить Grpc до 2.64+ |

## ❌ Not Done (TelegramBot — no project file)

TelegramBot/ — набор .cs файлов без .csproj. Все фиксы (using, generics, scope, configurators) **применены к исходникам**, но собрать проект нельзя — нет файла проекта.

**Требуется:** создать .csproj или добавить в существующий solution.

## 📊 Stats

| Метрика | До | После |
|---------|----|-------|
| Compilation errors | 28+ | **0** |
| Warnings (ChatFSM) | 6 | **0** |
| Warnings (BookShop) | 12 CS + 2 NuGet | **0 CS + 2 NuGet** |
| Dead projects | 2 (OCR, HandWriteReader-stub) | **0** |
| Dead code files | 5+ | **0** |
