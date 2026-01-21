//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Sample provider for live monitoring of audio input with level metering.

using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Provides live monitoring capability for audio input.
/// Buffers incoming samples and provides them through the ISampleProvider interface
/// for playback through the output device, enabling real-time monitoring.
/// </summary>
public sealed class MonitoringSampleProvider : ISampleProvider, IDisposable
{
    private readonly WaveFormat _waveFormat;
    private readonly object _bufferLock = new();
    private readonly Queue<float> _sampleQueue = new();
    private readonly int _maxBufferSamples;

    private bool _isEnabled = true;
    private float _volume = 1.0f;
    private float _pan;
    private float _leftPeak;
    private float _rightPeak;
    private float _leftRms;
    private float _rightRms;
    private int _rmsSampleCount;
    private float _leftRmsSum;
    private float _rightRmsSum;
    private const int RmsWindowSamples = 4410; // ~100ms at 44.1kHz

    /// <summary>
    /// Gets the wave format of the monitoring output.
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Gets or sets whether monitoring is enabled.
    /// When disabled, silence is output.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    /// <summary>
    /// Gets or sets the monitoring volume (0.0 to 2.0).
    /// </summary>
    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 2f);
    }

    /// <summary>
    /// Gets or sets the stereo pan (-1.0 = full left, 0.0 = center, 1.0 = full right).
    /// </summary>
    public float Pan
    {
        get => _pan;
        set => _pan = Math.Clamp(value, -1f, 1f);
    }

    /// <summary>
    /// Gets the current peak level for the left channel (0.0 to 1.0).
    /// </summary>
    public float LeftPeak => _leftPeak;

    /// <summary>
    /// Gets the current peak level for the right channel (0.0 to 1.0).
    /// </summary>
    public float RightPeak => _rightPeak;

    /// <summary>
    /// Gets the current RMS level for the left channel (0.0 to 1.0).
    /// </summary>
    public float LeftRms => _leftRms;

    /// <summary>
    /// Gets the current RMS level for the right channel (0.0 to 1.0).
    /// </summary>
    public float RightRms => _rightRms;

    /// <summary>
    /// Gets the current peak level in decibels for the left channel.
    /// </summary>
    public float LeftPeakDb => PeakToDb(_leftPeak);

    /// <summary>
    /// Gets the current peak level in decibels for the right channel.
    /// </summary>
    public float RightPeakDb => PeakToDb(_rightPeak);

    /// <summary>
    /// Gets the current RMS level in decibels for the left channel.
    /// </summary>
    public float LeftRmsDb => PeakToDb(_leftRms);

    /// <summary>
    /// Gets the current RMS level in decibels for the right channel.
    /// </summary>
    public float RightRmsDb => PeakToDb(_rightRms);

    /// <summary>
    /// Gets the number of samples currently buffered.
    /// </summary>
    public int BufferedSamples
    {
        get
        {
            lock (_bufferLock)
            {
                return _sampleQueue.Count;
            }
        }
    }

    /// <summary>
    /// Gets the buffer latency in milliseconds.
    /// </summary>
    public double BufferLatencyMs
    {
        get
        {
            int samples = BufferedSamples;
            return (samples / (double)_waveFormat.Channels) / _waveFormat.SampleRate * 1000.0;
        }
    }

    /// <summary>
    /// Event raised when level meters are updated.
    /// </summary>
    public event EventHandler<LevelMeterEventArgs>? LevelUpdated;

    /// <summary>
    /// Creates a new MonitoringSampleProvider.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of channels (1 or 2).</param>
    /// <param name="bufferMs">Maximum buffer size in milliseconds (default 200ms).</param>
    public MonitoringSampleProvider(int sampleRate = 44100, int channels = 2, int bufferMs = 200)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
        if (channels < 1 || channels > 2)
            throw new ArgumentOutOfRangeException(nameof(channels), "Channels must be 1 or 2.");

        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        _maxBufferSamples = (sampleRate * channels * bufferMs) / 1000;
    }

    /// <summary>
    /// Adds samples from the input device to the monitoring buffer.
    /// </summary>
    /// <param name="samples">The samples to add.</param>
    /// <param name="offset">Offset in the samples array.</param>
    /// <param name="count">Number of samples to add.</param>
    public void AddSamples(float[] samples, int offset, int count)
    {
        if (!_isEnabled) return;

        lock (_bufferLock)
        {
            // Calculate levels from input samples
            UpdateLevels(samples, offset, count);

            // Add samples to queue with volume applied
            for (int i = 0; i < count; i++)
            {
                _sampleQueue.Enqueue(samples[offset + i] * _volume);
            }

            // Remove old samples if buffer is too full
            while (_sampleQueue.Count > _maxBufferSamples)
            {
                _sampleQueue.Dequeue();
            }
        }
    }

    /// <summary>
    /// Adds samples from the input device to the monitoring buffer.
    /// </summary>
    /// <param name="samples">The samples to add.</param>
    public void AddSamples(float[] samples)
    {
        AddSamples(samples, 0, samples.Length);
    }

    /// <summary>
    /// Reads samples for output playback.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        lock (_bufferLock)
        {
            if (!_isEnabled || _sampleQueue.Count == 0)
            {
                // Output silence
                Array.Clear(buffer, offset, count);
                return count;
            }

            int samplesToRead = Math.Min(count, _sampleQueue.Count);
            int actuallyRead = 0;

            if (_waveFormat.Channels == 2)
            {
                // Stereo output with panning
                float leftGain = _pan < 0 ? 1.0f : 1.0f - _pan;
                float rightGain = _pan > 0 ? 1.0f : 1.0f + _pan;

                for (int i = 0; i < samplesToRead - 1; i += 2)
                {
                    if (_sampleQueue.Count >= 2)
                    {
                        buffer[offset + actuallyRead] = _sampleQueue.Dequeue() * leftGain;
                        buffer[offset + actuallyRead + 1] = _sampleQueue.Dequeue() * rightGain;
                        actuallyRead += 2;
                    }
                }
            }
            else
            {
                // Mono output
                for (int i = 0; i < samplesToRead; i++)
                {
                    if (_sampleQueue.Count > 0)
                    {
                        buffer[offset + actuallyRead] = _sampleQueue.Dequeue();
                        actuallyRead++;
                    }
                }
            }

            // Fill remaining with silence
            if (actuallyRead < count)
            {
                Array.Clear(buffer, offset + actuallyRead, count - actuallyRead);
            }

            return count;
        }
    }

    /// <summary>
    /// Clears the monitoring buffer.
    /// </summary>
    public void ClearBuffer()
    {
        lock (_bufferLock)
        {
            _sampleQueue.Clear();
        }
    }

    /// <summary>
    /// Resets the level meters.
    /// </summary>
    public void ResetLevels()
    {
        _leftPeak = 0;
        _rightPeak = 0;
        _leftRms = 0;
        _rightRms = 0;
        _leftRmsSum = 0;
        _rightRmsSum = 0;
        _rmsSampleCount = 0;
    }

    private void UpdateLevels(float[] samples, int offset, int count)
    {
        // Decay peak levels
        _leftPeak *= 0.95f;
        _rightPeak *= 0.95f;

        if (_waveFormat.Channels == 2)
        {
            for (int i = 0; i < count - 1; i += 2)
            {
                float left = Math.Abs(samples[offset + i]);
                float right = Math.Abs(samples[offset + i + 1]);

                // Peak detection
                if (left > _leftPeak) _leftPeak = left;
                if (right > _rightPeak) _rightPeak = right;

                // RMS calculation
                _leftRmsSum += left * left;
                _rightRmsSum += right * right;
                _rmsSampleCount++;

                if (_rmsSampleCount >= RmsWindowSamples)
                {
                    _leftRms = MathF.Sqrt(_leftRmsSum / _rmsSampleCount);
                    _rightRms = MathF.Sqrt(_rightRmsSum / _rmsSampleCount);
                    _leftRmsSum = 0;
                    _rightRmsSum = 0;
                    _rmsSampleCount = 0;

                    // Raise level updated event
                    LevelUpdated?.Invoke(this, new LevelMeterEventArgs(
                        _leftPeak, _rightPeak, _leftRms, _rightRms));
                }
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                float sample = Math.Abs(samples[offset + i]);

                // Peak detection (mono to both channels)
                if (sample > _leftPeak) _leftPeak = sample;
                _rightPeak = _leftPeak;

                // RMS calculation
                _leftRmsSum += sample * sample;
                _rmsSampleCount++;

                if (_rmsSampleCount >= RmsWindowSamples)
                {
                    _leftRms = MathF.Sqrt(_leftRmsSum / _rmsSampleCount);
                    _rightRms = _leftRms;
                    _leftRmsSum = 0;
                    _rmsSampleCount = 0;

                    LevelUpdated?.Invoke(this, new LevelMeterEventArgs(
                        _leftPeak, _rightPeak, _leftRms, _rightRms));
                }
            }
        }
    }

    private static float PeakToDb(float peak)
    {
        if (peak <= 0) return -96f;
        return 20f * MathF.Log10(peak);
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        ClearBuffer();
    }
}

