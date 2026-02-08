// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VST3-backed instrument wrapper for MIDI routing.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using ThreadingTimer = System.Threading.Timer;
using MusicEngine.Core;
using NAudio.Wave;

namespace MusicEngine.Vst;

/// <summary>
/// VST3-backed instrument wrapper for MIDI routing and audio output.
/// </summary>
public sealed class Vst3Instrument : IVstInstrument, IDisposable
{
    private readonly string _pluginPath;
    private readonly IntPtr _hostHandle;
    private readonly int _outputChannels;
    private readonly WaveFormat _waveFormat;
    private int _lastBlockSize;
    private bool _disposed;
    private float[]? _tempBuffer;
    private bool _tempBufferFromPool;
    private Dictionary<string, int>? _parameterMap;
    private readonly object _stateLock = new();
    private ThreadingTimer? _autoSaveTimer;
    private bool _autoSaveEnabled = true;
    private string? _autoStatePath;
    private double _autoSaveIntervalSeconds = 2.0;

    /// <summary>
    /// Create a VST3 instrument from a plugin path.
    /// </summary>
    /// <param name="pluginPath">Path to the VST3 plugin.</param>
    /// <param name="name">Display name for the instance.</param>
    public Vst3Instrument(string pluginPath, string name, string? statePath = null)
    {
        if (string.IsNullOrWhiteSpace(pluginPath))
        {
            throw new ArgumentException("Plugin path is required.", nameof(pluginPath));
        }

        _pluginPath = pluginPath;
        _hostHandle = VstUiContext.Shared.Invoke(() => Vst3Native.Vst3Host_Create(pluginPath));
        if (_hostHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to load VST3: {pluginPath}");
        }

        _outputChannels = Math.Max(1, Vst3Native.Vst3Host_GetOutputChannels(_hostHandle));
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(Settings.SampleRate, _outputChannels);
        Name = name;

        _autoStatePath = !string.IsNullOrWhiteSpace(statePath) ? statePath : GetDefaultStatePath(name);
        if (!string.IsNullOrWhiteSpace(_autoStatePath) && File.Exists(_autoStatePath))
        {
            LoadState(_autoStatePath);
        }
        EnsureAutoSaveTimer();
    }

    /// <summary>
    /// Display name for the instance.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Master volume (0..1).
    /// </summary>
    public float Volume { get; set; } = 1f;

    /// <summary>
    /// Pan position (-1..1).
    /// </summary>
    public float Pan { get; set; } = 0f;

    /// <summary>
    /// Mod wheel value (0..1).
    /// </summary>
    public float ModWheel { get; set; } = 0f;

    /// <summary>
    /// MIDI channel (0..15), or -1 for all.
    /// </summary>
    public int Channel { get; set; } = -1;

    /// <summary>
    /// Reverb amount (0..1).
    /// </summary>
    public float Reverb { get; set; } = 0f;

    /// <summary>
    /// Chorus amount (0..1).
    /// </summary>
    public float Chorus { get; set; } = 0f;

