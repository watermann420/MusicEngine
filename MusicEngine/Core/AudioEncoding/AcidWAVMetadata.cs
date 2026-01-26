// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio encoding/export component.

using System;
using System.IO;
using System.Text;

namespace MusicEngine.Core.AudioEncoding;

/// <summary>
/// ACID loop type flags.
/// </summary>
[Flags]
public enum AcidLoopType
{
    /// <summary>No loop information.</summary>
    None = 0,

    /// <summary>One-shot sample (not looped).</summary>
    OneShot = 0x01,

    /// <summary>Root note information is valid.</summary>
    RootNoteValid = 0x02,

    /// <summary>Time stretch information is valid.</summary>
    StretchValid = 0x04,

    /// <summary>Disk-based sample.</summary>
    DiskBased = 0x08,

    /// <summary>8-bit sample (vs 16-bit).</summary>
    Sample8Bit = 0x10,

    /// <summary>Acidized (processed for ACID compatibility).</summary>
    Acidized = 0x20
}

/// <summary>
/// ACID stretch mode types.
/// </summary>
public enum AcidStretchMode
{
    /// <summary>Normal stretching (default).</summary>
    Normal = 0,

    /// <summary>Optimize for vocal content.</summary>
    Vocal = 1,

    /// <summary>Optimize for percussion/drums.</summary>
    Percussion = 2,

    /// <summary>Optimize for bass sounds.</summary>
    Bass = 3,

    /// <summary>No time stretching (pitch shifts with tempo).</summary>
    NoStretch = 4
}

/// <summary>
/// Contains ACID WAV loop metadata.
/// </summary>
public class AcidWAVMetadata
{
    /// <summary>Loop type flags.</summary>
    public AcidLoopType LoopType { get; set; } = AcidLoopType.None;

    /// <summary>Root note (MIDI note number, 60 = C4).</summary>
    public int RootNote { get; set; } = 60;

    /// <summary>Fine tune adjustment in cents (-50 to +50).</summary>
    public int FineTune { get; set; } = 0;

    /// <summary>Number of beats in the loop.</summary>
    public int NumBeats { get; set; } = 4;

    /// <summary>Time signature numerator.</summary>
    public int TimeSignatureNumerator { get; set; } = 4;

    /// <summary>Time signature denominator.</summary>
    public int TimeSignatureDenominator { get; set; } = 4;

    /// <summary>Original tempo in BPM (calculated from beats and duration).</summary>
    public double Tempo { get; set; } = 120.0;

    /// <summary>Stretch mode for time stretching.</summary>
    public AcidStretchMode StretchMode { get; set; } = AcidStretchMode.Normal;

    /// <summary>Number of slices/transients in the loop.</summary>
    public int NumSlices { get; set; } = 0;

    /// <summary>Gets whether this is a one-shot (non-looping) sample.</summary>
    public bool IsOneShot => (LoopType & AcidLoopType.OneShot) != 0;

    /// <summary>Gets whether root note information is valid.</summary>
    public bool HasRootNote => (LoopType & AcidLoopType.RootNoteValid) != 0;

    /// <summary>Gets whether stretch information is valid.</summary>
    public bool HasStretchInfo => (LoopType & AcidLoopType.StretchValid) != 0;

    /// <summary>Gets whether the file has been acidized.</summary>
    public bool IsAcidized => (LoopType & AcidLoopType.Acidized) != 0;

