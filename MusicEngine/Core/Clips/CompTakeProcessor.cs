// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core.Clips;

/// <summary>
/// Represents a crossfade type for comp region boundaries.
/// </summary>
public enum CompCrossfadeType
{
    /// <summary>Linear crossfade.</summary>
    Linear,

    /// <summary>Equal power crossfade (maintains constant loudness).</summary>
    EqualPower,

    /// <summary>S-Curve crossfade (smooth transition).</summary>
    SCurve
}

/// <summary>
/// Represents a recording take with audio data and metadata.
/// </summary>
public class CompTake : IEquatable<CompTake>
{
    /// <summary>Unique identifier for this take.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Display name of the take.</summary>
    public string Name { get; set; } = "Take";

    /// <summary>Sequential number of this take.</summary>
    public int TakeNumber { get; set; } = 1;

    /// <summary>When this take was recorded.</summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Raw audio data (interleaved samples for multi-channel).</summary>
    public float[] AudioData { get; set; } = Array.Empty<float>();

    /// <summary>Sample rate of the audio data.</summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>Number of audio channels (1 = mono, 2 = stereo).</summary>
    public int Channels { get; set; } = 2;

    /// <summary>Start position in the arrangement (in beats).</summary>
    public double StartBeat { get; set; }

    /// <summary>Length of the take (in beats).</summary>
    public double LengthBeats { get; set; }

    /// <summary>End position in beats.</summary>
    public double EndBeat => StartBeat + LengthBeats;

    /// <summary>User rating for this take (0-5 stars).</summary>
    public int Rating { get; set; }

    /// <summary>Color for visual organization (hex format).</summary>
    public string Color { get; set; } = "#00BCD4";

    /// <summary>Whether this take is muted from playback.</summary>
    public bool IsMuted { get; set; }

    /// <summary>Notes or comments about this take.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Selected regions within this take for the comp.</summary>
    public List<CompSelectedRegion> SelectedRegions { get; } = new();

    /// <summary>Gets the duration of the take in seconds.</summary>
    public double DurationSeconds => Channels > 0 && SampleRate > 0
        ? (double)AudioData.Length / Channels / SampleRate
        : 0;

    /// <summary>Gets the total number of samples per channel.</summary>
    public int SampleCountPerChannel => Channels > 0 ? AudioData.Length / Channels : 0;

    /// <summary>
    /// Creates a deep copy of this take.
    /// </summary>
    public CompTake Clone()
    {
        var clone = new CompTake
        {
            Name = Name,
            TakeNumber = TakeNumber,
            RecordedAt = RecordedAt,
            AudioData = (float[])AudioData.Clone(),
            SampleRate = SampleRate,
            Channels = Channels,
            StartBeat = StartBeat,
            LengthBeats = LengthBeats,
            Rating = Rating,
            Color = Color,
            IsMuted = IsMuted,
            Notes = Notes
        };

        foreach (var region in SelectedRegions)
        {
            clone.SelectedRegions.Add(region.Clone());
        }

        return clone;
    }

    /// <summary>
    /// Reads audio samples from this take at the specified position.
    /// </summary>
    /// <param name="buffer">Buffer to fill with audio data.</param>
    /// <param name="offsetSamples">Offset in samples from the start of the take.</param>
    /// <param name="count">Number of sample frames to read.</param>
    /// <returns>Number of sample frames actually read.</returns>
    public int ReadAudio(float[] buffer, int offsetSamples, int count)
    {
        if (AudioData.Length == 0 || IsMuted)
        {
            Array.Clear(buffer, 0, Math.Min(count * Channels, buffer.Length));
            return count;
        }

        int startIndex = offsetSamples * Channels;
        int samplesToRead = Math.Min(count * Channels, AudioData.Length - startIndex);

        if (samplesToRead <= 0)
        {
            Array.Clear(buffer, 0, Math.Min(count * Channels, buffer.Length));
            return count;
        }

        Array.Copy(AudioData, startIndex, buffer, 0, Math.Min(samplesToRead, buffer.Length));

        if (samplesToRead < count * Channels && samplesToRead < buffer.Length)
        {
            Array.Clear(buffer, samplesToRead, Math.Min(count * Channels - samplesToRead, buffer.Length - samplesToRead));
        }

        return count;
    }

