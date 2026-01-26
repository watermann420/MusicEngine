// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Filter effect processor.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Filters;

/// <summary>
/// Filter types available for FilterEffect
/// </summary>
public enum FilterType
{
    /// <summary>Low-pass filter - passes frequencies below cutoff</summary>
    Lowpass,
    /// <summary>High-pass filter - passes frequencies above cutoff</summary>
    Highpass,
    /// <summary>Band-pass filter - passes frequencies around cutoff</summary>
    Bandpass,
    /// <summary>Band-reject (notch) filter - rejects frequencies around cutoff</summary>
    Bandreject,
    /// <summary>Allpass filter - passes all frequencies but changes phase</summary>
    Allpass
}

/// <summary>
/// State-variable filter effect with multiple filter types.
/// Uses the Chamberlin state-variable filter algorithm for smooth, musical filtering.
/// </summary>
public class FilterEffect : EffectBase
{
    private FilterState[] _channelStates;

    /// <summary>
    /// Creates a new filter effect
    /// </summary>
    /// <param name="source">Audio source to filter</param>
    /// <param name="name">Effect name</param>
    /// <param name="filterType">Type of filter (lowpass, highpass, etc.)</param>
    public FilterEffect(ISampleProvider source, string name, FilterType filterType = FilterType.Lowpass)
        : base(source, name)
    {
        _channelStates = new FilterState[source.WaveFormat.Channels];
        for (int i = 0; i < _channelStates.Length; i++)
        {
            _channelStates[i] = new FilterState();
        }

        // Initialize parameters
        RegisterParameter("Cutoff", 1000f);      // 1000 Hz default cutoff
        RegisterParameter("Resonance", 0.707f);   // Q factor (0.707 = Butterworth response)
        RegisterParameter("FilterType", (float)filterType);
        Mix = 1.0f;        // 100% wet by default
    }

    /// <summary>
    /// Filter cutoff frequency in Hz (20 - 20000)
    /// </summary>
    public float Cutoff
    {
        get => GetParameter("Cutoff");
        set => SetParameter("Cutoff", Math.Clamp(value, 20f, 20000f));
    }

    /// <summary>
    /// Filter resonance/Q factor (0.1 - 10.0)
    /// Higher values create more resonance at the cutoff frequency
    /// </summary>
    public float Resonance
    {
        get => GetParameter("Resonance");
        set => SetParameter("Resonance", Math.Clamp(value, 0.1f, 10f));
    }

    /// <summary>
    /// Type of filter to apply
    /// </summary>
    public FilterType Type
    {
        get => (FilterType)GetParameter("FilterType");
        set => SetParameter("FilterType", (float)value);
    }

    /// <summary>
    /// Dry/wet mix (0.0 - 1.0)
    /// </summary>
    public float DryWet
    {
        get => Mix;
        set => Mix = value;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        float cutoff = Cutoff;
        float resonance = Resonance;
        FilterType filterType = Type;

        // Calculate filter coefficients (Chamberlin state-variable filter)
        // f = 2 * sin(π * cutoff / sampleRate)
        float f = 2f * MathF.Sin(MathF.PI * cutoff / sampleRate);
        // q = 1 / resonance
        float q = 1f / resonance;

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int srcIndex = i + ch;
                int destIndex = offset + i + ch;
                float input = sourceBuffer[srcIndex];

                ref FilterState state = ref _channelStates[ch];

                // State-variable filter algorithm
                state.low += f * state.band;
                state.high = input - state.low - q * state.band;
                state.band += f * state.high;
                state.notch = state.high + state.low;

                // Select output based on filter type
                float output = filterType switch
                {
                    FilterType.Lowpass => state.low,
                    FilterType.Highpass => state.high,
                    FilterType.Bandpass => state.band,
                    FilterType.Bandreject => state.notch,
                    FilterType.Allpass => state.low - q * state.band + state.high,
                    _ => input
                };

                destBuffer[destIndex] = output;
            }
        }
    }

    /// <summary>
    /// Internal filter state for each channel
    /// </summary>
    private struct FilterState
    {
        public float low;    // Lowpass output
        public float high;   // Highpass output
        public float band;   // Bandpass output
        public float notch;  // Notch (band-reject) output
    }
}
