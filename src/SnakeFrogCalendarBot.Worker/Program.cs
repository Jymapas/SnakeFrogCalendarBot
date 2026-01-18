using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Quartz;
using Serilog;
using SnakeFrogCalendarBot.Application.Abstractions.Parsing;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Telegram;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Formatting;
using SnakeFrogCalendarBot.Application.UseCases.Birthdays;
using SnakeFrogCalendarBot.Application.UseCases.Events;
using SnakeFrogCalendarBot.Application.UseCases.Notifications;
using SnakeFrogCalendarBot.Infrastructure.Jobs;
using SnakeFrogCalendarBot.Infrastructure.Parsing;
using SnakeFrogCalendarBot.Infrastructure.Persistence;
using SnakeFrogCalendarBot.Infrastructure.Persistence.Repositories;
using SnakeFrogCalendarBot.Infrastructure.Telegram;
using SnakeFrogCalendarBot.Infrastructure.Time;
using SnakeFrogCalendarBot.Worker.Config;
using SnakeFrogCalendarBot.Worker.Hosting;
using SnakeFrogCalendarBot.Worker.Logging;
using SnakeFrogCalendarBot.Worker.Telegram;
using SnakeFrogCalendarBot.Worker.Telegram.Handlers;
using Telegram.Bot;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Npgsql.EntityFrameworkCore.PostgreSQL.Extensions;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Net.Sockets;
using System.Data;

var currentDir = Directory.GetCurrentDirectory();
var envPath = Path.Combine(currentDir, ".env");

if (!File.Exists(envPath))
{
    var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
    var assemblyDir = Path.GetDirectoryName(assemblyLocation);
    var projectRoot = assemblyDir;
    
    for (int i = 0; i < 6 && projectRoot != null; i++)
    {
        projectRoot = Path.GetDirectoryName(projectRoot);
        var candidateEnv = Path.Combine(projectRoot ?? "", ".env");
        if (File.Exists(candidateEnv))
        {
            envPath = candidateEnv;
            break;
        }
    }
}

if (File.Exists(envPath))
{
    Env.Load(envPath);
}

Log.Logger = SerilogSetup.CreateLogger();

