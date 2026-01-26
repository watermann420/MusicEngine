// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Mid/Side processing mode.
/// </summary>
public enum MidSideMode
{
    /// <summary>
    /// Normal processing - encode to M/S, process, decode back to L/R.
    /// </summary>
    Normal,

    /// <summary>
    /// Monitor mid channel only.
    /// </summary>
    MonitorMid,

    /// <summary>
    /// Monitor side channel only.
    /// </summary>
    MonitorSide,

    /// <summary>
    /// Output M/S encoded signal (for external processing).
    /// </summary>
    EncodeOnly,

    /// <summary>
    /// Input is M/S, decode to L/R only.
    /// </summary>
    DecodeOnly
}

/// <summary>
/// Mid/Side stereo processor for encoding, decoding, and independent M/S manipulation.
/// </summary>
/// <remarks>
/// Features:
/// - Encode stereo to M/S format
/// - Independent mid and side gain control
/// - Stereo width control
/// - Separate mid/side processing chains
/// - Decode back to L/R with phase compensation
/// </remarks>
public class MidSideProcessor : EffectBase
{
    // Filter states for mid/side EQ
    private float _midLpState;
    private float _midHpState;
    private float _sideLpState;
    private float _sideHpState;

    // Bass mono crossover
    private float _bassMonoLpStateL;
    private float _bassMonoLpStateR;

    // Correlation metering
    private float _correlationSum;
    private float _powerSumL;
    private float _powerSumR;
    private int _correlationCount;

    /// <summary>
    /// Creates a new Mid/Side processor.
    /// </summary>
    /// <param name="source">Stereo audio source to process.</param>
    public MidSideProcessor(ISampleProvider source) : this(source, "Mid/Side Processor")
    {
    }

    /// <summary>
    /// Creates a new Mid/Side processor with a custom name.
    /// </summary>
    /// <param name="source">Stereo audio source to process.</param>
    /// <param name="name">Effect name.</param>
    public MidSideProcessor(ISampleProvider source, string name) : base(source, name)
    {
        if (source.WaveFormat.Channels != 2)
        {
            throw new ArgumentException("Source must be stereo (2 channels)", nameof(source));
        }

        RegisterParameter("MidGain", 0f);           // dB (-24 to +24)
        RegisterParameter("SideGain", 0f);          // dB (-24 to +24)
        RegisterParameter("Width", 1f);             // 0 = mono, 1 = normal, 2 = extra wide
        RegisterParameter("Mode", (float)MidSideMode.Normal);
        RegisterParameter("BassMonoFreq", 0f);      // Hz (0 = disabled, 20-300)
        RegisterParameter("MidHighCut", 20000f);    // Hz
        RegisterParameter("MidLowCut", 20f);        // Hz
        RegisterParameter("SideHighCut", 20000f);   // Hz
        RegisterParameter("SideLowCut", 20f);       // Hz
        RegisterParameter("OutputGain", 0f);        // dB
        RegisterParameter("Mix", 1f);
    }

    /// <summary>
    /// Gets or sets the mid channel gain in dB (-24 to +24).
    /// </summary>
    public float MidGain
    {
        get => GetParameter("MidGain");
        set => SetParameter("MidGain", Math.Clamp(value, -24f, 24f));
    }

    /// <summary>
    /// Gets or sets the side channel gain in dB (-24 to +24).
    /// </summary>
    public float SideGain
    {
        get => GetParameter("SideGain");
        set => SetParameter("SideGain", Math.Clamp(value, -24f, 24f));
    }

    /// <summary>
    /// Gets or sets the stereo width (0 = mono, 1 = normal, 2 = extra wide).
    /// </summary>
    public float Width
    {
        get => GetParameter("Width");
        set => SetParameter("Width", Math.Clamp(value, 0f, 2f));
    }

    /// <summary>
    /// Gets or sets the processing mode.
    /// </summary>
    public MidSideMode Mode
    {
        get => (MidSideMode)GetParameter("Mode");
        set => SetParameter("Mode", (float)value);
    }

    /// <summary>
    /// Gets or sets the bass mono crossover frequency in Hz (0 = disabled).
    /// Frequencies below this are summed to mono.
    /// </summary>
    public float BassMonoFrequency
    {
        get => GetParameter("BassMonoFreq");
        set => SetParameter("BassMonoFreq", Math.Clamp(value, 0f, 300f));
    }

    /// <summary>
    /// Gets or sets the mid channel high-cut frequency in Hz.
    /// </summary>
    public float MidHighCut
    {
        get => GetParameter("MidHighCut");
        set => SetParameter("MidHighCut", Math.Clamp(value, 100f, 20000f));
    }

