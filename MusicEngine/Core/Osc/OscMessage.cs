// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;
using System.Text;


namespace MusicEngine.Core.Osc;


/// <summary>
/// Represents the type of an OSC argument.
/// </summary>
public enum OscType
{
    /// <summary>32-bit integer (type tag 'i').</summary>
    Int32,

    /// <summary>32-bit float (type tag 'f').</summary>
    Float32,

    /// <summary>OSC-string (type tag 's').</summary>
    String,

    /// <summary>OSC-blob (type tag 'b').</summary>
    Blob,

    /// <summary>True (type tag 'T').</summary>
    True,

    /// <summary>False (type tag 'F').</summary>
    False,

    /// <summary>Nil (type tag 'N').</summary>
    Nil,

    /// <summary>Infinitum (type tag 'I').</summary>
    Infinitum,

    /// <summary>64-bit integer (type tag 'h').</summary>
    Int64,

    /// <summary>64-bit double (type tag 'd').</summary>
    Float64,

    /// <summary>OSC-timetag (type tag 't').</summary>
    Timetag,

    /// <summary>Character (type tag 'c').</summary>
    Char
}


/// <summary>
/// Represents a single OSC argument with type information.
/// </summary>
public readonly struct OscArgument
{
    /// <summary>The type of this argument.</summary>
    public OscType Type { get; }

    /// <summary>The value of this argument.</summary>
    public object? Value { get; }

    public OscArgument(OscType type, object? value)
    {
        Type = type;
        Value = value;
    }

    /// <summary>Creates an Int32 argument.</summary>
    public static OscArgument Int(int value) => new(OscType.Int32, value);

    /// <summary>Creates a Float32 argument.</summary>
    public static OscArgument Float(float value) => new(OscType.Float32, value);

    /// <summary>Creates a String argument.</summary>
    public static OscArgument String(string value) => new(OscType.String, value);

    /// <summary>Creates a Blob argument.</summary>
    public static OscArgument Blob(byte[] value) => new(OscType.Blob, value);

    /// <summary>Creates a True argument.</summary>
    public static OscArgument True() => new(OscType.True, true);

    /// <summary>Creates a False argument.</summary>
    public static OscArgument False() => new(OscType.False, false);

    /// <summary>Creates a Nil argument.</summary>
    public static OscArgument Nil() => new(OscType.Nil, null);

    /// <summary>Creates an Int64 argument.</summary>
    public static OscArgument Long(long value) => new(OscType.Int64, value);

    /// <summary>Creates a Float64 argument.</summary>
    public static OscArgument Double(double value) => new(OscType.Float64, value);

    /// <summary>Creates a Timetag argument.</summary>
    public static OscArgument Timetag(ulong value) => new(OscType.Timetag, value);

    /// <summary>Creates a Char argument.</summary>
    public static OscArgument Char(char value) => new(OscType.Char, value);

    /// <summary>Gets the type tag character for this argument.</summary>
    public char GetTypeTag() => Type switch
    {
        OscType.Int32 => 'i',
        OscType.Float32 => 'f',
        OscType.String => 's',
        OscType.Blob => 'b',
        OscType.True => 'T',
        OscType.False => 'F',
        OscType.Nil => 'N',
        OscType.Infinitum => 'I',
        OscType.Int64 => 'h',
        OscType.Float64 => 'd',
        OscType.Timetag => 't',
        OscType.Char => 'c',
        _ => throw new ArgumentException($"Unknown OSC type: {Type}")
    };

    public override string ToString() => $"{Type}: {Value}";
}


/// <summary>
/// Represents an OSC message with an address pattern and arguments.
/// Follows the OSC 1.0 specification.
/// </summary>
public class OscMessage
{
    /// <summary>The OSC address pattern (e.g., "/transport/play").</summary>
    public string Address { get; }

    /// <summary>The arguments of this message.</summary>
    public IReadOnlyList<OscArgument> Arguments => _arguments;
    private readonly List<OscArgument> _arguments;

