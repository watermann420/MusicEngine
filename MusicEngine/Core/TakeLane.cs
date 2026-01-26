// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core;

/// <summary>
/// Represents a color for visual organization of takes.
/// </summary>
public readonly struct TakeColor : IEquatable<TakeColor>
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }

    public TakeColor(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
    }

    /// <summary>Creates a color from a hex string (e.g., "#FF5500").</summary>
    public static TakeColor FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return Default;

        hex = hex.TrimStart('#');
        if (hex.Length != 6)
            return Default;

        try
        {
            var r = Convert.ToByte(hex.Substring(0, 2), 16);
            var g = Convert.ToByte(hex.Substring(2, 2), 16);
            var b = Convert.ToByte(hex.Substring(4, 2), 16);
            return new TakeColor(r, g, b);
        }
        catch
        {
            return Default;
        }
    }

    /// <summary>Converts the color to a hex string.</summary>
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>Default take color (cyan).</summary>
    public static TakeColor Default => new(0x00, 0xBC, 0xD4);

    /// <summary>Preset colors for takes.</summary>
    public static TakeColor Red => new(0xF4, 0x43, 0x36);
    public static TakeColor Orange => new(0xFF, 0x98, 0x00);
    public static TakeColor Yellow => new(0xFF, 0xEB, 0x3B);
    public static TakeColor Green => new(0x4C, 0xAF, 0x50);
    public static TakeColor Blue => new(0x21, 0x96, 0xF3);
    public static TakeColor Purple => new(0x9C, 0x27, 0xB0);
    public static TakeColor Pink => new(0xE9, 0x1E, 0x63);

    public bool Equals(TakeColor other) => R == other.R && G == other.G && B == other.B;
    public override bool Equals(object? obj) => obj is TakeColor other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(R, G, B);
    public static bool operator ==(TakeColor left, TakeColor right) => left.Equals(right);
    public static bool operator !=(TakeColor left, TakeColor right) => !left.Equals(right);
    public override string ToString() => ToHex();
}

/// <summary>
/// Represents a single recording take.
/// </summary>
public class Take
{
    /// <summary>Unique identifier for this take.</summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>Display name of the take.</summary>
    public string Name { get; set; } = "Take";

    /// <summary>Sequential number of this take within the lane.</summary>
    public int TakeNumber { get; set; } = 1;

    /// <summary>When this take was recorded.</summary>
    public DateTime RecordedAt { get; set; } = DateTime.Now;

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

    /// <summary>Rating for organization (0-5 stars).</summary>
    public int Rating { get; set; }

    /// <summary>Color for visual organization.</summary>
    public TakeColor Color { get; set; } = TakeColor.Default;

    /// <summary>Whether this take is muted.</summary>
    public bool IsMuted { get; set; }

    /// <summary>Notes or comments about this take.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Gets the duration of the take in seconds.</summary>
    public double DurationSeconds => Channels > 0 && SampleRate > 0
        ? (double)AudioData.Length / Channels / SampleRate
        : 0;

    /// <summary>Gets the total number of samples per channel.</summary>
    public int SampleCount => Channels > 0 ? AudioData.Length / Channels : 0;

    /// <summary>
    /// Creates a deep copy of this take.
    /// </summary>
    public Take Clone()
    {
        return new Take
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
    }

    /// <summary>
    /// Reads audio from this take at the specified position.
    /// </summary>
    /// <param name="buffer">Buffer to fill with audio data.</param>
    /// <param name="offsetSamples">Offset in samples from the start of the take.</param>
    /// <param name="count">Number of samples to read.</param>
    /// <returns>Number of samples actually read.</returns>
    public int ReadAudio(float[] buffer, int offsetSamples, int count)
    {
        if (AudioData.Length == 0 || IsMuted)
        {
            Array.Clear(buffer, 0, Math.Min(count, buffer.Length));
            return count;
        }

        var startIndex = offsetSamples * Channels;
        var samplesToRead = Math.Min(count * Channels, AudioData.Length - startIndex);

        if (samplesToRead <= 0)
        {
            Array.Clear(buffer, 0, Math.Min(count, buffer.Length));
            return count;
        }

        Array.Copy(AudioData, startIndex, buffer, 0, Math.Min(samplesToRead, buffer.Length));

        // Clear remaining buffer if not enough data
        if (samplesToRead < count * Channels)
        {
            Array.Clear(buffer, samplesToRead, Math.Min(count * Channels - samplesToRead, buffer.Length - samplesToRead));
        }

        return count;
    }

