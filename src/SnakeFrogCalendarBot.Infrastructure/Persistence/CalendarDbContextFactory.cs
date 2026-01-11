using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Npgsql.EntityFrameworkCore.PostgreSQL.Extensions;

namespace SnakeFrogCalendarBot.Infrastructure.Persistence;

public sealed class CalendarDbContextFactory : IDesignTimeDbContextFactory<CalendarDbContext>
{
    public CalendarDbContext CreateDbContext(string[] args)
    {
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var portRaw = Environment.GetEnvironmentVariable("POSTGRES_PORT");
        var port = 5432;
        if (!string.IsNullOrWhiteSpace(portRaw) && int.TryParse(portRaw, out var parsedPort))
        {
            port = parsedPort;
        }

        var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "snakefrogcalendarbot";
        var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "snakefrog";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "snakefrog";

        var connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";

        var optionsBuilder = new DbContextOptionsBuilder<CalendarDbContext>();
        optionsBuilder.UseNpgsql(
                connectionString,
                options => options.MigrationsAssembly(typeof(CalendarDbContext).Assembly.FullName));

        return new CalendarDbContext(optionsBuilder.Options);
    }
}
