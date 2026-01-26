// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// A sample provider that serves as a sidechain source.
/// Buffers audio from a source and provides it to multiple consumers.
/// Supports level detection and monitoring for sidechain triggering.
/// </summary>
public class SidechainInput : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly object _lock = new();
    private readonly float[] _ringBuffer;
    private readonly int _ringBufferSize;
    private int _writePosition;
    private int _samplesAvailable;

    // Envelope follower state
    private float[] _envelope;
    private float _peakLevel;
    private float _rmsLevel;

    // High-pass filter state for sidechain filtering
    private float[] _filterState;
    private float _filterFrequency;
    private float _filterCoefficient;

    /// <summary>
    /// Creates a new sidechain input from an audio source.
    /// </summary>
    /// <param name="source">The audio source to use as sidechain</param>
    /// <param name="bufferSizeMs">Buffer size in milliseconds (default 100ms)</param>
    public SidechainInput(ISampleProvider source, int bufferSizeMs = 100)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));

        int channels = source.WaveFormat.Channels;
        _ringBufferSize = (source.WaveFormat.SampleRate * channels * bufferSizeMs) / 1000;
        _ringBuffer = new float[_ringBufferSize];
        _envelope = new float[channels];
        _filterState = new float[channels];
        _filterFrequency = 0f;
        _filterCoefficient = 0f;
    }

    /// <summary>
    /// Gets the wave format of the sidechain input.
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets the name identifier for this sidechain input.
    /// </summary>
    public string Name { get; set; } = "Sidechain";

    /// <summary>
    /// Gets the current peak level (0.0 - 1.0+).
    /// Updated during Read operations.
    /// </summary>
    public float PeakLevel => _peakLevel;

    /// <summary>
    /// Gets the current RMS level (0.0 - 1.0+).
    /// Updated during Read operations.
    /// </summary>
    public float RmsLevel => _rmsLevel;

    /// <summary>
    /// Gets the current envelope level per channel.
    /// </summary>
    public ReadOnlySpan<float> EnvelopeLevels => _envelope;

    /// <summary>
    /// Gets or sets the high-pass filter frequency in Hz.
    /// Set to 0 to disable filtering.
    /// </summary>
    public float FilterFrequency
    {
        get => _filterFrequency;
        set
        {
            _filterFrequency = Math.Max(0f, value);
            UpdateFilterCoefficient();
        }
    }

    /// <summary>
    /// Gets or sets the envelope attack time in seconds.
    /// </summary>
    public float EnvelopeAttack { get; set; } = 0.001f;

    /// <summary>
    /// Gets or sets the envelope release time in seconds.
    /// </summary>
    public float EnvelopeRelease { get; set; } = 0.05f;

    /// <summary>
    /// Gets or sets the input gain (0.1 - 10.0).
    /// </summary>
    public float Gain { get; set; } = 1.0f;

    /// <summary>
    /// Gets the number of samples currently available in the buffer.
    /// </summary>
    public int SamplesAvailable
    {
        get
        {
            lock (_lock)
            {
                return _samplesAvailable;
            }
        }
    }

    /// <summary>
    /// Reads audio samples from the sidechain source.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        // Read from the underlying source
        int samplesRead = _source.Read(buffer, offset, count);

        if (samplesRead == 0)
            return 0;

        int channels = WaveFormat.Channels;
        int sampleRate = WaveFormat.SampleRate;
        float gain = Math.Clamp(Gain, 0.1f, 10f);

        // Calculate envelope coefficients
        float attackCoeff = MathF.Exp(-1f / (EnvelopeAttack * sampleRate));
        float releaseCoeff = MathF.Exp(-1f / (EnvelopeRelease * sampleRate));

        float sumSquared = 0f;
        float peak = 0f;

        // Process samples
        for (int i = 0; i < samplesRead; i += channels)
        {
            for (int ch = 0; ch < channels; ch++)
            {
                int idx = offset + i + ch;
                float sample = buffer[idx] * gain;

                // Apply high-pass filter if enabled
                if (_filterFrequency > 0f)
                {
                    float filtered = sample - _filterState[ch];
                    _filterState[ch] += filtered * _filterCoefficient;
                    sample = filtered;
                }

                buffer[idx] = sample;

                // Update envelope follower
                float absSample = MathF.Abs(sample);
                float coeff = absSample > _envelope[ch] ? attackCoeff : releaseCoeff;
                _envelope[ch] = absSample + coeff * (_envelope[ch] - absSample);

                // Track peak and RMS
                peak = MathF.Max(peak, absSample);
                sumSquared += sample * sample;
            }
        }

        // Update levels
        _peakLevel = peak;
        _rmsLevel = MathF.Sqrt(sumSquared / samplesRead);

        // Store in ring buffer for potential later use
        lock (_lock)
        {
            for (int i = 0; i < samplesRead; i++)
            {
                _ringBuffer[_writePosition] = buffer[offset + i];
                _writePosition = (_writePosition + 1) % _ringBufferSize;
            }
            _samplesAvailable = Math.Min(_samplesAvailable + samplesRead, _ringBufferSize);
        }

        return samplesRead;
    }

    /// <summary>
    /// Gets the current envelope level in decibels.
    /// </summary>
    /// <param name="channel">The channel index</param>
    /// <returns>The envelope level in dB</returns>
    public float GetEnvelopeDb(int channel = 0)
    {
        if (channel < 0 || channel >= _envelope.Length)
            return -100f;

        float level = _envelope[channel];
        if (level <= 0f)
            return -100f;

        return 20f * MathF.Log10(level);
    }

    /// <summary>
    /// Checks if the sidechain level exceeds a threshold.
    /// </summary>
    /// <param name="thresholdDb">The threshold in decibels</param>
    /// <returns>True if the level exceeds the threshold</returns>
    public bool IsTriggered(float thresholdDb)
    {
        for (int ch = 0; ch < _envelope.Length; ch++)
        {
            if (GetEnvelopeDb(ch) > thresholdDb)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Resets the envelope follower and filter state.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_envelope);
        Array.Clear(_filterState);
        _peakLevel = 0f;
        _rmsLevel = 0f;

        lock (_lock)
        {
            Array.Clear(_ringBuffer);
            _writePosition = 0;
            _samplesAvailable = 0;
        }
    }

    private void UpdateFilterCoefficient()
    {
        if (_filterFrequency <= 0f || WaveFormat.SampleRate <= 0)
        {
            _filterCoefficient = 0f;
            return;
        }

        // Simple one-pole high-pass filter coefficient
        float rc = 1f / (2f * MathF.PI * _filterFrequency);
        float dt = 1f / WaveFormat.SampleRate;
        _filterCoefficient = dt / (rc + dt);
    }
}

