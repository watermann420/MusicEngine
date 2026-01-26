// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace MusicEngine.Infrastructure.Configuration;

/// <summary>
/// Manages configuration loading and hot-reload functionality.
/// </summary>
public class ConfigurationManager : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IDisposable? _changeToken;
    private MusicEngineOptions _currentOptions;

    /// <summary>
    /// Occurs when the configuration has been reloaded.
    /// </summary>
    public event EventHandler<MusicEngineOptions>? ConfigurationChanged;

    /// <summary>
    /// Gets the current configuration options.
    /// </summary>
    public MusicEngineOptions CurrentOptions => _currentOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationManager"/> class.
    /// </summary>
    /// <param name="configuration">The configuration instance to manage.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
    public ConfigurationManager(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _currentOptions = LoadOptions();

        // Setup hot-reload if supported
        _changeToken = ChangeToken.OnChange(
            () => _configuration.GetReloadToken(),
            OnConfigurationChanged);
    }

    /// <summary>
    /// Loads the configuration options from the configuration source.
    /// </summary>
    /// <returns>The loaded <see cref="MusicEngineOptions"/>.</returns>
    private MusicEngineOptions LoadOptions()
    {
        var options = new MusicEngineOptions();
        _configuration.GetSection(MusicEngineOptions.SectionName).Bind(options);
        return options;
    }

    /// <summary>
    /// Handles configuration change events by reloading options and raising the ConfigurationChanged event.
    /// </summary>
    private void OnConfigurationChanged()
    {
        _currentOptions = LoadOptions();
        ConfigurationChanged?.Invoke(this, _currentOptions);
    }

    /// <summary>
    /// Builds a configuration instance with standard configuration sources.
    /// </summary>
    /// <param name="basePath">Optional base path for configuration files.</param>
    /// <returns>The built <see cref="IConfiguration"/> instance.</returns>
    public static IConfiguration BuildConfiguration(string? basePath = null)
    {
        var builder = new ConfigurationBuilder();

        if (!string.IsNullOrEmpty(basePath))
        {
            builder.SetBasePath(basePath);
        }

        builder
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("MUSICENGINE_");

        return builder.Build();
    }

    /// <summary>
    /// Disposes of the configuration manager and releases any resources.
    /// </summary>
    public void Dispose()
    {
        _changeToken?.Dispose();
    }
}
