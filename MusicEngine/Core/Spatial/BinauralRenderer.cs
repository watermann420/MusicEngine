// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;

namespace MusicEngine.Core.Spatial;

/// <summary>
/// HRTF dataset type.
/// </summary>
public enum HrtfDataset
{
    /// <summary>Built-in compact HRTF (fast, reasonable quality)</summary>
    BuiltIn,
    /// <summary>MIT KEMAR dataset (if loaded externally)</summary>
    MitKemar,
    /// <summary>CIPIC dataset (if loaded externally)</summary>
    Cipic,
    /// <summary>Custom user-provided HRTF</summary>
    Custom
}

/// <summary>
/// HRTF-based binaural renderer for realistic 3D audio perception over headphones.
/// </summary>
public class BinauralRenderer : IDisposable
{
    private readonly int _sampleRate;
    private HrtfDataset _dataset = HrtfDataset.BuiltIn;

    // HRTF filter length
    private const int HrtfLength = 128;

    // Built-in HRTF data (simplified, compact dataset)
    // Real HRTF would have many more positions and longer impulse responses
    private readonly float[][,] _hrtfData; // [positionIndex][channel, sample]
    private readonly (float Azimuth, float Elevation)[] _hrtfPositions;

    // Convolution state
    private readonly float[] _inputHistory;
    private int _historyIndex;

    // Interpolated HRTF
    private float[] _currentHrtfLeft;
    private float[] _currentHrtfRight;
    private float _currentAzimuth;
    private float _currentElevation;
    private bool _hrtfDirty = true;

    // ITD (Interaural Time Difference) delay line
    private readonly float[] _itdDelayLeft;
    private readonly float[] _itdDelayRight;
    private int _itdDelayIndexLeft;
    private int _itdDelayIndexRight;
    private const int MaxItdSamples = 44; // ~1ms at 44.1kHz (max ITD for humans)

    // ILD (Interaural Level Difference) parameters
    private float _ildGainLeft = 1f;
    private float _ildGainRight = 1f;

    // Crossfade for smooth HRTF transitions
    private readonly float[] _previousHrtfLeft;
    private readonly float[] _previousHrtfRight;
    private int _crossfadeCounter;
    private const int CrossfadeSamples = 64;

    // Head shadow low-pass filter state
    private float _headShadowStateLeft;
    private float _headShadowStateRight;

    /// <summary>
    /// Gets or sets the HRTF dataset to use.
    /// </summary>
    public HrtfDataset Dataset
    {
        get => _dataset;
        set
        {
            _dataset = value;
            _hrtfDirty = true;
        }
    }

    /// <summary>
    /// Head radius in meters (affects ITD calculation).
    /// Default is 0.0875m (average adult head radius).
    /// </summary>
    public float HeadRadius { get; set; } = 0.0875f;

    /// <summary>
    /// Speed of sound in meters per second.
    /// </summary>
    public float SpeedOfSound { get; set; } = 343f;

    /// <summary>
    /// Enable/disable ITD processing.
    /// </summary>
    public bool ItdEnabled { get; set; } = true;

    /// <summary>
    /// Enable/disable ILD processing.
    /// </summary>
    public bool IldEnabled { get; set; } = true;

    /// <summary>
    /// Enable/disable head shadow filtering.
    /// </summary>
    public bool HeadShadowEnabled { get; set; } = true;

    /// <summary>
    /// Creates a new binaural renderer.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate</param>
    public BinauralRenderer(int sampleRate)
    {
        _sampleRate = sampleRate;

        // Initialize HRTF storage
        _currentHrtfLeft = new float[HrtfLength];
        _currentHrtfRight = new float[HrtfLength];
        _previousHrtfLeft = new float[HrtfLength];
        _previousHrtfRight = new float[HrtfLength];

        // Initialize convolution history
        _inputHistory = new float[HrtfLength];

        // Initialize ITD delay lines
        _itdDelayLeft = new float[MaxItdSamples * 2];
        _itdDelayRight = new float[MaxItdSamples * 2];

        // Build the built-in HRTF dataset
        (_hrtfPositions, _hrtfData) = BuildBuiltInHrtf();
    }

