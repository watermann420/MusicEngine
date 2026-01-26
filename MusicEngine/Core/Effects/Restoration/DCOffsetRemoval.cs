//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: DC offset removal using single-pole high-pass filter.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Restoration;

/// <summary>
/// DC offset removal effect using a single-pole high-pass filter.
/// Removes DC offset and very low frequency content that can cause headroom issues.
/// </summary>
/// <remarks>
/// Uses the formula: y[n] = alpha * (y[n-1] + x[n] - x[n-1])
/// where alpha = (1 - tan(pi*fc/fs)) / (1 + tan(pi*fc/fs))
/// </remarks>
public class DCOffsetRemoval : EffectBase
{
    // Filter state per channel
    private float[] _previousInput;
    private float[] _previousOutput;
    private float _alpha;
    private bool _coefficientsValid;

    /// <summary>
    /// Creates a new DC offset removal effect.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    public DCOffsetRemoval(ISampleProvider source) : this(source, "DC Offset Removal")
    {
    }

    /// <summary>
    /// Creates a new DC offset removal effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public DCOffsetRemoval(ISampleProvider source, string name) : base(source, name)
    {
        int channels = source.WaveFormat.Channels;
        _previousInput = new float[channels];
        _previousOutput = new float[channels];

        // Register parameters with defaults
        RegisterParameter("CutoffHz", 10f);  // 5-100Hz, default 10Hz
        RegisterParameter("Mix", 1f);

        _coefficientsValid = false;
        UpdateCoefficients();
    }

    /// <summary>
    /// Cutoff frequency in Hz (5 - 100 Hz).
    /// Lower values preserve more low frequency content.
    /// Default is 10 Hz which removes DC offset while preserving bass.
    /// </summary>
    public float CutoffHz
    {
        get => GetParameter("CutoffHz");
        set => SetParameter("CutoffHz", Math.Clamp(value, 5f, 100f));
    }

    /// <summary>
    /// Updates the filter coefficient based on cutoff frequency.
    /// </summary>
    private void UpdateCoefficients()
    {
        float fc = CutoffHz;
        float fs = SampleRate;

        // Calculate alpha = (1 - tan(pi*fc/fs)) / (1 + tan(pi*fc/fs))
        float tanTerm = MathF.Tan(MathF.PI * fc / fs);
        _alpha = (1f - tanTerm) / (1f + tanTerm);

        _coefficientsValid = true;
    }

    protected override void OnParameterChanged(string name, float value)
    {
        if (name.Equals("CutoffHz", StringComparison.OrdinalIgnoreCase))
        {
            _coefficientsValid = false;
        }
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_coefficientsValid)
        {
            UpdateCoefficients();
        }

        int channels = Channels;
        float alpha = _alpha;

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[i + ch];

                // Single-pole high-pass filter: y[n] = alpha * (y[n-1] + x[n] - x[n-1])
                float output = alpha * (_previousOutput[ch] + input - _previousInput[ch]);

                // Store state for next sample
                _previousInput[ch] = input;
                _previousOutput[ch] = output;

                destBuffer[offset + i + ch] = output;
            }
        }
    }

    /// <summary>
    /// Resets the filter state (clears stored samples).
    /// Call this when seeking or starting playback from a new position.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_previousInput, 0, _previousInput.Length);
        Array.Clear(_previousOutput, 0, _previousOutput.Length);
    }

    #region Presets

    /// <summary>
    /// Creates a preset with minimal filtering (5Hz cutoff).
    /// Removes only true DC offset while preserving all audible content.
    /// </summary>
    public static DCOffsetRemoval CreateMinimal(ISampleProvider source)
    {
        var effect = new DCOffsetRemoval(source, "DC Offset - Minimal");
        effect.CutoffHz = 5f;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset with standard filtering (10Hz cutoff).
    /// Removes DC offset and subsonic rumble.
    /// </summary>
    public static DCOffsetRemoval CreateStandard(ISampleProvider source)
    {
        var effect = new DCOffsetRemoval(source, "DC Offset - Standard");
        effect.CutoffHz = 10f;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset with aggressive filtering (20Hz cutoff).
    /// Removes DC offset and more subsonic content for cleaner mixes.
    /// </summary>
    public static DCOffsetRemoval CreateAggressive(ISampleProvider source)
    {
        var effect = new DCOffsetRemoval(source, "DC Offset - Aggressive");
        effect.CutoffHz = 20f;
        effect.Mix = 1f;
        return effect;
    }

    #endregion
}
