// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Provides audio input recording functionality with support for device selection,
/// live monitoring, punch-in/out recording, and input level metering.
/// </summary>
public sealed class AudioInputRecorder : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _fileWriter;
    private MemoryStream? _memoryStream;
    private WaveFileWriter? _memoryWriter;
    private MonitoringSampleProvider? _monitoringProvider;

    private readonly object _lock = new();
    private bool _isRecording;
    private bool _isPaused;
    private bool _isArmed;
    private bool _isMonitoring;
    private bool _isDisposed;

    private InputDeviceInfo? _selectedDevice;
    private WaveFormat _recordingFormat;
    private string? _outputFilePath;
    private long _recordedSamples;
    private DateTime _recordingStartTime;

    // Punch-In/Out settings
    private bool _punchEnabled;
    private TimeSpan _punchInTime;
    private TimeSpan _punchOutTime;
    private TimeSpan _currentPosition;
    private bool _isPunchedIn;

    // Level metering
    private float _leftPeak;
    private float _rightPeak;
    private float _leftRms;
    private float _rightRms;
    private float _leftRmsSum;
    private float _rightRmsSum;
    private int _rmsSampleCount;
    private const int RmsWindowSamples = 4410;

    /// <summary>
    /// Gets whether the recorder is currently recording.
    /// </summary>
    public bool IsRecording => _isRecording;

    /// <summary>
    /// Gets whether recording is paused.
    /// </summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Gets whether the recorder is armed for recording.
    /// </summary>
    public bool IsArmed => _isArmed;

    /// <summary>
    /// Gets whether input monitoring is active.
    /// </summary>
    public bool IsMonitoring => _isMonitoring;

    /// <summary>
    /// Gets the currently selected input device.
    /// </summary>
    public InputDeviceInfo? SelectedDevice => _selectedDevice;

    /// <summary>
    /// Gets the recording wave format.
    /// </summary>
    public WaveFormat RecordingFormat => _recordingFormat;

    /// <summary>
    /// Gets the duration of the current recording.
    /// </summary>
    public TimeSpan RecordingDuration
    {
        get
        {
            if (!_isRecording) return TimeSpan.Zero;
            return DateTime.Now - _recordingStartTime;
        }
    }

    /// <summary>
    /// Gets the number of samples recorded.
    /// </summary>
    public long RecordedSamples => _recordedSamples;

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
    /// Gets the left peak level in decibels.
    /// </summary>
    public float LeftPeakDb => _leftPeak > 0 ? 20f * MathF.Log10(_leftPeak) : -96f;

    /// <summary>
    /// Gets the right peak level in decibels.
    /// </summary>
    public float RightPeakDb => _rightPeak > 0 ? 20f * MathF.Log10(_rightPeak) : -96f;

    /// <summary>
    /// Gets or sets whether punch-in/out is enabled.
    /// </summary>
    public bool PunchEnabled
    {
        get => _punchEnabled;
        set => _punchEnabled = value;
    }

    /// <summary>
    /// Gets or sets the punch-in time.
    /// </summary>
    public TimeSpan PunchInTime
    {
        get => _punchInTime;
        set => _punchInTime = value;
    }

    /// <summary>
    /// Gets or sets the punch-out time.
    /// </summary>
    public TimeSpan PunchOutTime
    {
        get => _punchOutTime;
        set => _punchOutTime = value;
    }

    /// <summary>
    /// Gets whether currently punched in (actively recording in punch mode).
    /// </summary>
    public bool IsPunchedIn => _isPunchedIn;

    /// <summary>
    /// Gets the monitoring sample provider for routing to output.
    /// </summary>
    public MonitoringSampleProvider? MonitoringProvider => _monitoringProvider;

    /// <summary>
    /// Event raised when data is available from the input device.
    /// </summary>
    public event EventHandler<WaveInEventArgs>? DataAvailable;

    /// <summary>
    /// Event raised when recording has stopped.
    /// </summary>
    public event EventHandler<StoppedEventArgs>? RecordingStopped;

    /// <summary>
    /// Event raised when input levels are updated.
    /// </summary>
    public event EventHandler<LevelMeterEventArgs>? LevelUpdated;

    /// <summary>
    /// Event raised when punch-in occurs.
    /// </summary>
    public event EventHandler? PunchedIn;

    /// <summary>
    /// Event raised when punch-out occurs.
    /// </summary>
    public event EventHandler? PunchedOut;

    /// <summary>
    /// Creates a new AudioInputRecorder with default settings.
    /// </summary>
    public AudioInputRecorder()
    {
        _recordingFormat = new WaveFormat(44100, 16, 2);
        _selectedDevice = InputDeviceInfo.GetDefaultDevice();
    }

    /// <summary>
    /// Creates a new AudioInputRecorder with the specified device.
    /// </summary>
    /// <param name="device">The input device to use.</param>
    public AudioInputRecorder(InputDeviceInfo device)
    {
        _selectedDevice = device ?? throw new ArgumentNullException(nameof(device));
        _recordingFormat = device.GetRecommendedFormat();
    }

    /// <summary>
    /// Sets the input device to use for recording.
    /// </summary>
    /// <param name="device">The input device.</param>
    public void SetDevice(InputDeviceInfo device)
    {
        if (_isRecording)
            throw new InvalidOperationException("Cannot change device while recording.");

        _selectedDevice = device ?? throw new ArgumentNullException(nameof(device));
    }

    /// <summary>
    /// Sets the input device by index.
    /// </summary>
    /// <param name="deviceIndex">The device index.</param>
    public void SetDevice(int deviceIndex)
    {
        var device = InputDeviceInfo.GetDevice(deviceIndex)
            ?? throw new ArgumentException($"Device with index {deviceIndex} not found.", nameof(deviceIndex));
        SetDevice(device);
    }

    /// <summary>
    /// Sets the recording format.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="bitDepth">Bit depth (8, 16, 24, or 32).</param>
    /// <param name="channels">Number of channels (1 or 2).</param>
    public void SetFormat(int sampleRate, int bitDepth, int channels)
    {
        if (_isRecording)
            throw new InvalidOperationException("Cannot change format while recording.");

        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (bitDepth != 8 && bitDepth != 16 && bitDepth != 24 && bitDepth != 32)
            throw new ArgumentOutOfRangeException(nameof(bitDepth), "Bit depth must be 8, 16, 24, or 32.");
        if (channels < 1 || channels > 2)
            throw new ArgumentOutOfRangeException(nameof(channels), "Channels must be 1 or 2.");

        _recordingFormat = new WaveFormat(sampleRate, bitDepth, channels);
    }

    /// <summary>
    /// Arms the recorder for recording.
    /// </summary>
    public void Arm()
    {
        if (_isRecording)
            throw new InvalidOperationException("Already recording.");

        _isArmed = true;
        Console.WriteLine("[AudioInputRecorder] Armed for recording");
    }

    /// <summary>
    /// Disarms the recorder.
    /// </summary>
    public void Disarm()
    {
        if (_isRecording)
            throw new InvalidOperationException("Cannot disarm while recording.");

        _isArmed = false;
        Console.WriteLine("[AudioInputRecorder] Disarmed");
    }

    /// <summary>
    /// Starts input monitoring (audio pass-through to output).
    /// </summary>
    public void StartMonitoring()
    {
        if (_isMonitoring) return;

        EnsureWaveInCreated();
        _monitoringProvider = new MonitoringSampleProvider(
            _recordingFormat.SampleRate,
            _recordingFormat.Channels);

        _isMonitoring = true;

        // Start recording on the device if not already running
        try
        {
            _waveIn?.StartRecording();
        }
        catch (InvalidOperationException)
        {
            // Already recording, ignore
        }

        Console.WriteLine("[AudioInputRecorder] Monitoring started");
    }

    /// <summary>
    /// Stops input monitoring.
    /// </summary>
    public void StopMonitoring()
    {
        if (!_isMonitoring) return;

        _isMonitoring = false;

        if (!_isRecording)
        {
            try
            {
                _waveIn?.StopRecording();
            }
            catch (InvalidOperationException)
            {
                // Not recording, ignore
            }
        }

        _monitoringProvider?.Dispose();
        _monitoringProvider = null;

        Console.WriteLine("[AudioInputRecorder] Monitoring stopped");
    }

    /// <summary>
    /// Starts recording to a file.
    /// </summary>
    /// <param name="filePath">The output file path (.wav).</param>
    public void StartRecording(string filePath)
    {
        if (_isRecording)
            throw new InvalidOperationException("Already recording.");

        if (_selectedDevice == null)
            throw new InvalidOperationException("No input device selected.");

        _outputFilePath = filePath;
        _fileWriter = new WaveFileWriter(filePath, _recordingFormat);

        StartRecordingInternal();
        Console.WriteLine($"[AudioInputRecorder] Recording to: {filePath}");
    }

    /// <summary>
    /// Starts recording to memory.
    /// </summary>
    public void StartRecordingToMemory()
    {
        if (_isRecording)
            throw new InvalidOperationException("Already recording.");

        if (_selectedDevice == null)
            throw new InvalidOperationException("No input device selected.");

        _memoryStream = new MemoryStream();
        _memoryWriter = new WaveFileWriter(_memoryStream, _recordingFormat);

        StartRecordingInternal();
        Console.WriteLine("[AudioInputRecorder] Recording to memory");
    }

    private void StartRecordingInternal()
    {
        EnsureWaveInCreated();

        _isRecording = true;
        _isPaused = false;
        _recordedSamples = 0;
        _recordingStartTime = DateTime.Now;
        _currentPosition = TimeSpan.Zero;
        _isPunchedIn = !_punchEnabled; // If punch not enabled, always "punched in"

        try
        {
            _waveIn?.StartRecording();
        }
        catch (InvalidOperationException)
        {
            // Already recording, ignore
        }
    }

    /// <summary>
    /// Stops recording.
    /// </summary>
    /// <returns>The recorded audio data if recording to memory, otherwise null.</returns>
    public byte[]? StopRecording()
    {
        if (!_isRecording) return null;

        lock (_lock)
        {
            _isRecording = false;
            _isPaused = false;
            _isPunchedIn = false;

            // Stop WaveIn if not monitoring
            if (!_isMonitoring)
            {
                try
                {
                    _waveIn?.StopRecording();
                }
                catch (InvalidOperationException)
                {
                    // Not recording, ignore
                }
            }

            byte[]? result = null;

            // Close file writer
            if (_fileWriter != null)
            {
                _fileWriter.Dispose();
                _fileWriter = null;
                Console.WriteLine($"[AudioInputRecorder] Recording saved to: {_outputFilePath}");
            }

            // Close memory writer and get data
            if (_memoryWriter != null)
            {
                _memoryWriter.Flush();
                result = _memoryStream?.ToArray();
                _memoryWriter.Dispose();
                _memoryWriter = null;
                _memoryStream?.Dispose();
                _memoryStream = null;
                Console.WriteLine($"[AudioInputRecorder] Recording complete: {result?.Length ?? 0} bytes");
            }

            RecordingStopped?.Invoke(this, new StoppedEventArgs(null));
            return result;
        }
    }

    /// <summary>
    /// Pauses recording.
    /// </summary>
    public void PauseRecording()
    {
        if (!_isRecording || _isPaused) return;

        _isPaused = true;
        Console.WriteLine("[AudioInputRecorder] Recording paused");
    }

    /// <summary>
    /// Resumes recording.
    /// </summary>
    public void ResumeRecording()
    {
        if (!_isRecording || !_isPaused) return;

        _isPaused = false;
        Console.WriteLine("[AudioInputRecorder] Recording resumed");
    }

    /// <summary>
    /// Sets punch-in/out times.
    /// </summary>
    /// <param name="punchIn">Punch-in time.</param>
    /// <param name="punchOut">Punch-out time.</param>
    public void SetPunchPoints(TimeSpan punchIn, TimeSpan punchOut)
    {
        if (punchOut <= punchIn)
            throw new ArgumentException("Punch-out time must be greater than punch-in time.");

        _punchInTime = punchIn;
        _punchOutTime = punchOut;
        _punchEnabled = true;

        Console.WriteLine($"[AudioInputRecorder] Punch points set: In={punchIn:mm\\:ss\\.fff}, Out={punchOut:mm\\:ss\\.fff}");
    }

    /// <summary>
    /// Clears punch-in/out points.
    /// </summary>
    public void ClearPunchPoints()
    {
        _punchEnabled = false;
        _punchInTime = TimeSpan.Zero;
        _punchOutTime = TimeSpan.Zero;

        Console.WriteLine("[AudioInputRecorder] Punch points cleared");
    }

    /// <summary>
    /// Updates the current playback position for punch-in/out detection.
    /// Call this from your playback engine to sync punch recording.
    /// </summary>
    /// <param name="position">Current playback position.</param>
    public void UpdatePosition(TimeSpan position)
    {
        _currentPosition = position;

        if (!_isRecording || !_punchEnabled) return;

        bool shouldBePunchedIn = position >= _punchInTime && position < _punchOutTime;

        if (shouldBePunchedIn && !_isPunchedIn)
        {
            _isPunchedIn = true;
            PunchedIn?.Invoke(this, EventArgs.Empty);
            Console.WriteLine($"[AudioInputRecorder] Punch-in at {position:mm\\:ss\\.fff}");
        }
        else if (!shouldBePunchedIn && _isPunchedIn)
        {
            _isPunchedIn = false;
            PunchedOut?.Invoke(this, EventArgs.Empty);
            Console.WriteLine($"[AudioInputRecorder] Punch-out at {position:mm\\:ss\\.fff}");
        }
    }

    /// <summary>
    /// Manually triggers a punch-in.
    /// </summary>
    public void ManualPunchIn()
    {
        if (!_isRecording) return;

        _isPunchedIn = true;
        PunchedIn?.Invoke(this, EventArgs.Empty);
        Console.WriteLine("[AudioInputRecorder] Manual punch-in");
    }

    /// <summary>
    /// Manually triggers a punch-out.
    /// </summary>
    public void ManualPunchOut()
    {
        if (!_isRecording) return;

        _isPunchedIn = false;
        PunchedOut?.Invoke(this, EventArgs.Empty);
        Console.WriteLine("[AudioInputRecorder] Manual punch-out");
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

    private void EnsureWaveInCreated()
    {
        if (_waveIn != null) return;

        if (_selectedDevice == null)
            throw new InvalidOperationException("No input device selected.");

        _waveIn = new WaveInEvent
        {
            DeviceNumber = _selectedDevice.DeviceIndex,
            WaveFormat = _recordingFormat,
            BufferMilliseconds = 50
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_isDisposed) return;

        try
        {
            // Convert bytes to samples for level metering and monitoring
            int bytesPerSample = _recordingFormat.BitsPerSample / 8;
            int sampleCount = e.BytesRecorded / bytesPerSample;
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                int byteOffset = i * bytesPerSample;

                float sample = _recordingFormat.BitsPerSample switch
                {
                    8 => (e.Buffer[byteOffset] - 128) / 128f,
                    16 => BitConverter.ToInt16(e.Buffer, byteOffset) / 32768f,
                    24 => (e.Buffer[byteOffset] | (e.Buffer[byteOffset + 1] << 8) |
                           (e.Buffer[byteOffset + 2] << 16)) / 8388608f,
                    32 => BitConverter.ToInt32(e.Buffer, byteOffset) / 2147483648f,
                    _ => 0f
                };

                samples[i] = sample;
            }

            // Update level meters
            UpdateLevels(samples);

            // Send to monitoring provider
            if (_isMonitoring && _monitoringProvider != null)
            {
                _monitoringProvider.AddSamples(samples);
            }

            // Write to file/memory if recording and not paused
            if (_isRecording && !_isPaused && _isPunchedIn)
            {
                lock (_lock)
                {
                    _fileWriter?.Write(e.Buffer, 0, e.BytesRecorded);
                    _memoryWriter?.Write(e.Buffer, 0, e.BytesRecorded);
                    _recordedSamples += sampleCount;
                }
            }

            DataAvailable?.Invoke(this, e);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AudioInputRecorder] Error processing audio: {ex.Message}");
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            Console.WriteLine($"[AudioInputRecorder] Recording stopped with error: {e.Exception.Message}");
        }

        RecordingStopped?.Invoke(this, e);
    }

    private void UpdateLevels(float[] samples)
    {
        // Decay peak levels
        _leftPeak *= 0.95f;
        _rightPeak *= 0.95f;

        int channels = _recordingFormat.Channels;

        if (channels == 2)
        {
            for (int i = 0; i < samples.Length - 1; i += 2)
            {
                float left = Math.Abs(samples[i]);
                float right = Math.Abs(samples[i + 1]);

                if (left > _leftPeak) _leftPeak = Math.Min(left, 1f);
                if (right > _rightPeak) _rightPeak = Math.Min(right, 1f);

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

                    LevelUpdated?.Invoke(this, new LevelMeterEventArgs(
                        _leftPeak, _rightPeak, _leftRms, _rightRms));
                }
            }
        }
        else
        {
            for (int i = 0; i < samples.Length; i++)
            {
                float sample = Math.Abs(samples[i]);

                if (sample > _leftPeak) _leftPeak = Math.Min(sample, 1f);
                _rightPeak = _leftPeak;

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

    /// <summary>
    /// Disposes the recorder and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        StopRecording();
        StopMonitoring();

        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }

        _monitoringProvider?.Dispose();
        _fileWriter?.Dispose();
        _memoryWriter?.Dispose();
        _memoryStream?.Dispose();
    }
}

/// <summary>
/// Recording state enumeration.
/// </summary>
public enum RecordingState
{
    /// <summary>Recording is stopped.</summary>
    Stopped,

    /// <summary>Recording is armed and ready.</summary>
    Armed,

    /// <summary>Currently recording.</summary>
    Recording,

    /// <summary>Recording is paused.</summary>
    Paused
}
