//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Audio bus system for send/return routing and sub-mixing.


using System;
using System.Collections.Generic;
using NAudio.Wave;


namespace MusicEngine.Core;


/// <summary>
/// Represents an audio bus for routing and mixing audio signals.
/// Can be used as a sub-mix, send/return, or effects bus.
/// </summary>
public class AudioBus : ISampleProvider, IDisposable
{
    private readonly WaveFormat _waveFormat;
    private readonly List<AudioBusInput> _inputs = new();
    private readonly List<IEffect> _effects = new();
    private readonly object _lock = new();
    private float[] _mixBuffer;
    private bool _disposed;

    /// <summary>
    /// Bus name for identification
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets the audio format
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Gets or sets the bus volume (0-2, 1 = unity)
    /// </summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the pan position (-1 = left, 0 = center, 1 = right)
    /// </summary>
    public float Pan { get; set; } = 0f;

    /// <summary>
    /// Gets or sets whether the bus is muted
    /// </summary>
    public bool Muted { get; set; }

    /// <summary>
    /// Gets or sets whether the bus is soloed
    /// </summary>
    public bool Solo { get; set; }

    /// <summary>
    /// Gets the number of inputs
    /// </summary>
    public int InputCount
    {
        get
        {
            lock (_lock)
            {
                return _inputs.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of effects
    /// </summary>
    public int EffectCount
    {
        get
        {
            lock (_lock)
            {
                return _effects.Count;
            }
        }
    }

    /// <summary>
    /// Current peak level (for metering)
    /// </summary>
    public float PeakLevel { get; private set; }

    /// <summary>
    /// Creates a new audio bus
    /// </summary>
    public AudioBus(string name = "Bus", int? sampleRate = null)
    {
        Name = name;
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);
        _mixBuffer = new float[Settings.VstBufferSize * Settings.Channels];
    }

    /// <summary>
    /// Add an input source to the bus
    /// </summary>
    public void AddInput(ISampleProvider source, float level = 1.0f)
    {
        if (source.WaveFormat.SampleRate != _waveFormat.SampleRate ||
            source.WaveFormat.Channels != _waveFormat.Channels)
        {
            throw new ArgumentException("Source format must match bus format");
        }

        lock (_lock)
        {
            _inputs.Add(new AudioBusInput(source, level));
        }
    }

    /// <summary>
    /// Remove an input source
    /// </summary>
    public bool RemoveInput(ISampleProvider source)
    {
        lock (_lock)
        {
            var input = _inputs.Find(i => i.Source == source);
            if (input != null)
            {
                return _inputs.Remove(input);
            }
            return false;
        }
    }

    /// <summary>
    /// Set the send level for an input
    /// </summary>
    public void SetInputLevel(ISampleProvider source, float level)
    {
        lock (_lock)
        {
            var input = _inputs.Find(i => i.Source == source);
            if (input != null)
            {
                input.Level = level;
            }
        }
    }

    /// <summary>
    /// Add an effect to the bus
    /// </summary>
    public void AddEffect(IEffect effect)
    {
        lock (_lock)
        {
            _effects.Add(effect);
        }
    }

    /// <summary>
    /// Remove an effect from the bus
    /// </summary>
    public bool RemoveEffect(IEffect effect)
    {
        lock (_lock)
        {
            return _effects.Remove(effect);
        }
    }

    /// <summary>
    /// Clear all effects
    /// </summary>
    public void ClearEffects()
    {
        lock (_lock)
        {
            _effects.Clear();
        }
    }

    /// <summary>
    /// Read audio samples (mixes all inputs and applies effects)
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        if (_disposed) return 0;

        // Clear output buffer
        for (int i = 0; i < count; i++)
        {
            buffer[offset + i] = 0;
        }

        if (Muted && !Solo)
        {
            return count;
        }

        // Ensure mix buffer is large enough
        lock (_lock)
        {
            if (_mixBuffer.Length < count)
            {
                _mixBuffer = new float[count];
            }

            // Mix all inputs
            foreach (var input in _inputs)
            {
                if (input.Muted) continue;

                // Read from input
                Array.Clear(_mixBuffer, 0, count);
                int read = input.Source.Read(_mixBuffer, 0, count);

                // Apply input level and add to output
                for (int i = 0; i < read; i++)
                {
                    buffer[offset + i] += _mixBuffer[i] * input.Level;
                }
            }

            // Apply effects
            foreach (var effect in _effects)
            {
                if (effect.Enabled)
                {
                    // Copy buffer to mix buffer
                    Array.Copy(buffer, offset, _mixBuffer, 0, count);

                    // Process effect
                    effect.Read(_mixBuffer, 0, count);

                    // Copy back
                    Array.Copy(_mixBuffer, 0, buffer, offset, count);
                }
            }
        }

        // Apply volume and pan
        int channels = _waveFormat.Channels;
        float leftGain = Volume * (Pan <= 0 ? 1f : 1f - Pan);
        float rightGain = Volume * (Pan >= 0 ? 1f : 1f + Pan);

        float peak = 0;
        for (int i = 0; i < count; i += channels)
        {
            if (channels >= 1)
            {
                buffer[offset + i] *= leftGain;
                peak = Math.Max(peak, Math.Abs(buffer[offset + i]));
            }
            if (channels >= 2)
            {
                buffer[offset + i + 1] *= rightGain;
                peak = Math.Max(peak, Math.Abs(buffer[offset + i + 1]));
            }
        }

        PeakLevel = peak;

        return count;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            _inputs.Clear();
            _effects.Clear();
        }

        GC.SuppressFinalize(this);
    }