try
{
    await EnsurePostgresRunningAsync();

    var builder = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureAppConfiguration((context, config) =>
        {
            config.AddEnvironmentVariables();
        })
        .ConfigureServices((context, services) =>
        {
            var options = AppOptions.FromConfiguration(context.Configuration);
            services.AddSingleton(options);

            services.AddSingleton<AccessGuard>();
            services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(options.TelegramBotToken));
            services.AddSingleton<UpdateDispatcher>();
            services.AddScoped<CommandHandlers>();
            services.AddScoped<MessageHandlers>();
            services.AddScoped<CallbackHandlers>(sp =>
            {
                var botClient = sp.GetRequiredService<ITelegramBotClient>();
                var conversationRepository = sp.GetRequiredService<IConversationStateRepository>();
                var clock = sp.GetRequiredService<IClock>();
                var getEventWithAttachment = sp.GetRequiredService<GetEventWithAttachment>();
                var replaceEventFile = sp.GetRequiredService<ReplaceEventFile>();
                var eventRepository = sp.GetRequiredService<IEventRepository>();
                var birthdayRepository = sp.GetRequiredService<IBirthdayRepository>();
                var deleteEvent = sp.GetRequiredService<DeleteEvent>();
                var deleteBirthday = sp.GetRequiredService<DeleteBirthday>();
                var appOptions = sp.GetRequiredService<AppOptions>();
                var listBirthdays = sp.GetRequiredService<ListBirthdays>();
                var birthdayFormatter = sp.GetRequiredService<BirthdayListFormatter>();
                return new CallbackHandlers(
                    botClient,
                    conversationRepository,
                    clock,
                    getEventWithAttachment,
                    replaceEventFile,
                    eventRepository,
                    birthdayRepository,
                    deleteEvent,
                    deleteBirthday,
                    appOptions,
                    sp,
                    listBirthdays,
                    birthdayFormatter);
            });
            services.AddHostedService<BotHostedService>();

            services.AddDbContext<CalendarDbContext>(db =>
                db.UseNpgsql(
                        options.PostgresConnectionString,
                        npgsql => npgsql.MigrationsAssembly("SnakeFrogCalendarBot.Infrastructure")));

            services.AddScoped<IBirthdayRepository, BirthdayRepository>();
            services.AddScoped<IEventRepository, EventRepository>();
            services.AddScoped<IAttachmentRepository, AttachmentRepository>();
            services.AddScoped<INotificationRunRepository, NotificationRunRepository>();
            services.AddScoped<IConversationStateRepository, ConversationStateRepository>();

            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<ITimeZoneProvider>(sp =>
            {
                var options = sp.GetRequiredService<AppOptions>();
                return new EnvTimeZoneProvider(options.TimeZone);
            });
            services.AddSingleton<IBirthdayDateParser, RuBirthdayDateParser>();
            services.AddSingleton<IDateTimeParser, RuDateTimeParser>();
            services.AddSingleton<BirthdayListFormatter>();
            services.AddSingleton<EventListFormatter>();
            services.AddSingleton<DigestFormatter>();

            services.AddSingleton<ITelegramPublisher>(sp =>
            {
                var botClient = sp.GetRequiredService<ITelegramBotClient>();
                var options = sp.GetRequiredService<AppOptions>();
                var logger = sp.GetRequiredService<ILogger<TelegramPublisher>>();
                return new TelegramPublisher(botClient, options.TelegramTargetChat, logger);
            });

            services.AddScoped<CreateBirthday>();
            services.AddScoped<ListBirthdays>();
            services.AddScoped<UpdateBirthday>();
            services.AddScoped<DeleteBirthday>();
            services.AddScoped<CreateEvent>();
            services.AddScoped<ListUpcomingItems>();
            services.AddScoped<UpdateEvent>();
            services.AddScoped<DeleteEvent>();
            services.AddScoped<AttachFileToEvent>();
            services.AddScoped<ReplaceEventFile>();
            services.AddScoped<GetEventWithAttachment>();
            services.AddScoped<BuildDailyDigest>();
            services.AddScoped<BuildWeeklyDigest>();
            services.AddScoped<BuildMonthlyDigest>();
            services.AddScoped<SendDigest>();

            services.AddQuartz(q =>
            {
                var dailyJobKey = new JobKey("DailyDigestJob");
                q.AddJob<DailyDigestJob>(dailyJobKey);
                q.AddTrigger(opts => opts
                    .ForJob(dailyJobKey)
                    .WithIdentity("DailyDigestTrigger")
                    .WithCronSchedule("0 46 20 ? * *")); // 19:41 для теста (было: 0 0 9 ? * *)

                var weeklyJobKey = new JobKey("WeeklyDigestJob");
                q.AddJob<WeeklyDigestJob>(weeklyJobKey);
                q.AddTrigger(opts => opts
                    .ForJob(weeklyJobKey)
                    .WithIdentity("WeeklyDigestTrigger")
                    .WithCronSchedule("0 0 21 ? * SUN"));

                var monthlyJobKey = new JobKey("MonthlyDigestJob");
                q.AddJob<MonthlyDigestJob>(monthlyJobKey);
                q.AddTrigger(opts => opts
                    .ForJob(monthlyJobKey)
                    .WithIdentity("MonthlyDigestTrigger")
                    .WithCronSchedule("0 0 18 L * ?"));
            });

            services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
        });

    var host = builder.Build();

    using (var scope = host.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<AppOptions>();
        
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(options.PostgresConnectionString);
        var databaseName = connectionStringBuilder.Database;
        var userName = connectionStringBuilder.Username;
        var password = connectionStringBuilder.Password;
        
        var adminConnectionString = new NpgsqlConnectionStringBuilder
        {
            Host = connectionStringBuilder.Host,
            Port = connectionStringBuilder.Port,
            Database = "postgres"
        };
        
        var currentUser = Environment.GetEnvironmentVariable("USER") ?? Environment.GetEnvironmentVariable("USERNAME") ?? "postgres";
        adminConnectionString.Username = currentUser;
        
        Log.Debug("Подключение к системной БД postgres с пользователем {AdminUser} для создания пользователя {TargetUser} и БД {DatabaseName}", currentUser, userName, databaseName);

        try
        {
            await using (var adminConnection = new NpgsqlConnection(adminConnectionString.ConnectionString))
            {
                await adminConnection.OpenAsync();
                Log.Debug("Подключение к системной БД postgres успешно");
                
                var checkUserCommand = adminConnection.CreateCommand();
                checkUserCommand.CommandText = "SELECT 1 FROM pg_roles WHERE rolname = $1";
                checkUserCommand.Parameters.AddWithValue(userName ?? (object)DBNull.Value);
                var userExists = await checkUserCommand.ExecuteScalarAsync() != null;
                Log.Debug("Пользователь {UserName} существует: {Exists}", userName, userExists);

                if (!userExists)
                {
                    var createUserCommand = adminConnection.CreateCommand();
                    if (string.IsNullOrEmpty(password))
                    {
                        createUserCommand.CommandText = $"CREATE USER \"{userName}\"";
                    }
                    else
                    {
                        createUserCommand.CommandText = $"CREATE USER \"{userName}\" WITH PASSWORD '{password.Replace("'", "''")}'";
                    }
                    await createUserCommand.ExecuteNonQueryAsync();
                    Log.Information("Пользователь {UserName} создан", userName);
                }

                var checkDbCommand = adminConnection.CreateCommand();
                checkDbCommand.CommandText = "SELECT 1 FROM pg_database WHERE datname = $1";
                checkDbCommand.Parameters.AddWithValue(databaseName ?? (object)DBNull.Value);
                var dbExists = await checkDbCommand.ExecuteScalarAsync() != null;
                Log.Debug("База данных {DatabaseName} существует: {Exists}", databaseName, dbExists);

                if (!dbExists)
                {
                    var createDbCommand = adminConnection.CreateCommand();
                    createDbCommand.CommandText = $"CREATE DATABASE \"{databaseName}\" OWNER \"{userName}\"";
                    await createDbCommand.ExecuteNonQueryAsync();
                    Log.Information("База данных {DatabaseName} создана", databaseName);
                }
                else
                {
                    var grantCommand = adminConnection.CreateCommand();
                    grantCommand.CommandText = $"GRANT ALL PRIVILEGES ON DATABASE \"{databaseName}\" TO \"{userName}\"";
                    try
                    {
                        await grantCommand.ExecuteNonQueryAsync();
                        Log.Debug("Права на базу данных {DatabaseName} выданы пользователю {UserName}", databaseName, userName);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Не удалось выдать права (возможно, уже выданы)");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при создании пользователя/базы данных. Попытка подключения с текущими учетными данными...");
        }

        try
        {
            await dbContext.Database.CanConnectAsync();
            Log.Debug("Подключение к базе данных {DatabaseName} успешно", databaseName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Не удалось подключиться к базе данных {DatabaseName} с пользователем {UserName}", databaseName, userName);
            throw;
        }

        try
        {
            var allMigrations = dbContext.Database.GetMigrations();
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
            var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
            
            Log.Information("Всего миграций в сборке: {Count}", allMigrations.Count());
            if (allMigrations.Any())
            {
                Log.Information("Список миграций: {Migrations}", string.Join(", ", allMigrations));
            }
            Log.Information("Применено миграций: {Count}", appliedMigrations.Count());
            if (appliedMigrations.Any())
            {
                Log.Information("Примененные миграции: {Migrations}", string.Join(", ", appliedMigrations));
            }
            Log.Information("Ожидают применения: {Count}", pendingMigrations.Count());
            if (pendingMigrations.Any())
            {
                Log.Information("Ожидающие миграции: {Migrations}", string.Join(", ", pendingMigrations));
            }
            
            if (allMigrations.Any())
            {
                if (pendingMigrations.Any())
                {
                    Log.Information("Применение {Count} миграций: {Migrations}", pendingMigrations.Count(), string.Join(", ", pendingMigrations));
                    await dbContext.Database.MigrateAsync();
                    Log.Information("Миграции применены успешно");
                }
                else
                {
                    Log.Information("Все миграции уже применены");
                }
            }
            else
            {
                Log.Warning("Миграции не найдены в сборке. Используется EnsureCreated() для создания схемы БД");
                
                try
                {
                    var canConnect = await dbContext.Database.CanConnectAsync();
                    
                    if (canConnect)
                    {
                        var created = await dbContext.Database.EnsureCreatedAsync();
                        Log.Information("Схема БД создана через EnsureCreated(): {Created}", created);
                    }
                    else
                    {
                        Log.Error("Не удалось подключиться к базе данных для создания схемы");
                    }
                }
                catch (Exception ensureEx)
                {
                    Log.Error(ensureEx, "Ошибка при создании схемы через EnsureCreated()");
                }
            }
            
            var tablesExist = await CheckTablesExistAsync(dbContext);
            Log.Information("Проверка таблиц: conversation_states={HasConversationStates}, birthdays={HasBirthdays}, events={HasEvents}", 
                tablesExist.HasConversationStates, tablesExist.HasBirthdays, tablesExist.HasEvents);
            
            if (!tablesExist.HasConversationStates || !tablesExist.HasBirthdays || !tablesExist.HasEvents)
            {
                Log.Warning("Не все таблицы существуют. Принудительное создание схемы через EnsureCreated()...");
                
                try
                {
                    var created = await dbContext.Database.EnsureCreatedAsync();
                    Log.Information("Схема БД создана через EnsureCreated(): {Created}", created);
                    
                    if (!created)
                    {
                        Log.Warning("EnsureCreated() вернул false. Попытка создать таблицы вручную через SQL...");
                        await CreateTablesManuallyAsync(dbContext);
                    }
                    
                    var tablesExistAfter = await CheckTablesExistAsync(dbContext);
                    Log.Information("Проверка таблиц после EnsureCreated: conversation_states={HasConversationStates}, birthdays={HasBirthdays}, events={HasEvents}", 
                        tablesExistAfter.HasConversationStates, tablesExistAfter.HasBirthdays, tablesExistAfter.HasEvents);
                }
                catch (Exception createEx)
                {
                    Log.Error(createEx, "Ошибка при принудительном создании схемы");
                    Log.Warning("Попытка создать таблицы вручную через SQL...");
                    await CreateTablesManuallyAsync(dbContext);
                }
            }
            
            if (tablesExist.HasEvents)
            {
                Log.Information("Проверка и добавление недостающих колонок в таблице events...");
                await EnsureEventColumnsExistAsync(dbContext);
            }
            
            var notificationRunsExist = await CheckTableExistsAsync(dbContext, "notification_runs");
            Log.Information("Таблица notification_runs существует: {Exists}", notificationRunsExist);
            if (notificationRunsExist)
            {
                Log.Information("Проверка и исправление типа колонок в таблице notification_runs...");
                await EnsureNotificationRunColumnsTypeAsync(dbContext);
            }
            
            var attachmentsExist = await CheckTableExistsAsync(dbContext, "attachments");
            Log.Information("Таблица attachments существует: {Exists}", attachmentsExist);
            if (attachmentsExist)
            {
                Log.Information("Принудительное удаление устаревших колонок из attachments...");
                try
                {
                    await ForceAddAttachmentColumnsAsync(dbContext);
                }
                catch (Exception forceEx)
                {
                    Log.Warning(forceEx, "Не удалось принудительно обновить колонки в attachments");
                }
                
                var finalCheckCommand = dbContext.Database.GetDbConnection().CreateCommand();
                if (dbContext.Database.GetDbConnection().State != ConnectionState.Open)
                {
                    await dbContext.Database.GetDbConnection().OpenAsync();
                }
                finalCheckCommand.CommandText = @"
                    SELECT column_name, data_type, is_nullable
                    FROM information_schema.columns 
                    WHERE table_schema = 'public' 
                    AND table_name = 'attachments' 
                    ORDER BY column_name";
                
                var allColumns = new List<string>();
                using (var reader = await finalCheckCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        allColumns.Add($"{reader.GetString(0)} ({reader.GetString(1)}, nullable: {reader.GetString(2)})");
                    }
                }
                Log.Information("Финальная структура таблицы attachments: {Columns}", string.Join(", ", allColumns));
                
                Log.Information("Проверка и добавление недостающих колонок в таблице attachments...");
                try
                {
                    await EnsureAttachmentColumnsExistAsync(dbContext);
                }
                catch (Exception attachEx)
                {
                    Log.Error(attachEx, "Критическая ошибка при добавлении колонок в attachments. Попытка принудительного добавления...");
                    await ForceAddAttachmentColumnsAsync(dbContext);
                }
            }
            else
            {
                Log.Warning("Таблица attachments не найдена. Попытка принудительного добавления колонок на случай, если таблица существует...");
                try
                {
                    await ForceAddAttachmentColumnsAsync(dbContext);
                }
                catch (Exception forceEx)
                {
                    Log.Warning(forceEx, "Не удалось добавить колонки в attachments (таблица может не существовать)");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ошибка при применении миграций: {Message}", ex.Message);
            Log.Warning("Попытка создать схему БД через EnsureCreated()...");
            try
            {
                var created = await dbContext.Database.EnsureCreatedAsync();
                Log.Information("Схема БД создана через EnsureCreated(): {Created}", created);
            }
            catch (Exception ensureEx)
            {
                Log.Error(ensureEx, "Не удалось создать схему БД");
                throw;
            }
        }
    }

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static async Task EnsurePostgresRunningAsync()
{
    if (IsRunningInDocker())
    {
        return;
    }

    var postgresHost = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
    var postgresPort = int.TryParse(Environment.GetEnvironmentVariable("POSTGRES_PORT"), out var port) ? port : 5432;

    var checkHost = postgresHost == "postgres" ? "localhost" : postgresHost;

    if (!await IsPortOpenAsync(checkHost, postgresPort))
    {
        Log.Information("PostgreSQL не доступен на {Host}:{Port}, пытаюсь запустить локально...", checkHost, postgresPort);
        
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "brew",
                Arguments = "services start postgresql@16",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                var output = await outputTask;
                var error = await errorTask;

                if (process.ExitCode == 0)
                {
                    Log.Information("Запуск PostgreSQL через brew services, ожидание готовности...");
                    
                    for (int i = 0; i < 10; i++)
                    {
                        await Task.Delay(2000);
                        if (await IsPortOpenAsync("localhost", postgresPort))
                        {
                            Log.Information("PostgreSQL готов к работе");
                            return;
                        }
                    }
                    
                    Log.Warning("PostgreSQL не стал доступен в течение 20 секунд");
                }
                else
                {
                    if (error.Contains("Service `postgresql@16` is already started") || 
                        error.Contains("already started") ||
                        output.Contains("already started"))
                    {
                        Log.Information("PostgreSQL уже запущен через brew services");
                    }
                    else
                    {
                        Log.Warning("Не удалось запустить PostgreSQL через brew services. Exit code: {ExitCode}, Error: {Error}", process.ExitCode, error);
                        Log.Information("Попробуйте запустить вручную: brew services start postgresql@16");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ошибка при попытке запустить PostgreSQL. Убедитесь, что PostgreSQL установлен через Homebrew.");
            Log.Information("Для установки: brew install postgresql@16");
            Log.Information("Для запуска: brew services start postgresql@16");
        }
    }
    else
    {
        Log.Information("PostgreSQL уже доступен на {Host}:{Port}", checkHost, postgresPort);
    }
}

static async Task CreateTablesManuallyAsync(CalendarDbContext dbContext)
{
    try
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS conversation_states (
                user_id BIGINT NOT NULL PRIMARY KEY,
                conversation_name VARCHAR(100) NOT NULL,
                step VARCHAR(100) NOT NULL,
                state_json TEXT,
                created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
                updated_at_utc TIMESTAMP WITH TIME ZONE NOT NULL
            );
            
            CREATE TABLE IF NOT EXISTS birthdays (
                id SERIAL PRIMARY KEY,
                person_name VARCHAR(200) NOT NULL,
                day INTEGER NOT NULL,
                month INTEGER NOT NULL,
                birth_year INTEGER,
                contact VARCHAR(200),
                created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
                updated_at_utc TIMESTAMP WITH TIME ZONE NOT NULL
            );
            
            CREATE INDEX IF NOT EXISTS ix_birthdays_month_day ON birthdays(month, day);
            
            CREATE TABLE IF NOT EXISTS events (
                id SERIAL PRIMARY KEY,
                title VARCHAR(200) NOT NULL,
                kind INTEGER NOT NULL,
                is_all_day BOOLEAN NOT NULL,
                occurs_at_utc TIMESTAMP WITH TIME ZONE,
                month INTEGER,
                day INTEGER,
                time_of_day BIGINT,
                description VARCHAR(1000),
                place VARCHAR(200),
                link VARCHAR(500),
                created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
                updated_at_utc TIMESTAMP WITH TIME ZONE NOT NULL
            );
            
            CREATE TABLE IF NOT EXISTS attachments (
                id SERIAL PRIMARY KEY,
                event_id INTEGER NOT NULL,
                telegram_file_id VARCHAR(200) NOT NULL,
                telegram_file_unique_id VARCHAR(200) NOT NULL,
                file_name VARCHAR(500) NOT NULL,
                mime_type VARCHAR(100),
                size BIGINT,
                version INTEGER NOT NULL,
                is_current BOOLEAN NOT NULL,
                uploaded_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
                FOREIGN KEY (event_id) REFERENCES events(id) ON DELETE CASCADE
            );
            
            CREATE TABLE IF NOT EXISTS notification_runs (
                id SERIAL PRIMARY KEY,
                digest_type INTEGER NOT NULL,
                period_start_local TIMESTAMP WITHOUT TIME ZONE NOT NULL,
                period_end_local TIMESTAMP WITHOUT TIME ZONE NOT NULL,
                time_zone_id VARCHAR(100) NOT NULL,
                created_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
                CONSTRAINT uq_notification_runs_type_period_timezone UNIQUE(digest_type, period_start_local, period_end_local, time_zone_id)
            );";
        
        await command.ExecuteNonQueryAsync();
        Log.Information("Таблицы созданы вручную через SQL");
        
        var dropOldColumnsCommand = connection.CreateCommand();
        dropOldColumnsCommand.CommandText = @"
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'attachments' AND column_name = 'file_id') THEN
                    ALTER TABLE attachments DROP COLUMN file_id CASCADE;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'attachments' AND column_name = 'file_type') THEN
                    ALTER TABLE attachments DROP COLUMN file_type CASCADE;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'attachments' AND column_name = 'file_size') THEN
                    ALTER TABLE attachments DROP COLUMN file_size CASCADE;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'attachments' AND column_name = 'created_at_utc') THEN
                    ALTER TABLE attachments DROP COLUMN created_at_utc CASCADE;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'attachments' AND column_name = 'updated_at_utc') THEN
                    ALTER TABLE attachments DROP COLUMN updated_at_utc CASCADE;
                END IF;
            END $$;";
        
        try
        {
            await dropOldColumnsCommand.ExecuteNonQueryAsync();
            Log.Information("Удаление устаревших колонок из attachments выполнено");
        }
        catch (Exception dropEx)
        {
            Log.Warning(dropEx, "Ошибка при удалении устаревших колонок (возможно, их уже нет)");
        }
        
        var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = @"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'events' AND column_name = 'month') THEN
                    ALTER TABLE events ADD COLUMN month INTEGER;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'events' AND column_name = 'day') THEN
                    ALTER TABLE events ADD COLUMN day INTEGER;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'events' AND column_name = 'time_of_day') THEN
                    ALTER TABLE events ADD COLUMN time_of_day BIGINT;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'events' AND column_name = 'occurs_at_utc') THEN
                    ALTER TABLE events ADD COLUMN occurs_at_utc TIMESTAMP WITH TIME ZONE;
                END IF;
            END $$;";
        
        try
        {
            await alterCommand.ExecuteNonQueryAsync();
            Log.Information("Проверка и добавление недостающих колонок выполнены");
        }
        catch (Exception alterEx)
        {
            Log.Warning(alterEx, "Ошибка при добавлении недостающих колонок (возможно, они уже существуют)");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Ошибка при создании таблиц вручную");
        throw;
    }
}

