//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Main spatial audio processor supporting stereo, surround, and ambisonic formats.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Spatial;

/// <summary>
/// Supported spatial audio output formats.
/// </summary>
public enum SpatialFormat
{
    /// <summary>Standard stereo output (2 channels: L, R)</summary>
    Stereo,
    /// <summary>5.1 surround sound (6 channels: L, R, C, LFE, Ls, Rs)</summary>
    Surround51,
    /// <summary>7.1 surround sound (8 channels: L, R, C, LFE, Ls, Rs, Lrs, Rrs)</summary>
    Surround71,
    /// <summary>First-order Ambisonics (4 channels: W, X, Y, Z)</summary>
    AmbisonicsFirstOrder,
    /// <summary>Second-order Ambisonics (9 channels)</summary>
    AmbisonicsSecondOrder,
    /// <summary>Third-order Ambisonics (16 channels)</summary>
    AmbisonicsThirdOrder,
    /// <summary>Binaural stereo output using HRTF</summary>
    Binaural
}

/// <summary>
/// Panning law algorithms for spatial positioning.
/// </summary>
public enum PanningLaw
{
    /// <summary>Linear amplitude panning</summary>
    Linear,
    /// <summary>Equal power (constant power) panning - maintains perceived loudness</summary>
    EqualPower,
    /// <summary>Vector Base Amplitude Panning - optimal for arbitrary speaker layouts</summary>
    VBAP
}

/// <summary>
/// Distance attenuation models for spatial audio.
/// </summary>
public enum DistanceModel
{
    /// <summary>No distance attenuation</summary>
    None,
    /// <summary>Linear distance falloff</summary>
    Linear,
    /// <summary>Inverse distance (1/r) - physically accurate</summary>
    Inverse,
    /// <summary>Inverse square (1/r^2) - more dramatic falloff</summary>
    InverseSquare,
    /// <summary>Exponential falloff</summary>
    Exponential,
    /// <summary>Custom rolloff curve</summary>
    Custom
}

/// <summary>
/// Represents a speaker position in 3D space.
/// </summary>
public readonly struct SpeakerPosition
{
    /// <summary>The speaker channel name (e.g., "L", "R", "C", "LFE")</summary>
    public string Name { get; }

    /// <summary>Azimuth angle in degrees (0 = front, 90 = right, -90 = left, 180 = back)</summary>
    public float Azimuth { get; }

    /// <summary>Elevation angle in degrees (0 = ear level, 90 = above, -90 = below)</summary>
    public float Elevation { get; }

    /// <summary>Distance from listener (normalized, 1.0 = reference distance)</summary>
    public float Distance { get; }

    public SpeakerPosition(string name, float azimuth, float elevation = 0f, float distance = 1f)
    {
        Name = name;
        Azimuth = azimuth;
        Elevation = elevation;
        Distance = distance;
    }

    /// <summary>
    /// Converts the speaker position to a Cartesian unit vector.
    /// </summary>
    public (float X, float Y, float Z) ToCartesian()
    {
        float azRad = Azimuth * MathF.PI / 180f;
        float elRad = Elevation * MathF.PI / 180f;

        float cosEl = MathF.Cos(elRad);
        float x = MathF.Sin(azRad) * cosEl;  // Right is positive
        float y = MathF.Cos(azRad) * cosEl;  // Front is positive
        float z = MathF.Sin(elRad);          // Up is positive

        return (x, y, z);
    }
}

/// <summary>
/// Predefined speaker layouts for various surround formats.
/// </summary>
public static class SpeakerLayouts
{
    /// <summary>Standard stereo layout</summary>
    public static readonly SpeakerPosition[] Stereo =
    {
        new("L", -30f),
        new("R", 30f)
    };

    /// <summary>ITU-R BS.775 5.1 surround layout</summary>
    public static readonly SpeakerPosition[] Surround51 =
    {
        new("L", -30f),
        new("R", 30f),
        new("C", 0f),
        new("LFE", 0f, -30f),  // LFE typically below ear level
        new("Ls", -110f),
        new("Rs", 110f)
    };

    /// <summary>ITU-R BS.775 7.1 surround layout</summary>
    public static readonly SpeakerPosition[] Surround71 =
    {
        new("L", -30f),
        new("R", 30f),
        new("C", 0f),
        new("LFE", 0f, -30f),
        new("Ls", -90f),
        new("Rs", 90f),
        new("Lrs", -150f),
        new("Rrs", 150f)
    };