    /// <summary>
    /// Processes a mono sample and returns binaural stereo output.
    /// </summary>
    /// <param name="monoInput">The mono input sample</param>
    /// <param name="azimuth">Azimuth angle in degrees (0=front, 90=right, -90=left, 180=back)</param>
    /// <param name="elevation">Elevation angle in degrees (0=ear level, 90=above, -90=below)</param>
    /// <returns>Tuple of (left, right) output samples</returns>
    public (float Left, float Right) Process(float monoInput, float azimuth, float elevation)
    {
        // Update HRTF if position changed significantly
        if (_hrtfDirty || MathF.Abs(azimuth - _currentAzimuth) > 1f || MathF.Abs(elevation - _currentElevation) > 1f)
        {
            UpdateHrtf(azimuth, elevation);
        }

        // Add input to history buffer
        _inputHistory[_historyIndex] = monoInput;

        // Convolve with HRTF
        float leftSample = 0f;
        float rightSample = 0f;

        // Apply crossfade if transitioning
        float crossfadeMix = _crossfadeCounter > 0 ? (float)_crossfadeCounter / CrossfadeSamples : 0f;

        for (int i = 0; i < HrtfLength; i++)
        {
            int histIdx = (_historyIndex - i + HrtfLength) % HrtfLength;
            float input = _inputHistory[histIdx];

            if (crossfadeMix > 0f)
            {
                // Crossfade between previous and current HRTF
                leftSample += input * (_currentHrtfLeft[i] * (1f - crossfadeMix) + _previousHrtfLeft[i] * crossfadeMix);
                rightSample += input * (_currentHrtfRight[i] * (1f - crossfadeMix) + _previousHrtfRight[i] * crossfadeMix);
            }
            else
            {
                leftSample += input * _currentHrtfLeft[i];
                rightSample += input * _currentHrtfRight[i];
            }
        }

        _historyIndex = (_historyIndex + 1) % HrtfLength;
        if (_crossfadeCounter > 0) _crossfadeCounter--;

        // Apply ITD (Interaural Time Difference)
        if (ItdEnabled)
        {
            (leftSample, rightSample) = ApplyItd(leftSample, rightSample, azimuth);
        }

        // Apply ILD (Interaural Level Difference)
        if (IldEnabled)
        {
            leftSample *= _ildGainLeft;
            rightSample *= _ildGainRight;
        }

        // Apply head shadow filtering
        if (HeadShadowEnabled)
        {
            (leftSample, rightSample) = ApplyHeadShadow(leftSample, rightSample, azimuth);
        }

        return (leftSample, rightSample);
    }

    /// <summary>
    /// Processes a buffer of mono samples.
    /// </summary>
    public void ProcessBuffer(float[] monoInput, float[] stereoOutput, int sampleCount, float azimuth, float elevation)
    {
        for (int i = 0; i < sampleCount; i++)
        {
            var (left, right) = Process(monoInput[i], azimuth, elevation);
            stereoOutput[i * 2] = left;
            stereoOutput[i * 2 + 1] = right;
        }
    }

