/**
 * @file midi_queue.cpp
 * @brief Lock-free MIDI Event Queue Implementation
 */

#include "../include/midi_queue.h"

#include <algorithm>
#include <new>

namespace MusicEngine::VstBridge {

// =============================================================================
// MidiQueue Implementation
// =============================================================================

MidiQueue::MidiQueue(int capacity)
    : capacity_(capacity) {

    buffer_ = new MidiEvent[capacity];
    sysexBuffer_ = new SysExEvent[MaxSysExEvents];

    writeIndex_.store(0, std::memory_order_relaxed);
    readIndex_.store(0, std::memory_order_relaxed);
    sysexWriteIndex_.store(0, std::memory_order_relaxed);
    sysexReadIndex_.store(0, std::memory_order_relaxed);
}

MidiQueue::~MidiQueue() {
    delete[] buffer_;
    delete[] sysexBuffer_;
}

bool MidiQueue::Push(const MidiEvent& event) {
    int write = writeIndex_.load(std::memory_order_relaxed);
    int nextWrite = (write + 1) % capacity_;

    // Check if full (would wrap around to read position)
    if (nextWrite == readIndex_.load(std::memory_order_acquire)) {
        return false; // Queue full
    }

    buffer_[write] = event;
    writeIndex_.store(nextWrite, std::memory_order_release);
    return true;
}

bool MidiQueue::Push(int32_t deltaFrames, uint8_t status, uint8_t data1, uint8_t data2) {
    return Push(MidiEvent(deltaFrames, status, data1, data2));
}

bool MidiQueue::Pop(MidiEvent& event) {
    int read = readIndex_.load(std::memory_order_relaxed);

    // Check if empty
    if (read == writeIndex_.load(std::memory_order_acquire)) {
        return false; // Queue empty
    }

    event = buffer_[read];
    readIndex_.store((read + 1) % capacity_, std::memory_order_release);
    return true;
}

bool MidiQueue::Peek(MidiEvent& event) const {
    int read = readIndex_.load(std::memory_order_relaxed);

    if (read == writeIndex_.load(std::memory_order_acquire)) {
        return false;
    }

    event = buffer_[read];
    return true;
}

bool MidiQueue::PushSysEx(const SysExEvent& event) {
    int write = sysexWriteIndex_.load(std::memory_order_relaxed);
    int nextWrite = (write + 1) % MaxSysExEvents;

    if (nextWrite == sysexReadIndex_.load(std::memory_order_acquire)) {
        return false;
    }

    sysexBuffer_[write] = event;
    sysexWriteIndex_.store(nextWrite, std::memory_order_release);
    return true;
}

bool MidiQueue::PushSysEx(int32_t deltaFrames, const uint8_t* data, int length) {
    if (length > static_cast<int>(sizeof(SysExEvent::data))) {
        return false; // SysEx too large
    }

    SysExEvent event;
    event.deltaFrames = deltaFrames;
    event.length = length;
    std::memcpy(event.data, data, length);

    return PushSysEx(event);
}

bool MidiQueue::PopSysEx(SysExEvent& event) {
    int read = sysexReadIndex_.load(std::memory_order_relaxed);

    if (read == sysexWriteIndex_.load(std::memory_order_acquire)) {
        return false;
    }

    event = sysexBuffer_[read];
    sysexReadIndex_.store((read + 1) % MaxSysExEvents, std::memory_order_release);
    return true;
}

void MidiQueue::Clear() {
    readIndex_.store(writeIndex_.load(std::memory_order_relaxed),
        std::memory_order_relaxed);
    sysexReadIndex_.store(sysexWriteIndex_.load(std::memory_order_relaxed),
        std::memory_order_relaxed);
}

void MidiQueue::AllNotesOff() {
    // Send All Notes Off (CC 123) on all 16 channels
    for (uint8_t ch = 0; ch < 16; ++ch) {
        Push(MidiEvent::ControlChange(0, ch, 123, 0));
    }
}

void MidiQueue::AllSoundOff() {
    // Send All Sound Off (CC 120) on all 16 channels
    for (uint8_t ch = 0; ch < 16; ++ch) {
        Push(MidiEvent::ControlChange(0, ch, 120, 0));
    }
}

int MidiQueue::Size() const {
    int write = writeIndex_.load(std::memory_order_acquire);
    int read = readIndex_.load(std::memory_order_acquire);

    if (write >= read) {
        return write - read;
    }
    return capacity_ - read + write;
}

int MidiQueue::SysExSize() const {
    int write = sysexWriteIndex_.load(std::memory_order_acquire);
    int read = sysexReadIndex_.load(std::memory_order_acquire);

    if (write >= read) {
        return write - read;
    }
    return MaxSysExEvents - read + write;
}

bool MidiQueue::IsEmpty() const {
    return readIndex_.load(std::memory_order_acquire) ==
           writeIndex_.load(std::memory_order_acquire);
}

bool MidiQueue::IsFull() const {
    int write = writeIndex_.load(std::memory_order_relaxed);
    int nextWrite = (write + 1) % capacity_;
    return nextWrite == readIndex_.load(std::memory_order_acquire);
}

int MidiQueue::GetSortedEvents(MidiEvent* events, int maxEvents) {
    if (!events || maxEvents <= 0) {
        return 0;
    }

    int count = 0;
    MidiEvent event;

    // Pop all events into the output array
    while (count < maxEvents && Pop(event)) {
        events[count++] = event;
    }

    // Sort by delta time (stable sort to preserve order for same time)
    std::stable_sort(events, events + count,
        [](const MidiEvent& a, const MidiEvent& b) {
            return a.deltaFrames < b.deltaFrames;
        });

    return count;
}

// =============================================================================
// MidiEventBuffer Implementation
// =============================================================================

void MidiEventBuffer::Sort() {
    std::stable_sort(events_, events_ + count_,
        [](const MidiEvent& a, const MidiEvent& b) {
            return a.deltaFrames < b.deltaFrames;
        });
}

} // namespace MusicEngine::VstBridge
