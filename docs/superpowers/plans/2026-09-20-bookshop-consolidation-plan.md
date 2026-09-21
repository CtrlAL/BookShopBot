# Консолидация кода в BookShop/ + фикс базы worktree → план

> **Для агентов-исполнителей:** требуемый суб-скилл superpowers:subagent-driven-development
> или superpowers:executing-plans. Шаги — чекбоксы `- [ ]`.

**Цель:** Перенести весь живой код (.NET-решение и проекты) ОДНУ папку `BookShop/`
(структура `BookShop/src/*` + `BookShop/tests/*`), оставив инфраструктуру в корне Git,
и починить все ссылки (sln/csproj/Dockerfile/compose). Параллельно — починить базу
ворктри: ветки должны писаться в корневую `F:/SpaceApp/BookShopBot/.worktrees/<branch>`,
а не в новую папку `BookShopBot/`.

**Архитектура:** Целевой корень Git = только инфраструктура:
`.git/  .opencode/  .tasks/  .worktrees/  Design/  docs/  scripts/` +
`Dockerfile  docker-compose.yml  Directory.Build.props  dotnet-tools.json  .gitignore  .dockerignore  roadmap.md`.
Все проекты — внутри `BookShop/src/`, тесты — `BookShop/tests/`.

**Техстек:** git (worktree/mv/rm), .NET (sln/csproj ProjectReference), docker (Dockerfile COPY/docker-compose).

## Глобальные ограничения
- Только `git mv`/`git rm` (история сохраняется). Никаких физических копий через robocopy.
- Сборка после каждой фазы: `dotnet build F:/SpaceApp/BookShopBot/BookShop/BookShop.sln`.
- Ветки ворктри пишутся ТОЛЬКО в `F:/SpaceApp/BookShopBot/.worktrees/`.
- Никаких `..\..\`-выходов за пределы `BookShop/` в новых путях (кроме обязательного
  решения для общего Directory.Build.props на корне — оставляем корневой props инфраструктурой).

---

## Фаза 0 — Починить писателя ворктри (база)
- [ ] **Шаг 0.1:** Убедиться, что writer worktree использует базу `F:/SpaceApp/BookShopBot/.worktrees/`.
  Удалить лишнюю верхнеуровневую `BookShopBot/` (внутри только пустой `.worktrees`) через `git rm -r`.

## Фаза 1 — Перенос проектов в src/ (git mv, по одному коммиту на проект или фазой)
- [ ] **Шаг 1.1:** `BookAiClient BookCatalogService BookRecognitionService BookShop.AppHost
  BookShop.ServiceDefaults` → `BookShop/src/<name>` (новые верхние в BookShop/src)
- [ ] **Шаг 1.2:** `BotApi ChatApi DeepSeekClient GrpcBookRecognitionService GrpcBookService
  GrpcHandWriteReader S3Tool` → `BookShop/src/<name>`
- [ ] **Шаг 1.3:** верхние `ChatFSM/`, `TelegramBot/`, `tests/` → `BookShop/src/ChatFSM`,
  `BookShop/src/TelegramBot`, `BookShop/tests/` (BookShopBot.Tests → BookShop/tests)

## Фаза 2 — Починка ссылок (ядро задачи)
- [ ] **Шаг 2.1:** `BookShop/BookShop.sln` — пути проектов на `src\*.csproj`, `tests\*.csproj`.
- [ ] **Шаг 2.2:** `.csproj` ProjectReference внутри `src/` — относительные пути без выхода за BookShop.
- [ ] **Шаг 2.3:** `Dockerfile`/`.dockerignore`/`docker-compose.yml` — COPY/build-контекст → `BookShop/src`.

## Фаза 3 — Проверка
- [ ] **Шаг 3.1:** `dotnet build BookShop/BookShop.sln` — собирается начисто.
- [ ] **Шаг 3.2:** `git worktree list` — ветки в `.worktrees/`, никакой `BookShopBot/`.

## Фаза 4 — Ветка + PR + merge + деплой
- [ ] **Шаг 4.1:** ветка `refactor/consolidate-bookshop` → PR в main.
- [ ] **Шаг 4.2:** merge PR в main.
- [ ] **Шаг 4.3:** деплой (docker compose) — по принятому процессу репо.
