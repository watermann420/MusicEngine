//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Multiband compressor with configurable crossover frequencies and per-band dynamics.


using System;
using NAudio.Wave;


namespace MusicEngine.Core.Effects.Dynamics;


/// <summary>
/// A single band of the multiband compressor
/// </summary>
public class CompressorBand
{
    /// <summary>Threshold in dB</summary>
    public float Threshold { get; set; } = -20f;

    /// <summary>Compression ratio</summary>
    public float Ratio { get; set; } = 4f;

    /// <summary>Attack time in seconds</summary>
    public float Attack { get; set; } = 0.005f;

    /// <summary>Release time in seconds</summary>
    public float Release { get; set; } = 0.1f;

    /// <summary>Output gain for this band in dB</summary>
    public float Gain { get; set; } = 0f;

    /// <summary>Solo this band</summary>
    public bool Solo { get; set; } = false;

    /// <summary>Mute this band</summary>
    public bool Mute { get; set; } = false;

    /// <summary>Bypass compression for this band</summary>
    public bool Bypass { get; set; } = false;

    // Internal state
    internal float[] Envelope;
    internal float[] GainSmooth;

    public CompressorBand(int channels)
    {
        Envelope = new float[channels];
        GainSmooth = new float[channels];
        for (int i = 0; i < channels; i++)
        {
            GainSmooth[i] = 1f;
        }
    }
}


/// <summary>
/// Multiband compressor with 4 bands and Linkwitz-Riley crossover filters.
/// Allows independent compression settings for each frequency band.
/// </summary>
public class MultibandCompressor : EffectBase
{
    private const int NumBands = 4;

    // Bands
    private readonly CompressorBand[] _bands;

    // Crossover frequencies (3 crossovers for 4 bands)
    private float[] _crossoverFreqs = { 200f, 1000f, 5000f };

    // Linkwitz-Riley filter states (2nd order = 2 biquads cascaded)
    // For each crossover: lowpass and highpass, 2 stages each, per channel
    private readonly BiquadFilter[,,] _lowpassFilters;   // [crossover, stage, channel]
    private readonly BiquadFilter[,,] _highpassFilters;

    /// <summary>
    /// Low crossover frequency (Hz)
    /// </summary>
    public float CrossoverLow
    {
        get => _crossoverFreqs[0];
        set
        {
            _crossoverFreqs[0] = Math.Clamp(value, 20f, CrossoverMid - 50f);
            UpdateFilters();
        }
    }

    /// <summary>
    /// Mid crossover frequency (Hz)
    /// </summary>
    public float CrossoverMid
    {
        get => _crossoverFreqs[1];
        set
        {
            _crossoverFreqs[1] = Math.Clamp(value, CrossoverLow + 50f, CrossoverHigh - 100f);
            UpdateFilters();
        }
    }

    /// <summary>
    /// High crossover frequency (Hz)
    /// </summary>
    public float CrossoverHigh
    {
        get => _crossoverFreqs[2];
        set
        {
            _crossoverFreqs[2] = Math.Clamp(value, CrossoverMid + 100f, 18000f);
            UpdateFilters();
        }
    }

    /// <summary>
    /// Get a band by index (0=Low, 1=LowMid, 2=HighMid, 3=High)
    /// </summary>
    public CompressorBand GetBand(int index) => _bands[Math.Clamp(index, 0, NumBands - 1)];

    /// <summary>
    /// Low band (0-200 Hz by default)
    /// </summary>
    public CompressorBand LowBand => _bands[0];

    /// <summary>
    /// Low-mid band (200-1000 Hz by default)
    /// </summary>
    public CompressorBand LowMidBand => _bands[1];

    /// <summary>
    /// High-mid band (1000-5000 Hz by default)
    /// </summary>
    public CompressorBand HighMidBand => _bands[2];

    /// <summary>
    /// High band (5000+ Hz by default)
    /// </summary>
    public CompressorBand HighBand => _bands[3];

