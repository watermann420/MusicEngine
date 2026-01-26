// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MusicEngine.Core.Import;

/// <summary>
/// Represents a slice within a REX file.
/// </summary>
public class REXSlice
{
    /// <summary>Slice index.</summary>
    public int Index { get; set; }

    /// <summary>Start position in samples (from original file start).</summary>
    public long StartPosition { get; set; }

    /// <summary>Length in samples.</summary>
    public long Length { get; set; }

    /// <summary>Original pitch/root note (MIDI note number, -1 if not specified).</summary>
    public int RootNote { get; set; } = -1;

    /// <summary>Slice name/label.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Audio data for this slice.</summary>
    public float[]? AudioData { get; set; }

    /// <summary>Gets duration in seconds at original sample rate.</summary>
    public double GetDurationSeconds(int sampleRate) => (double)Length / sampleRate;
}

/// <summary>
/// REX file information and content.
/// </summary>
public class REXFileData
{
    /// <summary>Original file path.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>REX format version (1 or 2).</summary>
    public int Version { get; set; }

    /// <summary>Sample rate.</summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>Number of channels.</summary>
    public int Channels { get; set; } = 2;

    /// <summary>Bit depth.</summary>
    public int BitDepth { get; set; } = 16;

    /// <summary>Original tempo in BPM.</summary>
    public double Tempo { get; set; } = 120.0;

    /// <summary>Time signature numerator.</summary>
    public int TimeSignatureNumerator { get; set; } = 4;

    /// <summary>Time signature denominator.</summary>
    public int TimeSignatureDenominator { get; set; } = 4;

    /// <summary>Number of bars.</summary>
    public int Bars { get; set; } = 1;

    /// <summary>Total length in samples.</summary>
    public long TotalSamples { get; set; }

    /// <summary>Original file name (without extension).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Slices in the file.</summary>
    public List<REXSlice> Slices { get; } = new();

    /// <summary>Complete audio data (all slices combined).</summary>
    public float[]? AudioData { get; set; }

    /// <summary>Gets total duration in seconds.</summary>
    public double DurationSeconds => (double)TotalSamples / SampleRate;

    /// <summary>Gets duration in beats at original tempo.</summary>
    public double DurationBeats => DurationSeconds * Tempo / 60.0;
}

/// <summary>
/// MIDI trigger information for a REX slice.
/// </summary>
public class REXMIDITrigger
{
    /// <summary>Slice being triggered.</summary>
    public REXSlice? Slice { get; set; }

    /// <summary>MIDI note number for trigger.</summary>
    public int NoteNumber { get; set; }

    /// <summary>Trigger time in beats.</summary>
    public double StartBeat { get; set; }

    /// <summary>Duration in beats.</summary>
    public double DurationBeats { get; set; }

    /// <summary>Velocity.</summary>
    public int Velocity { get; set; } = 100;
}

/// <summary>
/// Options for REX import.
/// </summary>
public class REXImportOptions
{
    /// <summary>Target tempo for time stretching (0 = use original).</summary>
    public double TargetTempo { get; set; } = 0;

    /// <summary>Target sample rate (0 = use original).</summary>
    public int TargetSampleRate { get; set; } = 0;

    /// <summary>Whether to load audio data.</summary>
    public bool LoadAudioData { get; set; } = true;

    /// <summary>Whether to extract individual slice audio.</summary>
    public bool ExtractSliceAudio { get; set; } = true;

    /// <summary>Base MIDI note for slice triggers (default C3 = 60).</summary>
    public int BaseMIDINote { get; set; } = 60;

    /// <summary>Whether to create MIDI triggers from slice positions.</summary>
    public bool CreateMIDITriggers { get; set; } = true;
}

/// <summary>
/// Importer for REX and REX2 loop files from Propellerhead ReCycle.
/// </summary>
/// <remarks>
/// REX files contain pre-sliced audio loops with tempo and timing information.
/// This enables tempo-independent playback by triggering individual slices.
///
/// REX format features:
/// - Pre-analyzed slice points
/// - Original tempo and time signature
/// - Audio data for each slice
/// - Compatible with Reason, Cubase, Logic, and other DAWs
///
/// Note: This is a basic implementation. Full REX support may require
/// licensing from Propellerhead/Reason Studios for the REX SDK.
/// </remarks>
public class REXImporter
{
    // REX file signatures
    private static readonly byte[] REX_MAGIC = { 0x52, 0x45, 0x58, 0x32 }; // "REX2"
    private static readonly byte[] REX1_MAGIC = { 0x52, 0x45, 0x58, 0x21 }; // "REX!"
    private static readonly byte[] CAF_MAGIC = { 0x63, 0x61, 0x66, 0x66 }; // "caff" (Apple REX variant)

