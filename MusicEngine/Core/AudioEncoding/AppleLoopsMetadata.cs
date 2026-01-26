// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio encoding/export component.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MusicEngine.Core.AudioEncoding;

/// <summary>
/// Apple Loops file type categories.
/// </summary>
public enum AppleLoopType
{
    /// <summary>Non-Apple Loop audio.</summary>
    None = 0,

    /// <summary>Loop (tempo-synced, pitch-shifted).</summary>
    Loop = 1,

    /// <summary>One-shot (single hit, not tempo-synced).</summary>
    OneShot = 2
}

/// <summary>
/// Apple Loops scale types.
/// </summary>
public enum AppleLoopScaleType
{
    /// <summary>Scale type not specified.</summary>
    None = 0,

    /// <summary>Major scale.</summary>
    Major = 1,

    /// <summary>Minor scale.</summary>
    Minor = 2,

    /// <summary>Neither major nor minor (chromatic/atonal).</summary>
    Neither = 3,

    /// <summary>Both major and minor (ambiguous).</summary>
    Both = 4
}

/// <summary>
/// Apple Loops genre categories.
/// </summary>
public static class AppleLoopGenre
{
    /// <summary>Rock genre.</summary>
    public const string Rock = "Rock/Blues";
    /// <summary>Electronic genre.</summary>
    public const string Electronic = "Electronic";
    /// <summary>Jazz genre.</summary>
    public const string Jazz = "Jazz";
    /// <summary>Urban genre.</summary>
    public const string Urban = "Urban";
    /// <summary>World genre.</summary>
    public const string World = "World";
    /// <summary>Orchestral/Cinematic genre.</summary>
    public const string Orchestral = "Orchestral";
    /// <summary>Country/Folk genre.</summary>
    public const string Country = "Country/Folk";
    /// <summary>Experimental genre.</summary>
    public const string Experimental = "Experimental";
    /// <summary>Other genre.</summary>
    public const string Other = "Other Genre";
}

/// <summary>
/// Apple Loops instrument categories.
/// </summary>
public static class AppleLoopInstrument
{
    /// <summary>Drums category.</summary>
    public const string Drums = "Drums";
    /// <summary>Percussion category.</summary>
    public const string Percussion = "Percussion";
    /// <summary>Bass category.</summary>
    public const string Bass = "Bass";
    /// <summary>Guitar category.</summary>
    public const string Guitar = "Guitars";
    /// <summary>Piano/Keys category.</summary>
    public const string Piano = "Piano/Keys";
    /// <summary>Organ category.</summary>
    public const string Organ = "Organ";
    /// <summary>Synth category.</summary>
    public const string Synth = "Synths";
    /// <summary>Strings category.</summary>
    public const string Strings = "Strings";
    /// <summary>Brass category.</summary>
    public const string Brass = "Horns/Wind";
    /// <summary>Vocals category.</summary>
    public const string Vocals = "Vocals";
    /// <summary>FX category.</summary>
    public const string FX = "FX";
    /// <summary>Textures category.</summary>
    public const string Textures = "Textures";
    /// <summary>Other instrument category.</summary>
    public const string Other = "Other Inst";
}

/// <summary>
/// Apple Loops descriptor tags.
/// </summary>
public static class AppleLoopDescriptor
{
    // Feel descriptors
    /// <summary>Acoustic feel.</summary>
    public const string Acoustic = "Acoustic";
    /// <summary>Electric feel.</summary>
    public const string Electric = "Electric";
    /// <summary>Clean sound.</summary>
    public const string Clean = "Clean";
    /// <summary>Distorted sound.</summary>
    public const string Distorted = "Distorted";
    /// <summary>Dry (no effects).</summary>
    public const string Dry = "Dry";
    /// <summary>Processed (with effects).</summary>
    public const string Processed = "Processed";

    // Mood descriptors
    /// <summary>Relaxed mood.</summary>
    public const string Relaxed = "Relaxed";
    /// <summary>Intense mood.</summary>
    public const string Intense = "Intense";
    /// <summary>Dark mood.</summary>
    public const string Dark = "Dark";
    /// <summary>Cheerful mood.</summary>
    public const string Cheerful = "Cheerful";
    /// <summary>Grooving feel.</summary>
    public const string Grooving = "Grooving";
    /// <summary>Arrhythmic.</summary>
    public const string Arrhythmic = "Arrhythmic";

