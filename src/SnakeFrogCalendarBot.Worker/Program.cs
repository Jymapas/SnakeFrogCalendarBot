using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

Log.Logger = SerilogSetup.CreateLogger();

try
{
    var builder = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            var options = AppOptions.FromConfiguration(context.Configuration);
            services.AddSingleton(options);

            services.AddSingleton<AccessGuard>();
            services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(options.TelegramBotToken));
            services.AddSingleton<UpdateDispatcher>();
            services.AddScoped<CommandHandlers>();
            services.AddScoped<MessageHandlers>();
            services.AddScoped<CallbackHandlers>();
            services.AddHostedService<BotHostedService>();

            services.AddDbContext<CalendarDbContext>(db =>
                db.UseNpgsql(
                        options.PostgresConnectionString,
                        npgsql => npgsql.MigrationsAssembly(typeof(CalendarDbContext).Assembly.FullName))
                    .UseSnakeCaseNamingConvention());

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
            services.AddScoped<CreateEvent>();
            services.AddScoped<ListUpcomingItems>();
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
                    .WithCronSchedule("0 0 9 ? * *"));

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
        await dbContext.Database.MigrateAsync();
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
