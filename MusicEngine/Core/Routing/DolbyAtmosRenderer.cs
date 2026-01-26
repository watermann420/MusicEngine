// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio/MIDI routing component.

using NAudio.Wave;

namespace MusicEngine.Core.Routing;

/// <summary>
/// Represents an audio object with 3D position for object-based audio.
/// </summary>
public class AudioObject
{
    /// <summary>
    /// Unique identifier for this object.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the object.
    /// </summary>
    public string Name { get; set; } = "Audio Object";

    /// <summary>
    /// The audio source for this object.
    /// </summary>
    public ISampleProvider? Source { get; set; }

    /// <summary>
    /// X position (-1 = left, 0 = center, 1 = right).
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// Y position (-1 = below, 0 = ear level, 1 = above).
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    /// Z position (-1 = behind, 0 = center, 1 = front).
    /// </summary>
    public float Z { get; set; }

    /// <summary>
    /// Object size/spread (0 = point source, 1 = full spread).
    /// </summary>
    public float Size { get; set; }

    /// <summary>
    /// Object gain (0 to 1).
    /// </summary>
    public float Gain { get; set; } = 1f;

    /// <summary>
    /// Whether this object is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Internal buffer for reading audio.
    /// </summary>
    internal float[] Buffer { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Sets the position using spherical coordinates.
    /// </summary>
    /// <param name="azimuth">Horizontal angle in degrees (-180 to 180).</param>
    /// <param name="elevation">Vertical angle in degrees (-90 to 90).</param>
    /// <param name="distance">Distance from listener (0 to 1).</param>
    public void SetSphericalPosition(float azimuth, float elevation, float distance)
    {
        float azRad = azimuth * MathF.PI / 180f;
        float elRad = elevation * MathF.PI / 180f;
        float cosEl = MathF.Cos(elRad);

        X = MathF.Sin(azRad) * cosEl * distance;
        Y = MathF.Sin(elRad) * distance;
        Z = MathF.Cos(azRad) * cosEl * distance;
    }
}

/// <summary>
/// Binaural rendering mode for headphone output.
/// </summary>
public enum BinauralMode
{
    /// <summary>
    /// Binaural rendering disabled.
    /// </summary>
    Off,

    /// <summary>
    /// Simple HRTF-based binaural.
    /// </summary>
    Simple,

    /// <summary>
    /// High-quality binaural with room simulation.
    /// </summary>
    HighQuality
}

/// <summary>
/// Object-based audio renderer for Dolby Atmos-style immersive audio.
/// </summary>
/// <remarks>
/// Features:
/// - Audio objects with 3D position and size
/// - 7.1.4 bed channel support
/// - Object panning with height
/// - Binaural downmix option for headphones
/// - Up to 128 simultaneous objects
/// </remarks>
public class DolbyAtmosRenderer : ISampleProvider
{
    private const int MaxObjects = 128;
    private const int BedChannels = 12; // 7.1.4

    private readonly List<AudioObject> _objects = new();
    private readonly ISampleProvider? _bedSource;
    private readonly WaveFormat _waveFormat;
    private readonly float[][] _objectGains;
    private readonly float[][] _targetGains;
    private readonly SurroundChannel[] _channels;

    // Binaural rendering
    private BinauralMode _binauralMode = BinauralMode.Off;
    private float[] _surroundBuffer = Array.Empty<float>();
    private float[] _binauralMixBuffer = Array.Empty<float>();

    // Bed channel buffer
    private float[] _bedBuffer = Array.Empty<float>();

    // Object mixing buffer
    private float[] _objectMixBuffer = Array.Empty<float>();

    // Speaker configuration
    private readonly SurroundFormat _format;

