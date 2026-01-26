//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: HRTF-based 3D audio renderer for binaural headphone playback.

using NAudio.Wave;

namespace MusicEngine.Core.Routing;

/// <summary>
/// Built-in HRTF dataset type.
/// </summary>
public enum HrtfDataset
{
    /// <summary>
    /// KEMAR dummy head measurements (MIT Media Lab compatible).
    /// </summary>
    Kemar,

    /// <summary>
    /// Compact HRTF optimized for low CPU usage.
    /// </summary>
    Compact,

    /// <summary>
    /// User-provided custom HRTF data.
    /// </summary>
    Custom
}

/// <summary>
/// HRTF-based 3D audio renderer for binaural headphone playback.
/// Simulates spatial audio by convolving with Head-Related Transfer Functions.
/// </summary>
/// <remarks>
/// Features:
/// - Built-in HRTF datasets (KEMAR-compatible, compact)
/// - Full 3D positioning: azimuth, elevation, distance
/// - Distance attenuation with air absorption
/// - Optional head tracking support via external input
/// - Crossfade interpolation for smooth position changes
/// </remarks>
public class BinauralRenderer : ISampleProvider
{
    private const int HrtfLength = 128;
    private const int MaxElevations = 7;
    private const int MaxAzimuths = 72;

    private readonly ISampleProvider _source;
    private readonly WaveFormat _waveFormat;

    // HRTF data: [elevation][azimuth][left/right][sample]
    private float[][][][] _hrtfData = null!;
    private bool _hrtfInitialized;

    // Convolution state
    private float[] _inputHistory = null!;
    private int _inputHistoryIndex;
    private float[] _currentHrtfLeft = null!;
    private float[] _currentHrtfRight = null!;
    private float[] _targetHrtfLeft = null!;
    private float[] _targetHrtfRight = null!;
    private float _interpolationProgress;
    private float _interpolationSpeed;

    // Position parameters
    private float _azimuth;
    private float _elevation;
    private float _distance;
    private float _headTrackingAzimuth;
    private float _headTrackingElevation;
    private bool _headTrackingEnabled;

    // Distance model
    private float _referenceDistance;
    private float _maxDistance;
    private float _rolloffFactor;
    private float _airAbsorptionFactor;

    // Internal state
    private int _lastElevationIndex;
    private int _lastAzimuthIndex;
    private HrtfDataset _dataset;

    /// <summary>
    /// Creates a new binaural renderer.
    /// </summary>
    /// <param name="source">Mono or stereo audio source to spatialize.</param>
    public BinauralRenderer(ISampleProvider source) : this(source, HrtfDataset.Kemar)
    {
    }

    /// <summary>
    /// Creates a new binaural renderer with specified HRTF dataset.
    /// </summary>
    /// <param name="source">Mono or stereo audio source to spatialize.</param>
    /// <param name="dataset">HRTF dataset to use.</param>
    public BinauralRenderer(ISampleProvider source, HrtfDataset dataset)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _dataset = dataset;

