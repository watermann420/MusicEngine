// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Broadcast WAV metadata handling.

using System;
using System.IO;
using System.Text;

namespace MusicEngine.Core.AudioEncoding;

/// <summary>
/// SMPTE timecode format representation.
/// </summary>
public struct SmpteTimecode
{
    /// <summary>
    /// Hours (0-23).
    /// </summary>
    public int Hours { get; set; }

    /// <summary>
    /// Minutes (0-59).
    /// </summary>
    public int Minutes { get; set; }

    /// <summary>
    /// Seconds (0-59).
    /// </summary>
    public int Seconds { get; set; }

    /// <summary>
    /// Frames (0-29 for 30fps, 0-24 for 25fps).
    /// </summary>
    public int Frames { get; set; }

    /// <summary>
    /// Frame rate (24, 25, 29.97, 30).
    /// </summary>
    public double FrameRate { get; set; }

    /// <summary>
    /// Drop frame flag (for 29.97 fps).
    /// </summary>
    public bool DropFrame { get; set; }

    /// <summary>
    /// Creates a new SMPTE timecode.
    /// </summary>
    public SmpteTimecode(int hours, int minutes, int seconds, int frames, double frameRate = 30.0, bool dropFrame = false)
    {
        Hours = hours;
        Minutes = minutes;
        Seconds = seconds;
        Frames = frames;
        FrameRate = frameRate;
        DropFrame = dropFrame;
    }

    /// <summary>
    /// Converts timecode to total frames.
    /// </summary>
    public long ToTotalFrames()
    {
        long totalFrames = Frames;
        totalFrames += Seconds * (long)Math.Round(FrameRate);
        totalFrames += Minutes * 60 * (long)Math.Round(FrameRate);
        totalFrames += Hours * 3600 * (long)Math.Round(FrameRate);

        // Adjust for drop frame
        if (DropFrame && Math.Abs(FrameRate - 29.97) < 0.01)
        {
            // Drop 2 frames every minute except every 10th minute
            int totalMinutes = Hours * 60 + Minutes;
            int droppedFrames = 2 * (totalMinutes - totalMinutes / 10);
            totalFrames -= droppedFrames;
        }

        return totalFrames;
    }

    /// <summary>
    /// Creates timecode from total frames.
    /// </summary>
    public static SmpteTimecode FromTotalFrames(long totalFrames, double frameRate = 30.0, bool dropFrame = false)
    {
        var tc = new SmpteTimecode { FrameRate = frameRate, DropFrame = dropFrame };
        int fps = (int)Math.Round(frameRate);

        if (dropFrame && Math.Abs(frameRate - 29.97) < 0.01)
        {
            // Complex drop frame calculation
            int d = (int)(totalFrames / 17982);
            int m = (int)(totalFrames % 17982);
            if (m < 2) m += 2;
            totalFrames += 18 * d + 2 * ((m - 2) / 1798);
        }

        tc.Frames = (int)(totalFrames % fps);
        totalFrames /= fps;
        tc.Seconds = (int)(totalFrames % 60);
        totalFrames /= 60;
        tc.Minutes = (int)(totalFrames % 60);
        tc.Hours = (int)(totalFrames / 60);

        return tc;
    }

    /// <summary>
    /// Converts timecode to sample position.
    /// </summary>
    public long ToSamplePosition(int sampleRate)
    {
        double totalSeconds = Hours * 3600.0 + Minutes * 60.0 + Seconds + Frames / FrameRate;
        return (long)(totalSeconds * sampleRate);
    }

    /// <summary>
    /// Creates timecode from sample position.
    /// </summary>
    public static SmpteTimecode FromSamplePosition(long samplePosition, int sampleRate, double frameRate = 30.0, bool dropFrame = false)
    {
        double totalSeconds = (double)samplePosition / sampleRate;
        long totalFrames = (long)(totalSeconds * frameRate);
        return FromTotalFrames(totalFrames, frameRate, dropFrame);
    }

    /// <summary>
    /// Returns string representation (HH:MM:SS:FF or HH:MM:SS;FF for drop frame).
    /// </summary>
    public override string ToString()
    {
        char separator = DropFrame ? ';' : ':';
        return $"{Hours:D2}:{Minutes:D2}:{Seconds:D2}{separator}{Frames:D2}";
    }