    ~AudioBus()
    {
        Dispose();
    }
}


/// <summary>
/// Represents an input to an audio bus
/// </summary>
internal class AudioBusInput
{
    public ISampleProvider Source { get; }
    public float Level { get; set; }
    public bool Muted { get; set; }

    public AudioBusInput(ISampleProvider source, float level = 1.0f)
    {
        Source = source;
        Level = level;
    }
}


/// <summary>
/// Audio send that routes audio to a bus without interrupting the original signal
/// </summary>
public class AudioSend : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly AudioBus _sendBus;
    private float[] _sendBuffer;

    /// <summary>
    /// Gets the audio format
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets or sets the send level (0-1)
    /// </summary>
    public float SendLevel { get; set; } = 0.5f;

    /// <summary>
    /// Gets or sets whether the send is pre-fader
    /// </summary>
    public bool PreFader { get; set; }

    /// <summary>
    /// Creates a new audio send
    /// </summary>
    public AudioSend(ISampleProvider source, AudioBus sendBus)
    {
        _source = source;
        _sendBus = sendBus;
        _sendBuffer = new float[Settings.VstBufferSize * Settings.Channels];

        // Add this as an input to the send bus
        _sendBus.AddInput(new SendSampleProvider(this), 1.0f);
    }

    /// <summary>
    /// Read audio samples
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        // Read from source
        int read = _source.Read(buffer, offset, count);

        // Copy to send buffer for the bus to read
        if (_sendBuffer.Length < count)
        {
            _sendBuffer = new float[count];
        }

        for (int i = 0; i < read; i++)
        {
            _sendBuffer[i] = buffer[offset + i] * SendLevel;
        }

        return read;
    }

    /// <summary>
    /// Internal sample provider for the send bus
    /// </summary>
    private class SendSampleProvider : ISampleProvider
    {
        private readonly AudioSend _send;

        public WaveFormat WaveFormat => _send.WaveFormat;

        public SendSampleProvider(AudioSend send)
        {
            _send = send;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            // Copy from send's buffer
            int toCopy = Math.Min(count, _send._sendBuffer.Length);
            Array.Copy(_send._sendBuffer, 0, buffer, offset, toCopy);
            return toCopy;
        }
    }
}


/// <summary>
/// Master bus that mixes multiple buses together
/// </summary>
public class MasterBus : ISampleProvider, IDisposable
{
    private readonly WaveFormat _waveFormat;
    private readonly List<AudioBus> _buses = new();
    private readonly object _lock = new();
    private float[] _mixBuffer;
    private bool _disposed;

    /// <summary>
    /// Gets the audio format
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Gets or sets the master volume
    /// </summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets limiter threshold (0 = off)
    /// </summary>
    public float LimiterThreshold { get; set; } = 0.95f;

    /// <summary>
    /// Current peak level (for metering)
    /// </summary>
    public float PeakLevel { get; private set; }

    /// <summary>
    /// Gets the bus list
    /// </summary>
    public IReadOnlyList<AudioBus> Buses
    {
        get
        {
            lock (_lock)
            {
                return _buses.ToArray();
            }
        }
    }

