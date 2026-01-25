//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Audio Pool / Media Browser for managing all audio files in a project.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MusicEngine.Core.Analysis;
using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Event arguments for audio pool entry events.
/// </summary>
public class AudioPoolEntryEventArgs : EventArgs
{
    /// <summary>The audio pool entry associated with this event.</summary>
    public AudioPoolEntry Entry { get; }

    public AudioPoolEntryEventArgs(AudioPoolEntry entry)
    {
        Entry = entry;
    }
}

/// <summary>
/// Represents an audio file entry in the pool.
/// </summary>
public class AudioPoolEntry
{
    /// <summary>Unique identifier for this entry.</summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>Full path to the audio file.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>File name without path.</summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>File name without extension.</summary>
    public string FileNameWithoutExtension => Path.GetFileNameWithoutExtension(FilePath);

    /// <summary>File extension (e.g., ".wav").</summary>
    public string Extension => Path.GetExtension(FilePath);

    // Audio properties
    /// <summary>Sample rate in Hz.</summary>
    public int SampleRate { get; set; }

    /// <summary>Number of audio channels.</summary>
    public int Channels { get; set; }

    /// <summary>Bit depth (e.g., 16, 24, 32).</summary>
    public int BitDepth { get; set; }

    /// <summary>Duration of the audio file.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; set; }

    // Usage tracking
    /// <summary>How many clips use this file.</summary>
    public int UsageCount => UsedByClipIds.Count;

    /// <summary>IDs of clips that reference this audio file.</summary>
    public List<string> UsedByClipIds { get; } = new();

    // Metadata
    /// <summary>Artist name from file metadata.</summary>
    public string? Artist { get; set; }

    /// <summary>Album name from file metadata.</summary>
    public string? Album { get; set; }

    /// <summary>Detected BPM/tempo.</summary>
    public double? DetectedBpm { get; set; }

    /// <summary>Detected musical key (e.g., "C Major", "A Minor").</summary>
    public string? DetectedKey { get; set; }

    /// <summary>Camelot notation for DJs (e.g., "8A", "8B").</summary>
    public string? CamelotNotation { get; set; }

    /// <summary>User-defined comment or description.</summary>
    public string? Comment { get; set; }

    // Tags for organization
    /// <summary>User-defined tags for organization.</summary>
    public List<string> Tags { get; } = new();

    // Waveform cache
    /// <summary>Cached waveform peak data for visual display.</summary>
    public float[]? WaveformPeaks { get; set; }

    /// <summary>Number of peak samples in the waveform data.</summary>
    public int WaveformPeakCount => WaveformPeaks?.Length ?? 0;

    // Timestamps
    /// <summary>When this entry was added to the pool.</summary>
    public DateTime AddedAt { get; } = DateTime.UtcNow;

    /// <summary>When this entry was last accessed.</summary>
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether the file exists on disk.</summary>
    public bool FileExists => File.Exists(FilePath);

    /// <summary>Whether this file is located outside the project folder.</summary>
    public bool IsExternal { get; set; }

    /// <summary>
    /// Gets a formatted duration string (MM:SS.ms).
    /// </summary>
    public string DurationFormatted
    {
        get
        {
            var ts = Duration;
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            return $"{ts.Minutes}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}";
        }
    }

    /// <summary>
    /// Gets a formatted file size string.
    /// </summary>
    public string FileSizeFormatted
    {
        get
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            return FileSizeBytes switch
            {
                >= GB => $"{FileSizeBytes / (double)GB:F2} GB",
                >= MB => $"{FileSizeBytes / (double)MB:F2} MB",
                >= KB => $"{FileSizeBytes / (double)KB:F2} KB",
                _ => $"{FileSizeBytes} B"
            };
        }
    }

    /// <summary>
    /// Gets a formatted sample rate string (e.g., "44.1 kHz").
    /// </summary>
    public string SampleRateFormatted => SampleRate >= 1000
        ? $"{SampleRate / 1000.0:F1} kHz"
        : $"{SampleRate} Hz";

    /// <summary>
    /// Gets the channel format string (e.g., "Stereo", "Mono").
    /// </summary>
    public string ChannelFormat => Channels switch
    {
        1 => "Mono",
        2 => "Stereo",
        6 => "5.1",
        8 => "7.1",
        _ => $"{Channels} ch"
    };

    public override string ToString() => $"[{FileName}] {DurationFormatted} ({SampleRateFormatted}, {ChannelFormat})";
}