    /// <summary>
    /// Creates a multiband compressor
    /// </summary>
    public MultibandCompressor(ISampleProvider source) : base(source, "Multiband Compressor")
    {
        int channels = Channels;

        // Initialize bands
        _bands = new CompressorBand[NumBands];
        for (int i = 0; i < NumBands; i++)
        {
            _bands[i] = new CompressorBand(channels);
        }

        // Set default band settings
        _bands[0].Threshold = -24f; _bands[0].Ratio = 3f;   // Low: gentle compression
        _bands[1].Threshold = -20f; _bands[1].Ratio = 4f;   // LowMid: moderate
        _bands[2].Threshold = -18f; _bands[2].Ratio = 4f;   // HighMid: moderate
        _bands[3].Threshold = -16f; _bands[3].Ratio = 3f;   // High: gentle

        // Initialize filters (3 crossovers, 2 stages each, per channel)
        _lowpassFilters = new BiquadFilter[3, 2, channels];
        _highpassFilters = new BiquadFilter[3, 2, channels];

        for (int xover = 0; xover < 3; xover++)
        {
            for (int stage = 0; stage < 2; stage++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    _lowpassFilters[xover, stage, ch] = new BiquadFilter();
                    _highpassFilters[xover, stage, ch] = new BiquadFilter();
                }
            }
        }

        UpdateFilters();

