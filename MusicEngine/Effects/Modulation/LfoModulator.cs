// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Simple LFO modulator for parameter automation.

using System;

namespace MusicEngine.Effects.Modulation;

/// <summary>
/// Simple LFO modulator for parameter automation.
/// </summary>
public sealed class LfoModulator
{
    public float Min { get; set; } = 0f;
    public float Max { get; set; } = 1f;
    public float RateHz { get; set; } = 2f;

    private double _phase;

    /// <summary>
    /// Advance the LFO and return the next value.
    /// </summary>
    public float NextValue(double deltaSeconds)
    {
        if (RateHz <= 0) return Min;
        _phase += deltaSeconds * RateHz * Math.PI * 2.0;
        if (_phase >= Math.PI * 2.0) _phase -= Math.PI * 2.0;
        float t = (float)(Math.Sin(_phase) * 0.5 + 0.5);
        return Min + t * (Max - Min);
    }
}
