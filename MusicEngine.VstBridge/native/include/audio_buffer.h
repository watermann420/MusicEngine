/**
 * @file audio_buffer.h
 * @brief Pre-allocated Audio Buffer Pool for VST Processing
 *
 * Provides zero-allocation audio processing by pre-allocating buffers
 * and reusing them across process calls.
 */

#ifndef AUDIO_BUFFER_H
#define AUDIO_BUFFER_H

#include <cstddef>
#include <cstdint>
#include <memory>
#include <vector>
#include <atomic>

namespace MusicEngine::VstBridge {

/**
 * @brief Aligned memory block for audio data
 *
 * Uses 64-byte alignment for optimal SIMD performance and cache line alignment.
 */
class AlignedBuffer {
public:
    static constexpr size_t Alignment = 64;

    AlignedBuffer() = default;
    explicit AlignedBuffer(size_t sizeInBytes);
    ~AlignedBuffer();

    // Move semantics
    AlignedBuffer(AlignedBuffer&& other) noexcept;
    AlignedBuffer& operator=(AlignedBuffer&& other) noexcept;

    // No copying
    AlignedBuffer(const AlignedBuffer&) = delete;
    AlignedBuffer& operator=(const AlignedBuffer&) = delete;

    /**
     * @brief Allocates or reallocates the buffer
     * @param sizeInBytes New size in bytes
     */
    void Allocate(size_t sizeInBytes);

    /**
     * @brief Frees the buffer
     */
    void Free();

    /**
     * @brief Gets the raw data pointer
     */
    void* Data() { return data_; }
    const void* Data() const { return data_; }

    /**
     * @brief Gets the data as float pointer
     */
    float* AsFloat() { return static_cast<float*>(data_); }
    const float* AsFloat() const { return static_cast<const float*>(data_); }

    /**
     * @brief Gets the data as double pointer
     */
    double* AsDouble() { return static_cast<double*>(data_); }
    const double* AsDouble() const { return static_cast<const double*>(data_); }

    /**
     * @brief Gets the buffer size in bytes
     */
    size_t Size() const { return size_; }

    /**
     * @brief Checks if buffer is allocated
     */
    bool IsAllocated() const { return data_ != nullptr; }

    /**
     * @brief Clears the buffer to zero
     */
    void Clear();

private:
    void* data_ = nullptr;
    size_t size_ = 0;
};

/**
 * @brief Audio buffer with channel management
 *
 * Manages a multi-channel audio buffer with pre-allocated memory.
 */
class AudioBuffer {
public:
    AudioBuffer() = default;

    /**
     * @brief Constructs a buffer with specified dimensions
     * @param numChannels Number of audio channels
     * @param numSamples Number of samples per channel
     */
    AudioBuffer(int numChannels, int numSamples);

    /**
     * @brief Resizes the buffer (may reallocate)
     * @param numChannels New number of channels
     * @param numSamples New number of samples
     */
    void Resize(int numChannels, int numSamples);

    /**
     * @brief Clears all channels to zero
     */
    void Clear();

    /**
     * @brief Gets a channel's data
     * @param channel Channel index
     * @return Pointer to channel data
     */
    float* GetChannel(int channel);
    const float* GetChannel(int channel) const;

    /**
     * @brief Gets array of channel pointers (for VST process calls)
     * @return Array of float* pointers
     */
    float** GetChannelArray() { return channelPtrs_.data(); }
    const float* const* GetChannelArray() const { return channelPtrs_.data(); }

    /**
     * @brief Copies data from external buffers
     * @param inputs External buffer array
     * @param numChannels Number of channels to copy
     * @param numSamples Number of samples to copy
     */
    void CopyFrom(float** inputs, int numChannels, int numSamples);

    /**
     * @brief Copies data to external buffers
     * @param outputs External buffer array
     * @param numChannels Number of channels to copy
     * @param numSamples Number of samples to copy
     */
    void CopyTo(float** outputs, int numChannels, int numSamples) const;

