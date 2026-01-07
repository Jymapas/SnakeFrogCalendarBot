using Serilog;

namespace SnakeFrogCalendarBot.Worker.Logging;

public static class SerilogSetup
{
    public static ILogger CreateLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/worker-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }
}