    public override string ToString() => $"Take {TakeNumber}: {Name} ({Rating} stars)";
}

/// <summary>
/// Represents a comp region - a selected portion of a take for the final comp.
/// </summary>
public class CompRegion : IEquatable<CompRegion>
{
    /// <summary>Unique identifier for this region.</summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>The source take this region comes from.</summary>
    public Take SourceTake { get; set; } = null!;

    /// <summary>Start position in the arrangement (in beats).</summary>
    public double StartBeat { get; set; }

    /// <summary>End position in the arrangement (in beats).</summary>
    public double EndBeat { get; set; }

    /// <summary>Length of the region in beats.</summary>
    public double LengthBeats => EndBeat - StartBeat;

    /// <summary>Offset into the source take (in beats from take start).</summary>
    public double SourceOffset { get; set; }

    /// <summary>Fade-in duration in beats (for smooth crossfades).</summary>
    public double FadeInBeats { get; set; } = 0.0625; // 1/16 beat

    /// <summary>Fade-out duration in beats (for smooth crossfades).</summary>
    public double FadeOutBeats { get; set; } = 0.0625;

    /// <summary>Whether this region is active in the comp.</summary>
    public bool IsActive { get; set; } = true;

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
    public float GetFadeGainAt(double beat)
    {
        if (!ContainsBeat(beat)) return 0f;

        var posInRegion = beat - StartBeat;
        var length = LengthBeats;
        var gain = 1f;

        // Apply fade-in
        if (FadeInBeats > 0 && posInRegion < FadeInBeats)
        {
            gain *= (float)(posInRegion / FadeInBeats);
        }

        // Apply fade-out
        var fadeOutStart = length - FadeOutBeats;
        if (FadeOutBeats > 0 && posInRegion > fadeOutStart)
        {
            var fadePos = (posInRegion - fadeOutStart) / FadeOutBeats;
            gain *= 1f - (float)fadePos;
        }

        return Math.Clamp(gain, 0f, 1f);
    }

    public bool Equals(CompRegion? other)
    {
        if (other is null) return false;
        return Id == other.Id;
    }

    public override bool Equals(object? obj) => obj is CompRegion other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => $"CompRegion [{StartBeat:F2} - {EndBeat:F2}] from {SourceTake?.Name ?? "null"}";
}

/// <summary>
/// Event arguments for take added events.
/// </summary>
public class TakeAddedEventArgs : EventArgs
{
    /// <summary>The take that was added.</summary>
    public Take Take { get; }

    /// <summary>The index of the take in the lane.</summary>
    public int Index { get; }

    public TakeAddedEventArgs(Take take, int index)
    {
        Take = take;
        Index = index;
    }
}

/// <summary>
/// Event arguments for take removed events.
/// </summary>
public class TakeRemovedEventArgs : EventArgs
{
    /// <summary>The take that was removed.</summary>
    public Take Take { get; }

    public TakeRemovedEventArgs(Take take)
    {
        Take = take;
    }
}

/// <summary>
/// Event arguments for comp region changed events.
/// </summary>
public class CompRegionChangedEventArgs : EventArgs
{
    /// <summary>The comp region that changed.</summary>
    public CompRegion? Region { get; }

    /// <summary>The type of change that occurred.</summary>
    public CompRegionChangeType ChangeType { get; }

    public CompRegionChangedEventArgs(CompRegion? region, CompRegionChangeType changeType)
    {
        Region = region;
        ChangeType = changeType;
    }
}

/// <summary>
/// The type of change to a comp region.
/// </summary>
public enum CompRegionChangeType
{
    Added,
    Removed,
    Modified,
    Cleared
}

