# SnakeFrogCalendarBot

## 1. Что это за проект

Telegram-бот для ведения календаря событий и дней рождения с хранением в Postgres, long polling, FSM-сценариями, вложениями к событиям и автоматическими дайджестами через Quartz.

`AGENTS.md` описывает текущее состояние репозитория. Не нужно записывать сюда желаемую архитектуру, которой ещё нет в коде.

## 2. Текущий стек

- `.NET 10`
- `Telegram.Bot 22.8.1`
- `EF Core 9.0.4` + `Npgsql`
- `Quartz 3.13.0`
- `NodaTime 3.3.0`
- `Serilog`
- `NUnit`
- `Docker Compose` с `postgres:16`

## 3. Структура solution

```text
/src
  /SnakeFrogCalendarBot.Domain
  /SnakeFrogCalendarBot.Application
  /SnakeFrogCalendarBot.Infrastructure
  /SnakeFrogCalendarBot.Worker

/tests
  /SnakeFrogCalendarBot.Tests
```

Имена проектов, namespaces и пути начинаются с `SnakeFrogCalendarBot`, не с `CalendarBot`.

## 4. Архитектура по слоям

### Domain

Хранит сущности и инварианты:

- `Birthday`
- `Event`
- `Attachment`
- `NotificationRun`
- `LatestDigestPost`
- `ConversationState`
- enum'ы `EventKind`, `DigestType`
- `DomainException`

Ключевые факты:

- `Event` поддерживает 2 режима:
  - `OneOff` через `OccursAtUtc`
  - `Yearly` через `Month` + `Day` + optional `TimeOfDay`
- `Attachment` версионируется через `Version` и флаг `IsCurrent`
- `NotificationRun` используется для идемпотентности отправки дайджестов
- `LatestDigestPost` хранит последний опубликованный Telegram `message_id` для `Daily`, `Weekly`, `Monthly` и ссылку на соответствующий `notification_run`
- отдельного `MonthDay` value object сейчас в проекте нет

### Application

Здесь находятся:

- use cases
- интерфейсы к инфраструктуре
- DTO
- форматтеры сообщений

Основные use cases:

- `Birthdays`: create/list/update/delete
- `Events`: create/list/update/delete/get/attach/replace file
- `Notifications`: build daily/weekly/monthly digest, send digest, refresh latest digest posts

Форматирование живет в `Formatting`, а не в Worker:

- `BirthdayListFormatter`
- `EventListFormatter`
- `DigestFormatter`

Текущие важные классы notifications:

- `BuildDailyDigest`
- `BuildWeeklyDigest`
- `BuildMonthlyDigest`
- `DigestItemsProvider`
- `SendDigest`
- `RefreshLatestDigestPosts`

### Infrastructure

Содержит:

- `CalendarDbContext`
- EF-конфигурации и репозитории
- миграции
- `RuDateTimeParser`
- `RuBirthdayDateParser`
- `SystemClock`
- `EnvTimeZoneProvider`
- Quartz jobs
- `TelegramPublisher`

### Worker

Содержит Telegram-рантайм:

- запуск хоста
- загрузка `.env`
- настройку DI
- long polling
- access control
- маршрутизацию update'ов
- FSM-сценарии и клавиатуры
- логирование

Текущие важные файлы:

- `Program.cs`
- `Config/AppOptions.cs`
- `Hosting/BotHostedService.cs`
- `Telegram/UpdateDispatcher.cs`
- `Telegram/Handlers/*.cs`
- `Telegram/*Conversation*.cs`
- `Telegram/InlineKeyboards.cs`
- `Telegram/ReplyKeyboards.cs`

`AccessGuard` находится в `Worker/Telegram`, а не в Infrastructure.

## 5. Что уже реализовано

### Команды бота

Текущий список команд:

- `/birthday_add`
- `/birthday_list`
- `/birthday_edit`
- `/birthday_delete`
- `/event_add`
- `/event_list`
- `/event_edit`
- `/event_delete`
- `/cancel`
- `/digest_test`
- `/start`
- `/menu`

Дополнительно используются текстовые кнопки:

- `📅 На неделю`
- `📅 На месяц`

### FSM-сценарии

Реально есть отдельные сценарии:

- добавление дня рождения
- редактирование дня рождения
- добавление события
- редактирование события
- ожидание прикрепления/замены файла к событию

Состояние диалога хранится в таблице `conversation_states`, ключом служит `user_id`.

### События

Поддерживаются:

- one-off события
- ежегодные события
- all-day и события со временем
- описание
- место
- ссылка
- вложения

### Вложения

Поддерживается:

- прикрепление нескольких файлов к событию
- замена файла
- хранение telegram file metadata
- версия вложения (`Version`)
- текущая версия через `IsCurrent`

### Дайджесты

Есть:

- daily digest
- weekly digest
- monthly digest
- отправка в `TELEGRAM_TARGET_CHAT`
- идемпотентность через `notification_runs`
- хранение последних опубликованных post/message id через `latest_digest_posts`
- пересборка и редактирование последних daily/weekly/monthly постов при создании нового события или дня рождения, если новая запись попадает в период соответствующего поста

## 6. Правила даты, времени и TZ

Все расчеты делаются в локальной таймзоне из `TZ`, через `NodaTime`.

### One-off дата без года

Для `RuDateTimeParser` дата без года трактуется как ближайшая будущая локальная дата:

- берется текущий локальный год
- если дата уже прошла относительно локального `today`, используется следующий год

Это покрыто тестами.

### Поддерживаемые форматы событий

`RuDateTimeParser` сейчас поддерживает:

