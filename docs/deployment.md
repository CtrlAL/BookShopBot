# Деплой BookShopBot: Docker Compose, Aspire, Kubernetes

## 1. Стратегии деплоя одним экраном

| Стратегия | Статус | Что разворачивает | Когда использовать |
|---|---|---|---|
| **Docker Compose** | Сейчас (основной путь) | `postgres:16-alpine`, `minio/minio`, 4 сервиса: `bookcatalogservice`, `bookrecognitionservice`, `botapi`, `chatapi` | Прод/dev на одной VPS-машине. Один `docker compose up -d` поднимает весь стек. |
| **Aspire AppHost** | Локальная оркестрация (dev) | Postgres, MinIO, BotApi + существующие проекты (`bookcatalogservice`, `bookrecognitionservice`, `chatapi`) | Разработка: F5 в VS или `dotnet run`. Окрестрация контейнеров инфраструктуры вручную, dashboard OpenTelemetry. |
| **Kubernetes** | НЕ сейчас | Полноценная оркестрация | Когда понадобится автоскейлинг, мульти-реплики, отказоустойчивость (см. раздел 6). |

**Почему Compose, а не K8s сейчас:** один сервис-хост, 4 контейнера приложения, статическая нагрузка. K8s добавит сложности (Ingress, service mesh, persistent volumes, RBAC) без выгоды на текущем масштабе.

---

## 2. Быстрый старт (Docker Compose)

```bash
# 1. Скопировать шаблон переменных
cp .env.example .env            # PowerShell: Copy-Item .env.example .env

# 2. Вписать креды в .env: BOT_TOKEN, DEEPSEEK_API_KEY (остальное можно оставить дефолтным)

# 3. Поднять весь стек
docker compose up -d

# 4. Проверить состояние
docker compose ps

# 5. Логи бота
docker compose logs -f botapi
```

Что поднимается (порты наружу):

| Сервис | Порт (host → контейнер) | Доступ |
|---|---|---|
| `postgres` | 5432 → 5432 | Postgres |
| `minio` | 9000 → 9000 (S3 API), 9001 → 9001 (консоль) | http://localhost:9001 — MinIO Web-консоль |
| `botapi` | 8080 → 8080 | http://localhost:8080 — «BookShop Bot is running» |
| `chatapi` | 8081 → 8080 | — |
| `bookcatalogservice`, `bookrecognitionservice` | не пробрасываются наружу | gRPC по внутренней сети compose |

Внутри compose используется внутренняя сеть: MinIO доступен по `http://minio:9000`, catalog — по `http://bookcatalogservice:8080`.

---

## 3. Креды (переменные окружения)

| Переменная | Где применяется | Значение по умолчанию | Что будет при пустом |
|---|---|---|---|
| `BOT_TOKEN` | `botapi` (`BotConfig:Token`, env `BotConfig__Token`) | — | BotApi упадёт при старте с `InvalidOperationException` («Telegram Bot Token не задан в конфигурации…», `TelegramBot/Extensions/DiExtensions.cs`) — бот не может стартовать без токена |
| `MINIO_ROOT_USER` | `minio` контейнер + `botapi` (`S3__AccessKey`) | `minioadmin` | Подставится `minioadmin` (compose `${VAR:-minioadmin}`) |
| `MINIO_ROOT_PASSWORD` | `minio` контейнер + `botapi` (`S3__SecretKey`) | `minioadmin` | Подставится `minioadmin` |
| `POSTGRES_USER` | `postgres` + строки подключения `bookcatalogservice` и `botapi` | `bookshop` | Подставится `bookshop` |
| `POSTGRES_PASSWORD` | `postgres` + строки подключения | `bookshop` | Подставится `bookshop` |
| `POSTGRES_DB` | `postgres` + строки подключения | `bookshop` | Подставится `bookshop` |
| `DEEPSEEK_API_KEY` | `bookrecognitionservice` (`DeepSeekConfig:ApiKey`, env `DeepSeekConfig__ApiKey`) | — (пусто → mock-режим) | Сервис поднимется и НЕ упадёт: compose не переопределяет `UseMockAiService`, дефолт `true` из `appsettings.json` → распознавание вернёт mock-результат, вызовов DeepSeek не будет |

Жёстко заданы в `docker-compose.yml` (не переменные): `S3__Endpoint=http://minio:9000`, `S3__Bucket=bookshop`, `BookCatalogService__Url=http://bookcatalogservice:8080` для botapi и recognition.

> Как включить реальный DeepSeek: (a) в `.env` задать `DEEPSEEK_API_KEY`; (b) в `docker-compose.yml` в сервис `bookrecognitionservice` добавить env `DeepSeekConfig__UseMockAiService: "false"` (имя ключа подтверждено по коду: `BookRecognitionService/Program.cs` биндит секцию `DeepSeekConfig`, `BookAiClient/DependencyInjection.cs` читает `UseMockAiService` из неё); (c) пересоздать контейнер: `docker compose up -d --force-recreate bookrecognitionservice`.
>
> Оговорка: если явно принудить `UseMockAiService=false`, но ключ пуст — реальный DeepSeek-вызов упадёт (клиент создастся с пустым ApiKey). Это отдельный кейс, не дефолт: по умолчанию включён mock.

---

## 4. Локальный деплой через Aspire

```bash
dotnet run --project BookShop/BookShop.AppHost
```

Из VS: открыть `BookShop/BookShop.AppHost` и нажать F5.

AppHost поднимает:

