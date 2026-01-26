// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Runtime.InteropServices;

namespace MusicEngine.Core.Vst.Vst3.Structures;

/// <summary>
/// VST3 Result codes
/// </summary>
public enum Vst3Result : int
{
    Ok = 0,
    True = 0,
    False = 1,
    InvalidArgument = -1,
    NotImplemented = -2,
    InternalError = -3,
    NotInitialized = -4,
    OutOfMemory = -5
}

/// <summary>
/// VST3 Process modes
/// </summary>
public enum Vst3ProcessMode : int
{
    Realtime = 0,
    Prefetch = 1,
    Offline = 2
}

/// <summary>
/// VST3 Sample sizes
/// </summary>
public enum Vst3SymbolicSampleSize : int
{
    Sample32 = 0,
    Sample64 = 1
}

/// <summary>
/// VST3 I/O modes
/// </summary>
public enum Vst3IoMode : int
{
    Simple = 0,
    Advanced = 1,
    OfflineProcessing = 2
}

/// <summary>
/// VST3 Component flags
/// </summary>
[Flags]
public enum Vst3ComponentFlags : uint
{
    None = 0,
    Distributable = 1 << 0,
    SimpleModeSupported = 1 << 1
}

/// <summary>
/// VST3 Parameter flags
/// </summary>
[Flags]
public enum Vst3ParameterFlags : int
{
    None = 0,
    CanAutomate = 1 << 0,
    IsReadOnly = 1 << 1,
    IsWrapAround = 1 << 2,
    IsList = 1 << 3,
    IsHidden = 1 << 4,
    IsProgramChange = 1 << 15,
    IsBypass = 1 << 16
}

/// <summary>
/// VST3 Restart flags
/// </summary>
[Flags]
public enum Vst3RestartFlags : int
{
    ReloadComponent = 1 << 0,
    IoChanged = 1 << 1,
    ParamValuesChanged = 1 << 2,
    LatencyChanged = 1 << 3,
    ParamTitlesChanged = 1 << 4,
    MidiCCAssignmentChanged = 1 << 5,
    NoteExpressionChanged = 1 << 6,
    IoTitlesChanged = 1 << 7,
    PrefetchableSupportChanged = 1 << 8,
    RoutingInfoChanged = 1 << 9,
    KeyswitchChanged = 1 << 10,
    ParamIdChanged = 1 << 11
}

/// <summary>
/// VST3 Note Expression types
/// </summary>
public static class Vst3NoteExpressionTypeIds
{
    public const uint Volume = 0;
    public const uint Pan = 1;
    public const uint Tuning = 2;
    public const uint Vibrato = 3;
    public const uint Expression = 4;
    public const uint Brightness = 5;
    public const uint Text = 6;
    public const uint Phoneme = 7;

    public const uint CustomStart = 100000;
    public const uint CustomEnd = 200000;
    public const uint InvalidTypeId = 0xFFFFFFFF;
}

/// <summary>
/// VST3 Speaker arrangement constants
/// </summary>
public static class Vst3SpeakerArrangement
{
    public const ulong Empty = 0;
    public const ulong Mono = 1UL << 19; // kSpeakerM
    public const ulong Stereo = (1UL << 0) | (1UL << 1); // kSpeakerL | kSpeakerR
    public const ulong StereoSurround = (1UL << 4) | (1UL << 5); // kSpeakerLs | kSpeakerRs
    public const ulong StereoCenter = (1UL << 6) | (1UL << 7); // kSpeakerLc | kSpeakerRc
    public const ulong StereoWide = (1UL << 29) | (1UL << 30); // kSpeakerPl | kSpeakerPr

    public const ulong Surround30 = Stereo | (1UL << 3); // Stereo + Center
    public const ulong Surround40 = Stereo | StereoSurround;
    public const ulong Surround50 = Surround40 | (1UL << 3); // + Center
    public const ulong Surround51 = Surround50 | (1UL << 2); // + LFE

    public static int GetChannelCount(ulong arrangement)
    {
        int count = 0;
        while (arrangement != 0)
        {
            count += (int)(arrangement & 1);
            arrangement >>= 1;
        }
        return count;
    }
}