    /// <summary>
    /// Creates a new Dolby Atmos-style renderer.
    /// </summary>
    /// <param name="sampleRate">Sample rate for output.</param>
    /// <param name="bedSource">Optional 7.1.4 bed channel source.</param>
    public DolbyAtmosRenderer(int sampleRate, ISampleProvider? bedSource = null)
    {
        _format = SurroundFormat.Atmos_7_1_4;
        _bedSource = bedSource;

        // Validate bed source if provided
        if (bedSource != null && bedSource.WaveFormat.Channels != BedChannels)
        {
            throw new ArgumentException($"Bed source must have {BedChannels} channels", nameof(bedSource));
        }

        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, BedChannels);
        _channels = SurroundChannel.CreateChannelsForFormat(_format);

        // Initialize gain matrices
        _objectGains = new float[MaxObjects][];
        _targetGains = new float[MaxObjects][];
        for (int i = 0; i < MaxObjects; i++)
        {
            _objectGains[i] = new float[BedChannels];
            _targetGains[i] = new float[BedChannels];
        }
    }

    /// <summary>
    /// Gets the output wave format (7.1.4).
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Gets the collection of audio objects.
    /// </summary>
    public IReadOnlyList<AudioObject> Objects => _objects.AsReadOnly();

    /// <summary>
    /// Gets or sets the binaural rendering mode.
    /// </summary>
    public BinauralMode BinauralMode
    {
        get => _binauralMode;
        set => _binauralMode = value;
    }

    /// <summary>
    /// Gets or sets the master output gain.
    /// </summary>
    public float MasterGain { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the object pan smoothing coefficient (0-1).
    /// Higher values result in smoother but slower position changes.
    /// </summary>
    public float PanSmoothing { get; set; } = 0.95f;

    /// <summary>
    /// Adds an audio object to the renderer.
    /// </summary>
    /// <param name="obj">The audio object to add.</param>
    /// <returns>True if object was added.</returns>
    public bool AddObject(AudioObject obj)
    {
        if (_objects.Count >= MaxObjects)
            return false;

        _objects.Add(obj);
        return true;
    }

    /// <summary>
    /// Removes an audio object from the renderer.
    /// </summary>
    /// <param name="objectId">The ID of the object to remove.</param>
    /// <returns>True if object was found and removed.</returns>
    public bool RemoveObject(Guid objectId)
    {
        var obj = _objects.FirstOrDefault(o => o.Id == objectId);
        if (obj != null)
        {
            _objects.Remove(obj);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Creates and adds a new audio object.
    /// </summary>
    /// <param name="source">Audio source for the object.</param>
    /// <param name="name">Object name.</param>
    /// <returns>The created audio object, or null if max objects reached.</returns>
    public AudioObject? CreateObject(ISampleProvider source, string name = "Audio Object")
    {
        if (_objects.Count >= MaxObjects)
            return null;

        var obj = new AudioObject
        {
            Source = source,
            Name = name
        };

        _objects.Add(obj);
        return obj;
    }

    /// <summary>
    /// Clears all audio objects.
    /// </summary>
    public void ClearObjects()
    {
        _objects.Clear();
    }

    /// <summary>
    /// Reads audio and renders all objects and bed channels.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int frames = count / BedChannels;

        // Ensure buffers are large enough
        EnsureBuffers(frames);

        // Clear output
        Array.Clear(buffer, offset, count);

        // Mix bed channels if available
        if (_bedSource != null)
        {
            int bedSamples = _bedSource.Read(_bedBuffer, 0, count);
            if (bedSamples > 0)
            {
                for (int i = 0; i < bedSamples; i++)
                {
                    buffer[offset + i] += _bedBuffer[i];
                }
            }
        }

        // Mix audio objects
        for (int objIdx = 0; objIdx < _objects.Count; objIdx++)
        {
            var obj = _objects[objIdx];
            if (!obj.IsActive || obj.Source == null)
                continue;

            // Calculate target gains for this object's position
            CalculateObjectGains(obj, _targetGains[objIdx]);

            // Read object audio (mono)
            int sourceChannels = obj.Source.WaveFormat.Channels;
            int sourceSamples = frames * sourceChannels;

            if (obj.Buffer.Length < sourceSamples)
            {
                obj.Buffer = new float[sourceSamples];
            }

            int samplesRead = obj.Source.Read(obj.Buffer, 0, sourceSamples);
            if (samplesRead == 0)
                continue;

            int objectFrames = samplesRead / sourceChannels;

            // Mix object to output with gain smoothing
            for (int frame = 0; frame < objectFrames; frame++)
            {
                // Get mono sample (mix down if stereo)
                float sample;
                if (sourceChannels == 1)
                {
                    sample = obj.Buffer[frame];
                }
                else
                {
                    sample = 0f;
                    for (int ch = 0; ch < sourceChannels; ch++)
                    {
                        sample += obj.Buffer[frame * sourceChannels + ch];
                    }
                    sample /= sourceChannels;
                }

                sample *= obj.Gain;

                // Pan to all channels with smoothed gains
                int frameOffset = offset + frame * BedChannels;
                for (int ch = 0; ch < BedChannels; ch++)
                {
                    // Smooth gain transition
                    _objectGains[objIdx][ch] = _objectGains[objIdx][ch] * PanSmoothing +
                                                _targetGains[objIdx][ch] * (1f - PanSmoothing);

                    buffer[frameOffset + ch] += sample * _objectGains[objIdx][ch];
                }
            }
        }

        // Apply master gain
        if (MathF.Abs(MasterGain - 1f) > 0.001f)
        {
            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] *= MasterGain;
            }
        }

        // Binaural downmix if enabled
        if (_binauralMode != BinauralMode.Off)
        {
            DownmixToBinaural(buffer, offset, count, frames);
        }

        return count;
    }

