using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MusicEngine.Core;
using MusicEngine.Infrastructure.Configuration;
using MusicEngine.Infrastructure.DependencyInjection.Interfaces;

namespace MusicEngine.Infrastructure.DependencyInjection;

/// <summary>
/// Static factory for creating MusicEngine instances without DI (backward compatibility).
/// </summary>
public static class MusicEngineFactory
{
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Creates a new AudioEngine with default settings.
    /// </summary>
    public static AudioEngine CreateAudioEngine(int? sampleRate = null)
    {
        return new AudioEngine(sampleRate);
    }

    /// <summary>
    /// Creates a new Sequencer with default settings.
    /// </summary>
    public static Sequencer CreateSequencer()
    {
        return new Sequencer();
    }

    /// <summary>
    /// Creates a new Sequencer with specified timing precision.
    /// </summary>
    public static Sequencer CreateSequencer(TimingPrecision precision)
    {
        return new Sequencer(precision);
    }

    /// <summary>
    /// Creates a new VstHost.
    /// </summary>
    public static VstHost CreateVstHost()
    {
        return new VstHost();
    }

    /// <summary>
    /// Creates a new Session.
    /// </summary>
    public static Session CreateSession()
    {
        return new Session();
    }

    /// <summary>
    /// Creates a fully configured MusicEngine environment using DI.
    /// </summary>
    public static IServiceProvider CreateServiceProvider(IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();

        configuration ??= MusicEngine.Infrastructure.Configuration.ConfigurationManager.BuildConfiguration();

        services.AddMusicEngine(configuration);
        services.AddMusicEngineLogging();

        _serviceProvider = services.BuildServiceProvider();
        return _serviceProvider;
    }

    /// <summary>
    /// Gets or creates the global service provider.
    /// </summary>
    public static IServiceProvider GetServiceProvider()
    {
        return _serviceProvider ?? CreateServiceProvider();
    }

    /// <summary>
    /// Gets a service from the global service provider.
    /// </summary>
    public static T? GetService<T>() where T : class
    {
        return GetServiceProvider().GetService<T>();
    }
}
