1) Архитектура приложения

Слои и ответственность
	•	CalendarBot.Worker
        - Long polling; получение апдейтов; маршрутизация (commands/callbacks/messages); FSM оркестрация; проверка доступа.
	•	CalendarBot.Application
        - Use-cases; формирование дайджестов; единый формат вывода; интерфейсы к инфраструктуре.
	•	CalendarBot.Domain
        - Сущности; инварианты; value objects.
	•	CalendarBot.Infrastructure
        - EF Core (Postgres); репозитории; Quartz jobs; TelegramPublisher; парсер даты/времени; логирование.

Уведомления
    Quartz jobs:
	•	Daily: каждый день 09:00
	•	Weekly: воскресенье 21:00 (период: следующий календарный понедельник–воскресенье, либо текущий? фиксируем ниже)
	•	Monthly: последний день месяца 18:00 (период: следующий месяц)

Чтобы не было дублей при рестарте: notification_runs (идемпотентность) с unique key по (type; period_start_local; period_end_local; timezone).

Канал
	•	TELEGRAM_TARGET_CHAT=@channelname (username); отправка через ChatId("@...").

⸻

2) Правила (зафиксированные)

2.1. Неделя “на неделю вперёд”

Ты сказал: неделя пн–вс. Для воскресного уведомления логично и предсказуемо:
	•	если сейчас воскресенье; строим период следующий понедельник 00:00 → следующее воскресенье 23:59:59 (локально).

Если хочешь “текущая неделя” — скажешь, но сейчас фиксирую “следующая неделя”, иначе в воскресенье ты получаешь почти прошедшую неделю.

2.2. Дата без года для one-off
	•	Ввод 7 января трактуется как ближайшая будущая дата в локальной TZ:
	•	берём текущий год; собираем дату;
	•	если дата < сегодня (локально) → +1 год.

2.3. Парсинг даты/времени (минимальный поддерживаемый набор)
	•	yyyy-MM-dd HH:mm
	•	yyyy-MM-dd (это all-day)
	•	d MMMM yyyy (русские месяцы)
	•	d MMMM (без года; правило выше)
	•	HH:mm (как отдельный ввод времени на шаге FSM)

⸻

3) Структура проектов и файлов

(Это итоговая; её можно копировать как checklist.)

/src
  /CalendarBot.Domain
    /Entities
      Event.cs
      Birthday.cs
      Attachment.cs
      NotificationRun.cs
      ConversationState.cs
    /Enums
      EventKind.cs
      DigestType.cs
    /ValueObjects
      MonthDay.cs
    /Exceptions
      DomainException.cs

  /CalendarBot.Application
    /Abstractions
      /Persistence
        IEventRepository.cs
        IBirthdayRepository.cs
        IAttachmentRepository.cs
        INotificationRunRepository.cs
        IConversationStateRepository.cs
      /Telegram
        ITelegramPublisher.cs
      /Time
        IClock.cs
        ITimeZoneProvider.cs
      /Parsing
        IDateTimeParser.cs
    /Dto
      CalendarItemDto.cs
    /Formatting
      DigestFormatter.cs
    /UseCases
      /Events
        CreateEvent.cs
        UpdateEvent.cs
        DeleteEvent.cs
        AttachFileToEvent.cs
        ListUpcomingItems.cs
      /Birthdays
        CreateBirthday.cs
        UpdateBirthday.cs
        DeleteBirthday.cs
        ListBirthdays.cs
      /Notifications
        BuildDailyDigest.cs
        BuildWeeklyDigest.cs
        BuildMonthlyDigest.cs
        SendDigest.cs
    /Conversation
      ConversationName.cs
      ConversationContext.cs
      ConversationStepResult.cs

  /CalendarBot.Infrastructure
    /Persistence
      CalendarDbContext.cs
      /Configurations
        EventConfiguration.cs
        BirthdayConfiguration.cs
        AttachmentConfiguration.cs
        NotificationRunConfiguration.cs
        ConversationStateConfiguration.cs
      /Repositories
        EventRepository.cs
        BirthdayRepository.cs
        AttachmentRepository.cs
        NotificationRunRepository.cs
        ConversationStateRepository.cs
    /Time
      SystemClock.cs
      EnvTimeZoneProvider.cs
    /Parsing
      RuDateTimeParser.cs
    /Telegram
      TelegramPublisher.cs
      AccessGuard.cs
      TargetChatResolver.cs
    /Jobs
      DailyDigestJob.cs
      WeeklyDigestJob.cs
      MonthlyDigestJob.cs

  /CalendarBot.Worker
    Program.cs
    /Config
      AppOptions.cs
    /Hosting
      BotHostedService.cs
    /Telegram
      UpdateDispatcher.cs
      /Handlers
        CommandHandlers.cs
        CallbackHandlers.cs
        MessageHandlers.cs
        UnknownHandler.cs
    /Logging
      SerilogSetup.cs

/tests
  /CalendarBot.Tests
    /Parsing
      RuDateTimeParserTests.cs
    /Domain
      MonthDayTests.cs
    /UseCases
      DigestBuildTests.cs
    /Infrastructure
      RepositoryIntegrationTests.cs


⸻

4) FSM (как будут выглядеть сценарии)

4.1. /event_add

Шаги:
	1.	Title
	2.	DateTime input:
	•	если строка содержит и дату и время → парсим
	•	если только дата → спросить “весь день?” (да/нет)
	•	да → all-day; time=null
	•	нет → следующий шаг “введите время HH:mm”
	3.	Optional fields (кнопками inline):
	•	“Добавить описание”
	•	“Добавить место”
	•	“Добавить ссылку”
	•	“Прикрепить файл”
	•	“Сохранить”
	4.	Если “Прикрепить файл” → ожидаем Document/Photo/Video/Audio/…; сохраняем как Attachment current v1.

4.2. /birthday_add
	1.	Name
	2.	Date input d MMMM (или yyyy-MM-dd, если хочется)
	3.	BirthYear? (опционально; кнопка “Пропустить”)
	4.	Contact? (опционально; “Пропустить”)
	5.	Save

⸻

5) Шаги разработки (по спринтам)

Спринт 1 — база + бот “живой”
	1.	Solution; проекты; docker-compose (postgres + bot); env.
	2.	Long polling; allowlist; отправка “нет доступа”.
	3.	EF Core DbContext; migrations; базовые таблицы.
	4.	/birthday_add; /birthday_list.

Спринт 2 — события + парсинг даты
	5.	RuDateTimeParser + unit-тесты, включая “7 января” → ближайшая будущая дата.
	6.	/event_add (FSM); /event_list.

Спринт 3 — файлы
	7.	Attach/replace файл к событию; версии attachments; /event_edit минимум для файла.

Спринт 4 — уведомления
	8.	Quartz + 3 jobs; notification_runs (идемпотентность).
	9.	DigestFormatter (daily/weekly/monthly) и отправка в канал @username.

Спринт 5 — доведение UX
	10.	Inline-клавиатуры для list/edit/delete.
	11.	Обработка ошибок; отмена сценариев; логирование.

⸻