    /// <summary>
    /// Calculates object gain coefficients for all channels.
    /// </summary>
    private void CalculateObjectGains(AudioObject obj, float[] gains)
    {
        // Convert Cartesian to spherical for VBAP-style panning
        float distance = MathF.Sqrt(obj.X * obj.X + obj.Y * obj.Y + obj.Z * obj.Z);
        if (distance < 0.001f) distance = 0.001f;

        float azimuth = MathF.Atan2(obj.X, obj.Z) * 180f / MathF.PI;
        float elevation = MathF.Asin(Math.Clamp(obj.Y / distance, -1f, 1f)) * 180f / MathF.PI;

        // Distance attenuation
        float distanceGain = 1f / (1f + distance * 2f);

        // Calculate gains for each speaker
        for (int ch = 0; ch < BedChannels; ch++)
        {
            var channel = _channels[ch];

            if (channel.IsLFE)
            {
                // LFE gets a portion of low frequencies
                gains[ch] = 0.1f * distanceGain;
                continue;
            }

            // Calculate angular distance to speaker
            float speakerAz = channel.Azimuth;
            float speakerEl = channel.Elevation;

            float azDiff = NormalizeAngle(azimuth - speakerAz);
            float elDiff = elevation - speakerEl;

            float angularDist = MathF.Sqrt(azDiff * azDiff + elDiff * elDiff);

            // VBAP-style gain calculation
            float maxAngle = 90f;  // Max spread angle
            float spreadAngle = maxAngle * (1f + obj.Size);

            float gain;
            if (angularDist < spreadAngle)
            {
                // Cosine-based falloff
                float normalizedDist = angularDist / spreadAngle;
                gain = MathF.Cos(normalizedDist * MathF.PI / 2f);
                gain = MathF.Max(0f, gain);
            }
            else
            {
                gain = 0f;
            }

            gains[ch] = gain * distanceGain;
        }

        // Normalize gains (power preservation)
        float sumSquared = 0f;
        for (int ch = 0; ch < BedChannels; ch++)
        {
            if (!_channels[ch].IsLFE)
            {
                sumSquared += gains[ch] * gains[ch];
            }
        }

        if (sumSquared > 0.001f)
        {
            float normalizer = 1f / MathF.Sqrt(sumSquared);
            for (int ch = 0; ch < BedChannels; ch++)
            {
                if (!_channels[ch].IsLFE)
                {
                    gains[ch] *= normalizer;
                }
            }
        }
    }