    int NumChannels() const { return numChannels_; }
    int NumSamples() const { return numSamples_; }

private:
    AlignedBuffer buffer_;
    std::vector<float*> channelPtrs_;
    int numChannels_ = 0;
    int numSamples_ = 0;
};

/**
 * @brief Pool of pre-allocated audio buffers
 *
 * Provides buffer reuse without runtime allocation during audio processing.
 * Thread-safe for single-producer/single-consumer use.
 */
class AudioBufferPool {
public:
    /**
     * @brief Constructs a buffer pool
     * @param maxChannels Maximum channels per buffer
     * @param maxSamples Maximum samples per buffer
     * @param poolSize Number of buffers to pre-allocate
     */
    AudioBufferPool(int maxChannels = 8, int maxSamples = 4096, int poolSize = 4);

    /**
     * @brief Resizes all buffers in the pool
     * @param maxChannels New maximum channels
     * @param maxSamples New maximum samples
     */
    void Resize(int maxChannels, int maxSamples);

    /**
     * @brief Gets a buffer from the pool
     * @return Pointer to available buffer, or nullptr if pool exhausted
     */
    AudioBuffer* Acquire();

    /**
     * @brief Returns a buffer to the pool
     * @param buffer Buffer to return
     */
    void Release(AudioBuffer* buffer);

    /**
     * @brief Gets the internal input buffer
     */
    AudioBuffer& GetInputBuffer() { return inputBuffer_; }

    /**
     * @brief Gets the internal output buffer
     */
    AudioBuffer& GetOutputBuffer() { return outputBuffer_; }

    int MaxChannels() const { return maxChannels_; }
    int MaxSamples() const { return maxSamples_; }

private:
    std::vector<std::unique_ptr<AudioBuffer>> pool_;
    std::vector<std::atomic<bool>> inUse_;
    AudioBuffer inputBuffer_;
    AudioBuffer outputBuffer_;
    int maxChannels_;
    int maxSamples_;
};

/**
 * @brief RAII wrapper for pool buffer acquisition
 */
class ScopedBuffer {
public:
    ScopedBuffer(AudioBufferPool& pool) : pool_(pool), buffer_(pool.Acquire()) {}
    ~ScopedBuffer() { if (buffer_) pool_.Release(buffer_); }

    // No copying
    ScopedBuffer(const ScopedBuffer&) = delete;
    ScopedBuffer& operator=(const ScopedBuffer&) = delete;

    AudioBuffer* operator->() { return buffer_; }
    AudioBuffer& operator*() { return *buffer_; }
    AudioBuffer* Get() { return buffer_; }
    operator bool() const { return buffer_ != nullptr; }

private:
    AudioBufferPool& pool_;
    AudioBuffer* buffer_;
};

/**
 * @brief Double-precision audio buffer
 */
class AudioBufferDouble {
public:
    AudioBufferDouble() = default;
    AudioBufferDouble(int numChannels, int numSamples);

    void Resize(int numChannels, int numSamples);
    void Clear();

    double* GetChannel(int channel);
    const double* GetChannel(int channel) const;
    double** GetChannelArray() { return channelPtrs_.data(); }

    void CopyFrom(double** inputs, int numChannels, int numSamples);
    void CopyTo(double** outputs, int numChannels, int numSamples) const;

    // Convert from/to float buffers
    void ConvertFrom(const AudioBuffer& floatBuffer);
    void ConvertTo(AudioBuffer& floatBuffer) const;

    int NumChannels() const { return numChannels_; }
    int NumSamples() const { return numSamples_; }

private:
    AlignedBuffer buffer_;
    std::vector<double*> channelPtrs_;
    int numChannels_ = 0;
    int numSamples_ = 0;
};

} // namespace MusicEngine::VstBridge

#endif // AUDIO_BUFFER_H