/// <summary>
/// Manages multiple takes for a track and compositing them into a final comp.
/// Used for comping workflows where you record multiple takes and select the best parts.
/// </summary>
public class TakeLane
{
    /// <summary>Unique identifier for this take lane.</summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>Display name of the take lane.</summary>
    public string Name { get; set; } = "Take Lane";

    /// <summary>Track index this lane belongs to.</summary>
    public int TrackIndex { get; set; }

    private readonly List<Take> _takes = new();
    private readonly List<CompRegion> _compRegions = new();
    private readonly object _lock = new();

    /// <summary>Event raised when a take is added.</summary>
    public event EventHandler<TakeAddedEventArgs>? TakeAdded;

    /// <summary>Event raised when a take is removed.</summary>
    public event EventHandler<TakeRemovedEventArgs>? TakeRemoved;

    /// <summary>Event raised when the comp changes.</summary>
    public event EventHandler<CompRegionChangedEventArgs>? CompChanged;

    /// <summary>Event raised when any property changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the number of takes in this lane.</summary>
    public int TakeCount => _takes.Count;

    /// <summary>Gets the number of comp regions.</summary>
    public int CompRegionCount => _compRegions.Count;

    /// <summary>
    /// Creates a new take with automatic naming.
    /// </summary>
    /// <param name="name">Optional name (defaults to "Take N").</param>
    /// <returns>The new take.</returns>
    public Take CreateTake(string? name = null)
    {
        lock (_lock)
        {
            var takeNumber = _takes.Count + 1;
            var take = new Take
            {
                Name = name ?? $"Take {takeNumber}",
                TakeNumber = takeNumber,
                RecordedAt = DateTime.Now
            };

            AddTake(take);
            return take;
        }
    }

