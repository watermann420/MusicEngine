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
    /// Returns a previously rented buffer to the pool.
    /// </summary>
    void Return(float[] buffer, bool clearArray = false);

    /// <summary>
    /// Returns a previously rented buffer to the pool.
    /// </summary>
    void Return<T>(T[] buffer, bool clearArray = false);
}
