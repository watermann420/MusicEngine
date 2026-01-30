// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Description: Thread-safe generic circular/ring buffer for audio processing with interpolated reads.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MusicEngine.Core.Utilities;

/// <summary>
/// Interpolation mode for fractional position reads.
/// </summary>
public enum InterpolationMode
{
    /// <summary>No interpolation - returns nearest sample (floor).</summary>
    None,
    /// <summary>Linear interpolation between two adjacent samples.</summary>
    Linear,
    /// <summary>Cubic interpolation using four samples for smoother results.</summary>
    Cubic,
    /// <summary>Hermite interpolation for high-quality audio applications.</summary>
    Hermite
}

/// <summary>
/// Thread-safe generic circular/ring buffer optimized for audio processing.
/// Supports block read/write operations, interpolated reads for fractional positions,
/// and dynamic resizing.
/// </summary>
/// <typeparam name="T">The element type. For audio, typically float or double.</typeparam>
public class CircularBuffer<T> where T : struct
{
    private T[] _buffer;
    private int _writePosition;
    private int _readPosition;
    private int _count;
    private readonly object _lock = new();

    // For interpolation, we need to be able to convert T to/from double
    private static readonly bool _isNumeric = typeof(T) == typeof(float) ||
                                               typeof(T) == typeof(double) ||
                                               typeof(T) == typeof(int) ||
                                               typeof(T) == typeof(short) ||
                                               typeof(T) == typeof(byte);

    /// <summary>
    /// Creates a new circular buffer with the specified capacity.
    /// </summary>
    /// <param name="capacity">Initial capacity of the buffer. Must be greater than 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity is less than 1.</exception>
    public CircularBuffer(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least 1.");

        _buffer = new T[capacity];
        _writePosition = 0;
        _readPosition = 0;
        _count = 0;
    }

    /// <summary>
    /// Gets the total capacity of the buffer.
    /// </summary>
    public int Capacity
    {
        get
        {
            lock (_lock)
            {
                return _buffer.Length;
            }
        }
    }

