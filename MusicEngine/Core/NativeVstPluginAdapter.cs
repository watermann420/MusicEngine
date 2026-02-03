// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Adapter that wraps native VST plugin for use with IVstPlugin interface.

using MusicEngine.Core.Automation;
using MusicEngine.VstBridge;
using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Adapts a native VST plugin (INativeVstPlugin) to the IVstPlugin interface.
/// This enables seamless integration of native C++ VST hosting with the managed C# codebase.
/// </summary>
public sealed class NativeVstPluginAdapter : IVstPlugin
{
    private readonly INativeVstPlugin _nativePlugin;
    private readonly string _pluginPath;
    private readonly WaveFormat _waveFormat;
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isActive;
    private bool _isBypassed;
    private float _masterVolume = 1.0f;
    private ISampleProvider? _inputProvider;

    // Audio buffers
    private float[][] _inputBuffers;
    private float[][] _outputBuffers;
    private float[] _inputReadBuffer;
    private float[] _interleavedOutput;

    /// <summary>
    /// Creates a new adapter wrapping the specified native VST plugin.
    /// </summary>
    /// <param name="nativePlugin">The native plugin instance to wrap.</param>
    /// <param name="pluginPath">Full path to the plugin file.</param>
    /// <param name="sampleRate">Audio sample rate.</param>
    public NativeVstPluginAdapter(INativeVstPlugin nativePlugin, string pluginPath, int sampleRate = 0)
    {
        _nativePlugin = nativePlugin ?? throw new ArgumentNullException(nameof(nativePlugin));
        _pluginPath = pluginPath;

        int rate = sampleRate > 0 ? sampleRate : Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        int bufferSize = Settings.VstBufferSize;
        int numInputs = Math.Max(2, nativePlugin.NumInputs);
        int numOutputs = Math.Max(2, nativePlugin.NumOutputs);

        // Allocate audio buffers
        _inputBuffers = new float[numInputs][];
        _outputBuffers = new float[numOutputs][];
        for (int i = 0; i < numInputs; i++)
            _inputBuffers[i] = new float[bufferSize];
        for (int i = 0; i < numOutputs; i++)
            _outputBuffers[i] = new float[bufferSize];

        _inputReadBuffer = new float[bufferSize * 2];
        _interleavedOutput = new float[bufferSize * 2];
    }

    #region IVstPlugin Properties

    public string Name
    {
        get => _nativePlugin.Name;
        set { /* Native plugins don't support renaming */ }
    }

    public string PluginPath => _pluginPath;
    public string Vendor => _nativePlugin.Vendor;

    public string Version => _nativePlugin.Version.ToString();

    public bool IsVst3 => _nativePlugin.PluginType == VstPluginType.Vst3;

    public bool IsLoaded => _nativePlugin.IsValid;

    public bool IsInstrument => _nativePlugin.IsSynth;

    public int NumAudioInputs => _nativePlugin.NumInputs;

    public int NumAudioOutputs => _nativePlugin.NumOutputs;

    public int SampleRate => _waveFormat.SampleRate;

    public int BlockSize => Settings.VstBufferSize;