    public bool Equals(CompTake? other)
    {
        if (other is null) return false;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => obj is CompTake other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => $"Take {TakeNumber}: {Name} ({Rating} stars)";
}

/// <summary>
/// Represents a selected region within a take that will be used in the final comp.
/// </summary>
public class CompSelectedRegion : IEquatable<CompSelectedRegion>
{
    /// <summary>Unique identifier for this region.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Start position in beats (relative to take start).</summary>
    public double StartBeat { get; set; }

    /// <summary>End position in beats (relative to take start).</summary>
    public double EndBeat { get; set; }

    /// <summary>Length of the region in beats.</summary>
    public double LengthBeats => EndBeat - StartBeat;

    /// <summary>Whether this region is selected for the comp.</summary>
    public bool IsSelected { get; set; } = true;

    /// <summary>
    /// Creates a copy of this region.
    /// </summary>
    public CompSelectedRegion Clone()
    {
        return new CompSelectedRegion
        {
            StartBeat = StartBeat,
            EndBeat = EndBeat,
            IsSelected = IsSelected
        };
    }

    public bool Equals(CompSelectedRegion? other)
    {
        if (other is null) return false;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => obj is CompSelectedRegion other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => $"Region [{StartBeat:F2} - {EndBeat:F2}]";
}

/// <summary>
/// Represents a region in the final comp, linking to a specific portion of a take.
/// </summary>
public class CompRegionEntry : IEquatable<CompRegionEntry>
{
    /// <summary>Unique identifier for this comp region.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Reference to the source take.</summary>
    public CompTake SourceTake { get; set; } = null!;

    /// <summary>Index of the source take for serialization.</summary>
    public int TakeIndex { get; set; }

    /// <summary>Start position in the arrangement (in beats).</summary>
    public double StartBeat { get; set; }

    /// <summary>End position in the arrangement (in beats).</summary>
    public double EndBeat { get; set; }

    /// <summary>Offset into the source take (in beats from take start).</summary>
    public double SourceOffset { get; set; }

    /// <summary>Fade-in duration at region start (in beats).</summary>
    public double FadeInBeats { get; set; } = 0.03125; // 1/32 beat default

    /// <summary>Fade-out duration at region end (in beats).</summary>
    public double FadeOutBeats { get; set; } = 0.03125;

    /// <summary>Crossfade type for the fade-in.</summary>
    public CompCrossfadeType FadeInType { get; set; } = CompCrossfadeType.EqualPower;

    /// <summary>Crossfade type for the fade-out.</summary>
    public CompCrossfadeType FadeOutType { get; set; } = CompCrossfadeType.EqualPower;

    /// <summary>Whether this region is active in the comp.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Length of the region in beats.</summary>
    public double LengthBeats => EndBeat - StartBeat;

    /// <summary>
    /// Checks if this region overlaps with the specified beat range.
    /// </summary>
    public bool OverlapsWith(double startBeat, double endBeat)
    {
        return StartBeat < endBeat && EndBeat > startBeat;
    }

    /// <summary>
    /// Checks if the specified beat position is within this region.
    /// </summary>
    public bool ContainsBeat(double beat)
    {
        return beat >= StartBeat && beat < EndBeat;
    }

