# Дизайн: консолидация проекта в одной папке BookShop + починка работы с .worktrees

Дата: 2026-09-20. Ветка: main (F:/SpaceApp/BookShopBot).

## Цели

1. Локализовать весь код и проект в ОДНОЙ папке `BookShop/` на верхнем уровне Git-корня.
   Перенести туда все ссылаемые библиотеки/папки (ChatFSM, TelegramBot, tests и т.п.)
   и починить все ссылки (.sln, .csproj, Dockerfile, docker-compose, .props).
2. Починить работу с ворктри: у нас уже есть папка `.worktrees/` в корне Git, но
   почему-то ветки пишутся в новую папку `BookShopBot` и внутри неё делаются ветки.
   Ветки должны писаться строго в корневую `F:/SpaceApp/BookShopBot/.worktrees/<branch>`.

## Подтверждённая целевая структура (одобрено пользователем)

```
F:/SpaceApp/BookShopBot/                  <- КОРЕНЬ GIT (только инфраструктура)
+-- .git/
+-- .worktrees/       <- ЕДИНСТВЕННАЯ база: git worktree add .worktrees/<branch>
+-- .opencode/  .tasks/  Design/  docs/  scripts/
+-- Dockerfile  docker-compose.yml  Directory.Build.props  dotnet-tools.json
+-- .gitignore  .dockerignore  roadmap.md  code-review.md
+-- BookShop/                              <- ВЕСЬ КОД И ПРОЕКТ
    +-- BookShop.sln
    +-- src/          <- все проекты/библиотеки (BookAiClient, BookCatalogService,
    |                    BookRecognitionService, BookShop.AppHost, BookShop.ServiceDefaults,
    |                    BotApi, ChatApi, ChatFSM, DeepSeekClient, Grpc*, S3Tool, TelegramBot...)
    +-- tests/        <- все тестовые проекты (BookShop.BookShopBot.Tests и др.)
```

## Как реализуем

### Шаг 0. Починка ворктри (независимо и первым)
- Найти писателя ворктри (скрипт/конфиг в scripts/, .opencode/, .tasks/), который создаёт
  ветки не в корневую `.worktrees/`, а в новую папку `BookShopBot/` (на верхнем уровне уже
  есть лишняя пустая `BookShopBot/`). Исправить базу на корневую `.worktrees/`.
- Корневая `F:/SpaceApp/BookShopBot/.worktrees/` — убедиться, что пустая, и использовать её.

### Шаг 1. Перенос кода в BookShop/src + BookShop/tests (через git mv)
- `git mv TelegramBot BookShop/src/TelegramBot`
- `git mv ChatFSM BookShop/src/ChatFSM`
- `git mv tests BookShop/tests` (или слить с существующим тестовым проектом)
- Остальные проекты, что уже под BookShop/, разложить: живые -> `src/`, тесты -> `tests/`.

### Шаг 2. Починка ссылок
- `BookShop.sln` -> пути к `.csproj` внутри `src/`, `tests/`.
- `.csproj` ProjectReference -> новые относительные пути внутри `src/`.
- `Directory.Build.props` (если ссылается на папки) -> коррекция путей.
- `Dockerfile` COPY -> BookShop/src/...; `.dockerignore`; `docker-compose.yml` build context.

### Шаг 3. Проверка
- `dotnet build` из корня Git собирает с нуля.
- `docker build` / `docker compose build` проходит.
- `git worktree list` показывает ветки только в `.worktrees/`.

## Риски
- Сломать sln-пути -> билд падает. Поэтому все переносы через `git mv` (сохраняет историю),
  сборка после каждого этапа, один коммит на этап.
- Потерять живой код верхних ChatFSM/TelegramBot/tests -> проверяем содержимое перед переносом.
