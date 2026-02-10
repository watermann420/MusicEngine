// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VST3 effect wrapper for audio routing chains.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MusicEngine.Core;
using MusicEngine.Effects.Audio;
using MusicEngine.Vst;
using NAudio.Wave;

namespace MusicEngine.Effects.Vst;

/// <summary>
/// VST3 effect wrapper for audio routing chains.
/// </summary>
public sealed class Vst3Effect : IAudioEffect
{
    private readonly string _pluginPath;
    private readonly IntPtr _hostHandle;
    private readonly int _inputChannels;
    private readonly int _outputChannels;
    private readonly object _stateLock = new();
    private bool _disposed;
    private bool _attached;
    private Vst3EffectProcessor? _processor;
    private Dictionary<string, int>? _parameterMap;

    /// <summary>
    /// Create a VST3 effect from a plugin path.
    /// </summary>
    /// <param name="pluginPath">Path to the VST3 plugin.</param>
    /// <param name="name">Display name for the instance.</param>
    public Vst3Effect(string pluginPath, string name)
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

        _inputChannels = Math.Max(0, Vst3Native.Vst3Host_GetInputChannels(_hostHandle));
        _outputChannels = Math.Max(1, Vst3Native.Vst3Host_GetOutputChannels(_hostHandle));
        Name = name;
    }

    /// <summary>
    /// Display name for the effect.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// When enabled, processing sleeps after silence to save CPU.
    /// </summary>
    public bool SleepWhenIdle { get; set; } = Settings.VstEffectSleepWhenIdle;

    /// <summary>
    /// Silence threshold for idle detection.
    /// </summary>
    public float IdleThreshold { get; set; } = Settings.VstIdleThreshold;

    /// <summary>
    /// Seconds of silence before sleeping.
    /// </summary>
    public double IdleTimeoutSeconds { get; set; } = Settings.VstIdleTimeoutSeconds;

    /// <summary>
    /// Attach the effect to an input stream.
    /// </summary>
    public ISampleProvider Attach(ISampleProvider input, WaveFormat targetFormat)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(Vst3Effect));
        if (_attached || _processor != null) throw new InvalidOperationException($"VST effect already attached: {Name}");
        _attached = true;
        _processor = new Vst3EffectProcessor(this, input, targetFormat);
        return _processor;
    }

    /// <summary>
    /// Detach and release processing state.
    /// </summary>
    public void Detach()
    {
        _attached = false;
        if (_processor != null)
        {
            _processor.Dispose();
            _processor = null;
        }
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
        _processor?.Wake();
        Vst3Native.Vst3Host_SetParameter(_hostHandle, id, normalized);
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
    /// Open the VST3 editor window for this effect.
    /// </summary>
    public void OpenEditor()
    {
        Vst3EditorWindow.OpenExisting(_hostHandle, Name, _pluginPath);
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
        if (_disposed) return;
        if (data == null || data.Length == 0) return;
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
    /// Close the plugin and release native resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        VstUiContext.Shared.Invoke(() =>
        {
            Vst3Native.Vst3Host_Close(_hostHandle);
            return 0;
        });
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

    private sealed class Vst3EffectProcessor : ISampleProvider, IDisposable
    {
        private readonly Vst3Effect _owner;
        private readonly ISampleProvider _input;
        private readonly WaveFormat _waveFormat;
        private readonly int _inputChannels;
        private readonly int _outputChannels;
        private int _lastBlockSize;
        private float[]? _inputBuffer;
        private float[]? _outputBuffer;
        private bool _inputBufferFromPool;
        private bool _outputBufferFromPool;
        private bool _isSleeping;
        private long _lastActivityTick;

        public Vst3EffectProcessor(Vst3Effect owner, ISampleProvider input, WaveFormat targetFormat)
        {
            _owner = owner;
            _inputChannels = owner._inputChannels > 0 ? owner._inputChannels : targetFormat.Channels;
            _outputChannels = owner._outputChannels > 0 ? owner._outputChannels : targetFormat.Channels;
            _input = AudioFormatAdapter.EnsureFormat(input, targetFormat.SampleRate, _inputChannels);
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(targetFormat.SampleRate, targetFormat.Channels);
            _lastActivityTick = Environment.TickCount64;
        }

        public WaveFormat WaveFormat => _waveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            if (_owner._disposed) return 0;
            if (count == 0) return 0;

            int frames = count / _waveFormat.Channels;
            if (frames <= 0) return 0;

            EnsureSetup(frames);

            var inputCount = frames * _inputChannels;
            var outputCount = frames * _outputChannels;

            var inputTemp = GetInputBuffer(inputCount);
            var outputTemp = GetOutputBuffer(outputCount);

            int read = _input.Read(inputTemp, 0, inputCount);
            if (read < inputCount)
            {
                Array.Clear(inputTemp, read, inputCount - read);
            }

            if (!Settings.VstEffectsEnabled)
            {
                CopyInputAsOutput(inputTemp, buffer, offset, frames);
                return count;
            }

            var inputSilent = AudioSilence.IsSilent(inputTemp, 0, inputCount, _owner.IdleThreshold);
            if (_owner.SleepWhenIdle && _isSleeping && inputSilent)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

            var ok = Process(inputTemp, outputTemp, frames);
            if (!ok)
            {
                Array.Clear(outputTemp, 0, outputCount);
            }

            var outputSilent = AudioSilence.IsSilent(outputTemp, 0, outputCount, _owner.IdleThreshold);
            UpdateSleepState(inputSilent, outputSilent);

            CopyOutput(outputTemp, buffer, offset, frames);
            return count;
        }

        public void Dispose()
        {
            if (_inputBuffer != null && _inputBufferFromPool)
            {
                ArrayPool<float>.Shared.Return(_inputBuffer);
            }
            if (_outputBuffer != null && _outputBufferFromPool)
            {
                ArrayPool<float>.Shared.Return(_outputBuffer);
            }
            _inputBuffer = null;
            _outputBuffer = null;
            _inputBufferFromPool = false;
            _outputBufferFromPool = false;
        }

        private void EnsureSetup(int frames)
        {
            if (frames == _lastBlockSize) return;
            Vst3Native.Vst3Host_SetupAudio(_owner._hostHandle, Settings.SampleRate, frames);
            _lastBlockSize = frames;
        }

        public void Wake()
        {
            _isSleeping = false;
            _lastActivityTick = Environment.TickCount64;
        }

        private bool Process(float[] input, float[] output, int frames)
        {
            var inputHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
            var outputHandle = GCHandle.Alloc(output, GCHandleType.Pinned);
            try
            {
                if (_inputChannels <= 0)
                {
                    return Vst3Native.Vst3Host_Process(_owner._hostHandle, outputHandle.AddrOfPinnedObject(),
                        frames, _outputChannels);
                }

                return Vst3Native.Vst3Host_ProcessWithInput(_owner._hostHandle, inputHandle.AddrOfPinnedObject(),
                    outputHandle.AddrOfPinnedObject(), frames, _inputChannels, _outputChannels);
            }
            finally
            {
                inputHandle.Free();
                outputHandle.Free();
            }
        }

        private void CopyOutput(float[] output, float[] target, int offset, int frames)
        {
            var targetChannels = _waveFormat.Channels;
            if (_outputChannels == targetChannels)
            {
                Array.Copy(output, 0, target, offset, frames * targetChannels);
                return;
            }

            for (int frame = 0; frame < frames; frame++)
            {
                for (int ch = 0; ch < targetChannels; ch++)
                {
                    float sample = 0f;
                    if (_outputChannels == 1)
                    {
                        sample = output[frame];
                    }
                    else if (_outputChannels == 2 && targetChannels == 1)
                    {
                        sample = 0.5f * (output[frame * 2] + output[frame * 2 + 1]);
                    }
                    else if (ch < _outputChannels)
                    {
                        sample = output[frame * _outputChannels + ch];
                    }

                    target[offset + frame * targetChannels + ch] = sample;
                }
            }
        }

        private float[] GetInputBuffer(int count)
        {
            if (_inputBuffer == null || _inputBuffer.Length < count)
            {
                _inputBuffer = ArrayPool<float>.Shared.Rent(count);
                _inputBufferFromPool = true;
            }
            return _inputBuffer;
        }

        private float[] GetOutputBuffer(int count)
        {
            if (_outputBuffer == null || _outputBuffer.Length < count)
            {
                _outputBuffer = ArrayPool<float>.Shared.Rent(count);
                _outputBufferFromPool = true;
            }
            return _outputBuffer;
        }

        private void UpdateSleepState(bool inputSilent, bool outputSilent)
        {
            if (!_owner.SleepWhenIdle) return;
            if (!inputSilent || !outputSilent)
            {
                _isSleeping = false;
                _lastActivityTick = Environment.TickCount64;
                return;
            }

            if (_owner.IdleTimeoutSeconds <= 0)
            {
                _isSleeping = true;
                return;
            }

            var elapsed = TimeSpan.FromMilliseconds(Environment.TickCount64 - _lastActivityTick);
            if (elapsed.TotalSeconds >= _owner.IdleTimeoutSeconds)
            {
                _isSleeping = true;
            }
        }

        private void CopyInputAsOutput(float[] input, float[] target, int offset, int frames)
        {
            var targetChannels = _waveFormat.Channels;
            if (_inputChannels == targetChannels)
            {
                Array.Copy(input, 0, target, offset, frames * targetChannels);
                return;
            }

            for (int frame = 0; frame < frames; frame++)
            {
                for (int ch = 0; ch < targetChannels; ch++)
                {
                    float sample = 0f;
                    if (_inputChannels == 1)
                    {
                        sample = input[frame];
                    }
                    else if (_inputChannels == 2 && targetChannels == 1)
                    {
                        sample = 0.5f * (input[frame * 2] + input[frame * 2 + 1]);
                    }
                    else if (ch < _inputChannels)
                    {
                        sample = input[frame * _inputChannels + ch];
                    }

                    target[offset + frame * targetChannels + ch] = sample;
                }
            }
        }
    }
}
