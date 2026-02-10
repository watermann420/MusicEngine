// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Waveform generation helpers for synth modules.

using System;

namespace MusicEngine.Instruments.Modules;

/// <summary>
/// Waveform generation helpers for synth modules.
/// </summary>
public static class WaveformGenerator
{
    /// <summary>
    /// Basic waveform generation without band-limiting.
    /// </summary>
    /// <param name="type">Waveform type.</param>
    /// <param name="phase">Normalized phase in [0, 1].</param>
    /// <param name="pulseWidth">Pulse width for pulse wave.</param>
    /// <returns>Sample value.</returns>
    public static float Basic(WaveType type, float phase, float pulseWidth)
    {
        return type switch
        {
            WaveType.Sine => (float)Math.Sin(phase * Math.PI * 2),
            WaveType.Square => phase < 0.5f ? 1f : -1f,
            WaveType.Sawtooth => 2f * phase - 1f,
            WaveType.Triangle => phase < 0.5f ? 4f * phase - 1f : 3f - 4f * phase,
            WaveType.Pulse => phase < pulseWidth ? 1f : -1f,
            WaveType.Noise => 0f,
            _ => 0f
        };
    }

    /// <summary>
    /// Band-limited oscillator using PolyBLEP for selected waveforms.
    /// </summary>
    /// <param name="type">Waveform type.</param>
    /// <param name="phase">Normalized phase in [0, 1].</param>
    /// <param name="pulseWidth">Pulse width for pulse wave.</param>
    /// <param name="random">Random source for noise.</param>
    /// <param name="phaseInc">Phase increment per sample.</param>
    /// <returns>Sample value.</returns>
    public static float Oscillator(WaveType type, float phase, float pulseWidth, Random random, float phaseInc)
    {
        float dt = phaseInc;

        return type switch
        {
            WaveType.Sine => (float)Math.Sin(phase * Math.PI * 2),
            WaveType.Sawtooth => 2f * phase - 1f - PolyBlep(phase, dt),
            WaveType.Square => (phase < 0.5f ? 0.9f : -0.9f) + PolyBlep(phase, dt) - PolyBlep((phase + 0.5f) % 1f, dt),
            WaveType.Pulse => (phase < pulseWidth ? 0.9f : -0.9f) + PolyBlep(phase, dt) - PolyBlep((phase + (1f - pulseWidth)) % 1f, dt),
            WaveType.Triangle => phase < 0.5f ? 4f * phase - 1f : 3f - 4f * phase,
            WaveType.Noise => (float)random.NextDouble() * 2f - 1f,
            _ => 0f
        };
    }

    /// <summary>
    /// PolyBLEP correction term for band-limiting.
    /// </summary>
    /// <param name="t">Normalized phase.</param>
    /// <param name="dt">Phase increment.</param>
    /// <returns>Correction term.</returns>
    public static float PolyBlep(float t, float dt)
    {
        if (t < dt)
        {
            t /= dt;
            return t + t - t * t - 1f;
        }
        if (t > 1f - dt)
        {
            t = (t - 1f) / dt;
            return t * t + t + t + 1f;
        }
        return 0f;
    }
}
