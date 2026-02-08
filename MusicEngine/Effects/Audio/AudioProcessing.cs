// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Simple audio processing helpers.

using System.Collections.Generic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicEngine.Effects.Audio;

internal sealed class DcBlockingSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _alpha;
    private float _prevX_L, _prevY_L;
    private float _prevX_R, _prevY_R;

    public DcBlockingSampleProvider(ISampleProvider source, float cutoffHz, int sampleRate)
    {
        _source = source;
        WaveFormat = source.WaveFormat;
        var rc = 1f / (2f * (float)System.Math.PI * cutoffHz);
        var dt = 1f / sampleRate;
        _alpha = rc / (rc + dt);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        for (int n = 0; n < read; n += WaveFormat.Channels)
        {
            int i = offset + n;
            float xL = buffer[i];
            float yL = xL - _prevX_L + _alpha * _prevY_L;
            _prevX_L = xL;
            _prevY_L = yL;
            buffer[i] = yL;

            if (WaveFormat.Channels > 1)
            {
                float xR = buffer[i + 1];
                float yR = xR - _prevX_R + _alpha * _prevY_R;
                _prevX_R = xR;
                _prevY_R = yR;
                buffer[i + 1] = yR;
            }
        }
        return read;
    }
}

internal sealed class SoftClipSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _threshold;

    public SoftClipSampleProvider(ISampleProvider source, float threshold)
    {
        _source = source;
        _threshold = System.Math.Clamp(threshold, 0.5f, 0.999f);
        WaveFormat = source.WaveFormat;
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        for (int i = offset; i < offset + read; i++)
        {
            float x = buffer[i];
            float abs = System.Math.Abs(x);
            if (abs <= _threshold) continue;
            float sign = System.Math.Sign(x);
            float t = (abs - _threshold) / (1f - _threshold);
            buffer[i] = sign * (_threshold + (float)System.Math.Tanh(t) * (1f - _threshold));
        }
        return read;
    }
}

internal sealed class LimiterSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _threshold;
    private readonly float _attackCoeff;
    private readonly float _releaseCoeff;
    private float _gain = 1f;

    public LimiterSampleProvider(ISampleProvider source, float threshold, int sampleRate, float attackMs = 2f, float releaseMs = 50f)
    {
        _source = source;
        _threshold = System.Math.Clamp(threshold, 0.5f, 0.999f);
        WaveFormat = source.WaveFormat;
        _attackCoeff = (float)System.Math.Exp(-1.0 / (attackMs * 0.001f * sampleRate));
        _releaseCoeff = (float)System.Math.Exp(-1.0 / (releaseMs * 0.001f * sampleRate));
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        for (int i = offset; i < offset + read; i++)
        {
            float x = buffer[i];
            float abs = System.Math.Abs(x);
            float targetGain = abs > _threshold ? _threshold / abs : 1f;

            if (targetGain < _gain)
            {
                _gain = targetGain + _attackCoeff * (_gain - targetGain);
            }
            else
            {
                _gain = targetGain + _releaseCoeff * (_gain - targetGain);
            }

            buffer[i] = x * _gain;
        }
        return read;
    }
}

internal sealed class AudioEffectChain : ISampleProvider
{
    private readonly ISampleProvider _input;
    private readonly WaveFormat _targetFormat;
    private readonly List<IAudioEffect> _effects = new();
    private readonly object _lock = new();
    private ISampleProvider _current;

    public AudioEffectChain(ISampleProvider input, WaveFormat targetFormat)
    {
        _input = input;
        _targetFormat = targetFormat;
        _current = AudioFormatAdapter.EnsureFormat(input, _targetFormat.SampleRate, _targetFormat.Channels);
    }

    public WaveFormat WaveFormat => _targetFormat;

    public void AddEffect(IAudioEffect effect)
    {
        if (effect == null) return;
        lock (_lock)
        {
            _effects.Add(effect);
            Rebuild();
        }
    }

    public void ClearEffects()
    {
        lock (_lock)
        {
            foreach (var effect in _effects)
            {
                effect.Detach();
            }
            _effects.Clear();
            Rebuild();
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var current = _current;
        return current.Read(buffer, offset, count);
    }

    private void Rebuild()
    {
        foreach (var effect in _effects)
        {
            effect.Detach();
        }

        ISampleProvider current = _input;
        foreach (var effect in _effects)
        {
            current = effect.Attach(current, _targetFormat);
            current = AudioFormatAdapter.EnsureFormat(current, _targetFormat.SampleRate, _targetFormat.Channels);
        }
        _current = current;
    }
}

internal static class AudioFormatAdapter
{
    public static ISampleProvider EnsureFormat(ISampleProvider source, int sampleRate, int channels)
    {
        var current = source;
        if (current.WaveFormat.SampleRate != sampleRate)
        {
            current = new WdlResamplingSampleProvider(current, sampleRate);
        }

        if (current.WaveFormat.Channels != channels)
        {
            current = current.WaveFormat.Channels == 1 && channels == 2
                ? new MonoToStereoSampleProvider(current)
                : new StereoToMonoSampleProvider(current);
        }

        return current;
    }
}
