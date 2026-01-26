// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;


namespace MusicEngine.Core.Freeze;


/// <summary>
/// A lightweight ISynth implementation that plays back pre-rendered frozen audio.
/// Provides minimal CPU usage compared to live synthesis.
/// </summary>
public class FrozenTrackPlayer : ISynth, IDisposable
{
    private float[]? _audioBuffer;
    private long _playbackPosition;
    private bool _isPlaying;
    private readonly object _lock = new();

    private readonly WaveFormat _waveFormat;
    private double _playbackSpeed = 1.0;
    private float _volume = 1.0f;
    private bool _loop;
    private long _loopStartSample;
    private long _loopEndSample;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the name of this player.
    /// </summary>
    public string Name { get; set; } = "Frozen Track";

    /// <summary>
    /// Gets the wave format for this sample provider.
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Gets whether audio is currently loaded.
    /// </summary>
    public bool HasAudio => _audioBuffer != null && _audioBuffer.Length > 0;

    /// <summary>
    /// Gets or sets whether the playback is active.
    /// </summary>
    public bool IsPlaying
    {
        get
        {
            lock (_lock) return _isPlaying;
        }
        set
        {
            lock (_lock) _isPlaying = value;
        }
    }

    /// <summary>
    /// Gets or sets whether playback should loop.
    /// </summary>
    public bool Loop
    {
        get => _loop;
        set => _loop = value;
    }

