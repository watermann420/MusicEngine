/**
 * @file audio_buffer.cpp
 * @brief Pre-allocated Audio Buffer Pool Implementation
 */

#include "../include/audio_buffer.h"

#include <cstdlib>
#include <cstring>
#include <algorithm>

#ifdef _WIN32
#include <malloc.h>
#endif

namespace MusicEngine::VstBridge {

// =============================================================================
// AlignedBuffer Implementation
// =============================================================================

AlignedBuffer::AlignedBuffer(size_t sizeInBytes) {
    Allocate(sizeInBytes);
}

AlignedBuffer::~AlignedBuffer() {
    Free();
}

AlignedBuffer::AlignedBuffer(AlignedBuffer&& other) noexcept
    : data_(other.data_), size_(other.size_) {
    other.data_ = nullptr;
    other.size_ = 0;
}

AlignedBuffer& AlignedBuffer::operator=(AlignedBuffer&& other) noexcept {
    if (this != &other) {
        Free();
        data_ = other.data_;
        size_ = other.size_;
        other.data_ = nullptr;
        other.size_ = 0;
    }
    return *this;
}

void AlignedBuffer::Allocate(size_t sizeInBytes) {
    if (sizeInBytes == size_ && data_ != nullptr) {
        return; // Already allocated with correct size
    }

    Free();

    if (sizeInBytes == 0) {
        return;
    }

#ifdef _WIN32
    data_ = _aligned_malloc(sizeInBytes, Alignment);
#else
    if (posix_memalign(&data_, Alignment, sizeInBytes) != 0) {
        data_ = nullptr;
    }
#endif

    if (data_) {
        size_ = sizeInBytes;
        Clear();
    }
}

void AlignedBuffer::Free() {
    if (data_) {
#ifdef _WIN32
        _aligned_free(data_);
#else
        free(data_);
#endif
        data_ = nullptr;
        size_ = 0;
    }
}

void AlignedBuffer::Clear() {
    if (data_ && size_ > 0) {
        std::memset(data_, 0, size_);
    }
}

// =============================================================================
// AudioBuffer Implementation
// =============================================================================

AudioBuffer::AudioBuffer(int numChannels, int numSamples) {
    Resize(numChannels, numSamples);
}

void AudioBuffer::Resize(int numChannels, int numSamples) {
    if (numChannels == numChannels_ && numSamples == numSamples_) {
        return;
    }

    numChannels_ = numChannels;
    numSamples_ = numSamples;

    if (numChannels <= 0 || numSamples <= 0) {
        buffer_.Free();
        channelPtrs_.clear();
        return;
    }

    size_t bytesPerChannel = static_cast<size_t>(numSamples) * sizeof(float);
    size_t totalBytes = static_cast<size_t>(numChannels) * bytesPerChannel;

    buffer_.Allocate(totalBytes);

    channelPtrs_.resize(numChannels);
    float* data = buffer_.AsFloat();
    for (int i = 0; i < numChannels; ++i) {
        channelPtrs_[i] = data + (i * numSamples);
    }
}

void AudioBuffer::Clear() {
    buffer_.Clear();
}

float* AudioBuffer::GetChannel(int channel) {
    if (channel < 0 || channel >= numChannels_) {
        return nullptr;
    }
    return channelPtrs_[channel];
}

const float* AudioBuffer::GetChannel(int channel) const {
    if (channel < 0 || channel >= numChannels_) {
        return nullptr;
    }
    return channelPtrs_[channel];
}

void AudioBuffer::CopyFrom(float** inputs, int numChannels, int numSamples) {
    if (!inputs) return;

    int channelsToCopy = std::min(numChannels, numChannels_);
    int samplesToCopy = std::min(numSamples, numSamples_);

    for (int ch = 0; ch < channelsToCopy; ++ch) {
        if (inputs[ch] && channelPtrs_[ch]) {
            std::memcpy(channelPtrs_[ch], inputs[ch], samplesToCopy * sizeof(float));
        }
    }
}

void AudioBuffer::CopyTo(float** outputs, int numChannels, int numSamples) const {
    if (!outputs) return;

    int channelsToCopy = std::min(numChannels, numChannels_);
    int samplesToCopy = std::min(numSamples, numSamples_);

    for (int ch = 0; ch < channelsToCopy; ++ch) {
        if (outputs[ch] && channelPtrs_[ch]) {
            std::memcpy(outputs[ch], channelPtrs_[ch], samplesToCopy * sizeof(float));
        }
    }
}

// =============================================================================
// AudioBufferPool Implementation
// =============================================================================

AudioBufferPool::AudioBufferPool(int maxChannels, int maxSamples, int poolSize)
    : maxChannels_(maxChannels), maxSamples_(maxSamples) {

    pool_.reserve(poolSize);
    inUse_.resize(poolSize);

    for (int i = 0; i < poolSize; ++i) {
        pool_.push_back(std::make_unique<AudioBuffer>(maxChannels, maxSamples));
        inUse_[i].store(false, std::memory_order_relaxed);
    }

    inputBuffer_.Resize(maxChannels, maxSamples);
    outputBuffer_.Resize(maxChannels, maxSamples);
}

void AudioBufferPool::Resize(int maxChannels, int maxSamples) {
    maxChannels_ = maxChannels;
    maxSamples_ = maxSamples;

    for (auto& buffer : pool_) {
        buffer->Resize(maxChannels, maxSamples);
    }

    inputBuffer_.Resize(maxChannels, maxSamples);
    outputBuffer_.Resize(maxChannels, maxSamples);
}

AudioBuffer* AudioBufferPool::Acquire() {
    for (size_t i = 0; i < pool_.size(); ++i) {
        bool expected = false;
        if (inUse_[i].compare_exchange_strong(expected, true,
            std::memory_order_acquire, std::memory_order_relaxed)) {
            return pool_[i].get();
        }
    }
    return nullptr; // Pool exhausted
}

void AudioBufferPool::Release(AudioBuffer* buffer) {
    for (size_t i = 0; i < pool_.size(); ++i) {
        if (pool_[i].get() == buffer) {
            inUse_[i].store(false, std::memory_order_release);
            return;
        }
    }
}

// =============================================================================
// AudioBufferDouble Implementation
// =============================================================================

AudioBufferDouble::AudioBufferDouble(int numChannels, int numSamples) {
    Resize(numChannels, numSamples);
}

void AudioBufferDouble::Resize(int numChannels, int numSamples) {
    if (numChannels == numChannels_ && numSamples == numSamples_) {
        return;
    }

    numChannels_ = numChannels;
    numSamples_ = numSamples;

    if (numChannels <= 0 || numSamples <= 0) {
        buffer_.Free();
        channelPtrs_.clear();
        return;
    }

    size_t bytesPerChannel = static_cast<size_t>(numSamples) * sizeof(double);
    size_t totalBytes = static_cast<size_t>(numChannels) * bytesPerChannel;

    buffer_.Allocate(totalBytes);

    channelPtrs_.resize(numChannels);
    double* data = buffer_.AsDouble();
    for (int i = 0; i < numChannels; ++i) {
        channelPtrs_[i] = data + (i * numSamples);
    }
}

void AudioBufferDouble::Clear() {
    buffer_.Clear();
}

double* AudioBufferDouble::GetChannel(int channel) {
    if (channel < 0 || channel >= numChannels_) {
        return nullptr;
    }
    return channelPtrs_[channel];
}

const double* AudioBufferDouble::GetChannel(int channel) const {
    if (channel < 0 || channel >= numChannels_) {
        return nullptr;
    }
    return channelPtrs_[channel];
}

void AudioBufferDouble::CopyFrom(double** inputs, int numChannels, int numSamples) {
    if (!inputs) return;

    int channelsToCopy = std::min(numChannels, numChannels_);
    int samplesToCopy = std::min(numSamples, numSamples_);

    for (int ch = 0; ch < channelsToCopy; ++ch) {
        if (inputs[ch] && channelPtrs_[ch]) {
            std::memcpy(channelPtrs_[ch], inputs[ch], samplesToCopy * sizeof(double));
        }
    }
}

void AudioBufferDouble::CopyTo(double** outputs, int numChannels, int numSamples) const {
    if (!outputs) return;

    int channelsToCopy = std::min(numChannels, numChannels_);
    int samplesToCopy = std::min(numSamples, numSamples_);

    for (int ch = 0; ch < channelsToCopy; ++ch) {
        if (outputs[ch] && channelPtrs_[ch]) {
            std::memcpy(outputs[ch], channelPtrs_[ch], samplesToCopy * sizeof(double));
        }
    }
}

void AudioBufferDouble::ConvertFrom(const AudioBuffer& floatBuffer) {
    Resize(floatBuffer.NumChannels(), floatBuffer.NumSamples());

    for (int ch = 0; ch < numChannels_; ++ch) {
        const float* src = floatBuffer.GetChannel(ch);
        double* dst = channelPtrs_[ch];
        if (src && dst) {
            for (int i = 0; i < numSamples_; ++i) {
                dst[i] = static_cast<double>(src[i]);
            }
        }
    }
}

void AudioBufferDouble::ConvertTo(AudioBuffer& floatBuffer) const {
    floatBuffer.Resize(numChannels_, numSamples_);

    for (int ch = 0; ch < numChannels_; ++ch) {
        const double* src = channelPtrs_[ch];
        float* dst = floatBuffer.GetChannel(ch);
        if (src && dst) {
            for (int i = 0; i < numSamples_; ++i) {
                dst[i] = static_cast<float>(src[i]);
            }
        }
    }
}

} // namespace MusicEngine::VstBridge