/// <summary>
/// Manages all audio files used in a project.
/// Provides centralized access, usage tracking, and file management.
/// </summary>
public class AudioPool
{
    private readonly Dictionary<string, AudioPoolEntry> _entries = new();
    private readonly Dictionary<string, AudioPoolEntry> _entriesByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <summary>Project folder for consolidation.</summary>
    public string ProjectFolder { get; set; } = string.Empty;

    /// <summary>Subfolder within project for audio files.</summary>
    public string AudioSubfolder { get; set; } = "Audio";

    /// <summary>Gets the full audio folder path.</summary>
    public string AudioFolderPath => string.IsNullOrEmpty(ProjectFolder)
        ? string.Empty
        : Path.Combine(ProjectFolder, AudioSubfolder);

    /// <summary>Gets the total number of entries in the pool.</summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>Gets the total size of all files in bytes.</summary>
    public long TotalSizeBytes
    {
        get
        {
            lock (_lock)
            {
                return _entries.Values.Sum(e => e.FileSizeBytes);
            }
        }
    }

    /// <summary>Gets a formatted total size string.</summary>
    public string TotalSizeFormatted
    {
        get
        {
            var size = TotalSizeBytes;
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            return size switch
            {
                >= GB => $"{size / (double)GB:F2} GB",
                >= MB => $"{size / (double)MB:F2} MB",
                >= KB => $"{size / (double)KB:F2} KB",
                _ => $"{size} B"
            };
        }
    }

    /// <summary>Event raised when an entry is added.</summary>
    public event EventHandler<AudioPoolEntryEventArgs>? EntryAdded;

    /// <summary>Event raised when an entry is removed.</summary>
    public event EventHandler<AudioPoolEntryEventArgs>? EntryRemoved;

    /// <summary>Event raised when an entry is updated.</summary>
    public event EventHandler<AudioPoolEntryEventArgs>? EntryUpdated;

