// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Vocoder effect processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Carrier signal type for the vocoder.
/// </summary>
public enum VocoderCarrierType
{
    /// <summary>
    /// Internal sawtooth wave carrier.
    /// </summary>
    Sawtooth,

    /// <summary>
    /// Internal square wave carrier.
    /// </summary>
    Square,

    /// <summary>
    /// Internal noise carrier for whispered effect.
    /// </summary>
    Noise,

    /// <summary>
    /// Mixed saw + noise for more natural sound.
    /// </summary>
    Mixed,

    /// <summary>
    /// External carrier signal provided via sidechain.
    /// </summary>
    External
}

/// <summary>
/// Number of frequency bands in the vocoder.
/// </summary>
public enum VocoderBandCount
{
    /// <summary>
    /// 8 bands - minimal CPU, robotic sound.
    /// </summary>
    Bands8 = 8,

    /// <summary>
    /// 16 bands - balanced quality and performance.
    /// </summary>
    Bands16 = 16,

    /// <summary>
    /// 32 bands - high quality, more natural.
    /// </summary>
    Bands32 = 32,

    /// <summary>
    /// 64 bands - maximum quality, highest CPU.
    /// </summary>
    Bands64 = 64
}

/// <summary>
/// Enhanced vocoder effect with formant shifting, multiple carrier options,
/// per-band attack/release, and sibilance preservation.
/// </summary>
/// <remarks>
/// Features:
/// - Configurable band count (8, 16, 32, 64)
/// - Multiple carrier types (saw, square, noise, mixed, external)
/// - Formant shifting (+/- 12 semitones)
/// - Per-band attack/release envelope followers
/// - Sibilance preservation for intelligible speech
/// - Sidechain input for external carrier
/// </remarks>
public class EnhancedVocoder : EffectBase
{
    // Band processing
    private EnhancedVocoderBand[] _bands = null!;
    private int _bandCount;
    private bool _initialized;

    // Internal carrier state
    private double _carrierPhase;
    private readonly Random _random = new();
    private float _lastNoiseSample;

    // Sidechain (external carrier)
    private float[] _sidechainBuffer = Array.Empty<float>();
    private int _sidechainReadIndex;
    private bool _hasSidechain;

    // Sibilance detection (high-frequency energy tracking)
    private float _sibilanceEnvelope;

    /// <summary>
    /// Creates a new enhanced vocoder effect.
    /// </summary>
    /// <param name="source">Modulator audio source (typically voice).</param>
    public EnhancedVocoder(ISampleProvider source) : this(source, "Enhanced Vocoder")
    {
    }

    /// <summary>
    /// Creates a new enhanced vocoder effect with a custom name.
    /// </summary>
    /// <param name="source">Modulator audio source (typically voice).</param>
    /// <param name="name">Effect name.</param>
    public EnhancedVocoder(ISampleProvider source, string name) : base(source, name)
    {
        RegisterParameter("CarrierFrequency", 110f);
        RegisterParameter("Attack", 0.01f);
        RegisterParameter("Release", 0.1f);
        RegisterParameter("FormantShift", 0f);
        RegisterParameter("Sibilance", 0.5f);
        RegisterParameter("OutputGain", 1f);
        RegisterParameter("Mix", 1f);

        CarrierType = VocoderCarrierType.Sawtooth;
        BandCount = VocoderBandCount.Bands16;
        _initialized = false;
    }

    /// <summary>
    /// Gets or sets the carrier signal type.
    /// </summary>
    public VocoderCarrierType CarrierType { get; set; }

    /// <summary>
    /// Gets or sets the number of frequency bands.
    /// </summary>
    public VocoderBandCount BandCount
    {
        get => (VocoderBandCount)_bandCount;
        set
        {
            int newCount = (int)value;
            if (_bandCount != newCount)
            {
                _bandCount = newCount;
                _initialized = false;
            }
        }
    }

    /// <summary>
    /// Gets or sets the carrier frequency in Hz (for internal carriers).
    /// </summary>
    public float CarrierFrequency
    {
        get => GetParameter("CarrierFrequency");
        set => SetParameter("CarrierFrequency", Math.Clamp(value, 20f, 2000f));
    }

    /// <summary>
    /// Gets or sets the envelope attack time in seconds.
    /// </summary>
    public float Attack
    {
        get => GetParameter("Attack");
        set => SetParameter("Attack", Math.Clamp(value, 0.001f, 1f));
    }

    /// <summary>
    /// Gets or sets the envelope release time in seconds.
    /// </summary>
    public float Release
    {
        get => GetParameter("Release");
        set => SetParameter("Release", Math.Clamp(value, 0.001f, 2f));
    }