    /// <summary>
    /// Parses timecode from string.
    /// </summary>
    public static SmpteTimecode Parse(string timecode, double frameRate = 30.0)
    {
        bool dropFrame = timecode.Contains(';');
        string[] parts = timecode.Replace(';', ':').Split(':');

        if (parts.Length != 4)
            throw new FormatException("Invalid timecode format. Expected HH:MM:SS:FF or HH:MM:SS;FF");

        return new SmpteTimecode(
            int.Parse(parts[0]),
            int.Parse(parts[1]),
            int.Parse(parts[2]),
            int.Parse(parts[3]),
            frameRate,
            dropFrame
        );
    }
}

/// <summary>
/// Broadcast WAV format (BWF) metadata according to EBU Tech 3285.
/// Supports bext chunk for broadcast metadata and iXML for extended metadata.
/// </summary>
public class BWFMetadata
{
    // bext chunk fields (EBU Tech 3285)
    private const int DescriptionMaxLength = 256;
    private const int OriginatorMaxLength = 32;
    private const int OriginatorRefMaxLength = 32;
    private const int OriginDateLength = 10;
    private const int OriginTimeLength = 8;
    private const int UmidLength = 64;
    private const int CodingHistoryMaxLength = 1024;

    /// <summary>
    /// Description of the audio content (max 256 characters).
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Name of the originator (max 32 characters).
    /// </summary>
    public string Originator { get; set; } = string.Empty;

    /// <summary>
    /// Reference of the originator (max 32 characters).
    /// </summary>
    public string OriginatorReference { get; set; } = string.Empty;

    /// <summary>
    /// Date of creation (YYYY-MM-DD format).
    /// </summary>
    public DateTime OriginationDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Time of creation (HH:MM:SS format).
    /// </summary>
    public TimeSpan OriginationTime { get; set; } = DateTime.Now.TimeOfDay;

    /// <summary>
    /// Time reference (sample count from midnight).
    /// </summary>
    public long TimeReference { get; set; }

    /// <summary>
    /// BWF version (1 or 2).
    /// </summary>
    public ushort Version { get; set; } = 2;

    /// <summary>
    /// UMID (Unique Material Identifier) - 64 bytes.
    /// </summary>
    public byte[] UMID { get; set; } = new byte[UmidLength];

    /// <summary>
    /// Loudness value in LUFS (ITU-R BS.1770-4).
    /// </summary>
    public short LoudnessValue { get; set; }

    /// <summary>
    /// Loudness range in LU.
    /// </summary>
    public short LoudnessRange { get; set; }

    /// <summary>
    /// Max true peak level in dBTP.
    /// </summary>
    public short MaxTruePeakLevel { get; set; }

    /// <summary>
    /// Max momentary loudness in LUFS.
    /// </summary>
    public short MaxMomentaryLoudness { get; set; }

    /// <summary>
    /// Max short-term loudness in LUFS.
    /// </summary>
    public short MaxShortTermLoudness { get; set; }

    /// <summary>
    /// Coding history text (max 1024 characters).
    /// </summary>
    public string CodingHistory { get; set; } = string.Empty;

    /// <summary>
    /// SMPTE timecode for the file start.
    /// </summary>
    public SmpteTimecode Timecode { get; set; }

    /// <summary>
    /// iXML metadata (extended XML-based metadata).
    /// </summary>
    public string IXmlContent { get; set; } = string.Empty;

    /// <summary>
    /// Project name (for iXML).
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Scene name (for iXML).
    /// </summary>
    public string SceneName { get; set; } = string.Empty;

    /// <summary>
    /// Take number (for iXML).
    /// </summary>
    public int TakeNumber { get; set; }

    /// <summary>
    /// User-defined notes (for iXML).
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Creates empty BWF metadata.
    /// </summary>
    public BWFMetadata() { }

    /// <summary>
    /// Creates BWF metadata with basic info.
    /// </summary>
    public BWFMetadata(string description, string originator)
    {
        Description = description;
        Originator = originator;
        OriginatorReference = Guid.NewGuid().ToString("N")[..32];
    }

    /// <summary>
    /// Sets the time reference from SMPTE timecode.
    /// </summary>
    public void SetTimeReferenceFromTimecode(int sampleRate)
    {
        TimeReference = Timecode.ToSamplePosition(sampleRate);
    }