/// <summary>
/// A buffered sidechain input that can provide samples to multiple consumers
/// without re-reading from the source.
/// </summary>
public class BufferedSidechainInput : ISampleProvider
{
    private readonly SidechainInput _sidechainInput;
    private readonly float[] _sharedBuffer;
    private readonly object _bufferLock = new();
    private int _bufferSamplesAvailable;
    private int _bufferReadPosition;

    /// <summary>
    /// Creates a buffered sidechain input.
    /// </summary>
    /// <param name="source">The audio source</param>
    /// <param name="bufferSizeMs">Buffer size in milliseconds</param>
    public BufferedSidechainInput(ISampleProvider source, int bufferSizeMs = 100)
    {
        _sidechainInput = new SidechainInput(source, bufferSizeMs);
        int bufferSize = (source.WaveFormat.SampleRate * source.WaveFormat.Channels * bufferSizeMs) / 1000;
        _sharedBuffer = new float[bufferSize];
    }

    /// <summary>
    /// Gets the underlying sidechain input for monitoring.
    /// </summary>
    public SidechainInput SidechainInput => _sidechainInput;

    /// <summary>
    /// Gets the wave format.
    /// </summary>
    public WaveFormat WaveFormat => _sidechainInput.WaveFormat;

    /// <summary>
    /// Fills the shared buffer with new samples from the source.
    /// Call this once per audio processing cycle before any consumers read.
    /// </summary>
    /// <param name="sampleCount">Number of samples to read</param>
    /// <returns>Number of samples actually read</returns>
    public int FillBuffer(int sampleCount)
    {
        lock (_bufferLock)
        {
            int toRead = Math.Min(sampleCount, _sharedBuffer.Length);
            _bufferSamplesAvailable = _sidechainInput.Read(_sharedBuffer, 0, toRead);
            _bufferReadPosition = 0;
            return _bufferSamplesAvailable;
        }
    }

    /// <summary>
    /// Reads samples from the shared buffer.
    /// Multiple consumers can call this to get the same sidechain data.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        lock (_bufferLock)
        {
            int available = _bufferSamplesAvailable - _bufferReadPosition;
            int toRead = Math.Min(count, available);

            if (toRead <= 0)
                return 0;

            Array.Copy(_sharedBuffer, _bufferReadPosition, buffer, offset, toRead);
            _bufferReadPosition += toRead;
            return toRead;
        }
    }

    /// <summary>
    /// Resets the buffer read position to allow re-reading.
    /// </summary>
    public void ResetReadPosition()
    {
        lock (_bufferLock)
        {
            _bufferReadPosition = 0;
        }
    }
}
