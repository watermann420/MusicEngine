// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: External integration component.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MusicEngine.Core.Integration;

/// <summary>
/// NKS preset categories.
/// </summary>
public enum NKSCategory
{
    /// <summary>Uncategorized.</summary>
    None,
    /// <summary>Bass sounds.</summary>
    Bass,
    /// <summary>Brass sounds.</summary>
    Brass,
    /// <summary>Chromatic percussion.</summary>
    ChromaticPerc,
    /// <summary>Drum kit.</summary>
    DrumKit,
    /// <summary>Ethnic sounds.</summary>
    Ethnic,
    /// <summary>FX sounds.</summary>
    FX,
    /// <summary>Guitar sounds.</summary>
    Guitar,
    /// <summary>Keyboard sounds.</summary>
    Keyboard,
    /// <summary>Lead sounds.</summary>
    Lead,
    /// <summary>Orchestral sounds.</summary>
    Orchestral,
    /// <summary>Organ sounds.</summary>
    Organ,
    /// <summary>Pad sounds.</summary>
    Pad,
    /// <summary>Percussion.</summary>
    Percussion,
    /// <summary>Piano sounds.</summary>
    Piano,
    /// <summary>Pluck sounds.</summary>
    Pluck,
    /// <summary>Strings sounds.</summary>
    Strings,
    /// <summary>Synth lead.</summary>
    SynthLead,
    /// <summary>Synth pad.</summary>
    SynthPad,
    /// <summary>Vocal sounds.</summary>
    Vocal,
    /// <summary>Woodwind sounds.</summary>
    Woodwind,
    /// <summary>Synth bass.</summary>
    SynthBass,
    /// <summary>Synth misc.</summary>
    SynthMisc,
    /// <summary>Acoustic drum.</summary>
    AcousticDrum,
    /// <summary>Acoustic bass.</summary>
    AcousticBass,
    /// <summary>Synth drum.</summary>
    SynthDrum
}

/// <summary>
/// NKS sub-categories for further classification.
/// </summary>
public static class NKSSubCategory
{
    /// <summary>Analog style.</summary>
    public const string Analog = "Analog";
    /// <summary>Digital style.</summary>
    public const string Digital = "Digital";
    /// <summary>FM synthesis.</summary>
    public const string FM = "FM";
    /// <summary>Wavetable synthesis.</summary>
    public const string Wavetable = "Wavetable";
    /// <summary>Additive synthesis.</summary>
    public const string Additive = "Additive";
    /// <summary>Spectral processing.</summary>
    public const string Spectral = "Spectral";
    /// <summary>Physical modeling.</summary>
    public const string Physical = "Physical";
    /// <summary>Sampled sounds.</summary>
    public const string Sampled = "Sampled";
    /// <summary>Granular synthesis.</summary>
    public const string Granular = "Granular";
    /// <summary>Hard/aggressive sound.</summary>
    public const string Hard = "Hard";
    /// <summary>Soft/mellow sound.</summary>
    public const string Soft = "Soft";
    /// <summary>Clean sound.</summary>
    public const string Clean = "Clean";
    /// <summary>Distorted sound.</summary>
    public const string Distorted = "Distorted";
    /// <summary>Bright sound.</summary>
    public const string Bright = "Bright";
    /// <summary>Dark sound.</summary>
    public const string Dark = "Dark";
    /// <summary>Warm sound.</summary>
    public const string Warm = "Warm";
    /// <summary>Cold sound.</summary>
    public const string Cold = "Cold";
    /// <summary>Evolving texture.</summary>
    public const string Evolving = "Evolving";
    /// <summary>Static texture.</summary>
    public const string Static = "Static";
    /// <summary>Mono.</summary>
    public const string Mono = "Mono";
    /// <summary>Poly.</summary>
    public const string Poly = "Poly";
    /// <summary>Arpeggiated.</summary>
    public const string Arpeggiated = "Arpeggiated";
    /// <summary>Sequenced.</summary>
    public const string Sequenced = "Sequenced";
}

/// <summary>
/// NKS parameter mapping entry.
/// </summary>
public class NKSParameterMapping
{
    /// <summary>Parameter ID in the plugin.</summary>
    public int ParameterId { get; set; }

    /// <summary>NKS control page index.</summary>
    public int PageIndex { get; set; }

    /// <summary>NKS control slot index (0-7 per page).</summary>
    public int SlotIndex { get; set; }

    /// <summary>Display name on NKS controller.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Section name (grouping).</summary>
    public string? Section { get; set; }

    /// <summary>Template parameter ID (for standard mappings).</summary>
    public string? TemplateId { get; set; }
}

/// <summary>
/// NKS preset metadata.
/// </summary>
public class NKSPresetMetadata
{
    /// <summary>Preset name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Preset author.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Preset vendor/company.</summary>
    public string Vendor { get; set; } = string.Empty;