- `yyyy-MM-dd HH:mm`
- `d MMMM yyyy HH:mm`
- `dd.MM.yyyy HH:mm`
- `d.MM.yyyy HH:mm`
- `dd.M.yyyy HH:mm`
- `d.M.yyyy HH:mm`
- `dd.MM.yyyy H:mm`
- `d.MM.yyyy H:mm`
- `dd.M.yyyy H:mm`
- `d.M.yyyy H:mm`
- `d MMMM yyyy`
- `d MMMM`
- `yyyy-MM-dd`
- `d.MM`
- `dd.MM`
- `d.MM.yyyy`
- `dd.MM.yyyy`
- `HH:mm`
- `H:mm`
- `HHmm`
- `Hmm`

### Поддерживаемые форматы дней рождения

`RuBirthdayDateParser` сейчас поддерживает:

- `d MMMM`
- `d MMMM yyyy`
- `d.MM`
- `dd.MM`
- `d.MM.yyyy`
- `dd.MM.yyyy`
- `yyyy-MM-dd`

Для дней рождения в доменной модели хранится `day/month`, а год остается optional.

## 7. Правила расчета дайджестов

### Daily

- триггер: каждый день в `09:00`
- период: текущий локальный день

### Weekly

- триггер: воскресенье в `21:00`
- период: следующая неделя `понедельник -> воскресенье`
- если сегодня уже понедельник, weekly digest все равно строится на следующий понедельник, а не на текущую неделю

Это поведение подтверждено кодом и тестами.

### Monthly

- триггер: последний день месяца в `18:00`
- период: следующий календарный месяц целиком

### Идемпотентность

Таблица `notification_runs` имеет уникальный ключ по:

- `digest_type`
- `period_start_local`
- `period_end_local`
- `time_zone_id`

Это правило нельзя ломать при изменении jobs или логики отправки.

### Последние посты

Таблица `latest_digest_posts` хранит по одной записи на каждый `DigestType`:

- `Daily`
- `Weekly`
- `Monthly`

При публикации нового дайджеста соответствующего типа:

- в Telegram отправляется новое сообщение
- создается `notification_run`
- в `latest_digest_posts` заменяется `notification_run_id` и `telegram_message_id` для этого `digest_type`

При создании нового события или дня рождения:

- бот смотрит последние daily/weekly/monthly посты
- если новая запись попадает в период поста, текст этого поста пересобирается заново
- затем бот делает `EditMessageText` по сохраненному `telegram_message_id`

Это поведение относится именно к созданию, не к редактированию существующих записей.

## 8. Конфигурация и окружение

Обязательные env-переменные:

- `TELEGRAM_BOT_TOKEN`
- `TELEGRAM_ALLOWED_USER_IDS`
- `TELEGRAM_TARGET_CHAT`
- `TZ`
- `POSTGRES_HOST`
- `POSTGRES_PORT`
- `POSTGRES_DB`
- `POSTGRES_USER`
- `POSTGRES_PASSWORD`

Дополнительно используются:

- `LOG_LEVEL`
- `LOG_LEVEL_FILE`
- `TELEGRAM_CHANNEL_TRIGGER_WINDOW_MINUTES`

Практические детали из текущего кода:

- Worker пытается загрузить `.env` из текущей директории, а если не находит, ищет выше по дереву
- `AppOptions` автоматически подменяет `POSTGRES_HOST`:
  - в Docker `localhost` -> `postgres`
  - вне Docker `postgres` -> `localhost`
- `Program.cs` содержит логику проверки/поднятия Postgres перед запуском хоста
- `Program.cs` также содержит fallback SQL для старых схем: создание таблиц и восстановление структуры `notification_runs`, `attachments`, `latest_digest_posts`
- `TELEGRAM_CHANNEL_TRIGGER_WINDOW_MINUTES` задает окно догоняющей публикации после рестарта; по умолчанию используется `180` минут

## 9. База данных

Текущие сущности в `CalendarDbContext`:

- `Birthdays`
- `ConversationStates`
- `Events`
- `Attachments`
- `NotificationRuns`
- `LatestDigestPosts`

Конфигурации лежат в:

`src/SnakeFrogCalendarBot.Infrastructure/Persistence/Configurations`

При изменении схемы нужно:

- обновлять EF-конфигурации
- добавлять миграции
- проверять, что не сломаны уникальные индексы и лимиты длины строк

## 10. Тесты

Сейчас в репозитории есть как минимум:

- парсер дат/времени: `tests/SnakeFrogCalendarBot.Tests/Parsing`
- расчеты периодов дайджестов: `tests/SnakeFrogCalendarBot.Tests/Notifications/PeriodCalculationTests.cs`
- обновление последних постов после создания новых записей: `tests/SnakeFrogCalendarBot.Tests/Notifications/RefreshLatestDigestPostsTests.cs`

При изменении логики парсинга, TZ, периодов дайджеста или обновления опубликованных постов тесты нужно обновлять вместе с кодом.

## 11. Команды для локальной работы

```bash
dotnet build SnakeFrogCalendarBot.sln
dotnet test tests/SnakeFrogCalendarBot.Tests/SnakeFrogCalendarBot.Tests.csproj
docker compose up --build
```

## 12. Правила для будущих изменений

1. Не описывать в `AGENTS.md` несуществующие классы, папки и сценарии как уже реализованные.
2. Новую Telegram-логику держать в `Worker`, а не протаскивать детали Bot API в `Application` или `Domain`.
3. Бизнес-правила по датам, дайджестам и инвариантам хранить в `Application`/`Domain`, а не размазывать по handler'ам.
4. Любые изменения парсинга даты, периодов дайджеста или логики обновления последних постов сопровождать тестами.
5. При добавлении новых команд обновлять сразу:
   - `BotCommands`
   - handler'ы
   - клавиатуры
   - `AGENTS.md`, если меняется пользовательский сценарий
