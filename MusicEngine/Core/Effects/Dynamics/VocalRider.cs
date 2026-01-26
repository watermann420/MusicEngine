//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Automatic gain riding for vocals with target level, attack/release, and sidechain frequency filtering.

using System;
using NAudio.Wave;

namespace MusicEngine.Core.Effects.Dynamics;

/// <summary>
/// Automatic gain rider for vocals that adjusts gain to maintain a consistent target level.
/// Features sidechain frequency filtering to focus on vocal range, ducking when music is louder,
/// and configurable attack/release times for smooth gain transitions.
/// </summary>
public class VocalRider : EffectBase
{
    private ISampleProvider? _musicSource;
    private float[] _musicBuffer = Array.Empty<float>();

    // Envelope followers
    private float[] _vocalEnvelope;
    private float[] _musicEnvelope;
    private float[] _currentGain;

    // Highpass/Lowpass filter states for vocal frequency focus
    private float[] _hpState1;
    private float[] _hpState2;
    private float[] _lpState1;
    private float[] _lpState2;

    // Filter coefficients
    private float _hpA1, _hpA2, _hpB0, _hpB1, _hpB2;
    private float _lpA1, _lpA2, _lpB0, _lpB1, _lpB2;

    /// <summary>
    /// Creates a new vocal rider effect.
    /// </summary>
    /// <param name="source">The vocal audio source to process.</param>
    /// <param name="name">Effect name.</param>
    public VocalRider(ISampleProvider source, string name = "Vocal Rider")
        : base(source, name)
    {
        int channels = Channels;
        _vocalEnvelope = new float[channels];
        _musicEnvelope = new float[channels];
        _currentGain = new float[channels];
        _hpState1 = new float[channels];
        _hpState2 = new float[channels];
        _lpState1 = new float[channels];
        _lpState2 = new float[channels];

        // Initialize gain to unity
        for (int i = 0; i < channels; i++)
        {
            _currentGain[i] = 1f;
        }

        // Register parameters with defaults
        RegisterParameter("TargetLevel", -12f);        // -12 dBFS target
        RegisterParameter("Attack", 0.01f);            // 10ms attack
        RegisterParameter("Release", 0.1f);            // 100ms release
        RegisterParameter("RangeMin", -12f);           // Minimum gain change in dB
        RegisterParameter("RangeMax", 12f);            // Maximum gain change in dB
        RegisterParameter("Sensitivity", 1f);          // Sensitivity multiplier
        RegisterParameter("VocalLowFreq", 200f);       // Vocal range low frequency
        RegisterParameter("VocalHighFreq", 4000f);     // Vocal range high frequency
        RegisterParameter("DuckAmount", 6f);           // Ducking amount in dB when music is louder
        RegisterParameter("DuckThreshold", -20f);      // Music level threshold for ducking

        UpdateFilters();
    }

    /// <summary>
    /// Target output level for vocals in dBFS (-18 to -6).
    /// </summary>
    public float TargetLevel
    {
        get => GetParameter("TargetLevel");
        set => SetParameter("TargetLevel", Math.Clamp(value, -18f, -6f));
    }

    /// <summary>
    /// Attack time in seconds (0.001 to 0.5).
    /// How fast the gain increases when vocal gets quieter.
    /// </summary>
    public float Attack
    {
        get => GetParameter("Attack");
        set => SetParameter("Attack", Math.Clamp(value, 0.001f, 0.5f));
    }

    /// <summary>
    /// Release time in seconds (0.01 to 2.0).
    /// How fast the gain decreases when vocal gets louder.
    /// </summary>
    public float Release
    {
        get => GetParameter("Release");
        set => SetParameter("Release", Math.Clamp(value, 0.01f, 2f));
    }

    /// <summary>
    /// Minimum gain change in dB (-24 to 0).
    /// Limits how much the signal can be attenuated.
    /// </summary>
    public float RangeMin
    {
        get => GetParameter("RangeMin");
        set => SetParameter("RangeMin", Math.Clamp(value, -24f, 0f));
    }

    /// <summary>
    /// Maximum gain change in dB (0 to 24).
    /// Limits how much the signal can be boosted.
    /// </summary>
    public float RangeMax
    {
        get => GetParameter("RangeMax");
        set => SetParameter("RangeMax", Math.Clamp(value, 0f, 24f));
    }

    /// <summary>
    /// Sensitivity multiplier (0.1 to 3.0).
    /// Higher values make the rider more responsive.
    /// </summary>
    public float Sensitivity
    {
        get => GetParameter("Sensitivity");
        set => SetParameter("Sensitivity", Math.Clamp(value, 0.1f, 3f));
    }

    /// <summary>
    /// Low frequency cutoff for vocal detection in Hz (80 to 500).
    /// </summary>
    public float VocalLowFreq
    {
        get => GetParameter("VocalLowFreq");
        set
        {
            SetParameter("VocalLowFreq", Math.Clamp(value, 80f, 500f));
            UpdateFilters();
        }
    }

