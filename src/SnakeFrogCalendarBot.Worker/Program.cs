using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SnakeFrogCalendarBot.Application.Abstractions.Parsing;
using SnakeFrogCalendarBot.Application.Abstractions.Persistence;
using SnakeFrogCalendarBot.Application.Abstractions.Time;
using SnakeFrogCalendarBot.Application.Formatting;
using SnakeFrogCalendarBot.Application.UseCases.Birthdays;
using SnakeFrogCalendarBot.Infrastructure.Parsing;
using SnakeFrogCalendarBot.Infrastructure.Persistence;
using SnakeFrogCalendarBot.Infrastructure.Persistence.Repositories;
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
            services.AddHostedService<BotHostedService>();

            services.AddDbContext<CalendarDbContext>(db =>
                db.UseNpgsql(
                        options.PostgresConnectionString,
                        npgsql => npgsql.MigrationsAssembly(typeof(CalendarDbContext).Assembly.FullName))
                    .UseSnakeCaseNamingConvention());

            services.AddScoped<IBirthdayRepository, BirthdayRepository>();
            services.AddScoped<IConversationStateRepository, ConversationStateRepository>();

            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<IBirthdayDateParser, RuBirthdayDateParser>();
            services.AddSingleton<BirthdayListFormatter>();

            services.AddScoped<CreateBirthday>();
            services.AddScoped<ListBirthdays>();
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
