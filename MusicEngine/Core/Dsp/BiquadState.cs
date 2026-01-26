// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Description: Biquad filter state structure for SIMD-optimized DSP processing.

using System.Runtime.InteropServices;

namespace MusicEngine.Core.Dsp;

/// <summary>
/// Represents the internal state of a biquad filter (Direct Form II Transposed).
/// Used with <see cref="SimdDsp.ApplyBiquad"/> for SIMD-optimized filtering.
/// </summary>
/// <remarks>
/// The biquad filter state maintains two delay line values (z1, z2) that represent
/// the filter's memory. These values persist between processing calls to maintain
/// filter continuity.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct BiquadState
{
    /// <summary>
    /// First delay line value (z^-1 state).
    /// </summary>
    public float Z1;

    /// <summary>
    /// Second delay line value (z^-2 state).
    /// </summary>
    public float Z2;

    /// <summary>
    /// Creates a new biquad state with zeroed delay lines.
    /// </summary>
    public static BiquadState Create() => new() { Z1 = 0f, Z2 = 0f };

    /// <summary>
    /// Resets the filter state to zero (clears delay lines).
    /// Call this when starting a new audio stream or to prevent clicks.
    /// </summary>
    public void Reset()
    {
        Z1 = 0f;
        Z2 = 0f;
    }

    /// <summary>
    /// Checks if the state values are valid (not NaN or Infinity).
    /// </summary>
    public readonly bool IsValid =>
        !float.IsNaN(Z1) && !float.IsInfinity(Z1) &&
        !float.IsNaN(Z2) && !float.IsInfinity(Z2);
}

/// <summary>
/// Array of biquad states for multi-channel processing.
/// </summary>
public sealed class BiquadStateArray
{
    private readonly BiquadState[] _states;

    /// <summary>
    /// Creates a new array of biquad states for the specified number of channels.
    /// </summary>
    /// <param name="channels">Number of audio channels (typically 1 for mono, 2 for stereo).</param>
    public BiquadStateArray(int channels)
    {
        if (channels <= 0)
            throw new ArgumentOutOfRangeException(nameof(channels), "Channel count must be positive.");

        _states = new BiquadState[channels];
        Reset();
    }

    /// <summary>
    /// Gets or sets the state for the specified channel.
    /// </summary>
    public ref BiquadState this[int channel] => ref _states[channel];

    /// <summary>
    /// Gets the number of channels.
    /// </summary>
    public int Channels => _states.Length;

    /// <summary>
    /// Resets all channel states to zero.
    /// </summary>
    public void Reset()
    {
        for (int i = 0; i < _states.Length; i++)
        {
            _states[i].Reset();
        }
    }

    /// <summary>
    /// Gets the underlying array for direct access.
    /// </summary>
    internal BiquadState[] GetArray() => _states;
}