    /// <summary>Preset description.</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>Bank or collection name.</summary>
    public string BankChain { get; set; } = string.Empty;

    /// <summary>Main category.</summary>
    public NKSCategory Category { get; set; }

    /// <summary>Sub-categories/modes.</summary>
    public List<string> SubCategories { get; } = new();

    /// <summary>Character tags.</summary>
    public List<string> Characters { get; } = new();

    /// <summary>Preset UUID.</summary>
    public string? Uuid { get; set; }

    /// <summary>Device/plugin name this preset belongs to.</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>Plugin type (effect, instrument, etc.).</summary>
    public string PluginType { get; set; } = string.Empty;

    /// <summary>Tempo if rhythm-based (0 = not applicable).</summary>
    public double Tempo { get; set; }

    /// <summary>Key signature if applicable.</summary>
    public string? KeySignature { get; set; }

    /// <summary>Time signature if applicable.</summary>
    public string? TimeSignature { get; set; }

    /// <summary>Parameter mappings for NKS controllers.</summary>
    public List<NKSParameterMapping> ParameterMappings { get; } = new();

    /// <summary>Preview audio file path.</summary>
    public string? PreviewAudioPath { get; set; }

    /// <summary>Raw preset data.</summary>
    public byte[]? PresetData { get; set; }
}

/// <summary>
/// NKS file container information.
/// </summary>
public class NKSFileInfo
{
    /// <summary>File path.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>NKS format version.</summary>
    public int Version { get; set; }

    /// <summary>Contained preset metadata.</summary>
    public NKSPresetMetadata? Metadata { get; set; }

    /// <summary>Plugin state data (chunk).</summary>
    public byte[]? PluginState { get; set; }

    /// <summary>Controller mapping data.</summary>
    public byte[]? ControllerMapping { get; set; }

    /// <summary>Preview audio data (OGG format).</summary>
    public byte[]? PreviewAudio { get; set; }
}

/// <summary>
/// Reader for Native Kontrol Standard (NKS) preset files.
/// </summary>
/// <remarks>
/// NKS is Native Instruments' standard for preset browsing and hardware
/// controller integration. NKS files (.nksf for instruments, .nksfx for effects)
/// contain:
/// - Preset metadata (name, author, categories, tags)
/// - Plugin state data (the actual preset)
/// - Parameter mappings for NI hardware controllers
/// - Optional preview audio (OGG format)
/// </remarks>
public static class NKSReader
{
    private const uint NKS_MAGIC = 0x4E4B5346; // "NKSF"
    private const uint NKS_MAGIC_FX = 0x4E4B5358; // "NKSX"