static async Task EnsureEventColumnsExistAsync(CalendarDbContext dbContext)
{
    try
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        
        var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = @"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'events' AND column_name = 'month') THEN
                    ALTER TABLE events ADD COLUMN month INTEGER;
                    RAISE NOTICE 'Added column month';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'events' AND column_name = 'day') THEN
                    ALTER TABLE events ADD COLUMN day INTEGER;
                    RAISE NOTICE 'Added column day';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'events' AND column_name = 'time_of_day') THEN
                    ALTER TABLE events ADD COLUMN time_of_day BIGINT;
                    RAISE NOTICE 'Added column time_of_day';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'events' AND column_name = 'occurs_at_utc') THEN
                    ALTER TABLE events ADD COLUMN occurs_at_utc TIMESTAMP WITH TIME ZONE;
                    RAISE NOTICE 'Added column occurs_at_utc';
                END IF;
            END $$;";
        
        await alterCommand.ExecuteNonQueryAsync();
        Log.Information("Проверка и добавление недостающих колонок в таблице events выполнены");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Ошибка при добавлении недостающих колонок в таблице events");
    }
}

static async Task EnsureAttachmentColumnsExistAsync(CalendarDbContext dbContext)
{
    try
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        
        var dropCommands = new List<string>();
        var addCommands = new List<string>();
        
        var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = @"
            SELECT column_name 
            FROM information_schema.columns 
            WHERE table_schema = 'public' 
            AND table_name = 'attachments' 
            ORDER BY column_name";
        
        var existingColumns = new List<string>();
        using (var reader = await checkCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                existingColumns.Add(reader.GetString(0));
            }
        }
        
        Log.Information("Существующие колонки в таблице attachments: {Columns}", string.Join(", ", existingColumns));
        
        if (existingColumns.Contains("file_id"))
        {
            dropCommands.Add("ALTER TABLE attachments DROP COLUMN file_id CASCADE");
        }
        if (existingColumns.Contains("file_type"))
        {
            dropCommands.Add("ALTER TABLE attachments DROP COLUMN file_type CASCADE");
        }
        if (existingColumns.Contains("file_size"))
        {
            dropCommands.Add("ALTER TABLE attachments DROP COLUMN file_size CASCADE");
        }
        if (existingColumns.Contains("created_at_utc"))
        {
            dropCommands.Add("ALTER TABLE attachments DROP COLUMN created_at_utc CASCADE");
        }
        if (existingColumns.Contains("updated_at_utc"))
        {
            dropCommands.Add("ALTER TABLE attachments DROP COLUMN updated_at_utc CASCADE");
        }
        
        if (!existingColumns.Contains("telegram_file_id"))
        {
            addCommands.Add("ALTER TABLE attachments ADD COLUMN telegram_file_id VARCHAR(200)");
        }
        if (!existingColumns.Contains("telegram_file_unique_id"))
        {
            addCommands.Add("ALTER TABLE attachments ADD COLUMN telegram_file_unique_id VARCHAR(200)");
        }
        if (!existingColumns.Contains("mime_type"))
        {
            addCommands.Add("ALTER TABLE attachments ADD COLUMN mime_type VARCHAR(100)");
        }
        if (!existingColumns.Contains("size"))
        {
            addCommands.Add("ALTER TABLE attachments ADD COLUMN size BIGINT");
        }
        if (!existingColumns.Contains("is_current"))
        {
            addCommands.Add("ALTER TABLE attachments ADD COLUMN is_current BOOLEAN NOT NULL DEFAULT true");
        }
        if (!existingColumns.Contains("uploaded_at_utc"))
        {
            addCommands.Add("ALTER TABLE attachments ADD COLUMN uploaded_at_utc TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()");
        }
        
        if (dropCommands.Any())
        {
            foreach (var dropCmd in dropCommands)
            {
                try
                {
                    var dropCommand = connection.CreateCommand();
                    dropCommand.CommandText = dropCmd;
                    Log.Information("Удаление устаревшей колонки: {Command}", dropCmd);
                    await dropCommand.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Ошибка при удалении колонки {Command}: {Message}", dropCmd, ex.Message);
                }
            }
            
            Log.Information("Завершено удаление устаревших колонок");
        }
        
        if (addCommands.Any())
        {
            var addCommand = connection.CreateCommand();
            addCommand.CommandText = string.Join("; ", addCommands) + ";";
            Log.Information("Выполнение ALTER TABLE для добавления колонок: {Statements}", addCommand.CommandText);
            await addCommand.ExecuteNonQueryAsync();
            Log.Information("Добавлено {Count} колонок в таблицу attachments", addCommands.Count);
        }
        else if (!dropCommands.Any())
        {
            Log.Information("Все необходимые колонки уже существуют в таблице attachments");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Ошибка при добавлении недостающих колонок в таблице attachments: {Message}", ex.Message);
        throw;
    }
}

