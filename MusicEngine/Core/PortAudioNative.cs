#if !WINDOWS
// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Minimal PortAudio P/Invoke bridge.

using System;
using System.Runtime.InteropServices;

namespace MusicEngine.Core;

internal static class PortAudioNative
{
    private const string LibraryName = "portaudio";
    public const ulong PaFloat32 = 0x00000001;

    private static int _refCount;

    public static void EnsureInitialized()
    {
        if (_refCount++ == 0)
        {
            Initialize();
        }
    }

    public static void Release()
    {
        if (_refCount <= 0) return;
        _refCount--;
        if (_refCount == 0)
        {
            Terminate();
        }
    }

    public static int OpenDefaultStream(out IntPtr stream, int inputChannels, int outputChannels, ulong sampleFormat,
        int sampleRate, uint framesPerBuffer)
    {
        return Pa_OpenDefaultStream(out stream, inputChannels, outputChannels, sampleFormat, sampleRate, framesPerBuffer,
            IntPtr.Zero, IntPtr.Zero);
    }

    public static int StartStream(IntPtr stream) => Pa_StartStream(stream);
    public static int StopStream(IntPtr stream) => Pa_StopStream(stream);
    public static int CloseStream(IntPtr stream) => Pa_CloseStream(stream);
    public static int WriteStream(IntPtr stream, float[] buffer, uint frames)
        => Pa_WriteStream(stream, buffer, frames);

    public static int GetDeviceCount() => Pa_GetDeviceCount();

    public static PaDeviceInfo? GetDeviceInfo(int index)
    {
        var infoPtr = Pa_GetDeviceInfo(index);
        if (infoPtr == IntPtr.Zero) return null;
        return Marshal.PtrToStructure<PaDeviceInfo>(infoPtr);
    }

    private static void Initialize()
    {
        var result = Pa_Initialize();
        if (result != 0)
        {
            throw new InvalidOperationException($"PortAudio initialization failed ({result}).");
        }
    }

    private static void Terminate()
    {
        Pa_Terminate();
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PaDeviceInfo
    {
        public int structVersion;
        public IntPtr name;
        public int hostApi;
        public int maxInputChannels;
        public int maxOutputChannels;
        public double defaultLowInputLatency;
        public double defaultLowOutputLatency;
        public double defaultHighInputLatency;
        public double defaultHighOutputLatency;
        public double defaultSampleRate;
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_Initialize();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_Terminate();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_GetDeviceCount();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr Pa_GetDeviceInfo(int device);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_OpenDefaultStream(out IntPtr stream, int numInputChannels, int numOutputChannels,
        ulong sampleFormat, int sampleRate, uint framesPerBuffer, IntPtr streamCallback, IntPtr userData);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_StartStream(IntPtr stream);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_StopStream(IntPtr stream);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_CloseStream(IntPtr stream);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int Pa_WriteStream(IntPtr stream, float[] buffer, uint frames);
}
#endif
