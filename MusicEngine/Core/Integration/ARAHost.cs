// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: External integration component.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MusicEngine.Core.Integration;

/// <summary>
/// ARA plugin role capabilities.
/// </summary>
[Flags]
public enum ARAPluginRole
{
    /// <summary>No specific role.</summary>
    None = 0,

    /// <summary>Plugin provides playback rendering.</summary>
    PlaybackRenderer = 1 << 0,

    /// <summary>Plugin provides editor rendering.</summary>
    EditorRenderer = 1 << 1,

    /// <summary>Plugin provides editor view.</summary>
    EditorView = 1 << 2,

    /// <summary>Plugin provides content analysis.</summary>
    ContentAnalyzer = 1 << 3
}

/// <summary>
/// ARA content types for analysis results.
/// </summary>
[Flags]
public enum ARAContentType
{
    /// <summary>No content available.</summary>
    None = 0,

    /// <summary>Note content (pitch, timing).</summary>
    Notes = 1 << 0,

    /// <summary>Tempo content.</summary>
    Tempo = 1 << 1,

    /// <summary>Time signature content.</summary>
    TimeSignature = 1 << 2,

    /// <summary>Key signature content.</summary>
    KeySignature = 1 << 3,

    /// <summary>Chord content.</summary>
    Chords = 1 << 4,

    /// <summary>Transient content.</summary>
    Transients = 1 << 5,

    /// <summary>Formant content.</summary>
    Formants = 1 << 6,

    /// <summary>Harmonic content.</summary>
    Harmonics = 1 << 7
}

/// <summary>
/// ARA playback region properties.
/// </summary>
public class ARAPlaybackRegion
{
    /// <summary>Unique identifier for this region.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Name of the region.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Start time in the arrangement (seconds).</summary>
    public double StartTimeInArrangement { get; set; }

    /// <summary>Duration of the region (seconds).</summary>
    public double Duration { get; set; }

    /// <summary>Start time in the audio source (seconds).</summary>
    public double StartTimeInSource { get; set; }

    /// <summary>Reference to the audio source.</summary>
    public ARAAudioSource? AudioSource { get; set; }

    /// <summary>Whether the region is currently enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Time stretch factor (1.0 = no stretch).</summary>
    public double TimeStretchFactor { get; set; } = 1.0;

    /// <summary>Pitch shift in semitones.</summary>
    public double PitchShiftSemitones { get; set; } = 0.0;

    /// <summary>Whether formant preservation is enabled during pitch shift.</summary>
    public bool PreserveFormants { get; set; } = true;
}

/// <summary>
/// ARA audio source representation.
/// </summary>
public class ARAAudioSource
{
    /// <summary>Unique identifier for this audio source.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Name of the audio source.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>File path to the audio file.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Sample rate of the audio.</summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>Number of channels.</summary>
    public int ChannelCount { get; set; } = 2;

    /// <summary>Total duration in seconds.</summary>
    public double Duration { get; set; }

    /// <summary>Total sample count.</summary>
    public long SampleCount { get; set; }

    /// <summary>Content types available after analysis.</summary>
    public ARAContentType AvailableContent { get; set; } = ARAContentType.None;

    /// <summary>Whether analysis is complete.</summary>
    public bool IsAnalysisComplete { get; set; }
}

/// <summary>
/// ARA document controller managing the ARA session.
/// </summary>
public class ARADocumentController
{
    /// <summary>Unique identifier for this document.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Name of the document.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Sample rate for the document.</summary>
    public int SampleRate { get; set; } = 44100;
}

/// <summary>
/// Represents detected note content from ARA analysis.
/// </summary>
public class ARANoteContent
{
    /// <summary>Start time in seconds.</summary>
    public double StartTime { get; set; }

    /// <summary>Duration in seconds.</summary>
    public double Duration { get; set; }

    /// <summary>MIDI note number (0-127).</summary>
    public int NoteNumber { get; set; }

    /// <summary>Frequency in Hz.</summary>
    public double Frequency { get; set; }

    /// <summary>Velocity (0-127).</summary>
    public int Velocity { get; set; } = 100;

    /// <summary>Confidence level (0.0-1.0).</summary>
    public double Confidence { get; set; } = 1.0;
}

/// <summary>
/// Represents detected tempo content from ARA analysis.
/// </summary>
public class ARATempoContent
{
    /// <summary>Time position in seconds.</summary>
    public double Time { get; set; }

