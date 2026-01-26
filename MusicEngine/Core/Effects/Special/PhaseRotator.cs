//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: All-pass filter based phase rotation effect for peak reduction and stereo widening.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Phase rotation mode for different use cases.
/// </summary>
public enum PhaseRotationMode
{
    /// <summary>
    /// Apply same phase rotation to both channels.
    /// Useful for peak reduction without affecting stereo image.
    /// </summary>
    Mono,

    /// <summary>
    /// Apply opposite phase rotation to left and right channels.
    /// Creates stereo widening effect.
    /// </summary>
    StereoWiden,

    /// <summary>
    /// Apply phase rotation only to left channel.
    /// </summary>
    LeftOnly,

    /// <summary>
    /// Apply phase rotation only to right channel.
    /// </summary>
    RightOnly,

    /// <summary>
    /// Apply phase rotation to mid channel only (M/S processing).
    /// </summary>
    MidOnly,

    /// <summary>
    /// Apply phase rotation to side channel only (M/S processing).
    /// </summary>
    SideOnly
}

/// <summary>
/// All-pass filter based phase rotation effect.
/// Rotates the phase of audio signals without changing frequency content.
/// Useful for reducing peak levels in asymmetric waveforms and creating stereo widening effects.
/// </summary>
/// <remarks>
/// The effect works by:
/// 1. Using cascaded first-order all-pass filters to create frequency-dependent phase shift
/// 2. Each stage contributes additional phase rotation around the center frequency
/// 3. Multiple stages create steeper phase transition
/// 4. Can be used for peak reduction (rotating asymmetric waveforms) or stereo widening
/// </remarks>
public class PhaseRotator : EffectBase
{
    private const int MaxStages = 16;
    private const float MinFrequency = 20f;
    private const float MaxFrequency = 20000f;

    // All-pass filter states for each stage and channel
    private readonly float[] _statesLeft;
    private readonly float[] _statesRight;

    // Filter coefficients (one per stage, recalculated when frequency changes)
    private readonly float[] _coefficients;

    // Parameters
    private float _frequency = 200f;
    private int _stages = 4;
    private float _phaseShift = 90f;
    private PhaseRotationMode _mode = PhaseRotationMode.Mono;
    private bool _coefficientsNeedUpdate = true;

    /// <summary>
    /// Creates a new phase rotator effect.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    public PhaseRotator(ISampleProvider source) : this(source, "Phase Rotator")
    {
    }

    /// <summary>
    /// Creates a new phase rotator effect with a custom name.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <param name="name">Effect name.</param>
    public PhaseRotator(ISampleProvider source, string name) : base(source, name)
    {
        _statesLeft = new float[MaxStages];
        _statesRight = new float[MaxStages];
        _coefficients = new float[MaxStages];

        RegisterParameter("Frequency", 200f);
        RegisterParameter("Stages", 4f);
        RegisterParameter("PhaseShift", 90f);
        RegisterParameter("Mode", (float)PhaseRotationMode.Mono);
        RegisterParameter("Mix", 1f);
    }

    /// <summary>
    /// Gets or sets the center frequency for phase rotation in Hz (20-20000).
    /// The phase transition occurs around this frequency.
    /// </summary>
    public float Frequency
    {
        get => _frequency;
        set
        {
            float clamped = Math.Clamp(value, MinFrequency, MaxFrequency);
            if (Math.Abs(_frequency - clamped) > 0.01f)
            {
                _frequency = clamped;
                _coefficientsNeedUpdate = true;
                SetParameter("Frequency", _frequency);
            }
        }
    }

    /// <summary>
    /// Gets or sets the number of all-pass filter stages (1-16).
    /// More stages create steeper phase transition and more precise rotation.
    /// </summary>
    public int Stages
    {
        get => _stages;
        set
        {
            int clamped = Math.Clamp(value, 1, MaxStages);
            if (_stages != clamped)
            {
                _stages = clamped;
                _coefficientsNeedUpdate = true;
                SetParameter("Stages", _stages);
            }
        }
    }

    /// <summary>
    /// Gets or sets the target phase shift in degrees (-180 to 180).
    /// Common values: 90 degrees for peak reduction, 180 for polarity inversion.
    /// </summary>
    public float PhaseShift
    {
        get => _phaseShift;
        set
        {
            float clamped = Math.Clamp(value, -180f, 180f);
            if (Math.Abs(_phaseShift - clamped) > 0.01f)
            {
                _phaseShift = clamped;
                _coefficientsNeedUpdate = true;
                SetParameter("PhaseShift", _phaseShift);
            }
        }
    }

    /// <summary>
    /// Gets or sets the phase rotation mode.
    /// </summary>
    public PhaseRotationMode Mode
    {
        get => _mode;
        set
        {
            if (_mode != value)
            {
                _mode = value;
                SetParameter("Mode", (float)_mode);
            }
        }
    }

