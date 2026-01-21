using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using MusicEngine.Infrastructure.Configuration;

namespace MusicEngine.Infrastructure.Logging;

/// <summary>
/// Configures Serilog logging for MusicEngine.
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// Creates a configured Serilog logger based on options.
    /// </summary>
    public static Serilog.ILogger CreateLogger(LoggingOptions? options = null)
    {
        options ??= new LoggingOptions();

        var logLevel = ParseLogLevel(options.MinimumLevel);

        var config = new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", "MusicEngine");

        if (options.EnableConsole)
        {
            config.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");
        }

        if (options.EnableFile)
        {
            var logPath = Path.Combine(options.LogDirectory, "musicengine-.log");
            config.WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");
        }

        return config.CreateLogger();
    }

    /// <summary>
    /// Configures Microsoft.Extensions.Logging to use Serilog.
    /// </summary>
    public static ILoggingBuilder AddMusicEngineLogging(this ILoggingBuilder builder, LoggingOptions? options = null)
    {
        var serilogLogger = CreateLogger(options);
        Log.Logger = serilogLogger;

        builder.ClearProviders();
        builder.AddSerilog(serilogLogger, dispose: true);

        return builder;
    }

    private static LogEventLevel ParseLogLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "verbose" or "trace" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" or "info" => LogEventLevel.Information,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" or "critical" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }

    /// <summary>
    /// Creates a logger for a specific category.
    /// </summary>
    public static Microsoft.Extensions.Logging.ILogger CreateCategoryLogger(ILoggerFactory factory, string category)
    {
        return factory.CreateLogger(category);
    }
}