    /// <summary>
    /// Creates a new OSC message with the specified address and optional arguments.
    /// </summary>
    /// <param name="address">The OSC address pattern (must start with '/').</param>
    /// <param name="args">The message arguments.</param>
    public OscMessage(string address, params object[] args)
    {
        if (string.IsNullOrEmpty(address))
            throw new ArgumentException("Address cannot be null or empty.", nameof(address));
        if (!address.StartsWith('/'))
            throw new ArgumentException("OSC address must start with '/'.", nameof(address));

        Address = address;
        _arguments = new List<OscArgument>();

        foreach (var arg in args)
        {
            _arguments.Add(ConvertToOscArgument(arg));
        }
    }

    /// <summary>
    /// Creates a new OSC message with the specified address and typed arguments.
    /// </summary>
    public OscMessage(string address, IEnumerable<OscArgument> arguments)
    {
        if (string.IsNullOrEmpty(address))
            throw new ArgumentException("Address cannot be null or empty.", nameof(address));
        if (!address.StartsWith('/'))
            throw new ArgumentException("OSC address must start with '/'.", nameof(address));

        Address = address;
        _arguments = new List<OscArgument>(arguments);
    }

    /// <summary>
    /// Converts a .NET object to an OSC argument.
    /// </summary>
    private static OscArgument ConvertToOscArgument(object arg)
    {
        return arg switch
        {
            int i => OscArgument.Int(i),
            float f => OscArgument.Float(f),
            string s => OscArgument.String(s),
            byte[] b => OscArgument.Blob(b),
            bool b => b ? OscArgument.True() : OscArgument.False(),
            null => OscArgument.Nil(),
            long l => OscArgument.Long(l),
            double d => OscArgument.Double(d),
            char c => OscArgument.Char(c),
            OscArgument oa => oa,
            _ => throw new ArgumentException($"Unsupported argument type: {arg.GetType()}")
        };
    }

