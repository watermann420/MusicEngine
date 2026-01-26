// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.IO;
using System.Text;

namespace MusicEngine.Core.Vst.Vst3.Presets;

/// <summary>
/// Contains data read from a VST3 preset file.
/// </summary>
public class VstPresetData
{
    /// <summary>
    /// The plugin class ID (GUID) this preset is for.
    /// </summary>
    public Guid ClassId { get; set; }

    /// <summary>
    /// Component/Processor state data (from "Comp" chunk).
    /// </summary>
    public byte[]? ComponentState { get; set; }

    /// <summary>
    /// Controller state data (from "Cont" chunk).
    /// </summary>
    public byte[]? ControllerState { get; set; }

    /// <summary>
    /// Preset name (from "Info" chunk).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Preset author (from "Info" chunk).
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Preset category (from "Info" chunk).
    /// </summary>
    public string? Category { get; set; }
}

/// <summary>
/// Reads VST3 .vstpreset files.
/// </summary>
/// <remarks>
/// VST3 preset format:
/// - Header: "VST3" (4 bytes) + Version (4 bytes) + Class ID (32 bytes) + Chunk List Offset (8 bytes)
/// - Chunks: "Comp" (component state), "Cont" (controller state), "Info" (metadata)
/// </remarks>
public class VstPresetReader
{
    private const string Magic = "VST3";
    private const int HeaderSize = 48; // 4 + 4 + 32 + 8

    /// <summary>
    /// Reads a VST3 preset from the specified file path.
    /// </summary>
    /// <param name="filePath">Path to the .vstpreset file.</param>
    /// <returns>Parsed preset data.</returns>
    /// <exception cref="FileNotFoundException">File does not exist.</exception>
    /// <exception cref="InvalidDataException">Invalid preset format.</exception>
    public VstPresetData Read(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Preset file not found.", filePath);
        }

        using var stream = File.OpenRead(filePath);
        return Read(stream);
    }

    /// <summary>
    /// Reads a VST3 preset from a stream.
    /// </summary>
    /// <param name="stream">Stream containing the preset data.</param>
    /// <returns>Parsed preset data.</returns>
    /// <exception cref="InvalidDataException">Invalid preset format.</exception>
    public VstPresetData Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var preset = new VstPresetData();

        // Read and validate header
        ReadHeader(reader, preset);

        // Read chunk list
        ReadChunks(reader, preset);

        return preset;
    }

    private void ReadHeader(BinaryReader reader, VstPresetData preset)
    {
        // Read magic bytes
        var magicBytes = reader.ReadBytes(4);
        var magic = Encoding.ASCII.GetString(magicBytes);

        if (magic != Magic)
        {
            throw new InvalidDataException($"Invalid VST3 preset magic. Expected 'VST3', got '{magic}'.");
        }

        // Read version (4 bytes, little endian)
        var version = reader.ReadInt32();

        // Read Class ID (32 bytes - ASCII hex representation of GUID)
        var classIdBytes = reader.ReadBytes(32);
        var classIdHex = Encoding.ASCII.GetString(classIdBytes);
        preset.ClassId = ParseClassId(classIdHex);

        // Read chunk list offset (8 bytes, little endian)
        var chunkListOffset = reader.ReadInt64();

        // Seek to chunk list if needed
        if (chunkListOffset > 0 && reader.BaseStream.Position != chunkListOffset)
        {
            reader.BaseStream.Seek(chunkListOffset, SeekOrigin.Begin);
        }
    }

    private void ReadChunks(BinaryReader reader, VstPresetData preset)
    {
        // Read chunk list ID
        var listIdBytes = reader.ReadBytes(4);
        var listId = Encoding.ASCII.GetString(listIdBytes);

        if (listId != "List")
        {
            throw new InvalidDataException($"Invalid chunk list ID. Expected 'List', got '{listId}'.");
        }

        // Read chunk count
        var chunkCount = reader.ReadInt32();

        // Read chunk entries
        var chunkEntries = new (string Id, long Offset, long Size)[chunkCount];
        for (int i = 0; i < chunkCount; i++)
        {
            var chunkIdBytes = reader.ReadBytes(4);
            var chunkId = Encoding.ASCII.GetString(chunkIdBytes);
            var offset = reader.ReadInt64();
            var size = reader.ReadInt64();
            chunkEntries[i] = (chunkId, offset, size);
        }

        // Read each chunk
        foreach (var (id, offset, size) in chunkEntries)
        {
            reader.BaseStream.Seek(offset, SeekOrigin.Begin);
            ReadChunk(reader, preset, id, size);
        }
    }

    private void ReadChunk(BinaryReader reader, VstPresetData preset, string chunkId, long size)
    {
        switch (chunkId)
        {
            case "Comp":
                preset.ComponentState = reader.ReadBytes((int)size);
                break;

            case "Cont":
                preset.ControllerState = reader.ReadBytes((int)size);
                break;

            case "Info":
                ReadInfoChunk(reader, preset, size);
                break;

            default:
                // Skip unknown chunks
                reader.BaseStream.Seek(size, SeekOrigin.Current);
                break;
        }
    }

    private void ReadInfoChunk(BinaryReader reader, VstPresetData preset, long size)
    {
        var endPosition = reader.BaseStream.Position + size;

        while (reader.BaseStream.Position < endPosition)
        {
            // Read attribute ID (4 bytes)
            var attrIdBytes = reader.ReadBytes(4);
            if (attrIdBytes.Length < 4)
                break;

            var attrId = Encoding.ASCII.GetString(attrIdBytes);

            // Read attribute size (8 bytes)
            var attrSize = reader.ReadInt64();

            // Read attribute value
            var valueBytes = reader.ReadBytes((int)attrSize);
            var value = Encoding.UTF8.GetString(valueBytes).TrimEnd('\0');

            switch (attrId)
            {
                case "Name":
                    preset.Name = value;
                    break;
                case "Auth":
                    preset.Author = value;
                    break;
                case "Catg":
                    preset.Category = value;
                    break;
            }
        }
    }

    private static Guid ParseClassId(string hexString)
    {
        // Class ID is stored as 32 hex characters (uppercase)
        if (hexString.Length != 32)
        {
            throw new InvalidDataException($"Invalid class ID length: {hexString.Length}");
        }

        var bytes = new byte[16];
        for (int i = 0; i < 16; i++)
        {
            bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
        }

        // Convert from VST3 byte order to .NET Guid
        // VST3 uses big-endian for the first 3 groups
        return new Guid(
            (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3],
            (short)((bytes[4] << 8) | bytes[5]),
            (short)((bytes[6] << 8) | bytes[7]),
            bytes[8], bytes[9], bytes[10], bytes[11],
            bytes[12], bytes[13], bytes[14], bytes[15]
        );
    }
}