/// <summary>
/// VST3 Process context state flags
/// </summary>
[Flags]
public enum Vst3ProcessContextFlags : uint
{
    None = 0,
    Playing = 1 << 1,
    CycleActive = 1 << 2,
    Recording = 1 << 3,
    SystemTimeValid = 1 << 8,
    ContTimeValid = 1 << 17,
    ProjectTimeMusicValid = 1 << 9,
    BarPositionValid = 1 << 11,
    CycleValid = 1 << 12,
    TempoValid = 1 << 10,
    TimeSigValid = 1 << 13,
    ChordValid = 1 << 18,
    SmpteValid = 1 << 14,
    ClockValid = 1 << 15
}

/// <summary>
/// VST3 SMPTE frame rates
/// </summary>
public enum Vst3FrameRate : uint
{
    Fps24 = 0,
    Fps25 = 1,
    Fps2997 = 2,
    Fps30 = 3,
    Fps2997Drop = 4,
    Fps30Drop = 5
}

/// <summary>
/// VST3 TUID - 16 byte unique identifier
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Vst3Tuid
{
    public byte B0, B1, B2, B3, B4, B5, B6, B7;
    public byte B8, B9, B10, B11, B12, B13, B14, B15;

    public Vst3Tuid(Guid guid)
    {
        var bytes = guid.ToByteArray();
        // Convert from .NET GUID format to VST3 TUID format
        // .NET: DDDDDDDD-DDDD-DDDD-DDDD-DDDDDDDDDDDD (little endian first 3 groups)
        // VST3: Big endian throughout
        B0 = bytes[3]; B1 = bytes[2]; B2 = bytes[1]; B3 = bytes[0];
        B4 = bytes[5]; B5 = bytes[4];
        B6 = bytes[7]; B7 = bytes[6];
        B8 = bytes[8]; B9 = bytes[9]; B10 = bytes[10]; B11 = bytes[11];
        B12 = bytes[12]; B13 = bytes[13]; B14 = bytes[14]; B15 = bytes[15];
    }

    public Guid ToGuid()
    {
        var bytes = new byte[16]
        {
            B3, B2, B1, B0,
            B5, B4,
            B7, B6,
            B8, B9, B10, B11, B12, B13, B14, B15
        };
        return new Guid(bytes);
    }

    public static Vst3Tuid FromGuid(Guid guid) => new(guid);

    public override string ToString()
    {
        return $"{B0:X2}{B1:X2}{B2:X2}{B3:X2}-{B4:X2}{B5:X2}-{B6:X2}{B7:X2}-{B8:X2}{B9:X2}-{B10:X2}{B11:X2}{B12:X2}{B13:X2}{B14:X2}{B15:X2}";
    }

    public bool IsEmpty()
    {
        return B0 == 0 && B1 == 0 && B2 == 0 && B3 == 0 &&
               B4 == 0 && B5 == 0 && B6 == 0 && B7 == 0 &&
               B8 == 0 && B9 == 0 && B10 == 0 && B11 == 0 &&
               B12 == 0 && B13 == 0 && B14 == 0 && B15 == 0;
    }
}

/// <summary>
/// VST3 Factory info structure
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct Vst3FactoryInfo
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Vendor;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Url;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Email;

    public int Flags;
}

/// <summary>
/// VST3 Class info structure (version 1)
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct Vst3ClassInfo
{
    public Vst3Tuid Cid;

    public int Cardinality;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string Category;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Name;
}

/// <summary>
/// VST3 Class info structure (version 2)
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct Vst3ClassInfo2
{
    public Vst3Tuid Cid;

    public int Cardinality;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string Category;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Name;

    public uint ClassFlags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string SubCategories;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Vendor;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Version;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string SdkVersion;
}

/// <summary>
/// VST3 Unicode class info (IPluginFactory3)
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct Vst3ClassInfoW
{
    public Vst3Tuid Cid;

    public int Cardinality;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string Category;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Name;

    public uint ClassFlags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string SubCategories;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Vendor;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Version;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string SdkVersion;
}

/// <summary>
/// VST3 constants
/// </summary>
public static class Vst3Constants
{
    public const int MaxNameLength = 128;
    public const int MaxShortNameLength = 128;
    public const int MaxCategoryLength = 32;
    public const int MaxFileNameLength = 100;
    public const int MaxVendorLength = 64;
    public const int MaxVersionLength = 64;

    public const uint NoParentUnitId = 0xFFFFFFFF;
    public const int RootUnitId = 0;
    public const int NoProgramListId = -1;

    public const double DefaultGain = 1.0;
    public const double MaxGain = 10.0;
    public const int MaxDimensions = 3;
    public const int MaxBusChannels = 128;
}