    /// <summary>
    /// Gets or sets the mid channel low-cut frequency in Hz.
    /// </summary>
    public float MidLowCut
    {
        get => GetParameter("MidLowCut");
        set => SetParameter("MidLowCut", Math.Clamp(value, 20f, 2000f));
    }

    /// <summary>
    /// Gets or sets the side channel high-cut frequency in Hz.
    /// </summary>
    public float SideHighCut
    {
        get => GetParameter("SideHighCut");
        set => SetParameter("SideHighCut", Math.Clamp(value, 100f, 20000f));
    }

    /// <summary>
    /// Gets or sets the side channel low-cut frequency in Hz.
    /// </summary>
    public float SideLowCut
    {
        get => GetParameter("SideLowCut");
        set => SetParameter("SideLowCut", Math.Clamp(value, 20f, 2000f));
    }

    /// <summary>
    /// Gets or sets the output gain in dB (-24 to +24).
    /// </summary>
    public float OutputGain
    {
        get => GetParameter("OutputGain");
        set => SetParameter("OutputGain", Math.Clamp(value, -24f, 24f));
    }

    /// <summary>
    /// Gets the current stereo correlation (-1 to +1).
    /// +1 = mono, 0 = uncorrelated, -1 = out of phase
    /// </summary>
    public float Correlation { get; private set; } = 1f;

    /// <summary>
    /// Gets the current mid level in dB.
    /// </summary>
    public float MidLevel { get; private set; } = -60f;

    /// <summary>
    /// Gets the current side level in dB.
    /// </summary>
    public float SideLevel { get; private set; } = -60f;

    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int sampleRate = SampleRate;
        var mode = Mode;

        // Get parameters
        float midGainLinear = DbToLinear(MidGain);
        float sideGainLinear = DbToLinear(SideGain);
        float width = Width;
        float outputGainLinear = DbToLinear(OutputGain);
        float bassMonoFreq = BassMonoFrequency;

        // Calculate filter coefficients
        float midHpCoef = MathF.Exp(-2f * MathF.PI * MidLowCut / sampleRate);
        float midLpCoef = MathF.Exp(-2f * MathF.PI * MidHighCut / sampleRate);
        float sideHpCoef = MathF.Exp(-2f * MathF.PI * SideLowCut / sampleRate);
        float sideLpCoef = MathF.Exp(-2f * MathF.PI * SideHighCut / sampleRate);
        float bassMonoCoef = bassMonoFreq > 0 ? MathF.Exp(-2f * MathF.PI * bassMonoFreq / sampleRate) : 0f;

        // Metering accumulators
        float midPower = 0f;
        float sidePower = 0f;
        int frames = count / 2;

        // Process stereo frames
        for (int i = 0; i < count; i += 2)
        {
            float left = sourceBuffer[i];
            float right = sourceBuffer[i + 1];

            float outputL, outputR;

            if (mode == MidSideMode.DecodeOnly)
            {
                // Input is M/S, decode to L/R
                float mid = left;
                float side = right;
                outputL = (mid + side) * 0.5f;
                outputR = (mid - side) * 0.5f;
            }
            else
            {
                // Encode to M/S
                float mid = (left + right) * 0.5f;
                float side = (left - right) * 0.5f;

                // Apply bass mono if enabled
                if (bassMonoCoef > 0)
                {
                    // Extract bass
                    _bassMonoLpStateL = _bassMonoLpStateL * bassMonoCoef + left * (1f - bassMonoCoef);
                    _bassMonoLpStateR = _bassMonoLpStateR * bassMonoCoef + right * (1f - bassMonoCoef);

                    // Sum bass to mono, keep highs stereo
                    float bassL = _bassMonoLpStateL;
                    float bassR = _bassMonoLpStateR;
                    float bassMono = (bassL + bassR) * 0.5f;

                    float highL = left - bassL;
                    float highR = right - bassR;

                    // Re-encode highs to M/S, bass goes to mid only
                    mid = bassMono + (highL + highR) * 0.5f;
                    side = (highL - highR) * 0.5f;
                }

                // Apply mid filtering
                if (MidLowCut > 20f)
                {
                    _midHpState = _midHpState * midHpCoef + mid * (1f - midHpCoef);
                    mid = mid - _midHpState;
                }
                if (MidHighCut < 20000f)
                {
                    _midLpState = _midLpState * midLpCoef + mid * (1f - midLpCoef);
                    mid = _midLpState;
                }

                // Apply side filtering
                if (SideLowCut > 20f)
                {
                    _sideHpState = _sideHpState * sideHpCoef + side * (1f - sideHpCoef);
                    side = side - _sideHpState;
                }
                if (SideHighCut < 20000f)
                {
                    _sideLpState = _sideLpState * sideLpCoef + side * (1f - sideLpCoef);
                    side = _sideLpState;
                }

                // Apply gains
                mid *= midGainLinear;
                side *= sideGainLinear;

                // Apply width (affects side channel)
                side *= width;

                // Metering
                midPower += mid * mid;
                sidePower += side * side;

                // Output based on mode
                switch (mode)
                {
                    case MidSideMode.MonitorMid:
                        outputL = mid;
                        outputR = mid;
                        break;

                    case MidSideMode.MonitorSide:
                        outputL = side;
                        outputR = -side;  // Invert for proper stereo monitoring
                        break;

                    case MidSideMode.EncodeOnly:
                        outputL = mid;
                        outputR = side;
                        break;

                    case MidSideMode.Normal:
                    default:
                        // Decode back to L/R
                        outputL = mid + side;
                        outputR = mid - side;
                        break;
                }
            }

            // Apply output gain
            outputL *= outputGainLinear;
            outputR *= outputGainLinear;

            // Update correlation metering
            _correlationSum += outputL * outputR;
            _powerSumL += outputL * outputL;
            _powerSumR += outputR * outputR;
            _correlationCount++;

            // Update correlation periodically
            if (_correlationCount >= sampleRate / 10)
            {
                float power = MathF.Sqrt(_powerSumL * _powerSumR);
                Correlation = power > 1e-6f ? _correlationSum / power : 1f;
                _correlationSum = 0f;
                _powerSumL = 0f;
                _powerSumR = 0f;
                _correlationCount = 0;
            }

            destBuffer[offset + i] = outputL;
            destBuffer[offset + i + 1] = outputR;
        }