    /// <summary>
    /// Gets SMPTE timecode from time reference.
    /// </summary>
    public SmpteTimecode GetTimecodeFromTimeReference(int sampleRate, double frameRate = 30.0, bool dropFrame = false)
    {
        return SmpteTimecode.FromSamplePosition(TimeReference, sampleRate, frameRate, dropFrame);
    }

    /// <summary>
    /// Sets loudness values from measurements.
    /// </summary>
    public void SetLoudnessValues(double integratedLufs, double lra, double truePeakDbtp,
        double momentaryLufs = double.NegativeInfinity, double shortTermLufs = double.NegativeInfinity)
    {
        // Store as 1/100 LUFS (EBU Tech 3285-s5)
        LoudnessValue = double.IsNegativeInfinity(integratedLufs) ? short.MinValue : (short)(integratedLufs * 100);
        LoudnessRange = (short)(lra * 100);
        MaxTruePeakLevel = double.IsNegativeInfinity(truePeakDbtp) ? short.MinValue : (short)(truePeakDbtp * 100);
        MaxMomentaryLoudness = double.IsNegativeInfinity(momentaryLufs) ? short.MinValue : (short)(momentaryLufs * 100);
        MaxShortTermLoudness = double.IsNegativeInfinity(shortTermLufs) ? short.MinValue : (short)(shortTermLufs * 100);
    }

    /// <summary>
    /// Generates a basic UMID.
    /// </summary>
    public void GenerateUMID()
    {
        // Basic UMID structure (SMPTE 330M)
        var umid = new byte[UmidLength];

        // Universal label (12 bytes)
        byte[] universalLabel = { 0x06, 0x0A, 0x2B, 0x34, 0x01, 0x01, 0x01, 0x05, 0x01, 0x01, 0x0D, 0x20 };
        Array.Copy(universalLabel, 0, umid, 0, 12);

        // Length byte
        umid[12] = 0x13;

        // Instance number (3 bytes)
        var instanceBytes = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
        Array.Copy(instanceBytes, 0, umid, 13, 3);

        // Material number - use GUID (16 bytes)
        var guid = Guid.NewGuid().ToByteArray();
        Array.Copy(guid, 0, umid, 16, 16);

        UMID = umid;
    }

    /// <summary>
    /// Writes the bext chunk to a stream.
    /// </summary>
    public void WriteBextChunk(BinaryWriter writer)
    {
        using var chunkData = new MemoryStream();
        using var chunkWriter = new BinaryWriter(chunkData, Encoding.ASCII, true);

        // Description (256 bytes)
        WriteFixedString(chunkWriter, Description, DescriptionMaxLength);

        // Originator (32 bytes)
        WriteFixedString(chunkWriter, Originator, OriginatorMaxLength);

        // OriginatorReference (32 bytes)
        WriteFixedString(chunkWriter, OriginatorReference, OriginatorRefMaxLength);

        // OriginationDate (10 bytes) - YYYY-MM-DD
        WriteFixedString(chunkWriter, OriginationDate.ToString("yyyy-MM-dd"), OriginDateLength);

        // OriginationTime (8 bytes) - HH:MM:SS
        WriteFixedString(chunkWriter, OriginationTime.ToString(@"hh\:mm\:ss"), OriginTimeLength);

        // TimeReference (8 bytes)
        chunkWriter.Write((uint)(TimeReference & 0xFFFFFFFF));
        chunkWriter.Write((uint)(TimeReference >> 32));

        // Version (2 bytes)
        chunkWriter.Write(Version);

        // UMID (64 bytes)
        chunkWriter.Write(UMID.Length == UmidLength ? UMID : new byte[UmidLength]);

        // Loudness values (10 bytes total, version 2)
        chunkWriter.Write(LoudnessValue);
        chunkWriter.Write(LoudnessRange);
        chunkWriter.Write(MaxTruePeakLevel);
        chunkWriter.Write(MaxMomentaryLoudness);
        chunkWriter.Write(MaxShortTermLoudness);

        // Reserved (180 bytes)
        chunkWriter.Write(new byte[180]);

        // CodingHistory (variable, but we limit to max)
        var codingHistoryBytes = Encoding.ASCII.GetBytes(CodingHistory);
        if (codingHistoryBytes.Length > 0)
        {
            int length = Math.Min(codingHistoryBytes.Length, CodingHistoryMaxLength);
            chunkWriter.Write(codingHistoryBytes, 0, length);
        }

        // Write chunk header and data
        byte[] data = chunkData.ToArray();
        writer.Write(Encoding.ASCII.GetBytes("bext"));
        writer.Write(data.Length);
        writer.Write(data);

        // Pad to even boundary
        if (data.Length % 2 != 0)
        {
            writer.Write((byte)0);
        }
    }

