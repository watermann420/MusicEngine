// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MusicEngine.Core.Vst.Vst3.Presets;

/// <summary>
/// Writes VST3 .vstpreset files.
/// </summary>
/// <remarks>
/// VST3 preset format:
/// - Header: "VST3" (4 bytes) + Version (4 bytes) + Class ID (32 bytes) + Chunk List Offset (8 bytes)
/// - Chunks: "Comp" (component state), "Cont" (controller state), "Info" (metadata)
/// </remarks>
public class VstPresetWriter
{
    private const string Magic = "VST3";
    private const int Version = 1;
    private const int HeaderSize = 48; // 4 + 4 + 32 + 8

    /// <summary>
    /// Writes a VST3 preset to the specified file path.
    /// </summary>
    /// <param name="filePath">Path for the .vstpreset file.</param>
    /// <param name="preset">Preset data to write.</param>
    public void Write(string filePath, VstPresetData preset)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(filePath);
        Write(stream, preset);
    }

    /// <summary>
    /// Writes a VST3 preset to a stream.
    /// </summary>
    /// <param name="stream">Stream to write the preset to.</param>
    /// <param name="preset">Preset data to write.</param>
    public void Write(Stream stream, VstPresetData preset)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        // Build chunks first to calculate offsets
        var chunks = BuildChunks(preset);

        // Calculate chunk list offset (after header and all chunk data)
        long chunkListOffset = HeaderSize;
        foreach (var chunk in chunks)
        {
            chunkListOffset += chunk.Data.Length;
        }

        // Write header
        WriteHeader(writer, preset.ClassId, chunkListOffset);

        // Track chunk offsets as we write data
        var chunkOffsets = new List<(string Id, long Offset, long Size)>();
        long currentOffset = HeaderSize;

        // Write chunk data
        foreach (var chunk in chunks)
        {
            chunkOffsets.Add((chunk.Id, currentOffset, chunk.Data.Length));
            writer.Write(chunk.Data);
            currentOffset += chunk.Data.Length;
        }

        // Write chunk list
        WriteChunkList(writer, chunkOffsets);
    }

    private void WriteHeader(BinaryWriter writer, Guid classId, long chunkListOffset)
    {
        // Magic bytes
        writer.Write(Encoding.ASCII.GetBytes(Magic));

        // Version
        writer.Write(Version);

        // Class ID (32 bytes ASCII hex)
        var classIdHex = FormatClassId(classId);
        writer.Write(Encoding.ASCII.GetBytes(classIdHex));

        // Chunk list offset
        writer.Write(chunkListOffset);
    }

    private void WriteChunkList(BinaryWriter writer, List<(string Id, long Offset, long Size)> chunks)
    {
        // List ID
        writer.Write(Encoding.ASCII.GetBytes("List"));

        // Chunk count
        writer.Write(chunks.Count);

        // Chunk entries
        foreach (var (id, offset, size) in chunks)
        {
            writer.Write(Encoding.ASCII.GetBytes(id.PadRight(4).Substring(0, 4)));
            writer.Write(offset);
            writer.Write(size);
        }
    }

    private List<(string Id, byte[] Data)> BuildChunks(VstPresetData preset)
    {
        var chunks = new List<(string Id, byte[] Data)>();

        // Component state chunk
        if (preset.ComponentState != null && preset.ComponentState.Length > 0)
        {
            chunks.Add(("Comp", preset.ComponentState));
        }

        // Controller state chunk
        if (preset.ControllerState != null && preset.ControllerState.Length > 0)
        {
            chunks.Add(("Cont", preset.ControllerState));
        }

        // Info chunk (metadata)
        var infoData = BuildInfoChunk(preset);
        if (infoData.Length > 0)
        {
            chunks.Add(("Info", infoData));
        }

        return chunks;
    }

    private byte[] BuildInfoChunk(VstPresetData preset)
    {
        using var memStream = new MemoryStream();
        using var writer = new BinaryWriter(memStream, Encoding.UTF8);

        // Write Name attribute
        if (!string.IsNullOrEmpty(preset.Name))
        {
            WriteInfoAttribute(writer, "Name", preset.Name);
        }

        // Write Author attribute
        if (!string.IsNullOrEmpty(preset.Author))
        {
            WriteInfoAttribute(writer, "Auth", preset.Author);
        }

        // Write Category attribute
        if (!string.IsNullOrEmpty(preset.Category))
        {
            WriteInfoAttribute(writer, "Catg", preset.Category);
        }

        return memStream.ToArray();
    }

    private void WriteInfoAttribute(BinaryWriter writer, string attrId, string value)
    {
        // Attribute ID (4 bytes)
        writer.Write(Encoding.ASCII.GetBytes(attrId.PadRight(4).Substring(0, 4)));

        // Value bytes (null-terminated UTF8)
        var valueBytes = Encoding.UTF8.GetBytes(value + '\0');

        // Attribute size (8 bytes)
        writer.Write((long)valueBytes.Length);

        // Value
        writer.Write(valueBytes);
    }

    private static string FormatClassId(Guid classId)
    {
        var bytes = classId.ToByteArray();

        // Convert from .NET Guid byte order to VST3 format
        // .NET stores first 3 groups as little-endian, VST3 wants big-endian
        var vst3Bytes = new byte[16];

        // First 4 bytes (little-endian in .NET, need big-endian)
        vst3Bytes[0] = bytes[3];
        vst3Bytes[1] = bytes[2];
        vst3Bytes[2] = bytes[1];
        vst3Bytes[3] = bytes[0];

        // Next 2 bytes
        vst3Bytes[4] = bytes[5];
        vst3Bytes[5] = bytes[4];

        // Next 2 bytes
        vst3Bytes[6] = bytes[7];
        vst3Bytes[7] = bytes[6];

        // Remaining 8 bytes stay the same
        for (int i = 8; i < 16; i++)
        {
            vst3Bytes[i] = bytes[i];
        }

        // Convert to hex string
        var sb = new StringBuilder(32);
        foreach (var b in vst3Bytes)
        {
            sb.Append(b.ToString("X2"));
        }

        return sb.ToString();
    }
}
