# План: консолидация всего кода BookShopBot в BookShop/src + BookShop/tests

> **Для агентов-исполнителей:** суперпаверс:executing-plans / subagent-driven-development.
> Чекбоксы `- [ ]` = шаги.

**Цель (согласована с пользователем):** Весь живой .NET-код и проекты, на которые
ссылается `BookShop.sln`, переезжают в одну папку `BookShop/` со структурой
`src/` (живые проекты) + `tests/` (тесты). Инфраструктура воркстри живёт ТОЛЬКО
в корневой `F:/SpaceApp/BookShopBot/.worktrees/`, а не в паразитной `BookShopBot/` папке.

**Архитектура (2-3 предложения):** Через `git mv` (история сохраняется) переносим
все csproj-проекты в `BookShop/src/` и тестовые в `BookShop/tests/`, затем чиним
все внешние ссылки: пути в `BookShop.sln`, `ProjectReference` во всех `.csproj`
(включая те, что смотрят наружу через `..\..\`), `Dockerfile` COPY и
`docker-compose.yml` build-контекст. Ветка → PR → merge в main → деплой.

**Техстек:** git (git mv / worktree), .NET (sln/csproj), Docker (Dockerfile/docker-compose).

## Ограничения
- Перенос только `git mv` (без историю: `git mv` сохраняет её), коммит на каждом этапе.
- Сборка обязательна после каждого переноса: `dotnet build BookShop/BookShop.sln`.
- Все воркстри: `git worktree add .worktrees/<branch>` — база строго `.worktrees/`.
- Никаких символических линков.

## Согласовано в брейншторме
- Ответ «BookShop/src + BookShop/tests»: целевой вид структуры.
- Ответ «Инфраструктура в корне Git (Рекомендую)»: docker-compose/Dockerfile/Directory.Build.props
  остаются на корневом уровне, код — внутри BookShop/src.
- «Живой код — переносим»: ChatFSM, TelegramBot, tests — живые, переносим.

## Текущие факты (сняты пробами)
- sln: `BookShop/BookShop.sln`
- sln-проекты: BookShop.AppHost, BookShop.ServiceDefaults, Chat(папка), FSM->..\ChatFSM\Fsm.csproj,
  BookAi->BookAiClient\BookAi.csproj, Ai(папка), CoreApi(папка),
  BookCatalogService, BookRecognitionService, ChatApi, BookShopBot.Tests->..\tests\...,
  TelegramBot->..\TelegramBot\TelegramBot.csproj, S3Tool, BotApi
- Внешние ProjectReference наружу (`..\..\`): BotApi->..\..\TelegramBot,
  TelegramBot->..\BookShop\S3Tool,..\ChatFSM\Fsm.csproj,
  tests\BookShopBot.Tests->..\..\BookShop\S3Tool,..\..\ChatFSM,..\..\TelegramBot

---

### Этап 1. Удалить паразитную папку BookShopBot (база воркстри)
- [ ] **Шаг 1.1:** Проверить `git worktree list` — сейчас только main; BookShopBot/ не в списке.
- [ ] **Шаг 1.2:** Удалить `BookShopBot/` из корня (`git rm -r BookShopBot`если tracked, иначе просто физудаление), т.к. туда ошибочно писались ветки.
- [ ] **Шаг 1.3:** Зафиксировать дизайн: все воркстри через `git worktree add F:/SpaceApp/BookShopBot/.worktrees/<branch>`.

### Этап 2. Перенос живого кода в BookShop/src
- [ ] **Шаг 2.1:** `git mv ChatFSM BookShop/src/ChatFSM`
- [ ] **Шаг 2.2:** `git mv TelegramBot BookShop/src/TelegramBot`
- [ ] **Шаг 2.3:** перенести внутренние проекты BookShop/* → BookShop/src/* (git mv по одному)
- [ ] **Шаг 2.4:** `git mv tests BookShop/tests` (BookShopBot.Tests → BookShop/tests/BookShopBot.Tests)

### Этап 3. Починка ссылок
- [ ] **Шаг 3.1:** Переписать `BookShop.sln`: все `*.csproj` на `src\*.csproj`, тестовые на `tests\*.csproj`
- [ ] **Шаг 3.2:** В каждом `.csproj` выправить `ProjectReference` (убрать выход `..\BookShop\`, `..\..\`)
- [ ] **Шаг 3.3:** Обновить `Dockerfile` (COPY src/...), `docker-compose.yml` (build context), при необходимости `.dockerignore`
- [ ] **Шаг 3.4:** Проверить, что в `Directory.Build.props` нет ссылок на конкретные пути

### Этап 4. Ветка, проверка, PR
- [ ] **Шаг 4.1:** Создать в ..: `git worktree add .worktrees/refactor-bookshop-consolidate`
- [ ] **Шаг 4.2:** В ней выполнить перенос (или сделать прямо, потом PR main<-ветка)
- [ ] **Шаг 4.3:** `dotnet build BookShop/BookShop.sln` — сборка зелёная
- [ ] **Шаг 4.4:** Коммиты + `git push`, открыть PR в main (через gh)

### Этап 5. Merge + деплой
- [ ] **Шаг 5.1:** После одобрения — merge PR в main
- [ ] **Шаг 5.2:** На main: `dotnet build` + попытка `docker login`/`docker compose build` и деплой
