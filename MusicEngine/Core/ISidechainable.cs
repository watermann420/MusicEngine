// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Interface for audio effects that support sidechain input.
/// Sidechain allows an external audio signal to control the effect's behavior.
/// Common use cases include ducking (lowering music when voice comes in),
/// frequency-selective compression, and rhythmic pumping effects.
/// </summary>
public interface ISidechainable
{
    /// <summary>
    /// Gets or sets the sidechain source.
    /// When set, the effect uses this signal to control its processing
    /// instead of (or in addition to) the main input signal.
    /// </summary>
    ISampleProvider? SidechainSource { get; set; }

    /// <summary>
    /// Gets or sets whether the sidechain is enabled.
    /// When disabled, the effect processes audio normally without sidechain influence.
    /// </summary>
    bool SidechainEnabled { get; set; }

    /// <summary>
    /// Gets or sets the sidechain input gain (0.1 - 10.0).
    /// Amplifies or attenuates the sidechain signal for sensitivity control.
    /// Values greater than 1.0 make the effect more sensitive to the sidechain input.
    /// </summary>
    float SidechainGain { get; set; }

    /// <summary>
    /// Gets or sets the sidechain filter frequency in Hz.
    /// When greater than 0, applies a high-pass filter to the sidechain signal.
    /// Useful for focusing on specific frequency ranges (e.g., kick drum detection).
    /// </summary>
    float SidechainFilterFrequency { get; set; }

    /// <summary>
    /// Gets whether the sidechain source is currently connected and valid.
    /// </summary>
    bool IsSidechainConnected { get; }

    /// <summary>
    /// Connects a sidechain source with optional format validation.
    /// </summary>
    /// <param name="source">The sidechain audio source</param>
    /// <param name="validateFormat">Whether to validate that formats match</param>
    /// <exception cref="ArgumentException">Thrown when format validation fails</exception>
    void ConnectSidechain(ISampleProvider source, bool validateFormat = true);

    /// <summary>
    /// Disconnects the current sidechain source.
    /// </summary>
    void DisconnectSidechain();
}

/// <summary>
/// Provides extension methods for ISidechainable effects.
/// </summary>
public static class SidechainableExtensions
{
    /// <summary>
    /// Configures a sidechain connection with common settings.
    /// </summary>
    /// <param name="effect">The sidechainable effect</param>
    /// <param name="source">The sidechain source</param>
    /// <param name="gain">The sidechain gain (default 1.0)</param>
    /// <param name="filterFrequency">The sidechain filter frequency in Hz (default 0 = no filter)</param>
    /// <returns>The effect for method chaining</returns>
    public static T WithSidechain<T>(this T effect, ISampleProvider source, float gain = 1.0f, float filterFrequency = 0f)
        where T : ISidechainable
    {
        effect.ConnectSidechain(source);
        effect.SidechainGain = gain;
        effect.SidechainFilterFrequency = filterFrequency;
        effect.SidechainEnabled = true;
        return effect;
    }

    /// <summary>
    /// Creates a ducking configuration for voice-over-music scenarios.
    /// Applies settings optimized for ducking music when vocals come in.
    /// </summary>
    /// <param name="effect">The sidechainable effect</param>
    /// <param name="voiceSource">The voice/vocal audio source</param>
    /// <returns>The effect for method chaining</returns>
    public static T ConfigureForDucking<T>(this T effect, ISampleProvider voiceSource)
        where T : ISidechainable
    {
        effect.ConnectSidechain(voiceSource);
        effect.SidechainGain = 1.5f; // Slightly boosted sensitivity
        effect.SidechainFilterFrequency = 300f; // Focus on voice frequencies
        effect.SidechainEnabled = true;
        return effect;
    }

    /// <summary>
    /// Creates a pumping configuration for EDM-style rhythmic effects.
    /// Applies settings optimized for kick drum triggered pumping.
    /// </summary>
    /// <param name="effect">The sidechainable effect</param>
    /// <param name="kickSource">The kick drum audio source</param>
    /// <returns>The effect for method chaining</returns>
    public static T ConfigureForPumping<T>(this T effect, ISampleProvider kickSource)
        where T : ISidechainable
    {
        effect.ConnectSidechain(kickSource);
        effect.SidechainGain = 2.0f; // Strong sensitivity
        effect.SidechainFilterFrequency = 0f; // Full frequency range for kick
        effect.SidechainEnabled = true;
        return effect;
    }
}
