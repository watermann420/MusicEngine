//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Click and pop removal using derivative analysis and interpolation.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Restoration;

/// <summary>
/// Interpolation method used for click repair.
/// </summary>
public enum ClickInterpolationMode
{
    /// <summary>
    /// Linear interpolation - fast but may create audible artifacts on long clicks.
    /// </summary>
    Linear,

    /// <summary>
    /// Cubic spline interpolation - smoother results for longer click durations.
    /// </summary>
    Cubic
}

/// <summary>
/// Click and pop removal effect using derivative analysis and interpolation.
/// </summary>
/// <remarks>
/// The algorithm works by:
/// 1. Computing the derivative (difference between consecutive samples)
/// 2. Detecting sudden amplitude changes that exceed a threshold
/// 3. Interpolating over detected clicks to repair the audio
///
/// This is effective for removing vinyl clicks, digital glitches, and short pops.
/// </remarks>
public class ClickRemoval : EffectBase
{
    // Detection state per channel
    private float[] _previousSample;
    private float[] _previousDerivative;

    // Click detection buffer per channel
    private float[][] _sampleBuffer = Array.Empty<float[]>();
    private int[] _bufferWritePos = Array.Empty<int>();
    private int _bufferSize;

    // Detection state
    private bool[][] _clickDetected = Array.Empty<bool[]>();
    private int[] _clickStartPos = Array.Empty<int>();
    private int[] _clickLength = Array.Empty<int>();

    // State tracking
    private bool _initialized;

    /// <summary>
    /// Creates a new click removal effect.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    public ClickRemoval(ISampleProvider source) : this(source, "Click Removal")
    {
    }

    /// <summary>
    /// Creates a new click removal effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    public ClickRemoval(ISampleProvider source, string name) : base(source, name)
    {
        int channels = source.WaveFormat.Channels;
        _previousSample = new float[channels];
        _previousDerivative = new float[channels];

        // Register parameters with defaults
        RegisterParameter("Sensitivity", 0.5f);           // 0.1-1.0, default 0.5
        RegisterParameter("MaxClickDuration", 0.002f);    // 0.001-0.01 seconds, default 0.002 (2ms)
        RegisterParameter("InterpolationMode", (float)ClickInterpolationMode.Cubic);
        RegisterParameter("Mix", 1f);

        _initialized = false;
    }

    /// <summary>
    /// Detection sensitivity (0.1 - 1.0).
    /// Higher values detect more clicks but may cause false positives.
    /// Lower values only detect strong clicks.
    /// </summary>
    public float Sensitivity
    {
        get => GetParameter("Sensitivity");
        set => SetParameter("Sensitivity", Math.Clamp(value, 0.1f, 1f));
    }

    /// <summary>
    /// Maximum click duration in seconds (0.001 - 0.01).
    /// Clicks longer than this are not repaired to avoid damaging valid transients.
    /// </summary>
    public float MaxClickDuration
    {
        get => GetParameter("MaxClickDuration");
        set => SetParameter("MaxClickDuration", Math.Clamp(value, 0.001f, 0.01f));
    }

    /// <summary>
    /// Interpolation mode used for click repair.
    /// </summary>
    public ClickInterpolationMode InterpolationMode
    {
        get => (ClickInterpolationMode)GetParameter("InterpolationMode");
        set => SetParameter("InterpolationMode", (float)value);
    }

    /// <summary>
    /// Initializes internal buffers based on max click duration.
    /// </summary>
    private void Initialize()
    {
        int channels = Channels;
        int sampleRate = SampleRate;

        // Buffer size = 2x max click duration to have context before and after
        int maxClickSamples = (int)(MaxClickDuration * sampleRate);
        _bufferSize = maxClickSamples * 4; // Extra space for lookahead

        _sampleBuffer = new float[channels][];
        _bufferWritePos = new int[channels];
        _clickDetected = new bool[channels][];
        _clickStartPos = new int[channels];
        _clickLength = new int[channels];

        for (int ch = 0; ch < channels; ch++)
        {
            _sampleBuffer[ch] = new float[_bufferSize];
            _bufferWritePos[ch] = 0;
            _clickDetected[ch] = new bool[_bufferSize];
            _clickStartPos[ch] = -1;
            _clickLength[ch] = 0;
        }

        _initialized = true;
    }

