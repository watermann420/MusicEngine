//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: A positionable audio source in 3D space for spatial audio processing.

using System;
using NAudio.Wave;

namespace MusicEngine.Core.Spatial;

/// <summary>
/// Distance attenuation curve types for individual sources.
/// </summary>
public enum AttenuationCurve
{
    /// <summary>Uses the engine's global distance model</summary>
    UseGlobal,
    /// <summary>Linear falloff</summary>
    Linear,
    /// <summary>Logarithmic (natural sound propagation)</summary>
    Logarithmic,
    /// <summary>Inverse distance</summary>
    Inverse,
    /// <summary>Custom curve points</summary>
    Custom
}

/// <summary>
/// Represents a positionable audio source in 3D space.
/// </summary>
public class SpatialSource : ISampleProvider, IDisposable
{
    private readonly ISampleProvider _audioSource;
    private readonly int _sampleRate;

    // 3D Position
    private float _positionX;
    private float _positionY;
    private float _positionZ;

    // Orientation (for directional sources)
    private float _yaw;   // Rotation around vertical axis
    private float _pitch; // Rotation around lateral axis
    private float _roll;  // Rotation around forward axis

    // Distance attenuation
    private float _maxDistance = 100f;
    private float _referenceDistance = 1f;
    private float _rolloffFactor = 1f;
    private AttenuationCurve _attenuationCurve = AttenuationCurve.UseGlobal;

    // Doppler effect
    private bool _dopplerEnabled = false;
    private float _dopplerFactor = 1f;
    private float _previousDistance;
    private float _velocity;

    // Spread/width
    private float _spread = 0f; // 0 = point source, 1 = full spread
    private float _innerConeAngle = 360f;
    private float _outerConeAngle = 360f;
    private float _outerConeGain = 0f;

    // Occlusion/Obstruction
    private float _occlusion = 0f;    // 0 = no occlusion, 1 = full occlusion
    private float _obstruction = 0f;  // 0 = no obstruction, 1 = full obstruction
    private float _occlusionLfRatio = 0.25f; // Low frequency ratio that passes through

    // State
    private bool _enabled = true;
    private float _gain = 1f;

    // Doppler processing
    private float[] _dopplerBuffer = Array.Empty<float>();
    private float _dopplerPhase = 0f;

    // Occlusion filter state
    private float _occlusionFilterState;

    /// <summary>
    /// Creates a new spatial audio source.
    /// </summary>
    /// <param name="audioSource">The underlying audio sample provider</param>
    /// <param name="sampleRate">The audio sample rate</param>
    public SpatialSource(ISampleProvider audioSource, int sampleRate)
    {
        _audioSource = audioSource ?? throw new ArgumentNullException(nameof(audioSource));
        _sampleRate = sampleRate;
    }

    /// <summary>
    /// Gets the wave format of the source.
    /// </summary>
    public WaveFormat WaveFormat => _audioSource.WaveFormat;

    /// <summary>
    /// Gets or sets whether this source is enabled.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>
    /// Gets or sets the source gain (volume).
    /// </summary>
    public float Gain
    {
        get => _gain;
        set => _gain = Math.Max(0f, value);
    }

    #region Position Properties

    /// <summary>
    /// X position (right is positive).
    /// </summary>
    public float PositionX
    {
        get => _positionX;
        set
        {
            UpdateVelocity(value, _positionY, _positionZ);
            _positionX = value;
        }
    }

    /// <summary>
    /// Y position (front is positive).
    /// </summary>
    public float PositionY
    {
        get => _positionY;
        set
        {
            UpdateVelocity(_positionX, value, _positionZ);
            _positionY = value;
        }
    }

    /// <summary>
    /// Z position (up is positive).
    /// </summary>
    public float PositionZ
    {
        get => _positionZ;
        set
        {
            UpdateVelocity(_positionX, _positionY, value);
            _positionZ = value;
        }
    }

    /// <summary>
    /// Sets the 3D position in one call.
    /// </summary>
    public void SetPosition(float x, float y, float z)
    {
        UpdateVelocity(x, y, z);
        _positionX = x;
        _positionY = y;
        _positionZ = z;
    }

    #endregion

    #region Orientation Properties

    /// <summary>
    /// Yaw angle in degrees (rotation around vertical axis, 0 = facing forward).
    /// </summary>
    public float Yaw
    {
        get => _yaw;
        set => _yaw = NormalizeAngle(value);
    }

    /// <summary>
    /// Pitch angle in degrees (rotation around lateral axis, 0 = level).
    /// </summary>
    public float Pitch
    {
        get => _pitch;
        set => _pitch = Math.Clamp(value, -90f, 90f);
    }