    /// <summary>
    /// Gets the musical key as a string.
    /// </summary>
    public string KeyName
    {
        get
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int noteIndex = RootNote % 12;
            int octave = (RootNote / 12) - 1;
            return $"{noteNames[noteIndex]}{octave}";
        }
    }

    /// <summary>
    /// Creates ACID metadata from WAV file duration.
    /// </summary>
    /// <param name="durationSeconds">Audio duration in seconds.</param>
    /// <param name="beats">Number of beats.</param>
    /// <returns>Calculated tempo.</returns>
    public double CalculateTempo(double durationSeconds, int beats)
    {
        if (durationSeconds <= 0 || beats <= 0)
            return 120.0;

        Tempo = (beats / durationSeconds) * 60.0;
        NumBeats = beats;
        return Tempo;
    }

    /// <summary>
    /// Calculates expected duration at target tempo.
    /// </summary>
    /// <param name="targetTempo">Target tempo in BPM.</param>
    /// <param name="originalDuration">Original duration in seconds.</param>
    /// <returns>Duration at target tempo.</returns>
    public double GetDurationAtTempo(double targetTempo, double originalDuration)
    {
        if (targetTempo <= 0 || Tempo <= 0)
            return originalDuration;

        return originalDuration * (Tempo / targetTempo);
    }
}

/// <summary>
/// Reads and writes ACID loop metadata in WAV files.
/// </summary>
/// <remarks>
/// ACID WAV metadata is stored in a custom "acid" chunk within the WAV file.
/// This metadata enables tempo-synced playback in Sony ACID, FL Studio,
/// Ableton Live, and other DAWs that support ACID loops.
///
/// The "acid" chunk structure (32 bytes):
/// - Offset 0: Type flags (4 bytes)
/// - Offset 4: Root note (2 bytes)
/// - Offset 6: Unknown (2 bytes)
/// - Offset 8: Unknown (4 bytes)
/// - Offset 12: Number of beats (4 bytes)
/// - Offset 16: Meter denominator (2 bytes)
/// - Offset 18: Meter numerator (2 bytes)
/// - Offset 20: Tempo (4 bytes, float)
/// </remarks>
public static class AcidWAVReader
{
    private const string ACID_CHUNK_ID = "acid";
    private const int ACID_CHUNK_SIZE = 24;

