using Serilog;
using Serilog.Events;

namespace SnakeFrogCalendarBot.Worker.Logging;

public static class SerilogSetup
{
    public static ILogger CreateLogger()
    {
        var logLevelEnv = Environment.GetEnvironmentVariable("LOG_LEVEL")?.Trim() ?? "Information";
        var logLevel = ParseLogLevel(logLevelEnv);
        
        var fileLogLevelEnv = Environment.GetEnvironmentVariable("LOG_LEVEL_FILE")?.Trim();
        var fileLogLevel = fileLogLevelEnv != null ? ParseLogLevel(fileLogLevelEnv) : LogEventLevel.Information;
        
        return new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .MinimumLevel.Override("Microsoft", logLevel)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", logLevel)
            .MinimumLevel.Override("Quartz", logLevel)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/worker-.log", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: fileLogLevel)
            .CreateLogger();
    }
    
    private static LogEventLevel ParseLogLevel(string level)
    {
        return level.ToUpperInvariant() switch
        {
            "VERBOSE" or "TRACE" => LogEventLevel.Verbose,
            "DEBUG" => LogEventLevel.Debug,
            "INFORMATION" or "INFO" => LogEventLevel.Information,
            "WARNING" or "WARN" => LogEventLevel.Warning,
            "ERROR" => LogEventLevel.Error,
            "FATAL" or "CRITICAL" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }
}