    /// <summary>
    /// Reads NKS metadata from a file.
    /// </summary>
    /// <param name="path">Path to the NKS file (.nksf or .nksfx).</param>
    /// <returns>NKS file information or null if invalid.</returns>
    public static NKSFileInfo? ReadFile(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            return ReadFromStream(stream, path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading NKS file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Reads NKS metadata from a stream.
    /// </summary>
    /// <param name="stream">Input stream.</param>
    /// <param name="filePath">Original file path for reference.</param>
    /// <returns>NKS file information or null if invalid.</returns>
    public static NKSFileInfo? ReadFromStream(Stream stream, string filePath = "")
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        // Read magic number
        uint magic = reader.ReadUInt32();
        if (magic != NKS_MAGIC && magic != NKS_MAGIC_FX)
            return null;

        var info = new NKSFileInfo
        {
            FilePath = filePath,
            Version = 1
        };

        var metadata = new NKSPresetMetadata();
        info.Metadata = metadata;

        // Read chunks
        while (stream.Position < stream.Length - 8)
        {
            string chunkId = ReadChunkId(reader);
            uint chunkSize = reader.ReadUInt32();
            long chunkEnd = stream.Position + chunkSize;

            switch (chunkId)
            {
                case "VERS":
                    info.Version = reader.ReadInt32();
                    break;

                case "NICA": // NI Chunk - Attributes (metadata JSON)
                    var metaJson = ReadNullTerminatedString(reader, (int)chunkSize);
                    ParseMetadataJson(metaJson, metadata);
                    break;

                case "NISD": // NI Chunk - Sound Data (plugin state)
                    info.PluginState = reader.ReadBytes((int)chunkSize);
                    break;

                case "NISI": // NI Chunk - Sound Info
                    var soundInfo = ReadNullTerminatedString(reader, (int)chunkSize);
                    ParseSoundInfoJson(soundInfo, metadata);
                    break;

                case "PLID": // Plugin ID
                    metadata.DeviceName = ReadNullTerminatedString(reader, (int)chunkSize);
                    break;

                case "PCHK": // Plugin Chunk
                    info.PluginState = reader.ReadBytes((int)chunkSize);
                    break;

                case "CTMA": // Controller Mapping
                    info.ControllerMapping = reader.ReadBytes((int)chunkSize);
                    ParseControllerMapping(info.ControllerMapping, metadata);
                    break;

                case "PRVW": // Preview Audio
                    info.PreviewAudio = reader.ReadBytes((int)chunkSize);
                    break;

                default:
                    // Skip unknown chunks
                    break;
            }

            stream.Position = chunkEnd;
        }

        return info;
    }

    private static string ReadChunkId(BinaryReader reader)
    {
        var chars = reader.ReadChars(4);
        return new string(chars);
    }

    private static string ReadNullTerminatedString(BinaryReader reader, int maxLength)
    {
        var bytes = reader.ReadBytes(maxLength);
        int nullIndex = Array.IndexOf(bytes, (byte)0);
        int length = nullIndex >= 0 ? nullIndex : bytes.Length;
        return Encoding.UTF8.GetString(bytes, 0, length);
    }

    private static void ParseMetadataJson(string json, NKSPresetMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("name", out var name))
                metadata.Name = name.GetString() ?? "";

            if (root.TryGetProperty("author", out var author))
                metadata.Author = author.GetString() ?? "";

            if (root.TryGetProperty("vendor", out var vendor))
                metadata.Vendor = vendor.GetString() ?? "";

            if (root.TryGetProperty("comment", out var comment))
                metadata.Comment = comment.GetString() ?? "";

            if (root.TryGetProperty("bankchain", out var bankchain))
                metadata.BankChain = bankchain.GetString() ?? "";

            if (root.TryGetProperty("uuid", out var uuid))
                metadata.Uuid = uuid.GetString();

            if (root.TryGetProperty("types", out var types) && types.ValueKind == JsonValueKind.Array)
            {
                foreach (var type in types.EnumerateArray())
                {
                    if (type.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var t in type.EnumerateArray())
                        {
                            var categoryStr = t.GetString();
                            if (!string.IsNullOrEmpty(categoryStr))
                            {
                                if (Enum.TryParse<NKSCategory>(categoryStr.Replace(" ", ""), true, out var cat))
                                    metadata.Category = cat;
                                else
                                    metadata.SubCategories.Add(categoryStr);
                            }
                        }
                    }
                }
            }

            if (root.TryGetProperty("modes", out var modes) && modes.ValueKind == JsonValueKind.Array)
            {
                foreach (var mode in modes.EnumerateArray())
                {
                    var modeStr = mode.GetString();
                    if (!string.IsNullOrEmpty(modeStr))
                        metadata.Characters.Add(modeStr);
                }
            }

            if (root.TryGetProperty("tempo", out var tempo))
                metadata.Tempo = tempo.GetDouble();

            if (root.TryGetProperty("key", out var key))
                metadata.KeySignature = key.GetString();

            if (root.TryGetProperty("timesig", out var timesig))
                metadata.TimeSignature = timesig.GetString();
        }
        catch (JsonException)
        {
            // Invalid JSON, skip parsing
        }
    }

    private static void ParseSoundInfoJson(string json, NKSPresetMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("deviceName", out var deviceName))
                metadata.DeviceName = deviceName.GetString() ?? "";

            if (root.TryGetProperty("pluginType", out var pluginType))
                metadata.PluginType = pluginType.GetString() ?? "";
        }
        catch (JsonException)
        {
            // Invalid JSON, skip parsing
        }
    }

    private static void ParseControllerMapping(byte[] data, NKSPresetMetadata metadata)
    {
        // Controller mapping is typically JSON or a custom binary format
        try
        {
            string json = Encoding.UTF8.GetString(data);
            if (json.StartsWith("{") || json.StartsWith("["))
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("pages", out var pages) && pages.ValueKind == JsonValueKind.Array)
                {
                    int pageIndex = 0;
                    foreach (var page in pages.EnumerateArray())
                    {
                        if (page.TryGetProperty("slots", out var slots) && slots.ValueKind == JsonValueKind.Array)
                        {
                            int slotIndex = 0;
                            foreach (var slot in slots.EnumerateArray())
                            {
                                var mapping = new NKSParameterMapping
                                {
                                    PageIndex = pageIndex,
                                    SlotIndex = slotIndex
                                };

                                if (slot.TryGetProperty("id", out var id))
                                    mapping.ParameterId = id.GetInt32();

                                if (slot.TryGetProperty("name", out var name))
                                    mapping.DisplayName = name.GetString() ?? "";

                                if (slot.TryGetProperty("section", out var section))
                                    mapping.Section = section.GetString();

                                metadata.ParameterMappings.Add(mapping);
                                slotIndex++;
                            }
                        }
                        pageIndex++;
                    }
                }
            }
        }
        catch
        {
            // Could not parse controller mapping
        }
    }

    /// <summary>
    /// Extracts the plugin state data from an NKS file.
    /// </summary>
    /// <param name="path">Path to the NKS file.</param>
    /// <returns>Plugin state data or null.</returns>
    public static byte[]? ExtractPluginState(string path)
    {
        var info = ReadFile(path);
        return info?.PluginState;
    }

    /// <summary>
    /// Extracts the preview audio from an NKS file.
    /// </summary>
    /// <param name="path">Path to the NKS file.</param>
    /// <returns>Preview audio data (OGG format) or null.</returns>
    public static byte[]? ExtractPreviewAudio(string path)
    {
        var info = ReadFile(path);
        return info?.PreviewAudio;
    }

    /// <summary>
    /// Gets category string from NKSCategory enum.
    /// </summary>
    public static string GetCategoryString(NKSCategory category)
    {
        return category switch
        {
            NKSCategory.ChromaticPerc => "Chromatic Perc",
            NKSCategory.DrumKit => "Drum Kit",
            NKSCategory.SynthLead => "Synth Lead",
            NKSCategory.SynthPad => "Synth Pad",
            NKSCategory.SynthBass => "Synth Bass",
            NKSCategory.SynthMisc => "Synth Misc",
            NKSCategory.SynthDrum => "Synth Drum",
            NKSCategory.AcousticDrum => "Acoustic Drum",
            NKSCategory.AcousticBass => "Acoustic Bass",
            _ => category.ToString()
        };
    }
}