    public WaveFormat WaveFormat => _waveFormat;

    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Math.Clamp(value, 0f, 2f);
    }

    public bool HasEditor => _nativePlugin.HasEditor;

    public bool IsActive => _isActive;

    public int LatencySamples => _nativePlugin.Latency;

    public bool IsBypassed
    {
        get => _isBypassed;
        set
        {
            if (_isBypassed != value)
            {
                _isBypassed = value;
                BypassChanged?.Invoke(this, value);
            }
        }
    }

    public event EventHandler<bool>? BypassChanged;

    public ISampleProvider? InputProvider
    {
        get => _inputProvider;
        set
        {
            lock (_lock)
            {
                _inputProvider = value;
            }
        }
    }

    #endregion

    #region Audio Processing

    public void SetSampleRate(double sampleRate)
    {
        // Native host manages sample rate globally
    }

    public void SetBlockSize(int blockSize)
    {
        // Native host manages block size globally
    }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            if (_disposed) return 0;

            int samplesPerChannel = count / _waveFormat.Channels;
            int totalSamplesProcessed = 0;

            while (totalSamplesProcessed < count)
            {
                int samplesToProcess = Math.Min(samplesPerChannel, Settings.VstBufferSize);
                int interleavedSamples = samplesToProcess * _waveFormat.Channels;

                // Clear output buffers
                for (int c = 0; c < _outputBuffers.Length; c++)
                    Array.Clear(_outputBuffers[c], 0, samplesToProcess);

                // Handle input (for effects)
                if (_inputProvider != null && !IsInstrument)
                {
                    int inputRead = _inputProvider.Read(_inputReadBuffer, 0, interleavedSamples);
                    DeinterleaveAudio(_inputReadBuffer, _inputBuffers, samplesToProcess);
                }
                else
                {
                    for (int c = 0; c < _inputBuffers.Length; c++)
                        Array.Clear(_inputBuffers[c], 0, samplesToProcess);
                }

                // Handle bypass
                if (_isBypassed)
                {
                    if (!IsInstrument && _inputProvider != null)
                    {
                        for (int c = 0; c < Math.Min(_inputBuffers.Length, _outputBuffers.Length); c++)
                            Array.Copy(_inputBuffers[c], _outputBuffers[c], samplesToProcess);
                    }
                }
                else
                {
                    // Process through native plugin
                    _nativePlugin.Process(_inputBuffers, _outputBuffers, samplesToProcess);
                }

                // Interleave output with master volume
                InterleaveAudio(_outputBuffers, buffer, offset + totalSamplesProcessed, samplesToProcess, _masterVolume);

                totalSamplesProcessed += interleavedSamples;
            }

            return count;
        }
    }

    private void DeinterleaveAudio(float[] interleaved, float[][] channels, int samplesPerChannel)
    {
        int numChannels = Math.Min(channels.Length, 2);
        for (int i = 0; i < samplesPerChannel; i++)
        {
            int srcIndex = i * 2;
            if (srcIndex < interleaved.Length)
            {
                channels[0][i] = interleaved[srcIndex];
                if (numChannels > 1 && channels.Length > 1)
                    channels[1][i] = srcIndex + 1 < interleaved.Length ? interleaved[srcIndex + 1] : interleaved[srcIndex];
            }
        }
    }

    private void InterleaveAudio(float[][] channels, float[] output, int offset, int samplesPerChannel, float gain)
    {
        for (int i = 0; i < samplesPerChannel; i++)
        {
            int dstIndex = offset + i * 2;
            if (dstIndex < output.Length)
            {
                output[dstIndex] = channels[0][i] * gain;
                if (dstIndex + 1 < output.Length && channels.Length > 1)
                    output[dstIndex + 1] = channels[1][i] * gain;
            }
        }
    }

    public void Activate()
    {
        lock (_lock)
        {
            _nativePlugin.Resume();
            _nativePlugin.StartProcessing();
            _isActive = true;
        }
    }

    public void Deactivate()
    {
        lock (_lock)
        {
            _nativePlugin.StopProcessing();
            _nativePlugin.Suspend();
            _isActive = false;
        }
    }

    #endregion

    #region MIDI

    public void NoteOn(int note, int velocity)
    {
        _nativePlugin.NoteOn(0, note, velocity);
    }

    public void NoteOff(int note)
    {
        _nativePlugin.NoteOff(0, note);
    }

    public void AllNotesOff()
    {
        _nativePlugin.AllNotesOff();
    }

    public void SetParameter(string name, float value)
    {
        // Handle special parameter names
        switch (name.ToLowerInvariant())
        {
            case "volume":
            case "gain":
            case "level":
                _masterVolume = Math.Clamp(value, 0f, 2f);
                return;
            case "pitchbend":
                int bendValue = (int)(value * 16383);
                SendPitchBend(0, bendValue);
                return;
        }

        // Try to find parameter by name
        for (int i = 0; i < _nativePlugin.ParameterCount; i++)
        {
            var param = _nativePlugin.GetParameter(i);
            if (param.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                _nativePlugin[i] = Math.Clamp(value, 0f, 1f);
                return;
            }
        }

        // Try to parse as parameter index
        if (int.TryParse(name, out int paramIndex) && paramIndex >= 0 && paramIndex < _nativePlugin.ParameterCount)
        {
            _nativePlugin[paramIndex] = Math.Clamp(value, 0f, 1f);
        }
    }

    public void SendControlChange(int channel, int controller, int value)
    {
        _nativePlugin.ControlChange(channel, controller, value);
    }

    public void SendPitchBend(int channel, int value)
    {
        // Convert 14-bit value to LSB/MSB format expected by MIDI
        int lsb = value & 0x7F;
        int msb = (value >> 7) & 0x7F;
        _nativePlugin.SendMidi(0xE0 | (channel & 0x0F), lsb, msb);
    }

    public void SendProgramChange(int channel, int program)
    {
        _nativePlugin.ProgramChange(channel, program);
    }

    #endregion

    #region Parameters

    public int GetParameterCount()
    {
        return _nativePlugin.ParameterCount;
    }

    public string GetParameterName(int index)
    {
        if (index < 0 || index >= _nativePlugin.ParameterCount)
            return $"Param {index}";
        return _nativePlugin.GetParameter(index).Name;
    }

    public float GetParameterValue(int index)
    {
        if (index < 0 || index >= _nativePlugin.ParameterCount)
            return 0f;
        return _nativePlugin[index];
    }

    public void SetParameterValue(int index, float value)
    {
        if (index >= 0 && index < _nativePlugin.ParameterCount)
            _nativePlugin[index] = Math.Clamp(value, 0f, 1f);
    }

    public string GetParameterDisplay(int index)
    {
        if (index < 0 || index >= _nativePlugin.ParameterCount)
            return "";
        var param = _nativePlugin.GetParameter(index);
        return $"{param.DisplayValue} {param.Label}".Trim();
    }

    public VstParameterInfo? GetParameterInfo(int index)
    {
        if (index < 0 || index >= _nativePlugin.ParameterCount)
            return null;

        var param = _nativePlugin.GetParameter(index);
        return new VstParameterInfo
        {
            Index = param.Index,
            Name = param.Name,
            ShortName = param.Name.Length > 8 ? param.Name[..8] : param.Name,
            Label = param.Label,
            MinValue = 0f,
            MaxValue = 1f,
            DefaultValue = param.Value,
            StepCount = 0,
            IsAutomatable = true,
            IsReadOnly = false,
            ParameterId = (uint)param.Index
        };
    }

    public IReadOnlyList<VstParameterInfo> GetAllParameterInfo()
    {
        var result = new List<VstParameterInfo>();
        for (int i = 0; i < _nativePlugin.ParameterCount; i++)
        {
            var info = GetParameterInfo(i);
            if (info != null)
                result.Add(info);
        }
        return result.AsReadOnly();
    }

    public bool CanParameterBeAutomated(int index)
    {
        return index >= 0 && index < _nativePlugin.ParameterCount;
    }

    #endregion

    #region Presets

    public IReadOnlyList<string> GetPresetNames()
    {
        var names = new List<string>();
        for (int i = 0; i < _nativePlugin.ProgramCount; i++)
        {
            string name = _nativePlugin.GetProgramName(i);
            names.Add(string.IsNullOrEmpty(name) ? $"Preset {i + 1}" : name);
        }
        return names.AsReadOnly();
    }

    public void SetPreset(int index)
    {
        if (index >= 0 && index < _nativePlugin.ProgramCount)
            _nativePlugin.CurrentProgram = index;
    }

    public int CurrentPresetIndex => _nativePlugin.CurrentProgram;

    public string CurrentPresetName => _nativePlugin.GetProgramName(_nativePlugin.CurrentProgram);

    public bool LoadPreset(string path)
    {
        // Native plugin state management is done through GetState/SetState
        // For file-based presets, we'd need to implement FXP/FXB parsing
        // For now, return false to indicate not supported
        return false;
    }

    public bool SavePreset(string path)
    {
        // For file-based presets, we'd need to implement FXP/FXB writing
        return false;
    }

    #endregion

    #region Editor

    public IntPtr OpenEditor(IntPtr parentWindow)
    {
        if (!_nativePlugin.HasEditor)
            return IntPtr.Zero;

        _nativePlugin.OpenEditor(parentWindow);
        return parentWindow; // Native plugin uses the parent directly
    }

    public void CloseEditor()
    {
        _nativePlugin.CloseEditor();
    }

    public bool GetEditorSize(out int width, out int height)
    {
        if (!_nativePlugin.HasEditor)
        {
            width = 0;
            height = 0;
            return false;
        }

        var size = _nativePlugin.GetEditorSize();
        width = size.Width;
        height = size.Height;
        return width > 0 && height > 0;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _disposed = true;
            AllNotesOff();
            _nativePlugin.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    ~NativeVstPluginAdapter()
    {
        Dispose();
    }

    #endregion
}