| Компонент | Что именно |
|---|---|
| Postgres | `AddPostgres("postgres")`, persistent lifetime |
| MinIO | контейнер `minio/minio:RELEASE.2024-08-03T04-33-23Z`, endpoint `s3` (9000), `console` (9001), root-креды `minioadmin`/`minioadmin`, volume `minio-data` |
| `bookcatalogservice` | с ссылкой на Postgres |
| `bookrecognitionservice` | — |
| `botapi` | с ссылкой на Postgres и endpoint `s3` MinIO, env `S3__Endpoint=http://localhost:9000`, `S3__AccessKey/SecretKey=minioadmin` |
| `chatapi` | — |

- Dashboard Aspire (OpenTelemetry) — в логах AppHost на старте (адрес вида http://localhost:1xxxx).
- MinIO консоль: http://localhost:9001 (логин/пароль `minioadmin`, заданы в `AppHost.cs`).
- Токен бота для Aspire-запуска BotApi берётся из `BookShop/BotApi/appsettings.Local.json` (см. раздел 5) — AppHost его **не** задаёт.

---

## 5. Локальные секреты (`appsettings.Local.json`)

Секреты никогда не попадают в git:

```gitignore
# .gitignore
**/appsettings.Local.json
**/appsettings.*.Local.json
*.env
```

BotApi при старте явно загружает `appsettings.Local.json` (optional) поверх `appsettings.json` (`Program.cs`). Пример заполнения (`BookShop/BotApi/appsettings.Local.json`):

```json
{
  "BotConfig": {
    "Token": "123456:AAH..."
  },
  "S3": {
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin"
  },
  "ConnectionStrings": {
    "ChatSession": "Host=localhost;Port=5432;Database=bookshop;Username=bookshop;Password=bookshop"
  }
}
```

Что читает BotApi из конфигурации (факт по `Program.cs`/`appsettings.json`):

| Ключ конфигурации | Значение по умолчанию | где используется |
|---|---|---|
| `BotConfig:Token` | — (должен быть в Local) | `AddTelegramBotClient` |
| `S3:Endpoint` | `http://localhost:9000` (appsettings.json) | `AddS3Client` |
| `S3:Bucket` | `bookshop` | бакет для файлов |
| `S3:PresignedLifetime` | `365.00:00:00` (1 год) | TTL presigned URL |
| `S3:AccessKey` / `S3:SecretKey` | пустые (Local) | доступ к MinIO |
| `ConnectionStrings:ChatSession` | пустая, fallback `Host=localhost;Database=bookshop_bot` | session persistence (Postgres) |
| `BookCatalogService:Url` | fallback `http://book-catalog-service:5000` | gRPC-клиент к catalog |
| `GrpcClient:IgnoreSslErrors` | — (bool, опционально) | отключает проверку SSL gRPC |

`.env` для compose и `appsettings.Local.json` для BotApi — независимые механизмы: первый для `docker compose up`, второй для запусков через Aspire/`dotnet run`.

---

## 6. Почему не Kubernetes (и когда перейдём)

**Сейчас K8s не нужен:** один бота-сервис, 4 контейнера, статическая нагрузка. Compose даёт декларативный деплой без инфраструктурной сложности K8s (Ingress, Service, RBAC, PV/PVC, операторы).

**Когда понадобится** (ориентир из design-doc — целевой масштаб **100M DAU**): автоскейлинг по нагрузке, мульти-реплики бота/сервисов, отказоустойчивость, rolling-deploy без даунтайма.

**Как перейти:**
1. Aspire AppHost остаётся источником оркестрации — генерируем манифест: `dotnet run --project BookShop/BookShop.AppHost -- --publisher manifest --output-path manifest.json` (или через Aspirate: `aspirate generate` → выходной манифест/образ, путь к переезду на K8s).
2. **Aspirate** конвертирует Aspire-манифест в Helm-чарты/K8s-манифесты.
3. Применяем: `helm install` / `kubectl apply`.
4. Секреты из `.env`/`appsettings.Local.json` переезжают в K8s Secrets; Postgres/MinIO — в StatefulSets/PVC.

---

## 7. Сборка Docker-образа вручную

Единый multi-stage Dockerfile в корне репозитория. Образ выбирается через `ARG SERVICE` (имя csproj). **По умолчанию в Dockerfile задан `ChatApi`** — для других сервисов `--build-arg` обязателен.

```bash
# BotApi
docker build --build-arg SERVICE=BotApi -t bookshop/botapi .

# BookCatalogService
docker build --build-arg SERVICE=BookCatalogService -t bookshop/bookcatalogservice .

# BookRecognitionService
docker build --build-arg SERVICE=BookRecognitionService -t bookshop/bookrecognitionservice .

# ChatApi
docker build --build-arg SERVICE=ChatApi -t bookshop/chatapi .
```

Запуск готового образа (runtime слушает `:8080`, `ASPNETCORE_URLS=http://+:8080`):

```bash
docker run -p 8080:8080 bookshop/botapi
```

Особенности Dockerfile:
- **build** стадия: `sdk:8.0`, `COPY Directory.Build.props`, `ChatFSM/`, `TelegramBot/`, `tests/`, `BookShop/` целиком → `dotnet restore BookShop/BookShop.sln` → `dotnet publish BookShop/${SERVICE}/${SERVICE}.csproj`. Весь контекст обязателен: решение ссылается на `TelegramBot/`/`ChatFSM/`/`tests/` и корневой `Directory.Build.props`.
- **final** стадия: `aspnet:8.0`, `ENV ASPNETCORE_URLS=http://+:8080`, `EXPOSE 8080`, `ENTRYPOINT ["sh", "-c", "exec dotnet ${SERVICE}.dll"]`.
- `.dockerignore` исключает `**/bin/`, `**/obj/`, `.worktrees/`, `.env*`.