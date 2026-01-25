namespace MusicEngine.Infrastructure.Memory;

/// <summary>
/// IDisposable wrapper for rented audio buffers that automatically returns to pool on dispose.
/// Use with the using pattern for automatic resource management.
/// </summary>
/// <example>
/// <code>
/// using var buffer = AudioBufferPool.Instance.RentScoped(1024);
/// // Work with buffer.Data span
/// ProcessAudio(buffer.Data);
/// // Buffer is automatically returned on dispose
/// </code>
/// </example>
public sealed class PooledAudioBuffer : IDisposable
{
    private readonly float[] _buffer;
    private readonly int _length;
    private readonly OptimizedAudioBufferPool _pool;
    private bool _disposed;

    /// <summary>
    /// Gets the data as a Span.
    /// </summary>
    public Span<float> Data => _disposed
        ? throw new ObjectDisposedException(nameof(PooledAudioBuffer))
        : _buffer.AsSpan(0, _length);

    /// <summary>
    /// Gets the data as a ReadOnlySpan.
    /// </summary>
    public ReadOnlySpan<float> ReadOnlyData => Data;

    /// <summary>
    /// Gets the data as a Memory.
    /// </summary>
    public Memory<float> Memory => _disposed
        ? throw new ObjectDisposedException(nameof(PooledAudioBuffer))
        : _buffer.AsMemory(0, _length);

    /// <summary>
    /// Gets the requested length (not the actual buffer size which may be larger).
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets the actual underlying buffer array.
    /// Use with caution - prefer Data span for bounds-checked access.
    /// </summary>
    public float[] Array => _disposed
        ? throw new ObjectDisposedException(nameof(PooledAudioBuffer))
        : _buffer;

    /// <summary>
    /// Gets whether this buffer has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Creates a new pooled audio buffer wrapper.
    /// </summary>
    internal PooledAudioBuffer(float[] buffer, int length, OptimizedAudioBufferPool pool)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _length = length;
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _disposed = false;
    }

    /// <summary>
    /// Indexer for direct element access.
    /// </summary>
    public ref float this[int index]
    {
        get
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PooledAudioBuffer));
            if ((uint)index >= (uint)_length)
                throw new IndexOutOfRangeException();
            return ref _buffer[index];
        }
    }

    /// <summary>
    /// Clears the buffer contents to zero.
    /// </summary>
    public void Clear()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PooledAudioBuffer));
        System.Array.Clear(_buffer, 0, _length);
    }

    /// <summary>
    /// Copies data from a source span into this buffer.
    /// </summary>
    /// <param name="source">Source data to copy.</param>
    public void CopyFrom(ReadOnlySpan<float> source)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PooledAudioBuffer));
        if (source.Length > _length)
            throw new ArgumentException("Source is larger than buffer", nameof(source));
        source.CopyTo(Data);
    }

    /// <summary>
    /// Copies data from this buffer to a destination span.
    /// </summary>
    /// <param name="destination">Destination for the data.</param>
    public void CopyTo(Span<float> destination)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PooledAudioBuffer));
        Data.CopyTo(destination);
    }

    /// <summary>
    /// Fills the buffer with a specified value.
    /// </summary>
    /// <param name="value">The value to fill with.</param>
    public void Fill(float value)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PooledAudioBuffer));
        Data.Fill(value);
    }

    /// <summary>
    /// Returns the buffer to the pool.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pool.Return(_buffer);
    }

    /// <summary>
    /// Implicit conversion to Span for convenience.
    /// </summary>
    public static implicit operator Span<float>(PooledAudioBuffer buffer) => buffer.Data;

    /// <summary>
    /// Implicit conversion to ReadOnlySpan for convenience.
    /// </summary>
    public static implicit operator ReadOnlySpan<float>(PooledAudioBuffer buffer) => buffer.Data;
}
