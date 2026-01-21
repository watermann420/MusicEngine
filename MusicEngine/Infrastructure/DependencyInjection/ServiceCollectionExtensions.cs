using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MusicEngine.Core;
using MusicEngine.Infrastructure.Configuration;
using MusicEngine.Infrastructure.Logging;
using MusicEngine.Infrastructure.Memory;
using MusicEngine.Infrastructure.DependencyInjection.Interfaces;

namespace MusicEngine.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for configuring MusicEngine services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MusicEngine services to the service collection.
    /// </summary>
    public static IServiceCollection AddMusicEngine(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // Configuration
        if (configuration != null)
        {
            services.Configure<MusicEngineOptions>(configuration.GetSection(MusicEngineOptions.SectionName));
            services.AddSingleton(configuration);
        }

        // Memory pooling
        services.AddSingleton<IAudioBufferPool, AudioBufferPool>();

        // Core services - register concrete types
        services.AddSingleton<AudioEngine>(sp =>
        {
            var options = sp.GetService<Microsoft.Extensions.Options.IOptions<MusicEngineOptions>>()?.Value;
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger(LogCategories.Audio);
            return new AudioEngine(options?.Audio.SampleRate, logger);
        });

        services.AddSingleton<Sequencer>(sp =>
        {
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger(LogCategories.Sequencer);
            return new Sequencer(logger);
        });

        services.AddSingleton<VstHost>(sp =>
        {
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger(LogCategories.Vst);
            return new VstHost(logger);
        });

        // Session management
        services.AddTransient<Session>();

        return services;
    }

    /// <summary>
    /// Adds MusicEngine logging services.
    /// </summary>
    public static IServiceCollection AddMusicEngineLogging(this IServiceCollection services, LoggingOptions? options = null)
    {
        services.AddLogging(builder => builder.AddMusicEngineLogging(options));
        return services;
    }
}