    // Part descriptors
    /// <summary>Single part.</summary>
    public const string Single = "Single";
    /// <summary>Ensemble/multiple parts.</summary>
    public const string Ensemble = "Ensemble";
    /// <summary>Part of a song.</summary>
    public const string Part = "Part";
    /// <summary>Fill.</summary>
    public const string Fill = "Fill";
}

/// <summary>
/// Contains Apple Loops metadata.
/// </summary>
public class AppleLoopsMetadata
{
    /// <summary>Loop type.</summary>
    public AppleLoopType LoopType { get; set; } = AppleLoopType.None;

    /// <summary>Tempo in BPM.</summary>
    public double Tempo { get; set; } = 120.0;

    /// <summary>Number of beats.</summary>
    public int NumBeats { get; set; } = 4;

    /// <summary>Time signature numerator.</summary>
    public int TimeSignatureNumerator { get; set; } = 4;

    /// <summary>Time signature denominator.</summary>
    public int TimeSignatureDenominator { get; set; } = 4;

    /// <summary>Key signature (0-11, 0=C).</summary>
    public int Key { get; set; } = 0;

    /// <summary>Scale type.</summary>
    public AppleLoopScaleType ScaleType { get; set; } = AppleLoopScaleType.None;

    /// <summary>Genre category.</summary>
    public string Genre { get; set; } = AppleLoopGenre.Other;

    /// <summary>Instrument category.</summary>
    public string Instrument { get; set; } = AppleLoopInstrument.Other;

    /// <summary>Descriptor tags.</summary>
    public List<string> Descriptors { get; } = new();

    /// <summary>Author/creator name.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Copyright information.</summary>
    public string Copyright { get; set; } = string.Empty;

    /// <summary>Comment/description.</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>Loop name/title.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Search tags (for browser).</summary>
    public List<string> SearchTags { get; } = new();

    /// <summary>Whether the loop is in Apple Loops format.</summary>
    public bool IsAppleLoop => LoopType != AppleLoopType.None;

    /// <summary>Gets the key name.</summary>
    public string KeyName
    {
        get
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            string keyStr = noteNames[Key % 12];
            return ScaleType switch
            {
                AppleLoopScaleType.Major => $"{keyStr} Major",
                AppleLoopScaleType.Minor => $"{keyStr} Minor",
                _ => keyStr
            };
        }
    }
}

/// <summary>
/// Reads Apple Loops metadata from CAF and AIFF files.
/// </summary>
/// <remarks>
/// Apple Loops metadata is stored in CAF (Core Audio Format) and AIFF files
/// using specific chunk types:
/// - CAF: 'info' and 'user' chunks contain metadata
/// - AIFF: 'ANNO' (annotation) and custom chunks
///
/// This metadata enables loop browsing in GarageBand, Logic Pro,
/// and other Apple audio applications.
/// </remarks>
public static class AppleLoopsReader
{
    // CAF magic
    private static readonly byte[] CAF_MAGIC = { 0x63, 0x61, 0x66, 0x66 }; // "caff"

    // AIFF magic
    private static readonly byte[] AIFF_MAGIC = { 0x46, 0x4F, 0x52, 0x4D }; // "FORM"

    /// <summary>
    /// Reads Apple Loops metadata from a file.
    /// </summary>
    /// <param name="path">Path to the audio file.</param>
    /// <returns>Apple Loops metadata or null if not an Apple Loop.</returns>
    public static AppleLoopsMetadata? ReadMetadata(string path)
    {
        if (!File.Exists(path))
            return null;

        string extension = Path.GetExtension(path).ToLowerInvariant();

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);

            byte[] magic = reader.ReadBytes(4);

            if (BytesEqual(magic, CAF_MAGIC))
            {
                return ReadCAFMetadata(reader, stream);
            }
            else if (BytesEqual(magic, AIFF_MAGIC))
            {
                return ReadAIFFMetadata(reader, stream);
            }

