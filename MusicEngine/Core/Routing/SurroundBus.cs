using NAudio.Wave;

namespace MusicEngine.Core.Routing;

/// <summary>
/// Surround sound mixing bus.
/// Mixes multiple surround audio sources and provides downmixing capabilities.
/// </summary>
public class SurroundBus : ISampleProvider
{
    private readonly object _lock = new();
    private readonly List<ISampleProvider> _inputs = new();
    private readonly SurroundFormat _format;
    private readonly float[] _mixBuffer;
    private readonly float[] _inputBuffer;
    private readonly int _sampleRate;

    /// <summary>
    /// Output wave format (multichannel).
    /// </summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// The surround format of this bus.
    /// </summary>
    public SurroundFormat Format => _format;

    /// <summary>
    /// Master volume for the entire bus (0.0 to 2.0).
    /// </summary>
    public float MasterVolume { get; set; } = 1.0f;

    /// <summary>
    /// Per-channel trim levels for calibration.
    /// </summary>
    public float[] ChannelLevels { get; }

    /// <summary>
    /// Mute state for the bus.
    /// </summary>
    public bool Mute { get; set; }

    /// <summary>
    /// Solo state for the bus.
    /// </summary>
    public bool Solo { get; set; }

    /// <summary>
    /// Bus name for identification.
    /// </summary>
    public string Name { get; set; } = "Surround Bus";

    /// <summary>
    /// Creates a new surround bus.
    /// </summary>
    /// <param name="format">The surround format</param>
    /// <param name="sampleRate">Audio sample rate (e.g., 44100, 48000)</param>
    public SurroundBus(SurroundFormat format, int sampleRate)
    {
        _format = format;
        _sampleRate = sampleRate;

        int channelCount = format.GetChannelCount();
        ChannelLevels = new float[channelCount];

        // Initialize all channel levels to unity
        for (int i = 0; i < channelCount; i++)
        {
            ChannelLevels[i] = 1.0f;
        }

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount);