        RegisterParameter("mix", 1f);
        RegisterParameter("crossoverlow", 200f);
        RegisterParameter("crossovermid", 1000f);
        RegisterParameter("crossoverhigh", 5000f);
    }

    private void UpdateFilters()
    {
        int channels = Channels;

        for (int xover = 0; xover < 3; xover++)
        {
            float freq = _crossoverFreqs[xover];

            // Calculate Butterworth coefficients for cascaded 2nd order = 4th order LR
            float w0 = 2f * MathF.PI * freq / SampleRate;
            float cosW0 = MathF.Cos(w0);
            float sinW0 = MathF.Sin(w0);
            float alpha = sinW0 / (2f * 0.7071f); // Q = 0.7071 for Butterworth

            // Lowpass coefficients
            float lpB0 = (1f - cosW0) / 2f;
            float lpB1 = 1f - cosW0;
            float lpB2 = (1f - cosW0) / 2f;
            float lpA0 = 1f + alpha;
            float lpA1 = -2f * cosW0;
            float lpA2 = 1f - alpha;

            // Highpass coefficients
            float hpB0 = (1f + cosW0) / 2f;
            float hpB1 = -(1f + cosW0);
            float hpB2 = (1f + cosW0) / 2f;

            for (int stage = 0; stage < 2; stage++)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    _lowpassFilters[xover, stage, ch].SetCoefficients(
                        lpB0 / lpA0, lpB1 / lpA0, lpB2 / lpA0, lpA1 / lpA0, lpA2 / lpA0);

                    _highpassFilters[xover, stage, ch].SetCoefficients(
                        hpB0 / lpA0, hpB1 / lpA0, hpB2 / lpA0, lpA1 / lpA0, lpA2 / lpA0);
                }
            }
        }
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;

        // Temporary buffers for band splitting
        var bandSignals = new float[NumBands][];
        for (int b = 0; b < NumBands; b++)
        {
            bandSignals[b] = new float[count];
        }

        // Split into bands
        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[i + ch];

                // Split using crossover filters
                // Band 0: input -> LP[0]
                // Band 1: input -> HP[0] -> LP[1]
                // Band 2: input -> HP[0] -> HP[1] -> LP[2]
                // Band 3: input -> HP[0] -> HP[1] -> HP[2]

                float lp0 = ApplyFilter(_lowpassFilters, 0, ch, input);
                float hp0 = ApplyFilter(_highpassFilters, 0, ch, input);

                float lp1 = ApplyFilter(_lowpassFilters, 1, ch, hp0);
                float hp1 = ApplyFilter(_highpassFilters, 1, ch, hp0);

                float lp2 = ApplyFilter(_lowpassFilters, 2, ch, hp1);
                float hp2 = ApplyFilter(_highpassFilters, 2, ch, hp1);

                bandSignals[0][i + ch] = lp0;
                bandSignals[1][i + ch] = lp1;
                bandSignals[2][i + ch] = lp2;
                bandSignals[3][i + ch] = hp2;
            }
        }

        // Check for solo
        bool anySolo = false;
        for (int b = 0; b < NumBands; b++)
        {
            if (_bands[b].Solo) { anySolo = true; break; }
        }

        // Process each band with compression
        for (int b = 0; b < NumBands; b++)
        {
            var band = _bands[b];

            // Skip if muted or not soloed when another is
            bool shouldProcess = !band.Mute && (!anySolo || band.Solo);
            if (!shouldProcess)
            {
                Array.Clear(bandSignals[b], 0, count);
                continue;
            }

            if (!band.Bypass)
            {
                ProcessBandCompression(band, bandSignals[b], count, channels);
            }

            // Apply band gain
            float gainLin = MathF.Pow(10f, band.Gain / 20f);
            for (int i = 0; i < count; i++)
            {
                bandSignals[b][i] *= gainLin;
            }
        }

        // Sum bands
        for (int i = 0; i < count; i++)
        {
            float sum = 0;
            for (int b = 0; b < NumBands; b++)
            {
                sum += bandSignals[b][i];
            }
            destBuffer[offset + i] = sum;
        }
    }

    private float ApplyFilter(BiquadFilter[,,] filters, int xover, int channel, float input)
    {
        float output = filters[xover, 0, channel].Process(input);
        output = filters[xover, 1, channel].Process(output);
        return output;
    }

    private void ProcessBandCompression(CompressorBand band, float[] buffer, int count, int channels)
    {
        float attackCoeff = MathF.Exp(-1f / (band.Attack * SampleRate));
        float releaseCoeff = MathF.Exp(-1f / (band.Release * SampleRate));

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = buffer[i + ch];

                // Envelope follower
                float inputAbs = MathF.Abs(input);
                float coeff = inputAbs > band.Envelope[ch] ? attackCoeff : releaseCoeff;
                band.Envelope[ch] = inputAbs + coeff * (band.Envelope[ch] - inputAbs);

                // Convert to dB
                float inputDb = 20f * MathF.Log10(band.Envelope[ch] + 1e-6f);

                // Calculate gain reduction
                float gainReductionDb = 0f;
                if (inputDb > band.Threshold)
                {
                    gainReductionDb = (band.Threshold - inputDb) * (1f - 1f / band.Ratio);
                }

                // Apply smoothed gain
                float targetGain = MathF.Pow(10f, gainReductionDb / 20f);
                float smoothCoeff = targetGain < band.GainSmooth[ch] ? attackCoeff : releaseCoeff;
                band.GainSmooth[ch] = targetGain + smoothCoeff * (band.GainSmooth[ch] - targetGain);

                buffer[i + ch] = input * band.GainSmooth[ch];
            }
        }
    }

    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "crossoverlow":
                CrossoverLow = value;
                break;
            case "crossovermid":
                CrossoverMid = value;
                break;
            case "crossoverhigh":
                CrossoverHigh = value;
                break;
        }
    }

    /// <summary>
    /// Create a mastering preset
    /// </summary>
    public static MultibandCompressor CreateMasteringPreset(ISampleProvider source)
    {
        var comp = new MultibandCompressor(source);
        comp.CrossoverLow = 150f;
        comp.CrossoverMid = 800f;
        comp.CrossoverHigh = 6000f;

        comp.LowBand.Threshold = -18f; comp.LowBand.Ratio = 2f;
        comp.LowMidBand.Threshold = -16f; comp.LowMidBand.Ratio = 3f;
        comp.HighMidBand.Threshold = -14f; comp.HighMidBand.Ratio = 3f;
        comp.HighBand.Threshold = -12f; comp.HighBand.Ratio = 2f;

        return comp;
    }

    /// <summary>
    /// Create a vocal preset
    /// </summary>
    public static MultibandCompressor CreateVocalPreset(ISampleProvider source)
    {
        var comp = new MultibandCompressor(source);
        comp.CrossoverLow = 200f;
        comp.CrossoverMid = 2000f;
        comp.CrossoverHigh = 8000f;

        comp.LowBand.Threshold = -24f; comp.LowBand.Ratio = 4f;
        comp.LowMidBand.Threshold = -18f; comp.LowMidBand.Ratio = 3f;
        comp.HighMidBand.Threshold = -16f; comp.HighMidBand.Ratio = 2.5f;
        comp.HighBand.Threshold = -20f; comp.HighBand.Ratio = 2f; comp.HighBand.Gain = 2f;

        return comp;
    }
}


/// <summary>
/// Simple biquad filter for crossover implementation
/// </summary>
internal class BiquadFilter
{
    private float _b0, _b1, _b2, _a1, _a2;
    private float _x1, _x2, _y1, _y2;

    public void SetCoefficients(float b0, float b1, float b2, float a1, float a2)
    {
        _b0 = b0; _b1 = b1; _b2 = b2;
        _a1 = a1; _a2 = a2;
    }

    public float Process(float input)
    {
        float output = _b0 * input + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;

        _x2 = _x1;
        _x1 = input;
        _y2 = _y1;
        _y1 = output;

        return output;
    }

    public void Reset()
    {
        _x1 = _x2 = _y1 = _y2 = 0;
    }
}
