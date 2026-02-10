// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Modular oscillator for SimpleSynth.

using MusicEngine.Instruments.Modules;

namespace MusicEngine.Instruments;

/// <summary>
/// Modular oscillator used by SimpleSynth.
/// </summary>
public sealed class SynthOscillator
{
    /// <summary>
    /// Oscillator waveform.
    /// </summary>
    public WaveType Waveform { get; set; } = WaveType.Sine;

    /// <summary>
    /// Octave offset.
    /// </summary>
    public int Octave { get; set; } = 0;

    /// <summary>
    /// Semitone offset.
    /// </summary>
    public int Semi { get; set; } = 0;

    /// <summary>
    /// Fine tune in cents.
    /// </summary>
    public float Fine { get; set; } = 0f;

    /// <summary>
    /// Output level.
    /// </summary>
    public float Level { get; set; } = 0.5f;

    /// <summary>
    /// Pulse width (for Pulse wave).
    /// </summary>
    public float PulseWidth { get; set; } = 0.5f;

    /// <summary>
    /// Stereo pan (-1..1 typical).
    /// </summary>
    public float Pan { get; set; } = 0f;

    /// <summary>
    /// Enable or disable this oscillator.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Modulate pitch (semitones, audio-rate).
    /// </summary>
    public float ModToPitch { get; set; } = 0f;

    /// <summary>
    /// Modulate filter cutoff (audio-rate).
    /// </summary>
    public float ModToFilter { get; set; } = 0f;

    /// <summary>
    /// Modulate amplitude (audio-rate).
    /// </summary>
    public float ModToAmp { get; set; } = 0f;

    /// <summary>
    /// Modulate pulse width (audio-rate).
    /// </summary>
    public float ModToPulseWidth { get; set; } = 0f;
}