    /// <summary>
    /// Adds a file to the pool.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <returns>The created or existing audio pool entry.</returns>
    public AudioPoolEntry AddFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);

        lock (_lock)
        {
            // Check if already in pool
            if (_entriesByPath.TryGetValue(fullPath, out var existing))
            {
                existing.LastAccessedAt = DateTime.UtcNow;
                return existing;
            }

            // Create new entry
            var entry = new AudioPoolEntry
            {
                FilePath = fullPath
            };

            // Read file info
            ReadFileInfo(entry);

            // Check if external
            if (!string.IsNullOrEmpty(ProjectFolder))
            {
                entry.IsExternal = !fullPath.StartsWith(ProjectFolder, StringComparison.OrdinalIgnoreCase);
            }

            _entries[entry.Id] = entry;
            _entriesByPath[fullPath] = entry;

            EntryAdded?.Invoke(this, new AudioPoolEntryEventArgs(entry));
            return entry;
        }
    }

    /// <summary>
    /// Removes a file from the pool (only if not in use).
    /// </summary>
    /// <param name="id">Entry ID to remove.</param>
    /// <param name="force">If true, removes even if in use.</param>
    /// <returns>True if removed successfully.</returns>
    public bool RemoveFile(string id, bool force = false)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(id, out var entry))
                return false;

            if (!force && entry.UsageCount > 0)
                return false;

            _entries.Remove(id);
            _entriesByPath.Remove(entry.FilePath);

            EntryRemoved?.Invoke(this, new AudioPoolEntryEventArgs(entry));
            return true;
        }
    }

    /// <summary>
    /// Gets an entry by ID.
    /// </summary>
    /// <param name="id">Entry ID.</param>
    /// <returns>The entry, or null if not found.</returns>
    public AudioPoolEntry? GetEntry(string id)
    {
        lock (_lock)
        {
            return _entries.GetValueOrDefault(id);
        }
    }

    /// <summary>
    /// Gets an entry by file path.
    /// </summary>
    /// <param name="filePath">File path.</param>
    /// <returns>The entry, or null if not found.</returns>
    public AudioPoolEntry? GetEntryByPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        var fullPath = Path.GetFullPath(filePath);

        lock (_lock)
        {
            return _entriesByPath.GetValueOrDefault(fullPath);
        }
    }

    /// <summary>
    /// Gets all entries in the pool.
    /// </summary>
    /// <returns>Read-only list of all entries.</returns>
    public IReadOnlyList<AudioPoolEntry> GetAllEntries()
    {
        lock (_lock)
        {
            return _entries.Values.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Searches entries by file name or tags.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <returns>Matching entries.</returns>
    public IEnumerable<AudioPoolEntry> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetAllEntries();

        var lowerQuery = query.ToLowerInvariant();

        lock (_lock)
        {
            return _entries.Values
                .Where(e =>
                    e.FileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    e.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (e.Artist?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.Album?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.DetectedKey?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.CamelotNotation?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.Comment?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }
    }

    /// <summary>
    /// Filters entries by tag.
    /// </summary>
    /// <param name="tag">Tag to filter by.</param>
    /// <returns>Entries with the specified tag.</returns>
    public IEnumerable<AudioPoolEntry> GetByTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return GetAllEntries();

        lock (_lock)
        {
            return _entries.Values
                .Where(e => e.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <summary>
    /// Gets all unique tags in the pool.
    /// </summary>
    /// <returns>List of unique tags.</returns>
    public IReadOnlyList<string> GetAllTags()
    {
        lock (_lock)
        {
            return _entries.Values
                .SelectMany(e => e.Tags)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t)
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Gets entries that are not used by any clips.
    /// </summary>
    /// <returns>Unused entries.</returns>
    public IEnumerable<AudioPoolEntry> GetUnusedFiles()
    {
        lock (_lock)
        {
            return _entries.Values
                .Where(e => e.UsageCount == 0)
                .ToList();
        }
    }

    /// <summary>
    /// Gets entries that are located outside the project folder.
    /// </summary>
    /// <returns>External entries.</returns>
    public IEnumerable<AudioPoolEntry> GetExternalFiles()
    {
        lock (_lock)
        {
            return _entries.Values
                .Where(e => e.IsExternal)
                .ToList();
        }
    }

    /// <summary>
    /// Gets entries where the file no longer exists on disk.
    /// </summary>
    /// <returns>Missing file entries.</returns>
    public IEnumerable<AudioPoolEntry> GetMissingFiles()
    {
        lock (_lock)
        {
            return _entries.Values
                .Where(e => !e.FileExists)
                .ToList();
        }
    }

    /// <summary>
    /// Increments usage count for an entry.
    /// </summary>
    /// <param name="id">Entry ID.</param>
    /// <param name="clipId">ID of the clip using this entry.</param>
    public void IncrementUsage(string id, string clipId)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                if (!entry.UsedByClipIds.Contains(clipId))
                {
                    entry.UsedByClipIds.Add(clipId);
                    entry.LastAccessedAt = DateTime.UtcNow;
                    EntryUpdated?.Invoke(this, new AudioPoolEntryEventArgs(entry));
                }
            }
        }
    }

    /// <summary>
    /// Decrements usage count for an entry.
    /// </summary>
    /// <param name="id">Entry ID.</param>
    /// <param name="clipId">ID of the clip that was using this entry.</param>
    public void DecrementUsage(string id, string clipId)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                entry.UsedByClipIds.Remove(clipId);
                EntryUpdated?.Invoke(this, new AudioPoolEntryEventArgs(entry));
            }
        }
    }

    /// <summary>
    /// Consolidates all external files into the project folder.
    /// </summary>
    /// <param name="progress">Progress reporter (0.0 to 1.0).</param>
    /// <returns>Number of files consolidated.</returns>
    public async Task<int> ConsolidateAsync(IProgress<double>? progress = null)
    {
        if (string.IsNullOrEmpty(ProjectFolder))
            throw new InvalidOperationException("Project folder must be set before consolidation.");

        var audioFolder = AudioFolderPath;
        if (!Directory.Exists(audioFolder))
        {
            Directory.CreateDirectory(audioFolder);
        }

        List<AudioPoolEntry> externalEntries;
        lock (_lock)
        {
            externalEntries = _entries.Values.Where(e => e.IsExternal && e.FileExists).ToList();
        }

        int consolidated = 0;
        for (int i = 0; i < externalEntries.Count; i++)
        {
            var entry = externalEntries[i];
            var newPath = Path.Combine(audioFolder, entry.FileName);

            // Handle duplicate file names
            int counter = 1;
            while (File.Exists(newPath) && !PathsAreEqual(newPath, entry.FilePath))
            {
                var name = Path.GetFileNameWithoutExtension(entry.FilePath);
                var ext = Path.GetExtension(entry.FilePath);
                newPath = Path.Combine(audioFolder, $"{name}_{counter}{ext}");
                counter++;
            }

            if (!PathsAreEqual(newPath, entry.FilePath))
            {
                await Task.Run(() => File.Copy(entry.FilePath, newPath, overwrite: false));

                lock (_lock)
                {
                    _entriesByPath.Remove(entry.FilePath);
                    entry.FilePath = newPath;
                    entry.IsExternal = false;
                    _entriesByPath[newPath] = entry;
                }

                consolidated++;
                EntryUpdated?.Invoke(this, new AudioPoolEntryEventArgs(entry));
            }

            progress?.Report((i + 1) / (double)externalEntries.Count);
        }

        return consolidated;
    }

    /// <summary>
    /// Removes all unused files from the pool.
    /// </summary>
    /// <returns>Number of entries removed.</returns>
    public int RemoveUnusedFiles()
    {
        var unused = GetUnusedFiles().ToList();
        int removed = 0;

        foreach (var entry in unused)
        {
            if (RemoveFile(entry.Id, force: true))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>
    /// Analyzes all files in the pool for BPM and key detection.
    /// </summary>
    /// <param name="progress">Progress reporter (0.0 to 1.0).</param>
    public async Task AnalyzeAllAsync(IProgress<double>? progress = null)
    {
        List<AudioPoolEntry> entries;
        lock (_lock)
        {
            entries = _entries.Values.Where(e => e.FileExists).ToList();
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            await AnalyzeEntryAsync(entry);
            progress?.Report((i + 1) / (double)entries.Count);
        }
    }

    /// <summary>
    /// Analyzes a single entry for BPM and key detection.
    /// </summary>
    /// <param name="entry">Entry to analyze.</param>
    public async Task AnalyzeEntryAsync(AudioPoolEntry entry)
    {
        if (!entry.FileExists)
            return;

        await Task.Run(() =>
        {
            try
            {
                using var reader = new AudioFileReader(entry.FilePath);

                // Convert to mono float array
                var samples = new List<float>();
                var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
                int read;

                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    // Mix to mono
                    for (int i = 0; i < read; i += reader.WaveFormat.Channels)
                    {
                        float sum = 0;
                        for (int ch = 0; ch < reader.WaveFormat.Channels; ch++)
                        {
                            sum += buffer[i + ch];
                        }
                        samples.Add(sum / reader.WaveFormat.Channels);
                    }
                }

                var monoSamples = samples.ToArray();

                // BPM detection
                var tempoDetector = new TempoDetector(reader.WaveFormat.SampleRate);
                var tempoResult = tempoDetector.AnalyzeBuffer(monoSamples, reader.WaveFormat.SampleRate);
                if (tempoResult.Confidence > 0.3)
                {
                    entry.DetectedBpm = Math.Round(tempoResult.DetectedBpm, 1);
                }

                // Key detection
                var keyDetector = new KeyDetector(reader.WaveFormat.SampleRate);
                var keyResult = keyDetector.AnalyzeBuffer(monoSamples, reader.WaveFormat.SampleRate);
                if (keyResult.Confidence > 0.3)
                {
                    entry.DetectedKey = keyResult.KeyName;
                    entry.CamelotNotation = keyResult.CamelotNotation;
                }

                EntryUpdated?.Invoke(this, new AudioPoolEntryEventArgs(entry));
            }
            catch
            {
                // Analysis failed, ignore
            }
        });
    }

    /// <summary>
    /// Generates waveform peak data for an entry.
    /// </summary>
    /// <param name="entry">Entry to generate waveform for.</param>
    /// <param name="peakCount">Number of peaks to generate.</param>
    public async Task GenerateWaveformAsync(AudioPoolEntry entry, int peakCount = 1000)
    {
        if (!entry.FileExists)
            return;

        await Task.Run(() =>
        {
            try
            {
                using var reader = new AudioFileReader(entry.FilePath);

                long totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);
                int samplesPerPeak = (int)Math.Max(1, totalSamples / peakCount);

                var peaks = new List<float>();
                var buffer = new float[samplesPerPeak * reader.WaveFormat.Channels];
                int read;

                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    float maxPeak = 0;
                    for (int i = 0; i < read; i++)
                    {
                        maxPeak = Math.Max(maxPeak, Math.Abs(buffer[i]));
                    }
                    peaks.Add(maxPeak);
                }

                entry.WaveformPeaks = peaks.ToArray();
                EntryUpdated?.Invoke(this, new AudioPoolEntryEventArgs(entry));
            }
            catch
            {
                // Waveform generation failed, ignore
            }
        });
    }

    /// <summary>
    /// Adds a tag to an entry.
    /// </summary>
    /// <param name="id">Entry ID.</param>
    /// <param name="tag">Tag to add.</param>
    public void AddTag(string id, string tag)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                if (!entry.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    entry.Tags.Add(tag);
                    EntryUpdated?.Invoke(this, new AudioPoolEntryEventArgs(entry));
                }
            }
        }
    }

    /// <summary>
    /// Removes a tag from an entry.
    /// </summary>
    /// <param name="id">Entry ID.</param>
    /// <param name="tag">Tag to remove.</param>
    public void RemoveTag(string id, string tag)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                var existing = entry.Tags.FirstOrDefault(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    entry.Tags.Remove(existing);
                    EntryUpdated?.Invoke(this, new AudioPoolEntryEventArgs(entry));
                }
            }
        }
    }

    /// <summary>
    /// Clears all entries from the pool.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            _entriesByPath.Clear();
        }
    }

    /// <summary>
    /// Re-reads file info for an entry (useful after file modification).
    /// </summary>
    /// <param name="id">Entry ID.</param>
    public void RefreshEntry(string id)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                ReadFileInfo(entry);
                EntryUpdated?.Invoke(this, new AudioPoolEntryEventArgs(entry));
            }
        }
    }

    private void ReadFileInfo(AudioPoolEntry entry)
    {
        try
        {
            var fileInfo = new FileInfo(entry.FilePath);
            entry.FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0;

            if (fileInfo.Exists)
            {
                using var reader = new AudioFileReader(entry.FilePath);
                entry.SampleRate = reader.WaveFormat.SampleRate;
                entry.Channels = reader.WaveFormat.Channels;
                entry.BitDepth = reader.WaveFormat.BitsPerSample;
                entry.Duration = reader.TotalTime;
            }
        }
        catch
        {
            // Unable to read file info, leave defaults
        }
    }

    private static bool PathsAreEqual(string path1, string path2)
    {
        return string.Equals(
            Path.GetFullPath(path1),
            Path.GetFullPath(path2),
            StringComparison.OrdinalIgnoreCase);
    }
}
