// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio/MIDI routing component.

using NAudio.Wave;

namespace MusicEngine.Core.Routing;

/// <summary>
/// Ambisonic order (determines spatial resolution).
/// </summary>
public enum AmbisonicOrder
{
    /// <summary>
    /// First order ambisonics (4 channels: W, X, Y, Z).
    /// </summary>
    First = 1,

    /// <summary>
    /// Second order ambisonics (9 channels).
    /// </summary>
    Second = 2,

    /// <summary>
    /// Third order ambisonics (16 channels).
    /// </summary>
    Third = 3
}

/// <summary>
/// Ambisonic normalization convention.
/// </summary>
public enum AmbisonicNormalization
{
    /// <summary>
    /// SN3D (Schmidt semi-normalized) - commonly used in AmbiX.
    /// </summary>
    SN3D,

    /// <summary>
    /// N3D (full 3D normalization).
    /// </summary>
    N3D,

    /// <summary>
    /// FuMa (Furse-Malham) - legacy B-format.
    /// </summary>
    FuMa
}

/// <summary>
/// Represents a sound source in the ambisonic field.
/// </summary>
public class AmbisonicSource
{
    /// <summary>
    /// Unique identifier for this source.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the source.
    /// </summary>
    public string Name { get; set; } = "Source";

    /// <summary>
    /// The audio source provider.
    /// </summary>
    public ISampleProvider? Source { get; set; }

    /// <summary>
    /// Azimuth angle in degrees (-180 to 180). 0 = front, 90 = right.
    /// </summary>
    public float Azimuth { get; set; }

    /// <summary>
    /// Elevation angle in degrees (-90 to 90). 0 = horizon, 90 = zenith.
    /// </summary>
    public float Elevation { get; set; }

    /// <summary>
    /// Distance from center (affects gain, 0-1 range for near-field).
    /// </summary>
    public float Distance { get; set; } = 1f;

    /// <summary>
    /// Source spread/width (0 = point source, 1 = omnidirectional).
    /// </summary>
    public float Spread { get; set; }

    /// <summary>
    /// Source gain multiplier.
    /// </summary>
    public float Gain { get; set; } = 1f;

    /// <summary>
    /// Whether this source is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Internal read buffer.
    /// </summary>
    internal float[] Buffer { get; set; } = Array.Empty<float>();
}

/// <summary>
/// B-format ambisonic encoder for VR/360 audio applications.
/// </summary>
/// <remarks>
/// Features:
/// - First, second, and third order ambisonics
/// - Multiple normalization conventions (SN3D, N3D, FuMa)
/// - Sound source positioning with spread control
/// - Soundfield rotation support
/// - Built-in binaural decoder for headphone monitoring
/// </remarks>
public class AmbisonicEncoder : ISampleProvider
{
    private readonly List<AmbisonicSource> _sources = new();
    private readonly AmbisonicOrder _order;
    private readonly int _channelCount;
    private readonly WaveFormat _waveFormat;
    private readonly float[][] _encodingMatrix;

    // Rotation state (Euler angles in degrees)
    private float _yaw;    // Rotation around Y axis
    private float _pitch;  // Rotation around X axis
    private float _roll;   // Rotation around Z axis
    private float[,] _rotationMatrix = new float[3, 3];
    private bool _rotationDirty = true;

    // Binaural decoder
    private bool _binauralEnabled;
    private float[][] _binauralFilters = null!;
    private float[][] _convolutionHistory = null!;
    private int _convolutionIndex;
    private const int BinauralFilterLength = 128;

    // Normalization
    private AmbisonicNormalization _normalization;

    /// <summary>
    /// Creates a new ambisonic encoder.
    /// </summary>
    /// <param name="sampleRate">Sample rate for output.</param>
    /// <param name="order">Ambisonic order (1, 2, or 3).</param>
    /// <param name="normalization">Normalization convention.</param>
    public AmbisonicEncoder(int sampleRate, AmbisonicOrder order = AmbisonicOrder.First,
                            AmbisonicNormalization normalization = AmbisonicNormalization.SN3D)
    {
        _order = order;
        _normalization = normalization;

        // Channel count: (order + 1)^2
        _channelCount = ((int)order + 1) * ((int)order + 1);

        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, _channelCount);

        // Initialize encoding matrix (coefficients for each ACN channel)
        _encodingMatrix = new float[_channelCount][];
        for (int i = 0; i < _channelCount; i++)
        {
            _encodingMatrix[i] = new float[_channelCount];
        }