        // Allocate buffers (1 second max)
        int maxSamples = sampleRate * channelCount;
        _mixBuffer = new float[maxSamples];
        _inputBuffer = new float[maxSamples];
    }

    /// <summary>
    /// Adds an audio source to this bus.
    /// Source must match the bus channel count and sample rate.
    /// </summary>
    public void AddInput(ISampleProvider source)
    {
        if (source.WaveFormat.SampleRate != _sampleRate)
        {
            throw new ArgumentException(
                $"Source sample rate ({source.WaveFormat.SampleRate}) must match bus sample rate ({_sampleRate})",
                nameof(source));
        }

        if (source.WaveFormat.Channels != _format.GetChannelCount())
        {
            throw new ArgumentException(
                $"Source channel count ({source.WaveFormat.Channels}) must match bus channel count ({_format.GetChannelCount()})",
                nameof(source));
        }

        lock (_lock)
        {
            _inputs.Add(source);
        }
    }

    /// <summary>
    /// Removes an audio source from this bus.
    /// </summary>
    public void RemoveInput(ISampleProvider source)
    {
        lock (_lock)
        {
            _inputs.Remove(source);
        }
    }

    /// <summary>
    /// Clears all inputs from this bus.
    /// </summary>
    public void ClearInputs()
    {
        lock (_lock)
        {
            _inputs.Clear();
        }
    }

    /// <summary>
    /// Gets the number of current inputs.
    /// </summary>
    public int InputCount
    {
        get
        {
            lock (_lock)
            {
                return _inputs.Count;
            }
        }
    }

    /// <summary>
    /// Reads and mixes all inputs.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int channelCount = _format.GetChannelCount();

        // Clear mix buffer
        Array.Clear(_mixBuffer, 0, count);

        // Get a snapshot of inputs to avoid holding lock during read
        ISampleProvider[] inputSnapshot;
        lock (_lock)
        {
            if (_inputs.Count == 0)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

            inputSnapshot = _inputs.ToArray();
        }

        // Mix all inputs
        foreach (var input in inputSnapshot)
        {
            int samplesRead = input.Read(_inputBuffer, 0, count);

            for (int i = 0; i < samplesRead; i++)
            {
                _mixBuffer[i] += _inputBuffer[i];
            }
        }

        // Apply channel levels and master volume
        float masterGain = Mute ? 0f : MasterVolume;

        for (int frame = 0; frame < count / channelCount; frame++)
        {
            int frameOffset = frame * channelCount;

            for (int ch = 0; ch < channelCount; ch++)
            {
                _mixBuffer[frameOffset + ch] *= ChannelLevels[ch] * masterGain;
            }
        }

        // Copy to output
        Array.Copy(_mixBuffer, 0, buffer, offset, count);

        return count;
    }

    /// <summary>
    /// Downmixes surround audio to stereo for monitoring.
    /// Uses ITU-R BS.775-1 downmix coefficients.
    /// </summary>
    /// <param name="surroundBuffer">Source surround buffer</param>
    /// <param name="sampleCount">Number of samples (total, including all channels)</param>
    /// <returns>Stereo buffer (2 channels)</returns>
    public float[] DownmixToStereo(float[] surroundBuffer, int sampleCount)
    {
        int channelCount = _format.GetChannelCount();
        int frameCount = sampleCount / channelCount;
        var stereoBuffer = new float[frameCount * 2];

        // Get downmix coefficients for this format
        var (leftCoeffs, rightCoeffs) = GetDownmixCoefficients();

        for (int frame = 0; frame < frameCount; frame++)
        {
            int surroundOffset = frame * channelCount;
            int stereoOffset = frame * 2;

            float left = 0f, right = 0f;

            for (int ch = 0; ch < channelCount; ch++)
            {
                float sample = surroundBuffer[surroundOffset + ch];
                left += sample * leftCoeffs[ch];
                right += sample * rightCoeffs[ch];
            }

            stereoBuffer[stereoOffset] = left;
            stereoBuffer[stereoOffset + 1] = right;
        }

        return stereoBuffer;
    }

    /// <summary>
    /// Creates a stereo downmix sample provider that wraps this bus.
    /// </summary>
    public ISampleProvider CreateStereoDownmix()
    {
        return new StereoDownmixProvider(this);
    }

    /// <summary>
    /// Gets ITU-R BS.775-1 downmix coefficients.
    /// Returns (leftCoeffs, rightCoeffs) arrays indexed by channel.
    /// </summary>
    private (float[] Left, float[] Right) GetDownmixCoefficients()
    {
        int channelCount = _format.GetChannelCount();
        var leftCoeffs = new float[channelCount];
        var rightCoeffs = new float[channelCount];

        // Standard coefficients
        const float centerCoeff = 0.707f;  // -3 dB
        const float surroundCoeff = 0.707f; // -3 dB
        const float lfeCoeff = 0.0f;        // LFE typically not included in downmix

        var channelTypes = _format.GetChannelTypes();

        for (int i = 0; i < channelCount; i++)
        {
            switch (channelTypes[i])
            {
                case SurroundChannelType.Left:
                    leftCoeffs[i] = 1.0f;
                    rightCoeffs[i] = 0.0f;
                    break;

                case SurroundChannelType.Right:
                    leftCoeffs[i] = 0.0f;
                    rightCoeffs[i] = 1.0f;
                    break;

                case SurroundChannelType.Center:
                    leftCoeffs[i] = centerCoeff;
                    rightCoeffs[i] = centerCoeff;
                    break;

                case SurroundChannelType.LeftSurround:
                case SurroundChannelType.LeftSideSurround:
                case SurroundChannelType.LeftRearSurround:
                    leftCoeffs[i] = surroundCoeff;
                    rightCoeffs[i] = 0.0f;
                    break;

                case SurroundChannelType.RightSurround:
                case SurroundChannelType.RightSideSurround:
                case SurroundChannelType.RightRearSurround:
                    leftCoeffs[i] = 0.0f;
                    rightCoeffs[i] = surroundCoeff;
                    break;

                case SurroundChannelType.LFE:
                    leftCoeffs[i] = lfeCoeff;
                    rightCoeffs[i] = lfeCoeff;
                    break;

                case SurroundChannelType.TopFrontLeft:
                case SurroundChannelType.TopRearLeft:
                    leftCoeffs[i] = surroundCoeff;
                    rightCoeffs[i] = 0.0f;
                    break;

                case SurroundChannelType.TopFrontRight:
                case SurroundChannelType.TopRearRight:
                    leftCoeffs[i] = 0.0f;
                    rightCoeffs[i] = surroundCoeff;
                    break;
            }
        }

        return (leftCoeffs, rightCoeffs);
    }

    /// <summary>
    /// Sets the channel level for a specific channel by type.
    /// </summary>
    public void SetChannelLevel(SurroundChannelType channelType, float level)
    {
        var channelTypes = _format.GetChannelTypes();
        for (int i = 0; i < channelTypes.Length; i++)
        {
            if (channelTypes[i] == channelType)
            {
                ChannelLevels[i] = Math.Clamp(level, 0f, 2f);
                break;
            }
        }
    }

    /// <summary>
    /// Gets the channel level for a specific channel by type.
    /// </summary>
    public float GetChannelLevel(SurroundChannelType channelType)
    {
        var channelTypes = _format.GetChannelTypes();
        for (int i = 0; i < channelTypes.Length; i++)
        {
            if (channelTypes[i] == channelType)
            {
                return ChannelLevels[i];
            }
        }

        return 1.0f;
    }

    /// <summary>
    /// Internal sample provider that downmixes surround to stereo.
    /// </summary>
    private class StereoDownmixProvider : ISampleProvider
    {
        private readonly SurroundBus _bus;
        private readonly float[] _surroundBuffer;

        public WaveFormat WaveFormat { get; }

        public StereoDownmixProvider(SurroundBus bus)
        {
            _bus = bus;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(bus.WaveFormat.SampleRate, 2);
            _surroundBuffer = new float[bus.WaveFormat.SampleRate * bus._format.GetChannelCount()];
        }

        public int Read(float[] buffer, int offset, int count)
        {
            // Calculate how many surround samples we need
            int stereoFrames = count / 2;
            int surroundChannels = _bus._format.GetChannelCount();
            int surroundSampleCount = stereoFrames * surroundChannels;

            // Read surround audio
            int surroundRead = _bus.Read(_surroundBuffer, 0, surroundSampleCount);

            // Downmix to stereo
            var stereoData = _bus.DownmixToStereo(_surroundBuffer, surroundRead);

            // Copy to output
            int stereoSamplesToWrite = Math.Min(stereoData.Length, count);
            Array.Copy(stereoData, 0, buffer, offset, stereoSamplesToWrite);

            return stereoSamplesToWrite;
        }
    }
}