    /// <summary>
    /// High frequency cutoff for vocal detection in Hz (2000 to 8000).
    /// </summary>
    public float VocalHighFreq
    {
        get => GetParameter("VocalHighFreq");
        set
        {
            SetParameter("VocalHighFreq", Math.Clamp(value, 2000f, 8000f));
            UpdateFilters();
        }
    }

    /// <summary>
    /// Amount to duck vocals when music is louder, in dB (0 to 12).
    /// </summary>
    public float DuckAmount
    {
        get => GetParameter("DuckAmount");
        set => SetParameter("DuckAmount", Math.Clamp(value, 0f, 12f));
    }

    /// <summary>
    /// Music level threshold for ducking in dBFS (-40 to 0).
    /// </summary>
    public float DuckThreshold
    {
        get => GetParameter("DuckThreshold");
        set => SetParameter("DuckThreshold", Math.Clamp(value, -40f, 0f));
    }

    /// <summary>
    /// Gets the current gain reduction/boost in dB for monitoring.
    /// </summary>
    public float CurrentGainDb
    {
        get
        {
            float avgGain = 0;
            for (int ch = 0; ch < Channels; ch++)
            {
                avgGain += _currentGain[ch];
            }
            avgGain /= Channels;
            return 20f * MathF.Log10(avgGain + 1e-6f);
        }
    }

    /// <summary>
    /// Sets the music/instrumental source for ducking.
    /// When music is louder than the threshold, vocals will be ducked.
    /// </summary>
    /// <param name="musicSource">The music audio source.</param>
    public void SetMusicSource(ISampleProvider musicSource)
    {
        if (musicSource.WaveFormat.SampleRate != SampleRate ||
            musicSource.WaveFormat.Channels != Channels)
        {
            throw new ArgumentException("Music source wave format must match vocal source");
        }
        _musicSource = musicSource;
    }

    /// <summary>
    /// Removes the music source (disables ducking).
    /// </summary>
    public void ClearMusicSource()
    {
        _musicSource = null;
    }

