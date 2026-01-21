//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Combines TempoMap and TimeSignatureMap to provide complete timing conversions
//              between bars, beats, samples, and seconds.

using System;

namespace MusicEngine.Core;

/// <summary>
/// Represents a complete position in musical time.
/// </summary>
public readonly struct MusicalPosition : IEquatable<MusicalPosition>, IComparable<MusicalPosition>
{
    /// <summary>Gets the bar number (0-indexed).</summary>
    public int Bar { get; }

    /// <summary>Gets the beat within the bar (0-indexed, fractional).</summary>
    public double Beat { get; }

    /// <summary>Gets the position in quarter notes (beats).</summary>
    public double QuarterNotes { get; }

    /// <summary>Gets the position in seconds.</summary>
    public double Seconds { get; }

    /// <summary>Gets the position in samples.</summary>
    public long Samples { get; }

    /// <summary>Gets the time signature at this position.</summary>
    public TimeSignature TimeSignature { get; }

    /// <summary>Gets the tempo at this position.</summary>
    public double Bpm { get; }

    /// <summary>
    /// Creates a new musical position.
    /// </summary>
    internal MusicalPosition(int bar, double beat, double quarterNotes, double seconds, long samples, TimeSignature timeSignature, double bpm)
    {
        Bar = bar;
        Beat = beat;
        QuarterNotes = quarterNotes;
        Seconds = seconds;
        Samples = samples;
        TimeSignature = timeSignature;
        Bpm = bpm;
    }

    /// <summary>Gets a display string for the position (1-indexed for display).</summary>
    public string DisplayPosition => $"{Bar + 1}:{(int)Beat + 1}:{(Beat % 1.0) * 100:00}";

    public bool Equals(MusicalPosition other) => QuarterNotes.Equals(other.QuarterNotes);
    public override bool Equals(object? obj) => obj is MusicalPosition other && Equals(other);
    public override int GetHashCode() => QuarterNotes.GetHashCode();
    public int CompareTo(MusicalPosition other) => QuarterNotes.CompareTo(other.QuarterNotes);

    public static bool operator ==(MusicalPosition left, MusicalPosition right) => left.Equals(right);
    public static bool operator !=(MusicalPosition left, MusicalPosition right) => !left.Equals(right);
    public static bool operator <(MusicalPosition left, MusicalPosition right) => left.CompareTo(right) < 0;
    public static bool operator <=(MusicalPosition left, MusicalPosition right) => left.CompareTo(right) <= 0;
    public static bool operator >(MusicalPosition left, MusicalPosition right) => left.CompareTo(right) > 0;
    public static bool operator >=(MusicalPosition left, MusicalPosition right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"Bar {Bar + 1}, Beat {Beat + 1:F2} ({QuarterNotes:F3} QN, {Seconds:F3}s)";
}

/// <summary>
/// Combines TempoMap and TimeSignatureMap to provide complete timing functionality.
/// Handles conversions between bars, beats (quarter notes), samples, and seconds.
/// Thread-safe for concurrent access.
/// </summary>
public sealed class TempoTrack
{
    private readonly object _lock = new();
    private readonly TempoMap _tempoMap;
    private readonly TimeSignatureMap _timeSignatureMap;
    private int _sampleRate;

    /// <summary>
    /// Gets the tempo map for this track.
    /// </summary>
    public TempoMap TempoMap => _tempoMap;

    /// <summary>
    /// Gets the time signature map for this track.
    /// </summary>
    public TimeSignatureMap TimeSignatureMap => _timeSignatureMap;

    /// <summary>
    /// Gets or sets the sample rate used for sample conversions.
    /// </summary>
    public int SampleRate
    {
        get
        {
            lock (_lock)
            {
                return _sampleRate;
            }
        }
        set
        {
            lock (_lock)
            {
                _sampleRate = Math.Clamp(value, 8000, 384000);
            }
        }
    }

    /// <summary>
    /// Gets or sets the default BPM (delegates to TempoMap).
    /// </summary>
    public double DefaultBpm
    {
        get => _tempoMap.DefaultBpm;
        set => _tempoMap.DefaultBpm = value;
    }

    /// <summary>
    /// Gets or sets the default time signature (delegates to TimeSignatureMap).
    /// </summary>
    public TimeSignature DefaultTimeSignature
    {
        get => _timeSignatureMap.DefaultTimeSignature;
        set => _timeSignatureMap.DefaultTimeSignature = value;
    }

    /// <summary>
    /// Creates a new TempoTrack with default settings (120 BPM, 4/4 time, 44100 Hz).
    /// </summary>
    public TempoTrack() : this(120.0, TimeSignature.Common, Settings.SampleRate)
    {
    }

    /// <summary>
    /// Creates a new TempoTrack with specified defaults.
    /// </summary>
    /// <param name="defaultBpm">The default tempo in BPM.</param>
    /// <param name="defaultTimeSignature">The default time signature.</param>
    /// <param name="sampleRate">The sample rate in Hz.</param>
    public TempoTrack(double defaultBpm, TimeSignature defaultTimeSignature, int sampleRate)
    {
        _tempoMap = new TempoMap(defaultBpm);
        _timeSignatureMap = new TimeSignatureMap(defaultTimeSignature);
        _sampleRate = Math.Clamp(sampleRate, 8000, 384000);
    }

    /// <summary>
    /// Creates a new TempoTrack with existing TempoMap and TimeSignatureMap.
    /// </summary>
    /// <param name="tempoMap">The tempo map to use.</param>
    /// <param name="timeSignatureMap">The time signature map to use.</param>
    /// <param name="sampleRate">The sample rate in Hz.</param>
    public TempoTrack(TempoMap tempoMap, TimeSignatureMap timeSignatureMap, int sampleRate = 44100)
    {
        _tempoMap = tempoMap ?? throw new ArgumentNullException(nameof(tempoMap));
        _timeSignatureMap = timeSignatureMap ?? throw new ArgumentNullException(nameof(timeSignatureMap));
        _sampleRate = Math.Clamp(sampleRate, 8000, 384000);
    }

    #region Tempo Changes

    /// <summary>
    /// Adds a tempo change at the specified position in beats (quarter notes).
    /// </summary>
    /// <param name="positionBeats">The position in quarter notes.</param>
    /// <param name="bpm">The tempo in BPM.</param>
    /// <returns>The created tempo change.</returns>
    public TempoChange AddTempoChange(double positionBeats, double bpm)
    {
        return _tempoMap.AddTempoChange(positionBeats, bpm);
    }

    /// <summary>
    /// Adds a tempo change with ramping at the specified position.
    /// </summary>
    /// <param name="positionBeats">The position in quarter notes.</param>
    /// <param name="bpm">The tempo in BPM.</param>
    /// <param name="isRamp">Whether to ramp to the next tempo.</param>
    /// <param name="rampCurve">The ramp curve (0 = linear).</param>
    /// <returns>The created tempo change.</returns>
    public TempoChange AddTempoChange(double positionBeats, double bpm, bool isRamp, double rampCurve = 0.0)
    {
        return _tempoMap.AddTempoChange(positionBeats, bpm, isRamp, rampCurve);
    }

    /// <summary>
    /// Adds a tempo ramp between two positions.
    /// </summary>
    public void AddTempoRamp(double startBeats, double endBeats, double startBpm, double endBpm, double curve = 0.0)
    {
        _tempoMap.AddTempoRamp(startBeats, endBeats, startBpm, endBpm, curve);
    }

    /// <summary>
    /// Gets the tempo at the specified position in beats.
    /// </summary>
    /// <param name="positionBeats">The position in quarter notes.</param>
    /// <returns>The tempo in BPM.</returns>
    public double GetTempoAt(double positionBeats)
    {
        return _tempoMap.GetTempoAt(positionBeats);
    }

    #endregion

    #region Time Signature Changes

    /// <summary>
    /// Adds a time signature change at the specified bar.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="timeSignature">The time signature.</param>
    /// <returns>The created time signature change.</returns>
    public TimeSignatureChange AddTimeSignatureChange(int bar, TimeSignature timeSignature)
    {
        return _timeSignatureMap.AddTimeSignatureChange(bar, timeSignature);
    }

    /// <summary>
    /// Adds a time signature change at the specified bar using numerator and denominator.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="numerator">The time signature numerator.</param>
    /// <param name="denominator">The time signature denominator.</param>
    /// <returns>The created time signature change.</returns>
    public TimeSignatureChange AddTimeSignatureChange(int bar, int numerator, int denominator)
    {
        return _timeSignatureMap.AddTimeSignatureChange(bar, numerator, denominator);
    }

    /// <summary>
    /// Gets the time signature at the specified bar.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <returns>The time signature.</returns>
    public TimeSignature GetTimeSignatureAt(int bar)
    {
        return _timeSignatureMap.GetTimeSignatureAt(bar);
    }

    /// <summary>
    /// Gets the time signature at the specified position in beats.
    /// </summary>
    /// <param name="positionBeats">The position in quarter notes.</param>
    /// <returns>The time signature.</returns>
    public TimeSignature GetTimeSignatureAtBeats(double positionBeats)
    {
        return _timeSignatureMap.GetTimeSignatureAtQuarterNotes(positionBeats);
    }

    #endregion

    #region Position Conversions: Bars <-> Beats

    /// <summary>
    /// Converts a bar number to a position in beats (quarter notes).
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <returns>The position in quarter notes.</returns>
    public double BarsToBeats(int bar)
    {
        return _timeSignatureMap.BarToQuarterNotes(bar);
    }

    /// <summary>
    /// Converts a bar and beat position to beats (quarter notes).
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="beat">The beat within the bar (0-indexed).</param>
    /// <returns>The position in quarter notes.</returns>
    public double BarBeatToBeats(int bar, double beat)
    {
        return _timeSignatureMap.BarBeatToQuarterNotes(bar, beat);
    }

    /// <summary>
    /// Converts a position in beats to bar number.
    /// </summary>
    /// <param name="beats">The position in quarter notes.</param>
    /// <returns>The bar number (0-indexed).</returns>
    public int BeatsToBar(double beats)
    {
        return _timeSignatureMap.QuarterNotesToBar(beats);
    }

    /// <summary>
    /// Converts a position in beats to bar and beat.
    /// </summary>
    /// <param name="beats">The position in quarter notes.</param>
    /// <returns>A tuple of (bar, beat).</returns>
    public (int Bar, double Beat) BeatsToBarBeat(double beats)
    {
        return _timeSignatureMap.QuarterNotesToBarBeat(beats);
    }

    #endregion

    #region Position Conversions: Beats <-> Seconds

    /// <summary>
    /// Converts a position in beats to seconds.
    /// </summary>
    /// <param name="beats">The position in quarter notes.</param>
    /// <returns>The time in seconds.</returns>
    public double BeatsToSeconds(double beats)
    {
        return _tempoMap.BeatsToSeconds(beats);
    }

    /// <summary>
    /// Converts a time in seconds to beats.
    /// </summary>
    /// <param name="seconds">The time in seconds.</param>
    /// <returns>The position in quarter notes.</returns>
    public double SecondsToBeats(double seconds)
    {
        return _tempoMap.SecondsToBeats(seconds);
    }

    #endregion

    #region Position Conversions: Beats <-> Samples

    /// <summary>
    /// Converts a position in beats to samples.
    /// </summary>
    /// <param name="beats">The position in quarter notes.</param>
    /// <returns>The position in samples.</returns>
    public long BeatsToSamples(double beats)
    {
        double seconds = BeatsToSeconds(beats);
        return (long)(seconds * _sampleRate);
    }

    /// <summary>
    /// Converts a position in samples to beats.
    /// </summary>
    /// <param name="samples">The position in samples.</param>
    /// <returns>The position in quarter notes.</returns>
    public double SamplesToBeats(long samples)
    {
        double seconds = (double)samples / _sampleRate;
        return SecondsToBeats(seconds);
    }

    #endregion

    #region Position Conversions: Bars <-> Seconds

    /// <summary>
    /// Converts a bar number to seconds.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <returns>The time in seconds.</returns>
    public double BarsToSeconds(int bar)
    {
        double beats = BarsToBeats(bar);
        return BeatsToSeconds(beats);
    }

    /// <summary>
    /// Converts a time in seconds to bar number.
    /// </summary>
    /// <param name="seconds">The time in seconds.</param>
    /// <returns>The bar number (0-indexed).</returns>
    public int SecondsToBar(double seconds)
    {
        double beats = SecondsToBeats(seconds);
        return BeatsToBar(beats);
    }

    /// <summary>
    /// Converts a bar and beat position to seconds.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="beat">The beat within the bar.</param>
    /// <returns>The time in seconds.</returns>
    public double BarBeatToSeconds(int bar, double beat)
    {
        double beats = BarBeatToBeats(bar, beat);
        return BeatsToSeconds(beats);
    }

    /// <summary>
    /// Converts a time in seconds to bar and beat.
    /// </summary>
    /// <param name="seconds">The time in seconds.</param>
    /// <returns>A tuple of (bar, beat).</returns>
    public (int Bar, double Beat) SecondsToBarBeat(double seconds)
    {
        double beats = SecondsToBeats(seconds);
        return BeatsToBarBeat(beats);
    }

    #endregion

    #region Position Conversions: Bars <-> Samples

    /// <summary>
    /// Converts a bar number to samples.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <returns>The position in samples.</returns>
    public long BarsToSamples(int bar)
    {
        double seconds = BarsToSeconds(bar);
        return (long)(seconds * _sampleRate);
    }

    /// <summary>
    /// Converts a position in samples to bar number.
    /// </summary>
    /// <param name="samples">The position in samples.</param>
    /// <returns>The bar number (0-indexed).</returns>
    public int SamplesToBar(long samples)
    {
        double seconds = (double)samples / _sampleRate;
        return SecondsToBar(seconds);
    }

    /// <summary>
    /// Converts a bar and beat position to samples.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="beat">The beat within the bar.</param>
    /// <returns>The position in samples.</returns>
    public long BarBeatToSamples(int bar, double beat)
    {
        double seconds = BarBeatToSeconds(bar, beat);
        return (long)(seconds * _sampleRate);
    }

    /// <summary>
    /// Converts a position in samples to bar and beat.
    /// </summary>
    /// <param name="samples">The position in samples.</param>
    /// <returns>A tuple of (bar, beat).</returns>
    public (int Bar, double Beat) SamplesToBarBeat(long samples)
    {
        double seconds = (double)samples / _sampleRate;
        return SecondsToBarBeat(seconds);
    }

    #endregion

    #region Position Conversions: Seconds <-> Samples

    /// <summary>
    /// Converts seconds to samples.
    /// </summary>
    /// <param name="seconds">The time in seconds.</param>
    /// <returns>The position in samples.</returns>
    public long SecondsToSamples(double seconds)
    {
        return (long)(seconds * _sampleRate);
    }

    /// <summary>
    /// Converts samples to seconds.
    /// </summary>
    /// <param name="samples">The position in samples.</param>
    /// <returns>The time in seconds.</returns>
    public double SamplesToSeconds(long samples)
    {
        return (double)samples / _sampleRate;
    }

    #endregion

    #region Full Position Information

    /// <summary>
    /// Gets complete position information from beats (quarter notes).
    /// </summary>
    /// <param name="beats">The position in quarter notes.</param>
    /// <returns>Complete musical position information.</returns>
    public MusicalPosition GetPositionFromBeats(double beats)
    {
        var (bar, beat) = BeatsToBarBeat(beats);
        double seconds = BeatsToSeconds(beats);
        long samples = (long)(seconds * _sampleRate);
        var timeSignature = GetTimeSignatureAt(bar);
        double bpm = GetTempoAt(beats);

        return new MusicalPosition(bar, beat, beats, seconds, samples, timeSignature, bpm);
    }

    /// <summary>
    /// Gets complete position information from seconds.
    /// </summary>
    /// <param name="seconds">The time in seconds.</param>
    /// <returns>Complete musical position information.</returns>
    public MusicalPosition GetPositionFromSeconds(double seconds)
    {
        double beats = SecondsToBeats(seconds);
        return GetPositionFromBeats(beats);
    }

    /// <summary>
    /// Gets complete position information from samples.
    /// </summary>
    /// <param name="samples">The position in samples.</param>
    /// <returns>Complete musical position information.</returns>
    public MusicalPosition GetPositionFromSamples(long samples)
    {
        double seconds = (double)samples / _sampleRate;
        return GetPositionFromSeconds(seconds);
    }

    /// <summary>
    /// Gets complete position information from bar and beat.
    /// </summary>
    /// <param name="bar">The bar number (0-indexed).</param>
    /// <param name="beat">The beat within the bar.</param>
    /// <returns>Complete musical position information.</returns>
    public MusicalPosition GetPositionFromBarBeat(int bar, double beat)
    {
        double beats = BarBeatToBeats(bar, beat);
        return GetPositionFromBeats(beats);
    }

    /// <summary>
    /// Gets beat information at the specified position.
    /// </summary>
    /// <param name="beats">The position in quarter notes.</param>
    /// <returns>Beat information including accent and downbeat status.</returns>
    public BeatInfo GetBeatInfo(double beats)
    {
        return _timeSignatureMap.GetBeatInfo(beats);
    }

    #endregion

    #region Duration Conversions

    /// <summary>
    /// Converts a duration in beats to seconds at a specific position.
    /// </summary>
    /// <param name="startBeats">The start position in quarter notes.</param>
    /// <param name="durationBeats">The duration in quarter notes.</param>
    /// <returns>The duration in seconds.</returns>
    public double DurationBeatsToSeconds(double startBeats, double durationBeats)
    {
        double startSeconds = BeatsToSeconds(startBeats);
        double endSeconds = BeatsToSeconds(startBeats + durationBeats);
        return endSeconds - startSeconds;
    }

    /// <summary>
    /// Converts a duration in seconds to beats at a specific position.
    /// </summary>
    /// <param name="startSeconds">The start time in seconds.</param>
    /// <param name="durationSeconds">The duration in seconds.</param>
    /// <returns>The duration in beats.</returns>
    public double DurationSecondsToBeats(double startSeconds, double durationSeconds)
    {
        double startBeats = SecondsToBeats(startSeconds);
        double endBeats = SecondsToBeats(startSeconds + durationSeconds);
        return endBeats - startBeats;
    }

    /// <summary>
    /// Converts a duration in beats to samples at a specific position.
    /// </summary>
    /// <param name="startBeats">The start position in quarter notes.</param>
    /// <param name="durationBeats">The duration in quarter notes.</param>
    /// <returns>The duration in samples.</returns>
    public long DurationBeatsToSamples(double startBeats, double durationBeats)
    {
        double durationSeconds = DurationBeatsToSeconds(startBeats, durationBeats);
        return (long)(durationSeconds * _sampleRate);
    }

    /// <summary>
    /// Converts a duration in samples to beats at a specific position.
    /// </summary>
    /// <param name="startSamples">The start position in samples.</param>
    /// <param name="durationSamples">The duration in samples.</param>
    /// <returns>The duration in beats.</returns>
    public double DurationSamplesToBeats(long startSamples, long durationSamples)
    {
        double startSeconds = (double)startSamples / _sampleRate;
        double durationSeconds = (double)durationSamples / _sampleRate;
        return DurationSecondsToBeats(startSeconds, durationSeconds);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Clears all tempo and time signature changes.
    /// </summary>
    public void Clear()
    {
        _tempoMap.Clear();
        _timeSignatureMap.Clear();
    }

    /// <summary>
    /// Creates a simple TempoTrack with constant tempo and time signature.
    /// </summary>
    /// <param name="bpm">The tempo in BPM.</param>
    /// <param name="timeSignature">The time signature.</param>
    /// <param name="sampleRate">The sample rate.</param>
    /// <returns>A new TempoTrack.</returns>
    public static TempoTrack CreateSimple(double bpm, TimeSignature timeSignature, int sampleRate = 44100)
    {
        return new TempoTrack(bpm, timeSignature, sampleRate);
    }

    /// <summary>
    /// Creates a TempoTrack with common 4/4 time and specified BPM.
    /// </summary>
    /// <param name="bpm">The tempo in BPM.</param>
    /// <param name="sampleRate">The sample rate.</param>
    /// <returns>A new TempoTrack.</returns>
    public static TempoTrack CreateCommon(double bpm = 120.0, int sampleRate = 44100)
    {
        return new TempoTrack(bpm, TimeSignature.Common, sampleRate);
    }

    public override string ToString()
    {
        return $"TempoTrack: {_tempoMap.DefaultBpm:F1} BPM, {_timeSignatureMap.DefaultTimeSignature}, {_sampleRate} Hz";
    }

    #endregion
}