    /// <summary>Gets the speaker layout for a given spatial format.</summary>
    public static SpeakerPosition[] GetLayout(SpatialFormat format)
    {
        return format switch
        {
            SpatialFormat.Stereo => Stereo,
            SpatialFormat.Surround51 => Surround51,
            SpatialFormat.Surround71 => Surround71,
            SpatialFormat.Binaural => Stereo, // Binaural outputs to stereo
            _ => Stereo // Default to stereo for ambisonics (decoded later)
        };
    }

    /// <summary>Gets the channel count for a given spatial format.</summary>
    public static int GetChannelCount(SpatialFormat format)
    {
        return format switch
        {
            SpatialFormat.Stereo => 2,
            SpatialFormat.Surround51 => 6,
            SpatialFormat.Surround71 => 8,
            SpatialFormat.AmbisonicsFirstOrder => 4,
            SpatialFormat.AmbisonicsSecondOrder => 9,
            SpatialFormat.AmbisonicsThirdOrder => 16,
            SpatialFormat.Binaural => 2,
            _ => 2
        };
    }
}

/// <summary>
/// Main spatial audio processing engine.
/// Manages spatial sources, listener position, and renders to various output formats.
/// </summary>
public class SpatialAudioEngine : IDisposable
{
    private readonly int _sampleRate;
    private readonly List<SpatialSource> _sources = new();
    private readonly SpatialListener _listener;
    private readonly object _sourceLock = new();

    private SpatialFormat _outputFormat = SpatialFormat.Stereo;
    private PanningLaw _panningLaw = PanningLaw.EqualPower;
    private DistanceModel _distanceModel = DistanceModel.Inverse;
    private SpeakerPosition[] _speakerLayout;

    // Ambisonic processing
    private AmbisonicEncoder? _ambiEncoder;
    private AmbisonicDecoder? _ambiDecoder;

    // Binaural processing
    private BinauralRenderer? _binauralRenderer;

    // Custom distance rolloff curve (distance -> gain)
    private Func<float, float>? _customRolloff;

    // Output buffer
    private float[] _outputBuffer = Array.Empty<float>();
    private float[] _ambiBuffer = Array.Empty<float>();

    /// <summary>
    /// Gets or sets the output spatial format.
    /// </summary>
    public SpatialFormat OutputFormat
    {
        get => _outputFormat;
        set
        {
            if (_outputFormat != value)
            {
                _outputFormat = value;
                _speakerLayout = SpeakerLayouts.GetLayout(value);
                UpdateProcessors();
            }
        }
    }

    /// <summary>
    /// Gets or sets the panning law algorithm.
    /// </summary>
    public PanningLaw PanningLaw
    {
        get => _panningLaw;
        set => _panningLaw = value;
    }

    /// <summary>
    /// Gets or sets the distance attenuation model.
    /// </summary>
    public DistanceModel DistanceModel
    {
        get => _distanceModel;
        set => _distanceModel = value;
    }

    /// <summary>
    /// Gets the spatial listener.
    /// </summary>
    public SpatialListener Listener => _listener;

    /// <summary>
    /// Gets the number of output channels.
    /// </summary>
    public int OutputChannelCount => SpeakerLayouts.GetChannelCount(_outputFormat);

    /// <summary>
    /// Gets the sample rate.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Creates a new spatial audio engine.
    /// </summary>
    /// <param name="sampleRate">The audio sample rate</param>
    /// <param name="outputFormat">The desired output format</param>
    public SpatialAudioEngine(int sampleRate = 44100, SpatialFormat outputFormat = SpatialFormat.Stereo)
    {
        _sampleRate = sampleRate;
        _outputFormat = outputFormat;
        _speakerLayout = SpeakerLayouts.GetLayout(outputFormat);
        _listener = new SpatialListener();

        UpdateProcessors();
    }

    /// <summary>
    /// Sets a custom distance rolloff curve.
    /// </summary>
    /// <param name="rolloffFunction">Function mapping distance to gain (0-1)</param>
    public void SetCustomRolloff(Func<float, float> rolloffFunction)
    {
        _customRolloff = rolloffFunction;
        _distanceModel = DistanceModel.Custom;
    }