    /// <summary>
    /// Gets or sets the formant shift in semitones (-12 to +12).
    /// </summary>
    public float FormantShift
    {
        get => GetParameter("FormantShift");
        set => SetParameter("FormantShift", Math.Clamp(value, -12f, 12f));
    }

    /// <summary>
    /// Gets or sets the sibilance preservation amount (0.0 - 1.0).
    /// </summary>
    public float Sibilance
    {
        get => GetParameter("Sibilance");
        set => SetParameter("Sibilance", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the output gain (0.0 - 2.0).
    /// </summary>
    public float OutputGain
    {
        get => GetParameter("OutputGain");
        set => SetParameter("OutputGain", Math.Clamp(value, 0f, 2f));
    }

    /// <summary>
    /// Sets the external carrier signal buffer.
    /// </summary>
    /// <param name="carrierSamples">Array of carrier samples.</param>
    public void SetExternalCarrier(float[] carrierSamples)
    {
        if (carrierSamples == null || carrierSamples.Length == 0)
        {
            _hasSidechain = false;
            return;
        }

        _sidechainBuffer = (float[])carrierSamples.Clone();
        _sidechainReadIndex = 0;
        _hasSidechain = true;
        CarrierType = VocoderCarrierType.External;
    }

    /// <summary>
    /// Provides real-time sidechain input for external carrier.
    /// Call this from your audio callback with carrier samples.
    /// </summary>
    /// <param name="samples">Carrier samples to feed.</param>
    /// <param name="offset">Start offset in array.</param>
    /// <param name="count">Number of samples.</param>
    public void FeedSidechain(float[] samples, int offset, int count)
    {
        if (samples == null || count <= 0)
            return;

        // Resize buffer if needed
        if (_sidechainBuffer.Length < count)
        {
            _sidechainBuffer = new float[count * 2];
        }

        Array.Copy(samples, offset, _sidechainBuffer, 0, count);
        _sidechainReadIndex = 0;
        _hasSidechain = true;
    }

    /// <summary>
    /// Initializes the vocoder bands.
    /// </summary>
    private void Initialize()
    {
        int sampleRate = SampleRate;
        _bands = new EnhancedVocoderBand[_bandCount];

        // Logarithmically spaced bands from 80Hz to 12kHz
        float minFreq = 80f;
        float maxFreq = 12000f;
        float freqRatio = MathF.Pow(maxFreq / minFreq, 1f / (_bandCount - 1));

        for (int i = 0; i < _bandCount; i++)
        {
            float centerFreq = minFreq * MathF.Pow(freqRatio, i);
            float bandwidth = centerFreq * 0.5f; // Q approximately 2

            _bands[i] = new EnhancedVocoderBand
            {
                CenterFrequency = centerFreq,
                Bandwidth = bandwidth,
                Envelope = 0f
            };

            _bands[i].AnalysisFilter.SetBandpass(centerFreq, bandwidth, sampleRate);
            _bands[i].SynthesisFilter.SetBandpass(centerFreq, bandwidth, sampleRate);
        }

        _initialized = true;
    }

    /// <inheritdoc/>
    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        int sampleRate = SampleRate;
        int channels = Channels;

        float attack = Attack;
        float release = Release;
        float attackCoeff = MathF.Exp(-1f / (attack * sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (release * sampleRate));

        float formantShift = FormantShift;
        int formantShiftBands = (int)MathF.Round(formantShift * _bandCount / 24f);

        float sibilanceAmount = Sibilance;
        float outputGain = OutputGain;
        float mix = Mix;

        float carrierFreq = CarrierFrequency;
        float phaseInc = 2f * MathF.PI * carrierFreq / sampleRate;

        for (int n = 0; n < count; n += channels)
        {
            // Get modulator signal (mono from input)
            float modulatorL = sourceBuffer[n];
            float modulatorR = channels > 1 ? sourceBuffer[n + 1] : modulatorL;
            float modulator = (modulatorL + modulatorR) * 0.5f;

            // Generate or fetch carrier signal
            float carrier = GetCarrierSample(phaseInc);

            // Process vocoder bands
            float outputL = 0f;
            float outputR = 0f;

            for (int b = 0; b < _bandCount; b++)
            {
                var band = _bands[b];

                // Analyze modulator - extract band energy
                float bandSignal = band.AnalysisFilter.Process(modulator);
                float bandEnergy = MathF.Abs(bandSignal);

                // Envelope follower with per-band attack/release
                if (bandEnergy > band.Envelope)
                {
                    band.Envelope = bandEnergy + attackCoeff * (band.Envelope - bandEnergy);
                }
                else
                {
                    band.Envelope = bandEnergy + releaseCoeff * (band.Envelope - bandEnergy);
                }

                // Apply formant shift
                int targetBand = b + formantShiftBands;
                if (targetBand < 0 || targetBand >= _bandCount)
                    continue;

                // Filter carrier through synthesis filter at target frequency
                float synthOutput = _bands[targetBand].SynthesisFilter.Process(carrier);

                // Apply envelope to filtered carrier
                float vocodedSignal = synthOutput * band.Envelope;

                outputL += vocodedSignal;
                outputR += vocodedSignal;
            }

            // Sibilance preservation
            if (sibilanceAmount > 0f)
            {
                // Track high-frequency energy (last 1/4 of bands)
                float sibilanceSignal = 0f;
                int sibilanceStartBand = _bandCount * 3 / 4;
                for (int b = sibilanceStartBand; b < _bandCount; b++)
                {
                    sibilanceSignal += _bands[b].AnalysisFilter.Process(modulator);
                }
                sibilanceSignal *= 4f / (_bandCount - sibilanceStartBand);

                // Envelope follow the sibilance
                float sibilanceEnergy = MathF.Abs(sibilanceSignal);
                if (sibilanceEnergy > _sibilanceEnvelope)
                {
                    _sibilanceEnvelope = sibilanceEnergy + 0.99f * (_sibilanceEnvelope - sibilanceEnergy);
                }
                else
                {
                    _sibilanceEnvelope = sibilanceEnergy + 0.9995f * (_sibilanceEnvelope - sibilanceEnergy);
                }

                // Add noise-modulated sibilance
                float sibilanceOutput = GetNoiseSample() * _sibilanceEnvelope * sibilanceAmount * 0.5f;
                outputL += sibilanceOutput;
                outputR += sibilanceOutput;
            }

            // Apply output gain
            outputL *= outputGain;
            outputR *= outputGain;

            // Mix with dry signal
            destBuffer[offset + n] = modulatorL * (1f - mix) + outputL * mix;
            if (channels > 1)
            {
                destBuffer[offset + n + 1] = modulatorR * (1f - mix) + outputR * mix;
            }
        }
    }

    /// <summary>
    /// Gets the next carrier sample based on carrier type.
    /// </summary>
    private float GetCarrierSample(float phaseInc)
    {
        float sample;

        switch (CarrierType)
        {
            case VocoderCarrierType.Sawtooth:
                sample = (float)(_carrierPhase / Math.PI - 1.0);
                _carrierPhase += phaseInc;
                if (_carrierPhase >= 2.0 * Math.PI) _carrierPhase -= 2.0 * Math.PI;
                break;

            case VocoderCarrierType.Square:
                sample = _carrierPhase < Math.PI ? 1f : -1f;
                _carrierPhase += phaseInc;
                if (_carrierPhase >= 2.0 * Math.PI) _carrierPhase -= 2.0 * Math.PI;
                break;

            case VocoderCarrierType.Noise:
                sample = GetNoiseSample();
                break;

            case VocoderCarrierType.Mixed:
                float saw = (float)(_carrierPhase / Math.PI - 1.0);
                float noise = GetNoiseSample();
                sample = saw * 0.7f + noise * 0.3f;
                _carrierPhase += phaseInc;
                if (_carrierPhase >= 2.0 * Math.PI) _carrierPhase -= 2.0 * Math.PI;
                break;

            case VocoderCarrierType.External:
                if (_hasSidechain && _sidechainBuffer.Length > 0)
                {
                    sample = _sidechainBuffer[_sidechainReadIndex];
                    _sidechainReadIndex = (_sidechainReadIndex + 1) % _sidechainBuffer.Length;
                }
                else
                {
                    // Fallback to sawtooth if no sidechain
                    sample = (float)(_carrierPhase / Math.PI - 1.0);
                    _carrierPhase += phaseInc;
                    if (_carrierPhase >= 2.0 * Math.PI) _carrierPhase -= 2.0 * Math.PI;
                }
                break;

            default:
                sample = 0f;
                break;
        }

        return sample;
    }

    /// <summary>
    /// Gets a noise sample with simple lowpass smoothing.
    /// </summary>
    private float GetNoiseSample()
    {
        float noise = (float)(_random.NextDouble() * 2.0 - 1.0);
        _lastNoiseSample = _lastNoiseSample * 0.3f + noise * 0.7f;
        return _lastNoiseSample;
    }

    /// <summary>
    /// Creates a classic robot voice preset.
    /// </summary>
    public static EnhancedVocoder CreateRobotVoice(ISampleProvider source)
    {
        var vocoder = new EnhancedVocoder(source, "Robot Voice");
        vocoder.BandCount = VocoderBandCount.Bands16;
        vocoder.CarrierType = VocoderCarrierType.Sawtooth;
        vocoder.CarrierFrequency = 110f;
        vocoder.Attack = 0.005f;
        vocoder.Release = 0.05f;
        vocoder.FormantShift = 0f;
        vocoder.Sibilance = 0.2f;
        vocoder.OutputGain = 1.2f;
        vocoder.Mix = 1f;
        return vocoder;
    }

    /// <summary>
    /// Creates a whispered voice preset.
    /// </summary>
    public static EnhancedVocoder CreateWhisper(ISampleProvider source)
    {
        var vocoder = new EnhancedVocoder(source, "Whisper");
        vocoder.BandCount = VocoderBandCount.Bands32;
        vocoder.CarrierType = VocoderCarrierType.Noise;
        vocoder.Attack = 0.015f;
        vocoder.Release = 0.15f;
        vocoder.FormantShift = 0f;
        vocoder.Sibilance = 0.8f;
        vocoder.OutputGain = 0.8f;
        vocoder.Mix = 1f;
        return vocoder;
    }

    /// <summary>
    /// Creates a choir-like preset.
    /// </summary>
    public static EnhancedVocoder CreateChoir(ISampleProvider source)
    {
        var vocoder = new EnhancedVocoder(source, "Choir");
        vocoder.BandCount = VocoderBandCount.Bands32;
        vocoder.CarrierType = VocoderCarrierType.Mixed;
        vocoder.CarrierFrequency = 220f;
        vocoder.Attack = 0.03f;
        vocoder.Release = 0.2f;
        vocoder.FormantShift = 0f;
        vocoder.Sibilance = 0.4f;
        vocoder.OutputGain = 1f;
        vocoder.Mix = 0.8f;
        return vocoder;
    }

    /// <summary>
    /// Creates an alien voice preset with formant shift.
    /// </summary>
    public static EnhancedVocoder CreateAlienVoice(ISampleProvider source)
    {
        var vocoder = new EnhancedVocoder(source, "Alien Voice");
        vocoder.BandCount = VocoderBandCount.Bands8;
        vocoder.CarrierType = VocoderCarrierType.Square;
        vocoder.CarrierFrequency = 80f;
        vocoder.Attack = 0.002f;
        vocoder.Release = 0.03f;
        vocoder.FormantShift = 5f;
        vocoder.Sibilance = 0.15f;
        vocoder.OutputGain = 1.3f;
        vocoder.Mix = 1f;
        return vocoder;
    }

    /// <summary>
    /// Creates a high-quality talk box preset.
    /// </summary>
    public static EnhancedVocoder CreateTalkBox(ISampleProvider source)
    {
        var vocoder = new EnhancedVocoder(source, "Talk Box");
        vocoder.BandCount = VocoderBandCount.Bands64;
        vocoder.CarrierType = VocoderCarrierType.Sawtooth;
        vocoder.CarrierFrequency = 130f;
        vocoder.Attack = 0.008f;
        vocoder.Release = 0.08f;
        vocoder.FormantShift = 0f;
        vocoder.Sibilance = 0.6f;
        vocoder.OutputGain = 1f;
        vocoder.Mix = 1f;
        return vocoder;
    }

    /// <summary>
    /// Internal band state for the enhanced vocoder.
    /// </summary>
    private class EnhancedVocoderBand
    {
        public float CenterFrequency { get; set; }
        public float Bandwidth { get; set; }
        public float Envelope { get; set; }
        public EnhancedStateVariableFilter AnalysisFilter { get; } = new();
        public EnhancedStateVariableFilter SynthesisFilter { get; } = new();
    }
}

/// <summary>
/// State variable filter optimized for vocoder band processing.
/// </summary>
internal class EnhancedStateVariableFilter
{
    private float _low, _band, _high;
    private float _f, _q;

    /// <summary>
    /// Configures the filter as a bandpass.
    /// </summary>
    /// <param name="frequency">Center frequency in Hz.</param>
    /// <param name="bandwidth">Bandwidth in Hz.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    public void SetBandpass(float frequency, float bandwidth, int sampleRate)
    {
        _f = 2f * MathF.Sin(MathF.PI * frequency / sampleRate);
        _q = 1f / (bandwidth / frequency);
        _f = MathF.Min(_f, 1f);
    }

    /// <summary>
    /// Processes a single sample through the bandpass filter.
    /// </summary>
    /// <param name="input">Input sample.</param>
    /// <returns>Bandpass filtered output.</returns>
    public float Process(float input)
    {
        _low += _f * _band;
        _high = input - _low - _q * _band;
        _band += _f * _high;
        return _band;
    }

    /// <summary>
    /// Resets the filter state.
    /// </summary>
    public void Reset()
    {
        _low = _band = _high = 0f;
    }
}