    /// <summary>
    /// Adds an existing take to this lane.
    /// </summary>
    /// <param name="take">The take to add.</param>
    public void AddTake(Take take)
    {
        if (take == null) throw new ArgumentNullException(nameof(take));

        lock (_lock)
        {
            // Ensure unique take number
            if (_takes.Any(t => t.TakeNumber == take.TakeNumber))
            {
                take.TakeNumber = _takes.Count > 0
                    ? _takes.Max(t => t.TakeNumber) + 1
                    : 1;
            }

            _takes.Add(take);
            var index = _takes.Count - 1;

            TakeAdded?.Invoke(this, new TakeAddedEventArgs(take, index));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Removes a take from this lane.
    /// </summary>
    /// <param name="take">The take to remove.</param>
    /// <returns>True if the take was removed.</returns>
    public bool RemoveTake(Take take)
    {
        if (take == null) return false;

        lock (_lock)
        {
            // Remove any comp regions using this take
            _compRegions.RemoveAll(r => r.SourceTake == take);

            if (_takes.Remove(take))
            {
                TakeRemoved?.Invoke(this, new TakeRemovedEventArgs(take));
                CompChanged?.Invoke(this, new CompRegionChangedEventArgs(null, CompRegionChangeType.Modified));
                Changed?.Invoke(this, EventArgs.Empty);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Gets all takes in this lane.
    /// </summary>
    public IReadOnlyList<Take> GetTakes()
    {
        lock (_lock)
        {
            return _takes.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets a take by its take number.
    /// </summary>
    /// <param name="takeNumber">The take number to find.</param>
    /// <returns>The take, or null if not found.</returns>
    public Take? GetTake(int takeNumber)
    {
        lock (_lock)
        {
            return _takes.FirstOrDefault(t => t.TakeNumber == takeNumber);
        }
    }

    /// <summary>
    /// Gets a take by its ID.
    /// </summary>
    /// <param name="id">The take ID to find.</param>
    /// <returns>The take, or null if not found.</returns>
    public Take? GetTakeById(string id)
    {
        lock (_lock)
        {
            return _takes.FirstOrDefault(t => t.Id == id);
        }
    }

    /// <summary>
    /// Gets takes at a specific beat position.
    /// </summary>
    /// <param name="beat">The beat position.</param>
    /// <returns>Takes that overlap the position.</returns>
    public IEnumerable<Take> GetTakesAtBeat(double beat)
    {
        lock (_lock)
        {
            return _takes.Where(t => beat >= t.StartBeat && beat < t.EndBeat).ToList();
        }
    }

    /// <summary>
    /// Adds a comp region from a take.
    /// </summary>
    /// <param name="take">The source take.</param>
    /// <param name="startBeat">Start position in arrangement.</param>
    /// <param name="endBeat">End position in arrangement.</param>
    /// <returns>The new comp region.</returns>
    public CompRegion AddCompRegion(Take take, double startBeat, double endBeat)
    {
        if (take == null) throw new ArgumentNullException(nameof(take));
        if (endBeat <= startBeat) throw new ArgumentException("End beat must be greater than start beat");

        lock (_lock)
        {
            // Calculate the source offset within the take
            var sourceOffset = startBeat - take.StartBeat;

            var region = new CompRegion
            {
                SourceTake = take,
                StartBeat = startBeat,
                EndBeat = endBeat,
                SourceOffset = sourceOffset
            };

            // Remove overlapping regions (or split them)
            RemoveOverlappingRegions(startBeat, endBeat);

            _compRegions.Add(region);
            SortCompRegions();

            CompChanged?.Invoke(this, new CompRegionChangedEventArgs(region, CompRegionChangeType.Added));
            Changed?.Invoke(this, EventArgs.Empty);

            return region;
        }
    }

    /// <summary>
    /// Removes overlapping regions for a new region.
    /// </summary>
    private void RemoveOverlappingRegions(double startBeat, double endBeat)
    {
        var overlapping = _compRegions
            .Where(r => r.OverlapsWith(startBeat, endBeat))
            .ToList();

        foreach (var region in overlapping)
        {
            // Check if we need to split the region
            if (region.StartBeat < startBeat && region.EndBeat > endBeat)
            {
                // Split: create a region for the part after
                var afterRegion = new CompRegion
                {
                    SourceTake = region.SourceTake,
                    StartBeat = endBeat,
                    EndBeat = region.EndBeat,
                    SourceOffset = region.SourceOffset + (endBeat - region.StartBeat),
                    FadeInBeats = region.FadeInBeats,
                    FadeOutBeats = region.FadeOutBeats
                };

                // Trim the existing region to before
                region.EndBeat = startBeat;

                _compRegions.Add(afterRegion);
            }
            else if (region.StartBeat < startBeat)
            {
                // Trim end
                region.EndBeat = startBeat;
            }
            else if (region.EndBeat > endBeat)
            {
                // Trim start
                var trimAmount = endBeat - region.StartBeat;
                region.SourceOffset += trimAmount;
                region.StartBeat = endBeat;
            }
            else
            {
                // Fully overlapped, remove it
                _compRegions.Remove(region);
            }
        }
    }

    /// <summary>
    /// Removes a comp region.
    /// </summary>
    /// <param name="region">The region to remove.</param>
    /// <returns>True if removed.</returns>
    public bool RemoveCompRegion(CompRegion region)
    {
        if (region == null) return false;

        lock (_lock)
        {
            if (_compRegions.Remove(region))
            {
                CompChanged?.Invoke(this, new CompRegionChangedEventArgs(region, CompRegionChangeType.Removed));
                Changed?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Clears all comp regions.
    /// </summary>
    public void ClearCompRegions()
    {
        lock (_lock)
        {
            if (_compRegions.Count > 0)
            {
                _compRegions.Clear();
                CompChanged?.Invoke(this, new CompRegionChangedEventArgs(null, CompRegionChangeType.Cleared));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Gets all comp regions.
    /// </summary>
    public IReadOnlyList<CompRegion> GetCompRegions()
    {
        lock (_lock)
        {
            return _compRegions.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Gets active comp regions at a specific beat.
    /// </summary>
    public IEnumerable<CompRegion> GetCompRegionsAtBeat(double beat)
    {
        lock (_lock)
        {
            return _compRegions
                .Where(r => r.IsActive && r.ContainsBeat(beat))
                .ToList();
        }
    }

    /// <summary>
    /// Sorts comp regions by start beat.
    /// </summary>
    private void SortCompRegions()
    {
        _compRegions.Sort((a, b) => a.StartBeat.CompareTo(b.StartBeat));
    }

    /// <summary>
    /// Quick comp: select all audio from one take as the comp.
    /// </summary>
    /// <param name="take">The take to use for the entire comp.</param>
    public void CompFromTake(Take take)
    {
        if (take == null) throw new ArgumentNullException(nameof(take));

        lock (_lock)
        {
            _compRegions.Clear();

            var region = new CompRegion
            {
                SourceTake = take,
                StartBeat = take.StartBeat,
                EndBeat = take.EndBeat,
                SourceOffset = 0,
                FadeInBeats = 0,
                FadeOutBeats = 0
            };

            _compRegions.Add(region);

            CompChanged?.Invoke(this, new CompRegionChangedEventArgs(region, CompRegionChangeType.Added));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Flattens the comp to a single audio clip.
    /// </summary>
    /// <param name="sampleRate">Target sample rate.</param>
    /// <param name="bpm">Tempo for beat-to-sample conversion.</param>
    /// <returns>The flattened audio clip.</returns>
    public AudioClip FlattenComp(int sampleRate, double bpm = 120.0)
    {
        lock (_lock)
        {
            if (_compRegions.Count == 0)
            {
                return new AudioClip
                {
                    Name = $"{Name} (Flattened)",
                    Length = 0
                };
            }

            var minStart = _compRegions.Min(r => r.StartBeat);
            var maxEnd = _compRegions.Max(r => r.EndBeat);
            var totalBeats = maxEnd - minStart;

            // Convert beats to samples
            var samplesPerBeat = sampleRate * 60.0 / bpm;
            var totalSamples = (int)(totalBeats * samplesPerBeat);
            var channels = _compRegions.FirstOrDefault()?.SourceTake?.Channels ?? 2;

            var outputData = new float[totalSamples * channels];

            foreach (var region in _compRegions.Where(r => r.IsActive))
            {
                var take = region.SourceTake;
                if (take == null || take.AudioData.Length == 0) continue;

                var regionStartSample = (int)((region.StartBeat - minStart) * samplesPerBeat);
                var regionLengthSamples = (int)(region.LengthBeats * samplesPerBeat);
                var sourceStartSample = (int)(region.SourceOffset * samplesPerBeat);

                for (var i = 0; i < regionLengthSamples; i++)
                {
                    var outputIndex = (regionStartSample + i) * channels;
                    var sourceIndex = (sourceStartSample + i) * take.Channels;

                    if (outputIndex >= outputData.Length || sourceIndex >= take.AudioData.Length)
                        break;

                    // Calculate fade
                    var beatPos = minStart + (regionStartSample + i) / samplesPerBeat;
                    var fadeGain = region.GetFadeGainAt(beatPos);

                    for (var ch = 0; ch < Math.Min(channels, take.Channels); ch++)
                    {
                        if (outputIndex + ch < outputData.Length && sourceIndex + ch < take.AudioData.Length)
                        {
                            outputData[outputIndex + ch] += take.AudioData[sourceIndex + ch] * fadeGain;
                        }
                    }
                }
            }

            // Clamp output
            for (var i = 0; i < outputData.Length; i++)
            {
                outputData[i] = Math.Clamp(outputData[i], -1f, 1f);
            }

            // Create the audio clip
            var clip = new AudioClip
            {
                Name = $"{Name} (Flattened)",
                StartPosition = minStart,
                Length = totalBeats,
                OriginalLength = totalBeats
            };

            // Store audio data in a temporary file or memory (implementation depends on AudioClip design)
            // For now, we return the clip metadata; actual audio would be written to file

            return clip;
        }
    }

    /// <summary>
    /// Reads audio from the comp at the specified position.
    /// Handles crossfades between comp regions.
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
            if (_compRegions.Count == 0) return buffer.Length;

            var sampleRate = _compRegions.FirstOrDefault()?.SourceTake?.SampleRate ?? 44100;
            var channels = _compRegions.FirstOrDefault()?.SourceTake?.Channels ?? 2;
            var samplesPerBeat = sampleRate * 60.0 / bpm;

            var totalSamples = (int)(beatsToRead * samplesPerBeat);
            var samplesToRead = Math.Min(totalSamples * channels, buffer.Length);

            // Find regions that overlap our read range
            var endBeat = beatPosition + beatsToRead;
            var activeRegions = _compRegions
                .Where(r => r.IsActive && r.OverlapsWith(beatPosition, endBeat))
                .ToList();

            foreach (var region in activeRegions)
            {
                var take = region.SourceTake;
                if (take == null || take.AudioData.Length == 0) continue;

                // Calculate the range within this region that we need
                var regionReadStart = Math.Max(beatPosition, region.StartBeat);
                var regionReadEnd = Math.Min(endBeat, region.EndBeat);

                if (regionReadEnd <= regionReadStart) continue;

                // Convert to samples
                var bufferStartSample = (int)((regionReadStart - beatPosition) * samplesPerBeat);
                var readLengthSamples = (int)((regionReadEnd - regionReadStart) * samplesPerBeat);

                // Source offset in the take
                var takeOffset = region.SourceOffset + (regionReadStart - region.StartBeat);
                var sourceStartSample = (int)(takeOffset * samplesPerBeat);

                for (var i = 0; i < readLengthSamples; i++)
                {
                    var outputIndex = (bufferStartSample + i) * channels;
                    var sourceIndex = (sourceStartSample + i) * take.Channels;

                    if (outputIndex >= buffer.Length) break;
                    if (sourceIndex >= take.AudioData.Length) break;

                    // Calculate fade at this position
                    var beatPos = regionReadStart + i / samplesPerBeat;
                    var fadeGain = region.GetFadeGainAt(beatPos);

                    for (var ch = 0; ch < Math.Min(channels, take.Channels); ch++)
                    {
                        if (outputIndex + ch < buffer.Length && sourceIndex + ch < take.AudioData.Length)
                        {
                            buffer[outputIndex + ch] += take.AudioData[sourceIndex + ch] * fadeGain;
                        }
                    }
                }
            }

            // Clamp output
            for (var i = 0; i < samplesToRead; i++)
            {
                buffer[i] = Math.Clamp(buffer[i], -1f, 1f);
            }

            return samplesToRead / channels;
        }
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
            if (_compRegions.Count == 0) return (0, 0);

            var start = _compRegions.Min(r => r.StartBeat);
            var end = _compRegions.Max(r => r.EndBeat);
            return (start, end);
        }
    }

    /// <summary>
    /// Auto-comp: automatically select the best-rated take for each time segment.
    /// </summary>
    public void AutoComp()
    {
        lock (_lock)
        {
            if (_takes.Count == 0) return;

            _compRegions.Clear();

            // Get all time segments where takes exist
            var (start, end) = GetTakeRange();

            // Use a simple approach: find the highest-rated take at each position
            // and create regions accordingly
            var sortedTakes = _takes.OrderByDescending(t => t.Rating).ToList();

            foreach (var take in sortedTakes)
            {
                if (take.IsMuted) continue;

                // Check if this range is already covered by a higher-rated take
                var currentStart = take.StartBeat;
                var currentEnd = take.EndBeat;

                var covered = _compRegions.Where(r => r.OverlapsWith(currentStart, currentEnd)).ToList();

                // Fill gaps with this take
                var gaps = FindGaps(currentStart, currentEnd, covered);

                foreach (var (gapStart, gapEnd) in gaps)
                {
                    if (gapEnd > gapStart)
                    {
                        var region = new CompRegion
                        {
                            SourceTake = take,
                            StartBeat = gapStart,
                            EndBeat = gapEnd,
                            SourceOffset = gapStart - take.StartBeat
                        };
                        _compRegions.Add(region);
                    }
                }
            }

            SortCompRegions();
            CompChanged?.Invoke(this, new CompRegionChangedEventArgs(null, CompRegionChangeType.Modified));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Finds gaps in coverage within a range.
    /// </summary>
    private List<(double Start, double End)> FindGaps(double start, double end, List<CompRegion> existing)
    {
        var gaps = new List<(double, double)>();
        var sortedRegions = existing.OrderBy(r => r.StartBeat).ToList();

        var currentPos = start;

        foreach (var region in sortedRegions)
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

    public override string ToString() => $"TakeLane: {Name} ({TakeCount} takes, {CompRegionCount} regions)";
}
