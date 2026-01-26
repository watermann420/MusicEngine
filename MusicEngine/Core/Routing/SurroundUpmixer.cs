// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio/MIDI routing component.

using NAudio.Wave;

namespace MusicEngine.Core.Routing;

/// <summary>
/// Upmix algorithm selection.
/// </summary>
public enum UpmixAlgorithm
{
    /// <summary>
    /// Simple stereo to surround distribution.
    /// </summary>
    Simple,

    /// <summary>
    /// Matrix decoding similar to Dolby Pro Logic.
    /// </summary>
    Matrix,

    /// <summary>
    /// Advanced algorithm with ambience extraction.
    /// </summary>
    Advanced
}

/// <summary>
/// Stereo to 5.1/7.1 upmixer with ambience extraction and intelligent channel routing.
/// </summary>
/// <remarks>
/// Features:
/// - Ambience extraction from stereo for surround channels
/// - Center channel extraction (vocal isolation)
/// - LFE generation from bass content
/// - Surround decorrelation for spacious sound
/// - Support for 5.1 and 7.1 output formats
/// </remarks>
public class SurroundUpmixer : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly SurroundFormat _format;
    private readonly WaveFormat _waveFormat;
    private readonly float[] _stereoBuffer;
    private readonly int _outputChannels;

    // Processing state
    private float _lpStateL;
    private float _hpStateL, _hpStateR;
    private float _diffStateL;

    // Allpass filters for decorrelation
    private readonly float[] _allpassBufferL;
    private readonly float[] _allpassBufferR;
    private int _allpassIndexL;
    private int _allpassIndexR;
    private const int AllpassDelayL = 557;  // Prime numbers for decorrelation
    private const int AllpassDelayR = 709;

    // Center extraction state
    private float _centerHpState;
    private float _centerLpState;

    // Parameters
    private float _centerLevel = 0.7f;
    private float _surroundLevel = 0.5f;
    private float _lfeLevel = 0.5f;
    private float _decorrelation = 0.5f;
    private float _lfeCrossover = 120f;
    private float _centerWidth = 0.7f;
    private UpmixAlgorithm _algorithm = UpmixAlgorithm.Advanced;

    /// <summary>
    /// Creates a new surround upmixer.
    /// </summary>
    /// <param name="stereoSource">Stereo audio source to upmix.</param>
    /// <param name="format">Target surround format (5.1 or 7.1).</param>
    public SurroundUpmixer(ISampleProvider stereoSource, SurroundFormat format)
    {
        if (stereoSource.WaveFormat.Channels != 2)
        {
            throw new ArgumentException("Source must be stereo (2 channels)", nameof(stereoSource));
        }

        if (format != SurroundFormat.Surround_5_1 && format != SurroundFormat.Surround_7_1 &&
            format != SurroundFormat.Atmos_7_1_4)
        {
            throw new ArgumentException("Target format must be 5.1, 7.1, or 7.1.4", nameof(format));
        }

        _source = stereoSource;
        _format = format;
        _outputChannels = format.GetChannelCount();

        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
            stereoSource.WaveFormat.SampleRate,
            _outputChannels);

        _stereoBuffer = new float[stereoSource.WaveFormat.SampleRate]; // 0.5 second buffer

        // Initialize decorrelation allpass buffers
        _allpassBufferL = new float[AllpassDelayL];
        _allpassBufferR = new float[AllpassDelayR];
    }

    /// <summary>
    /// Gets the output wave format.
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Gets or sets the center channel level (0-1).
    /// </summary>
    public float CenterLevel
    {
        get => _centerLevel;
        set => _centerLevel = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets the surround channel level (0-1).
    /// </summary>
    public float SurroundLevel
    {
        get => _surroundLevel;
        set => _surroundLevel = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets the LFE channel level (0-1).
    /// </summary>
    public float LFELevel
    {
        get => _lfeLevel;
        set => _lfeLevel = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets the surround decorrelation amount (0-1).
    /// Higher values create more spatial separation.
    /// </summary>
    public float Decorrelation
    {
        get => _decorrelation;
        set => _decorrelation = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets the LFE crossover frequency in Hz.
    /// </summary>
    public float LFECrossover
    {
        get => _lfeCrossover;
        set => _lfeCrossover = Math.Clamp(value, 40f, 200f);
    }

    /// <summary>
    /// Gets or sets the center channel extraction width (0-1).
    /// Higher values extract more center content.
    /// </summary>
    public float CenterWidth
    {
        get => _centerWidth;
        set => _centerWidth = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Gets or sets the upmix algorithm.
    /// </summary>
    public UpmixAlgorithm Algorithm
    {
        get => _algorithm;
        set => _algorithm = value;
    }

    /// <summary>
    /// Reads audio and performs upmixing.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int outputFrames = count / _outputChannels;
        int stereoSamples = outputFrames * 2;

        // Ensure buffer is large enough
        if (stereoSamples > _stereoBuffer.Length)
        {
            stereoSamples = _stereoBuffer.Length;
            outputFrames = stereoSamples / 2;
        }

        // Read stereo source
        int samplesRead = _source.Read(_stereoBuffer, 0, stereoSamples);
        if (samplesRead == 0)
        {
            return 0;
        }

        int framesRead = samplesRead / 2;
        int sampleRate = _waveFormat.SampleRate;

        // Calculate filter coefficients
        float lfeLpCoef = MathF.Exp(-2f * MathF.PI * _lfeCrossover / sampleRate);
        float diffHpCoef = MathF.Exp(-2f * MathF.PI * 200f / sampleRate);  // 200Hz HP for surround
        float centerBandLp = MathF.Exp(-2f * MathF.PI * 8000f / sampleRate);  // Center band limit
        float centerBandHp = MathF.Exp(-2f * MathF.PI * 100f / sampleRate);   // Center band HP

        // Process each frame
        for (int i = 0; i < framesRead; i++)
        {
            float left = _stereoBuffer[i * 2];
            float right = _stereoBuffer[i * 2 + 1];

            // Extract components based on algorithm
            float center, frontL, frontR, surroundL, surroundR, lfe;
            float? rearL = null, rearR = null;

            switch (_algorithm)
            {
                case UpmixAlgorithm.Simple:
                    ProcessSimple(left, right, lfeLpCoef,
                        out center, out frontL, out frontR, out surroundL, out surroundR, out lfe);
                    break;

                case UpmixAlgorithm.Matrix:
                    ProcessMatrix(left, right, lfeLpCoef, diffHpCoef,
                        out center, out frontL, out frontR, out surroundL, out surroundR, out lfe);
                    break;

                case UpmixAlgorithm.Advanced:
                default:
                    ProcessAdvanced(left, right, lfeLpCoef, diffHpCoef, centerBandLp, centerBandHp,
                        out center, out frontL, out frontR, out surroundL, out surroundR, out lfe,
                        out rearL, out rearR);
                    break;
            }

            // Apply decorrelation to surround channels
            if (_decorrelation > 0)
            {
                surroundL = ApplyDecorrelation(surroundL, _allpassBufferL, ref _allpassIndexL, AllpassDelayL);
                surroundR = ApplyDecorrelation(surroundR, _allpassBufferR, ref _allpassIndexR, AllpassDelayR);

                if (rearL.HasValue)
                {
                    rearL = rearL.Value * (1f - _decorrelation * 0.3f) +
                            ApplyDecorrelation(rearL.Value, _allpassBufferL, ref _allpassIndexL, AllpassDelayL) * (_decorrelation * 0.3f);
                    rearR = rearR!.Value * (1f - _decorrelation * 0.3f) +
                            ApplyDecorrelation(rearR.Value, _allpassBufferR, ref _allpassIndexR, AllpassDelayR) * (_decorrelation * 0.3f);
                }
            }

            // Apply output levels
            center *= _centerLevel;
            surroundL *= _surroundLevel;
            surroundR *= _surroundLevel;
            lfe *= _lfeLevel;

            if (rearL.HasValue)
            {
                rearL *= _surroundLevel;
                rearR *= _surroundLevel;
            }

            // Write to output buffer based on format
            int frameOffset = offset + i * _outputChannels;
            WriteOutputFrame(buffer, frameOffset, frontL, center, frontR, surroundL, surroundR, lfe, rearL, rearR);
        }

        return framesRead * _outputChannels;
    }

    /// <summary>
    /// Simple upmix algorithm.
    /// </summary>
    private void ProcessSimple(float left, float right, float lfeLpCoef,
        out float center, out float frontL, out float frontR,
        out float surroundL, out float surroundR, out float lfe)
    {
        // Simple center extraction
        center = (left + right) * 0.5f * _centerWidth;

        // Front channels retain most of the stereo image
        frontL = left * 0.8f;
        frontR = right * 0.8f;

        // Surround gets the difference signal
        float diff = (left - right) * 0.5f;
        surroundL = diff * 0.6f;
        surroundR = -diff * 0.6f;

        // LFE from lowpass
        float mono = (left + right) * 0.5f;
        _lpStateL = _lpStateL * lfeLpCoef + mono * (1f - lfeLpCoef);
        lfe = _lpStateL;
    }

    /// <summary>
    /// Matrix upmix algorithm (Pro Logic style).
    /// </summary>
    private void ProcessMatrix(float left, float right, float lfeLpCoef, float diffHpCoef,
        out float center, out float frontL, out float frontR,
        out float surroundL, out float surroundR, out float lfe)
    {
        // Matrix decode: Lt = L + 0.707*C - 0.707*S
        //                Rt = R + 0.707*C + 0.707*S
        // So: C = 0.707*(Lt + Rt), S = 0.707*(Rt - Lt)

        float sum = (left + right) * 0.5f;
        float diff = (right - left) * 0.5f;

        // Center channel (mono content)
        float centerBand = sum;
        _centerHpState = _centerHpState * 0.99f + centerBand * 0.01f;
        center = (centerBand - _centerHpState) * _centerWidth;

        // Front L/R with center removed
        float centerRemoval = center * 0.707f;
        frontL = left - centerRemoval;
        frontR = right - centerRemoval;

        // Surround from difference (highpassed)
        _diffStateL = _diffStateL * diffHpCoef + diff * (1f - diffHpCoef);
        float surroundMono = diff - _diffStateL;  // Highpassed difference

        surroundL = surroundMono * 0.707f;
        surroundR = -surroundMono * 0.707f;

        // LFE
        _lpStateL = _lpStateL * lfeLpCoef + sum * (1f - lfeLpCoef);
        lfe = _lpStateL;
    }

    /// <summary>
    /// Advanced upmix algorithm with ambience extraction.
    /// </summary>
    private void ProcessAdvanced(float left, float right, float lfeLpCoef, float diffHpCoef,
        float centerBandLp, float centerBandHp,
        out float center, out float frontL, out float frontR,
        out float surroundL, out float surroundR, out float lfe,
        out float? rearL, out float? rearR)
    {
        float sum = (left + right) * 0.5f;
        float diff = (left - right) * 0.5f;

        // Center extraction with frequency band limiting
        _centerLpState = _centerLpState * centerBandLp + sum * (1f - centerBandLp);
        _centerHpState = _centerHpState * centerBandHp + _centerLpState * (1f - centerBandHp);
        float centerBand = _centerLpState - _centerHpState;

        // Correlation-based center extraction
        float correlation = MathF.Abs(left * right) / (MathF.Abs(left * left) + MathF.Abs(right * right) + 1e-10f);
        float centerStrength = correlation * _centerWidth;
        center = centerBand * centerStrength * 1.4f;  // Boost center

        // Front channels with phantom center preserved
        float centerRemoval = center * 0.5f;
        frontL = left - centerRemoval;
        frontR = right - centerRemoval;

        // Ambience extraction (decorrelated content)
        _hpStateL = _hpStateL * diffHpCoef + left * (1f - diffHpCoef);
        _hpStateR = _hpStateR * diffHpCoef + right * (1f - diffHpCoef);
        float ambL = left - _hpStateL;  // High-passed for surround
        float ambR = right - _hpStateR;

        // Surround from ambience + difference
        float ambience = (ambL - ambR) * 0.5f;
        _diffStateL = _diffStateL * 0.95f + ambience * 0.05f;

        // For 7.1, split surround into side and rear
        if (_format == SurroundFormat.Surround_7_1 || _format == SurroundFormat.Atmos_7_1_4)
        {
            // Side surrounds get more direct ambience
            surroundL = ambL * 0.5f + diff * 0.3f;
            surroundR = ambR * 0.5f - diff * 0.3f;

            // Rear surrounds get more diffuse content
            rearL = ambience * 0.4f + _diffStateL * 0.3f;
            rearR = -ambience * 0.4f + _diffStateL * 0.3f;
        }
        else
        {
            // 5.1 - all ambience to surround
            surroundL = ambL * 0.5f + diff * 0.4f;
            surroundR = ambR * 0.5f - diff * 0.4f;
            rearL = null;
            rearR = null;
        }

        // LFE from lowpassed mono
        _lpStateL = _lpStateL * lfeLpCoef + sum * (1f - lfeLpCoef);
        lfe = _lpStateL * 1.5f;  // Boost LFE slightly
    }

    /// <summary>
    /// Applies allpass filter for decorrelation.
    /// </summary>
    private float ApplyDecorrelation(float input, float[] buffer, ref int index, int delay)
    {
        float coefficient = 0.618f;  // Golden ratio for allpass

        float delayed = buffer[index];
        float output = -input * coefficient + delayed;
        buffer[index] = input + delayed * coefficient;

        index = (index + 1) % delay;

        return input * (1f - _decorrelation) + output * _decorrelation;
    }

    /// <summary>
    /// Writes output frame to buffer based on format.
    /// </summary>
    private void WriteOutputFrame(float[] buffer, int offset,
        float frontL, float center, float frontR,
        float surroundL, float surroundR, float lfe,
        float? rearL, float? rearR)
    {
        switch (_format)
        {
            case SurroundFormat.Surround_5_1:
                // L, C, R, Ls, Rs, LFE
                buffer[offset + 0] = frontL;
                buffer[offset + 1] = center;
                buffer[offset + 2] = frontR;
                buffer[offset + 3] = surroundL;
                buffer[offset + 4] = surroundR;
                buffer[offset + 5] = lfe;
                break;

            case SurroundFormat.Surround_7_1:
                // L, C, R, Lss, Rss, Lsr, Rsr, LFE
                buffer[offset + 0] = frontL;
                buffer[offset + 1] = center;
                buffer[offset + 2] = frontR;
                buffer[offset + 3] = surroundL;  // Side
                buffer[offset + 4] = surroundR;
                buffer[offset + 5] = rearL ?? surroundL * 0.7f;  // Rear
                buffer[offset + 6] = rearR ?? surroundR * 0.7f;
                buffer[offset + 7] = lfe;
                break;

            case SurroundFormat.Atmos_7_1_4:
                // L, C, R, Lss, Rss, Lsr, Rsr, LFE, TFL, TFR, TRL, TRR
                buffer[offset + 0] = frontL;
                buffer[offset + 1] = center;
                buffer[offset + 2] = frontR;
                buffer[offset + 3] = surroundL;
                buffer[offset + 4] = surroundR;
                buffer[offset + 5] = rearL ?? surroundL * 0.7f;
                buffer[offset + 6] = rearR ?? surroundR * 0.7f;
                buffer[offset + 7] = lfe;
                // Height channels get attenuated ambience
                buffer[offset + 8] = surroundL * 0.3f;   // Top Front Left
                buffer[offset + 9] = surroundR * 0.3f;   // Top Front Right
                buffer[offset + 10] = (rearL ?? surroundL * 0.5f) * 0.3f;  // Top Rear Left
                buffer[offset + 11] = (rearR ?? surroundR * 0.5f) * 0.3f;  // Top Rear Right
                break;
        }
    }

    #region Presets

    /// <summary>
    /// Creates a preset for music upmixing with wide surround.
    /// </summary>
    public static SurroundUpmixer CreateMusicPreset(ISampleProvider stereoSource, SurroundFormat format)
    {
        var upmixer = new SurroundUpmixer(stereoSource, format);
        upmixer.Algorithm = UpmixAlgorithm.Advanced;
        upmixer.CenterLevel = 0.5f;
        upmixer.SurroundLevel = 0.7f;
        upmixer.LFELevel = 0.6f;
        upmixer.Decorrelation = 0.6f;
        upmixer.CenterWidth = 0.5f;
        return upmixer;
    }

    /// <summary>
    /// Creates a preset for movie/dialogue content.
    /// </summary>
    public static SurroundUpmixer CreateMoviePreset(ISampleProvider stereoSource, SurroundFormat format)
    {
        var upmixer = new SurroundUpmixer(stereoSource, format);
        upmixer.Algorithm = UpmixAlgorithm.Matrix;
        upmixer.CenterLevel = 0.9f;
        upmixer.SurroundLevel = 0.5f;
        upmixer.LFELevel = 0.7f;
        upmixer.Decorrelation = 0.4f;
        upmixer.CenterWidth = 0.8f;
        return upmixer;
    }

    /// <summary>
    /// Creates a preset for ambient/electronic music.
    /// </summary>
    public static SurroundUpmixer CreateAmbientPreset(ISampleProvider stereoSource, SurroundFormat format)
    {
        var upmixer = new SurroundUpmixer(stereoSource, format);
        upmixer.Algorithm = UpmixAlgorithm.Advanced;
        upmixer.CenterLevel = 0.3f;
        upmixer.SurroundLevel = 0.9f;
        upmixer.LFELevel = 0.5f;
        upmixer.Decorrelation = 0.8f;
        upmixer.CenterWidth = 0.3f;
        return upmixer;
    }

    #endregion
}
