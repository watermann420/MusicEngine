//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Reference track for A/B comparison with reference audio during mixing.

using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.MediaFoundation;

namespace MusicEngine.Core;

/// <summary>
/// A special track for loading reference audio for A/B comparison during mixing.
/// Bypasses all master effects and provides level matching.
/// </summary>
public class ReferenceTrack : ISampleProvider, IDisposable
{
    private AudioFileReader? _audioReader;
    private ISampleProvider? _resampler;
    private readonly object _lock = new();
    private bool _disposed;

    // Target output format
    private readonly WaveFormat _targetFormat;

    /// <summary>
    /// Gets the wave format of the output audio stream.
    /// </summary>
    public WaveFormat WaveFormat { get; private set; }

    /// <summary>
    /// Gets the file path of the loaded reference audio.
    /// </summary>
    public string? FilePath { get; private set; }

    /// <summary>
    /// Gets the duration of the loaded reference audio.
    /// </summary>
    public TimeSpan Duration { get; private set; }

    /// <summary>
    /// Gets or sets whether the reference track is active (playing instead of mix).
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the playback position in seconds.
    /// </summary>
    public double Position
    {
        get
        {
            lock (_lock)
            {
                if (_audioReader == null) return 0;
                return _audioReader.CurrentTime.TotalSeconds;
            }
        }
        set
        {
            lock (_lock)
            {
                if (_audioReader != null && value >= 0 && value < Duration.TotalSeconds)
                {
                    _audioReader.CurrentTime = TimeSpan.FromSeconds(value);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the volume (linear, 0.0 to 2.0, default 1.0).
    /// </summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the target LUFS for level matching.
    /// </summary>
    public float TargetLufs { get; set; } = -14.0f;

    /// <summary>
    /// Gets or sets whether automatic level matching is enabled.
    /// </summary>
    public bool AutoLevelMatch { get; set; } = true;

    /// <summary>
    /// Gets the measured LUFS of the loaded reference audio.
    /// </summary>
    public float MeasuredLufs { get; private set; } = float.NegativeInfinity;

    /// <summary>
    /// Gets or sets whether looping is enabled.
    /// </summary>
    public bool LoopEnabled { get; set; }

    /// <summary>
    /// Gets or sets the loop start position in seconds.
    /// </summary>
    public double LoopStart { get; set; }

    /// <summary>
    /// Gets or sets the loop end position in seconds.
    /// </summary>
    public double LoopEnd { get; set; }

    /// <summary>
    /// Gets or sets whether to sync position with the main transport.
    /// </summary>
    public bool SyncWithTransport { get; set; } = true;

    /// <summary>
    /// Gets whether a file is currently loaded.
    /// </summary>
    public bool IsLoaded => _audioReader != null;

    /// <summary>
    /// Gets the sample rate of the loaded file (0 if no file loaded).
    /// </summary>
    public int SourceSampleRate => _audioReader?.WaveFormat.SampleRate ?? 0;

    /// <summary>
    /// Gets the number of channels of the loaded file (0 if no file loaded).
    /// </summary>
    public int SourceChannels => _audioReader?.WaveFormat.Channels ?? 0;

    /// <summary>
    /// Event raised when a file is loaded.
    /// </summary>
    public event EventHandler? FileLoaded;

    /// <summary>
    /// Event raised when a file is unloaded.
    /// </summary>
    public event EventHandler? FileUnloaded;

    /// <summary>
    /// Event raised when the active state changes.
    /// </summary>
    public event EventHandler? ActiveStateChanged;

    /// <summary>
    /// Event raised when loudness analysis is complete.
    /// </summary>
    public event EventHandler<LoudnessAnalysisEventArgs>? LoudnessAnalyzed;

    /// <summary>
    /// Creates a new reference track with the specified output format.
    /// </summary>
    /// <param name="sampleRate">The output sample rate (default: 44100).</param>
    /// <param name="channels">The number of output channels (default: 2).</param>
    public ReferenceTrack(int sampleRate = 44100, int channels = 2)
    {
        _targetFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        WaveFormat = _targetFormat;
    }

    /// <summary>
    /// Loads a reference audio file.
    /// </summary>
    /// <param name="filePath">The path to the audio file.</param>
    /// <returns>True if the file was loaded successfully, false otherwise.</returns>
    public bool LoadFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            return false;

        lock (_lock)
        {
            try
            {
                // Dispose any existing reader
                DisposeReader();

                // Create new reader
                _audioReader = new AudioFileReader(filePath);
                Duration = _audioReader.TotalTime;
                FilePath = filePath;

                // Set up resampler if needed
                SetupResampler();

                // Reset loop region to full file
                LoopStart = 0;
                LoopEnd = Duration.TotalSeconds;

                // Reset measured LUFS
                MeasuredLufs = float.NegativeInfinity;

                FileLoaded?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception)
            {
                DisposeReader();
                return false;
            }
        }
    }

    /// <summary>
    /// Unloads the current reference file.
    /// </summary>
    public void Unload()
    {
        lock (_lock)
        {
            DisposeReader();
            FilePath = null;
            Duration = TimeSpan.Zero;
            MeasuredLufs = float.NegativeInfinity;
            LoopStart = 0;
            LoopEnd = 0;
            FileUnloaded?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Seeks to the specified position in seconds.
    /// </summary>
    /// <param name="seconds">The position to seek to.</param>
    public void Seek(double seconds)
    {
        Position = seconds;
    }

    /// <summary>
    /// Analyzes the loudness of the loaded reference audio.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task AnalyzeLoudnessAsync(CancellationToken cancellationToken = default)
    {
        if (_audioReader == null)
            return;

        await Task.Run(() =>
        {
            lock (_lock)
            {
                if (_audioReader == null)
                    return;

                // Save current position
                var savedPosition = _audioReader.Position;

                try
                {
                    // Reset to beginning
                    _audioReader.Position = 0;

                    // Create loudness meter
                    var meter = new LoudnessMeter(_audioReader);

                    // Process entire file
                    var buffer = new float[_audioReader.WaveFormat.SampleRate * _audioReader.WaveFormat.Channels];
                    int samplesRead;

                    while ((samplesRead = meter.Read(buffer, 0, buffer.Length)) > 0 && !cancellationToken.IsCancellationRequested)
                    {
                        // Just processing through the meter
                    }

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        MeasuredLufs = (float)meter.IntegratedLoudness;
                        LoudnessAnalyzed?.Invoke(this, new LoudnessAnalysisEventArgs(MeasuredLufs));
                    }
                }
                finally
                {
                    // Restore position
                    if (_audioReader != null)
                    {
                        _audioReader.Position = savedPosition;
                    }
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Gets the gain adjustment needed to match the target LUFS.
    /// </summary>
    /// <returns>The gain in linear scale (1.0 = no change).</returns>
    public float GetLevelMatchGain()
    {
        if (float.IsNegativeInfinity(MeasuredLufs) || float.IsNegativeInfinity(TargetLufs))
            return 1.0f;

        // Calculate the dB difference
        var dbDifference = TargetLufs - MeasuredLufs;

        // Convert to linear gain
        return (float)Math.Pow(10.0, dbDifference / 20.0);
    }

    /// <summary>
    /// Reads audio samples from the reference track.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        // If not active or no file loaded, output silence
        if (!IsActive)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        lock (_lock)
        {
            if (_audioReader == null)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

            // Determine source
            ISampleProvider source = _resampler ?? _audioReader;

            int totalSamplesRead = 0;
            int remaining = count;
            int currentOffset = offset;

            while (remaining > 0)
            {
                // Check loop boundaries
                if (LoopEnabled && Position >= LoopEnd)
                {
                    Position = LoopStart;
                }

                int samplesRead = source.Read(buffer, currentOffset, remaining);

                if (samplesRead == 0)
                {
                    // End of file
                    if (LoopEnabled)
                    {
                        // Loop back to start
                        Position = LoopStart;
                        continue;
                    }
                    else
                    {
                        // Fill remaining with silence
                        Array.Clear(buffer, currentOffset, remaining);
                        totalSamplesRead += remaining;
                        break;
                    }
                }

                totalSamplesRead += samplesRead;
                currentOffset += samplesRead;
                remaining -= samplesRead;
            }

            // Apply gain
            float gain = Volume;
            if (AutoLevelMatch)
            {
                gain *= GetLevelMatchGain();
            }

            // Apply gain to buffer
            if (Math.Abs(gain - 1.0f) > 0.0001f)
            {
                for (int i = offset; i < offset + totalSamplesRead; i++)
                {
                    buffer[i] *= gain;
                }
            }

            return totalSamplesRead;
        }
    }

    /// <summary>
    /// Synchronizes the reference track position with the main transport.
    /// </summary>
    /// <param name="transportPositionSeconds">The transport position in seconds.</param>
    public void SyncPosition(double transportPositionSeconds)
    {
        if (!SyncWithTransport || _audioReader == null)
            return;

        // Clamp to valid range
        var targetPosition = Math.Max(0, Math.Min(transportPositionSeconds, Duration.TotalSeconds));

        // Only sync if loop is not enabled or position is within loop region
        if (LoopEnabled)
        {
            // Wrap position within loop region
            var loopLength = LoopEnd - LoopStart;
            if (loopLength > 0)
            {
                var wrappedPosition = LoopStart + ((targetPosition - LoopStart) % loopLength);
                if (wrappedPosition < LoopStart)
                    wrappedPosition += loopLength;
                Position = wrappedPosition;
            }
            else
            {
                Position = LoopStart;
            }
        }
        else
        {
            Position = targetPosition;
        }
    }

    /// <summary>
    /// Toggles the active state (A/B switch).
    /// </summary>
    public void Toggle()
    {
        IsActive = !IsActive;
        ActiveStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Sets the loop region.
    /// </summary>
    /// <param name="startSeconds">Loop start in seconds.</param>
    /// <param name="endSeconds">Loop end in seconds.</param>
    public void SetLoopRegion(double startSeconds, double endSeconds)
    {
        if (_audioReader == null)
            return;

        LoopStart = Math.Max(0, Math.Min(startSeconds, Duration.TotalSeconds));
        LoopEnd = Math.Max(LoopStart, Math.Min(endSeconds, Duration.TotalSeconds));
    }

    /// <summary>
    /// Resets the loop region to the full file.
    /// </summary>
    public void ResetLoopRegion()
    {
        LoopStart = 0;
        LoopEnd = Duration.TotalSeconds;
    }

    private void SetupResampler()
    {
        if (_audioReader == null)
        {
            _resampler = null;
            return;
        }

        // Check if resampling is needed
        bool needsResample = _audioReader.WaveFormat.SampleRate != _targetFormat.SampleRate;
        bool needsChannelConversion = _audioReader.WaveFormat.Channels != _targetFormat.Channels;

        if (!needsResample && !needsChannelConversion)
        {
            _resampler = null;
            return;
        }

        ISampleProvider current = _audioReader;

        // Handle channel conversion first
        if (needsChannelConversion)
        {
            if (_audioReader.WaveFormat.Channels == 1 && _targetFormat.Channels == 2)
            {
                // Mono to stereo - duplicate mono channel to both left and right
                current = new MonoToStereoConverter(current);
            }
            else if (_audioReader.WaveFormat.Channels == 2 && _targetFormat.Channels == 1)
            {
                // Stereo to mono - mix left and right channels
                current = new StereoToMonoConverter(current);
            }
        }

        // Handle sample rate conversion
        if (needsResample)
        {
            // Use MediaFoundationResampler which is available in all NAudio versions
            current = new MediaFoundationResampler(current.ToWaveProvider(), _targetFormat.SampleRate)
                .ToSampleProvider();
        }

        _resampler = current;
    }

    private void DisposeReader()
    {
        _resampler = null;
        _audioReader?.Dispose();
        _audioReader = null;
    }

    /// <summary>
    /// Disposes of all resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            DisposeReader();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer to ensure resources are released.
    /// </summary>
    ~ReferenceTrack()
    {
        Dispose();
    }
}

/// <summary>
/// Event arguments for loudness analysis completion.
/// </summary>
public class LoudnessAnalysisEventArgs : EventArgs
{
    /// <summary>
    /// Gets the measured integrated loudness in LUFS.
    /// </summary>
    public float IntegratedLufs { get; }

    /// <summary>
    /// Creates new loudness analysis event arguments.
    /// </summary>
    /// <param name="integratedLufs">The measured integrated loudness in LUFS.</param>
    public LoudnessAnalysisEventArgs(float integratedLufs)
    {
        IntegratedLufs = integratedLufs;
    }
}

/// <summary>
/// Converts mono audio to stereo by duplicating the mono channel.
/// </summary>
internal class MonoToStereoConverter : ISampleProvider
{
    private readonly ISampleProvider _source;
    private float[]? _sourceBuffer;

    public WaveFormat WaveFormat { get; }

    public MonoToStereoConverter(ISampleProvider source)
    {
        if (source.WaveFormat.Channels != 1)
            throw new ArgumentException("Source must be mono", nameof(source));

        _source = source;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 2);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRequested = count / 2;
        _sourceBuffer ??= new float[samplesRequested];

        if (_sourceBuffer.Length < samplesRequested)
            _sourceBuffer = new float[samplesRequested];

        int samplesRead = _source.Read(_sourceBuffer, 0, samplesRequested);

        // Duplicate mono to stereo
        for (int i = 0; i < samplesRead; i++)
        {
            buffer[offset + i * 2] = _sourceBuffer[i];       // Left
            buffer[offset + i * 2 + 1] = _sourceBuffer[i];   // Right
        }

        return samplesRead * 2;
    }
}

/// <summary>
/// Converts stereo audio to mono by mixing left and right channels.
/// </summary>
internal class StereoToMonoConverter : ISampleProvider
{
    private readonly ISampleProvider _source;
    private float[]? _sourceBuffer;

    public WaveFormat WaveFormat { get; }

    public StereoToMonoConverter(ISampleProvider source)
    {
        if (source.WaveFormat.Channels != 2)
            throw new ArgumentException("Source must be stereo", nameof(source));

        _source = source;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int stereoSamples = count * 2;
        _sourceBuffer ??= new float[stereoSamples];

        if (_sourceBuffer.Length < stereoSamples)
            _sourceBuffer = new float[stereoSamples];

        int samplesRead = _source.Read(_sourceBuffer, 0, stereoSamples);

        // Mix stereo to mono
        int monoSamples = samplesRead / 2;
        for (int i = 0; i < monoSamples; i++)
        {
            buffer[offset + i] = (_sourceBuffer[i * 2] + _sourceBuffer[i * 2 + 1]) * 0.5f;
        }

        return monoSamples;
    }
}