        InitializeBinauralDecoder(sampleRate);
    }

    /// <summary>
    /// Gets the output wave format.
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Gets the ambisonic order.
    /// </summary>
    public AmbisonicOrder Order => _order;

    /// <summary>
    /// Gets the number of ambisonic channels.
    /// </summary>
    public int ChannelCount => _channelCount;

    /// <summary>
    /// Gets the collection of sources.
    /// </summary>
    public IReadOnlyList<AmbisonicSource> Sources => _sources.AsReadOnly();

    /// <summary>
    /// Gets or sets the normalization convention.
    /// </summary>
    public AmbisonicNormalization Normalization
    {
        get => _normalization;
        set => _normalization = value;
    }

    /// <summary>
    /// Gets or sets whether binaural decoding is enabled.
    /// When enabled, output is stereo binaural instead of B-format.
    /// </summary>
    public bool BinauralEnabled
    {
        get => _binauralEnabled;
        set => _binauralEnabled = value;
    }

    /// <summary>
    /// Gets or sets the yaw rotation in degrees.
    /// </summary>
    public float Yaw
    {
        get => _yaw;
        set
        {
            _yaw = NormalizeAngle(value);
            _rotationDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the pitch rotation in degrees.
    /// </summary>
    public float Pitch
    {
        get => _pitch;
        set
        {
            _pitch = Math.Clamp(value, -90f, 90f);
            _rotationDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the roll rotation in degrees.
    /// </summary>
    public float Roll
    {
        get => _roll;
        set
        {
            _roll = NormalizeAngle(value);
            _rotationDirty = true;
        }
    }

    /// <summary>
    /// Adds a sound source to the encoder.
    /// </summary>
    public void AddSource(AmbisonicSource source)
    {
        _sources.Add(source);
    }

    /// <summary>
    /// Removes a sound source from the encoder.
    /// </summary>
    public bool RemoveSource(Guid sourceId)
    {
        var source = _sources.FirstOrDefault(s => s.Id == sourceId);
        if (source != null)
        {
            _sources.Remove(source);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Creates and adds a new source.
    /// </summary>
    public AmbisonicSource? CreateSource(ISampleProvider audioSource, string name = "Source")
    {
        var source = new AmbisonicSource
        {
            Source = audioSource,
            Name = name
        };
        _sources.Add(source);
        return source;
    }

    /// <summary>
    /// Sets the rotation from head tracking data.
    /// </summary>
    public void SetRotation(float yaw, float pitch, float roll)
    {
        _yaw = NormalizeAngle(yaw);
        _pitch = Math.Clamp(pitch, -90f, 90f);
        _roll = NormalizeAngle(roll);
        _rotationDirty = true;
    }

    /// <summary>
    /// Reads encoded ambisonic audio.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int outputChannels = _binauralEnabled ? 2 : _channelCount;
        int frames = count / outputChannels;

        // Update rotation matrix if needed
        if (_rotationDirty)
        {
            UpdateRotationMatrix();
            _rotationDirty = false;
        }

        // Clear output buffer
        Array.Clear(buffer, offset, count);

        // Temporary B-format buffer for binaural processing
        float[]? bFormatBuffer = _binauralEnabled ? new float[frames * _channelCount] : null;
        float[] outputBuffer = bFormatBuffer ?? buffer;
        int outputOffset = bFormatBuffer != null ? 0 : offset;

        // Process each source
        foreach (var source in _sources.Where(s => s.IsActive && s.Source != null))
        {
            // Read source audio
            int sourceChannels = source.Source!.WaveFormat.Channels;
            int sourceSamples = frames * sourceChannels;

            if (source.Buffer.Length < sourceSamples)
            {
                source.Buffer = new float[sourceSamples];
            }

            int samplesRead = source.Source.Read(source.Buffer, 0, sourceSamples);
            if (samplesRead == 0)
                continue;

            int sourceFrames = samplesRead / sourceChannels;

            // Calculate ambisonic coefficients for this source
            float[] coefficients = CalculateCoefficients(source.Azimuth, source.Elevation, source.Spread);

            // Apply rotation if any
            if (_yaw != 0 || _pitch != 0 || _roll != 0)
            {
                coefficients = RotateCoefficients(coefficients);
            }

            // Encode source to B-format
            for (int frame = 0; frame < sourceFrames; frame++)
            {
                // Get mono sample
                float sample;
                if (sourceChannels == 1)
                {
                    sample = source.Buffer[frame];
                }
                else
                {
                    sample = 0f;
                    for (int ch = 0; ch < sourceChannels; ch++)
                    {
                        sample += source.Buffer[frame * sourceChannels + ch];
                    }
                    sample /= sourceChannels;
                }

                sample *= source.Gain;

                // Apply distance attenuation
                float distanceGain = 1f / (1f + source.Distance);
                sample *= distanceGain;

                // Encode to each ambisonic channel
                int frameOffset = outputOffset + frame * _channelCount;
                for (int ch = 0; ch < _channelCount; ch++)
                {
                    outputBuffer[frameOffset + ch] += sample * coefficients[ch];
                }
            }
        }

        // Binaural decode if enabled
        if (_binauralEnabled && bFormatBuffer != null)
        {
            DecodeBinaural(bFormatBuffer, buffer, offset, frames);
        }

        return frames * outputChannels;
    }

    /// <summary>
    /// Calculates ambisonic encoding coefficients for a direction.
    /// Uses ACN channel ordering.
    /// </summary>
    private float[] CalculateCoefficients(float azimuth, float elevation, float spread)
    {
        float[] coeffs = new float[_channelCount];

        // Convert to radians
        float az = azimuth * MathF.PI / 180f;
        float el = elevation * MathF.PI / 180f;

        float cosEl = MathF.Cos(el);
        float sinEl = MathF.Sin(el);
        float cosAz = MathF.Cos(az);
        float sinAz = MathF.Sin(az);

        // Cartesian coordinates
        float x = cosEl * sinAz;  // Right
        float y = sinEl;          // Up
        float z = cosEl * cosAz;  // Front

        // Get normalization factors
        float[] norm = GetNormalizationFactors();

        // ACN 0: W (omnidirectional)
        coeffs[0] = norm[0];

        if (_order >= AmbisonicOrder.First)
        {
            // ACN 1: Y (front-back)
            coeffs[1] = norm[1] * y;
            // ACN 2: Z (left-right)
            coeffs[2] = norm[2] * z;
            // ACN 3: X (up-down)
            coeffs[3] = norm[3] * x;
        }

        if (_order >= AmbisonicOrder.Second)
        {
            // Second order spherical harmonics
            // ACN 4: V
            coeffs[4] = norm[4] * x * y;
            // ACN 5: T
            coeffs[5] = norm[5] * y * z;
            // ACN 6: R
            coeffs[6] = norm[6] * (3f * z * z - 1f) * 0.5f;
            // ACN 7: S
            coeffs[7] = norm[7] * x * z;
            // ACN 8: U
            coeffs[8] = norm[8] * (x * x - y * y) * 0.5f;
        }

        if (_order >= AmbisonicOrder.Third)
        {
            // Third order spherical harmonics (simplified)
            float x2 = x * x;
            float y2 = y * y;
            float z2 = z * z;

            coeffs[9] = norm[9] * y * (3f * x2 - y2);
            coeffs[10] = norm[10] * x * y * z;
            coeffs[11] = norm[11] * y * (5f * z2 - 1f);
            coeffs[12] = norm[12] * z * (5f * z2 - 3f);
            coeffs[13] = norm[13] * x * (5f * z2 - 1f);
            coeffs[14] = norm[14] * z * (x2 - y2);
            coeffs[15] = norm[15] * x * (x2 - 3f * y2);
        }

        // Apply spread (blend towards omnidirectional)
        if (spread > 0)
        {
            float spreadFactor = 1f - spread;
            for (int i = 1; i < _channelCount; i++)
            {
                coeffs[i] *= spreadFactor;
            }
        }

        return coeffs;
    }

    /// <summary>
    /// Gets normalization factors based on selected convention.
    /// </summary>
    private float[] GetNormalizationFactors()
    {
        float[] norm = new float[_channelCount];

        switch (_normalization)
        {
            case AmbisonicNormalization.SN3D:
                // SN3D normalization (AmbiX default)
                norm[0] = 1f;
                if (_order >= AmbisonicOrder.First)
                {
                    norm[1] = norm[2] = norm[3] = 1f;
                }
                if (_order >= AmbisonicOrder.Second)
                {
                    norm[4] = norm[5] = norm[7] = MathF.Sqrt(3f);
                    norm[6] = 1f;
                    norm[8] = MathF.Sqrt(3f) / 2f;
                }
                if (_order >= AmbisonicOrder.Third)
                {
                    for (int i = 9; i < 16; i++)
                    {
                        norm[i] = 1f;  // Simplified
                    }
                }
                break;

            case AmbisonicNormalization.N3D:
                // N3D normalization
                norm[0] = 1f;
                if (_order >= AmbisonicOrder.First)
                {
                    norm[1] = norm[2] = norm[3] = MathF.Sqrt(3f);
                }
                if (_order >= AmbisonicOrder.Second)
                {
                    norm[4] = norm[5] = norm[7] = MathF.Sqrt(15f);
                    norm[6] = MathF.Sqrt(5f);
                    norm[8] = MathF.Sqrt(15f) / 2f;
                }
                if (_order >= AmbisonicOrder.Third)
                {
                    for (int i = 9; i < 16; i++)
                    {
                        norm[i] = MathF.Sqrt(7f);  // Simplified
                    }
                }
                break;

            case AmbisonicNormalization.FuMa:
                // FuMa (legacy) normalization
                norm[0] = 1f / MathF.Sqrt(2f);
                if (_order >= AmbisonicOrder.First)
                {
                    norm[1] = norm[2] = norm[3] = 1f;
                }
                if (_order >= AmbisonicOrder.Second)
                {
                    for (int i = 4; i < 9; i++)
                    {
                        norm[i] = 1f;
                    }
                }
                if (_order >= AmbisonicOrder.Third)
                {
                    for (int i = 9; i < 16; i++)
                    {
                        norm[i] = 1f;
                    }
                }
                break;
        }

        return norm;
    }

    /// <summary>
    /// Rotates ambisonic coefficients using current rotation matrix.
    /// </summary>
    private float[] RotateCoefficients(float[] coeffs)
    {
        float[] rotated = new float[_channelCount];

        // W channel is rotation-invariant
        rotated[0] = coeffs[0];

        if (_order >= AmbisonicOrder.First)
        {
            // Rotate first-order components (X, Y, Z)
            float x = coeffs[3];
            float y = coeffs[1];
            float z = coeffs[2];

            rotated[1] = _rotationMatrix[0, 0] * y + _rotationMatrix[0, 1] * z + _rotationMatrix[0, 2] * x;
            rotated[2] = _rotationMatrix[1, 0] * y + _rotationMatrix[1, 1] * z + _rotationMatrix[1, 2] * x;
            rotated[3] = _rotationMatrix[2, 0] * y + _rotationMatrix[2, 1] * z + _rotationMatrix[2, 2] * x;
        }

        // Higher order rotation is more complex - simplified pass-through for now
        for (int i = 4; i < _channelCount; i++)
        {
            rotated[i] = coeffs[i];
        }

        return rotated;
    }

    /// <summary>
    /// Updates the rotation matrix from Euler angles.
    /// </summary>
    private void UpdateRotationMatrix()
    {
        float yawRad = _yaw * MathF.PI / 180f;
        float pitchRad = _pitch * MathF.PI / 180f;
        float rollRad = _roll * MathF.PI / 180f;

        float cy = MathF.Cos(yawRad);
        float sy = MathF.Sin(yawRad);
        float cp = MathF.Cos(pitchRad);
        float sp = MathF.Sin(pitchRad);
        float cr = MathF.Cos(rollRad);
        float sr = MathF.Sin(rollRad);

        // Combined rotation matrix (ZYX order)
        _rotationMatrix[0, 0] = cy * cp;
        _rotationMatrix[0, 1] = cy * sp * sr - sy * cr;
        _rotationMatrix[0, 2] = cy * sp * cr + sy * sr;

        _rotationMatrix[1, 0] = sy * cp;
        _rotationMatrix[1, 1] = sy * sp * sr + cy * cr;
        _rotationMatrix[1, 2] = sy * sp * cr - cy * sr;

        _rotationMatrix[2, 0] = -sp;
        _rotationMatrix[2, 1] = cp * sr;
        _rotationMatrix[2, 2] = cp * cr;
    }

    /// <summary>
    /// Initializes the binaural decoder filters.
    /// </summary>
    private void InitializeBinauralDecoder(int sampleRate)
    {
        // Simplified binaural filters based on HRTF approximation
        _binauralFilters = new float[_channelCount][];
        _convolutionHistory = new float[_channelCount][];

        for (int ch = 0; ch < _channelCount; ch++)
        {
            _binauralFilters[ch] = new float[BinauralFilterLength * 2]; // Left and right
            _convolutionHistory[ch] = new float[BinauralFilterLength];

            // Generate simplified HRTF-like filter
            GenerateBinauralFilter(ch, sampleRate);
        }
    }

    /// <summary>
    /// Generates a simplified binaural filter for an ambisonic channel.
    /// </summary>
    private void GenerateBinauralFilter(int channel, int sampleRate)
    {
        float[] filter = _binauralFilters[channel];

        // Get virtual speaker position for this channel
        float azimuth, elevation;
        GetVirtualSpeakerPosition(channel, out azimuth, out elevation);

        float azRad = azimuth * MathF.PI / 180f;
        float headRadius = 0.0875f;
        float speedOfSound = 343f;

        // ITD in samples
        float itd = (headRadius / speedOfSound) * sampleRate * MathF.Sin(azRad);

        // ILD factor
        float ildL = azimuth < 0 ? 1f : 1f - MathF.Abs(azimuth) / 180f * 0.3f;
        float ildR = azimuth > 0 ? 1f : 1f - MathF.Abs(azimuth) / 180f * 0.3f;

        // Generate impulse responses
        int centerL = BinauralFilterLength / 2 - (int)(itd / 2);
        int centerR = BinauralFilterLength / 2 + (int)(itd / 2);

        for (int i = 0; i < BinauralFilterLength; i++)
        {
            // Left ear
            float distL = i - centerL;
            float sincL = distL == 0 ? 1f : MathF.Sin(MathF.PI * distL * 0.5f) / (MathF.PI * distL * 0.5f);
            float windowL = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (BinauralFilterLength - 1)));
            filter[i] = sincL * windowL * ildL * 0.5f;

            // Right ear
            float distR = i - centerR;
            float sincR = distR == 0 ? 1f : MathF.Sin(MathF.PI * distR * 0.5f) / (MathF.PI * distR * 0.5f);
            float windowR = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (BinauralFilterLength - 1)));
            filter[BinauralFilterLength + i] = sincR * windowR * ildR * 0.5f;
        }
    }

    /// <summary>
    /// Gets virtual speaker position for an ambisonic channel.
    /// </summary>
    private void GetVirtualSpeakerPosition(int channel, out float azimuth, out float elevation)
    {
        // ACN channel to virtual speaker mapping
        switch (channel)
        {
            case 0: azimuth = 0; elevation = 0; break;      // W - omnidirectional
            case 1: azimuth = 0; elevation = 90; break;     // Y - up
            case 2: azimuth = 0; elevation = 0; break;      // Z - front
            case 3: azimuth = 90; elevation = 0; break;     // X - right
            case 4: azimuth = 45; elevation = 45; break;    // V
            case 5: azimuth = 0; elevation = 45; break;     // T
            case 6: azimuth = 0; elevation = 0; break;      // R
            case 7: azimuth = 45; elevation = 0; break;     // S
            case 8: azimuth = 90; elevation = 0; break;     // U
            default: azimuth = 0; elevation = 0; break;
        }
    }

    /// <summary>
    /// Decodes B-format to binaural stereo.
    /// </summary>
    private void DecodeBinaural(float[] bFormat, float[] output, int offset, int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            float leftSum = 0f;
            float rightSum = 0f;

            // Simple decode: convolve each channel with its binaural filter
            for (int ch = 0; ch < _channelCount; ch++)
            {
                float sample = bFormat[frame * _channelCount + ch];

                // Update convolution history
                _convolutionHistory[ch][_convolutionIndex] = sample;

                // Convolve (simplified - just use a few taps)
                int taps = Math.Min(16, BinauralFilterLength);
                for (int t = 0; t < taps; t++)
                {
                    int histIdx = (_convolutionIndex - t + BinauralFilterLength) % BinauralFilterLength;
                    leftSum += _convolutionHistory[ch][histIdx] * _binauralFilters[ch][t];
                    rightSum += _convolutionHistory[ch][histIdx] * _binauralFilters[ch][BinauralFilterLength + t];
                }
            }

            output[offset + frame * 2] = leftSum;
            output[offset + frame * 2 + 1] = rightSum;

            _convolutionIndex = (_convolutionIndex + 1) % BinauralFilterLength;
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

    #region Presets

    /// <summary>
    /// Creates a first-order encoder for VR applications.
    /// </summary>
    public static AmbisonicEncoder CreateVREncoder(int sampleRate)
    {
        var encoder = new AmbisonicEncoder(sampleRate, AmbisonicOrder.First, AmbisonicNormalization.SN3D);
        encoder.BinauralEnabled = true;
        return encoder;
    }

    /// <summary>
    /// Creates a third-order encoder for high-resolution spatial audio.
    /// </summary>
    public static AmbisonicEncoder CreateHighResEncoder(int sampleRate)
    {
        return new AmbisonicEncoder(sampleRate, AmbisonicOrder.Third, AmbisonicNormalization.SN3D);
    }

    /// <summary>
    /// Creates an encoder compatible with legacy B-format.
    /// </summary>
    public static AmbisonicEncoder CreateLegacyEncoder(int sampleRate)
    {
        return new AmbisonicEncoder(sampleRate, AmbisonicOrder.First, AmbisonicNormalization.FuMa);
    }

    #endregion
}