    /// <summary>
    /// Calculates the fade gain at a given position within the region.
    /// </summary>
    /// <param name="beat">Beat position (absolute arrangement position).</param>
    /// <returns>Gain multiplier (0.0 to 1.0).</returns>
    public float GetFadeGainAt(double beat)
    {
        if (!ContainsBeat(beat)) return 0f;

        double posInRegion = beat - StartBeat;
        double length = LengthBeats;
        float gain = 1f;

        // Apply fade-in
        if (FadeInBeats > 0 && posInRegion < FadeInBeats)
        {
            double t = posInRegion / FadeInBeats;
            gain *= CalculateFadeCurve(t, FadeInType);
        }

        // Apply fade-out
        double fadeOutStart = length - FadeOutBeats;
        if (FadeOutBeats > 0 && posInRegion > fadeOutStart)
        {
            double t = 1.0 - (posInRegion - fadeOutStart) / FadeOutBeats;
            gain *= CalculateFadeCurve(t, FadeOutType);
        }

        return Math.Clamp(gain, 0f, 1f);
    }

    private static float CalculateFadeCurve(double t, CompCrossfadeType type)
    {
        t = Math.Clamp(t, 0, 1);
        return type switch
        {
            CompCrossfadeType.Linear => (float)t,
            CompCrossfadeType.EqualPower => (float)Math.Sin(t * Math.PI / 2),
            CompCrossfadeType.SCurve => (float)(t * t * (3 - 2 * t)),
            _ => (float)t
        };
    }

    public bool Equals(CompRegionEntry? other)
    {
        if (other is null) return false;
        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj) => obj is CompRegionEntry other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() =>
        $"CompRegion [{StartBeat:F2} - {EndBeat:F2}] from Take {SourceTake?.TakeNumber ?? TakeIndex}";
}

/// <summary>
/// Represents an undoable action for the comp processor.
/// </summary>
public abstract class CompUndoAction
{
    /// <summary>Description of this action for UI display.</summary>
    public abstract string Description { get; }

    /// <summary>Executes the undo operation.</summary>
    public abstract void Undo(CompTakeProcessor processor);

    /// <summary>Executes the redo operation.</summary>
    public abstract void Redo(CompTakeProcessor processor);
}

/// <summary>
/// Undo action for adding a comp region.
/// </summary>
public class AddRegionUndoAction : CompUndoAction
{
    private readonly CompRegionEntry _region;

    public override string Description => $"Add comp region [{_region.StartBeat:F2} - {_region.EndBeat:F2}]";

    public AddRegionUndoAction(CompRegionEntry region)
    {
        _region = region;
    }

    public override void Undo(CompTakeProcessor processor) => processor.RemoveRegionInternal(_region);
    public override void Redo(CompTakeProcessor processor) => processor.AddRegionInternal(_region);
}

/// <summary>
/// Undo action for removing a comp region.
/// </summary>
public class RemoveRegionUndoAction : CompUndoAction
{
    private readonly CompRegionEntry _region;

    public override string Description => $"Remove comp region [{_region.StartBeat:F2} - {_region.EndBeat:F2}]";

    public RemoveRegionUndoAction(CompRegionEntry region)
    {
        _region = region;
    }

    public override void Undo(CompTakeProcessor processor) => processor.AddRegionInternal(_region);
    public override void Redo(CompTakeProcessor processor) => processor.RemoveRegionInternal(_region);
}

/// <summary>
/// Undo action for clearing all comp regions.
/// </summary>
public class ClearRegionsUndoAction : CompUndoAction
{
    private readonly List<CompRegionEntry> _regions;

    public override string Description => $"Clear {_regions.Count} comp regions";

    public ClearRegionsUndoAction(List<CompRegionEntry> regions)
    {
        _regions = new List<CompRegionEntry>(regions);
    }

    public override void Undo(CompTakeProcessor processor)
    {
        foreach (var region in _regions)
        {
            processor.AddRegionInternal(region);
        }
    }

    public override void Redo(CompTakeProcessor processor) => processor.ClearRegionsInternal();
}

/// <summary>
/// Event arguments for comp region changes.
/// </summary>
public class CompRegionEventArgs : EventArgs
{
    /// <summary>The affected region (may be null for bulk operations).</summary>
    public CompRegionEntry? Region { get; }

