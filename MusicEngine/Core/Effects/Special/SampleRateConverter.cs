// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Sample-based instrument.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Quality mode for sample rate conversion.
/// </summary>
public enum SampleRateConverterQuality
{
    /// <summary>Linear interpolation - fast but lower quality</summary>
    Fast,
    /// <summary>Cubic interpolation - balanced quality and performance</summary>
    Medium,
    /// <summary>Sinc interpolation with Kaiser window - highest quality</summary>
    High
}

/// <summary>
/// Sample rate converter effect with polyphase FIR filter and sinc interpolation.
/// Supports high-quality resampling for professional audio applications.
/// </summary>
public class SampleRateConverter : EffectBase
{
    private int _targetSampleRate = 44100;
    private SampleRateConverterQuality _quality = SampleRateConverterQuality.High;
    private bool _antiAlias = true;

    // Resampling state
    private double _resamplePosition;
    private readonly float[] _inputBuffer;
    private int _inputBufferLength;
    private const int InputBufferSize = 4096;

    // Sinc filter (for high quality)
    private readonly float[] _sincFilter;
    private const int SincFilterTaps = 64;
    private const double KaiserBeta = 6.0;

    // Anti-aliasing lowpass filter
    private readonly BiquadFilter[] _antiAliasFilters;

    /// <summary>
    /// Target sample rate for conversion (8000-192000 Hz).
    /// </summary>
    public int TargetSampleRate
    {
        get => _targetSampleRate;
        set
        {
            _targetSampleRate = Math.Clamp(value, 8000, 192000);
            UpdateFilters();
        }
    }

    /// <summary>
    /// Quality mode (0=Fast, 0.5=Medium, 1=High).
    /// </summary>
    public SampleRateConverterQuality Quality
    {
        get => _quality;
        set
        {
            _quality = value;
            UpdateFilters();
        }
    }

    /// <summary>
    /// Enable anti-aliasing filter when downsampling.
    /// </summary>
    public bool AntiAlias
    {
        get => _antiAlias;
        set
        {
            _antiAlias = value;
            UpdateFilters();
        }
    }

    /// <summary>
    /// Gets the resample ratio (target / source).
    /// </summary>
    public double ResampleRatio => (double)_targetSampleRate / SampleRate;

    /// <summary>
    /// Creates a new sample rate converter.
    /// </summary>
    /// <param name="source">The audio source to resample.</param>
    public SampleRateConverter(ISampleProvider source) : base(source, "SampleRateConverter")
    {
        _inputBuffer = new float[InputBufferSize * Channels];

        // Initialize sinc filter with Kaiser window
        _sincFilter = GenerateSincFilter(SincFilterTaps, KaiserBeta);

        // Initialize anti-aliasing filters (one per channel)
        _antiAliasFilters = new BiquadFilter[Channels];
        for (int ch = 0; ch < Channels; ch++)
        {
            _antiAliasFilters[ch] = new BiquadFilter();
        }

        UpdateFilters();

        RegisterParameter("TargetSampleRate", 44100);
        RegisterParameter("Quality", 1.0f);
        RegisterParameter("AntiAlias", 1.0f);
    }