    protected override void OnParameterChanged(string name, float value)
    {
        if (name.Equals("MaxClickDuration", StringComparison.OrdinalIgnoreCase))
        {
            _initialized = false; // Force reinitialization
        }
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        int channels = Channels;
        int sampleRate = SampleRate;

        // Calculate detection threshold based on sensitivity
        // Lower sensitivity = higher threshold (less sensitive)
        float sensitivity = Sensitivity;
        float baseThreshold = 0.3f; // Base derivative threshold
        float threshold = baseThreshold * (1.1f - sensitivity); // Inverse relationship

        // Maximum click length in samples
        int maxClickSamples = (int)(MaxClickDuration * sampleRate);

        // Delay for lookahead (half the buffer)
        int delay = _bufferSize / 2;

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[i + ch];
                int writePos = _bufferWritePos[ch];

                // Store sample in buffer
                _sampleBuffer[ch][writePos] = input;

                // Compute derivative (difference from previous sample)
                float derivative = input - _previousSample[ch];
                float derivativeChange = MathF.Abs(derivative - _previousDerivative[ch]);

                // Detect click based on sudden derivative change
                bool isClick = derivativeChange > threshold;
                _clickDetected[ch][writePos] = isClick;

                // Track click start and length
                if (isClick)
                {
                    if (_clickStartPos[ch] < 0)
                    {
                        _clickStartPos[ch] = writePos;
                        _clickLength[ch] = 1;
                    }
                    else
                    {
                        _clickLength[ch]++;
                    }
                }
                else if (_clickStartPos[ch] >= 0)
                {
                    // Click ended - repair if within max duration
                    if (_clickLength[ch] <= maxClickSamples && _clickLength[ch] > 0)
                    {
                        RepairClick(ch, _clickStartPos[ch], _clickLength[ch]);
                    }
                    _clickStartPos[ch] = -1;
                    _clickLength[ch] = 0;
                }

                // Read from delayed position (lookahead)
                int readPos = (writePos - delay + _bufferSize) % _bufferSize;
                float output = _sampleBuffer[ch][readPos];

                destBuffer[offset + i + ch] = output;

                // Update state
                _previousSample[ch] = input;
                _previousDerivative[ch] = derivative;
                _bufferWritePos[ch] = (writePos + 1) % _bufferSize;
            }
        }
    }

    /// <summary>
    /// Repairs a detected click by interpolating over it.
    /// </summary>
    private void RepairClick(int channel, int startPos, int length)
    {
        if (length <= 0) return;

        ClickInterpolationMode mode = InterpolationMode;

        // Get samples before and after the click for interpolation
        int beforePos = (startPos - 1 + _bufferSize) % _bufferSize;
        int afterPos = (startPos + length) % _bufferSize;

        float beforeSample = _sampleBuffer[channel][beforePos];
        float afterSample = _sampleBuffer[channel][afterPos];

        if (mode == ClickInterpolationMode.Linear)
        {
            // Linear interpolation
            for (int i = 0; i < length; i++)
            {
                int pos = (startPos + i) % _bufferSize;
                float t = (float)(i + 1) / (length + 1);
                _sampleBuffer[channel][pos] = beforeSample + t * (afterSample - beforeSample);
            }
        }
        else // Cubic interpolation
        {
            // Get additional samples for cubic interpolation
            int beforePos2 = (startPos - 2 + _bufferSize) % _bufferSize;
            int afterPos2 = (startPos + length + 1) % _bufferSize;

            float beforeSample2 = _sampleBuffer[channel][beforePos2];
            float afterSample2 = _sampleBuffer[channel][afterPos2];

            // Cubic spline interpolation using Catmull-Rom
            for (int i = 0; i < length; i++)
            {
                int pos = (startPos + i) % _bufferSize;
                float t = (float)(i + 1) / (length + 1);

                // Catmull-Rom spline formula
                float t2 = t * t;
                float t3 = t2 * t;

                float p0 = beforeSample2;
                float p1 = beforeSample;
                float p2 = afterSample;
                float p3 = afterSample2;

                float result = 0.5f * (
                    2f * p1 +
                    (-p0 + p2) * t +
                    (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                    (-p0 + 3f * p1 - 3f * p2 + p3) * t3
                );

                // Clamp to prevent overshoot
                result = Math.Clamp(result, -1f, 1f);

                _sampleBuffer[channel][pos] = result;
            }
        }

        // Clear click detection flags for repaired samples
        for (int i = 0; i < length; i++)
        {
            int pos = (startPos + i) % _bufferSize;
            _clickDetected[channel][pos] = false;
        }
    }

    /// <summary>
    /// Resets the effect state.
    /// Call this when seeking or starting playback from a new position.
    /// </summary>
    public void Reset()
    {
        if (!_initialized) return;

        int channels = Channels;
        for (int ch = 0; ch < channels; ch++)
        {
            Array.Clear(_sampleBuffer[ch], 0, _sampleBuffer[ch].Length);
            Array.Clear(_clickDetected[ch], 0, _clickDetected[ch].Length);
            _bufferWritePos[ch] = 0;
            _clickStartPos[ch] = -1;
            _clickLength[ch] = 0;
        }

        Array.Clear(_previousSample, 0, _previousSample.Length);
        Array.Clear(_previousDerivative, 0, _previousDerivative.Length);
    }

    #region Presets

    /// <summary>
    /// Creates a preset for gentle click removal.
    /// Only removes obvious, strong clicks. Safe for most material.
    /// </summary>
    public static ClickRemoval CreateGentle(ISampleProvider source)
    {
        var effect = new ClickRemoval(source, "Click Removal - Gentle");
        effect.Sensitivity = 0.3f;
        effect.MaxClickDuration = 0.001f;
        effect.InterpolationMode = ClickInterpolationMode.Cubic;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for standard click removal.
    /// Balanced settings for typical vinyl or digital click artifacts.
    /// </summary>
    public static ClickRemoval CreateStandard(ISampleProvider source)
    {
        var effect = new ClickRemoval(source, "Click Removal - Standard");
        effect.Sensitivity = 0.5f;
        effect.MaxClickDuration = 0.002f;
        effect.InterpolationMode = ClickInterpolationMode.Cubic;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for aggressive click removal.
    /// Removes more clicks but may affect some transients.
    /// </summary>
    public static ClickRemoval CreateAggressive(ISampleProvider source)
    {
        var effect = new ClickRemoval(source, "Click Removal - Aggressive");
        effect.Sensitivity = 0.7f;
        effect.MaxClickDuration = 0.005f;
        effect.InterpolationMode = ClickInterpolationMode.Cubic;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset optimized for vinyl records.
    /// Tuned for typical vinyl click characteristics.
    /// </summary>
    public static ClickRemoval CreateVinyl(ISampleProvider source)
    {
        var effect = new ClickRemoval(source, "Click Removal - Vinyl");
        effect.Sensitivity = 0.6f;
        effect.MaxClickDuration = 0.003f;
        effect.InterpolationMode = ClickInterpolationMode.Cubic;
        effect.Mix = 1f;
        return effect;
    }

    #endregion
}