            // Check for WAV with Apple Loops chunks (rare)
            if (magic[0] == 'R' && magic[1] == 'I' && magic[2] == 'F' && magic[3] == 'F')
            {
                return ReadWAVAppleMetadata(reader, stream);
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading Apple Loops metadata: {ex.Message}");
            return null;
        }
    }

    private static bool BytesEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }

    private static AppleLoopsMetadata? ReadCAFMetadata(BinaryReader reader, Stream stream)
    {
        var metadata = new AppleLoopsMetadata();

        // CAF version (should be 1)
        ushort version = ReadUInt16BE(reader);
        ushort flags = ReadUInt16BE(reader);

        // Read chunks
        while (stream.Position < stream.Length - 12)
        {
            string chunkType = new string(reader.ReadChars(4));
            long chunkSize = ReadInt64BE(reader);
            long chunkEnd = stream.Position + chunkSize;

            switch (chunkType)
            {
                case "info":
                    ReadCAFInfoChunk(reader, chunkSize, metadata);
                    break;

                case "user":
                    ReadCAFUserChunk(reader, chunkSize, metadata);
                    break;

                case "inst":
                    ReadCAFInstrumentChunk(reader, metadata);
                    break;
            }

            stream.Position = chunkEnd;
        }

        return metadata.IsAppleLoop ? metadata : null;
    }

    private static void ReadCAFInfoChunk(BinaryReader reader, long chunkSize, AppleLoopsMetadata metadata)
    {
        long endPosition = reader.BaseStream.Position + chunkSize;
        uint numEntries = ReadUInt32BE(reader);

        for (int i = 0; i < numEntries && reader.BaseStream.Position < endPosition; i++)
        {
            string key = ReadNullTerminatedString(reader);
            string value = ReadNullTerminatedString(reader);

            switch (key.ToLowerInvariant())
            {
                case "tempo":
                    if (double.TryParse(value, out double tempo))
                        metadata.Tempo = tempo;
                    break;

                case "key signature":
                case "key":
                    ParseKeySignature(value, metadata);
                    break;

                case "time signature":
                    ParseTimeSignature(value, metadata);
                    break;

                case "genre":
                    metadata.Genre = value;
                    break;

                case "instrument":
                    metadata.Instrument = value;
                    break;

                case "author":
                case "artist":
                    metadata.Author = value;
                    break;

                case "title":
                case "name":
                    metadata.Name = value;
                    break;

                case "comments":
                case "comment":
                    metadata.Comment = value;
                    break;

                case "copyright":
                    metadata.Copyright = value;
                    break;

                case "apple loops":
                    metadata.LoopType = AppleLoopType.Loop;
                    break;

                case "tags":
                case "keywords":
                    foreach (var tag in value.Split(',', ';', ' '))
                    {
                        var trimmed = tag.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            metadata.SearchTags.Add(trimmed);
                    }
                    break;
            }
        }
    }

    private static void ReadCAFUserChunk(BinaryReader reader, long chunkSize, AppleLoopsMetadata metadata)
    {
        // User data often contains Apple-specific loop information
        long endPosition = reader.BaseStream.Position + chunkSize;

        while (reader.BaseStream.Position < endPosition - 4)
        {
            string key = new string(reader.ReadChars(4));

            if (key == "loop" || key == "LOOP")
            {
                metadata.LoopType = AppleLoopType.Loop;
            }
            else if (key == "shot" || key == "SHOT")
            {
                metadata.LoopType = AppleLoopType.OneShot;
            }
        }
    }

    private static void ReadCAFInstrumentChunk(BinaryReader reader, AppleLoopsMetadata metadata)
    {
        // Base note (MIDI note number)
        float baseNote = ReadSingleBE(reader);
        metadata.Key = (int)baseNote % 12;

        // Detune (cents)
        float detune = ReadSingleBE(reader);

        // Low/high note
        float lowNote = ReadSingleBE(reader);
        float highNote = ReadSingleBE(reader);

        // Low/high velocity
        float lowVelocity = ReadSingleBE(reader);
        float highVelocity = ReadSingleBE(reader);

        // dB gain
        float dBGain = ReadSingleBE(reader);
    }

    private static AppleLoopsMetadata? ReadAIFFMetadata(BinaryReader reader, Stream stream)
    {
        var metadata = new AppleLoopsMetadata();

        // Read file size
        int fileSize = ReadInt32BE(reader);

        // Read form type
        string formType = new string(reader.ReadChars(4));
        if (formType != "AIFF" && formType != "AIFC")
            return null;

        // Read chunks
        while (stream.Position < stream.Length - 8)
        {
            string chunkId = new string(reader.ReadChars(4));
            int chunkSize = ReadInt32BE(reader);
            long chunkEnd = stream.Position + chunkSize;

            switch (chunkId)
            {
                case "ANNO":
                case "NAME":
                case "(c) ":
                    string text = new string(reader.ReadChars(chunkSize));
                    if (chunkId == "ANNO") metadata.Comment = text.TrimEnd('\0');
                    else if (chunkId == "NAME") metadata.Name = text.TrimEnd('\0');
                    else metadata.Copyright = text.TrimEnd('\0');
                    break;

                case "INST":
                    // Instrument chunk
                    metadata.Key = reader.ReadByte() % 12;
                    break;

                case "basc": // Apple Loops data
                    ReadAIFFLoopChunk(reader, chunkSize, metadata);
                    break;
            }

            stream.Position = chunkEnd;
            if (chunkSize % 2 != 0 && stream.Position < stream.Length)
                stream.Position++;
        }

        return metadata.IsAppleLoop ? metadata : null;
    }

    private static void ReadAIFFLoopChunk(BinaryReader reader, int chunkSize, AppleLoopsMetadata metadata)
    {
        metadata.LoopType = AppleLoopType.Loop;

        // Loop chunk structure varies by Apple's implementation
        // Basic structure:
        uint version = ReadUInt32BE(reader);
        uint loopLength = ReadUInt32BE(reader);
        metadata.NumBeats = (int)ReadUInt32BE(reader);

        // Additional data may include key, scale, tempo
        if (chunkSize > 12)
        {
            float tempo = ReadSingleBE(reader);
            if (tempo > 0) metadata.Tempo = tempo;
        }

        if (chunkSize > 16)
        {
            byte key = reader.ReadByte();
            byte scale = reader.ReadByte();
            metadata.Key = key % 12;
            metadata.ScaleType = (AppleLoopScaleType)scale;
        }
    }

    private static AppleLoopsMetadata? ReadWAVAppleMetadata(BinaryReader reader, Stream stream)
    {
        // Some Apple Loops may be stored as WAV with embedded metadata
        // This is less common but possible
        return null;
    }

    private static void ParseKeySignature(string value, AppleLoopsMetadata metadata)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        value = value.Trim().ToUpperInvariant();

        for (int i = 0; i < noteNames.Length; i++)
        {
            if (value.StartsWith(noteNames[i]))
            {
                metadata.Key = i;
                break;
            }
        }

        if (value.Contains("MINOR") || value.Contains("MIN") || value.EndsWith("M"))
        {
            metadata.ScaleType = AppleLoopScaleType.Minor;
        }
        else if (value.Contains("MAJOR") || value.Contains("MAJ"))
        {
            metadata.ScaleType = AppleLoopScaleType.Major;
        }
    }

    private static void ParseTimeSignature(string value, AppleLoopsMetadata metadata)
    {
        var parts = value.Split('/');
        if (parts.Length == 2)
        {
            if (int.TryParse(parts[0], out int num))
                metadata.TimeSignatureNumerator = num;
            if (int.TryParse(parts[1], out int den))
                metadata.TimeSignatureDenominator = den;
        }
    }

    private static string ReadNullTerminatedString(BinaryReader reader)
    {
        var sb = new StringBuilder();
        char c;
        while ((c = reader.ReadChar()) != '\0')
        {
            sb.Append(c);
        }
        return sb.ToString();
    }

    // Big-endian readers
    private static ushort ReadUInt16BE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(2);
        return (ushort)((bytes[0] << 8) | bytes[1]);
    }

    private static int ReadInt32BE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }

    private static uint ReadUInt32BE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
    }

    private static long ReadInt64BE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(8);
        return ((long)bytes[0] << 56) | ((long)bytes[1] << 48) | ((long)bytes[2] << 40) | ((long)bytes[3] << 32) |
               ((long)bytes[4] << 24) | ((long)bytes[5] << 16) | ((long)bytes[6] << 8) | bytes[7];
    }

    private static float ReadSingleBE(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>
    /// Checks if a file is an Apple Loop.
    /// </summary>
    public static bool IsAppleLoop(string path)
    {
        var metadata = ReadMetadata(path);
        return metadata?.IsAppleLoop ?? false;
    }
}

