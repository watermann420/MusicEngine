//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Saturator effect with multiple saturation types and oversampling for warmth and color.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Distortion;

/// <summary>
/// Saturation type determining the character of the harmonic distortion.
/// </summary>
public enum SaturationType
{
    /// <summary>
    /// Tape saturation - warm, smooth, with soft compression and high-frequency roll-off.
    /// Adds mostly odd harmonics with subtle even harmonics.
    /// </summary>
    Tape,

    /// <summary>
    /// Tube saturation - rich, warm, with asymmetric clipping.
    /// Adds predominantly even harmonics for a musical character.
    /// </summary>
    Tube,

    /// <summary>
    /// Transistor saturation - tighter, more aggressive with harder knee.
    /// Produces more odd harmonics and a grittier tone.
    /// </summary>
    Transistor,

    /// <summary>
    /// Digital saturation - clean, precise waveshaping.
    /// Produces predictable harmonic content with sharp transients.
    /// </summary>
    Digital
}

/// <summary>
/// Oversampling factor for aliasing reduction during saturation processing.
/// </summary>
public enum SaturatorOversampling
{
    /// <summary>
    /// No oversampling (1x). Fastest but may produce aliasing at high drive settings.
    /// </summary>
    None = 1,

    /// <summary>
    /// 2x oversampling. Good balance of quality and performance.
    /// </summary>
    TwoX = 2,

    /// <summary>
    /// 4x oversampling. High quality with minimal aliasing.
    /// </summary>
    FourX = 4,

    /// <summary>
    /// 8x oversampling. Maximum quality for critical applications.
    /// </summary>
    EightX = 8
}

/// <summary>
/// Saturator effect providing warm harmonic saturation with multiple saturation models.
/// Features adjustable drive, tone color, and oversampling for high-quality processing.
/// </summary>
/// <remarks>
/// The saturator generates harmonic content by applying nonlinear waveshaping to the input signal.
/// Different saturation types model various analog circuit behaviors:
/// - Tape: Magnetic tape hysteresis with soft compression
/// - Tube: Vacuum tube triode characteristics with even harmonic emphasis
/// - Transistor: Solid-state clipping with odd harmonic content
/// - Digital: Mathematical waveshaping for precise control
///
/// Oversampling processes the signal at a higher sample rate to prevent aliasing
/// artifacts that can occur when generating new harmonic frequencies.
/// </remarks>
public class Saturator : EffectBase
{
    // DC blocker state per channel
    private float[] _dcBlockerState = null!;
    private const float DcBlockerCoeff = 0.995f;

    // Tone filter state per channel (high shelf)
    private float[] _toneFilterState = null!;

    // Tube asymmetry state (for tube mode warmth accumulation)
    private float[] _tubeWarmthState = null!;

    // Oversampling buffers
    private float[] _upsampleBuffer = null!;
    private float[] _downsampleBuffer = null!;

    // Anti-aliasing filter state for oversampling
    private float[][] _aaFilterState = null!;
    private const int AaFilterOrder = 4;

    private bool _initialized;

    /// <summary>
    /// Creates a new Saturator effect with default settings.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    public Saturator(ISampleProvider source) : this(source, "Saturator", SaturationType.Tape)
    {
    }

    /// <summary>
    /// Creates a new Saturator effect with specified saturation type.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="saturationType">Type of saturation to apply</param>
    public Saturator(ISampleProvider source, SaturationType saturationType)
        : this(source, "Saturator", saturationType)
    {
    }

    /// <summary>
    /// Creates a new Saturator effect with custom name and saturation type.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <param name="name">Effect name</param>
    /// <param name="saturationType">Type of saturation to apply</param>
    public Saturator(ISampleProvider source, string name, SaturationType saturationType = SaturationType.Tape)
        : base(source, name)
    {
        // Register parameters with defaults
        RegisterParameter("Drive", 0.5f);           // 0.0 - 1.0: Saturation amount
        RegisterParameter("SaturationType", (float)saturationType);
        RegisterParameter("Color", 0.5f);           // 0.0 - 1.0: Tone control (dark to bright)
        RegisterParameter("Mix", 1.0f);             // 0.0 - 1.0: Dry/wet mix
        RegisterParameter("OutputLevel", 1.0f);     // 0.0 - 2.0: Output gain
        RegisterParameter("Oversampling", 1f);      // 0 = 1x, 1 = 2x, 2 = 4x, 3 = 8x

        _initialized = false;
    }