    /// <summary>
    /// Creates and adds a new spatial source.
    /// </summary>
    /// <param name="audioSource">The audio sample provider for this source</param>
    /// <returns>The created spatial source</returns>
    public SpatialSource CreateSource(ISampleProvider audioSource)
    {
        var source = new SpatialSource(audioSource, _sampleRate);

        lock (_sourceLock)
        {
            _sources.Add(source);
        }

        return source;
    }

    /// <summary>
    /// Removes a spatial source.
    /// </summary>
    /// <param name="source">The source to remove</param>
    public void RemoveSource(SpatialSource source)
    {
        lock (_sourceLock)
        {
            _sources.Remove(source);
        }
    }

    /// <summary>
    /// Gets all active spatial sources.
    /// </summary>
    public IReadOnlyList<SpatialSource> Sources
    {
        get
        {
            lock (_sourceLock)
            {
                return _sources.ToArray();
            }
        }
    }

    /// <summary>
    /// Processes audio and renders to the spatial output format.
    /// </summary>
    /// <param name="outputBuffer">Buffer to receive the spatialized audio</param>
    /// <param name="offset">Offset into the output buffer</param>
    /// <param name="sampleFrames">Number of sample frames to process</param>
    /// <returns>Number of sample frames processed</returns>
    public int Process(float[] outputBuffer, int offset, int sampleFrames)
    {
        int outputChannels = OutputChannelCount;
        int totalSamples = sampleFrames * outputChannels;

        // Clear output buffer
        Array.Clear(outputBuffer, offset, totalSamples);

        // Get sources snapshot
        SpatialSource[] sources;
        lock (_sourceLock)
        {
            sources = _sources.ToArray();
        }

        // Process each source
        foreach (var source in sources)
        {
            if (!source.Enabled) continue;

            // Read audio from source
            EnsureBufferSize(ref _outputBuffer, sampleFrames * 2); // Max stereo input
            int sourceSamples = source.Read(_outputBuffer, 0, sampleFrames * source.WaveFormat.Channels);
            if (sourceSamples == 0) continue;

            int sourceFrames = sourceSamples / source.WaveFormat.Channels;

            // Calculate spatial parameters
            var (azimuth, elevation, distance) = CalculateSourcePosition(source);
            float distanceGain = CalculateDistanceAttenuation(distance, source);

            // Route based on output format
            if (_outputFormat == SpatialFormat.Binaural)
            {
                ProcessBinaural(source, _outputBuffer, outputBuffer, offset, sourceFrames, azimuth, elevation, distanceGain);
            }
            else if (_outputFormat >= SpatialFormat.AmbisonicsFirstOrder && _outputFormat <= SpatialFormat.AmbisonicsThirdOrder)
            {
                ProcessAmbisonic(source, _outputBuffer, outputBuffer, offset, sourceFrames, azimuth, elevation, distanceGain);
            }
            else
            {
                ProcessSpeakerPanning(source, _outputBuffer, outputBuffer, offset, sourceFrames, azimuth, elevation, distanceGain);
            }
        }

        return sampleFrames;
    }

    /// <summary>
    /// Calculates the position of a source relative to the listener.
    /// </summary>
    private (float Azimuth, float Elevation, float Distance) CalculateSourcePosition(SpatialSource source)
    {
        // Get relative position
        float dx = source.PositionX - _listener.PositionX;
        float dy = source.PositionY - _listener.PositionY;
        float dz = source.PositionZ - _listener.PositionZ;

        // Apply listener orientation (rotate relative position)
        float yawRad = _listener.Yaw * MathF.PI / 180f;
        float pitchRad = _listener.Pitch * MathF.PI / 180f;

        // Rotate around vertical axis (yaw)
        float rotX = dx * MathF.Cos(yawRad) - dy * MathF.Sin(yawRad);
        float rotY = dx * MathF.Sin(yawRad) + dy * MathF.Cos(yawRad);
        float rotZ = dz;

        // Rotate around lateral axis (pitch)
        float finalY = rotY * MathF.Cos(pitchRad) - rotZ * MathF.Sin(pitchRad);
        float finalZ = rotY * MathF.Sin(pitchRad) + rotZ * MathF.Cos(pitchRad);

        // Calculate spherical coordinates
        float distance = MathF.Sqrt(rotX * rotX + finalY * finalY + finalZ * finalZ);
        if (distance < 0.0001f) distance = 0.0001f; // Avoid division by zero

        float azimuth = MathF.Atan2(rotX, finalY) * 180f / MathF.PI;
        float elevation = MathF.Asin(Math.Clamp(finalZ / distance, -1f, 1f)) * 180f / MathF.PI;

        return (azimuth, elevation, distance);
    }