    /// <summary>
    /// Roll angle in degrees (rotation around forward axis, 0 = level).
    /// </summary>
    public float Roll
    {
        get => _roll;
        set => _roll = NormalizeAngle(value);
    }

    /// <summary>
    /// Sets the orientation in one call.
    /// </summary>
    public void SetOrientation(float yaw, float pitch, float roll)
    {
        _yaw = NormalizeAngle(yaw);
        _pitch = Math.Clamp(pitch, -90f, 90f);
        _roll = NormalizeAngle(roll);
    }

    #endregion

    #region Distance Attenuation Properties

    /// <summary>
    /// Maximum audible distance (beyond this, gain = 0).
    /// </summary>
    public float MaxDistance
    {
        get => _maxDistance;
        set => _maxDistance = Math.Max(0.1f, value);
    }

    /// <summary>
    /// Reference distance where gain = 1 (full volume).
    /// </summary>
    public float ReferenceDistance
    {
        get => _referenceDistance;
        set => _referenceDistance = Math.Max(0.1f, value);
    }

    /// <summary>
    /// Rolloff factor (higher = faster falloff).
    /// </summary>
    public float RolloffFactor
    {
        get => _rolloffFactor;
        set => _rolloffFactor = Math.Max(0f, value);
    }

    /// <summary>
    /// Distance attenuation curve type.
    /// </summary>
    public AttenuationCurve AttenuationCurve
    {
        get => _attenuationCurve;
        set => _attenuationCurve = value;
    }

    #endregion

    #region Doppler Properties

    /// <summary>
    /// Whether doppler effect is enabled.
    /// </summary>
    public bool DopplerEnabled
    {
        get => _dopplerEnabled;
        set => _dopplerEnabled = value;
    }

    /// <summary>
    /// Doppler effect strength (1 = realistic, higher = exaggerated).
    /// </summary>
    public float DopplerFactor
    {
        get => _dopplerFactor;
        set => _dopplerFactor = Math.Clamp(value, 0f, 10f);
    }

    /// <summary>
    /// Current velocity of the source (calculated from position changes).
    /// </summary>
    public float Velocity => _velocity;

    #endregion

    #region Spread and Cone Properties

