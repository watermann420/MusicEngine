using System.Buffers;

namespace MusicEngine.Infrastructure.Memory;

/// <summary>
/// ArrayPool-based implementation of audio buffer pooling.
/// </summary>
public class AudioBufferPool : IAudioBufferPool
{
    private readonly ArrayPool<float> _floatPool;
    private readonly ArrayPool<byte> _bytePool;
    private readonly ArrayPool<double> _doublePool;

    public AudioBufferPool()
    {
        // Use shared pools for efficiency
        _floatPool = ArrayPool<float>.Shared;
        _bytePool = ArrayPool<byte>.Shared;
        _doublePool = ArrayPool<double>.Shared;
    }

    public RentedBuffer<float> Rent(int minimumLength)
    {
        var buffer = _floatPool.Rent(minimumLength);
        return new RentedBuffer<float>(buffer, minimumLength, this);
    }

    public RentedBuffer<T> Rent<T>(int minimumLength)
    {
        var pool = ArrayPool<T>.Shared;
        var buffer = pool.Rent(minimumLength);
        return new RentedBuffer<T>(buffer, minimumLength, pool);
    }

    public void Return(float[] buffer, bool clearArray = false)
    {
        _floatPool.Return(buffer, clearArray);
    }

    public void Return<T>(T[] buffer, bool clearArray = false)
    {
        ArrayPool<T>.Shared.Return(buffer, clearArray);
    }
}