    /// <summary>
    /// Calculates distance attenuation for a source.
    /// </summary>
    private float CalculateDistanceAttenuation(float distance, SpatialSource source)
    {
        float refDistance = _listener.ReferenceDistance;
        float maxDistance = source.MaxDistance;

        // Clamp distance to valid range
        distance = Math.Max(distance, refDistance);
        if (maxDistance > 0) distance = Math.Min(distance, maxDistance);

        float gain = _distanceModel switch
        {
            DistanceModel.None => 1f,
            DistanceModel.Linear => 1f - source.RolloffFactor * (distance - refDistance) / (maxDistance - refDistance),
            DistanceModel.Inverse => refDistance / (refDistance + source.RolloffFactor * (distance - refDistance)),
            DistanceModel.InverseSquare => (refDistance * refDistance) / (refDistance * refDistance + source.RolloffFactor * (distance - refDistance) * (distance - refDistance)),
            DistanceModel.Exponential => MathF.Pow(distance / refDistance, -source.RolloffFactor),
            DistanceModel.Custom => _customRolloff?.Invoke(distance / refDistance) ?? 1f,
            _ => 1f
        };

        return Math.Clamp(gain, 0f, 1f);
    }

    /// <summary>
    /// Processes audio using speaker-based panning (stereo, 5.1, 7.1).
    /// </summary>
    private void ProcessSpeakerPanning(SpatialSource source, float[] sourceBuffer, float[] outputBuffer,
        int offset, int sampleFrames, float azimuth, float elevation, float distanceGain)
    {
        int outputChannels = OutputChannelCount;
        int sourceChannels = source.WaveFormat.Channels;

        // Calculate speaker gains based on panning law
        float[] speakerGains = CalculateSpeakerGains(azimuth, elevation, source.Spread);

        for (int frame = 0; frame < sampleFrames; frame++)
        {
            // Get mono or convert stereo to mono
            float sample = 0f;
            for (int ch = 0; ch < sourceChannels; ch++)
            {
                sample += sourceBuffer[frame * sourceChannels + ch];
            }
            sample /= sourceChannels;

            // Apply distance attenuation
            sample *= distanceGain;

            // Distribute to speakers
            for (int sp = 0; sp < outputChannels && sp < speakerGains.Length; sp++)
            {
                outputBuffer[offset + frame * outputChannels + sp] += sample * speakerGains[sp];
            }
        }
    }

    /// <summary>
    /// Calculates speaker gains for a given source position.
    /// </summary>
    private float[] CalculateSpeakerGains(float azimuth, float elevation, float spread)
    {
        var layout = _speakerLayout;
        float[] gains = new float[layout.Length];

        switch (_panningLaw)
        {
            case PanningLaw.Linear:
                CalculateLinearPanning(layout, gains, azimuth, elevation, spread);
                break;

            case PanningLaw.EqualPower:
                CalculateEqualPowerPanning(layout, gains, azimuth, elevation, spread);
                break;

            case PanningLaw.VBAP:
                CalculateVBAPPanning(layout, gains, azimuth, elevation, spread);
                break;
        }

        return gains;
    }

    /// <summary>
    /// Linear amplitude panning.
    /// </summary>
    private void CalculateLinearPanning(SpeakerPosition[] layout, float[] gains, float azimuth, float elevation, float spread)
    {
        // Simple linear interpolation based on angular distance
        float totalWeight = 0f;

        for (int i = 0; i < layout.Length; i++)
        {
            // Skip LFE for spatial panning (index 3 in 5.1/7.1)
            if (layout[i].Name == "LFE")
            {
                gains[i] = 0.1f; // Low constant LFE level
                continue;
            }

            float angularDist = CalculateAngularDistance(azimuth, elevation, layout[i].Azimuth, layout[i].Elevation);
            float weight = 1f - Math.Min(angularDist / (180f + spread * 90f), 1f);
            gains[i] = weight;
            totalWeight += weight;
        }

        // Normalize
        if (totalWeight > 0.0001f)
        {
            for (int i = 0; i < gains.Length; i++)
            {
                if (layout[i].Name != "LFE")
                    gains[i] /= totalWeight;
            }
        }
    }