    /// <summary>
    /// Output format for this instrument.
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Read audio samples into the target buffer.
    /// </summary>
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
            ApplyVolumePan(buffer, 0, count);
            return count;
        }

        var temp = GetTempBuffer(count);
        if (!Process(temp, frames))
        {
            Array.Clear(temp, 0, count);
        }
        ApplyVolumePan(temp, 0, count);
        Array.Copy(temp, 0, buffer, offset, count);
        return count;
    }

    /// <summary>
    /// Trigger a MIDI note-on.
    /// </summary>
    public void NoteOn(int note, int velocity) => Vst3Native.Vst3Host_SendNoteOn(_hostHandle, note, velocity, 0);

    /// <summary>
    /// Trigger a MIDI note-off.
    /// </summary>
    public void NoteOff(int note) => Vst3Native.Vst3Host_SendNoteOff(_hostHandle, note, 0, 0);

    /// <summary>
    /// Send all-notes-off to the plugin.
    /// </summary>
    public void AllNotesOff() => Vst3Native.Vst3Host_AllNotesOff(_hostHandle, 0);

    /// <summary>
    /// Reset common performance state such as notes and pitch bend.
    /// </summary>
    public void ResetState()
    {
        Vst3Native.Vst3Host_AllNotesOff(_hostHandle, 0);
        Vst3Native.Vst3Host_SendPitchBend(_hostHandle, 0f, 0);
    }

    /// <summary>
    /// Send pitch bend in normalized range [-1, 1].
    /// </summary>
    public void PitchBend(float normalized)
    {
        normalized = Math.Clamp(normalized, -1f, 1f);
        Vst3Native.Vst3Host_SendPitchBend(_hostHandle, normalized, 0);
    }

    /// <summary>
    /// Set a named parameter (normalized).
    /// </summary>
    public void SetParameter(string name, float value)
    {
        SetParameterNormalized(name, value);
    }

    /// <summary>
    /// Set a named parameter with normalized value in [0, 1].
    /// </summary>
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

    private void ApplyVolumePan(float[] buffer, int offset, int count)
    {
        float volume = Math.Clamp(Volume, 0f, 1f);
        if (volume <= 0f)
        {
            Array.Clear(buffer, offset, count);
            return;
        }

        float pan = Math.Clamp(Pan, -1f, 1f);
        float panL = Math.Min(1f, 1f - pan);
        float panR = Math.Min(1f, 1f + pan);

        int channels = _waveFormat.Channels;
        if (channels <= 1)
        {
            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] *= volume;
            }
            return;
        }

        int frames = count / channels;
        for (int i = 0; i < frames; i++)
        {
            int idx = offset + i * channels;
            buffer[idx] *= volume * panL;
            buffer[idx + 1] *= volume * panR;
            for (int ch = 2; ch < channels; ch++)
            {
                buffer[idx + ch] *= volume;
            }
        }
    }

    /// <summary>
    /// Create a setter for automation.
    /// </summary>
    public Action<float> Param(string name, float min = 0f, float max = 1f)
    {
        return value =>
        {
            var scaled = min + value * (max - min);
            SetParameterNormalized(name, scaled);
        };
    }

    /// <summary>
    /// Open the VST3 editor window for this instrument.
    /// </summary>
    public void OpenEditor()
    {
        Vst3EditorWindow.OpenExisting(_hostHandle, Name, _pluginPath);
    }

    /// <summary>
    /// Disable automatic state save/load.
    /// </summary>
    public void NoSave()
    {
        _autoSaveEnabled = false;
        _autoSaveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Get or set the state as base64.
    /// </summary>
    public string State(string? base64 = null)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return GetStateBase64();
        }

        SetStateBase64(base64);
        return base64;
    }

    /// <summary>
    /// Get the plugin state as a binary blob.
    /// </summary>
    public byte[] GetState()
    {
        if (_disposed) return Array.Empty<byte>();
        return VstUiContext.Shared.Invoke(() =>
        {
            lock (_stateLock)
            {
                return GetStateInternal();
            }
        });
    }

    /// <summary>
    /// Load the plugin state from a binary blob.
    /// </summary>
    public void SetState(byte[] data)
    {
        if (data == null || data.Length == 0 || _disposed) return;
        VstUiContext.Shared.Invoke(() =>
        {
            lock (_stateLock)
            {
                SetStateInternal(data);
            }
            return 0;
        });
    }

    /// <summary>
    /// Save the plugin state to a file.
    /// </summary>
    public void SaveState(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        _autoStatePath = path;
        _autoSaveEnabled = true;
        EnsureAutoSaveTimer();
        SaveStateOnce(path);
    }

    /// <summary>
    /// Load the plugin state from a file.
    /// </summary>
    public void LoadState(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        _autoStatePath = path;
        _autoSaveEnabled = true;
        EnsureAutoSaveTimer();
        LoadStateOnce(path);
    }

    /// <summary>
    /// Save the current state using the active auto-save path.
    /// </summary>
    public void SaveStateNow()
    {
        if (string.IsNullOrWhiteSpace(_autoStatePath)) return;
        SaveStateOnce(_autoStatePath);
    }

    /// <summary>
    /// Close the plugin and release native resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_autoSaveEnabled && !string.IsNullOrWhiteSpace(_autoStatePath))
        {
            SaveStateOnce(_autoStatePath);
        }
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = null;
        VstUiContext.Shared.Invoke(() =>
        {
            Vst3Native.Vst3Host_Close(_hostHandle);
            return 0;
        });
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

    private void EnsureAutoSaveTimer()
    {
        if (!_autoSaveEnabled) return;
        if (string.IsNullOrWhiteSpace(_autoStatePath)) return;

        var due = TimeSpan.FromSeconds(Math.Max(1.0, _autoSaveIntervalSeconds));
        if (_autoSaveTimer == null)
        {
            _autoSaveTimer = new ThreadingTimer(_ => SaveStateOnce(_autoStatePath), null, due, due);
        }
        else
        {
            _autoSaveTimer.Change(due, due);
        }
    }

    private void SaveStateOnce(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var data = GetState();
        if (data.Length == 0) return;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
        try
        {
            File.WriteAllBytes(path, data);
        }
        catch
        {
        }
    }

    private void LoadStateOnce(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!File.Exists(path)) return;
        try
        {
            var data = File.ReadAllBytes(path);
            SetState(data);
        }
        catch
        {
        }
    }

    private static string GetDefaultStatePath(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var safe = SanitizeFileName(name);
        if (string.IsNullOrWhiteSpace(safe)) return string.Empty;
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = AppContext.BaseDirectory;
        }
        return Path.Combine(root, "MusicEngine", "States", $"{safe}.state");
    }

    public static string? GetScriptStatePath(string instanceName, string? scriptFilePath)
    {
        if (string.IsNullOrWhiteSpace(instanceName)) return null;
        var safeName = SanitizeFileName(instanceName);
        if (string.IsNullOrWhiteSpace(safeName)) return null;

        string baseDir;
        string scriptName;
        if (!string.IsNullOrWhiteSpace(scriptFilePath))
        {
            baseDir = Path.GetDirectoryName(scriptFilePath) ?? AppContext.BaseDirectory;
            scriptName = Path.GetFileNameWithoutExtension(scriptFilePath);
        }
        else
        {
            baseDir = AppContext.BaseDirectory;
            scriptName = "Global";
        }

        var safeScript = SanitizeFileName(scriptName);
        if (string.IsNullOrWhiteSpace(safeScript))
        {
            safeScript = "Global";
        }
        return Path.Combine(baseDir, ".musicengine", "states", safeScript, $"{safeName}.state");
    }

    public static string? GetScriptStateDirectory(string? scriptFilePath)
    {
        string baseDir;
        string scriptName;
        if (!string.IsNullOrWhiteSpace(scriptFilePath))
        {
            baseDir = Path.GetDirectoryName(scriptFilePath) ?? AppContext.BaseDirectory;
            scriptName = Path.GetFileNameWithoutExtension(scriptFilePath);
        }
        else
        {
            baseDir = AppContext.BaseDirectory;
            scriptName = "Global";
        }

        var safeScript = SanitizeFileName(scriptName);
        if (string.IsNullOrWhiteSpace(safeScript))
        {
            safeScript = "Global";
        }
        return Path.Combine(baseDir, ".musicengine", "states", safeScript);
    }

    private byte[] GetStateInternal()
    {
        int size = Vst3Native.Vst3Host_GetStateSize(_hostHandle);
        if (size <= 0) return Array.Empty<byte>();

        var data = new byte[size];
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            int written = Vst3Native.Vst3Host_GetState(_hostHandle, handle.AddrOfPinnedObject(), size);
            if (written <= 0) return Array.Empty<byte>();
            if (written == size) return data;
            var trimmed = new byte[written];
            Array.Copy(data, trimmed, written);
            return trimmed;
        }
        finally
        {
            handle.Free();
        }
    }

    private void SetStateInternal(byte[] data)
    {
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            Vst3Native.Vst3Host_SetState(_hostHandle, handle.AddrOfPinnedObject(), data.Length);
        }
        finally
        {
            handle.Free();
        }
    }

    private string GetStateBase64()
    {
        var data = GetState();
        return data.Length == 0 ? string.Empty : Convert.ToBase64String(data);
    }

    private void SetStateBase64(string base64)
    {
        try
        {
            var data = Convert.FromBase64String(base64);
            if (data.Length == 0) return;
            SetState(data);
        }
        catch
        {
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name.Trim();
    }
}