    /// <summary>
    /// Creates a new master bus
    /// </summary>
    public MasterBus(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);
        _mixBuffer = new float[Settings.VstBufferSize * Settings.Channels];
    }

    /// <summary>
    /// Add a bus to the master
    /// </summary>
    public void AddBus(AudioBus bus)
    {
        lock (_lock)
        {
            _buses.Add(bus);
        }
    }

    /// <summary>
    /// Remove a bus from the master
    /// </summary>
    public bool RemoveBus(AudioBus bus)
    {
        lock (_lock)
        {
            return _buses.Remove(bus);
        }
    }

    /// <summary>
    /// Create a new bus and add it to the master
    /// </summary>
    public AudioBus CreateBus(string name = "Bus")
    {
        var bus = new AudioBus(name, _waveFormat.SampleRate);
        AddBus(bus);
        return bus;
    }

    /// <summary>
    /// Read audio samples
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        if (_disposed) return 0;

        // Clear output
        for (int i = 0; i < count; i++)
        {
            buffer[offset + i] = 0;
        }

        // Ensure mix buffer is large enough
        if (_mixBuffer.Length < count)
        {
            _mixBuffer = new float[count];
        }

        // Check if any bus is soloed
        bool anySolo = false;
        lock (_lock)
        {
            foreach (var bus in _buses)
            {
                if (bus.Solo)
                {
                    anySolo = true;
                    break;
                }
            }

            // Mix all buses
            foreach (var bus in _buses)
            {
                // Skip if muted or if solo mode is active and this bus isn't soloed
                if (bus.Muted || (anySolo && !bus.Solo)) continue;

                // Read from bus
                Array.Clear(_mixBuffer, 0, count);
                bus.Read(_mixBuffer, 0, count);

                // Add to output
                for (int i = 0; i < count; i++)
                {
                    buffer[offset + i] += _mixBuffer[i];
                }
            }
        }

        // Apply master volume and limiter
        float peak = 0;
        for (int i = 0; i < count; i++)
        {
            float sample = buffer[offset + i] * Volume;

            // Simple limiter
            if (LimiterThreshold > 0)
            {
                if (sample > LimiterThreshold)
                    sample = LimiterThreshold + (sample - LimiterThreshold) * 0.1f;
                else if (sample < -LimiterThreshold)
                    sample = -LimiterThreshold + (sample + LimiterThreshold) * 0.1f;
            }

            buffer[offset + i] = sample;
            peak = Math.Max(peak, Math.Abs(sample));
        }

        PeakLevel = peak;

        return count;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            foreach (var bus in _buses)
            {
                bus.Dispose();
            }
            _buses.Clear();
        }

        GC.SuppressFinalize(this);
    }

    ~MasterBus()
    {
        Dispose();
    }
}


/// <summary>
/// Audio routing helper for creating complex signal paths
/// </summary>
public static class AudioRouting
{
    /// <summary>
    /// Create a standard mixing setup with a master bus and sub-buses
    /// </summary>
    public static (MasterBus master, AudioBus drumBus, AudioBus synthBus, AudioBus fxBus) CreateStandardSetup()
    {
        var master = new MasterBus();

        var drumBus = master.CreateBus("Drums");
        var synthBus = master.CreateBus("Synths");
        var fxBus = master.CreateBus("FX");

        return (master, drumBus, synthBus, fxBus);
    }

    /// <summary>
    /// Create a bus with standard effects
    /// </summary>
    public static AudioBus CreateEffectsBus(string name, ISampleProvider source, bool reverb = true, bool delay = true)
    {
        var bus = new AudioBus(name);
        bus.AddInput(source);

        if (delay)
        {
            var delayEffect = new DelayEffect(bus);
            delayEffect.DelayTime = 0.25f;
            delayEffect.Feedback = 0.3f;
            delayEffect.Mix = 0.5f;
            bus.AddEffect(delayEffect);
        }

        if (reverb)
        {
            var reverbEffect = new ReverbEffect(bus);
            reverbEffect.RoomSize = 0.7f;
            reverbEffect.Damping = 0.5f;
            reverbEffect.Mix = 0.4f;
            bus.AddEffect(reverbEffect);
        }

        return bus;
    }
}