    /// <summary>
    /// Gets or sets the playback volume (0.0 to 2.0).
    /// </summary>
    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 2f);
    }

    /// <summary>
    /// Gets or sets the playback speed (1.0 = normal speed).
    /// </summary>
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => _playbackSpeed = Math.Clamp(value, 0.25, 4.0);
    }

    /// <summary>
    /// Gets the current playback position in samples.
    /// </summary>
    public long PlaybackPositionSamples
    {
        get
        {
            lock (_lock) return _playbackPosition;
        }
    }

    /// <summary>
    /// Gets the current playback position in seconds.
    /// </summary>
    public double PlaybackPositionSeconds => PlaybackPositionSamples / (double)(_waveFormat.SampleRate * _waveFormat.Channels);

    /// <summary>
    /// Gets the total length in samples.
    /// </summary>
    public long TotalSamples => _audioBuffer?.Length ?? 0;

    /// <summary>
    /// Gets the total duration in seconds.
    /// </summary>
    public double TotalDurationSeconds => TotalSamples / (double)(_waveFormat.SampleRate * _waveFormat.Channels);

    /// <summary>
    /// Gets the freeze data associated with this player.
    /// </summary>
    public FreezeData? FreezeData { get; private set; }

    /// <summary>
    /// Creates a new FrozenTrackPlayer instance.
    /// </summary>
    /// <param name="sampleRate">The sample rate (defaults to engine sample rate).</param>
    /// <param name="channels">The number of channels (defaults to 2 for stereo).</param>
    public FrozenTrackPlayer(int? sampleRate = null, int? channels = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        int ch = channels ?? Settings.Channels;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, ch);
    }

    /// <summary>
    /// Loads audio data from a buffer.
    /// </summary>
    /// <param name="buffer">The audio buffer to load.</param>
    /// <param name="freezeData">Optional freeze data containing the source information.</param>
    public void LoadFromBuffer(float[] buffer, FreezeData? freezeData = null)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));

        lock (_lock)
        {
            _audioBuffer = buffer;
            _playbackPosition = 0;
            _loopStartSample = 0;
            _loopEndSample = buffer.Length;
            FreezeData = freezeData;
        }
    }

    /// <summary>
    /// Loads audio data from a WAV file.
    /// </summary>
    /// <param name="filePath">The path to the WAV file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Audio file not found.", filePath);

        await Task.Run(() =>
        {
            using var reader = new AudioFileReader(filePath);

            var buffer = new float[(int)(reader.Length / sizeof(float))];
            int samplesRead = reader.Read(buffer, 0, buffer.Length);

            if (samplesRead < buffer.Length)
            {
                Array.Resize(ref buffer, samplesRead);
            }

            lock (_lock)
            {
                _audioBuffer = buffer;
                _playbackPosition = 0;
                _loopStartSample = 0;
                _loopEndSample = buffer.Length;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Sets the loop region.
    /// </summary>
    /// <param name="startSample">The start sample of the loop region.</param>
    /// <param name="endSample">The end sample of the loop region.</param>
    public void SetLoopRegion(long startSample, long endSample)
    {
        lock (_lock)
        {
            _loopStartSample = Math.Max(0, startSample);
            _loopEndSample = _audioBuffer != null
                ? Math.Min(_audioBuffer.Length, endSample)
                : endSample;
        }
    }

    /// <summary>
    /// Sets the loop region in seconds.
    /// </summary>
    /// <param name="startSeconds">The start time in seconds.</param>
    /// <param name="endSeconds">The end time in seconds.</param>
    public void SetLoopRegionSeconds(double startSeconds, double endSeconds)
    {
        long startSample = (long)(startSeconds * _waveFormat.SampleRate * _waveFormat.Channels);
        long endSample = (long)(endSeconds * _waveFormat.SampleRate * _waveFormat.Channels);
        SetLoopRegion(startSample, endSample);
    }

    /// <summary>
    /// Seeks to a position in samples.
    /// </summary>
    /// <param name="samplePosition">The sample position to seek to.</param>
    public void SeekToSample(long samplePosition)
    {
        lock (_lock)
        {
            _playbackPosition = Math.Clamp(samplePosition, 0, _audioBuffer?.Length ?? 0);
        }
    }

    /// <summary>
    /// Seeks to a position in seconds.
    /// </summary>
    /// <param name="seconds">The time in seconds to seek to.</param>
    public void SeekToSeconds(double seconds)
    {
        long samplePosition = (long)(seconds * _waveFormat.SampleRate * _waveFormat.Channels);
        SeekToSample(samplePosition);
    }

    /// <summary>
    /// Clears the loaded audio data.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _audioBuffer = null;
            _playbackPosition = 0;
            _isPlaying = false;
            FreezeData = null;
        }
    }

    #region ISynth Implementation

    /// <summary>
    /// Starts playback (Note On triggers playback from current position).
    /// </summary>
    /// <param name="note">The MIDI note number (ignored for frozen playback).</param>
    /// <param name="velocity">The velocity (used for volume modulation).</param>
    public void NoteOn(int note, int velocity)
    {
        lock (_lock)
        {
            if (_audioBuffer == null)
                return;

            // Velocity affects volume
            _volume = velocity / 127f;
            _isPlaying = true;
        }
    }

    /// <summary>
    /// Stops playback (Note Off stops playback).
    /// </summary>
    /// <param name="note">The MIDI note number (ignored for frozen playback).</param>
    public void NoteOff(int note)
    {
        lock (_lock)
        {
            _isPlaying = false;
        }
    }

    /// <summary>
    /// Stops all playback.
    /// </summary>
    public void AllNotesOff()
    {
        lock (_lock)
        {
            _isPlaying = false;
        }
    }

    /// <summary>
    /// Sets a parameter by name.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume":
            case "level":
            case "gain":
                Volume = value;
                break;
            case "speed":
            case "playbackspeed":
                PlaybackSpeed = value;
                break;
            case "loop":
                Loop = value > 0.5f;
                break;
            case "position":
                SeekToSeconds(value);
                break;
        }
    }

    /// <summary>
    /// Reads audio samples into the buffer.
    /// </summary>
    /// <param name="buffer">The output buffer.</param>
    /// <param name="offset">The offset in the buffer.</param>
    /// <param name="count">The number of samples to read.</param>
    /// <returns>The number of samples actually read.</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        // Clear buffer first
        for (int i = 0; i < count; i++)
        {
            buffer[offset + i] = 0;
        }

        lock (_lock)
        {
            if (_audioBuffer == null || !_isPlaying)
            {
                return count;
            }

            int samplesWritten = 0;
            int channels = _waveFormat.Channels;

            while (samplesWritten < count)
            {
                if (_playbackPosition >= _loopEndSample)
                {
                    if (_loop)
                    {
                        _playbackPosition = _loopStartSample;
                    }
                    else
                    {
                        _isPlaying = false;
                        break;
                    }
                }

                // Calculate how many samples we can copy
                long remaining = _loopEndSample - _playbackPosition;
                int toCopy = (int)Math.Min(remaining, count - samplesWritten);

                // Copy with volume adjustment
                for (int i = 0; i < toCopy; i++)
                {
                    if (_playbackPosition + i < _audioBuffer.Length)
                    {
                        buffer[offset + samplesWritten + i] = _audioBuffer[_playbackPosition + i] * _volume;
                    }
                }

                _playbackPosition += toCopy;
                samplesWritten += toCopy;
            }

            return count;
        }
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Disposes the player and releases resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            Clear();
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizer.
    /// </summary>
    ~FrozenTrackPlayer()
    {
        Dispose(false);
    }

    #endregion
}