    private void UpdateFilters()
    {
        float lowFreq = GetParameter("VocalLowFreq");
        float highFreq = GetParameter("VocalHighFreq");

        // Highpass filter coefficients (Butterworth)
        float w0Hp = 2f * MathF.PI * lowFreq / SampleRate;
        float cosW0Hp = MathF.Cos(w0Hp);
        float sinW0Hp = MathF.Sin(w0Hp);
        float alphaHp = sinW0Hp / (2f * 0.7071f);

        float hpA0 = 1f + alphaHp;
        _hpB0 = ((1f + cosW0Hp) / 2f) / hpA0;
        _hpB1 = (-(1f + cosW0Hp)) / hpA0;
        _hpB2 = ((1f + cosW0Hp) / 2f) / hpA0;
        _hpA1 = (-2f * cosW0Hp) / hpA0;
        _hpA2 = (1f - alphaHp) / hpA0;

        // Lowpass filter coefficients (Butterworth)
        float w0Lp = 2f * MathF.PI * highFreq / SampleRate;
        float cosW0Lp = MathF.Cos(w0Lp);
        float sinW0Lp = MathF.Sin(w0Lp);
        float alphaLp = sinW0Lp / (2f * 0.7071f);

        float lpA0 = 1f + alphaLp;
        _lpB0 = ((1f - cosW0Lp) / 2f) / lpA0;
        _lpB1 = (1f - cosW0Lp) / lpA0;
        _lpB2 = ((1f - cosW0Lp) / 2f) / lpA0;
        _lpA1 = (-2f * cosW0Lp) / lpA0;
        _lpA2 = (1f - alphaLp) / lpA0;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        float targetLevel = TargetLevel;
        float attack = Attack;
        float release = Release;
        float rangeMin = RangeMin;
        float rangeMax = RangeMax;
        float sensitivity = Sensitivity;
        float duckAmount = DuckAmount;
        float duckThreshold = DuckThreshold;

        // Calculate time constants
        float attackCoeff = MathF.Exp(-1f / (attack * sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (release * sampleRate));

        // Convert target level to linear
        float targetLinear = MathF.Pow(10f, targetLevel / 20f);

        // Convert range to linear
        float rangeMinLinear = MathF.Pow(10f, rangeMin / 20f);
        float rangeMaxLinear = MathF.Pow(10f, rangeMax / 20f);

        // Read music source if available
        float[]? musicBuffer = null;
        if (_musicSource != null)
        {
            if (_musicBuffer.Length < count)
            {
                _musicBuffer = new float[count];
            }
            _musicSource.Read(_musicBuffer, 0, count);
            musicBuffer = _musicBuffer;
        }

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int idx = i + ch;
                float input = sourceBuffer[idx];

                // Apply bandpass filter for vocal detection (highpass then lowpass)
                float filtered = ApplyHighpass(input, ch);
                filtered = ApplyLowpass(filtered, ch);

                // Envelope follower on filtered signal
                float inputAbs = MathF.Abs(filtered) * sensitivity;
                float coeff = inputAbs > _vocalEnvelope[ch] ? attackCoeff : releaseCoeff;
                _vocalEnvelope[ch] = inputAbs + coeff * (_vocalEnvelope[ch] - inputAbs);

                // Calculate target gain to reach target level
                float vocalLevel = _vocalEnvelope[ch];
                float targetGain = 1f;

                if (vocalLevel > 1e-6f)
                {
                    targetGain = targetLinear / vocalLevel;
                }

                // Apply range limits
                targetGain = Math.Clamp(targetGain, rangeMinLinear, rangeMaxLinear);

                // Apply ducking if music source is present
                if (musicBuffer != null)
                {
                    float musicSample = musicBuffer[idx];
                    float musicAbs = MathF.Abs(musicSample);

                    // Music envelope follower
                    float musicCoeff = musicAbs > _musicEnvelope[ch] ? attackCoeff : releaseCoeff;
                    _musicEnvelope[ch] = musicAbs + musicCoeff * (_musicEnvelope[ch] - musicAbs);

                    // Calculate music level in dB
                    float musicDb = 20f * MathF.Log10(_musicEnvelope[ch] + 1e-6f);

                    // Apply ducking if music exceeds threshold
                    if (musicDb > duckThreshold)
                    {
                        float duckDb = (musicDb - duckThreshold) * (duckAmount / 20f);
                        float duckLinear = MathF.Pow(10f, -duckDb / 20f);
                        targetGain *= duckLinear;
                    }
                }

                // Smooth gain changes
                float gainCoeff = targetGain < _currentGain[ch] ? attackCoeff : releaseCoeff;
                _currentGain[ch] = targetGain + gainCoeff * (_currentGain[ch] - targetGain);

                // Apply gain to original (unfiltered) signal
                destBuffer[offset + idx] = input * _currentGain[ch];
            }
        }
    }

    private float ApplyHighpass(float input, int channel)
    {
        float output = _hpB0 * input + _hpState1[channel];
        _hpState1[channel] = _hpB1 * input - _hpA1 * output + _hpState2[channel];
        _hpState2[channel] = _hpB2 * input - _hpA2 * output;
        return output;
    }

    private float ApplyLowpass(float input, int channel)
    {
        float output = _lpB0 * input + _lpState1[channel];
        _lpState1[channel] = _lpB1 * input - _lpA1 * output + _lpState2[channel];
        _lpState2[channel] = _lpB2 * input - _lpA2 * output;
        return output;
    }

    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "vocallowfreq":
            case "vocalhighfreq":
                UpdateFilters();
                break;
        }
    }

    /// <summary>
    /// Resets the envelope followers and filter states.
    /// </summary>
    public void Reset()
    {
        for (int ch = 0; ch < Channels; ch++)
        {
            _vocalEnvelope[ch] = 0;
            _musicEnvelope[ch] = 0;
            _currentGain[ch] = 1f;
            _hpState1[ch] = 0;
            _hpState2[ch] = 0;
            _lpState1[ch] = 0;
            _lpState2[ch] = 0;
        }
    }

    /// <summary>
    /// Creates a preset for speech/podcast vocals.
    /// </summary>
    public static VocalRider CreateSpeechPreset(ISampleProvider source)
    {
        var rider = new VocalRider(source, "Speech Rider");
        rider.TargetLevel = -14f;
        rider.Attack = 0.02f;
        rider.Release = 0.15f;
        rider.RangeMin = -6f;
        rider.RangeMax = 9f;
        rider.Sensitivity = 0.8f;
        rider.VocalLowFreq = 150f;
        rider.VocalHighFreq = 3500f;
        return rider;
    }

    /// <summary>
    /// Creates a preset for singing vocals.
    /// </summary>
    public static VocalRider CreateSingingPreset(ISampleProvider source)
    {
        var rider = new VocalRider(source, "Singing Rider");
        rider.TargetLevel = -10f;
        rider.Attack = 0.015f;
        rider.Release = 0.2f;
        rider.RangeMin = -9f;
        rider.RangeMax = 12f;
        rider.Sensitivity = 1.2f;
        rider.VocalLowFreq = 200f;
        rider.VocalHighFreq = 5000f;
        return rider;
    }

    /// <summary>
    /// Creates a preset for vocals that need to duck under music.
    /// </summary>
    public static VocalRider CreateDuckingPreset(ISampleProvider vocalSource, ISampleProvider musicSource)
    {
        var rider = new VocalRider(vocalSource, "Ducking Rider");
        rider.TargetLevel = -12f;
        rider.Attack = 0.01f;
        rider.Release = 0.1f;
        rider.RangeMin = -12f;
        rider.RangeMax = 6f;
        rider.DuckAmount = 6f;
        rider.DuckThreshold = -18f;
        rider.SetMusicSource(musicSource);
        return rider;
    }
}
