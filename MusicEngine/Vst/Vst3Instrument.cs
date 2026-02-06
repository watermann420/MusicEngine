// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VST3-backed instrument wrapper for MIDI routing.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MusicEngine.Core;
using NAudio.Wave;

namespace MusicEngine.Vst;

public sealed class Vst3Instrument : ISynth, IDisposable
{
    private readonly IntPtr _hostHandle;
    private readonly int _outputChannels;
    private readonly WaveFormat _waveFormat;
    private int _lastBlockSize;
    private bool _disposed;
    private float[]? _tempBuffer;
    private bool _tempBufferFromPool;
    private Dictionary<string, int>? _parameterMap;

    public Vst3Instrument(string pluginPath, string name)
    {
        if (string.IsNullOrWhiteSpace(pluginPath))
        {
            throw new ArgumentException("Plugin path is required.", nameof(pluginPath));
        }

        _hostHandle = VstUiContext.Shared.Invoke(() => Vst3Native.Vst3Host_Create(pluginPath));
        if (_hostHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to load VST3: {pluginPath}");
        }

        _outputChannels = Math.Max(1, Vst3Native.Vst3Host_GetOutputChannels(_hostHandle));
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(Settings.SampleRate, _outputChannels);
        Name = name;
    }

    public string Name { get; set; }

    public WaveFormat WaveFormat => _waveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        if (_disposed) return 0;
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 0) return 0;

        int frames = count / _outputChannels;
        if (frames <= 0) return 0;

        EnsureSetup(frames);

        if (offset == 0 && count == buffer.Length)
        {
            if (!Process(buffer, frames))
            {
                Array.Clear(buffer, 0, count);
            }
            return count;
        }

        var temp = GetTempBuffer(count);
        if (!Process(temp, frames))
        {
            Array.Clear(temp, 0, count);
        }
        Array.Copy(temp, 0, buffer, offset, count);
        return count;
    }

    public void NoteOn(int note, int velocity) => Vst3Native.Vst3Host_SendNoteOn(_hostHandle, note, velocity, 0);

    public void NoteOff(int note) => Vst3Native.Vst3Host_SendNoteOff(_hostHandle, note, 0, 0);

    public void AllNotesOff() => Vst3Native.Vst3Host_AllNotesOff(_hostHandle, 0);

    public void PitchBend(float normalized)
    {
        normalized = Math.Clamp(normalized, -1f, 1f);
        Vst3Native.Vst3Host_SendPitchBend(_hostHandle, normalized, 0);
    }

    public void SetParameter(string name, float value)
    {
        SetParameterNormalized(name, value);
    }

    public void SetParameterNormalized(string name, float value)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        EnsureParameterMap();
        if (_parameterMap == null || !_parameterMap.TryGetValue(name, out var id))
        {
            throw new InvalidOperationException($"VST parameter not found: {name}");
        }

        var normalized = Math.Clamp(value, 0f, 1f);
        Vst3Native.Vst3Host_SetParameter(_hostHandle, id, normalized);
    }

    public Action<float> Param(string name, float min = 0f, float max = 1f)
    {
        return value =>
        {
            var scaled = min + value * (max - min);
            SetParameterNormalized(name, scaled);
        };
    }

    public void OpenEditor()
    {
        Vst3EditorWindow.OpenExisting(_hostHandle, Name);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Vst3Native.Vst3Host_Close(_hostHandle);
        if (_tempBuffer != null && _tempBufferFromPool)
        {
            ArrayPool<float>.Shared.Return(_tempBuffer);
        }
        _tempBuffer = null;
        _tempBufferFromPool = false;
    }

    private void EnsureSetup(int frames)
    {
        if (frames == _lastBlockSize) return;
        Vst3Native.Vst3Host_SetupAudio(_hostHandle, Settings.SampleRate, frames);
        _lastBlockSize = frames;
    }

    private bool Process(float[] buffer, int frames)
    {
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            return Vst3Native.Vst3Host_Process(_hostHandle, handle.AddrOfPinnedObject(), frames, _outputChannels);
        }
        finally
        {
            handle.Free();
        }
    }

    private float[] GetTempBuffer(int count)
    {
        if (_tempBuffer == null || _tempBuffer.Length < count)
        {
            _tempBuffer = ArrayPool<float>.Shared.Rent(count);
            _tempBufferFromPool = true;
        }
        return _tempBuffer;
    }

    private void EnsureParameterMap()
    {
        if (_parameterMap != null) return;

        var count = Vst3Native.Vst3Host_GetParameterCount(_hostHandle);
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var nameBuffer = new System.Text.StringBuilder(128);

        for (var i = 0; i < count; i++)
        {
            nameBuffer.Clear();
            if (!Vst3Native.Vst3Host_GetParameterInfo(_hostHandle, i, out var id, nameBuffer, nameBuffer.Capacity))
            {
                continue;
            }

            var name = nameBuffer.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!map.ContainsKey(name))
            {
                map.Add(name, id);
            }
        }

        _parameterMap = map;
    }
}