    /// <summary>
    /// Reads ACID metadata from a WAV file.
    /// </summary>
    /// <param name="path">Path to the WAV file.</param>
    /// <returns>ACID metadata or null if not found.</returns>
    public static AcidWAVMetadata? ReadMetadata(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            return ReadMetadata(stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading ACID metadata: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Reads ACID metadata from a stream.
    /// </summary>
    /// <param name="stream">WAV file stream.</param>
    /// <returns>ACID metadata or null if not found.</returns>
    public static AcidWAVMetadata? ReadMetadata(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, true);

        // Verify RIFF header
        string riff = new string(reader.ReadChars(4));
        if (riff != "RIFF")
            return null;

        reader.ReadInt32(); // file size
        string wave = new string(reader.ReadChars(4));
        if (wave != "WAVE")
            return null;

        // Search for acid chunk
        while (stream.Position < stream.Length - 8)
        {
            string chunkId = new string(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();

            if (chunkId == ACID_CHUNK_ID)
            {
                return ReadAcidChunk(reader, chunkSize);
            }

            // Skip to next chunk
            stream.Position += chunkSize;
            if (chunkSize % 2 != 0 && stream.Position < stream.Length)
                stream.Position++;
        }

        return null;
    }

    private static AcidWAVMetadata ReadAcidChunk(BinaryReader reader, int chunkSize)
    {
        var metadata = new AcidWAVMetadata();

        if (chunkSize < ACID_CHUNK_SIZE)
            return metadata;

        // Read acid chunk data
        uint typeFlags = reader.ReadUInt32();
        metadata.LoopType = (AcidLoopType)(typeFlags & 0xFF);
        metadata.StretchMode = (AcidStretchMode)((typeFlags >> 8) & 0xFF);

        ushort rootNote = reader.ReadUInt16();
        metadata.RootNote = rootNote & 0xFF;
        metadata.FineTune = (rootNote >> 8) - 128; // Convert to signed

        reader.ReadUInt16(); // Unknown/reserved

        reader.ReadUInt32(); // Unknown/reserved

        metadata.NumBeats = reader.ReadInt32();

        metadata.TimeSignatureDenominator = reader.ReadUInt16();
        metadata.TimeSignatureNumerator = reader.ReadUInt16();

        metadata.Tempo = reader.ReadSingle();

        // Read number of slices if chunk is large enough
        if (chunkSize >= ACID_CHUNK_SIZE + 4)
        {
            metadata.NumSlices = reader.ReadInt32();
        }

        return metadata;
    }

    /// <summary>
    /// Checks if a WAV file contains ACID metadata.
    /// </summary>
    /// <param name="path">Path to the WAV file.</param>
    /// <returns>True if ACID metadata is present.</returns>
    public static bool HasAcidMetadata(string path)
    {
        return ReadMetadata(path) != null;
    }

    /// <summary>
    /// Detects if a WAV file is a one-shot or a loop.
    /// </summary>
    /// <param name="path">Path to the WAV file.</param>
    /// <returns>True if one-shot, false if loop, null if no ACID data.</returns>
    public static bool? IsOneShot(string path)
    {
        var metadata = ReadMetadata(path);
        return metadata?.IsOneShot;
    }
}

/// <summary>
/// Writes ACID metadata to WAV files.
/// </summary>
public static class AcidWAVWriter
{
    private const string ACID_CHUNK_ID = "acid";

    /// <summary>
    /// Writes ACID metadata to an existing WAV file.
    /// </summary>
    /// <param name="path">Path to the WAV file.</param>
    /// <param name="metadata">ACID metadata to write.</param>
    /// <returns>True if successful.</returns>
    public static bool WriteMetadata(string path, AcidWAVMetadata metadata)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            // Read existing file
            byte[] existingData = File.ReadAllBytes(path);

            using var inputStream = new MemoryStream(existingData);
            using var reader = new BinaryReader(inputStream, Encoding.ASCII);

            // Verify RIFF/WAVE
            string riff = new string(reader.ReadChars(4));
            if (riff != "RIFF")
                return false;

            int originalFileSize = reader.ReadInt32();
            string wave = new string(reader.ReadChars(4));
            if (wave != "WAVE")
                return false;

            // Check if acid chunk already exists
            long acidChunkPosition = -1;
            int acidChunkSize = 0;

            while (inputStream.Position < inputStream.Length - 8)
            {
                long chunkStart = inputStream.Position;
                string chunkId = new string(reader.ReadChars(4));
                int chunkSize = reader.ReadInt32();

                if (chunkId == ACID_CHUNK_ID)
                {
                    acidChunkPosition = chunkStart;
                    acidChunkSize = chunkSize;
                    break;
                }

                inputStream.Position += chunkSize;
                if (chunkSize % 2 != 0 && inputStream.Position < inputStream.Length)
                    inputStream.Position++;
            }

            // Create acid chunk data
            byte[] acidData = CreateAcidChunk(metadata);

            // Write new file
            using var outputStream = new MemoryStream();
            using var writer = new BinaryWriter(outputStream, Encoding.ASCII);

            // Write RIFF header (size will be updated later)
            writer.Write("RIFF".ToCharArray());
            writer.Write(0); // Placeholder for file size
            writer.Write("WAVE".ToCharArray());

            inputStream.Position = 12; // After RIFF/WAVE header

            // Copy chunks, replacing or adding acid chunk
            bool acidWritten = false;

            while (inputStream.Position < existingData.Length - 8)
            {
                long chunkStart = inputStream.Position;
                string chunkId = new string(reader.ReadChars(4));
                int chunkSize = reader.ReadInt32();

                if (chunkId == ACID_CHUNK_ID)
                {
                    // Replace existing acid chunk
                    writer.Write(acidData);
                    acidWritten = true;
                    inputStream.Position += chunkSize;
                }
                else
                {
                    // Copy existing chunk
                    inputStream.Position = chunkStart;
                    byte[] chunkData = reader.ReadBytes(8 + chunkSize);
                    writer.Write(chunkData);
                }

                if (chunkSize % 2 != 0 && inputStream.Position < existingData.Length)
                {
                    if (chunkId != ACID_CHUNK_ID)
                        writer.Write(reader.ReadByte());
                    else
                        inputStream.Position++;
                }
            }

            // Add acid chunk if not already written
            if (!acidWritten)
            {
                // Find position after fmt chunk but before data chunk
                // For simplicity, append before the last chunk or at the end
                writer.Write(acidData);
            }

            // Update file size
            int newFileSize = (int)outputStream.Length - 8;
            outputStream.Position = 4;
            writer.Write(newFileSize);

            // Write to file
            File.WriteAllBytes(path, outputStream.ToArray());
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing ACID metadata: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Creates ACID metadata for new WAV file creation.
    /// </summary>
    /// <param name="metadata">ACID metadata.</param>
    /// <returns>Complete acid chunk data including header.</returns>
    public static byte[] CreateAcidChunk(AcidWAVMetadata metadata)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Chunk header
        writer.Write(ACID_CHUNK_ID.ToCharArray());
        writer.Write(24); // Chunk size

        // Type flags and stretch mode
        uint typeFlags = (uint)metadata.LoopType | ((uint)metadata.StretchMode << 8);
        writer.Write(typeFlags);

        // Root note and fine tune
        ushort rootNoteData = (ushort)(metadata.RootNote | ((metadata.FineTune + 128) << 8));
        writer.Write(rootNoteData);

        // Reserved
        writer.Write((ushort)0);
        writer.Write((uint)0);

        // Number of beats
        writer.Write(metadata.NumBeats);

        // Time signature
        writer.Write((ushort)metadata.TimeSignatureDenominator);
        writer.Write((ushort)metadata.TimeSignatureNumerator);

        // Tempo
        writer.Write((float)metadata.Tempo);

        return stream.ToArray();
    }

    /// <summary>
    /// Creates metadata for a one-shot sample.
    /// </summary>
    /// <param name="rootNote">Root note (MIDI number).</param>
    /// <returns>ACID metadata configured for one-shot.</returns>
    public static AcidWAVMetadata CreateOneShotMetadata(int rootNote = 60)
    {
        return new AcidWAVMetadata
        {
            LoopType = AcidLoopType.OneShot | AcidLoopType.RootNoteValid,
            RootNote = rootNote,
            NumBeats = 0,
            Tempo = 0
        };
    }

    /// <summary>
    /// Creates metadata for a loop.
    /// </summary>
    /// <param name="tempo">Loop tempo in BPM.</param>
    /// <param name="beats">Number of beats.</param>
    /// <param name="rootNote">Root note (optional).</param>
    /// <param name="stretchMode">Stretch mode.</param>
    /// <returns>ACID metadata configured for loop.</returns>
    public static AcidWAVMetadata CreateLoopMetadata(
        double tempo,
        int beats,
        int? rootNote = null,
        AcidStretchMode stretchMode = AcidStretchMode.Normal)
    {
        var metadata = new AcidWAVMetadata
        {
            LoopType = AcidLoopType.StretchValid | AcidLoopType.Acidized,
            Tempo = tempo,
            NumBeats = beats,
            StretchMode = stretchMode
        };

        if (rootNote.HasValue)
        {
            metadata.LoopType |= AcidLoopType.RootNoteValid;
            metadata.RootNote = rootNote.Value;
        }

        return metadata;
    }

    /// <summary>
    /// Calculates and creates loop metadata from audio duration.
    /// </summary>
    /// <param name="durationSeconds">Audio duration in seconds.</param>
    /// <param name="targetTempo">Target tempo in BPM.</param>
    /// <param name="beatsPerBar">Beats per bar (default 4).</param>
    /// <returns>ACID metadata with calculated beat count.</returns>
    public static AcidWAVMetadata CreateLoopMetadataFromDuration(
        double durationSeconds,
        double targetTempo,
        int beatsPerBar = 4)
    {
        // Calculate number of beats
        double beatsExact = (durationSeconds * targetTempo) / 60.0;
        int beats = (int)Math.Round(beatsExact / beatsPerBar) * beatsPerBar;
        if (beats < beatsPerBar) beats = beatsPerBar;

        // Recalculate tempo to match exact beat count
        double adjustedTempo = (beats / durationSeconds) * 60.0;

        return CreateLoopMetadata(adjustedTempo, beats);
    }
}