    /// <summary>
    /// Gets the current number of elements in the buffer.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    /// <summary>
    /// Gets whether the buffer is empty.
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _count == 0;
            }
        }
    }

    /// <summary>
    /// Gets whether the buffer is full.
    /// </summary>
    public bool IsFull
    {
        get
        {
            lock (_lock)
            {
                return _count == _buffer.Length;
            }
        }
    }

    /// <summary>
    /// Gets the number of elements that can be written without overwriting.
    /// </summary>
    public int AvailableWrite
    {
        get
        {
            lock (_lock)
            {
                return _buffer.Length - _count;
            }
        }
    }

    /// <summary>
    /// Gets the number of elements available for reading.
    /// </summary>
    public int AvailableRead
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    /// <summary>
    /// Writes a single element to the buffer.
    /// </summary>
    /// <param name="item">The item to write.</param>
    /// <param name="overwrite">If true, overwrites oldest data when buffer is full.
    /// If false, returns false when buffer is full.</param>
    /// <returns>True if the write was successful, false if buffer was full and overwrite is false.</returns>
    public bool Write(T item, bool overwrite = true)
    {
        lock (_lock)
        {
            if (_count == _buffer.Length)
            {
                if (!overwrite)
                    return false;

                // Overwrite oldest data - advance read position
                _readPosition = (_readPosition + 1) % _buffer.Length;
            }
            else
            {
                _count++;
            }

            _buffer[_writePosition] = item;
            _writePosition = (_writePosition + 1) % _buffer.Length;
            return true;
        }
    }

    /// <summary>
    /// Writes multiple elements to the buffer (block write).
    /// </summary>
    /// <param name="items">Source array containing items to write.</param>
    /// <param name="offset">Offset in the source array.</param>
    /// <param name="count">Number of items to write.</param>
    /// <param name="overwrite">If true, overwrites oldest data when buffer is full.
    /// If false, writes only as many items as will fit.</param>
    /// <returns>The number of items actually written.</returns>
    public int Write(T[] items, int offset, int count, bool overwrite = true)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (offset < 0 || offset >= items.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > items.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        lock (_lock)
        {
            int toWrite = count;

            if (!overwrite)
            {
                // Limit to available space
                toWrite = Math.Min(count, _buffer.Length - _count);
            }
            else if (count > _buffer.Length)
            {
                // If writing more than capacity, only keep the last 'capacity' items
                offset += count - _buffer.Length;
                toWrite = _buffer.Length;
            }

            if (toWrite == 0)
                return 0;

            // Handle overwriting when buffer would overflow
            int overflow = (_count + toWrite) - _buffer.Length;
            if (overflow > 0 && overwrite)
            {
                _readPosition = (_readPosition + overflow) % _buffer.Length;
                _count -= overflow;
            }

            // Write in up to two chunks (wrap-around handling)
            int firstChunk = Math.Min(toWrite, _buffer.Length - _writePosition);
            Array.Copy(items, offset, _buffer, _writePosition, firstChunk);

            int secondChunk = toWrite - firstChunk;
            if (secondChunk > 0)
            {
                Array.Copy(items, offset + firstChunk, _buffer, 0, secondChunk);
            }

            _writePosition = (_writePosition + toWrite) % _buffer.Length;
            _count += toWrite;

            return toWrite;
        }
    }

    /// <summary>
    /// Writes a span of elements to the buffer.
    /// </summary>
    /// <param name="items">Span containing items to write.</param>
    /// <param name="overwrite">If true, overwrites oldest data when buffer is full.</param>
    /// <returns>The number of items actually written.</returns>
    public int Write(ReadOnlySpan<T> items, bool overwrite = true)
    {
        lock (_lock)
        {
            int toWrite = items.Length;

            if (!overwrite)
            {
                toWrite = Math.Min(items.Length, _buffer.Length - _count);
            }
            else if (items.Length > _buffer.Length)
            {
                items = items.Slice(items.Length - _buffer.Length);
                toWrite = _buffer.Length;
            }

            if (toWrite == 0)
                return 0;

            int overflow = (_count + toWrite) - _buffer.Length;
            if (overflow > 0 && overwrite)
            {
                _readPosition = (_readPosition + overflow) % _buffer.Length;
                _count -= overflow;
            }

            int firstChunk = Math.Min(toWrite, _buffer.Length - _writePosition);
            items.Slice(0, firstChunk).CopyTo(_buffer.AsSpan(_writePosition, firstChunk));

            int secondChunk = toWrite - firstChunk;
            if (secondChunk > 0)
            {
                items.Slice(firstChunk, secondChunk).CopyTo(_buffer.AsSpan(0, secondChunk));
            }

            _writePosition = (_writePosition + toWrite) % _buffer.Length;
            _count += toWrite;

            return toWrite;
        }
    }

    /// <summary>
    /// Reads and removes a single element from the buffer.
    /// </summary>
    /// <param name="item">The read item.</param>
    /// <returns>True if an item was read, false if buffer was empty.</returns>
    public bool Read(out T item)
    {
        lock (_lock)
        {
            if (_count == 0)
            {
                item = default;
                return false;
            }

            item = _buffer[_readPosition];
            _readPosition = (_readPosition + 1) % _buffer.Length;
            _count--;
            return true;
        }
    }

    /// <summary>
    /// Reads and removes multiple elements from the buffer (block read).
    /// </summary>
    /// <param name="destination">Destination array for read items.</param>
    /// <param name="offset">Offset in the destination array.</param>
    /// <param name="count">Maximum number of items to read.</param>
    /// <returns>The number of items actually read.</returns>
    public int Read(T[] destination, int offset, int count)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        if (offset < 0 || offset >= destination.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > destination.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        lock (_lock)
        {
            int toRead = Math.Min(count, _count);
            if (toRead == 0)
                return 0;

            // Read in up to two chunks (wrap-around handling)
            int firstChunk = Math.Min(toRead, _buffer.Length - _readPosition);
            Array.Copy(_buffer, _readPosition, destination, offset, firstChunk);

            int secondChunk = toRead - firstChunk;
            if (secondChunk > 0)
            {
                Array.Copy(_buffer, 0, destination, offset + firstChunk, secondChunk);
            }

            _readPosition = (_readPosition + toRead) % _buffer.Length;
            _count -= toRead;

            return toRead;
        }
    }

    /// <summary>
    /// Reads elements into a span, removing them from the buffer.
    /// </summary>
    /// <param name="destination">Destination span for read items.</param>
    /// <returns>The number of items actually read.</returns>
    public int Read(Span<T> destination)
    {
        lock (_lock)
        {
            int toRead = Math.Min(destination.Length, _count);
            if (toRead == 0)
                return 0;

            int firstChunk = Math.Min(toRead, _buffer.Length - _readPosition);
            _buffer.AsSpan(_readPosition, firstChunk).CopyTo(destination.Slice(0, firstChunk));

            int secondChunk = toRead - firstChunk;
            if (secondChunk > 0)
            {
                _buffer.AsSpan(0, secondChunk).CopyTo(destination.Slice(firstChunk, secondChunk));
            }

            _readPosition = (_readPosition + toRead) % _buffer.Length;
            _count -= toRead;

            return toRead;
        }
    }

    /// <summary>
    /// Peeks at a single element without removing it.
    /// </summary>
    /// <param name="item">The peeked item.</param>
    /// <returns>True if an item was peeked, false if buffer was empty.</returns>
    public bool Peek(out T item)
    {
        lock (_lock)
        {
            if (_count == 0)
            {
                item = default;
                return false;
            }

            item = _buffer[_readPosition];
            return true;
        }
    }

    /// <summary>
    /// Peeks at an element at a specific offset from the read position without removing it.
    /// </summary>
    /// <param name="offset">Offset from the current read position (0 = next item to read).</param>
    /// <param name="item">The peeked item.</param>
    /// <returns>True if an item was peeked, false if offset is out of range.</returns>
    public bool PeekAt(int offset, out T item)
    {
        lock (_lock)
        {
            if (offset < 0 || offset >= _count)
            {
                item = default;
                return false;
            }

            int position = (_readPosition + offset) % _buffer.Length;
            item = _buffer[position];
            return true;
        }
    }

    /// <summary>
    /// Peeks at multiple elements without removing them.
    /// </summary>
    /// <param name="destination">Destination array for peeked items.</param>
    /// <param name="offset">Offset in the destination array.</param>
    /// <param name="count">Maximum number of items to peek.</param>
    /// <returns>The number of items actually peeked.</returns>
    public int Peek(T[] destination, int offset, int count)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));
        if (offset < 0 || offset >= destination.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || offset + count > destination.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        lock (_lock)
        {
            int toPeek = Math.Min(count, _count);
            if (toPeek == 0)
                return 0;

            int firstChunk = Math.Min(toPeek, _buffer.Length - _readPosition);
            Array.Copy(_buffer, _readPosition, destination, offset, firstChunk);

            int secondChunk = toPeek - firstChunk;
            if (secondChunk > 0)
            {
                Array.Copy(_buffer, 0, destination, offset + firstChunk, secondChunk);
            }

            return toPeek;
        }
    }

    /// <summary>
    /// Clears all elements from the buffer.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _writePosition = 0;
            _readPosition = 0;
            _count = 0;
            // Optionally clear array contents for GC (only matters for reference types)
            Array.Clear(_buffer, 0, _buffer.Length);
        }
    }

    /// <summary>
    /// Resizes the buffer to a new capacity.
    /// </summary>
    /// <param name="newCapacity">New capacity. Must be greater than 0.</param>
    /// <param name="preserveData">If true, preserves existing data (up to new capacity).
    /// If false, clears the buffer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when newCapacity is less than 1.</exception>
    public void Resize(int newCapacity, bool preserveData = true)
    {
        if (newCapacity < 1)
            throw new ArgumentOutOfRangeException(nameof(newCapacity), "Capacity must be at least 1.");

        lock (_lock)
        {
            if (newCapacity == _buffer.Length)
                return;

            var newBuffer = new T[newCapacity];

            if (preserveData && _count > 0)
            {
                int toPreserve = Math.Min(_count, newCapacity);

                // Copy data to new buffer starting at position 0
                int firstChunk = Math.Min(toPreserve, _buffer.Length - _readPosition);
                Array.Copy(_buffer, _readPosition, newBuffer, 0, firstChunk);

                int secondChunk = toPreserve - firstChunk;
                if (secondChunk > 0)
                {
                    Array.Copy(_buffer, 0, newBuffer, firstChunk, secondChunk);
                }

                _count = toPreserve;
                _readPosition = 0;
                _writePosition = toPreserve % newCapacity;
            }
            else
            {
                _count = 0;
                _readPosition = 0;
                _writePosition = 0;
            }

            _buffer = newBuffer;
        }
    }

    /// <summary>
    /// Skips (discards) a number of elements from the read position.
    /// </summary>
    /// <param name="count">Number of elements to skip.</param>
    /// <returns>The number of elements actually skipped.</returns>
    public int Skip(int count)
    {
        lock (_lock)
        {
            int toSkip = Math.Min(count, _count);
            _readPosition = (_readPosition + toSkip) % _buffer.Length;
            _count -= toSkip;
            return toSkip;
        }
    }

    /// <summary>
    /// Reads at a fractional position with interpolation (for audio delay lines, pitch shifting, etc.).
    /// Only available for numeric types (float, double, int, short, byte).
    /// </summary>
    /// <param name="fractionalOffset">Fractional offset from read position (can be negative for lookback).</param>
    /// <param name="mode">Interpolation mode to use.</param>
    /// <returns>The interpolated value at the fractional position.</returns>
    /// <exception cref="InvalidOperationException">Thrown when T is not a supported numeric type.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when position is out of range.</exception>
    public double ReadInterpolated(double fractionalOffset, InterpolationMode mode = InterpolationMode.Linear)
    {
        if (!_isNumeric)
            throw new InvalidOperationException($"Interpolated read is only supported for numeric types. Type {typeof(T).Name} is not supported.");

        lock (_lock)
        {
            if (_count == 0)
                return 0.0;

            // Clamp to valid range
            double position = Math.Clamp(fractionalOffset, 0, _count - 1);

            int index0 = (int)Math.Floor(position);
            double frac = position - index0;

            return mode switch
            {
                InterpolationMode.None => GetSampleAsDouble(index0),
                InterpolationMode.Linear => InterpolateLinear(index0, frac),
                InterpolationMode.Cubic => InterpolateCubic(index0, frac),
                InterpolationMode.Hermite => InterpolateHermite(index0, frac),
                _ => GetSampleAsDouble(index0)
            };
        }
    }

    /// <summary>
    /// Reads at a fractional position using linear interpolation.
    /// Convenience method for the common case.
    /// </summary>
    /// <param name="fractionalOffset">Fractional offset from read position.</param>
    /// <returns>The linearly interpolated value.</returns>
    public double ReadLinear(double fractionalOffset)
    {
        return ReadInterpolated(fractionalOffset, InterpolationMode.Linear);
    }

    /// <summary>
    /// Gets the sample at a specific index relative to read position, converted to double.
    /// </summary>
    private double GetSampleAsDouble(int relativeIndex)
    {
        int clampedIndex = Math.Clamp(relativeIndex, 0, _count - 1);
        int actualIndex = (_readPosition + clampedIndex) % _buffer.Length;
        return ConvertToDouble(_buffer[actualIndex]);
    }

    /// <summary>
    /// Linear interpolation between two samples.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double InterpolateLinear(int index, double frac)
    {
        double y0 = GetSampleAsDouble(index);
        double y1 = GetSampleAsDouble(index + 1);
        return y0 + frac * (y1 - y0);
    }

    /// <summary>
    /// Cubic interpolation using four samples.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double InterpolateCubic(int index, double frac)
    {
        double y0 = GetSampleAsDouble(index - 1);
        double y1 = GetSampleAsDouble(index);
        double y2 = GetSampleAsDouble(index + 1);
        double y3 = GetSampleAsDouble(index + 2);

        double a0 = y3 - y2 - y0 + y1;
        double a1 = y0 - y1 - a0;
        double a2 = y2 - y0;
        double a3 = y1;

        double frac2 = frac * frac;
        return a0 * frac2 * frac + a1 * frac2 + a2 * frac + a3;
    }

    /// <summary>
    /// Hermite interpolation for high-quality audio.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double InterpolateHermite(int index, double frac)
    {
        double y0 = GetSampleAsDouble(index - 1);
        double y1 = GetSampleAsDouble(index);
        double y2 = GetSampleAsDouble(index + 1);
        double y3 = GetSampleAsDouble(index + 2);

        double c0 = y1;
        double c1 = 0.5 * (y2 - y0);
        double c2 = y0 - 2.5 * y1 + 2.0 * y2 - 0.5 * y3;
        double c3 = 0.5 * (y3 - y0) + 1.5 * (y1 - y2);

        return ((c3 * frac + c2) * frac + c1) * frac + c0;
    }

    /// <summary>
    /// Converts a value of type T to double for interpolation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ConvertToDouble(T value)
    {
        if (typeof(T) == typeof(float))
            return (double)(float)(object)value;
        if (typeof(T) == typeof(double))
            return (double)(object)value;
        if (typeof(T) == typeof(int))
            return (double)(int)(object)value;
        if (typeof(T) == typeof(short))
            return (double)(short)(object)value;
        if (typeof(T) == typeof(byte))
            return (double)(byte)(object)value;

        throw new InvalidOperationException($"Cannot convert type {typeof(T).Name} to double.");
    }

    /// <summary>
    /// Creates a copy of the current buffer contents as an array.
    /// </summary>
    /// <returns>Array containing all elements in read order.</returns>
    public T[] ToArray()
    {
        lock (_lock)
        {
            var result = new T[_count];
            if (_count == 0)
                return result;

            int firstChunk = Math.Min(_count, _buffer.Length - _readPosition);
            Array.Copy(_buffer, _readPosition, result, 0, firstChunk);

            int secondChunk = _count - firstChunk;
            if (secondChunk > 0)
            {
                Array.Copy(_buffer, 0, result, firstChunk, secondChunk);
            }

            return result;
        }
    }

    /// <summary>
    /// Fills the buffer with a specific value.
    /// </summary>
    /// <param name="value">Value to fill with.</param>
    public void Fill(T value)
    {
        lock (_lock)
        {
            Array.Fill(_buffer, value);
            _readPosition = 0;
            _writePosition = 0;
            _count = _buffer.Length;
        }
    }

    /// <summary>
    /// Gets the internal read position (for debugging/monitoring).
    /// </summary>
    public int ReadPosition
    {
        get
        {
            lock (_lock)
            {
                return _readPosition;
            }
        }
    }

    /// <summary>
    /// Gets the internal write position (for debugging/monitoring).
    /// </summary>
    public int WritePosition
    {
        get
        {
            lock (_lock)
            {
                return _writePosition;
            }
        }
    }
}

