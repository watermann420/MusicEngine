// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: P/Invoke bridge for native VST3 host.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MusicEngine.Vst;

internal static class Vst3Native
{
    private const string DllName = "MusicEngine.CppLayer.Native.dll";
    private const string CreateExport = "Vst3Host_Create";
    private const string OpenExport = "Vst3Host_OpenEditor";
    private const string CloseExport = "Vst3Host_Close";
    private const string SetupExport = "Vst3Host_SetupAudio";
    private const string ProcessExport = "Vst3Host_Process";
    private const string ProcessInputExport = "Vst3Host_ProcessWithInput";
    private const string NoteOnExport = "Vst3Host_SendNoteOn";
    private const string NoteOffExport = "Vst3Host_SendNoteOff";
    private const string AllNotesOffExport = "Vst3Host_AllNotesOff";
    private const string OutputChannelsExport = "Vst3Host_GetOutputChannels";
    private const string InputChannelsExport = "Vst3Host_GetInputChannels";
    private const string PitchBendExport = "Vst3Host_SendPitchBend";
    private const string ParamCountExport = "Vst3Host_GetParameterCount";
    private const string ParamInfoExport = "Vst3Host_GetParameterInfo";
    private const string ParamSetExport = "Vst3Host_SetParameter";

    public static bool TryValidate(out string message)
    {
        EnsureNativeDllCopied();
        var dllPath = Path.Combine(AppContext.BaseDirectory, DllName);
        if (!File.Exists(dllPath))
        {
            message = $"Missing native DLL: {dllPath}";
            return false;
        }

        if (!NativeLibrary.TryLoad(dllPath, out var handle))
        {
            message = $"Failed to load native DLL: {dllPath}";
            return false;
        }

        var ok = NativeLibrary.TryGetExport(handle, CreateExport, out _) &&
                 NativeLibrary.TryGetExport(handle, OpenExport, out _) &&
                 NativeLibrary.TryGetExport(handle, CloseExport, out _) &&
                 NativeLibrary.TryGetExport(handle, SetupExport, out _) &&
                 NativeLibrary.TryGetExport(handle, ProcessExport, out _) &&
                 NativeLibrary.TryGetExport(handle, ProcessInputExport, out _) &&
                 NativeLibrary.TryGetExport(handle, NoteOnExport, out _) &&
                 NativeLibrary.TryGetExport(handle, NoteOffExport, out _) &&
                 NativeLibrary.TryGetExport(handle, AllNotesOffExport, out _) &&
                 NativeLibrary.TryGetExport(handle, OutputChannelsExport, out _) &&
                 NativeLibrary.TryGetExport(handle, InputChannelsExport, out _) &&
                 NativeLibrary.TryGetExport(handle, PitchBendExport, out _) &&
                 NativeLibrary.TryGetExport(handle, ParamCountExport, out _) &&
                 NativeLibrary.TryGetExport(handle, ParamInfoExport, out _) &&
                 NativeLibrary.TryGetExport(handle, ParamSetExport, out _);
        NativeLibrary.Free(handle);

        message = ok
            ? "Native VST3 host loaded."
            : "Native VST3 host missing exports. Rebuild MusicEngine.CppLayer.Native (x64).";
        return ok;
    }

    private static void EnsureNativeDllCopied()
    {
        var targetPath = Path.Combine(AppContext.BaseDirectory, DllName);
        if (File.Exists(targetPath)) return;

        var searchRoot = AppContext.BaseDirectory;
        var root = searchRoot;
        while (root != null && !File.Exists(Path.Combine(root, "MusicEngine.slnx")))
        {
            root = Directory.GetParent(root)?.FullName;
        }
        if (root == null) return;

        var candidates = new[]
        {
            Path.Combine(root, "MusicEngine.CppLayer", "native", "x64", "Debug", DllName),
            Path.Combine(root, "MusicEngine.CppLayer", "native", "x64", "Release", DllName)
        };

        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate)) continue;
            try
            {
                File.Copy(candidate, targetPath, overwrite: true);
                return;
            }
            catch
            {
                return;
            }
        }
    }

    [DllImport(DllName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Vst3Host_Create(string path);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool Vst3Host_OpenEditor(IntPtr handle, IntPtr hwnd);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Vst3Host_Close(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool Vst3Host_SetupAudio(IntPtr handle, int sampleRate, int blockSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Vst3Host_GetOutputChannels(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Vst3Host_GetInputChannels(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool Vst3Host_Process(IntPtr handle, IntPtr outputInterleaved, int frames, int channels);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool Vst3Host_ProcessWithInput(IntPtr handle, IntPtr inputInterleaved,
        IntPtr outputInterleaved, int frames, int inputChannels, int outputChannels);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Vst3Host_SendNoteOn(IntPtr handle, int note, int velocity, int channel);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Vst3Host_SendNoteOff(IntPtr handle, int note, int velocity, int channel);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Vst3Host_AllNotesOff(IntPtr handle, int channel);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Vst3Host_SendPitchBend(IntPtr handle, float normalized, int channel);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Vst3Host_GetParameterCount(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool Vst3Host_GetParameterInfo(IntPtr handle, int index, out int id, StringBuilder name, int nameCapacity);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Vst3Host_SetParameter(IntPtr handle, int id, double normalized);
}