    /// <summary>
    /// Equal power (constant power) panning.
    /// </summary>
    private void CalculateEqualPowerPanning(SpeakerPosition[] layout, float[] gains, float azimuth, float elevation, float spread)
    {
        // Calculate linear panning first
        CalculateLinearPanning(layout, gains, azimuth, elevation, spread);

        // Apply equal power curve (square root)
        float totalPower = 0f;
        for (int i = 0; i < gains.Length; i++)
        {
            if (layout[i].Name != "LFE")
            {
                gains[i] = MathF.Sqrt(gains[i]);
                totalPower += gains[i] * gains[i];
            }
        }

        // Normalize to maintain constant power
        if (totalPower > 0.0001f)
        {
            float normFactor = 1f / MathF.Sqrt(totalPower);
            for (int i = 0; i < gains.Length; i++)
            {
                if (layout[i].Name != "LFE")
                    gains[i] *= normFactor;
            }
        }
    }

    /// <summary>
    /// Vector Base Amplitude Panning (VBAP).
    /// </summary>
    private void CalculateVBAPPanning(SpeakerPosition[] layout, float[] gains, float azimuth, float elevation, float spread)
    {
        // Find the two or three closest speakers and interpolate
        // This is a simplified 2D VBAP implementation

        var (srcX, srcY, srcZ) = (
            MathF.Sin(azimuth * MathF.PI / 180f) * MathF.Cos(elevation * MathF.PI / 180f),
            MathF.Cos(azimuth * MathF.PI / 180f) * MathF.Cos(elevation * MathF.PI / 180f),
            MathF.Sin(elevation * MathF.PI / 180f)
        );

        // Find closest speakers
        var distances = new (int Index, float Distance)[layout.Length];
        for (int i = 0; i < layout.Length; i++)
        {
            if (layout[i].Name == "LFE")
            {
                distances[i] = (i, float.MaxValue);
                gains[i] = 0.1f;
                continue;
            }

            var (spkX, spkY, spkZ) = layout[i].ToCartesian();
            float dist = MathF.Sqrt((srcX - spkX) * (srcX - spkX) + (srcY - spkY) * (srcY - spkY) + (srcZ - spkZ) * (srcZ - spkZ));
            distances[i] = (i, dist);
        }

        // Sort by distance
        Array.Sort(distances, (a, b) => a.Distance.CompareTo(b.Distance));

        // Use closest 2-3 speakers for interpolation
        int numSpeakers = Math.Min(3, layout.Length);
        float totalWeight = 0f;

        for (int i = 0; i < numSpeakers; i++)
        {
            if (distances[i].Distance < float.MaxValue)
            {
                float weight = 1f / (distances[i].Distance + 0.0001f);
                gains[distances[i].Index] = weight;
                totalWeight += weight;
            }
        }

        // Normalize and apply equal power
        if (totalWeight > 0.0001f)
        {
            float totalPower = 0f;
            for (int i = 0; i < gains.Length; i++)
            {
                if (layout[i].Name != "LFE")
                {
                    gains[i] = MathF.Sqrt(gains[i] / totalWeight);
                    totalPower += gains[i] * gains[i];
                }
            }

            if (totalPower > 0.0001f)
            {
                float normFactor = 1f / MathF.Sqrt(totalPower);
                for (int i = 0; i < gains.Length; i++)
                {
                    if (layout[i].Name != "LFE")
                        gains[i] *= normFactor;
                }
            }
        }
    }