    /// <summary>
    /// Updates the HRTF for a new position.
    /// </summary>
    private void UpdateHrtf(float azimuth, float elevation)
    {
        // Save previous HRTF for crossfade
        Array.Copy(_currentHrtfLeft, _previousHrtfLeft, HrtfLength);
        Array.Copy(_currentHrtfRight, _previousHrtfRight, HrtfLength);
        _crossfadeCounter = CrossfadeSamples;

        // Normalize azimuth to [-180, 180)
        while (azimuth >= 180f) azimuth -= 360f;
        while (azimuth < -180f) azimuth += 360f;

        // Clamp elevation
        elevation = Math.Clamp(elevation, -90f, 90f);

        // Find closest HRTF positions and interpolate
        int closest1 = 0, closest2 = 0;
        float minDist1 = float.MaxValue, minDist2 = float.MaxValue;

        for (int i = 0; i < _hrtfPositions.Length; i++)
        {
            float dist = AngularDistance(azimuth, elevation, _hrtfPositions[i].Azimuth, _hrtfPositions[i].Elevation);

            if (dist < minDist1)
            {
                minDist2 = minDist1;
                closest2 = closest1;
                minDist1 = dist;
                closest1 = i;
            }
            else if (dist < minDist2)
            {
                minDist2 = dist;
                closest2 = i;
            }
        }

        // Interpolate between two closest positions
        float totalDist = minDist1 + minDist2;
        float weight1 = totalDist > 0.001f ? 1f - (minDist1 / totalDist) : 1f;
        float weight2 = 1f - weight1;

        for (int i = 0; i < HrtfLength; i++)
        {
            _currentHrtfLeft[i] = _hrtfData[closest1][0, i] * weight1 + _hrtfData[closest2][0, i] * weight2;
            _currentHrtfRight[i] = _hrtfData[closest1][1, i] * weight1 + _hrtfData[closest2][1, i] * weight2;
        }

        // Calculate ILD gains based on azimuth
        CalculateIld(azimuth);

        _currentAzimuth = azimuth;
        _currentElevation = elevation;
        _hrtfDirty = false;
    }

    /// <summary>
    /// Calculates angular distance between two positions.
    /// </summary>
    private float AngularDistance(float az1, float el1, float az2, float el2)
    {
        // Use great circle distance on unit sphere
        float azRad1 = az1 * MathF.PI / 180f;
        float elRad1 = el1 * MathF.PI / 180f;
        float azRad2 = az2 * MathF.PI / 180f;
        float elRad2 = el2 * MathF.PI / 180f;

        float cosEl1 = MathF.Cos(elRad1);
        float cosEl2 = MathF.Cos(elRad2);

        float x1 = cosEl1 * MathF.Sin(azRad1);
        float y1 = cosEl1 * MathF.Cos(azRad1);
        float z1 = MathF.Sin(elRad1);

        float x2 = cosEl2 * MathF.Sin(azRad2);
        float y2 = cosEl2 * MathF.Cos(azRad2);
        float z2 = MathF.Sin(elRad2);

        float dot = x1 * x2 + y1 * y2 + z1 * z2;
        dot = Math.Clamp(dot, -1f, 1f);

        return MathF.Acos(dot) * 180f / MathF.PI;
    }

    /// <summary>
    /// Calculates ILD (Interaural Level Difference) based on azimuth.
    /// </summary>
    private void CalculateIld(float azimuth)
    {
        // ILD increases with azimuth angle (sounds from the side are louder in near ear)
        // Typical ILD is 0-20 dB depending on frequency
        float azRad = azimuth * MathF.PI / 180f;

        // Simple model: use sine of azimuth for lateral attenuation
        float lateralFactor = MathF.Sin(azRad);

        // Convert to dB difference (max ~6dB for broadband)
        float ildDb = lateralFactor * 6f;

        // Convert to linear gains
        _ildGainLeft = MathF.Pow(10f, -ildDb / 20f);  // Attenuate left for right sources
        _ildGainRight = MathF.Pow(10f, ildDb / 20f);  // Attenuate right for left sources
    }

