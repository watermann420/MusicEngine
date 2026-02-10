// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core waveform types for instruments.

namespace MusicEngine.Instruments;

/// <summary>
/// Core waveform types for oscillators.
/// </summary>
public enum WaveType
{
    /// <summary>Pure sine wave.</summary>
    Sine,
    /// <summary>Square wave.</summary>
    Square,
    /// <summary>Sawtooth wave.</summary>
    Sawtooth,
    /// <summary>Triangle wave.</summary>
    Triangle,
    /// <summary>Pulse wave with adjustable width.</summary>
    Pulse,
    /// <summary>Noise source.</summary>
    Noise
}