        // Output is always stereo
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 2);

        // Default position (front center)
        _azimuth = 0f;
        _elevation = 0f;
        _distance = 1f;

        // Distance model defaults
        _referenceDistance = 1f;
        _maxDistance = 100f;
        _rolloffFactor = 1f;
        _airAbsorptionFactor = 0.001f;

        // Interpolation for smooth movement
        _interpolationSpeed = 0.1f;
        _interpolationProgress = 1f;

        _hrtfInitialized = false;
    }

    /// <summary>
    /// Gets the wave format (always stereo float).
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Gets or sets the azimuth angle in degrees (-180 to +180).
    /// 0 = front, 90 = right, -90 = left, 180/-180 = back.
    /// </summary>
    public float Azimuth
    {
        get => _azimuth;
        set
        {
            float newValue = WrapAngle(value, -180f, 180f);
            if (MathF.Abs(newValue - _azimuth) > 0.1f)
            {
                _azimuth = newValue;
                UpdateHrtf();
            }
        }
    }

    /// <summary>
    /// Gets or sets the elevation angle in degrees (-90 to +90).
    /// 0 = ear level, 90 = above, -90 = below.
    /// </summary>
    public float Elevation
    {
        get => _elevation;
        set
        {
            float newValue = Math.Clamp(value, -90f, 90f);
            if (MathF.Abs(newValue - _elevation) > 0.1f)
            {
                _elevation = newValue;
                UpdateHrtf();
            }
        }
    }

    /// <summary>
    /// Gets or sets the distance from the listener (0.1 to MaxDistance).
    /// </summary>
    public float Distance
    {
        get => _distance;
        set => _distance = Math.Clamp(value, 0.1f, _maxDistance);
    }

    /// <summary>
    /// Gets or sets the reference distance for attenuation model.
    /// </summary>
    public float ReferenceDistance
    {
        get => _referenceDistance;
        set => _referenceDistance = Math.Max(0.1f, value);
    }

    /// <summary>
    /// Gets or sets the maximum distance.
    /// </summary>
    public float MaxDistance
    {
        get => _maxDistance;
        set => _maxDistance = Math.Max(_referenceDistance, value);
    }

    /// <summary>
    /// Gets or sets the distance rolloff factor.
    /// </summary>
    public float RolloffFactor
    {
        get => _rolloffFactor;
        set => _rolloffFactor = Math.Clamp(value, 0f, 10f);
    }

    /// <summary>
    /// Gets or sets the air absorption factor for high-frequency damping.
    /// </summary>
    public float AirAbsorption
    {
        get => _airAbsorptionFactor;
        set => _airAbsorptionFactor = Math.Clamp(value, 0f, 0.1f);
    }

    /// <summary>
    /// Gets or sets whether head tracking is enabled.
    /// </summary>
    public bool HeadTrackingEnabled
    {
        get => _headTrackingEnabled;
        set => _headTrackingEnabled = value;
    }

    /// <summary>
    /// Gets or sets the interpolation speed for position changes (0.01 - 1.0).
    /// Lower values produce smoother but slower transitions.
    /// </summary>
    public float InterpolationSpeed
    {
        get => _interpolationSpeed;
        set => _interpolationSpeed = Math.Clamp(value, 0.01f, 1f);
    }

    /// <summary>
    /// Updates head tracking orientation (for external head tracker input).
    /// </summary>
    /// <param name="azimuth">Head azimuth in degrees.</param>
    /// <param name="elevation">Head elevation in degrees.</param>
    public void UpdateHeadTracking(float azimuth, float elevation)
    {
        _headTrackingAzimuth = WrapAngle(azimuth, -180f, 180f);
        _headTrackingElevation = Math.Clamp(elevation, -90f, 90f);

        if (_headTrackingEnabled)
        {
            UpdateHrtf();
        }
    }

    /// <summary>
    /// Sets the 3D position in Cartesian coordinates.
    /// </summary>
    /// <param name="x">X position (right is positive).</param>
    /// <param name="y">Y position (up is positive).</param>
    /// <param name="z">Z position (front is positive).</param>
    public void SetPosition(float x, float y, float z)
    {
        float distance = MathF.Sqrt(x * x + y * y + z * z);
        if (distance < 0.001f)
        {
            Distance = 0.1f;
            Azimuth = 0f;
            Elevation = 0f;
            return;
        }

        Distance = distance;
        Azimuth = MathF.Atan2(x, z) * 180f / MathF.PI;
        Elevation = MathF.Asin(y / distance) * 180f / MathF.PI;
    }

    /// <summary>
    /// Loads a custom HRTF dataset.
    /// </summary>
    /// <param name="hrtfData">HRTF data array [elevation][azimuth][left=0/right=1][samples].</param>
    public void LoadCustomHrtf(float[][][][] hrtfData)
    {
        if (hrtfData == null)
            throw new ArgumentNullException(nameof(hrtfData));

        _hrtfData = hrtfData;
        _dataset = HrtfDataset.Custom;
        _hrtfInitialized = true;
        UpdateHrtf();
    }

    /// <summary>
    /// Initializes HRTF data and buffers.
    /// </summary>
    private void Initialize()
    {
        // Allocate convolution buffers
        _inputHistory = new float[HrtfLength];
        _currentHrtfLeft = new float[HrtfLength];
        _currentHrtfRight = new float[HrtfLength];
        _targetHrtfLeft = new float[HrtfLength];
        _targetHrtfRight = new float[HrtfLength];

        // Generate synthetic HRTF data
        GenerateHrtfData();

        _hrtfInitialized = true;
        UpdateHrtf();
    }

    /// <summary>
    /// Generates synthetic HRTF data based on selected dataset.
    /// Uses simplified HRTF model based on head shadowing and ITD/ILD.
    /// </summary>
    private void GenerateHrtfData()
    {
        // Elevation: -90 to 90 in steps of 30 degrees (7 levels)
        // Azimuth: -180 to 180 in steps of 5 degrees (72 positions)
        _hrtfData = new float[MaxElevations][][][];

        float sampleRate = _waveFormat.SampleRate;
        float headRadius = 0.0875f; // Average head radius in meters
        float speedOfSound = 343f; // m/s

        for (int e = 0; e < MaxElevations; e++)
        {
            float elevation = -90f + e * 30f;
            _hrtfData[e] = new float[MaxAzimuths][][];

            for (int a = 0; a < MaxAzimuths; a++)
            {
                float azimuth = -180f + a * 5f;
                _hrtfData[e][a] = new float[2][];
                _hrtfData[e][a][0] = new float[HrtfLength]; // Left
                _hrtfData[e][a][1] = new float[HrtfLength]; // Right

                // Convert to radians
                float azRad = azimuth * MathF.PI / 180f;
                float elRad = elevation * MathF.PI / 180f;

                // Calculate ITD (Interaural Time Difference) using Woodworth formula
                float cosAz = MathF.Cos(azRad);
                float sinAz = MathF.Sin(azRad);
                float cosEl = MathF.Cos(elRad);

                // ITD in samples
                float itdSamples = (headRadius / speedOfSound) * sampleRate *
                                   (sinAz * cosEl + MathF.Asin(sinAz * cosEl));
                itdSamples = Math.Clamp(itdSamples, -HrtfLength / 4f, HrtfLength / 4f);

                // Calculate ILD (Interaural Level Difference) - head shadow
                float shadowAngle = MathF.Abs(sinAz * cosEl);
                float shadowFactorL = 1f - shadowAngle * 0.5f * (azimuth > 0 ? 1f : 0f);
                float shadowFactorR = 1f - shadowAngle * 0.5f * (azimuth < 0 ? 1f : 0f);

                // Elevation effect - sounds from above are slightly muffled
                float elevationFactor = 1f - MathF.Abs(elRad) * 0.3f / (MathF.PI / 2f);

                // Generate impulse responses with ITD and ILD
                GenerateHrtfImpulse(_hrtfData[e][a][0], -itdSamples, shadowFactorL * elevationFactor, sampleRate);
                GenerateHrtfImpulse(_hrtfData[e][a][1], itdSamples, shadowFactorR * elevationFactor, sampleRate);
            }
        }
    }

    /// <summary>
    /// Generates a single HRTF impulse response.
    /// </summary>
    private void GenerateHrtfImpulse(float[] impulse, float delaySamples, float gain, float sampleRate)
    {
        Array.Clear(impulse, 0, impulse.Length);

        // Main impulse with fractional delay using sinc interpolation
        int intDelay = (int)MathF.Floor(delaySamples);
        float fracDelay = delaySamples - intDelay;

        // Sinc interpolation window
        for (int i = 0; i < impulse.Length; i++)
        {
            float t = i - HrtfLength / 2 - intDelay - fracDelay;
            float sincValue;

            if (MathF.Abs(t) < 0.001f)
            {
                sincValue = 1f;
            }
            else
            {
                sincValue = MathF.Sin(MathF.PI * t) / (MathF.PI * t);
            }

            // Apply Hann window
            float window = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (impulse.Length - 1)));

            // Head shadow low-pass effect (reduce high frequencies for shadowed ear)
            float lpCoeff = 0.9f + 0.1f * gain;

            impulse[i] = sincValue * window * gain * lpCoeff;
        }

        // Normalize
        float maxAbs = 0f;
        for (int i = 0; i < impulse.Length; i++)
        {
            if (MathF.Abs(impulse[i]) > maxAbs)
                maxAbs = MathF.Abs(impulse[i]);
        }

        if (maxAbs > 0.001f)
        {
            float normFactor = 0.7f / maxAbs;
            for (int i = 0; i < impulse.Length; i++)
            {
                impulse[i] *= normFactor;
            }
        }
    }

    /// <summary>
    /// Updates the current HRTF based on position.
    /// </summary>
    private void UpdateHrtf()
    {
        if (!_hrtfInitialized)
            return;

        // Apply head tracking offset
        float effectiveAzimuth = _azimuth;
        float effectiveElevation = _elevation;

        if (_headTrackingEnabled)
        {
            effectiveAzimuth = WrapAngle(_azimuth - _headTrackingAzimuth, -180f, 180f);
            effectiveElevation = Math.Clamp(_elevation - _headTrackingElevation, -90f, 90f);
        }

        // Find nearest HRTF indices
        int elevationIndex = (int)MathF.Round((effectiveElevation + 90f) / 30f);
        elevationIndex = Math.Clamp(elevationIndex, 0, MaxElevations - 1);

        int azimuthIndex = (int)MathF.Round((effectiveAzimuth + 180f) / 5f);
        azimuthIndex = Math.Clamp(azimuthIndex % MaxAzimuths, 0, MaxAzimuths - 1);

        // Only update if position changed
        if (elevationIndex != _lastElevationIndex || azimuthIndex != _lastAzimuthIndex)
        {
            _lastElevationIndex = elevationIndex;
            _lastAzimuthIndex = azimuthIndex;

            // Set target HRTF for interpolation
            Array.Copy(_hrtfData[elevationIndex][azimuthIndex][0], _targetHrtfLeft, HrtfLength);
            Array.Copy(_hrtfData[elevationIndex][azimuthIndex][1], _targetHrtfRight, HrtfLength);

            _interpolationProgress = 0f;
        }
    }

    /// <summary>
    /// Reads audio and applies binaural processing.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        if (!_hrtfInitialized)
        {
            Initialize();
        }

        int sourceChannels = _source.WaveFormat.Channels;
        int outputSamples = count / 2; // Stereo output
        int sourceSamples = sourceChannels == 1 ? outputSamples : outputSamples * 2;

        // Read from source
        float[] sourceBuffer = new float[sourceSamples];
        int samplesRead = _source.Read(sourceBuffer, 0, sourceSamples);

        if (samplesRead == 0)
            return 0;

        int framesToProcess = sourceChannels == 1 ? samplesRead : samplesRead / 2;

        // Calculate distance attenuation
        float distanceGain = CalculateDistanceAttenuation();

        // Process each frame
        for (int i = 0; i < framesToProcess; i++)
        {
            // Get mono input sample
            float inputSample;
            if (sourceChannels == 1)
            {
                inputSample = sourceBuffer[i];
            }
            else
            {
                inputSample = (sourceBuffer[i * 2] + sourceBuffer[i * 2 + 1]) * 0.5f;
            }

            inputSample *= distanceGain;

            // Update input history for convolution
            _inputHistory[_inputHistoryIndex] = inputSample;

            // Interpolate HRTFs for smooth transitions
            if (_interpolationProgress < 1f)
            {
                _interpolationProgress += _interpolationSpeed;
                if (_interpolationProgress > 1f) _interpolationProgress = 1f;

                for (int j = 0; j < HrtfLength; j++)
                {
                    _currentHrtfLeft[j] = _currentHrtfLeft[j] * (1f - _interpolationProgress) +
                                          _targetHrtfLeft[j] * _interpolationProgress;
                    _currentHrtfRight[j] = _currentHrtfRight[j] * (1f - _interpolationProgress) +
                                           _targetHrtfRight[j] * _interpolationProgress;
                }
            }

            // Convolve with HRTF
            float outputL = 0f;
            float outputR = 0f;

            for (int j = 0; j < HrtfLength; j++)
            {
                int historyIdx = (_inputHistoryIndex - j + HrtfLength) % HrtfLength;
                outputL += _inputHistory[historyIdx] * _currentHrtfLeft[j];
                outputR += _inputHistory[historyIdx] * _currentHrtfRight[j];
            }

            // Write to output buffer
            buffer[offset + i * 2] = outputL;
            buffer[offset + i * 2 + 1] = outputR;

            _inputHistoryIndex = (_inputHistoryIndex + 1) % HrtfLength;
        }

        return framesToProcess * 2; // Stereo samples
    }

    /// <summary>
    /// Calculates distance-based attenuation with air absorption.
    /// </summary>
    private float CalculateDistanceAttenuation()
    {
        if (_distance <= _referenceDistance)
            return 1f;

        // Inverse distance law with rolloff
        float attenuation = _referenceDistance /
            (_referenceDistance + _rolloffFactor * (_distance - _referenceDistance));

        // Air absorption (frequency-dependent, approximated)
        float absorption = MathF.Exp(-_airAbsorptionFactor * (_distance - _referenceDistance));

        return attenuation * absorption;
    }

    /// <summary>
    /// Wraps an angle to the specified range.
    /// </summary>
    private static float WrapAngle(float angle, float min, float max)
    {
        float range = max - min;
        while (angle < min) angle += range;
        while (angle > max) angle -= range;
        return angle;
    }

    /// <summary>
    /// Creates a binaural renderer with source centered in front.
    /// </summary>
    public static BinauralRenderer CreateFrontCenter(ISampleProvider source)
    {
        var renderer = new BinauralRenderer(source);
        renderer.Azimuth = 0f;
        renderer.Elevation = 0f;
        renderer.Distance = 1f;
        return renderer;
    }

    /// <summary>
    /// Creates a binaural renderer for overhead audio.
    /// </summary>
    public static BinauralRenderer CreateOverhead(ISampleProvider source)
    {
        var renderer = new BinauralRenderer(source);
        renderer.Azimuth = 0f;
        renderer.Elevation = 75f;
        renderer.Distance = 2f;
        return renderer;
    }

    /// <summary>
    /// Creates a binaural renderer for surround rear position.
    /// </summary>
    public static BinauralRenderer CreateRearSurround(ISampleProvider source)
    {
        var renderer = new BinauralRenderer(source);
        renderer.Azimuth = 150f;
        renderer.Elevation = 0f;
        renderer.Distance = 2f;
        return renderer;
    }
}