static async Task EnsureNotificationRunColumnsTypeAsync(CalendarDbContext dbContext)
{
    try
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        
        var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = @"
            SELECT column_name, data_type, udt_name
            FROM information_schema.columns 
            WHERE table_schema = 'public' 
            AND table_name = 'notification_runs' 
            AND column_name IN ('period_start_local', 'period_end_local')
            ORDER BY column_name";
        
        var columnsToFix = new List<string>();
        using (var reader = await checkCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(0);
                var dataType = reader.GetString(1);
                var udtName = reader.GetString(2);
                
                Log.Information("Колонка {ColumnName}: data_type={DataType}, udt_name={UdtName}", columnName, dataType, udtName);
                
                // Проверяем, если тип timestamp with time zone, нужно изменить на timestamp without time zone
                if (udtName == "timestamptz" || dataType == "timestamp with time zone")
                {
                    columnsToFix.Add(columnName);
                }
            }
        }
        
        if (columnsToFix.Any())
        {
            Log.Information("Обнаружены колонки с неправильным типом, требуется изменение: {Columns}", string.Join(", ", columnsToFix));
            
            // Временно удаляем индекс, если он существует
            var dropIndexCommand = connection.CreateCommand();
            dropIndexCommand.CommandText = @"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_indexes WHERE tablename = 'notification_runs' AND indexname LIKE '%period%') THEN
                        DROP INDEX IF EXISTS ix_notification_runs_digest_type_period_start_local_period_end_lo CASCADE;
                    END IF;
                END $$;";
            
            try
            {
                await dropIndexCommand.ExecuteNonQueryAsync();
                Log.Information("Временный индекс удален");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Ошибка при удалении индекса (возможно, его нет)");
            }
            
            // Изменяем тип колонок
            foreach (var columnName in columnsToFix)
            {
                var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = $@"
                    ALTER TABLE notification_runs 
                    ALTER COLUMN {columnName} TYPE timestamp without time zone 
                    USING {columnName}::timestamp without time zone;";
                
                try
                {
                    await alterCommand.ExecuteNonQueryAsync();
                    Log.Information("Тип колонки {ColumnName} изменен на timestamp without time zone", columnName);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Ошибка при изменении типа колонки {ColumnName}", columnName);
                    throw;
                }
            }
            
            // Восстанавливаем индекс
            var recreateIndexCommand = connection.CreateCommand();
            recreateIndexCommand.CommandText = @"
                CREATE UNIQUE INDEX IF NOT EXISTS ix_notification_runs_digest_type_period_start_local_period_end_lo 
                ON notification_runs (digest_type, period_start_local, period_end_local, time_zone_id);";
            
            try
            {
                await recreateIndexCommand.ExecuteNonQueryAsync();
                Log.Information("Индекс восстановлен");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Ошибка при восстановлении индекса (возможно, он уже существует)");
            }
        }
        else
        {
            Log.Information("Типы колонок в notification_runs корректны");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Ошибка при проверке и исправлении типов колонок в notification_runs");
        throw;
    }
}

