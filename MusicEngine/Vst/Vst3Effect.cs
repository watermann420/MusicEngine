// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VST3 effect wrapper for audio routing chains.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MusicEngine.Core;
using NAudio.Wave;

namespace MusicEngine.Vst;

public sealed class Vst3Effect : IAudioEffect
{
    private readonly IntPtr _hostHandle;
    private readonly int _inputChannels;
    private readonly int _outputChannels;
    private bool _disposed;
    private bool _attached;
    private Vst3EffectProcessor? _processor;
    private Dictionary<string, int>? _parameterMap;

    public Vst3Effect(string pluginPath, string name)
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

        _inputChannels = Math.Max(0, Vst3Native.Vst3Host_GetInputChannels(_hostHandle));
        _outputChannels = Math.Max(1, Vst3Native.Vst3Host_GetOutputChannels(_hostHandle));
        Name = name;
    }

    public string Name { get; }

    public ISampleProvider Attach(ISampleProvider input, WaveFormat targetFormat)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(Vst3Effect));
        if (_attached || _processor != null) throw new InvalidOperationException($"VST effect already attached: {Name}");
        _attached = true;
        _processor = new Vst3EffectProcessor(this, input, targetFormat);
        return _processor;
    }

    public void Detach()
    {
        _attached = false;
        if (_processor != null)
        {
            _processor.Dispose();
            _processor = null;
        }
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

        public Vst3EffectProcessor(Vst3Effect owner, ISampleProvider input, WaveFormat targetFormat)
        {
            _owner = owner;
            _inputChannels = owner._inputChannels > 0 ? owner._inputChannels : targetFormat.Channels;
            _outputChannels = owner._outputChannels > 0 ? owner._outputChannels : targetFormat.Channels;
            _input = AudioFormatAdapter.EnsureFormat(input, targetFormat.SampleRate, _inputChannels);
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(targetFormat.SampleRate, targetFormat.Channels);
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

            var ok = Process(inputTemp, outputTemp, frames);
            if (!ok)
            {
                Array.Clear(outputTemp, 0, outputCount);
            }

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

    }
}