/// <summary>
/// Writes Apple Loops metadata to CAF files.
/// </summary>
public static class AppleLoopsWriter
{
    /// <summary>
    /// Creates Apple Loops metadata for a loop.
    /// </summary>
    /// <param name="tempo">Tempo in BPM.</param>
    /// <param name="beats">Number of beats.</param>
    /// <param name="key">Key (0-11, 0=C).</param>
    /// <param name="scaleType">Scale type.</param>
    /// <param name="genre">Genre category.</param>
    /// <param name="instrument">Instrument category.</param>
    /// <returns>Apple Loops metadata.</returns>
    public static AppleLoopsMetadata CreateLoopMetadata(
        double tempo,
        int beats,
        int key = 0,
        AppleLoopScaleType scaleType = AppleLoopScaleType.None,
        string? genre = null,
        string? instrument = null)
    {
        return new AppleLoopsMetadata
        {
            LoopType = AppleLoopType.Loop,
            Tempo = tempo,
            NumBeats = beats,
            Key = key,
            ScaleType = scaleType,
            Genre = genre ?? AppleLoopGenre.Other,
            Instrument = instrument ?? AppleLoopInstrument.Other
        };
    }

    /// <summary>
    /// Creates Apple Loops metadata for a one-shot sample.
    /// </summary>
    /// <param name="key">Root note key (0-11).</param>
    /// <param name="instrument">Instrument category.</param>
    /// <returns>Apple Loops metadata.</returns>
    public static AppleLoopsMetadata CreateOneShotMetadata(
        int key = 0,
        string? instrument = null)
    {
        return new AppleLoopsMetadata
        {
            LoopType = AppleLoopType.OneShot,
            Key = key,
            Instrument = instrument ?? AppleLoopInstrument.Other
        };
    }