static async Task ForceAddAttachmentColumnsAsync(CalendarDbContext dbContext)
{
    try
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        
        var dropCommands = new[]
        {
            "ALTER TABLE attachments DROP COLUMN IF EXISTS file_id CASCADE",
            "ALTER TABLE attachments DROP COLUMN IF EXISTS file_type CASCADE",
            "ALTER TABLE attachments DROP COLUMN IF EXISTS file_size CASCADE",
            "ALTER TABLE attachments DROP COLUMN IF EXISTS created_at_utc CASCADE",
            "ALTER TABLE attachments DROP COLUMN IF EXISTS updated_at_utc CASCADE"
        };
        
        Log.Information("Принудительное удаление устаревших колонок из attachments...");
        foreach (var cmdText in dropCommands)
        {
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = cmdText;
                var rowsAffected = await command.ExecuteNonQueryAsync();
                Log.Information("Выполнено: {Command}, затронуто строк: {Rows}", cmdText, rowsAffected);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Ошибка при выполнении команды {Command}: {Message}", cmdText, ex.Message);
            }
        }
        
        var verifyCommand = connection.CreateCommand();
        verifyCommand.CommandText = @"
            SELECT column_name 
            FROM information_schema.columns 
            WHERE table_schema = 'public' 
            AND table_name = 'attachments' 
            AND column_name IN ('file_id', 'file_type', 'file_size', 'created_at_utc', 'updated_at_utc')
            ORDER BY column_name";
        
        var remainingOldColumns = new List<string>();
        using (var reader = await verifyCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                remainingOldColumns.Add(reader.GetString(0));
            }
        }
        
        if (remainingOldColumns.Any())
        {
            Log.Error("КРИТИЧЕСКАЯ ОШИБКА: Старые колонки все еще существуют после удаления: {Columns}. Попытка пересоздать таблицу...", string.Join(", ", remainingOldColumns));
            
            try
            {
                var checkDataCommand = connection.CreateCommand();
                checkDataCommand.CommandText = "SELECT COUNT(*) FROM attachments";
                var rowCount = await checkDataCommand.ExecuteScalarAsync();
                Log.Information("Количество строк в attachments перед пересозданием: {Count}", rowCount);
                
                var recreateCommand = connection.CreateCommand();
                recreateCommand.CommandText = @"
                    CREATE TABLE attachments_new (
                        id SERIAL PRIMARY KEY,
                        event_id INTEGER NOT NULL,
                        telegram_file_id VARCHAR(200) NOT NULL,
                        telegram_file_unique_id VARCHAR(200) NOT NULL,
                        file_name VARCHAR(500) NOT NULL,
                        mime_type VARCHAR(100),
                        size BIGINT,
                        version INTEGER NOT NULL,
                        is_current BOOLEAN NOT NULL,
                        uploaded_at_utc TIMESTAMP WITH TIME ZONE NOT NULL,
                        FOREIGN KEY (event_id) REFERENCES events(id) ON DELETE CASCADE
                    );
                    
                    INSERT INTO attachments_new (id, event_id, telegram_file_id, telegram_file_unique_id, file_name, mime_type, size, version, is_current, uploaded_at_utc)
                    SELECT id, event_id, 
                           COALESCE(telegram_file_id, '') as telegram_file_id,
                           COALESCE(telegram_file_unique_id, '') as telegram_file_unique_id,
                           file_name, mime_type, size, version, is_current, uploaded_at_utc
                    FROM attachments;
                    
                    DROP TABLE attachments CASCADE;
                    ALTER TABLE attachments_new RENAME TO attachments;
                    
                    CREATE INDEX IF NOT EXISTS ix_attachments_event_id_is_current ON attachments(event_id, is_current);";
                
                await recreateCommand.ExecuteNonQueryAsync();
                Log.Information("Таблица attachments пересоздана без старых колонок");
                
                var verifyAfterCommand = connection.CreateCommand();
                verifyAfterCommand.CommandText = @"
                    SELECT column_name 
                    FROM information_schema.columns 
                    WHERE table_schema = 'public' 
                    AND table_name = 'attachments' 
                    AND column_name IN ('file_id', 'file_type', 'file_size', 'created_at_utc', 'updated_at_utc')
                    ORDER BY column_name";
                
                var stillRemaining = new List<string>();
                using (var reader = await verifyAfterCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        stillRemaining.Add(reader.GetString(0));
                    }
                }
                
                if (stillRemaining.Any())
                {
                    Log.Error("КРИТИЧЕСКАЯ ОШИБКА: Старые колонки все еще существуют после пересоздания: {Columns}", string.Join(", ", stillRemaining));
                }
                else
                {
                    Log.Information("Подтверждено: все старые колонки удалены после пересоздания таблицы");
                }
            }
            catch (Exception recreateEx)
            {
                Log.Error(recreateEx, "Ошибка при пересоздании таблицы attachments: {Message}", recreateEx.Message);
                throw;
            }
        }
        else
        {
            Log.Information("Все старые колонки успешно удалены из attachments");
        }
        
        var addCommands = new[]
        {
            "ALTER TABLE attachments ADD COLUMN IF NOT EXISTS telegram_file_id VARCHAR(200)",
            "ALTER TABLE attachments ADD COLUMN IF NOT EXISTS telegram_file_unique_id VARCHAR(200)",
            "ALTER TABLE attachments ADD COLUMN IF NOT EXISTS mime_type VARCHAR(100)",
            "ALTER TABLE attachments ADD COLUMN IF NOT EXISTS size BIGINT",
            "ALTER TABLE attachments ADD COLUMN IF NOT EXISTS is_current BOOLEAN NOT NULL DEFAULT true",
            "ALTER TABLE attachments ADD COLUMN IF NOT EXISTS uploaded_at_utc TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()"
        };
        
        foreach (var cmdText in addCommands)
        {
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = cmdText;
                await command.ExecuteNonQueryAsync();
                Log.Information("Выполнено: {Command}", cmdText);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Ошибка при выполнении команды {Command}: {Message}", cmdText, ex.Message);
            }
        }
        
        Log.Information("Принудительное добавление колонок в таблицу attachments завершено");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Критическая ошибка при принудительном добавлении колонок в attachments");
    }
}