    /// <summary>
    /// Applies ITD (Interaural Time Difference) delay.
    /// </summary>
    private (float Left, float Right) ApplyItd(float left, float right, float azimuth)
    {
        // Calculate ITD based on Woodworth formula:
        // ITD = (r/c) * (sin(theta) + theta)
        // where r = head radius, c = speed of sound, theta = azimuth in radians

        float azRad = MathF.Abs(azimuth) * MathF.PI / 180f;
        float itdSeconds = (HeadRadius / SpeedOfSound) * (MathF.Sin(azRad) + azRad);
        int itdSamples = (int)(itdSeconds * _sampleRate);
        itdSamples = Math.Clamp(itdSamples, 0, MaxItdSamples - 1);

        // Determine which ear gets delayed
        float delayedLeft, delayedRight;

        if (azimuth > 0) // Sound from right, delay left ear
        {
            // Write to left delay line
            _itdDelayLeft[_itdDelayIndexLeft] = left;
            delayedLeft = _itdDelayLeft[(_itdDelayIndexLeft - itdSamples + _itdDelayLeft.Length) % _itdDelayLeft.Length];
            _itdDelayIndexLeft = (_itdDelayIndexLeft + 1) % _itdDelayLeft.Length;

            delayedRight = right;
        }
        else // Sound from left, delay right ear
        {
            // Write to right delay line
            _itdDelayRight[_itdDelayIndexRight] = right;
            delayedRight = _itdDelayRight[(_itdDelayIndexRight - itdSamples + _itdDelayRight.Length) % _itdDelayRight.Length];
            _itdDelayIndexRight = (_itdDelayIndexRight + 1) % _itdDelayRight.Length;

            delayedLeft = left;
        }

        return (delayedLeft, delayedRight);
    }

    /// <summary>
    /// Applies head shadow filtering (high frequency attenuation for shadowed ear).
    /// </summary>
    private (float Left, float Right) ApplyHeadShadow(float left, float right, float azimuth)
    {
        // Head shadow primarily affects high frequencies
        // Use a simple low-pass filter with cutoff based on azimuth

        float azRad = MathF.Abs(azimuth) * MathF.PI / 180f;
        float shadowAmount = MathF.Sin(azRad); // 0 at front, 1 at side

        // Calculate filter coefficients (simple first-order low-pass)
        // Cutoff varies from 20kHz (no shadow) to ~2kHz (full shadow)
        float minCutoff = 2000f;
        float maxCutoff = 20000f;
        float cutoff = maxCutoff - shadowAmount * (maxCutoff - minCutoff);
        float rc = 1f / (2f * MathF.PI * cutoff);
        float dt = 1f / _sampleRate;
        float alpha = dt / (rc + dt);

        // Apply filter to shadowed ear
        if (azimuth > 0) // Right source, shadow left ear
        {
            _headShadowStateLeft = _headShadowStateLeft + alpha * (left - _headShadowStateLeft);
            left = left * (1f - shadowAmount * 0.5f) + _headShadowStateLeft * shadowAmount * 0.5f;
        }
        else // Left source, shadow right ear
        {
            _headShadowStateRight = _headShadowStateRight + alpha * (right - _headShadowStateRight);
            right = right * (1f - shadowAmount * 0.5f) + _headShadowStateRight * shadowAmount * 0.5f;
        }

        return (left, right);
    }

    /// <summary>
    /// Builds a compact built-in HRTF dataset.
    /// This is a simplified HRTF for demonstration - real applications should use
    /// measured HRTF datasets like MIT KEMAR, CIPIC, or SOFA format files.
    /// </summary>
    private ((float Azimuth, float Elevation)[] Positions, float[][,] Data) BuildBuiltInHrtf()
    {
        // Create HRTF data for key positions
        var positions = new (float Azimuth, float Elevation)[]
        {
            // Horizontal plane (elevation = 0)
            (0f, 0f),     // Front
            (30f, 0f),    // Front-right
            (60f, 0f),
            (90f, 0f),    // Right
            (120f, 0f),
            (150f, 0f),
            (180f, 0f),   // Back
            (-150f, 0f),
            (-120f, 0f),
            (-90f, 0f),   // Left
            (-60f, 0f),
            (-30f, 0f),
            // Elevated positions
            (0f, 30f),    // Front-up
            (90f, 30f),   // Right-up
            (180f, 30f),  // Back-up
            (-90f, 30f),  // Left-up
            (0f, -30f),   // Front-down
            (90f, -30f),  // Right-down
            // Top and bottom
            (0f, 90f),    // Above
            (0f, -90f),   // Below
        };

        var data = new float[positions.Length][,];

        for (int i = 0; i < positions.Length; i++)
        {
            data[i] = GenerateSimplifiedHrtf(positions[i].Azimuth, positions[i].Elevation);
        }

        return (positions, data);
    }