    /// <summary>
    /// Creates CAF file info chunk data from metadata.
    /// </summary>
    /// <param name="metadata">Apple Loops metadata.</param>
    /// <returns>Info chunk bytes.</returns>
    public static byte[] CreateInfoChunk(AppleLoopsMetadata metadata)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);

        var entries = new List<(string Key, string Value)>
        {
            ("tempo", metadata.Tempo.ToString("F2")),
            ("time signature", $"{metadata.TimeSignatureNumerator}/{metadata.TimeSignatureDenominator}"),
            ("key", metadata.KeyName),
            ("genre", metadata.Genre),
            ("instrument", metadata.Instrument)
        };

        if (!string.IsNullOrEmpty(metadata.Name))
            entries.Add(("title", metadata.Name));
        if (!string.IsNullOrEmpty(metadata.Author))
            entries.Add(("author", metadata.Author));
        if (!string.IsNullOrEmpty(metadata.Copyright))
            entries.Add(("copyright", metadata.Copyright));
        if (!string.IsNullOrEmpty(metadata.Comment))
            entries.Add(("comments", metadata.Comment));
        if (metadata.SearchTags.Count > 0)
            entries.Add(("tags", string.Join(",", metadata.SearchTags)));

        // Write chunk
        writer.Write("info".ToCharArray());

        // Placeholder for size
        long sizePosition = stream.Position;
        WriteInt64BE(writer, 0);

        long dataStart = stream.Position;

        // Number of entries
        WriteUInt32BE(writer, (uint)entries.Count);

        // Write entries
        foreach (var (key, value) in entries)
        {
            WriteNullTerminatedString(writer, key);
            WriteNullTerminatedString(writer, value);
        }

        // Update size
        long dataEnd = stream.Position;
        stream.Position = sizePosition;
        WriteInt64BE(writer, dataEnd - dataStart);
        stream.Position = dataEnd;

        return stream.ToArray();
    }

    private static void WriteNullTerminatedString(BinaryWriter writer, string value)
    {
        writer.Write(Encoding.UTF8.GetBytes(value));
        writer.Write((byte)0);
    }

    private static void WriteInt64BE(BinaryWriter writer, long value)
    {
        writer.Write((byte)((value >> 56) & 0xFF));
        writer.Write((byte)((value >> 48) & 0xFF));
        writer.Write((byte)((value >> 40) & 0xFF));
        writer.Write((byte)((value >> 32) & 0xFF));
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private static void WriteUInt32BE(BinaryWriter writer, uint value)
    {
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }
}
