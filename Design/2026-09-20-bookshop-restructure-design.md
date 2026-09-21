# Дизайн: реструктуризация «всё в одной папке BookShop» + чинка ворктри

Дата: 2026-09-20
Ветка: main (F:/SpaceApp/BookShopBot)
Статус: дизайн, ждёт согласования перед реализацией

## Проблема

1. Код и папки, на которые ссылается проект, размазаны по корню репозитория:
   верхний уровень содержит и `BookShop/`, и отдельно `ChatFSM/`, `TelegramBot/`, `tests/`, а также
   паразитную `BookShopBot/` (внутри только пустой `.worktrees`). Часть ссылок в
   `BookShop.sln`, `.csproj`, `Dockerfile`, `docker-compose.yml` сделана как относительные
   пути наружу через `..`  (сам `BookShop.sln` лежит внутри `BookShop/`, но ссылается на
   проекты, физически лежащие РЯДОМ с `BookShop/`, т.е. на верхнем уровне) — это и есть
   «новые папки на которые мы ссылаемся», которые мы хотим локализовать.

2. Для worktree у нас уже есть папка `.worktrees/` в корне репо, но ветки почему-то пишутся
   в новую папку `BookShopBot/`, и внутри неё уже делаются ветки. Каноничная база ворктри
   должна быть одна: `.worktrees/` в корне репо.

## Целевая структура

```
F:/SpaceApp/BookShopBot/                (корень Git, main)
├─ .git/
├─ .worktrees/                          ← ЕДИНСТВЕННАЯ база: git worktree add .worktrees/<branch>
├─ .opencode/  .tasks/  Design/  docs/  scripts/
├─ Dockerfile  docker-compose.yml  Directory.Build.props  dotnet-tools.json
├─ .dockerignore  .gitignore  code-review.md  roadmap.md
└─ BookShop/                            ← ВЕСЬ живой код и проект (одна папка)
   ├─ BookShop.sln
   ├─ src/
   │   ├─ BookAiClient/  BookCatalogService/  BookRecognitionService/
   │   ├─ BookShop.AppHost/  BookShop.ServiceDefaults/
   │   ├─ BotApi/  ChatApi/  ChatFSM/  DeepSeekClient/
   │   ├─ GrpcBookRecognitionService/  GrpcBookService/  GrpcHandWriteReader/
   │   ├─ S3Tool/  TelegramBot/
   └─ tests/
       └─ (все тестовые проекты из верхнего tests/ и любые связи из src)
```

## План работ

### Шаг 1. Починить ворктри (безопасный, независимый)
- Удалить паразитную `BookShopBot/` (внутри пустой `.worktrees` — не имеет работы).
- Зафиксировать, что все `git worktree add` используют базу `F:/SpaceApp/BookShopBot/.worktrees/<branch>`
  (это уже настроено в используемых скиллах worktree; лишних баз не плодим).

### Шаг 2. Собрать весь код в BookShop/
- Через `git mv` перенести верхние живые папки внутрь `BookShop/`:
  - `ChatFSM/ → BookShop/src/ChatFSM`
  - `TelegramBot/ → BookShop/src/TelegramBot`
  - верхние проекты, на которые ссылается sln → `BookShop/src/*`
  - `tests/ → BookShop/tests`
- Перенести остальные проекты, физически лежащие в `BookShop/` на верхнем уровне решения,
  в подпапки `src/` (и тестовые — в `tests/`), чтобы в `BookShop/` остались только
  `BookShop.sln`, `src/`, `tests/`.

### Шаг 3. Починить все ссылки (ядро задачи)
- `BookShop.sln` → пути к проектам становятся `src\*.csproj`, `tests\*.csproj` (без `..\..\`).
- `.csproj` (`ProjectReference`, `..\` outbound) → `src\...`.
- `Dockerfile` → `COPY src/...`, корректный контекст.
- `docker-compose.yml` → пути build/volumes без выхода за `BookShop/`.
- `Directory.Build.props` / `dotnet-tools.json` — проверить, что не ссылаются наружу.
- `.dockerignore` / `.gitignore` — добавить `BookShop/**/bin`, `**.obj`, `..`-запреты на новый layout.

### Шаг 4. Проверка
- `dotnet build BookShop/BookShop.sln` собирается начисто.
- `docker build` (композ со старым контекстом) проходит COPY.
- `git worktree list` показывает ветки строго в `.worktrees/`.

## Развилки, которые решаем по фактам sln при реализации
- Одинаковые имена `ChatFSM/` в `BookShop/` и на верхнем уровне: источник истины — `BookShop.sln`
  (кто реально указан в проектах решения и в `ProjectReference`). Дубль идентифицируем по ссылкам
  и удаляем лишний через `git rm`, живой переносим.

## Известные риски
- Неверный относительный путь в `.sln`/`Dockerfile` ломает сборку/деплой → шаг 3 строго
  по карте ссылок, собранной из `BookShop.sln` и `docker-compose.yml` перед переносом.
- Случайное удаление живого кода → все операции через `git mv`/`git rm`, проверка `git status`.