    /// <summary>
    /// Generates a simplified HRTF impulse response for a given position.
    /// This is a synthetic approximation - real HRTF data should be measured.
    /// </summary>
    private float[,] GenerateSimplifiedHrtf(float azimuth, float elevation)
    {
        var hrtf = new float[2, HrtfLength];

        // Convert to radians
        float azRad = azimuth * MathF.PI / 180f;
        float elRad = elevation * MathF.PI / 180f;

        // Calculate ITD in samples for impulse positioning
        float itd = (HeadRadius / SpeedOfSound) * (MathF.Sin(MathF.Abs(azRad)) + MathF.Abs(azRad));
        int itdSamples = (int)(itd * _sampleRate);

        // Calculate ILD factor
        float ildFactor = MathF.Sin(azRad) * 0.3f;

        // Generate simplified impulse responses
        // Real HRTF would have complex frequency-dependent filtering

        for (int ch = 0; ch < 2; ch++)
        {
            bool isNearEar = (ch == 0 && azimuth < 0) || (ch == 1 && azimuth > 0);
            int earDelay = isNearEar ? 0 : itdSamples;
            float earGain = isNearEar ? 1f + ildFactor : 1f - ildFactor;

            // Main impulse
            int mainPos = Math.Clamp(earDelay + 4, 0, HrtfLength - 1);
            hrtf[ch, mainPos] = earGain * 0.5f;

            // Early reflection (pinna effect simulation)
            int pinnaDelay = mainPos + 8 + (int)(MathF.Cos(elRad) * 4);
            if (pinnaDelay < HrtfLength)
            {
                hrtf[ch, pinnaDelay] = earGain * 0.2f * (1f + elRad / MathF.PI);
            }

            // Add some diffuse energy
            for (int j = mainPos + 16; j < HrtfLength; j += 4)
            {
                float decay = MathF.Exp(-(j - mainPos) / 30f);
                hrtf[ch, j] = earGain * 0.05f * decay * ((j % 8 < 4) ? 1f : -1f);
            }
        }

        // Normalize
        float maxVal = 0f;
        for (int ch = 0; ch < 2; ch++)
        {
            for (int i = 0; i < HrtfLength; i++)
            {
                maxVal = MathF.Max(maxVal, MathF.Abs(hrtf[ch, i]));
            }
        }

        if (maxVal > 0.001f)
        {
            for (int ch = 0; ch < 2; ch++)
            {
                for (int i = 0; i < HrtfLength; i++)
                {
                    hrtf[ch, i] /= maxVal;
                }
            }
        }

        return hrtf;
    }

    /// <summary>
    /// Resets the internal state.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_inputHistory, 0, _inputHistory.Length);
        Array.Clear(_itdDelayLeft, 0, _itdDelayLeft.Length);
        Array.Clear(_itdDelayRight, 0, _itdDelayRight.Length);
        _historyIndex = 0;
        _itdDelayIndexLeft = 0;
        _itdDelayIndexRight = 0;
        _headShadowStateLeft = 0f;
        _headShadowStateRight = 0f;
        _hrtfDirty = true;
    }

    /// <summary>
    /// Disposes of resources.
    /// </summary>
    public void Dispose()
    {
        // No unmanaged resources to dispose
    }
}