    /// <summary>
    /// Gets an argument value at the specified index.
    /// </summary>
    public T GetArgument<T>(int index)
    {
        if (index < 0 || index >= _arguments.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var value = _arguments[index].Value;
        if (value is T typedValue)
            return typedValue;

        throw new InvalidCastException($"Argument at index {index} is not of type {typeof(T).Name}");
    }

    /// <summary>
    /// Tries to get an argument value at the specified index.
    /// </summary>
    public bool TryGetArgument<T>(int index, out T? value)
    {
        value = default;
        if (index < 0 || index >= _arguments.Count)
            return false;

        if (_arguments[index].Value is T typedValue)
        {
            value = typedValue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Serializes this message to OSC binary format.
    /// </summary>
    public byte[] ToBytes()
    {
        var buffer = new List<byte>();

        // Write address
        WriteString(buffer, Address);

        // Build type tag string
        var typeTag = new StringBuilder(",");
        foreach (var arg in _arguments)
        {
            typeTag.Append(arg.GetTypeTag());
        }
        WriteString(buffer, typeTag.ToString());

        // Write argument data
        foreach (var arg in _arguments)
        {
            WriteArgument(buffer, arg);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Parses an OSC message from binary data.
    /// </summary>
    public static OscMessage Parse(byte[] data)
    {
        return Parse(data, 0, data.Length);
    }

    /// <summary>
    /// Parses an OSC message from binary data with offset and length.
    /// </summary>
    public static OscMessage Parse(byte[] data, int offset, int length)
    {
        int position = offset;
        int end = offset + length;

        // Read address
        var address = ReadString(data, ref position, end);

        // Read type tag
        var typeTag = ReadString(data, ref position, end);
        if (!typeTag.StartsWith(','))
            throw new FormatException("Invalid OSC message: type tag string must start with ','");

        // Parse arguments
        var arguments = new List<OscArgument>();
        for (int i = 1; i < typeTag.Length; i++)
        {
            char tag = typeTag[i];
            arguments.Add(ReadArgument(data, ref position, end, tag));
        }

        return new OscMessage(address, arguments);
    }

    /// <summary>
    /// Writes an OSC-string to the buffer (null-terminated, padded to 4-byte boundary).
    /// </summary>
    private static void WriteString(List<byte> buffer, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        buffer.AddRange(bytes);
        buffer.Add(0); // Null terminator

        // Pad to 4-byte boundary
        int padding = (4 - (bytes.Length + 1) % 4) % 4;
        for (int i = 0; i < padding; i++)
            buffer.Add(0);
    }

    /// <summary>
    /// Writes an argument to the buffer based on its type.
    /// </summary>
    private static void WriteArgument(List<byte> buffer, OscArgument arg)
    {
        switch (arg.Type)
        {
            case OscType.Int32:
                WriteInt32(buffer, (int)arg.Value!);
                break;
            case OscType.Float32:
                WriteFloat32(buffer, (float)arg.Value!);
                break;
            case OscType.String:
                WriteString(buffer, (string)arg.Value!);
                break;
            case OscType.Blob:
                WriteBlob(buffer, (byte[])arg.Value!);
                break;
            case OscType.Int64:
                WriteInt64(buffer, (long)arg.Value!);
                break;
            case OscType.Float64:
                WriteFloat64(buffer, (double)arg.Value!);
                break;
            case OscType.Timetag:
                WriteUInt64(buffer, (ulong)arg.Value!);
                break;
            case OscType.Char:
                WriteChar(buffer, (char)arg.Value!);
                break;
            case OscType.True:
            case OscType.False:
            case OscType.Nil:
            case OscType.Infinitum:
                // These types have no data bytes
                break;
        }
    }

    private static void WriteInt32(List<byte> buffer, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        buffer.AddRange(bytes);
    }

    private static void WriteFloat32(List<byte> buffer, float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        buffer.AddRange(bytes);
    }

    private static void WriteInt64(List<byte> buffer, long value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        buffer.AddRange(bytes);
    }

    private static void WriteFloat64(List<byte> buffer, double value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        buffer.AddRange(bytes);
    }

    private static void WriteUInt64(List<byte> buffer, ulong value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        buffer.AddRange(bytes);
    }

    private static void WriteChar(List<byte> buffer, char value)
    {
        // OSC char is stored as 4 bytes (big-endian int32)
        buffer.Add(0);
        buffer.Add(0);
        buffer.Add(0);
        buffer.Add((byte)value);
    }

    private static void WriteBlob(List<byte> buffer, byte[] value)
    {
        WriteInt32(buffer, value.Length);
        buffer.AddRange(value);

        // Pad to 4-byte boundary
        int padding = (4 - value.Length % 4) % 4;
        for (int i = 0; i < padding; i++)
            buffer.Add(0);
    }

    /// <summary>
    /// Reads an OSC-string from the buffer.
    /// </summary>
    private static string ReadString(byte[] data, ref int position, int end)
    {
        int start = position;
        while (position < end && data[position] != 0)
            position++;

        string value = Encoding.ASCII.GetString(data, start, position - start);
        position++; // Skip null terminator

        // Skip padding to 4-byte boundary
        position = ((position + 3) / 4) * 4;

        return value;
    }

    /// <summary>
    /// Reads an argument from the buffer based on its type tag.
    /// </summary>
    private static OscArgument ReadArgument(byte[] data, ref int position, int end, char typeTag)
    {
        return typeTag switch
        {
            'i' => OscArgument.Int(ReadInt32(data, ref position)),
            'f' => OscArgument.Float(ReadFloat32(data, ref position)),
            's' => OscArgument.String(ReadString(data, ref position, end)),
            'b' => OscArgument.Blob(ReadBlob(data, ref position)),
            'T' => OscArgument.True(),
            'F' => OscArgument.False(),
            'N' => OscArgument.Nil(),
            'I' => new OscArgument(OscType.Infinitum, null),
            'h' => OscArgument.Long(ReadInt64(data, ref position)),
            'd' => OscArgument.Double(ReadFloat64(data, ref position)),
            't' => OscArgument.Timetag(ReadUInt64(data, ref position)),
            'c' => OscArgument.Char(ReadChar(data, ref position)),
            _ => throw new FormatException($"Unknown OSC type tag: {typeTag}")
        };
    }

    private static int ReadInt32(byte[] data, ref int position)
    {
        byte[] bytes = new byte[4];
        Array.Copy(data, position, bytes, 0, 4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        position += 4;
        return BitConverter.ToInt32(bytes, 0);
    }

    private static float ReadFloat32(byte[] data, ref int position)
    {
        byte[] bytes = new byte[4];
        Array.Copy(data, position, bytes, 0, 4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        position += 4;
        return BitConverter.ToSingle(bytes, 0);
    }

    private static long ReadInt64(byte[] data, ref int position)
    {
        byte[] bytes = new byte[8];
        Array.Copy(data, position, bytes, 0, 8);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        position += 8;
        return BitConverter.ToInt64(bytes, 0);
    }

    private static double ReadFloat64(byte[] data, ref int position)
    {
        byte[] bytes = new byte[8];
        Array.Copy(data, position, bytes, 0, 8);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        position += 8;
        return BitConverter.ToDouble(bytes, 0);
    }

    private static ulong ReadUInt64(byte[] data, ref int position)
    {
        byte[] bytes = new byte[8];
        Array.Copy(data, position, bytes, 0, 8);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        position += 8;
        return BitConverter.ToUInt64(bytes, 0);
    }

    private static char ReadChar(byte[] data, ref int position)
    {
        position += 3; // Skip first 3 bytes
        char value = (char)data[position];
        position++;
        return value;
    }

    private static byte[] ReadBlob(byte[] data, ref int position)
    {
        int length = ReadInt32(data, ref position);
        byte[] value = new byte[length];
        Array.Copy(data, position, value, 0, length);
        position += length;

        // Skip padding to 4-byte boundary
        position = ((position + 3) / 4) * 4;

        return value;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(Address);
        foreach (var arg in _arguments)
        {
            sb.Append(' ');
            sb.Append(arg.Value?.ToString() ?? "nil");
        }
        return sb.ToString();
    }
}


/// <summary>
/// Represents an OSC bundle containing multiple messages or nested bundles.
/// </summary>
public class OscBundle
{
    /// <summary>OSC bundle identifier string.</summary>
    public const string BundleIdentifier = "#bundle";

    /// <summary>The OSC timetag for this bundle (NTP format).</summary>
    public ulong Timetag { get; }

    /// <summary>The elements in this bundle (messages or nested bundles).</summary>
    public IReadOnlyList<object> Elements => _elements;
    private readonly List<object> _elements;

    /// <summary>
    /// Creates a new OSC bundle with immediate execution.
    /// </summary>
    public OscBundle() : this(1) // Timetag 1 = immediately
    {
    }

    /// <summary>
    /// Creates a new OSC bundle with the specified timetag.
    /// </summary>
    /// <param name="timetag">The NTP timetag (1 = immediately).</param>
    public OscBundle(ulong timetag)
    {
        Timetag = timetag;
        _elements = new List<object>();
    }

    /// <summary>
    /// Creates a new OSC bundle with a DateTime.
    /// </summary>
    public OscBundle(DateTime time)
    {
        Timetag = DateTimeToNtpTime(time);
        _elements = new List<object>();
    }

    /// <summary>
    /// Adds a message to this bundle.
    /// </summary>
    public OscBundle Add(OscMessage message)
    {
        _elements.Add(message);
        return this;
    }

    /// <summary>
    /// Adds a nested bundle to this bundle.
    /// </summary>
    public OscBundle Add(OscBundle bundle)
    {
        _elements.Add(bundle);
        return this;
    }

    /// <summary>
    /// Serializes this bundle to OSC binary format.
    /// </summary>
    public byte[] ToBytes()
    {
        var buffer = new List<byte>();

        // Write bundle identifier
        WriteOscString(buffer, BundleIdentifier);

        // Write timetag (8 bytes, big-endian)
        WriteTimetag(buffer, Timetag);

        // Write elements
        foreach (var element in _elements)
        {
            byte[] elementData;
            if (element is OscMessage message)
            {
                elementData = message.ToBytes();
            }
            else if (element is OscBundle bundle)
            {
                elementData = bundle.ToBytes();
            }
            else
            {
                throw new InvalidOperationException($"Unknown element type: {element.GetType()}");
            }

            // Write element size (4 bytes, big-endian)
            WriteInt32(buffer, elementData.Length);
            buffer.AddRange(elementData);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Parses an OSC bundle from binary data.
    /// </summary>
    public static OscBundle Parse(byte[] data)
    {
        return Parse(data, 0, data.Length);
    }

    /// <summary>
    /// Parses an OSC bundle from binary data with offset and length.
    /// </summary>
    public static OscBundle Parse(byte[] data, int offset, int length)
    {
        int position = offset;
        int end = offset + length;

        // Read and verify bundle identifier
        var identifier = ReadOscString(data, ref position, end);
        if (identifier != BundleIdentifier)
            throw new FormatException($"Invalid OSC bundle: expected '{BundleIdentifier}', got '{identifier}'");

        // Read timetag
        var timetag = ReadTimetag(data, ref position);
        var bundle = new OscBundle(timetag);

        // Read elements
        while (position < end)
        {
            int elementSize = ReadInt32(data, ref position);

            // Determine if element is a bundle or message
            if (position + elementSize <= end)
            {
                if (elementSize >= 8 &&
                    data[position] == '#' &&
                    data[position + 1] == 'b')
                {
                    bundle.Add(OscBundle.Parse(data, position, elementSize));
                }
                else
                {
                    bundle.Add(OscMessage.Parse(data, position, elementSize));
                }
                position += elementSize;
            }
        }

        return bundle;
    }

    /// <summary>
    /// Determines if the given data is an OSC bundle (starts with "#bundle").
    /// </summary>
    public static bool IsBundle(byte[] data)
    {
        return data.Length >= 8 &&
               data[0] == '#' &&
               data[1] == 'b' &&
               data[2] == 'u' &&
               data[3] == 'n' &&
               data[4] == 'd' &&
               data[5] == 'l' &&
               data[6] == 'e' &&
               data[7] == 0;
    }

    /// <summary>
    /// Converts a DateTime to NTP timetag format.
    /// </summary>
    public static ulong DateTimeToNtpTime(DateTime dateTime)
    {
        // NTP epoch is January 1, 1900
        var ntpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var span = dateTime.ToUniversalTime() - ntpEpoch;

        ulong seconds = (ulong)span.TotalSeconds;
        ulong fraction = (ulong)((span.TotalSeconds - seconds) * uint.MaxValue);

        return (seconds << 32) | fraction;
    }

    /// <summary>
    /// Converts an NTP timetag to DateTime.
    /// </summary>
    public static DateTime NtpTimeToDateTime(ulong timetag)
    {
        if (timetag == 1) // Immediately
            return DateTime.UtcNow;

        var ntpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ulong seconds = timetag >> 32;
        ulong fraction = timetag & 0xFFFFFFFF;

        double totalSeconds = seconds + (double)fraction / uint.MaxValue;
        return ntpEpoch.AddSeconds(totalSeconds);
    }

    private static void WriteOscString(List<byte> buffer, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        buffer.AddRange(bytes);
        buffer.Add(0);

        int padding = (4 - (bytes.Length + 1) % 4) % 4;
        for (int i = 0; i < padding; i++)
            buffer.Add(0);
    }

    private static void WriteTimetag(List<byte> buffer, ulong value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        buffer.AddRange(bytes);
    }

    private static void WriteInt32(List<byte> buffer, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        buffer.AddRange(bytes);
    }

    private static string ReadOscString(byte[] data, ref int position, int end)
    {
        int start = position;
        while (position < end && data[position] != 0)
            position++;

        string value = Encoding.ASCII.GetString(data, start, position - start);
        position++;
        position = ((position + 3) / 4) * 4;

        return value;
    }

    private static ulong ReadTimetag(byte[] data, ref int position)
    {
        byte[] bytes = new byte[8];
        Array.Copy(data, position, bytes, 0, 8);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        position += 8;
        return BitConverter.ToUInt64(bytes, 0);
    }

    private static int ReadInt32(byte[] data, ref int position)
    {
        byte[] bytes = new byte[4];
        Array.Copy(data, position, bytes, 0, 4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        position += 4;
        return BitConverter.ToInt32(bytes, 0);
    }

    public override string ToString()
    {
        return $"OscBundle[timetag={Timetag}, elements={_elements.Count}]";
    }
}