/// <summary>
/// Specialized circular buffer for float audio samples with additional audio-specific features.
/// </summary>
public class AudioCircularBuffer : CircularBuffer<float>
{
    private readonly int _channels;
    private readonly int _sampleRate;

    /// <summary>
    /// Creates a new audio circular buffer.
    /// </summary>
    /// <param name="capacityInSamples">Capacity in samples (per channel).</param>
    /// <param name="channels">Number of audio channels.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    public AudioCircularBuffer(int capacityInSamples, int channels = 2, int sampleRate = 44100)
        : base(capacityInSamples * channels)
    {
        _channels = channels;
        _sampleRate = sampleRate;
    }

    /// <summary>
    /// Gets the number of audio channels.
    /// </summary>
    public int Channels => _channels;

    /// <summary>
    /// Gets the sample rate.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the capacity in frames (samples per channel).
    /// </summary>
    public int CapacityInFrames => Capacity / _channels;

    /// <summary>
    /// Gets the current count in frames.
    /// </summary>
    public int CountInFrames => Count / _channels;

    /// <summary>
    /// Gets the duration of buffered audio in seconds.
    /// </summary>
    public double DurationSeconds => (double)CountInFrames / _sampleRate;

    /// <summary>
    /// Gets the total buffer duration capacity in seconds.
    /// </summary>
    public double CapacitySeconds => (double)CapacityInFrames / _sampleRate;

