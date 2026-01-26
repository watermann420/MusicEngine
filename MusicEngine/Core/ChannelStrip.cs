// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// A complete channel strip that combines volume, pan, mute/solo, and an effect chain.
/// This represents a single mixer channel with full signal processing capabilities.
/// </summary>
public class ChannelStrip : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly EffectChain _effectChain;
    private readonly List<SendConfiguration> _sends = [];
    private readonly float[] _processBuffer;
    private readonly object _lock = new();

    private float _volume = 1.0f;
    private float _pan;
    private float _fader = 1.0f;
    private bool _mute;
    private bool _solo;
    private bool _bypassEffects;

    // Metering
    private float _peakLeft;
    private float _peakRight;
    private float _rmsLeft;
    private float _rmsRight;
    private int _rmsSampleCount;
    private float _rmsAccumulatorLeft;
    private float _rmsAccumulatorRight;

    /// <summary>
    /// Creates a new channel strip with the specified audio source.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <param name="name">The name of the channel.</param>
    public ChannelStrip(ISampleProvider source, string name)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        WaveFormat = source.WaveFormat;
        _effectChain = new EffectChain(source);
        _processBuffer = new float[WaveFormat.SampleRate * WaveFormat.Channels];
    }

    /// <inheritdoc />
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Gets the name of this channel strip.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the channel color for UI representation.
    /// </summary>
    public string Color { get; set; } = "#4A9EFF";

    /// <summary>
    /// Gets or sets the volume level (0.0 to 2.0, where 1.0 is unity gain).
    /// </summary>
    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 2f);
    }

    /// <summary>
    /// Gets or sets the pan position (-1.0 = full left, 0.0 = center, 1.0 = full right).
    /// </summary>
    public float Pan
    {
        get => _pan;
        set => _pan = Math.Clamp(value, -1f, 1f);
    }

    /// <summary>
    /// Gets or sets the fader level (used for automation, 0.0 to 1.5).
    /// </summary>
    public float Fader
    {
        get => _fader;
        set => _fader = Math.Clamp(value, 0f, 1.5f);
    }

    /// <summary>
    /// Gets or sets whether the channel is muted.
    /// </summary>
    public bool Mute
    {
        get => _mute;
        set => _mute = value;
    }

    /// <summary>
    /// Gets or sets whether the channel is soloed.
    /// </summary>
    public bool Solo
    {
        get => _solo;
        set => _solo = value;
    }

    /// <summary>
    /// Gets or sets whether the effect chain is bypassed.
    /// </summary>
    public bool BypassEffects
    {
        get => _bypassEffects;
        set
        {
            _bypassEffects = value;
            _effectChain.Bypassed = value;
        }
    }

    /// <summary>
    /// Gets the effect chain for this channel.
    /// </summary>
    public EffectChain Effects => _effectChain;

    /// <summary>
    /// Gets the current peak level for the left channel.
    /// </summary>
    public float PeakLeft => _peakLeft;

    /// <summary>
    /// Gets the current peak level for the right channel.
    /// </summary>
    public float PeakRight => _peakRight;

    /// <summary>
    /// Gets the RMS level for the left channel.
    /// </summary>
    public float RmsLeft => _rmsLeft;

    /// <summary>
    /// Gets the RMS level for the right channel.
    /// </summary>
    public float RmsRight => _rmsRight;

    /// <summary>
    /// Gets the list of send configurations for this channel.
    /// </summary>
    public IReadOnlyList<SendConfiguration> Sends
    {
        get
        {
            lock (_lock)
            {
                return _sends.AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Adds an effect to the channel's effect chain.
    /// </summary>
    /// <typeparam name="T">The type of effect to add.</typeparam>
    /// <returns>The created effect instance.</returns>
    public T AddEffect<T>() where T : class, IEffect
    {
        return _effectChain.AddEffect<T>();
    }

    /// <summary>
    /// Removes an effect from the chain by index.
    /// </summary>
    /// <param name="index">The index of the effect to remove.</param>
    /// <returns>True if the effect was removed.</returns>
    public bool RemoveEffect(int index)
    {
        return _effectChain.RemoveEffect(index);
    }

    /// <summary>
    /// Adds a send to a bus channel.
    /// </summary>
    /// <param name="targetBus">The target bus to send to.</param>
    /// <param name="level">The send level (0.0 to 1.0).</param>
    /// <param name="preFader">Whether this is a pre-fader send.</param>
    public void AddSend(BusChannel targetBus, float level, bool preFader = false)
    {
        ArgumentNullException.ThrowIfNull(targetBus);

        lock (_lock)
        {
            _sends.Add(new SendConfiguration
            {
                TargetBus = targetBus,
                Level = Math.Clamp(level, 0f, 1f),
                PreFader = preFader,
                Enabled = true
            });
        }
    }

    /// <summary>
    /// Removes a send to a specific bus.
    /// </summary>
    /// <param name="targetBus">The bus to remove the send for.</param>
    public void RemoveSend(BusChannel targetBus)
    {
        lock (_lock)
        {
            _sends.RemoveAll(s => s.TargetBus == targetBus);
        }
    }

    /// <summary>
    /// Sets the level of a send to a specific bus.
    /// </summary>
    /// <param name="targetBus">The target bus.</param>
    /// <param name="level">The new send level.</param>
    public void SetSendLevel(BusChannel targetBus, float level)
    {
        lock (_lock)
        {
            var send = _sends.Find(s => s.TargetBus == targetBus);
            if (send != null)
            {
                send.Level = Math.Clamp(level, 0f, 1f);
            }
        }
    }

    /// <summary>
    /// Resets the peak meters.
    /// </summary>
    public void ResetPeakMeters()
    {
        _peakLeft = 0f;
        _peakRight = 0f;
    }

    /// <inheritdoc />
    public int Read(float[] buffer, int offset, int count)
    {
        // Read from effect chain (which reads from source)
        int samplesRead = _effectChain.Read(buffer, offset, count);

        if (samplesRead == 0)
            return 0;

        // Calculate pre-fader levels for sends
        float preFaderGain = _volume;

        // Process sends
        lock (_lock)
        {
            foreach (var send in _sends)
            {
                if (!send.Enabled || send.TargetBus == null)
                    continue;

                float sendGain = send.PreFader ? preFaderGain : preFaderGain * _fader;
                sendGain *= send.Level;

                if (sendGain > 0)
                {
                    // Create a copy of the signal for the send
                    var sendBuffer = new float[samplesRead];
                    for (int i = 0; i < samplesRead; i++)
                    {
                        sendBuffer[i] = buffer[offset + i] * sendGain;
                    }
                    send.TargetBus.AcceptSendSignal(sendBuffer, samplesRead);
                }
            }
        }

        // Apply volume, fader, pan, and mute
        ApplyChannelProcessing(buffer, offset, samplesRead);

        // Update meters
        UpdateMeters(buffer, offset, samplesRead);

        return samplesRead;
    }

    private void ApplyChannelProcessing(float[] buffer, int offset, int count)
    {
        int channels = WaveFormat.Channels;
        float totalGain = _mute ? 0f : _volume * _fader;

        // Calculate pan gains using constant power panning
        float leftGain = totalGain;
        float rightGain = totalGain;

        if (channels == 2 && _pan != 0f)
        {
            float panAngle = (_pan + 1f) * MathF.PI * 0.25f;
            leftGain *= MathF.Cos(panAngle);
            rightGain *= MathF.Sin(panAngle);
        }

        // Apply gains
        for (int i = 0; i < count; i += channels)
        {
            if (channels == 1)
            {
                buffer[offset + i] *= leftGain;
            }
            else if (channels >= 2)
            {
                buffer[offset + i] *= leftGain;
                buffer[offset + i + 1] *= rightGain;
            }
        }
    }

    private void UpdateMeters(float[] buffer, int offset, int count)
    {
        int channels = WaveFormat.Channels;
        float maxLeft = 0f;
        float maxRight = 0f;

        for (int i = 0; i < count; i += channels)
        {
            float left = Math.Abs(buffer[offset + i]);
            maxLeft = Math.Max(maxLeft, left);
            _rmsAccumulatorLeft += left * left;

            if (channels >= 2)
            {
                float right = Math.Abs(buffer[offset + i + 1]);
                maxRight = Math.Max(maxRight, right);
                _rmsAccumulatorRight += right * right;
            }
        }

        _rmsSampleCount += count / channels;

        // Update peaks with decay
        _peakLeft = Math.Max(_peakLeft * 0.99f, maxLeft);
        _peakRight = Math.Max(_peakRight * 0.99f, maxRight);

        // Calculate RMS every ~100ms worth of samples
        int rmsWindowSamples = WaveFormat.SampleRate / 10;
        if (_rmsSampleCount >= rmsWindowSamples)
        {
            _rmsLeft = MathF.Sqrt(_rmsAccumulatorLeft / _rmsSampleCount);
            _rmsRight = MathF.Sqrt(_rmsAccumulatorRight / _rmsSampleCount);
            _rmsAccumulatorLeft = 0f;
            _rmsAccumulatorRight = 0f;
            _rmsSampleCount = 0;
        }
    }
}

/// <summary>
/// Configuration for a send from a channel to a bus.
/// </summary>
public class SendConfiguration
{
    /// <summary>
    /// Gets or sets the target bus for this send.
    /// </summary>
    public required BusChannel TargetBus { get; set; }

    /// <summary>
    /// Gets or sets the send level (0.0 to 1.0).
    /// </summary>
    public float Level { get; set; } = 0.5f;

    /// <summary>
    /// Gets or sets whether this is a pre-fader send.
    /// </summary>
    public bool PreFader { get; set; }

    /// <summary>
    /// Gets or sets whether this send is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