    /// <summary>
    /// Downmixes surround output to binaural stereo.
    /// </summary>
    private void DownmixToBinaural(float[] buffer, int offset, int count, int frames)
    {
        // Simple binaural downmix using channel-based HRTF approximation
        // For high-quality binaural, use BinauralRenderer directly

        if (_binauralMixBuffer.Length < frames * 2)
        {
            _binauralMixBuffer = new float[frames * 2];
        }

        Array.Clear(_binauralMixBuffer, 0, frames * 2);

        // Downmix each channel with HRTF-approximated gains
        for (int frame = 0; frame < frames; frame++)
        {
            int srcOffset = offset + frame * BedChannels;
            int dstOffset = frame * 2;

            float leftSum = 0f;
            float rightSum = 0f;

            for (int ch = 0; ch < BedChannels; ch++)
            {
                var channel = _channels[ch];
                float sample = buffer[srcOffset + ch];

                if (channel.IsLFE)
                {
                    // LFE to both ears equally
                    leftSum += sample * 0.5f;
                    rightSum += sample * 0.5f;
                    continue;
                }

                // HRTF-approximated panning based on azimuth
                float azRad = channel.Azimuth * MathF.PI / 180f;
                float panL = MathF.Cos(azRad / 2f + MathF.PI / 4f);
                float panR = MathF.Sin(azRad / 2f + MathF.PI / 4f);

                // Add elevation effect (sounds from above are slightly attenuated)
                float elFactor = 1f - MathF.Abs(channel.Elevation) * 0.003f;

                // Add ITD approximation (interaural time difference)
                // Simplified: just use level differences

                leftSum += sample * panL * panL * elFactor;
                rightSum += sample * panR * panR * elFactor;
            }

            _binauralMixBuffer[dstOffset] = leftSum;
            _binauralMixBuffer[dstOffset + 1] = rightSum;
        }

        // Copy binaural mix back to first two channels
        // and zero out the rest
        for (int frame = 0; frame < frames; frame++)
        {
            int bufOffset = offset + frame * BedChannels;
            buffer[bufOffset] = _binauralMixBuffer[frame * 2];
            buffer[bufOffset + 1] = _binauralMixBuffer[frame * 2 + 1];

            for (int ch = 2; ch < BedChannels; ch++)
            {
                buffer[bufOffset + ch] = 0f;
            }
        }
    }

    /// <summary>
    /// Ensures internal buffers are large enough.
    /// </summary>
    private void EnsureBuffers(int frames)
    {
        int bedSamples = frames * BedChannels;

        if (_bedBuffer.Length < bedSamples)
        {
            _bedBuffer = new float[bedSamples];
        }

        if (_objectMixBuffer.Length < bedSamples)
        {
            _objectMixBuffer = new float[bedSamples];
        }

        if (_surroundBuffer.Length < bedSamples)
        {
            _surroundBuffer = new float[bedSamples];
        }
    }

    /// <summary>
    /// Normalizes angle to -180 to 180 range.
    /// </summary>
    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    #region Static Factory Methods

    /// <summary>
    /// Creates a renderer with a single centered object.
    /// </summary>
    public static DolbyAtmosRenderer CreateWithCenterObject(ISampleProvider source, int sampleRate)
    {
        var renderer = new DolbyAtmosRenderer(sampleRate);
        var obj = renderer.CreateObject(source, "Center");
        if (obj != null)
        {
            obj.X = 0f;
            obj.Y = 0f;
            obj.Z = 1f;
        }
        return renderer;
    }

    /// <summary>
    /// Creates a renderer configured for binaural output.
    /// </summary>
    public static DolbyAtmosRenderer CreateBinaural(int sampleRate)
    {
        var renderer = new DolbyAtmosRenderer(sampleRate);
        renderer.BinauralMode = BinauralMode.Simple;
        return renderer;
    }

    #endregion
}