    /// <summary>Type of change that occurred.</summary>
    public CompChangeType ChangeType { get; }

    public CompRegionEventArgs(CompRegionEntry? region, CompChangeType changeType)
    {
        Region = region;
        ChangeType = changeType;
    }
}

/// <summary>
/// Type of change to the comp.
/// </summary>
public enum CompChangeType
{
    /// <summary>A region was added.</summary>
    RegionAdded,

    /// <summary>A region was removed.</summary>
    RegionRemoved,

    /// <summary>A region was modified.</summary>
    RegionModified,

    /// <summary>All regions were cleared.</summary>
    Cleared,

    /// <summary>A take was added.</summary>
    TakeAdded,

    /// <summary>A take was removed.</summary>
    TakeRemoved
}

/// <summary>
/// Advanced processor for comping multiple recording takes.
/// Provides selection of regions, crossfading at boundaries, and flattening to a single clip.
/// </summary>
public class CompTakeProcessor
{
    private readonly List<CompTake> _takes = new();
    private readonly List<CompRegionEntry> _regions = new();
    private readonly Stack<CompUndoAction> _undoStack = new();
    private readonly Stack<CompUndoAction> _redoStack = new();
    private readonly object _lock = new();

    /// <summary>Maximum number of undo actions to retain.</summary>
    public int MaxUndoHistory { get; set; } = 100;

    /// <summary>Default crossfade duration for new regions (in beats).</summary>
    public double DefaultCrossfadeDuration { get; set; } = 0.03125; // 1/32 beat

    /// <summary>Default crossfade type for new regions.</summary>
    public CompCrossfadeType DefaultCrossfadeType { get; set; } = CompCrossfadeType.EqualPower;

    /// <summary>Gets the number of takes.</summary>
    public int TakeCount => _takes.Count;

    /// <summary>Gets the number of comp regions.</summary>
    public int RegionCount => _regions.Count;

    /// <summary>Gets whether undo is available.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>Gets whether redo is available.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Event raised when the comp changes.</summary>
    public event EventHandler<CompRegionEventArgs>? CompChanged;

    /// <summary>Event raised when undo/redo state changes.</summary>
    public event EventHandler? UndoStateChanged;

    /// <summary>
    /// Creates a new take with automatic naming.
    /// </summary>
    /// <returns>The created take.</returns>
    public CompTake CreateTake()
    {
        lock (_lock)
        {
            var takeNumber = _takes.Count + 1;
            var take = new CompTake
            {
                Name = $"Take {takeNumber}",
                TakeNumber = takeNumber,
                RecordedAt = DateTime.UtcNow
            };

            _takes.Add(take);
            CompChanged?.Invoke(this, new CompRegionEventArgs(null, CompChangeType.TakeAdded));
            return take;
        }
    }

    /// <summary>
    /// Adds an existing take.
    /// </summary>
    /// <param name="take">The take to add.</param>
    public void AddTake(CompTake take)
    {
        ArgumentNullException.ThrowIfNull(take);

        lock (_lock)
        {
            if (_takes.Any(t => t.TakeNumber == take.TakeNumber))
            {
                take.TakeNumber = _takes.Count > 0 ? _takes.Max(t => t.TakeNumber) + 1 : 1;
            }

            _takes.Add(take);
        }

        CompChanged?.Invoke(this, new CompRegionEventArgs(null, CompChangeType.TakeAdded));
    }