    /// <summary>
    /// Source spread (0 = point source, 1 = omnidirectional spread).
    /// </summary>
    public float Spread
    {
        get => _spread;
        set => _spread = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Inner cone angle in degrees (full gain within this cone).
    /// </summary>
    public float InnerConeAngle
    {
        get => _innerConeAngle;
        set => _innerConeAngle = Math.Clamp(value, 0f, 360f);
    }

    /// <summary>
    /// Outer cone angle in degrees (gain transitions to outer cone gain).
    /// </summary>
    public float OuterConeAngle
    {
        get => _outerConeAngle;
        set => _outerConeAngle = Math.Clamp(value, _innerConeAngle, 360f);
    }

    /// <summary>
    /// Gain outside the outer cone (0-1).
    /// </summary>
    public float OuterConeGain
    {
        get => _outerConeGain;
        set => _outerConeGain = Math.Clamp(value, 0f, 1f);
    }

    #endregion

    #region Occlusion Properties

    /// <summary>
    /// Occlusion amount (0 = none, 1 = full). Simulates sound passing through obstacles.
    /// </summary>
    public float Occlusion
    {
        get => _occlusion;
        set => _occlusion = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Obstruction amount (0 = none, 1 = full). Simulates direct path blockage.
    /// </summary>
    public float Obstruction
    {
        get => _obstruction;
        set => _obstruction = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Ratio of low frequencies that pass through during occlusion (0-1).
    /// </summary>
    public float OcclusionLfRatio
    {
        get => _occlusionLfRatio;
        set => _occlusionLfRatio = Math.Clamp(value, 0f, 1f);
    }

    #endregion

    /// <summary>
    /// Reads audio samples from the source with spatial processing.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        if (!_enabled)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        // Read from underlying source
        int samplesRead = _audioSource.Read(buffer, offset, count);
        if (samplesRead == 0) return 0;

        // Apply gain
        if (Math.Abs(_gain - 1f) > 0.0001f)
        {
            for (int i = 0; i < samplesRead; i++)
            {
                buffer[offset + i] *= _gain;
            }
        }

        // Apply occlusion filtering
        if (_occlusion > 0.001f || _obstruction > 0.001f)
        {
            ApplyOcclusionFilter(buffer, offset, samplesRead);
        }

        // Apply doppler effect
        if (_dopplerEnabled && Math.Abs(_velocity) > 0.001f)
        {
            ApplyDopplerEffect(buffer, offset, samplesRead);
        }

        return samplesRead;
    }

    /// <summary>
    /// Updates velocity calculation when position changes.
    /// </summary>
    private void UpdateVelocity(float newX, float newY, float newZ)
    {
        float newDistance = MathF.Sqrt(newX * newX + newY * newY + newZ * newZ);
        _velocity = newDistance - _previousDistance;
        _previousDistance = newDistance;
    }

    /// <summary>
    /// Applies occlusion low-pass filtering.
    /// </summary>
    private void ApplyOcclusionFilter(float[] buffer, int offset, int count)
    {
        // Combined occlusion/obstruction factor
        float totalOcclusion = Math.Max(_occlusion, _obstruction);

        // Calculate low-pass cutoff (lower occlusion = higher cutoff)
        float cutoffFreq = 20000f * (1f - totalOcclusion * 0.9f); // Range: 2000-20000 Hz
        float rc = 1f / (2f * MathF.PI * cutoffFreq);
        float dt = 1f / _sampleRate;
        float alpha = dt / (rc + dt);

        // Volume reduction based on occlusion
        float volumeReduction = 1f - (totalOcclusion * (1f - _occlusionLfRatio));

        int channels = WaveFormat.Channels;
        for (int i = 0; i < count; i++)
        {
            float sample = buffer[offset + i];

            // Apply low-pass filter
            _occlusionFilterState = _occlusionFilterState + alpha * (sample - _occlusionFilterState);

            // Blend between filtered and original based on LF ratio
            float filteredSample = _occlusionFilterState * volumeReduction;

            buffer[offset + i] = filteredSample;
        }
    }

    /// <summary>
    /// Applies doppler pitch shifting effect.
    /// </summary>
    private void ApplyDopplerEffect(float[] buffer, int offset, int count)
    {
        // Speed of sound in units per second (assuming meters)
        const float speedOfSound = 343f;

        // Calculate doppler shift ratio
        // If velocity is negative (approaching), pitch increases
        // If velocity is positive (receding), pitch decreases
        float dopplerRatio = speedOfSound / (speedOfSound + _velocity * _dopplerFactor * 100f);
        dopplerRatio = Math.Clamp(dopplerRatio, 0.5f, 2f); // Limit to reasonable range

        if (Math.Abs(dopplerRatio - 1f) < 0.001f) return; // No significant shift

        int channels = WaveFormat.Channels;

        // Simple sample-rate conversion for pitch shift
        EnsureBufferSize(ref _dopplerBuffer, count);
        Array.Copy(buffer, offset, _dopplerBuffer, 0, count);

        int frames = count / channels;
        for (int frame = 0; frame < frames; frame++)
        {
            // Calculate source position with doppler shift
            float srcPos = _dopplerPhase;
            int srcFrame = (int)srcPos;
            float frac = srcPos - srcFrame;

            if (srcFrame >= 0 && srcFrame < frames - 1)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    // Linear interpolation
                    float s0 = _dopplerBuffer[srcFrame * channels + ch];
                    float s1 = _dopplerBuffer[(srcFrame + 1) * channels + ch];
                    buffer[offset + frame * channels + ch] = s0 + frac * (s1 - s0);
                }
            }

            _dopplerPhase += dopplerRatio;
        }

        // Keep phase in range
        _dopplerPhase -= (int)_dopplerPhase;
        if (_dopplerPhase < 0) _dopplerPhase = 0;
    }

    /// <summary>
    /// Calculates the cone gain based on angle to listener.
    /// </summary>
    /// <param name="angleToListener">Angle in degrees from source forward direction to listener</param>
    public float CalculateConeGain(float angleToListener)
    {
        angleToListener = MathF.Abs(angleToListener);

        if (angleToListener <= _innerConeAngle * 0.5f)
        {
            return 1f; // Full gain within inner cone
        }
        else if (angleToListener >= _outerConeAngle * 0.5f)
        {
            return _outerConeGain; // Outer cone gain outside outer cone
        }
        else
        {
            // Interpolate between inner and outer
            float innerHalf = _innerConeAngle * 0.5f;
            float outerHalf = _outerConeAngle * 0.5f;
            float t = (angleToListener - innerHalf) / (outerHalf - innerHalf);
            return 1f + t * (_outerConeGain - 1f);
        }
    }

    /// <summary>
    /// Normalizes an angle to the range [-180, 180).
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        while (angle >= 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    /// <summary>
    /// Ensures the buffer is at least the specified size.
    /// </summary>
    private void EnsureBufferSize(ref float[] buffer, int requiredSize)
    {
        if (buffer.Length < requiredSize)
        {
            buffer = new float[requiredSize];
        }
    }

    /// <summary>
    /// Disposes of resources.
    /// </summary>
    public void Dispose()
    {
        // Dispose underlying source if it implements IDisposable
        if (_audioSource is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
