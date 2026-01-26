// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Infrastructure.Memory;

/// <summary>
/// Interface for audio buffer pooling to reduce GC pressure.
/// </summary>
public interface IAudioBufferPool
{
    /// <summary>
    /// Rents a buffer of at least the specified size.
    /// </summary>
    /// <param name="minimumLength">Minimum required length.</param>
    /// <returns>A rented buffer that must be returned when done.</returns>
    RentedBuffer<float> Rent(int minimumLength);

    /// <summary>
    /// Rents a buffer of at least the specified size for a specific type.
    /// </summary>
    RentedBuffer<T> Rent<T>(int minimumLength);

    /// <summary>
    /// Rents a raw float array from the pool.
    /// </summary>
    /// <param name="minimumLength">Minimum required length.</param>
    /// <returns>A float array that must be returned when done.</returns>
    float[] RentArray(int minimumLength);

    /// <summary>
    /// Rents a scoped buffer that auto-returns on dispose.
    /// </summary>
    /// <param name="minimumLength">Minimum required length.</param>
    /// <returns>A pooled buffer wrapper.</returns>
    PooledAudioBuffer RentScoped(int minimumLength);

    /// <summary>
    /// Returns a previously rented buffer to the pool.
    /// </summary>
    void Return(float[] buffer, bool clearArray = false);

    /// <summary>
    /// Returns a previously rented buffer to the pool.
    /// </summary>
    void Return<T>(T[] buffer, bool clearArray = false);

    /// <summary>
    /// Releases unused buffers to reduce memory usage.
    /// </summary>
    void Trim();

    /// <summary>
    /// Gets pool statistics.
    /// </summary>
    BufferPoolStatistics GetStatistics();
}