        // Update level meters
        if (frames > 0)
        {
            MidLevel = LinearToDb(MathF.Sqrt(midPower / frames));
            SideLevel = LinearToDb(MathF.Sqrt(sidePower / frames));
        }
    }

    private static float DbToLinear(float db) => MathF.Pow(10f, db / 20f);
    private static float LinearToDb(float linear) => 20f * MathF.Log10(linear + 1e-10f);

    #region Presets

    /// <summary>
    /// Creates a preset for subtle stereo widening.
    /// </summary>
    public static MidSideProcessor CreateSubtleWidePreset(ISampleProvider source)
    {
        var effect = new MidSideProcessor(source, "Subtle Wide");
        effect.MidGain = -1f;
        effect.SideGain = 2f;
        effect.Width = 1.2f;
        effect.BassMonoFrequency = 100f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for dramatic stereo widening.
    /// </summary>
    public static MidSideProcessor CreateExtraWidePreset(ISampleProvider source)
    {
        var effect = new MidSideProcessor(source, "Extra Wide");
        effect.MidGain = -3f;
        effect.SideGain = 4f;
        effect.Width = 1.8f;
        effect.BassMonoFrequency = 150f;
        effect.SideLowCut = 200f;  // Remove low-frequency side content
        return effect;
    }

    /// <summary>
    /// Creates a preset for mono-compatible bass.
    /// </summary>
    public static MidSideProcessor CreateMonoBassPreset(ISampleProvider source)
    {
        var effect = new MidSideProcessor(source, "Mono Bass");
        effect.MidGain = 0f;
        effect.SideGain = 0f;
        effect.Width = 1f;
        effect.BassMonoFrequency = 120f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for vocal isolation (mid emphasis).
    /// </summary>
    public static MidSideProcessor CreateVocalIsolationPreset(ISampleProvider source)
    {
        var effect = new MidSideProcessor(source, "Vocal Isolation");
        effect.MidGain = 3f;
        effect.SideGain = -12f;
        effect.Width = 0.3f;
        effect.MidLowCut = 100f;
        effect.MidHighCut = 8000f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for removing center content (karaoke).
    /// </summary>
    public static MidSideProcessor CreateKaraokePreset(ISampleProvider source)
    {
        var effect = new MidSideProcessor(source, "Karaoke");
        effect.MidGain = -60f;  // Essentially mute mid
        effect.SideGain = 3f;
        effect.Width = 1.5f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for narrowing stereo image.
    /// </summary>
    public static MidSideProcessor CreateNarrowPreset(ISampleProvider source)
    {
        var effect = new MidSideProcessor(source, "Narrow");
        effect.MidGain = 2f;
        effect.SideGain = -6f;
        effect.Width = 0.5f;
        return effect;
    }

    /// <summary>
    /// Creates a preset for converting to mono.
    /// </summary>
    public static MidSideProcessor CreateMonoPreset(ISampleProvider source)
    {
        var effect = new MidSideProcessor(source, "Mono");
        effect.Width = 0f;
        return effect;
    }

    #endregion
}