    /// <summary>Tempo in BPM.</summary>
    public double Bpm { get; set; }

    /// <summary>Confidence level (0.0-1.0).</summary>
    public double Confidence { get; set; } = 1.0;
}

/// <summary>
/// Represents detected chord content from ARA analysis.
/// </summary>
public class ARAChordContent
{
    /// <summary>Start time in seconds.</summary>
    public double StartTime { get; set; }

    /// <summary>Duration in seconds.</summary>
    public double Duration { get; set; }

    /// <summary>Root note (0-11, C=0).</summary>
    public int RootNote { get; set; }

    /// <summary>Chord quality (major, minor, etc.).</summary>
    public string Quality { get; set; } = "major";

    /// <summary>Bass note if different from root.</summary>
    public int? BassNote { get; set; }

    /// <summary>Confidence level (0.0-1.0).</summary>
    public double Confidence { get; set; } = 1.0;
}

/// <summary>
/// Analysis result container for ARA content.
/// </summary>
public class ARAAnalysisResult
{
    /// <summary>Associated audio source.</summary>
    public ARAAudioSource? AudioSource { get; set; }

    /// <summary>Detected notes.</summary>
    public List<ARANoteContent> Notes { get; } = new();

    /// <summary>Detected tempo changes.</summary>
    public List<ARATempoContent> TempoChanges { get; } = new();

    /// <summary>Detected chords.</summary>
    public List<ARAChordContent> Chords { get; } = new();

    /// <summary>Detected key signature (0-11 for root, negative for minor).</summary>
    public int? DetectedKey { get; set; }

    /// <summary>Detected time signature numerator.</summary>
    public int TimeSignatureNumerator { get; set; } = 4;

    /// <summary>Detected time signature denominator.</summary>
    public int TimeSignatureDenominator { get; set; } = 4;

    /// <summary>Content types that were analyzed.</summary>
    public ARAContentType AnalyzedContent { get; set; } = ARAContentType.None;
}

/// <summary>
/// Event arguments for ARA analysis progress.
/// </summary>
public class ARAAnalysisProgressEventArgs : EventArgs
{
    /// <summary>Audio source being analyzed.</summary>
    public ARAAudioSource? AudioSource { get; }

    /// <summary>Progress percentage (0-100).</summary>
    public int ProgressPercent { get; }

    /// <summary>Current analysis phase description.</summary>
    public string Phase { get; }

    /// <summary>Creates new analysis progress event args.</summary>
    public ARAAnalysisProgressEventArgs(ARAAudioSource? source, int progress, string phase)
    {
        AudioSource = source;
        ProgressPercent = progress;
        Phase = phase;
    }
}

/// <summary>
/// Audio Random Access (ARA) host implementation for integrating with ARA-compatible plugins.
/// </summary>
/// <remarks>
/// ARA is an extension to audio plugin APIs (VST3, AU) that enables deeper integration
/// between DAWs and plugins for advanced audio editing capabilities like:
/// - Non-destructive pitch and time manipulation
/// - Content-aware audio analysis (notes, tempo, chords)
/// - Real-time preview of audio modifications
/// - Efficient random access to audio data
///
/// This is a stub implementation providing the interface structure for future
/// native ARA 2.0 SDK integration.
/// </remarks>
public class ARAHost : IDisposable
{
    private readonly List<ARAAudioSource> _audioSources = new();
    private readonly List<ARAPlaybackRegion> _playbackRegions = new();
    private readonly Dictionary<Guid, ARAAnalysisResult> _analysisResults = new();
    private ARADocumentController? _documentController;
    private bool _disposed;
    private readonly object _lock = new();

    /// <summary>Gets the registered audio sources.</summary>
    public IReadOnlyList<ARAAudioSource> AudioSources => _audioSources.AsReadOnly();

    /// <summary>Gets the playback regions.</summary>
    public IReadOnlyList<ARAPlaybackRegion> PlaybackRegions => _playbackRegions.AsReadOnly();

    /// <summary>Gets the current document controller.</summary>
    public ARADocumentController? DocumentController => _documentController;

    /// <summary>Gets or sets the sample rate.</summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>Event raised during content analysis.</summary>
    public event EventHandler<ARAAnalysisProgressEventArgs>? AnalysisProgress;