    private void UpdateFilters()
    {
        // Configure anti-aliasing filter for downsampling
        if (_targetSampleRate < SampleRate && _antiAlias)
        {
            double cutoff = _targetSampleRate * 0.45 / SampleRate; // Nyquist * 0.9
            for (int ch = 0; ch < Channels; ch++)
            {
                _antiAliasFilters[ch].SetLowpass(cutoff, 0.707);
            }
        }
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        double ratio = ResampleRatio;
        bool isDownsampling = ratio < 1.0;

        // If no conversion needed, pass through
        if (Math.Abs(ratio - 1.0) < 0.0001)
        {
            Array.Copy(sourceBuffer, 0, destBuffer, offset, count);
            return;
        }

        // Apply anti-aliasing filter when downsampling
        float[] processBuffer = sourceBuffer;
        if (isDownsampling && _antiAlias)
        {
            processBuffer = new float[count];
            for (int i = 0; i < count; i += channels)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    processBuffer[i + ch] = _antiAliasFilters[ch].Process(sourceBuffer[i + ch]);
                }
            }
        }

        // Add to input buffer
        int framesToAdd = count / channels;
        int maxFramesToAdd = Math.Min(framesToAdd, (InputBufferSize - _inputBufferLength / channels));

        Array.Copy(processBuffer, 0, _inputBuffer, _inputBufferLength, maxFramesToAdd * channels);
        _inputBufferLength += maxFramesToAdd * channels;

        // Resample
        int outputFrames = count / channels;
        int outputWritten = 0;

        while (outputWritten < outputFrames && _resamplePosition < (_inputBufferLength / channels - SincFilterTaps))
        {
            int baseIndex = (int)_resamplePosition;
            double fraction = _resamplePosition - baseIndex;

            for (int ch = 0; ch < channels; ch++)
            {
                float sample = _quality switch
                {
                    SampleRateConverterQuality.Fast => InterpolateLinear(baseIndex, ch, fraction),
                    SampleRateConverterQuality.Medium => InterpolateCubic(baseIndex, ch, fraction),
                    SampleRateConverterQuality.High => InterpolateSinc(baseIndex, ch, fraction),
                    _ => InterpolateSinc(baseIndex, ch, fraction)
                };

                destBuffer[offset + outputWritten * channels + ch] = sample;
            }

            _resamplePosition += 1.0 / ratio;
            outputWritten++;
        }

        // Fill remaining with silence if needed
        for (int i = outputWritten * channels; i < count; i++)
        {
            destBuffer[offset + i] = 0f;
        }

        // Shift input buffer
        int consumedFrames = (int)_resamplePosition;
        if (consumedFrames > 0 && consumedFrames * channels < _inputBufferLength)
        {
            int remainingSamples = _inputBufferLength - consumedFrames * channels;
            Array.Copy(_inputBuffer, consumedFrames * channels, _inputBuffer, 0, remainingSamples);
            _inputBufferLength = remainingSamples;
            _resamplePosition -= consumedFrames;
        }
    }

    private float InterpolateLinear(int baseIndex, int channel, double fraction)
    {
        int idx0 = baseIndex * Channels + channel;
        int idx1 = Math.Min(idx0 + Channels, _inputBufferLength - 1);

        if (idx0 < 0 || idx0 >= _inputBufferLength) return 0f;
        if (idx1 >= _inputBufferLength) idx1 = idx0;

        return (float)(_inputBuffer[idx0] * (1 - fraction) + _inputBuffer[idx1] * fraction);
    }

    private float InterpolateCubic(int baseIndex, int channel, double fraction)
    {
        int idx0 = (baseIndex - 1) * Channels + channel;
        int idx1 = baseIndex * Channels + channel;
        int idx2 = (baseIndex + 1) * Channels + channel;
        int idx3 = (baseIndex + 2) * Channels + channel;

        // Clamp indices
        idx0 = Math.Clamp(idx0, 0, _inputBufferLength - 1);
        idx1 = Math.Clamp(idx1, 0, _inputBufferLength - 1);
        idx2 = Math.Clamp(idx2, 0, _inputBufferLength - 1);
        idx3 = Math.Clamp(idx3, 0, _inputBufferLength - 1);

        float y0 = _inputBuffer[idx0];
        float y1 = _inputBuffer[idx1];
        float y2 = _inputBuffer[idx2];
        float y3 = _inputBuffer[idx3];

        double t = fraction;
        double t2 = t * t;
        double t3 = t2 * t;

        // Catmull-Rom spline
        double a0 = -0.5 * y0 + 1.5 * y1 - 1.5 * y2 + 0.5 * y3;
        double a1 = y0 - 2.5 * y1 + 2 * y2 - 0.5 * y3;
        double a2 = -0.5 * y0 + 0.5 * y2;
        double a3 = y1;

        return (float)(a0 * t3 + a1 * t2 + a2 * t + a3);
    }

    private float InterpolateSinc(int baseIndex, int channel, double fraction)
    {
        double sum = 0;
        int halfTaps = SincFilterTaps / 2;

        for (int i = -halfTaps; i < halfTaps; i++)
        {
            int sampleIdx = (baseIndex + i) * Channels + channel;
            if (sampleIdx < 0 || sampleIdx >= _inputBufferLength) continue;

            double sincArg = i - fraction;
            double sincValue = Math.Abs(sincArg) < 0.0001 ? 1.0 : Math.Sin(Math.PI * sincArg) / (Math.PI * sincArg);

            // Apply Kaiser window
            int windowIdx = i + halfTaps;
            if (windowIdx >= 0 && windowIdx < _sincFilter.Length)
            {
                sincValue *= _sincFilter[windowIdx];
            }

            sum += _inputBuffer[sampleIdx] * sincValue;
        }

        return (float)sum;
    }

    private static float[] GenerateSincFilter(int taps, double beta)
    {
        float[] filter = new float[taps];
        int halfTaps = taps / 2;

        for (int i = 0; i < taps; i++)
        {
            // Kaiser window
            double alpha = (i - halfTaps) / (double)halfTaps;
            filter[i] = (float)BesselI0(beta * Math.Sqrt(1 - alpha * alpha)) / (float)BesselI0(beta);
        }

        return filter;
    }

    private static double BesselI0(double x)
    {
        // Approximation of modified Bessel function I0
        double sum = 1.0;
        double term = 1.0;

        for (int k = 1; k <= 20; k++)
        {
            term *= (x / 2) * (x / 2) / (k * k);
            sum += term;
        }

        return sum;
    }

    /// <summary>
    /// Resets the converter state.
    /// </summary>
    public void Reset()
    {
        _resamplePosition = 0;
        _inputBufferLength = 0;
        Array.Clear(_inputBuffer);

        foreach (var filter in _antiAliasFilters)
        {
            filter.Reset();
        }
    }

    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "targetsamplerate":
                TargetSampleRate = (int)value;
                break;
            case "quality":
                Quality = value < 0.33f ? SampleRateConverterQuality.Fast :
                         value < 0.66f ? SampleRateConverterQuality.Medium :
                         SampleRateConverterQuality.High;
                break;
            case "antialias":
                AntiAlias = value > 0.5f;
                break;
        }
    }

    #region Presets

    /// <summary>CD quality (44100 Hz)</summary>
    public static SampleRateConverter CreateCD(ISampleProvider source)
    {
        return new SampleRateConverter(source)
        {
            TargetSampleRate = 44100,
            Quality = SampleRateConverterQuality.High,
            AntiAlias = true
        };
    }

    /// <summary>DVD quality (48000 Hz)</summary>
    public static SampleRateConverter CreateDVD(ISampleProvider source)
    {
        return new SampleRateConverter(source)
        {
            TargetSampleRate = 48000,
            Quality = SampleRateConverterQuality.High,
            AntiAlias = true
        };
    }

    /// <summary>Hi-Res Audio (96000 Hz)</summary>
    public static SampleRateConverter CreateHiRes(ISampleProvider source)
    {
        return new SampleRateConverter(source)
        {
            TargetSampleRate = 96000,
            Quality = SampleRateConverterQuality.High,
            AntiAlias = true
        };
    }

    /// <summary>Downsample for preview (22050 Hz)</summary>
    public static SampleRateConverter CreateDownsample(ISampleProvider source)
    {
        return new SampleRateConverter(source)
        {
            TargetSampleRate = 22050,
            Quality = SampleRateConverterQuality.Medium,
            AntiAlias = true
        };
    }

    #endregion

    /// <summary>
    /// Simple biquad filter for anti-aliasing.
    /// </summary>
    private class BiquadFilter
    {
        private double _b0, _b1, _b2, _a1, _a2;
        private double _x1, _x2, _y1, _y2;

        public void SetLowpass(double normalizedFreq, double q)
        {
            double omega = 2 * Math.PI * normalizedFreq;
            double sinOmega = Math.Sin(omega);
            double cosOmega = Math.Cos(omega);
            double alpha = sinOmega / (2 * q);

            double a0 = 1 + alpha;
            _b0 = ((1 - cosOmega) / 2) / a0;
            _b1 = (1 - cosOmega) / a0;
            _b2 = ((1 - cosOmega) / 2) / a0;
            _a1 = (-2 * cosOmega) / a0;
            _a2 = (1 - alpha) / a0;
        }

        public float Process(float sample)
        {
            double output = _b0 * sample + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;

            _x2 = _x1;
            _x1 = sample;
            _y2 = _y1;
            _y1 = output;

            return (float)output;
        }

        public void Reset()
        {
            _x1 = _x2 = _y1 = _y2 = 0;
        }
    }
}