    /// <summary>
    /// Reads the bext chunk from a stream.
    /// </summary>
    public static BWFMetadata? ReadBextChunk(BinaryReader reader, int chunkSize)
    {
        if (chunkSize < 602) // Minimum bext chunk size
            return null;

        var metadata = new BWFMetadata();
        long startPos = reader.BaseStream.Position;

        // Description (256 bytes)
        metadata.Description = ReadFixedString(reader, DescriptionMaxLength);

        // Originator (32 bytes)
        metadata.Originator = ReadFixedString(reader, OriginatorMaxLength);

        // OriginatorReference (32 bytes)
        metadata.OriginatorReference = ReadFixedString(reader, OriginatorRefMaxLength);

        // OriginationDate (10 bytes)
        string dateStr = ReadFixedString(reader, OriginDateLength);
        if (DateTime.TryParse(dateStr, out var date))
        {
            metadata.OriginationDate = date;
        }

        // OriginationTime (8 bytes)
        string timeStr = ReadFixedString(reader, OriginTimeLength);
        if (TimeSpan.TryParse(timeStr, out var time))
        {
            metadata.OriginationTime = time;
        }

        // TimeReference (8 bytes)
        uint timeLow = reader.ReadUInt32();
        uint timeHigh = reader.ReadUInt32();
        metadata.TimeReference = timeLow | ((long)timeHigh << 32);

        // Version (2 bytes)
        metadata.Version = reader.ReadUInt16();

        // UMID (64 bytes)
        metadata.UMID = reader.ReadBytes(UmidLength);

        // Loudness values (version 2)
        if (metadata.Version >= 2)
        {
            metadata.LoudnessValue = reader.ReadInt16();
            metadata.LoudnessRange = reader.ReadInt16();
            metadata.MaxTruePeakLevel = reader.ReadInt16();
            metadata.MaxMomentaryLoudness = reader.ReadInt16();
            metadata.MaxShortTermLoudness = reader.ReadInt16();
        }
        else
        {
            reader.ReadBytes(10); // Skip reserved in version 1
        }

        // Reserved (180 bytes)
        reader.ReadBytes(180);

        // CodingHistory (remaining bytes)
        long bytesRead = reader.BaseStream.Position - startPos;
        int codingHistoryLength = chunkSize - (int)bytesRead;
        if (codingHistoryLength > 0)
        {
            byte[] codingHistoryBytes = reader.ReadBytes(codingHistoryLength);
            metadata.CodingHistory = Encoding.ASCII.GetString(codingHistoryBytes).TrimEnd('\0');
        }

        return metadata;
    }

    /// <summary>
    /// Generates iXML content.
    /// </summary>
    public string GenerateIXml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<BWFXML>");
        sb.AppendLine("  <IXML_VERSION>2.0</IXML_VERSION>");

        if (!string.IsNullOrEmpty(ProjectName))
        {
            sb.AppendLine($"  <PROJECT>{EscapeXml(ProjectName)}</PROJECT>");
        }

        if (!string.IsNullOrEmpty(SceneName))
        {
            sb.AppendLine($"  <SCENE>{EscapeXml(SceneName)}</SCENE>");
        }

        if (TakeNumber > 0)
        {
            sb.AppendLine($"  <TAKE>{TakeNumber}</TAKE>");
        }

        sb.AppendLine($"  <TAPE>{EscapeXml(OriginatorReference)}</TAPE>");

        // Timecode info
        sb.AppendLine("  <SPEED>");
        sb.AppendLine($"    <TIMECODE_RATE>{Timecode.FrameRate:F2}</TIMECODE_RATE>");
        sb.AppendLine($"    <TIMECODE_FLAG>{(Timecode.DropFrame ? "DF" : "NDF")}</TIMECODE_FLAG>");
        sb.AppendLine("  </SPEED>");

