using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace SnakeFrogCalendarBot.Infrastructure.Persistence;

public sealed class CalendarDbContextFactory : IDesignTimeDbContextFactory<CalendarDbContext>
{
    public CalendarDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=snakefrogcalendarbot;Username=snakefrog;Password=snakefrog";

        var optionsBuilder = new DbContextOptionsBuilder<CalendarDbContext>();
        optionsBuilder.UseNpgsql(
                connectionString,
                options => options.MigrationsAssembly(typeof(CalendarDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention();

        return new CalendarDbContext(optionsBuilder.Options);
    }
}