/// <summary>
/// Scans directories for NKS preset files and builds a browseable database.
/// </summary>
public class NKSPresetScanner
{
    private readonly List<NKSPresetMetadata> _presets = new();
    private readonly object _lock = new();

    /// <summary>Gets discovered presets.</summary>
    public IReadOnlyList<NKSPresetMetadata> Presets
    {
        get
        {
            lock (_lock)
            {
                return _presets.AsReadOnly();
            }
        }
    }

    /// <summary>Event raised during scanning.</summary>
    public event Action<int, int, string>? ScanProgress;

    /// <summary>
    /// Scans a directory for NKS preset files.
    /// </summary>
    /// <param name="directory">Directory to scan.</param>
    /// <param name="recursive">Whether to scan subdirectories.</param>
    public void ScanDirectory(string directory, bool recursive = true)
    {
        if (!Directory.Exists(directory))
            return;

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(directory, "*.nks*", searchOption);

        int total = files.Length;
        int current = 0;

        foreach (var file in files)
        {
            current++;
            ScanProgress?.Invoke(current, total, Path.GetFileName(file));

            var info = NKSReader.ReadFile(file);
            if (info?.Metadata != null)
            {
                lock (_lock)
                {
                    _presets.Add(info.Metadata);
                }
            }
        }
    }

    /// <summary>
    /// Clears all discovered presets.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _presets.Clear();
        }
    }

    /// <summary>
    /// Filters presets by category.
    /// </summary>
    public IEnumerable<NKSPresetMetadata> FilterByCategory(NKSCategory category)
    {
        lock (_lock)
        {
            foreach (var preset in _presets)
            {
                if (preset.Category == category)
                    yield return preset;
            }
        }
    }

    /// <summary>
    /// Filters presets by device/plugin name.
    /// </summary>
    public IEnumerable<NKSPresetMetadata> FilterByDevice(string deviceName)
    {
        lock (_lock)
        {
            foreach (var preset in _presets)
            {
                if (preset.DeviceName.Contains(deviceName, StringComparison.OrdinalIgnoreCase))
                    yield return preset;
            }
        }
    }

    /// <summary>
    /// Searches presets by name.
    /// </summary>
    public IEnumerable<NKSPresetMetadata> Search(string query)
    {
        lock (_lock)
        {
            foreach (var preset in _presets)
            {
                if (preset.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    preset.Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    preset.Characters.Exists(c => c.Contains(query, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return preset;
                }
            }
        }
    }

    /// <summary>
    /// Gets the default NKS preset directories.
    /// </summary>
    public static IEnumerable<string> GetDefaultDirectories()
    {
        var paths = new List<string>();

        // Native Instruments default locations
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var userDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        paths.Add(Path.Combine(appData, "Native Instruments", "User Content"));
        paths.Add(Path.Combine(userDocuments, "Native Instruments", "User Content"));

        // Check for NI environment variables
        var niContent = Environment.GetEnvironmentVariable("NATIVE_INSTRUMENTS_CONTENT");
        if (!string.IsNullOrEmpty(niContent))
            paths.Add(niContent);

        return paths;
    }
}