        sb.AppendLine($"  <BEXT>");
        sb.AppendLine($"    <BWF_DESCRIPTION>{EscapeXml(Description)}</BWF_DESCRIPTION>");
        sb.AppendLine($"    <BWF_ORIGINATOR>{EscapeXml(Originator)}</BWF_ORIGINATOR>");
        sb.AppendLine($"    <BWF_ORIGINATOR_REFERENCE>{EscapeXml(OriginatorReference)}</BWF_ORIGINATOR_REFERENCE>");
        sb.AppendLine($"    <BWF_ORIGINATION_DATE>{OriginationDate:yyyy-MM-dd}</BWF_ORIGINATION_DATE>");
        sb.AppendLine($"    <BWF_ORIGINATION_TIME>{OriginationTime:hh\\:mm\\:ss}</BWF_ORIGINATION_TIME>");
        sb.AppendLine($"    <BWF_TIME_REFERENCE_LOW>{TimeReference & 0xFFFFFFFF}</BWF_TIME_REFERENCE_LOW>");
        sb.AppendLine($"    <BWF_TIME_REFERENCE_HIGH>{TimeReference >> 32}</BWF_TIME_REFERENCE_HIGH>");
        sb.AppendLine($"  </BEXT>");

        if (!string.IsNullOrEmpty(Notes))
        {
            sb.AppendLine($"  <NOTE>{EscapeXml(Notes)}</NOTE>");
        }

        sb.AppendLine("</BWFXML>");

        IXmlContent = sb.ToString();
        return IXmlContent;
    }

    /// <summary>
    /// Writes the iXML chunk to a stream.
    /// </summary>
    public void WriteIXmlChunk(BinaryWriter writer)
    {
        if (string.IsNullOrEmpty(IXmlContent))
        {
            GenerateIXml();
        }

        byte[] xmlBytes = Encoding.UTF8.GetBytes(IXmlContent);

        writer.Write(Encoding.ASCII.GetBytes("iXML"));
        writer.Write(xmlBytes.Length);
        writer.Write(xmlBytes);

        // Pad to even boundary
        if (xmlBytes.Length % 2 != 0)
        {
            writer.Write((byte)0);
        }
    }

    /// <summary>
    /// Reads iXML chunk from a stream.
    /// </summary>
    public static string? ReadIXmlChunk(BinaryReader reader, int chunkSize)
    {
        byte[] xmlBytes = reader.ReadBytes(chunkSize);
        return Encoding.UTF8.GetString(xmlBytes).TrimEnd('\0');
    }

    /// <summary>
    /// Reads BWF metadata from a WAV file.
    /// </summary>
    public static BWFMetadata? ReadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII);

        // Read RIFF header
        string riff = new string(reader.ReadChars(4));
        if (riff != "RIFF")
            return null;

        reader.ReadInt32(); // File size

        string wave = new string(reader.ReadChars(4));
        if (wave != "WAVE")
            return null;

        BWFMetadata? metadata = null;

        // Read chunks
        while (stream.Position < stream.Length - 8)
        {
            string chunkId = new string(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();

            if (chunkId == "bext")
            {
                metadata = ReadBextChunk(reader, chunkSize);
            }
            else if (chunkId == "iXML" && metadata != null)
            {
                metadata.IXmlContent = ReadIXmlChunk(reader, chunkSize) ?? string.Empty;
            }
            else
            {
                // Skip chunk
                stream.Position += chunkSize;
            }

            // Skip padding byte
            if (chunkSize % 2 != 0 && stream.Position < stream.Length)
            {
                stream.Position++;
            }
        }

        return metadata;
    }

    /// <summary>
    /// Writes BWF metadata chunks to a stream (for embedding in WAV).
    /// </summary>
    public void WriteChunks(BinaryWriter writer)
    {
        WriteBextChunk(writer);
        WriteIXmlChunk(writer);
    }

    private static void WriteFixedString(BinaryWriter writer, string value, int length)
    {
        byte[] bytes = new byte[length];
        if (!string.IsNullOrEmpty(value))
        {
            byte[] valueBytes = Encoding.ASCII.GetBytes(value);
            int copyLength = Math.Min(valueBytes.Length, length);
            Array.Copy(valueBytes, bytes, copyLength);
        }
        writer.Write(bytes);
    }

    private static string ReadFixedString(BinaryReader reader, int length)
    {
        byte[] bytes = reader.ReadBytes(length);
        int nullIndex = Array.IndexOf(bytes, (byte)0);
        int actualLength = nullIndex >= 0 ? nullIndex : length;
        return Encoding.ASCII.GetString(bytes, 0, actualLength);
    }

    private static string EscapeXml(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