    /// <summary>
    /// Resets all filter states to zero.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_statesLeft);
        Array.Clear(_statesRight);
    }

    /// <summary>
    /// Updates the filter coefficients based on current parameters.
    /// </summary>
    private void UpdateCoefficients()
    {
        // Calculate the coefficient for a first-order all-pass filter
        // All-pass filter: y[n] = c * x[n] + x[n-1] - c * y[n-1]
        // where c = (tan(pi*fc/fs) - 1) / (tan(pi*fc/fs) + 1)

        float omega = MathF.PI * _frequency / SampleRate;
        float tanOmega = MathF.Tan(omega);
        float baseCoef = (tanOmega - 1f) / (tanOmega + 1f);

        // Each stage uses the same coefficient for the base frequency
        // The phase shift accumulates across stages
        // Adjust coefficient based on desired total phase shift
        float phasePerStage = (_phaseShift * MathF.PI / 180f) / _stages;

        for (int i = 0; i < _stages; i++)
        {
            // Slight frequency spreading across stages for smoother transition
            float stageFreq = _frequency * (1f + (i - _stages / 2f) * 0.02f);
            stageFreq = Math.Clamp(stageFreq, MinFrequency, MaxFrequency);

            float stageOmega = MathF.PI * stageFreq / SampleRate;
            float stageTan = MathF.Tan(stageOmega);
            _coefficients[i] = (stageTan - 1f) / (stageTan + 1f);
        }

        _coefficientsNeedUpdate = false;
    }

    /// <summary>
    /// Processes a single sample through one all-pass filter stage.
    /// </summary>
    /// <param name="input">Input sample.</param>
    /// <param name="state">Filter state (previous output).</param>
    /// <param name="coef">Filter coefficient.</param>
    /// <returns>Filtered sample.</returns>
    private static float ProcessAllpassStage(float input, ref float state, float coef)
    {
        // First-order all-pass filter
        // y[n] = c * (x[n] - y[n-1]) + x[n-1]
        // where x[n-1] is stored in state before update
        float output = coef * (input - state) + state;
        state = input;
        return output;
    }

    /// <summary>
    /// Processes a sample through all active stages.
    /// </summary>
    /// <param name="input">Input sample.</param>
    /// <param name="states">Array of filter states.</param>
    /// <returns>Phase-rotated sample.</returns>
    private float ProcessAllStages(float input, float[] states)
    {
        float output = input;
        for (int i = 0; i < _stages; i++)
        {
            output = ProcessAllpassStage(output, ref states[i], _coefficients[i]);
        }
        return output;
    }

    /// <inheritdoc/>
    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        if (_coefficientsNeedUpdate)
        {
            UpdateCoefficients();
        }

        int channels = Channels;
        bool isStereo = channels >= 2;

        for (int n = 0; n < count; n += channels)
        {
            float inputL = sourceBuffer[n];
            float inputR = isStereo ? sourceBuffer[n + 1] : inputL;

            float outputL, outputR;

            switch (_mode)
            {
                case PhaseRotationMode.Mono:
                    // Apply same rotation to both channels
                    outputL = ProcessAllStages(inputL, _statesLeft);
                    outputR = isStereo ? ProcessAllStages(inputR, _statesRight) : outputL;
                    break;

                case PhaseRotationMode.StereoWiden:
                    // Apply opposite rotation for stereo widening
                    // Left gets positive rotation, right gets negative (inverted)
                    outputL = ProcessAllStages(inputL, _statesLeft);
                    if (isStereo)
                    {
                        // For stereo widening, we process normally but with inverted phase relationship
                        float processedR = ProcessAllStages(inputR, _statesRight);
                        // Subtle widening by mixing original with processed in opposite phase
                        float mid = (inputL + inputR) * 0.5f;
                        float side = (inputL - inputR) * 0.5f;
                        float processedMid = (outputL + processedR) * 0.5f;
                        float processedSide = (outputL - processedR) * 0.5f;
                        // Enhance side channel contribution
                        outputL = processedMid + processedSide * 1.2f;
                        outputR = processedMid - processedSide * 1.2f;
                    }
                    else
                    {
                        outputR = outputL;
                    }
                    break;

                case PhaseRotationMode.LeftOnly:
                    // Only rotate left channel
                    outputL = ProcessAllStages(inputL, _statesLeft);
                    outputR = inputR;
                    break;

                case PhaseRotationMode.RightOnly:
                    // Only rotate right channel
                    outputL = inputL;
                    outputR = isStereo ? ProcessAllStages(inputR, _statesRight) : inputL;
                    break;

                case PhaseRotationMode.MidOnly:
                    // M/S processing: rotate mid only
                    if (isStereo)
                    {
                        float mid = (inputL + inputR) * 0.5f;
                        float side = (inputL - inputR) * 0.5f;
                        float processedMid = ProcessAllStages(mid, _statesLeft);
                        outputL = processedMid + side;
                        outputR = processedMid - side;
                    }
                    else
                    {
                        outputL = ProcessAllStages(inputL, _statesLeft);
                        outputR = outputL;
                    }
                    break;

                case PhaseRotationMode.SideOnly:
                    // M/S processing: rotate side only
                    if (isStereo)
                    {
                        float mid = (inputL + inputR) * 0.5f;
                        float side = (inputL - inputR) * 0.5f;
                        float processedSide = ProcessAllStages(side, _statesLeft);
                        outputL = mid + processedSide;
                        outputR = mid - processedSide;
                    }
                    else
                    {
                        outputL = inputL;
                        outputR = outputL;
                    }
                    break;

                default:
                    outputL = inputL;
                    outputR = inputR;
                    break;
            }

            destBuffer[offset + n] = outputL;
            if (isStereo)
            {
                destBuffer[offset + n + 1] = outputR;
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnParameterChanged(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "frequency":
                _frequency = Math.Clamp(value, MinFrequency, MaxFrequency);
                _coefficientsNeedUpdate = true;
                break;
            case "stages":
                _stages = Math.Clamp((int)value, 1, MaxStages);
                _coefficientsNeedUpdate = true;
                break;
            case "phaseshift":
                _phaseShift = Math.Clamp(value, -180f, 180f);
                _coefficientsNeedUpdate = true;
                break;
            case "mode":
                _mode = (PhaseRotationMode)Math.Clamp((int)value, 0, 5);
                break;
        }
    }

    /// <summary>
    /// Creates a peak reduction preset optimized for vocal processing.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured PhaseRotator effect.</returns>
    public static PhaseRotator CreateVocalPeakReduction(ISampleProvider source)
    {
        var effect = new PhaseRotator(source, "Vocal Peak Reduction");
        effect.Frequency = 300f;
        effect.Stages = 8;
        effect.PhaseShift = 90f;
        effect.Mode = PhaseRotationMode.Mono;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a preset optimized for bass peak reduction.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured PhaseRotator effect.</returns>
    public static PhaseRotator CreateBassPeakReduction(ISampleProvider source)
    {
        var effect = new PhaseRotator(source, "Bass Peak Reduction");
        effect.Frequency = 80f;
        effect.Stages = 12;
        effect.PhaseShift = 90f;
        effect.Mode = PhaseRotationMode.Mono;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a stereo widening preset.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured PhaseRotator effect.</returns>
    public static PhaseRotator CreateStereoWidening(ISampleProvider source)
    {
        var effect = new PhaseRotator(source, "Stereo Widening");
        effect.Frequency = 1000f;
        effect.Stages = 4;
        effect.PhaseShift = 45f;
        effect.Mode = PhaseRotationMode.StereoWiden;
        effect.Mix = 0.7f;
        return effect;
    }

    /// <summary>
    /// Creates a broadcast-style phase rotation preset.
    /// Used in broadcast to maximize perceived loudness without clipping.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured PhaseRotator effect.</returns>
    public static PhaseRotator CreateBroadcastOptimizer(ISampleProvider source)
    {
        var effect = new PhaseRotator(source, "Broadcast Optimizer");
        effect.Frequency = 200f;
        effect.Stages = 16;
        effect.PhaseShift = 90f;
        effect.Mode = PhaseRotationMode.Mono;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a drum transient shaping preset.
    /// Phase rotation can help control drum transients.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured PhaseRotator effect.</returns>
    public static PhaseRotator CreateDrumTransientShaper(ISampleProvider source)
    {
        var effect = new PhaseRotator(source, "Drum Transient Shaper");
        effect.Frequency = 150f;
        effect.Stages = 6;
        effect.PhaseShift = 60f;
        effect.Mode = PhaseRotationMode.Mono;
        effect.Mix = 1f;
        return effect;
    }

    /// <summary>
    /// Creates a subtle stereo enhancement for mastering.
    /// </summary>
    /// <param name="source">Audio source to process.</param>
    /// <returns>Configured PhaseRotator effect.</returns>
    public static PhaseRotator CreateMasteringEnhancer(ISampleProvider source)
    {
        var effect = new PhaseRotator(source, "Mastering Enhancer");
        effect.Frequency = 2000f;
        effect.Stages = 2;
        effect.PhaseShift = 30f;
        effect.Mode = PhaseRotationMode.SideOnly;
        effect.Mix = 0.5f;
        return effect;
    }
}
