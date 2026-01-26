// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;


namespace MusicEngine.Core.Freeze;


/// <summary>
/// Stores the original configuration of a track before it was frozen.
/// Used to restore the track to its live state when unfreezing.
/// </summary>
public class FreezeData
{
    /// <summary>
    /// Gets or sets the unique identifier for this freeze data.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the track index this freeze data belongs to.
    /// </summary>
    public int TrackIndex { get; set; }

    /// <summary>
    /// Gets or sets the original synthesizer reference.
    /// Stored to restore the track when unfreezing.
    /// </summary>
    public ISynth? OriginalSynth { get; set; }

    /// <summary>
    /// Gets or sets the original synth type name for serialization.
    /// </summary>
    public string OriginalSynthTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original synth parameters.
    /// </summary>
    public Dictionary<string, float> OriginalSynthParameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the original effect chain configuration.
    /// </summary>
    public List<EffectChainConfig> OriginalEffectChain { get; set; } = new();

    /// <summary>
    /// Gets or sets the original pattern data for this track.
    /// </summary>
    public List<PatternConfig> OriginalPatterns { get; set; } = new();

    /// <summary>
    /// Gets or sets the path to the frozen audio file (if saved to disk).
    /// </summary>
    public string? FrozenAudioFilePath { get; set; }

    /// <summary>
    /// Gets or sets the frozen audio buffer (if kept in memory).
    /// </summary>
    public float[]? FrozenAudioBuffer { get; set; }

    /// <summary>
    /// Gets or sets the sample rate of the frozen audio.
    /// </summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>
    /// Gets or sets the number of channels in the frozen audio.
    /// </summary>
    public int Channels { get; set; } = 2;

    /// <summary>
    /// Gets or sets the duration of the frozen audio in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the track was frozen.
    /// </summary>
    public DateTime FreezeTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the BPM at which the track was frozen.
    /// </summary>
    public double FreezeBpm { get; set; } = 120.0;

    /// <summary>
    /// Gets or sets the start position in beats where the freeze begins.
    /// </summary>
    public double StartPositionBeats { get; set; }

    /// <summary>
    /// Gets or sets the end position in beats where the freeze ends.
    /// </summary>
    public double EndPositionBeats { get; set; }

    /// <summary>
    /// Gets the length of the frozen region in beats.
    /// </summary>
    public double LengthBeats => EndPositionBeats - StartPositionBeats;

    /// <summary>
    /// Gets the total sample count of the frozen audio.
    /// </summary>
    public long TotalSamples => FrozenAudioBuffer?.Length / Channels ?? 0;

    /// <summary>
    /// Creates a deep copy of this freeze data (excluding audio buffer reference).
    /// </summary>
    /// <returns>A new FreezeData instance with copied values.</returns>
    public FreezeData Clone()
    {
        return new FreezeData
        {
            Id = Guid.NewGuid(),
            TrackIndex = TrackIndex,
            OriginalSynth = OriginalSynth,
            OriginalSynthTypeName = OriginalSynthTypeName,
            OriginalSynthParameters = new Dictionary<string, float>(OriginalSynthParameters),
            OriginalEffectChain = new List<EffectChainConfig>(OriginalEffectChain),
            OriginalPatterns = new List<PatternConfig>(OriginalPatterns),
            FrozenAudioFilePath = FrozenAudioFilePath,
            FrozenAudioBuffer = FrozenAudioBuffer, // Shallow copy - same buffer reference
            SampleRate = SampleRate,
            Channels = Channels,
            DurationSeconds = DurationSeconds,
            FreezeTimestamp = FreezeTimestamp,
            FreezeBpm = FreezeBpm,
            StartPositionBeats = StartPositionBeats,
            EndPositionBeats = EndPositionBeats
        };
    }

    /// <summary>
    /// Clears the frozen audio data from memory.
    /// </summary>
    public void ClearAudioBuffer()
    {
        FrozenAudioBuffer = null;
    }
}


/// <summary>
/// Configuration data for an effect in the chain for serialization/restoration.
/// </summary>
public class EffectChainConfig
{
    /// <summary>
    /// Gets or sets the effect type name.
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the effect display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the effect is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the dry/wet mix ratio.
    /// </summary>
    public float Mix { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the effect parameters.
    /// </summary>
    public Dictionary<string, float> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the slot index in the chain.
    /// </summary>
    public int SlotIndex { get; set; }

    /// <summary>
    /// Gets or sets whether this is a VST effect.
    /// </summary>
    public bool IsVstEffect { get; set; }

    /// <summary>
    /// Gets or sets the VST plugin path (for VST effects).
    /// </summary>
    public string? VstPath { get; set; }

    /// <summary>
    /// Gets or sets the VST plugin state (for VST effects).
    /// </summary>
    public byte[]? VstState { get; set; }
}