    /// <summary>
    /// Drive amount controlling saturation intensity (0.0 - 1.0).
    /// Higher values produce more harmonic distortion and compression.
    /// </summary>
    public float Drive
    {
        get => GetParameter("Drive");
        set => SetParameter("Drive", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Type of saturation determining the harmonic character.
    /// </summary>
    public SaturationType Type
    {
        get => (SaturationType)(int)GetParameter("SaturationType");
        set => SetParameter("SaturationType", (float)value);
    }

    /// <summary>
    /// Tone color control (0.0 - 1.0).
    /// 0.0 = dark/warm (high frequency cut), 1.0 = bright/crisp (high frequency boost).
    /// </summary>
    public float Color
    {
        get => GetParameter("Color");
        set => SetParameter("Color", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Dry/wet mix (0.0 - 1.0).
    /// 0.0 = fully dry (no effect), 1.0 = fully wet (full effect).
    /// </summary>
    public new float Mix
    {
        get => base.Mix;
        set => base.Mix = value;
    }

    /// <summary>
    /// Output level for gain compensation (0.0 - 2.0).
    /// Use to balance the output volume after saturation.
    /// </summary>
    public float OutputLevel
    {
        get => GetParameter("OutputLevel");
        set => SetParameter("OutputLevel", Math.Clamp(value, 0f, 2f));
    }

    /// <summary>
    /// Oversampling factor for aliasing reduction.
    /// Higher values provide cleaner harmonics at the cost of CPU usage.
    /// </summary>
    public SaturatorOversampling Oversampling
    {
        get
        {
            int val = (int)GetParameter("Oversampling");
            return val switch
            {
                0 => SaturatorOversampling.None,
                1 => SaturatorOversampling.TwoX,
                2 => SaturatorOversampling.FourX,
                3 => SaturatorOversampling.EightX,
                _ => SaturatorOversampling.TwoX
            };
        }
        set
        {
            int val = value switch
            {
                SaturatorOversampling.None => 0,
                SaturatorOversampling.TwoX => 1,
                SaturatorOversampling.FourX => 2,
                SaturatorOversampling.EightX => 3,
                _ => 1
            };
            SetParameter("Oversampling", val);
        }
    }

    /// <summary>
    /// Initializes internal processing buffers.
    /// </summary>
    private void Initialize()
    {
        int channels = Channels;

        _dcBlockerState = new float[channels];
        _toneFilterState = new float[channels];
        _tubeWarmthState = new float[channels];

        // Allocate oversampling buffers for maximum factor (8x)
        int maxOversampleSize = 8192 * 8;
        _upsampleBuffer = new float[maxOversampleSize];
        _downsampleBuffer = new float[maxOversampleSize];

        // Anti-aliasing filter state (cascaded biquads)
        _aaFilterState = new float[channels][];
        for (int ch = 0; ch < channels; ch++)
        {
            _aaFilterState[ch] = new float[AaFilterOrder * 2]; // 2 state variables per section
        }

        _initialized = true;
    }

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (!_initialized)
        {
            Initialize();
        }

        int channels = Channels;
        float drive = Drive;
        float color = Color;
        float outputLevel = OutputLevel;
        SaturationType satType = Type;
        int oversampleFactor = (int)Oversampling;

        // Convert drive to gain (exponential curve for more musical response)
        float inputGain = 1f + drive * drive * 9f; // 1x to 10x gain

        // Calculate tone filter coefficient (simple one-pole)
        // Color 0.5 = neutral, <0.5 = darker, >0.5 = brighter
        float toneFreq = 2000f + (color - 0.5f) * 6000f; // 2kHz to 8kHz
        float toneCoeff = MathF.Exp(-2f * MathF.PI * toneFreq / SampleRate);

        for (int i = 0; i < count; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                float input = sourceBuffer[i + ch];

                // Apply input gain (drive)
                float driven = input * inputGain;

                float saturated;
                if (oversampleFactor > 1)
                {
                    // Process with oversampling
                    saturated = ProcessWithOversampling(driven, ch, satType, oversampleFactor);
                }
                else
                {
                    // Process at native sample rate
                    saturated = ApplySaturation(driven, satType, ch);
                }

                // Apply DC blocker
                float dcBlocked = saturated - _dcBlockerState[ch];
                _dcBlockerState[ch] = saturated - dcBlocked * DcBlockerCoeff;

                // Apply tone control (high shelf filter)
                float toned = ApplyToneFilter(dcBlocked, ch, color, toneCoeff);

                // Apply output level
                float output = toned * outputLevel;

                // Soft limit to prevent harsh clipping
                output = SoftLimit(output);

                destBuffer[offset + i + ch] = output;
            }
        }
    }

    /// <summary>
    /// Processes a sample with oversampling for aliasing reduction.
    /// </summary>
    private float ProcessWithOversampling(float input, int channel, SaturationType satType, int factor)
    {
        // Upsample (zero-stuffing with interpolation)
        _upsampleBuffer[0] = input;
        for (int j = 1; j < factor; j++)
        {
            _upsampleBuffer[j] = 0f;
        }

        // Apply interpolation filter (smooth the upsampled signal)
        for (int j = 0; j < factor; j++)
        {
            _upsampleBuffer[j] = ApplyAntiAliasFilter(_upsampleBuffer[j], channel, true) * factor;
        }

        // Apply saturation at oversampled rate
        for (int j = 0; j < factor; j++)
        {
            _upsampleBuffer[j] = ApplySaturation(_upsampleBuffer[j], satType, channel);
        }

        // Apply anti-aliasing filter before downsampling
        for (int j = 0; j < factor; j++)
        {
            _upsampleBuffer[j] = ApplyAntiAliasFilter(_upsampleBuffer[j], channel, false);
        }

        // Downsample (decimate - take every Nth sample)
        return _upsampleBuffer[0];
    }

    /// <summary>
    /// Applies a simple anti-aliasing lowpass filter.
    /// </summary>
    private float ApplyAntiAliasFilter(float input, int channel, bool isUpsampling)
    {
        // Simple one-pole lowpass for anti-aliasing
        float coeff = isUpsampling ? 0.5f : 0.25f;
        int stateIdx = isUpsampling ? 0 : 1;

        float output = _aaFilterState[channel][stateIdx] + coeff * (input - _aaFilterState[channel][stateIdx]);
        _aaFilterState[channel][stateIdx] = output;

        return output;
    }

    /// <summary>
    /// Applies saturation based on the selected type.
    /// </summary>
    private float ApplySaturation(float input, SaturationType satType, int channel)
    {
        return satType switch
        {
            SaturationType.Tape => TapeSaturation(input),
            SaturationType.Tube => TubeSaturation(input, channel),
            SaturationType.Transistor => TransistorSaturation(input),
            SaturationType.Digital => DigitalSaturation(input),
            _ => input
        };
    }

    /// <summary>
    /// Tape saturation emulation - warm, smooth compression with soft clipping.
    /// Based on magnetic tape hysteresis characteristics.
    /// </summary>
    private float TapeSaturation(float input)
    {
        // Tape saturation uses a combination of soft clipping and compression
        // The curve is asymptotically approaching +/-1 with a soft knee

        float absInput = MathF.Abs(input);
        float sign = MathF.Sign(input);

        if (absInput < 0.5f)
        {
            // Linear region with slight boost
            return input * 1.1f;
        }
        else if (absInput < 1.0f)
        {
            // Soft knee region
            float excess = absInput - 0.5f;
            float compressed = 0.5f + excess * 0.6f + excess * excess * 0.15f;
            return sign * compressed * 1.1f;
        }
        else
        {
            // Heavy saturation region - asymptotic approach to limit
            float excess = absInput - 1.0f;
            float saturated = 0.8f + 0.2f * MathF.Tanh(excess * 2f);
            return sign * saturated;
        }
    }

    /// <summary>
    /// Tube saturation emulation - warm with even harmonic emphasis.
    /// Models triode vacuum tube characteristics with asymmetric clipping.
    /// </summary>
    private float TubeSaturation(float input, int channel)
    {
        // Tube saturation is asymmetric - positive and negative halves clip differently
        // This generates even harmonics (2nd, 4th, etc.) which sound musical

        // Add subtle DC bias for asymmetry
        float biased = input + 0.1f;

        // Different saturation curves for positive and negative
        float saturated;
        if (biased >= 0f)
        {
            // Positive half - softer clipping (triode plate saturation)
            saturated = biased / (1f + 0.4f * biased);
        }
        else
        {
            // Negative half - slightly harder clipping (grid conduction)
            float abs = -biased;
            saturated = -abs / (1f + 0.6f * abs);
        }

        // Remove the DC offset introduced by bias
        saturated -= 0.1f / (1f + 0.04f);

        // Add subtle second harmonic warmth
        float warmth = saturated * saturated * 0.1f * MathF.Sign(saturated);
        _tubeWarmthState[channel] = _tubeWarmthState[channel] * 0.99f + warmth * 0.01f;
        saturated += _tubeWarmthState[channel];

        return saturated;
    }

    /// <summary>
    /// Transistor saturation emulation - tight, aggressive with odd harmonics.
    /// Models solid-state clipping characteristics.
    /// </summary>
    private float TransistorSaturation(float input)
    {
        // Transistor clipping is more symmetric and produces odd harmonics
        // Has a harder knee than tube saturation

        float absInput = MathF.Abs(input);
        float sign = MathF.Sign(input);

        if (absInput < 0.7f)
        {
            // Linear region
            return input;
        }
        else if (absInput < 1.2f)
        {
            // Transition region with moderate knee
            float excess = absInput - 0.7f;
            float ratio = 0.5f; // Compression ratio
            return sign * (0.7f + excess * ratio);
        }
        else
        {
            // Hard clipping region
            float excess = absInput - 1.2f;
            float clipped = 0.95f + 0.05f * MathF.Tanh(excess);
            return sign * clipped;
        }
    }

    /// <summary>
    /// Digital saturation - clean, precise waveshaping.
    /// Uses mathematical functions for predictable harmonic generation.
    /// </summary>
    private float DigitalSaturation(float input)
    {
        // Clean tanh-based saturation
        // Produces mostly odd harmonics with smooth limiting
        return MathF.Tanh(input * 1.5f) / MathF.Tanh(1.5f);
    }

    /// <summary>
    /// Applies tone control using a simple high shelf filter.
    /// </summary>
    private float ApplyToneFilter(float input, int channel, float color, float toneCoeff)
    {
        // Extract high frequencies
        float highPass = input - _toneFilterState[channel];
        _toneFilterState[channel] = _toneFilterState[channel] * toneCoeff + input * (1f - toneCoeff);

        // Apply gain based on color setting
        // Color < 0.5 reduces highs, Color > 0.5 boosts highs
        float highGain = (color - 0.5f) * 2f; // -1 to +1
        float boostedHigh = highPass * (1f + highGain * 0.5f);

        return _toneFilterState[channel] + boostedHigh;
    }

    /// <summary>
    /// Soft limiter to prevent harsh digital clipping on output.
    /// </summary>
    private static float SoftLimit(float input)
    {
        const float threshold = 0.95f;

        if (MathF.Abs(input) <= threshold)
            return input;

        float sign = MathF.Sign(input);
        float absInput = MathF.Abs(input);
        float excess = absInput - threshold;

        // Soft knee limiting
        float limited = threshold + (1f - threshold) * MathF.Tanh(excess / (1f - threshold));
        return sign * limited;
    }

    /// <summary>
    /// Creates a subtle warmth preset for adding gentle color.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <returns>Configured Saturator instance</returns>
    public static Saturator CreateSubtleWarmth(ISampleProvider source)
    {
        var saturator = new Saturator(source, "Subtle Warmth", SaturationType.Tape);
        saturator.Drive = 0.25f;
        saturator.Color = 0.45f;
        saturator.OutputLevel = 0.95f;
        saturator.Oversampling = SaturatorOversampling.TwoX;
        saturator.Mix = 0.6f;
        return saturator;
    }

    /// <summary>
    /// Creates a tube warmth preset for rich harmonic content.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <returns>Configured Saturator instance</returns>
    public static Saturator CreateTubeWarmth(ISampleProvider source)
    {
        var saturator = new Saturator(source, "Tube Warmth", SaturationType.Tube);
        saturator.Drive = 0.4f;
        saturator.Color = 0.5f;
        saturator.OutputLevel = 0.9f;
        saturator.Oversampling = SaturatorOversampling.FourX;
        saturator.Mix = 1.0f;
        return saturator;
    }

    /// <summary>
    /// Creates a tape compression preset for glue and warmth.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <returns>Configured Saturator instance</returns>
    public static Saturator CreateTapeCompression(ISampleProvider source)
    {
        var saturator = new Saturator(source, "Tape Compression", SaturationType.Tape);
        saturator.Drive = 0.6f;
        saturator.Color = 0.4f;
        saturator.OutputLevel = 0.85f;
        saturator.Oversampling = SaturatorOversampling.FourX;
        saturator.Mix = 1.0f;
        return saturator;
    }

    /// <summary>
    /// Creates an aggressive transistor drive preset.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <returns>Configured Saturator instance</returns>
    public static Saturator CreateTransistorDrive(ISampleProvider source)
    {
        var saturator = new Saturator(source, "Transistor Drive", SaturationType.Transistor);
        saturator.Drive = 0.7f;
        saturator.Color = 0.6f;
        saturator.OutputLevel = 0.8f;
        saturator.Oversampling = SaturatorOversampling.FourX;
        saturator.Mix = 1.0f;
        return saturator;
    }

    /// <summary>
    /// Creates a clean digital saturation preset.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <returns>Configured Saturator instance</returns>
    public static Saturator CreateDigitalEdge(ISampleProvider source)
    {
        var saturator = new Saturator(source, "Digital Edge", SaturationType.Digital);
        saturator.Drive = 0.5f;
        saturator.Color = 0.55f;
        saturator.OutputLevel = 0.9f;
        saturator.Oversampling = SaturatorOversampling.TwoX;
        saturator.Mix = 1.0f;
        return saturator;
    }

    /// <summary>
    /// Creates a heavy saturation preset for aggressive processing.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <returns>Configured Saturator instance</returns>
    public static Saturator CreateHeavySaturation(ISampleProvider source)
    {
        var saturator = new Saturator(source, "Heavy Saturation", SaturationType.Tube);
        saturator.Drive = 0.85f;
        saturator.Color = 0.5f;
        saturator.OutputLevel = 0.7f;
        saturator.Oversampling = SaturatorOversampling.EightX;
        saturator.Mix = 1.0f;
        return saturator;
    }

    /// <summary>
    /// Creates a mastering-grade subtle saturation preset.
    /// </summary>
    /// <param name="source">Audio source to process</param>
    /// <returns>Configured Saturator instance</returns>
    public static Saturator CreateMasteringSaturation(ISampleProvider source)
    {
        var saturator = new Saturator(source, "Mastering Saturation", SaturationType.Tape);
        saturator.Drive = 0.15f;
        saturator.Color = 0.5f;
        saturator.OutputLevel = 1.0f;
        saturator.Oversampling = SaturatorOversampling.EightX;
        saturator.Mix = 0.4f;
        return saturator;
    }
}