    /// <summary>
    /// Calculates the angular distance between two positions.
    /// </summary>
    private float CalculateAngularDistance(float az1, float el1, float az2, float el2)
    {
        // Convert to Cartesian and use dot product
        var (x1, y1, z1) = (
            MathF.Sin(az1 * MathF.PI / 180f) * MathF.Cos(el1 * MathF.PI / 180f),
            MathF.Cos(az1 * MathF.PI / 180f) * MathF.Cos(el1 * MathF.PI / 180f),
            MathF.Sin(el1 * MathF.PI / 180f)
        );

        var (x2, y2, z2) = (
            MathF.Sin(az2 * MathF.PI / 180f) * MathF.Cos(el2 * MathF.PI / 180f),
            MathF.Cos(az2 * MathF.PI / 180f) * MathF.Cos(el2 * MathF.PI / 180f),
            MathF.Sin(el2 * MathF.PI / 180f)
        );

        float dot = x1 * x2 + y1 * y2 + z1 * z2;
        dot = Math.Clamp(dot, -1f, 1f);

        return MathF.Acos(dot) * 180f / MathF.PI;
    }

    /// <summary>
    /// Processes audio using ambisonic encoding.
    /// </summary>
    private void ProcessAmbisonic(SpatialSource source, float[] sourceBuffer, float[] outputBuffer,
        int offset, int sampleFrames, float azimuth, float elevation, float distanceGain)
    {
        if (_ambiEncoder == null) return;

        int sourceChannels = source.WaveFormat.Channels;
        int ambiChannels = _ambiEncoder.OutputChannelCount;

        EnsureBufferSize(ref _ambiBuffer, sampleFrames * ambiChannels);

        for (int frame = 0; frame < sampleFrames; frame++)
        {
            // Get mono sample
            float sample = 0f;
            for (int ch = 0; ch < sourceChannels; ch++)
            {
                sample += sourceBuffer[frame * sourceChannels + ch];
            }
            sample /= sourceChannels;
            sample *= distanceGain;

            // Encode to ambisonics
            _ambiEncoder.Encode(sample, azimuth, elevation, _ambiBuffer, frame * ambiChannels);
        }

        // Add to output
        int outputChannels = OutputChannelCount;
        for (int i = 0; i < sampleFrames * outputChannels; i++)
        {
            outputBuffer[offset + i] += _ambiBuffer[i];
        }
    }

    /// <summary>
    /// Processes audio using binaural HRTF rendering.
    /// </summary>
    private void ProcessBinaural(SpatialSource source, float[] sourceBuffer, float[] outputBuffer,
        int offset, int sampleFrames, float azimuth, float elevation, float distanceGain)
    {
        if (_binauralRenderer == null) return;

        int sourceChannels = source.WaveFormat.Channels;

        for (int frame = 0; frame < sampleFrames; frame++)
        {
            // Get mono sample
            float sample = 0f;
            for (int ch = 0; ch < sourceChannels; ch++)
            {
                sample += sourceBuffer[frame * sourceChannels + ch];
            }
            sample /= sourceChannels;
            sample *= distanceGain;

            // Apply HRTF
            var (left, right) = _binauralRenderer.Process(sample, azimuth, elevation);

            outputBuffer[offset + frame * 2] += left;
            outputBuffer[offset + frame * 2 + 1] += right;
        }
    }

    /// <summary>
    /// Updates internal processors when format changes.
    /// </summary>
    private void UpdateProcessors()
    {
        // Create ambisonic encoder/decoder if needed
        if (_outputFormat >= SpatialFormat.AmbisonicsFirstOrder && _outputFormat <= SpatialFormat.AmbisonicsThirdOrder)
        {
            int order = _outputFormat switch
            {
                SpatialFormat.AmbisonicsFirstOrder => 1,
                SpatialFormat.AmbisonicsSecondOrder => 2,
                SpatialFormat.AmbisonicsThirdOrder => 3,
                _ => 1
            };

            _ambiEncoder = new AmbisonicEncoder(order);
            _ambiDecoder = new AmbisonicDecoder(order, _sampleRate);
        }
        else
        {
            _ambiEncoder = null;
            _ambiDecoder = null;
        }

        // Create binaural renderer if needed
        if (_outputFormat == SpatialFormat.Binaural || _listener.HrtfEnabled)
        {
            _binauralRenderer ??= new BinauralRenderer(_sampleRate);
        }
        else
        {
            _binauralRenderer = null;
        }
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
        lock (_sourceLock)
        {
            foreach (var source in _sources)
            {
                source.Dispose();
            }
            _sources.Clear();
        }

        _binauralRenderer?.Dispose();
    }
}