static async Task<bool> CheckTableExistsAsync(CalendarDbContext dbContext, string tableName)
{
    try
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        
        var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT EXISTS (
                SELECT FROM information_schema.tables 
                WHERE table_schema = 'public' 
                AND table_name = '{tableName}'
            )";
        
        var result = await command.ExecuteScalarAsync();
        return result is bool exists && exists;
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Ошибка при проверке существования таблицы {TableName}: {Message}", tableName, ex.Message);
        return false;
    }
}

static async Task<(bool HasConversationStates, bool HasBirthdays, bool HasEvents)> CheckTablesExistAsync(CalendarDbContext dbContext)
{
    try
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT EXISTS (
                SELECT FROM information_schema.tables 
                WHERE table_schema = 'public' 
                AND table_name = 'conversation_states'
            ) as has_conversation_states,
            EXISTS (
                SELECT FROM information_schema.tables 
                WHERE table_schema = 'public' 
                AND table_name = 'birthdays'
            ) as has_birthdays,
            EXISTS (
                SELECT FROM information_schema.tables 
                WHERE table_schema = 'public' 
                AND table_name = 'events'
            ) as has_events";
        
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return (
                reader.GetBoolean(0),
                reader.GetBoolean(1),
                reader.GetBoolean(2)
            );
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Ошибка при проверке существования таблиц");
    }
    
    return (false, false, false);
}

static bool IsRunningInDocker()
{
    return File.Exists("/.dockerenv") || 
           !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"));
}

static async Task<bool> IsPortOpenAsync(string host, int port)
{
    try
    {
        using var client = new TcpClient();
        var connectTask = client.ConnectAsync(host, port);
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(2));
        var completedTask = await Task.WhenAny(connectTask, timeoutTask);
        
        if (completedTask == connectTask && client.Connected)
        {
            return true;
        }
    }
    catch
    {
    }
    
    return false;
}