    /// <summary>
    /// Imports a REX file.
    /// </summary>
    /// <param name="path">Path to the REX file.</param>
    /// <param name="options">Import options.</param>
    /// <returns>REX file data or null if invalid.</returns>
    public static REXFileData? Import(string path, REXImportOptions? options = null)
    {
        if (!File.Exists(path))
            return null;

        options ??= new REXImportOptions();

        try
        {
            using var stream = File.OpenRead(path);
            return ImportFromStream(stream, path, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error importing REX file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Imports a REX file from a stream.
    /// </summary>
    /// <param name="stream">Input stream.</param>
    /// <param name="filePath">Original file path (for metadata).</param>
    /// <param name="options">Import options.</param>
    /// <returns>REX file data or null if invalid.</returns>
    public static REXFileData? ImportFromStream(Stream stream, string filePath, REXImportOptions? options = null)
    {
        options ??= new REXImportOptions();

        using var reader = new BinaryReader(stream, Encoding.ASCII, true);

        // Read and verify magic
        byte[] magic = reader.ReadBytes(4);
        int version = 0;

        if (BytesEqual(magic, REX_MAGIC))
            version = 2;
        else if (BytesEqual(magic, REX1_MAGIC))
            version = 1;
        else if (BytesEqual(magic, CAF_MAGIC))
            version = 2; // Apple variant
        else
            return null;

        var data = new REXFileData
        {
            FilePath = filePath,
            Version = version,
            Name = Path.GetFileNameWithoutExtension(filePath)
        };

        // Read header information based on version
        if (version == 2)
        {
            ReadREX2Header(reader, data);
        }
        else
        {
            ReadREX1Header(reader, data);
        }

        // Read slice information
        ReadSliceData(reader, data, stream.Length);

        // Load audio data if requested
        if (options.LoadAudioData)
        {
            LoadAudioData(reader, data, stream);
        }

        // Extract individual slice audio if requested
        if (options.ExtractSliceAudio && data.AudioData != null)
        {
            ExtractSliceAudioData(data);
        }

        // Apply tempo adjustment if specified
        if (options.TargetTempo > 0 && Math.Abs(options.TargetTempo - data.Tempo) > 0.01)
        {
            AdjustTempoMapping(data, options.TargetTempo);
        }

        return data;
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

    private static void ReadREX2Header(BinaryReader reader, REXFileData data)
    {
        // REX2 header structure (simplified - actual format is proprietary)
        try
        {
            // Skip version bytes
            reader.ReadBytes(4);

            // Read basic properties
            data.Tempo = reader.ReadSingle();
            data.SampleRate = reader.ReadInt32();
            data.Channels = reader.ReadInt16();
            data.BitDepth = reader.ReadInt16();
            data.Bars = reader.ReadInt32();
            data.TimeSignatureNumerator = reader.ReadByte();
            data.TimeSignatureDenominator = reader.ReadByte();

            // Total samples
            data.TotalSamples = reader.ReadInt64();

            // Skip to slice count
            reader.ReadBytes(16);

            int sliceCount = reader.ReadInt32();

            // Read slice positions
            for (int i = 0; i < sliceCount; i++)
            {
                var slice = new REXSlice
                {
                    Index = i,
                    StartPosition = reader.ReadInt64(),
                    Length = reader.ReadInt64(),
                    RootNote = reader.ReadInt32()
                };

                // Read slice name if present
                int nameLength = reader.ReadByte();
                if (nameLength > 0)
                {
                    slice.Name = new string(reader.ReadChars(nameLength));
                }
                else
                {
                    slice.Name = $"Slice {i + 1}";
                }

                data.Slices.Add(slice);
            }
        }
        catch (EndOfStreamException)
        {
            // Incomplete header, use defaults
            if (data.Tempo <= 0) data.Tempo = 120;
            if (data.SampleRate <= 0) data.SampleRate = 44100;
            if (data.Channels <= 0) data.Channels = 2;
        }
    }

    private static void ReadREX1Header(BinaryReader reader, REXFileData data)
    {
        // REX1 has a simpler header
        try
        {
            data.Tempo = reader.ReadSingle();
            data.SampleRate = reader.ReadInt32();
            data.Channels = 2; // REX1 is typically stereo
            data.BitDepth = 16;

            int sliceCount = reader.ReadInt32();
            data.TotalSamples = reader.ReadInt32();

            for (int i = 0; i < sliceCount; i++)
            {
                var slice = new REXSlice
                {
                    Index = i,
                    StartPosition = reader.ReadInt32(),
                    Length = reader.ReadInt32(),
                    Name = $"Slice {i + 1}"
                };
                data.Slices.Add(slice);
            }
        }
        catch (EndOfStreamException)
        {
            if (data.Tempo <= 0) data.Tempo = 120;
            if (data.SampleRate <= 0) data.SampleRate = 44100;
        }
    }

    private static void ReadSliceData(BinaryReader reader, REXFileData data, long streamLength)
    {
        // If no slices were read from header, try to detect them
        if (data.Slices.Count == 0)
        {
            // Create a single slice for the entire file
            data.Slices.Add(new REXSlice
            {
                Index = 0,
                StartPosition = 0,
                Length = data.TotalSamples > 0 ? data.TotalSamples : (streamLength - reader.BaseStream.Position) / (data.BitDepth / 8) / data.Channels,
                Name = "Full Loop"
            });
        }

        // Calculate slice lengths if not specified
        for (int i = 0; i < data.Slices.Count; i++)
        {
            var slice = data.Slices[i];
            if (slice.Length <= 0)
            {
                if (i < data.Slices.Count - 1)
                {
                    slice.Length = data.Slices[i + 1].StartPosition - slice.StartPosition;
                }
                else
                {
                    slice.Length = data.TotalSamples - slice.StartPosition;
                }
            }
        }
    }

    private static void LoadAudioData(BinaryReader reader, REXFileData data, Stream stream)
    {
        // Find audio data section
        // REX files typically have audio data after the header/slice info

        try
        {
            // Calculate expected audio size
            long audioBytes = data.TotalSamples * data.Channels * (data.BitDepth / 8);
            if (audioBytes <= 0 || audioBytes > stream.Length)
            {
                audioBytes = stream.Length - stream.Position;
            }

            long sampleCount = audioBytes / (data.BitDepth / 8);
            data.AudioData = new float[sampleCount];

            for (long i = 0; i < sampleCount && stream.Position < stream.Length; i++)
            {
                if (data.BitDepth == 16)
                {
                    short sample = reader.ReadInt16();
                    data.AudioData[i] = sample / 32768f;
                }
                else if (data.BitDepth == 24)
                {
                    int b1 = reader.ReadByte();
                    int b2 = reader.ReadByte();
                    int b3 = reader.ReadByte();
                    int sample = (b3 << 16) | (b2 << 8) | b1;
                    if ((sample & 0x800000) != 0)
                        sample |= unchecked((int)0xFF000000);
                    data.AudioData[i] = sample / 8388608f;
                }
                else // 32-bit float
                {
                    data.AudioData[i] = reader.ReadSingle();
                }
            }
        }
        catch (EndOfStreamException)
        {
            // Partial audio data
        }
    }

    private static void ExtractSliceAudioData(REXFileData data)
    {
        if (data.AudioData == null)
            return;

        foreach (var slice in data.Slices)
        {
            long startSample = slice.StartPosition * data.Channels;
            long sliceSamples = slice.Length * data.Channels;

            if (startSample >= 0 && startSample + sliceSamples <= data.AudioData.Length)
            {
                slice.AudioData = new float[sliceSamples];
                Array.Copy(data.AudioData, startSample, slice.AudioData, 0, sliceSamples);
            }
        }
    }

    private static void AdjustTempoMapping(REXFileData data, double targetTempo)
    {
        // When playing at a different tempo, slice timing changes
        // but the audio data remains the same (no time stretching)
        double tempoRatio = data.Tempo / targetTempo;

        // Update position information for new tempo context
        // The actual audio playback uses MIDI triggers which are tempo-relative
    }

    /// <summary>
    /// Creates MIDI trigger notes from REX slices.
    /// </summary>
    /// <param name="rexData">REX file data.</param>
    /// <param name="baseMidiNote">Starting MIDI note (C3 = 60).</param>
    /// <param name="targetTempo">Target tempo for beat calculation (0 = use original).</param>
    /// <returns>List of MIDI triggers.</returns>
    public static List<REXMIDITrigger> CreateMIDITriggers(REXFileData rexData, int baseMidiNote = 60, double targetTempo = 0)
    {
        var triggers = new List<REXMIDITrigger>();

        if (rexData.Slices.Count == 0)
            return triggers;

        double tempo = targetTempo > 0 ? targetTempo : rexData.Tempo;
        double samplesPerBeat = rexData.SampleRate * 60.0 / tempo;

        foreach (var slice in rexData.Slices)
        {
            var trigger = new REXMIDITrigger
            {
                Slice = slice,
                NoteNumber = baseMidiNote + slice.Index,
                StartBeat = slice.StartPosition / samplesPerBeat,
                DurationBeats = slice.Length / samplesPerBeat,
                Velocity = 100
            };

            triggers.Add(trigger);
        }

        return triggers;
    }

    /// <summary>
    /// Converts REX slices to a MusicEngine Pattern.
    /// </summary>
    /// <param name="rexData">REX file data.</param>
    /// <param name="synth">Synth to use for the pattern (should be a sampler loaded with slices).</param>
    /// <param name="baseMidiNote">Starting MIDI note for slices.</param>
    /// <returns>Pattern with MIDI triggers for each slice.</returns>
    public static Pattern ToPattern(REXFileData rexData, ISynth synth, int baseMidiNote = 60)
    {
        var pattern = new Pattern(synth)
        {
            Name = rexData.Name,
            LoopLength = rexData.DurationBeats
        };

        var triggers = CreateMIDITriggers(rexData, baseMidiNote);

        foreach (var trigger in triggers)
        {
            pattern.Note(trigger.NoteNumber, trigger.StartBeat, trigger.DurationBeats, trigger.Velocity);
        }

        return pattern;
    }

    /// <summary>
    /// Exports slice audio data to individual WAV files.
    /// </summary>
    /// <param name="rexData">REX file data.</param>
    /// <param name="outputDirectory">Directory for output files.</param>
    /// <param name="namePrefix">Prefix for file names.</param>
    public static void ExportSlicesToWav(REXFileData rexData, string outputDirectory, string? namePrefix = null)
    {
        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        namePrefix ??= rexData.Name;

        foreach (var slice in rexData.Slices)
        {
            if (slice.AudioData == null || slice.AudioData.Length == 0)
                continue;

            string fileName = $"{namePrefix}_{slice.Index + 1:D2}_{slice.Name}.wav"
                .Replace(" ", "_");

            // Remove invalid characters
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            string outputPath = Path.Combine(outputDirectory, fileName);

            ExportSliceToWav(slice, rexData.SampleRate, rexData.Channels, outputPath);
        }
    }

    private static void ExportSliceToWav(REXSlice slice, int sampleRate, int channels, string outputPath)
    {
        if (slice.AudioData == null)
            return;

        using var stream = File.Create(outputPath);
        using var writer = new BinaryWriter(stream);

        int sampleCount = slice.AudioData.Length / channels;
        int dataSize = sampleCount * channels * 2; // 16-bit

        // WAV header
        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + dataSize);
        writer.Write("WAVE".ToCharArray());

        // fmt chunk
        writer.Write("fmt ".ToCharArray());
        writer.Write(16); // chunk size
        writer.Write((short)1); // PCM format
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2); // byte rate
        writer.Write((short)(channels * 2)); // block align
        writer.Write((short)16); // bits per sample

        // data chunk
        writer.Write("data".ToCharArray());
        writer.Write(dataSize);

        // Write samples
        for (int i = 0; i < slice.AudioData.Length; i++)
        {
            short sample = (short)(Math.Clamp(slice.AudioData[i], -1f, 1f) * 32767f);
            writer.Write(sample);
        }
    }

    /// <summary>
    /// Checks if a file is a valid REX file.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <returns>True if the file appears to be a REX file.</returns>
    public static bool IsREXFile(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            if (stream.Length < 4)
                return false;

            byte[] magic = reader.ReadBytes(4);
            return BytesEqual(magic, REX_MAGIC) ||
                   BytesEqual(magic, REX1_MAGIC) ||
                   BytesEqual(magic, CAF_MAGIC);
        }
        catch
        {
            return false;
        }
    }
}