    /// <summary>
    /// Removes a take and any associated comp regions.
    /// </summary>
    /// <param name="take">The take to remove.</param>
    /// <returns>True if the take was removed.</returns>
    public bool RemoveTake(CompTake take)
    {
        if (take == null) return false;

        lock (_lock)
        {
            // Remove regions using this take
            _regions.RemoveAll(r => r.SourceTake == take);

            if (_takes.Remove(take))
            {
                CompChanged?.Invoke(this, new CompRegionEventArgs(null, CompChangeType.TakeRemoved));
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Gets all takes.
    /// </summary>
    public IReadOnlyList<CompTake> GetTakes()
    {
        lock (_lock)
        {
            return _takes.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets a take by its ID.
    /// </summary>
    public CompTake? GetTake(Guid id)
    {
        lock (_lock)
        {
            return _takes.FirstOrDefault(t => t.Id == id);
        }
    }

    /// <summary>
    /// Gets a take by its take number.
    /// </summary>
    public CompTake? GetTake(int takeNumber)
    {
        lock (_lock)
        {
            return _takes.FirstOrDefault(t => t.TakeNumber == takeNumber);
        }
    }

    /// <summary>
    /// Adds a comp region from a take.
    /// </summary>
    /// <param name="take">The source take.</param>
    /// <param name="startBeat">Start position in the arrangement (beats).</param>
    /// <param name="endBeat">End position in the arrangement (beats).</param>
    /// <returns>The created comp region.</returns>
    public CompRegionEntry AddRegion(CompTake take, double startBeat, double endBeat)
    {
        ArgumentNullException.ThrowIfNull(take);
        if (endBeat <= startBeat)
            throw new ArgumentException("End beat must be greater than start beat.");

        var sourceOffset = startBeat - take.StartBeat;

        var region = new CompRegionEntry
        {
            SourceTake = take,
            TakeIndex = _takes.IndexOf(take),
            StartBeat = startBeat,
            EndBeat = endBeat,
            SourceOffset = sourceOffset,
            FadeInBeats = DefaultCrossfadeDuration,
            FadeOutBeats = DefaultCrossfadeDuration,
            FadeInType = DefaultCrossfadeType,
            FadeOutType = DefaultCrossfadeType
        };

        // Handle overlapping regions
        RemoveOverlappingRegions(startBeat, endBeat);

        AddRegionInternal(region);
        PushUndo(new AddRegionUndoAction(region));

        return region;
    }

    /// <summary>
    /// Removes a comp region.
    /// </summary>
    /// <param name="region">The region to remove.</param>
    /// <returns>True if removed.</returns>
    public bool RemoveRegion(CompRegionEntry region)
    {
        if (region == null) return false;

        bool removed = RemoveRegionInternal(region);
        if (removed)
        {
            PushUndo(new RemoveRegionUndoAction(region));
        }

        return removed;
    }

    /// <summary>
    /// Clears all comp regions.
    /// </summary>
    public void ClearRegions()
    {
        List<CompRegionEntry> oldRegions;
        lock (_lock)
        {
            oldRegions = new List<CompRegionEntry>(_regions);
            if (oldRegions.Count == 0) return;
        }

        ClearRegionsInternal();
        PushUndo(new ClearRegionsUndoAction(oldRegions));
    }

    /// <summary>
    /// Gets all comp regions sorted by start position.
    /// </summary>
    public IReadOnlyList<CompRegionEntry> GetRegions()
    {
        lock (_lock)
        {
            return _regions.OrderBy(r => r.StartBeat).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets active regions at a specific beat position.
    /// </summary>
    public IEnumerable<CompRegionEntry> GetRegionsAtBeat(double beat)
    {
        lock (_lock)
        {
            return _regions.Where(r => r.IsActive && r.ContainsBeat(beat)).ToList();
        }
    }

    /// <summary>
    /// Quick comp: select an entire take for the comp.
    /// </summary>
    /// <param name="take">The take to use.</param>
    public void CompFromTake(CompTake take)
    {
        ArgumentNullException.ThrowIfNull(take);

        ClearRegions();
        AddRegion(take, take.StartBeat, take.EndBeat);
    }

    /// <summary>
    /// Auto-comp: automatically select the best-rated take for each time segment.
    /// </summary>
    public void AutoComp()
    {
        lock (_lock)
        {
            if (_takes.Count == 0) return;

            var oldRegions = new List<CompRegionEntry>(_regions);
            _regions.Clear();

            // Get all time segments where takes exist
            var sortedTakes = _takes.Where(t => !t.IsMuted).OrderByDescending(t => t.Rating).ToList();

            foreach (var take in sortedTakes)
            {
                var start = take.StartBeat;
                var end = take.EndBeat;

                // Find gaps in existing coverage
                var gaps = FindGaps(start, end);

                foreach (var (gapStart, gapEnd) in gaps)
                {
                    if (gapEnd > gapStart)
                    {
                        var region = new CompRegionEntry
                        {
                            SourceTake = take,
                            TakeIndex = _takes.IndexOf(take),
                            StartBeat = gapStart,
                            EndBeat = gapEnd,
                            SourceOffset = gapStart - take.StartBeat,
                            FadeInBeats = DefaultCrossfadeDuration,
                            FadeOutBeats = DefaultCrossfadeDuration
                        };
                        _regions.Add(region);
                    }
                }
            }

            SortRegions();

            if (oldRegions.Count > 0 || _regions.Count > 0)
            {
                PushUndo(new ClearRegionsUndoAction(oldRegions));
            }
        }

        CompChanged?.Invoke(this, new CompRegionEventArgs(null, CompChangeType.RegionModified));
    }

    /// <summary>
    /// Flattens the comp to a single AudioClip.
    /// </summary>
    /// <param name="sampleRate">Target sample rate.</param>
    /// <param name="bpm">Tempo for beat-to-sample conversion.</param>
    /// <returns>A flattened audio clip with all comp regions combined.</returns>
    public AudioClip FlattenToClip(int sampleRate, double bpm = 120.0)
    {
        lock (_lock)
        {
            if (_regions.Count == 0)
            {
                return new AudioClip
                {
                    Name = "Flattened Comp",
                    Length = 0
                };
            }

            var minStart = _regions.Min(r => r.StartBeat);
            var maxEnd = _regions.Max(r => r.EndBeat);
            var totalBeats = maxEnd - minStart;

            var samplesPerBeat = sampleRate * 60.0 / bpm;
            var totalSamples = (int)(totalBeats * samplesPerBeat);
            var channels = _regions.FirstOrDefault()?.SourceTake?.Channels ?? 2;

            var outputData = new float[totalSamples * channels];

            foreach (var region in _regions.Where(r => r.IsActive))
            {
                var take = region.SourceTake;
                if (take == null || take.AudioData.Length == 0) continue;

                var regionStartSample = (int)((region.StartBeat - minStart) * samplesPerBeat);
                var regionLengthSamples = (int)(region.LengthBeats * samplesPerBeat);
                var sourceStartSample = (int)(region.SourceOffset * samplesPerBeat);

                for (int i = 0; i < regionLengthSamples; i++)
                {
                    int outputIndex = (regionStartSample + i) * channels;
                    int sourceIndex = (sourceStartSample + i) * take.Channels;

                    if (outputIndex >= outputData.Length || sourceIndex >= take.AudioData.Length)
                        break;

                    var beatPos = minStart + (regionStartSample + i) / samplesPerBeat;
                    var fadeGain = region.GetFadeGainAt(beatPos);

                    for (int ch = 0; ch < Math.Min(channels, take.Channels); ch++)
                    {
                        if (outputIndex + ch < outputData.Length && sourceIndex + ch < take.AudioData.Length)
                        {
                            outputData[outputIndex + ch] += take.AudioData[sourceIndex + ch] * fadeGain;
                        }
                    }
                }
            }

            // Clamp output
            for (int i = 0; i < outputData.Length; i++)
            {
                outputData[i] = Math.Clamp(outputData[i], -1f, 1f);
            }

            return new AudioClip
            {
                Name = "Flattened Comp",
                StartPosition = minStart,
                Length = totalBeats,
                OriginalLength = totalBeats,
                AudioData = outputData,
                SampleRate = sampleRate,
                Channels = channels,
                TotalSamples = totalSamples
            };
        }
    }

    /// <summary>
    /// Reads audio from the comp at the specified position.
    /// </summary>
    /// <param name="buffer">Buffer to fill with audio data.</param>
    /// <param name="beatPosition">Current beat position.</param>
    /// <param name="beatsToRead">Number of beats worth of audio to read.</param>
    /// <param name="bpm">Tempo in beats per minute.</param>
    /// <returns>Number of samples read.</returns>
    public int ReadAudio(float[] buffer, double beatPosition, double beatsToRead, double bpm)
    {
        if (buffer.Length == 0) return 0;

        Array.Clear(buffer, 0, buffer.Length);

        lock (_lock)
        {
            if (_regions.Count == 0) return buffer.Length;

            var sampleRate = _regions.FirstOrDefault()?.SourceTake?.SampleRate ?? 44100;
            var channels = _regions.FirstOrDefault()?.SourceTake?.Channels ?? 2;
            var samplesPerBeat = sampleRate * 60.0 / bpm;

            var totalSamples = (int)(beatsToRead * samplesPerBeat);
            var samplesToRead = Math.Min(totalSamples * channels, buffer.Length);

            var endBeat = beatPosition + beatsToRead;
            var activeRegions = _regions.Where(r => r.IsActive && r.OverlapsWith(beatPosition, endBeat)).ToList();

            foreach (var region in activeRegions)
            {
                var take = region.SourceTake;
                if (take == null || take.AudioData.Length == 0) continue;

                var regionReadStart = Math.Max(beatPosition, region.StartBeat);
                var regionReadEnd = Math.Min(endBeat, region.EndBeat);

                if (regionReadEnd <= regionReadStart) continue;

                var bufferStartSample = (int)((regionReadStart - beatPosition) * samplesPerBeat);
                var readLengthSamples = (int)((regionReadEnd - regionReadStart) * samplesPerBeat);
                var takeOffset = region.SourceOffset + (regionReadStart - region.StartBeat);
                var sourceStartSample = (int)(takeOffset * samplesPerBeat);

                for (int i = 0; i < readLengthSamples; i++)
                {
                    int outputIndex = (bufferStartSample + i) * channels;
                    int sourceIndex = (sourceStartSample + i) * take.Channels;

                    if (outputIndex >= buffer.Length) break;
                    if (sourceIndex >= take.AudioData.Length) break;

                    var beatPos = regionReadStart + i / samplesPerBeat;
                    var fadeGain = region.GetFadeGainAt(beatPos);

                    for (int ch = 0; ch < Math.Min(channels, take.Channels); ch++)
                    {
                        if (outputIndex + ch < buffer.Length && sourceIndex + ch < take.AudioData.Length)
                        {
                            buffer[outputIndex + ch] += take.AudioData[sourceIndex + ch] * fadeGain;
                        }
                    }
                }
            }

            // Clamp output
            for (int i = 0; i < samplesToRead; i++)
            {
                buffer[i] = Math.Clamp(buffer[i], -1f, 1f);
            }

            return samplesToRead / channels;
        }
    }

    /// <summary>
    /// Undoes the last action.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo) return;

        var action = _undoStack.Pop();
        action.Undo(this);
        _redoStack.Push(action);

        UndoStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Redoes the last undone action.
    /// </summary>
    public void Redo()
    {
        if (!CanRedo) return;

        var action = _redoStack.Pop();
        action.Redo(this);
        _undoStack.Push(action);

        UndoStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears the undo/redo history.
    /// </summary>
    public void ClearUndoHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        UndoStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets the overall time range covered by all takes.
    /// </summary>
    public (double Start, double End) GetTakeRange()
    {
        lock (_lock)
        {
            if (_takes.Count == 0) return (0, 0);

            var start = _takes.Min(t => t.StartBeat);
            var end = _takes.Max(t => t.EndBeat);
            return (start, end);
        }
    }

    /// <summary>
    /// Gets the overall time range covered by the comp.
    /// </summary>
    public (double Start, double End) GetCompRange()
    {
        lock (_lock)
        {
            if (_regions.Count == 0) return (0, 0);

            var start = _regions.Min(r => r.StartBeat);
            var end = _regions.Max(r => r.EndBeat);
            return (start, end);
        }
    }

    // Internal methods for undo/redo support

    internal void AddRegionInternal(CompRegionEntry region)
    {
        lock (_lock)
        {
            _regions.Add(region);
            SortRegions();
        }
        CompChanged?.Invoke(this, new CompRegionEventArgs(region, CompChangeType.RegionAdded));
    }

    internal bool RemoveRegionInternal(CompRegionEntry region)
    {
        bool removed;
        lock (_lock)
        {
            removed = _regions.Remove(region);
        }

        if (removed)
        {
            CompChanged?.Invoke(this, new CompRegionEventArgs(region, CompChangeType.RegionRemoved));
        }

        return removed;
    }

    internal void ClearRegionsInternal()
    {
        lock (_lock)
        {
            _regions.Clear();
        }
        CompChanged?.Invoke(this, new CompRegionEventArgs(null, CompChangeType.Cleared));
    }

    private void RemoveOverlappingRegions(double startBeat, double endBeat)
    {
        lock (_lock)
        {
            var overlapping = _regions.Where(r => r.OverlapsWith(startBeat, endBeat)).ToList();

            foreach (var region in overlapping)
            {
                if (region.StartBeat < startBeat && region.EndBeat > endBeat)
                {
                    // Split: create a region for the part after
                    var afterRegion = new CompRegionEntry
                    {
                        SourceTake = region.SourceTake,
                        TakeIndex = region.TakeIndex,
                        StartBeat = endBeat,
                        EndBeat = region.EndBeat,
                        SourceOffset = region.SourceOffset + (endBeat - region.StartBeat),
                        FadeInBeats = region.FadeInBeats,
                        FadeOutBeats = region.FadeOutBeats
                    };

                    region.EndBeat = startBeat;
                    _regions.Add(afterRegion);
                }
                else if (region.StartBeat < startBeat)
                {
                    region.EndBeat = startBeat;
                }
                else if (region.EndBeat > endBeat)
                {
                    var trimAmount = endBeat - region.StartBeat;
                    region.SourceOffset += trimAmount;
                    region.StartBeat = endBeat;
                }
                else
                {
                    _regions.Remove(region);
                }
            }
        }
    }

    private List<(double Start, double End)> FindGaps(double start, double end)
    {
        var gaps = new List<(double, double)>();
        var sorted = _regions.OrderBy(r => r.StartBeat).ToList();

        var currentPos = start;

        foreach (var region in sorted)
        {
            if (region.StartBeat > currentPos && currentPos < end)
            {
                gaps.Add((currentPos, Math.Min(region.StartBeat, end)));
            }
            currentPos = Math.Max(currentPos, region.EndBeat);
        }

        if (currentPos < end)
        {
            gaps.Add((currentPos, end));
        }

        return gaps;
    }

    private void SortRegions()
    {
        _regions.Sort((a, b) => a.StartBeat.CompareTo(b.StartBeat));
    }

    private void PushUndo(CompUndoAction action)
    {
        _undoStack.Push(action);
        _redoStack.Clear();

        // Trim history if needed
        while (_undoStack.Count > MaxUndoHistory)
        {
            var temp = new Stack<CompUndoAction>();
            while (_undoStack.Count > 1)
            {
                temp.Push(_undoStack.Pop());
            }
            _undoStack.Pop(); // Remove oldest
            while (temp.Count > 0)
            {
                _undoStack.Push(temp.Pop());
            }
        }

        UndoStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public override string ToString() => $"CompTakeProcessor: {TakeCount} takes, {RegionCount} regions";
}
