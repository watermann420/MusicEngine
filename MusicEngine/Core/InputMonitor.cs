// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Provides live input monitoring capability with optional audio passthrough.
/// Wraps an input source and allows real-time monitoring while buffering
/// audio for recording purposes.
/// </summary>
public sealed class InputMonitor : ISampleProvider, IDisposable
{
    private readonly WaveFormat _waveFormat;
    private readonly object _lock = new();
    private readonly Queue<float> _recordingBuffer = new();
    private readonly int _maxRecordingBufferSamples;

    private WaveInEvent? _waveIn;
    private int _inputDeviceNumber = -1;
    private bool _isMonitoringEnabled;
    private bool _isRecording;
    private float _monitoringVolume = 1.0f;
    private float _leftPeak;
    private float _rightPeak;
    private bool _isDisposed;

    // Buffer for passthrough audio
    private readonly Queue<float> _monitorBuffer = new();
    private readonly int _maxMonitorBufferSamples;

    /// <summary>
    /// Gets the wave format of the input monitor.
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Gets or sets the input device number. Set to -1 for default device.
    /// </summary>
    public int InputDevice
    {
        get => _inputDeviceNumber;
        set
        {
            if (_inputDeviceNumber != value)
            {
                _inputDeviceNumber = value;
                if (_waveIn != null)
                {
                    RestartInputCapture();
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets whether monitoring is enabled.
    /// When enabled, input audio is passed through to the output.
    /// </summary>
    public bool MonitoringEnabled
    {
        get => _isMonitoringEnabled;
        set => _isMonitoringEnabled = value;
    }

    /// <summary>
    /// Gets or sets the monitoring volume (0.0 to 2.0).
    /// </summary>
    public float MonitoringVolume
    {
        get => _monitoringVolume;
        set => _monitoringVolume = Math.Clamp(value, 0f, 2f);
    }

    /// <summary>
    /// Gets or sets whether recording is active.
    /// When active, input audio is buffered for later retrieval.
    /// </summary>
    public bool IsRecording
    {
        get => _isRecording;
        set => _isRecording = value;
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
    /// Gets the number of samples currently in the recording buffer.
    /// </summary>
    public int RecordingBufferSamples
    {
        get
        {
            lock (_lock)
            {
                return _recordingBuffer.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of samples currently in the monitoring buffer.
    /// </summary>
    public int MonitorBufferSamples
    {
        get
        {
            lock (_lock)
            {
                return _monitorBuffer.Count;
            }
        }
    }

    /// <summary>
    /// Gets the monitoring latency in milliseconds.
    /// </summary>
    public double MonitoringLatencyMs
    {
        get
        {
            int samples = MonitorBufferSamples;
            return (samples / (double)_waveFormat.Channels) / _waveFormat.SampleRate * 1000.0;
        }
    }

    /// <summary>
    /// Gets whether input capture is currently active.
    /// </summary>
    public bool IsInputActive => _waveIn != null;

    /// <summary>
    /// Event raised when input audio is received.
    /// </summary>
    public event EventHandler<InputAudioEventArgs>? AudioReceived;

    /// <summary>
    /// Event raised when peak levels are updated.
    /// </summary>
    public event EventHandler<LevelMeterEventArgs>? LevelUpdated;

    /// <summary>
    /// Creates a new InputMonitor.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of channels (1 or 2).</param>
    /// <param name="monitorBufferMs">Maximum monitor buffer size in milliseconds (default 100ms).</param>
    /// <param name="recordingBufferMs">Maximum recording buffer size in milliseconds (default 60000ms / 1 minute).</param>
    public InputMonitor(int sampleRate = 44100, int channels = 2, int monitorBufferMs = 100, int recordingBufferMs = 60000)
    {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
        if (channels < 1 || channels > 2)
            throw new ArgumentOutOfRangeException(nameof(channels), "Channels must be 1 or 2.");

        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        _maxMonitorBufferSamples = (sampleRate * channels * monitorBufferMs) / 1000;
        _maxRecordingBufferSamples = (sampleRate * channels * recordingBufferMs) / 1000;
    }

    /// <summary>
    /// Starts capturing audio from the input device.
    /// </summary>
    public void StartCapture()
    {
        lock (_lock)
        {
            if (_waveIn != null)
                return;

            try
            {
                _waveIn = new WaveInEvent
                {
                    DeviceNumber = _inputDeviceNumber >= 0 ? _inputDeviceNumber : 0,
                    WaveFormat = new WaveFormat(_waveFormat.SampleRate, 16, _waveFormat.Channels),
                    BufferMilliseconds = 50
                };

                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.StartRecording();
            }
            catch (Exception ex)
            {
                _waveIn?.Dispose();
                _waveIn = null;
                throw new InvalidOperationException($"Failed to start audio capture: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Stops capturing audio from the input device.
    /// </summary>
    public void StopCapture()
    {
        lock (_lock)
        {
            if (_waveIn == null)
                return;

            try
            {
                _waveIn.StopRecording();
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.Dispose();
            }
            catch
            {
                // Ignore errors during cleanup
            }
            finally
            {
                _waveIn = null;
            }
        }
    }

    /// <summary>
    /// Restarts input capture (used when device changes).
    /// </summary>
    private void RestartInputCapture()
    {
        StopCapture();
        StartCapture();
    }

    /// <summary>
    /// Handles incoming audio data from the input device.
    /// </summary>
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0)
            return;

        // Convert 16-bit samples to float
        int sampleCount = e.BytesRecorded / 2;
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(e.Buffer, i * 2);
            samples[i] = sample / 32768f;
        }

        // Update peak levels
        UpdateLevels(samples);

        lock (_lock)
        {
            // Add to recording buffer if recording
            if (_isRecording)
            {
                foreach (var sample in samples)
                {
                    _recordingBuffer.Enqueue(sample);
                }

                // Limit recording buffer size
                while (_recordingBuffer.Count > _maxRecordingBufferSamples)
                {
                    _recordingBuffer.Dequeue();
                }
            }

            // Add to monitor buffer if monitoring
            if (_isMonitoringEnabled)
            {
                foreach (var sample in samples)
                {
                    _monitorBuffer.Enqueue(sample * _monitoringVolume);
                }

                // Limit monitor buffer size
                while (_monitorBuffer.Count > _maxMonitorBufferSamples)
                {
                    _monitorBuffer.Dequeue();
                }
            }
        }

        // Raise event
        AudioReceived?.Invoke(this, new InputAudioEventArgs(samples));
    }

    /// <summary>
    /// Updates peak level meters.
    /// </summary>
    private void UpdateLevels(float[] samples)
    {
        // Decay existing peaks
        _leftPeak *= 0.95f;
        _rightPeak *= 0.95f;

        if (_waveFormat.Channels == 2)
        {
            for (int i = 0; i < samples.Length - 1; i += 2)
            {
                float left = Math.Abs(samples[i]);
                float right = Math.Abs(samples[i + 1]);

                if (left > _leftPeak) _leftPeak = left;
                if (right > _rightPeak) _rightPeak = right;
            }
        }
        else
        {
            foreach (var sample in samples)
            {
                float abs = Math.Abs(sample);
                if (abs > _leftPeak)
                {
                    _leftPeak = abs;
                    _rightPeak = abs;
                }
            }
        }

        LevelUpdated?.Invoke(this, new LevelMeterEventArgs(_leftPeak, _rightPeak, _leftPeak, _rightPeak));
    }

    /// <summary>
    /// Reads samples for output (monitoring passthrough).
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            if (!_isMonitoringEnabled || _monitorBuffer.Count == 0)
            {
                // Output silence
                Array.Clear(buffer, offset, count);
                return count;
            }

            int samplesToRead = Math.Min(count, _monitorBuffer.Count);
            for (int i = 0; i < samplesToRead; i++)
            {
                buffer[offset + i] = _monitorBuffer.Dequeue();
            }

            // Fill remaining with silence
            if (samplesToRead < count)
            {
                Array.Clear(buffer, offset + samplesToRead, count - samplesToRead);
            }

            return count;
        }
    }

    /// <summary>
    /// Gets recorded samples and clears the recording buffer.
    /// </summary>
    /// <returns>Array of recorded samples.</returns>
    public float[] GetRecordedSamples()
    {
        lock (_lock)
        {
            var samples = _recordingBuffer.ToArray();
            _recordingBuffer.Clear();
            return samples;
        }
    }

    /// <summary>
    /// Gets recorded samples without clearing the buffer.
    /// </summary>
    /// <returns>Array of recorded samples.</returns>
    public float[] PeekRecordedSamples()
    {
        lock (_lock)
        {
            return _recordingBuffer.ToArray();
        }
    }

    /// <summary>
    /// Clears all buffers.
    /// </summary>
    public void ClearBuffers()
    {
        lock (_lock)
        {
            _monitorBuffer.Clear();
            _recordingBuffer.Clear();
        }
    }

    /// <summary>
    /// Clears the recording buffer.
    /// </summary>
    public void ClearRecordingBuffer()
    {
        lock (_lock)
        {
            _recordingBuffer.Clear();
        }
    }

    /// <summary>
    /// Clears the monitoring buffer.
    /// </summary>
    public void ClearMonitorBuffer()
    {
        lock (_lock)
        {
            _monitorBuffer.Clear();
        }
    }

    /// <summary>
    /// Resets the peak level meters.
    /// </summary>
    public void ResetLevels()
    {
        _leftPeak = 0;
        _rightPeak = 0;
    }

    /// <summary>
    /// Gets the number of available input devices.
    /// </summary>
    public static int GetInputDeviceCount()
    {
        return WaveInEvent.DeviceCount;
    }

    /// <summary>
    /// Gets the name of an input device.
    /// </summary>
    /// <param name="deviceNumber">Device number (0-based).</param>
    /// <returns>Device name or empty string if not found.</returns>
    public static string GetInputDeviceName(int deviceNumber)
    {
        if (deviceNumber < 0 || deviceNumber >= WaveInEvent.DeviceCount)
            return string.Empty;

        var capabilities = WaveInEvent.GetCapabilities(deviceNumber);
        return capabilities.ProductName;
    }

    /// <summary>
    /// Gets all available input device names.
    /// </summary>
    /// <returns>List of device names.</returns>
    public static List<string> GetInputDeviceNames()
    {
        var names = new List<string>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            names.Add(GetInputDeviceName(i));
        }
        return names;
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        StopCapture();
        ClearBuffers();
    }
}

/// <summary>
/// Event arguments for input audio events.
/// </summary>
public sealed class InputAudioEventArgs : EventArgs
{
    /// <summary>
    /// Gets the audio samples.
    /// </summary>
    public float[] Samples { get; }

    /// <summary>
    /// Gets the number of samples.
    /// </summary>
    public int SampleCount => Samples.Length;

    /// <summary>
    /// Creates new InputAudioEventArgs.
    /// </summary>
    public InputAudioEventArgs(float[] samples)
    {
        Samples = samples;
    }
}