/// <summary>
/// Event arguments for level meter updates.
/// </summary>
public sealed class LevelMeterEventArgs : EventArgs
{
    /// <summary>
    /// Gets the left channel peak level (0.0 to 1.0).
    /// </summary>
    public float LeftPeak { get; }

    /// <summary>
    /// Gets the right channel peak level (0.0 to 1.0).
    /// </summary>
    public float RightPeak { get; }

    /// <summary>
    /// Gets the left channel RMS level (0.0 to 1.0).
    /// </summary>
    public float LeftRms { get; }

    /// <summary>
    /// Gets the right channel RMS level (0.0 to 1.0).
    /// </summary>
    public float RightRms { get; }

    /// <summary>
    /// Gets the left channel peak level in dB.
    /// </summary>
    public float LeftPeakDb => LeftPeak > 0 ? 20f * MathF.Log10(LeftPeak) : -96f;

    /// <summary>
    /// Gets the right channel peak level in dB.
    /// </summary>
    public float RightPeakDb => RightPeak > 0 ? 20f * MathF.Log10(RightPeak) : -96f;

    /// <summary>
    /// Gets the left channel RMS level in dB.
    /// </summary>
    public float LeftRmsDb => LeftRms > 0 ? 20f * MathF.Log10(LeftRms) : -96f;

    /// <summary>
    /// Gets the right channel RMS level in dB.
    /// </summary>
    public float RightRmsDb => RightRms > 0 ? 20f * MathF.Log10(RightRms) : -96f;

    /// <summary>
    /// Creates new LevelMeterEventArgs.
    /// </summary>
    public LevelMeterEventArgs(float leftPeak, float rightPeak, float leftRms, float rightRms)
    {
        LeftPeak = leftPeak;
        RightPeak = rightPeak;
        LeftRms = leftRms;
        RightRms = rightRms;
    }
}
