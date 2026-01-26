// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Delay/echo effect processor.

using System;

namespace MusicEngine.Core.PDC;

/// <summary>
/// A circular buffer that delays audio samples by a specified number of samples.
/// Used to compensate for latency differences between tracks in the PDC system.
/// </summary>
/// <remarks>
/// This buffer uses a circular (ring) buffer implementation for efficient
/// sample-by-sample or block-based delay processing without memory allocation
/// during audio processing.
/// </remarks>
public class DelayCompensationBuffer : IDisposable
{
    private float[] _buffer;
    private int _writePosition;
    private int _readPosition;
    private int _delaySamples;
    private readonly int _channels;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Gets the current delay in samples.
    /// </summary>
    public int DelaySamples
    {
        get
        {
            lock (_lock)
            {
                return _delaySamples;
            }
        }
    }

    /// <summary>
    /// Gets the number of audio channels.
    /// </summary>
    public int Channels => _channels;

    /// <summary>
    /// Gets the maximum delay capacity in samples (per channel).
    /// </summary>
    public int MaxDelaySamples => _buffer.Length / _channels;

    /// <summary>
    /// Creates a new delay compensation buffer.
    /// </summary>
    /// <param name="maxDelaySamples">Maximum delay capacity in samples (per channel).</param>
    /// <param name="channels">Number of audio channels (default: 2 for stereo).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if maxDelaySamples is less than 1 or channels is less than 1.
    /// </exception>
    public DelayCompensationBuffer(int maxDelaySamples, int channels = 2)
    {
        if (maxDelaySamples < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDelaySamples), "Max delay must be at least 1 sample.");
        if (channels < 1)
            throw new ArgumentOutOfRangeException(nameof(channels), "Channels must be at least 1.");

        _channels = channels;
        // Buffer size is maxDelay * channels to store interleaved samples
        _buffer = new float[maxDelaySamples * channels];
        _writePosition = 0;
        _readPosition = 0;
        _delaySamples = 0;
    }

    /// <summary>
    /// Sets the delay amount in samples (per channel).
    /// </summary>
    /// <param name="delaySamples">Number of samples to delay.</param>
    /// <remarks>
    /// Changing the delay may cause audio artifacts. For smooth transitions,
    /// consider gradually ramping the delay value.
    /// </remarks>
    public void SetDelay(int delaySamples)
    {
        lock (_lock)
        {
            if (delaySamples < 0)
                delaySamples = 0;

            int maxDelay = _buffer.Length / _channels;
            if (delaySamples > maxDelay)
            {
                // Need to resize buffer
                ResizeBuffer(delaySamples);
            }

            _delaySamples = delaySamples;

            // Update read position based on new delay
            // Read position trails write position by delaySamples * channels
            int delayInSamples = delaySamples * _channels;
            _readPosition = (_writePosition - delayInSamples + _buffer.Length) % _buffer.Length;
        }
    }

    /// <summary>
    /// Resizes the internal buffer to accommodate a larger delay.
    /// </summary>
    /// <param name="newMaxDelaySamples">New maximum delay in samples per channel.</param>
    private void ResizeBuffer(int newMaxDelaySamples)
    {
        int newSize = newMaxDelaySamples * _channels;
        float[] newBuffer = new float[newSize];

        // Copy existing data if possible
        int existingDataLength = Math.Min(_buffer.Length, newSize);
        if (_readPosition <= _writePosition)
        {
            // Data is contiguous
            int copyLength = Math.Min(_writePosition - _readPosition, existingDataLength);
            Array.Copy(_buffer, _readPosition, newBuffer, 0, copyLength);
            _writePosition = copyLength;
        }
        else
        {
            // Data wraps around
            int firstPart = _buffer.Length - _readPosition;
            int secondPart = _writePosition;

            if (firstPart + secondPart <= existingDataLength)
            {
                Array.Copy(_buffer, _readPosition, newBuffer, 0, firstPart);
                Array.Copy(_buffer, 0, newBuffer, firstPart, secondPart);
                _writePosition = firstPart + secondPart;
            }
            else
            {
                // Just copy what fits
                Array.Copy(_buffer, _readPosition, newBuffer, 0, Math.Min(firstPart, existingDataLength));
                _writePosition = Math.Min(firstPart, existingDataLength);
            }
        }

        _buffer = newBuffer;
        _readPosition = 0;
    }

    /// <summary>
    /// Processes audio through the delay buffer.
    /// </summary>
    /// <param name="input">Input samples (interleaved if multi-channel).</param>
    /// <param name="output">Output buffer for delayed samples.</param>
    /// <param name="offset">Offset into the output buffer.</param>
    /// <param name="count">Number of samples to process.</param>
    /// <returns>Number of samples written to output.</returns>
    public int Process(float[] input, float[] output, int offset, int count)
    {
        if (_disposed)
            return 0;

        lock (_lock)
        {
            // If no delay, just copy input to output
            if (_delaySamples == 0)
            {
                Array.Copy(input, 0, output, offset, count);
                return count;
            }

            int processed = 0;
            for (int i = 0; i < count; i++)
            {
                // Write input to buffer
                _buffer[_writePosition] = input[i];
                _writePosition = (_writePosition + 1) % _buffer.Length;

                // Read delayed output from buffer
                output[offset + i] = _buffer[_readPosition];
                _readPosition = (_readPosition + 1) % _buffer.Length;

                processed++;
            }

            return processed;
        }
    }

    /// <summary>
    /// Processes a single sample through the delay buffer.
    /// </summary>
    /// <param name="input">Input sample.</param>
    /// <returns>Delayed output sample.</returns>
    public float ProcessSample(float input)
    {
        if (_disposed)
            return input;

        lock (_lock)
        {
            if (_delaySamples == 0)
                return input;

            // Write input to buffer
            _buffer[_writePosition] = input;
            _writePosition = (_writePosition + 1) % _buffer.Length;

            // Read delayed output
            float output = _buffer[_readPosition];
            _readPosition = (_readPosition + 1) % _buffer.Length;

            return output;
        }
    }

    /// <summary>
    /// Clears the buffer, filling it with silence (zeros).
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _writePosition = 0;
            _readPosition = 0;

            // Restore read position based on current delay
            if (_delaySamples > 0)
            {
                int delayInSamples = _delaySamples * _channels;
                _readPosition = (_writePosition - delayInSamples + _buffer.Length) % _buffer.Length;
            }
        }
    }

    /// <summary>
    /// Disposes resources used by the buffer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _disposed = true;
            _buffer = Array.Empty<float>();
        }

        GC.SuppressFinalize(this);
    }

    ~DelayCompensationBuffer()
    {
        Dispose();
    }
}