    /// <summary>Event raised when analysis is complete.</summary>
    public event EventHandler<ARAAudioSource>? AnalysisComplete;

    /// <summary>
    /// Creates a new ARA host instance.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate.</param>
    public ARAHost(int sampleRate = 44100)
    {
        SampleRate = sampleRate;
    }

    /// <summary>
    /// Creates a new document controller for an ARA session.
    /// </summary>
    /// <param name="documentName">Name of the document.</param>
    /// <returns>The created document controller.</returns>
    public ARADocumentController CreateDocumentController(string documentName)
    {
        lock (_lock)
        {
            _documentController = new ARADocumentController
            {
                Name = documentName,
                SampleRate = SampleRate
            };

            return _documentController;
        }
    }

    /// <summary>
    /// Registers an audio source with the ARA host.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="name">Optional name for the source.</param>
    /// <returns>The created audio source.</returns>
    public ARAAudioSource RegisterAudioSource(string filePath, string? name = null)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        lock (_lock)
        {
            var source = new ARAAudioSource
            {
                FilePath = filePath,
                Name = name ?? System.IO.Path.GetFileNameWithoutExtension(filePath),
                SampleRate = SampleRate
            };

            // In a real implementation, we would read the audio file metadata here
            _audioSources.Add(source);
            return source;
        }
    }

    /// <summary>
    /// Unregisters an audio source from the ARA host.
    /// </summary>
    /// <param name="source">The audio source to unregister.</param>
    public void UnregisterAudioSource(ARAAudioSource source)
    {
        if (source == null) return;

        lock (_lock)
        {
            // Remove any playback regions using this source
            _playbackRegions.RemoveAll(r => r.AudioSource?.Id == source.Id);

            // Remove analysis results
            _analysisResults.Remove(source.Id);

            // Remove the source
            _audioSources.Remove(source);
        }
    }

    /// <summary>
    /// Creates a playback region for an audio source.
    /// </summary>
    /// <param name="source">The audio source.</param>
    /// <param name="startInArrangement">Start time in the arrangement (seconds).</param>
    /// <param name="duration">Duration of the region (seconds).</param>
    /// <param name="startInSource">Start time in the source (seconds).</param>
    /// <returns>The created playback region.</returns>
    public ARAPlaybackRegion CreatePlaybackRegion(
        ARAAudioSource source,
        double startInArrangement,
        double duration,
        double startInSource = 0)
    {
        ArgumentNullException.ThrowIfNull(source);

        lock (_lock)
        {
            var region = new ARAPlaybackRegion
            {
                AudioSource = source,
                StartTimeInArrangement = startInArrangement,
                Duration = duration,
                StartTimeInSource = startInSource,
                Name = $"{source.Name} Region"
            };

            _playbackRegions.Add(region);
            return region;
        }
    }

    /// <summary>
    /// Removes a playback region.
    /// </summary>
    /// <param name="region">The region to remove.</param>
    public void RemovePlaybackRegion(ARAPlaybackRegion region)
    {
        if (region == null) return;

        lock (_lock)
        {
            _playbackRegions.Remove(region);
        }
    }

    /// <summary>
    /// Requests content analysis for an audio source.
    /// </summary>
    /// <param name="source">The audio source to analyze.</param>
    /// <param name="contentTypes">Types of content to analyze.</param>
    /// <returns>The analysis result (may be incomplete initially).</returns>
    public ARAAnalysisResult RequestAnalysis(ARAAudioSource source, ARAContentType contentTypes)
    {
        ArgumentNullException.ThrowIfNull(source);

        lock (_lock)
        {
            if (!_analysisResults.TryGetValue(source.Id, out var result))
            {
                result = new ARAAnalysisResult { AudioSource = source };
                _analysisResults[source.Id] = result;
            }

            // In a real implementation, this would start async analysis
            // For now, we mark what was requested
            result.AnalyzedContent |= contentTypes;

            // Simulate progress
            OnAnalysisProgress(source, 0, "Starting analysis...");
            OnAnalysisProgress(source, 50, "Analyzing content...");
            OnAnalysisProgress(source, 100, "Analysis complete");

            source.AvailableContent |= contentTypes;
            source.IsAnalysisComplete = true;

            AnalysisComplete?.Invoke(this, source);

            return result;
        }
    }

    /// <summary>
    /// Gets the analysis result for an audio source.
    /// </summary>
    /// <param name="source">The audio source.</param>
    /// <returns>Analysis result or null if not analyzed.</returns>
    public ARAAnalysisResult? GetAnalysisResult(ARAAudioSource source)
    {
        if (source == null) return null;

        lock (_lock)
        {
            return _analysisResults.TryGetValue(source.Id, out var result) ? result : null;
        }
    }

    /// <summary>
    /// Updates playback region time stretch factor.
    /// </summary>
    /// <param name="region">The region to update.</param>
    /// <param name="stretchFactor">New stretch factor (1.0 = no stretch).</param>
    public void SetTimeStretch(ARAPlaybackRegion region, double stretchFactor)
    {
        ArgumentNullException.ThrowIfNull(region);

        if (stretchFactor <= 0)
            throw new ArgumentException("Stretch factor must be positive.", nameof(stretchFactor));

        lock (_lock)
        {
            region.TimeStretchFactor = stretchFactor;
        }
    }

    /// <summary>
    /// Updates playback region pitch shift.
    /// </summary>
    /// <param name="region">The region to update.</param>
    /// <param name="semitones">Pitch shift in semitones.</param>
    /// <param name="preserveFormants">Whether to preserve formants.</param>
    public void SetPitchShift(ARAPlaybackRegion region, double semitones, bool preserveFormants = true)
    {
        ArgumentNullException.ThrowIfNull(region);

        lock (_lock)
        {
            region.PitchShiftSemitones = semitones;
            region.PreserveFormants = preserveFormants;
        }
    }

    /// <summary>
    /// Reads audio samples from a playback region with ARA processing.
    /// </summary>
    /// <param name="region">The playback region.</param>
    /// <param name="buffer">Buffer to fill with samples.</param>
    /// <param name="positionInRegion">Position within the region (seconds).</param>
    /// <param name="sampleCount">Number of samples to read.</param>
    /// <returns>Number of samples actually read.</returns>
    public int ReadAudio(ARAPlaybackRegion region, float[] buffer, double positionInRegion, int sampleCount)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(buffer);

        if (region.AudioSource == null || !region.IsEnabled)
        {
            Array.Clear(buffer, 0, Math.Min(buffer.Length, sampleCount));
            return sampleCount;
        }

        // In a real implementation, this would:
        // 1. Read source audio from the audio source
        // 2. Apply time stretching based on TimeStretchFactor
        // 3. Apply pitch shifting based on PitchShiftSemitones
        // 4. Return the processed audio

        // Stub implementation - fill with silence
        int samplesToWrite = Math.Min(buffer.Length, sampleCount);
        Array.Clear(buffer, 0, samplesToWrite);
        return samplesToWrite;
    }

    /// <summary>
    /// Gets playback regions at a specific time position.
    /// </summary>
    /// <param name="timeInArrangement">Time position in seconds.</param>
    /// <returns>List of regions active at that time.</returns>
    public List<ARAPlaybackRegion> GetRegionsAtTime(double timeInArrangement)
    {
        lock (_lock)
        {
            var result = new List<ARAPlaybackRegion>();
            foreach (var region in _playbackRegions)
            {
                if (region.IsEnabled &&
                    timeInArrangement >= region.StartTimeInArrangement &&
                    timeInArrangement < region.StartTimeInArrangement + region.Duration)
                {
                    result.Add(region);
                }
            }
            return result;
        }
    }

    /// <summary>
    /// Notifies the ARA plugin of host playback state changes.
    /// </summary>
    /// <param name="isPlaying">Whether playback is active.</param>
    /// <param name="playheadPosition">Current playhead position in seconds.</param>
    public void NotifyPlaybackState(bool isPlaying, double playheadPosition)
    {
        // In a real implementation, this would notify the ARA plugin
        // of transport state changes for real-time rendering
    }

    private void OnAnalysisProgress(ARAAudioSource source, int progress, string phase)
    {
        AnalysisProgress?.Invoke(this, new ARAAnalysisProgressEventArgs(source, progress, phase));
    }

    /// <summary>
    /// Disposes the ARA host and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _disposed = true;

            _playbackRegions.Clear();
            _audioSources.Clear();
            _analysisResults.Clear();
            _documentController = null;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer.
    /// </summary>
    ~ARAHost()
    {
        Dispose();
    }
}