    /// <summary>
    /// Writes a stereo frame (left and right samples).
    /// </summary>
    /// <param name="left">Left channel sample.</param>
    /// <param name="right">Right channel sample.</param>
    /// <param name="overwrite">If true, overwrites oldest data when full.</param>
    /// <returns>True if write was successful.</returns>
    public bool WriteStereoFrame(float left, float right, bool overwrite = true)
    {
        if (_channels != 2)
            throw new InvalidOperationException("WriteStereoFrame requires a 2-channel buffer.");

        Span<float> frame = stackalloc float[2] { left, right };
        return Write(frame, overwrite) == 2;
    }

    /// <summary>
    /// Reads a stereo frame (left and right samples).
    /// </summary>
    /// <param name="left">Left channel sample.</param>
    /// <param name="right">Right channel sample.</param>
    /// <returns>True if read was successful.</returns>
    public bool ReadStereoFrame(out float left, out float right)
    {
        if (_channels != 2)
            throw new InvalidOperationException("ReadStereoFrame requires a 2-channel buffer.");

        Span<float> frame = stackalloc float[2];
        if (Read(frame) == 2)
        {
            left = frame[0];
            right = frame[1];
            return true;
        }

        left = 0;
        right = 0;
        return false;
    }

    /// <summary>
    /// Reads with delay in samples (for delay effects).
    /// </summary>
    /// <param name="delaySamples">Delay in samples.</param>
    /// <param name="mode">Interpolation mode for fractional delays.</param>
    /// <returns>The delayed sample value.</returns>
    public float ReadWithDelay(double delaySamples, InterpolationMode mode = InterpolationMode.Linear)
    {
        // For delay effects, we read from the past relative to write position
        double offset = Count - 1 - delaySamples;
        if (offset < 0) offset = 0;
        return (float)ReadInterpolated(offset, mode);
    }

    /// <summary>
    /// Reads with delay in milliseconds.
    /// </summary>
    /// <param name="delayMs">Delay in milliseconds.</param>
    /// <param name="mode">Interpolation mode.</param>
    /// <returns>The delayed sample value.</returns>
    public float ReadWithDelayMs(double delayMs, InterpolationMode mode = InterpolationMode.Linear)
    {
        double delaySamples = delayMs * _sampleRate / 1000.0 * _channels;
        return ReadWithDelay(delaySamples, mode);
    }

    /// <summary>
    /// Creates an audio buffer sized for a specific delay time.
    /// </summary>
    /// <param name="maxDelayMs">Maximum delay time in milliseconds.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="sampleRate">Sample rate.</param>
    /// <returns>Appropriately sized audio circular buffer.</returns>
    public static AudioCircularBuffer CreateForDelay(double maxDelayMs, int channels = 2, int sampleRate = 44100)
    {
        int samples = (int)Math.Ceiling(maxDelayMs * sampleRate / 1000.0) + 1;
        return new AudioCircularBuffer(samples, channels, sampleRate);
    }
}
