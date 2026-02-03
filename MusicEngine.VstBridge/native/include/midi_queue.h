/**
 * @file midi_queue.h
 * @brief Lock-free MIDI Event Queue for VST Processing
 *
 * Provides a lock-free ring buffer for MIDI events to allow
 * real-time safe communication between threads.
 */

#ifndef MIDI_QUEUE_H
#define MIDI_QUEUE_H

#include <cstdint>
#include <atomic>
#include <cstring>

namespace MusicEngine::VstBridge {

/**
 * @brief MIDI event data structure
 */
struct MidiEvent {
    int32_t deltaFrames;    ///< Sample offset from block start
    uint8_t status;         ///< MIDI status byte
    uint8_t data1;          ///< MIDI data byte 1
    uint8_t data2;          ///< MIDI data byte 2
    uint8_t padding;        ///< Padding for alignment

    MidiEvent() : deltaFrames(0), status(0), data1(0), data2(0), padding(0) {}

    MidiEvent(int32_t delta, uint8_t stat, uint8_t d1, uint8_t d2)
        : deltaFrames(delta), status(stat), data1(d1), data2(d2), padding(0) {}

    /**
     * @brief Creates a Note On event
     */
    static MidiEvent NoteOn(int32_t delta, uint8_t channel, uint8_t note, uint8_t velocity) {
        return MidiEvent(delta, 0x90 | (channel & 0x0F), note, velocity);
    }

    /**
     * @brief Creates a Note Off event
     */
    static MidiEvent NoteOff(int32_t delta, uint8_t channel, uint8_t note, uint8_t velocity = 0) {
        return MidiEvent(delta, 0x80 | (channel & 0x0F), note, velocity);
    }

    /**
     * @brief Creates a Control Change event
     */
    static MidiEvent ControlChange(int32_t delta, uint8_t channel, uint8_t cc, uint8_t value) {
        return MidiEvent(delta, 0xB0 | (channel & 0x0F), cc, value);
    }

    /**
     * @brief Creates a Program Change event
     */
    static MidiEvent ProgramChange(int32_t delta, uint8_t channel, uint8_t program) {
        return MidiEvent(delta, 0xC0 | (channel & 0x0F), program, 0);
    }

    /**
     * @brief Creates a Pitch Bend event
     */
    static MidiEvent PitchBend(int32_t delta, uint8_t channel, int16_t value) {
        // value: -8192 to 8191, map to 0-16383
        uint16_t mapped = static_cast<uint16_t>(value + 8192);
        return MidiEvent(delta, 0xE0 | (channel & 0x0F),
            static_cast<uint8_t>(mapped & 0x7F),
            static_cast<uint8_t>((mapped >> 7) & 0x7F));
    }

    /**
     * @brief Gets the MIDI channel (0-15)
     */
    uint8_t GetChannel() const { return status & 0x0F; }

    /**
     * @brief Gets the MIDI message type
     */
    uint8_t GetType() const { return status & 0xF0; }

    /**
     * @brief Checks if this is a Note On event
     */
    bool IsNoteOn() const { return GetType() == 0x90 && data2 > 0; }

    /**
     * @brief Checks if this is a Note Off event (or Note On with velocity 0)
     */
    bool IsNoteOff() const { return GetType() == 0x80 || (GetType() == 0x90 && data2 == 0); }
};

/**
 * @brief SysEx event data structure
 */
struct SysExEvent {
    int32_t deltaFrames;        ///< Sample offset from block start
    int32_t length;             ///< Data length
    uint8_t data[256];          ///< SysEx data (including F0 and F7)

    SysExEvent() : deltaFrames(0), length(0) {
        std::memset(data, 0, sizeof(data));
    }
};

/**
 * @brief Lock-free SPSC (Single Producer, Single Consumer) MIDI queue
 *
 * Uses atomic operations for thread-safe access without locks.
 * The producer (main thread) can push events while the consumer
 * (audio thread) reads them without blocking.
 */
class MidiQueue {
public:
    static constexpr int DefaultCapacity = 1024;
    static constexpr int MaxSysExEvents = 16;

    /**
     * @brief Constructs a MIDI queue
     * @param capacity Maximum number of MIDI events
     */
    explicit MidiQueue(int capacity = DefaultCapacity);

    /**
     * @brief Destructor
     */
    ~MidiQueue();

    // No copying
    MidiQueue(const MidiQueue&) = delete;
    MidiQueue& operator=(const MidiQueue&) = delete;

    /**
     * @brief Pushes a MIDI event to the queue
     * @param event The event to push
     * @return true if successful, false if queue is full
     */
    bool Push(const MidiEvent& event);

    /**
     * @brief Pushes a MIDI event to the queue
     * @param deltaFrames Sample offset
     * @param status MIDI status byte
     * @param data1 MIDI data byte 1
     * @param data2 MIDI data byte 2
     * @return true if successful, false if queue is full
     */
    bool Push(int32_t deltaFrames, uint8_t status, uint8_t data1, uint8_t data2);

    /**
     * @brief Pops a MIDI event from the queue
     * @param event Output event
     * @return true if an event was available, false if queue is empty
     */
    bool Pop(MidiEvent& event);

    /**
     * @brief Peeks at the next event without removing it
     * @param event Output event
     * @return true if an event is available, false if queue is empty
     */
    bool Peek(MidiEvent& event) const;

    /**
     * @brief Pushes a SysEx event
     * @param event The SysEx event
     * @return true if successful, false if queue is full
     */
    bool PushSysEx(const SysExEvent& event);

    /**
     * @brief Pushes SysEx data
     * @param deltaFrames Sample offset
     * @param data SysEx data (including F0 and F7)
     * @param length Data length
     * @return true if successful, false if queue is full
     */
    bool PushSysEx(int32_t deltaFrames, const uint8_t* data, int length);

    /**
     * @brief Pops a SysEx event from the queue
     * @param event Output event
     * @return true if an event was available, false if queue is empty
     */
    bool PopSysEx(SysExEvent& event);

    /**
     * @brief Clears all events in the queue
     */
    void Clear();

    /**
     * @brief Sends All Notes Off on all channels
     */
    void AllNotesOff();

    /**
     * @brief Sends All Sound Off on all channels
     */
    void AllSoundOff();

    /**
     * @brief Gets the number of events in the queue (approximate)
     */
    int Size() const;

    /**
     * @brief Gets the number of SysEx events in the queue (approximate)
     */
    int SysExSize() const;

    /**
     * @brief Checks if the queue is empty
     */
    bool IsEmpty() const;

    /**
     * @brief Checks if the queue is full
     */
    bool IsFull() const;

    /**
     * @brief Gets the queue capacity
     */
    int Capacity() const { return capacity_; }

    /**
     * @brief Gets all pending events sorted by delta time
     * @param events Output array
     * @param maxEvents Maximum events to retrieve
     * @return Number of events retrieved
     */
    int GetSortedEvents(MidiEvent* events, int maxEvents);

private:
    MidiEvent* buffer_;
    SysExEvent* sysexBuffer_;
    int capacity_;

    // SPSC queue indices (cache-line padded to prevent false sharing)
    alignas(64) std::atomic<int> writeIndex_;
    alignas(64) std::atomic<int> readIndex_;

    // SysEx queue indices
    alignas(64) std::atomic<int> sysexWriteIndex_;
    alignas(64) std::atomic<int> sysexReadIndex_;
};

/**
 * @brief Temporary storage for sorted MIDI events during processing
 */
class MidiEventBuffer {
public:
    static constexpr int MaxEvents = 1024;

    MidiEventBuffer() : count_(0) {}

    /**
     * @brief Adds an event to the buffer
     */
    bool Add(const MidiEvent& event) {
        if (count_ >= MaxEvents) return false;
        events_[count_++] = event;
        return true;
    }

    /**
     * @brief Clears all events
     */
    void Clear() { count_ = 0; }

    /**
     * @brief Sorts events by delta time
     */
    void Sort();

    /**
     * @brief Gets an event by index
     */
    const MidiEvent& operator[](int index) const { return events_[index]; }
    MidiEvent& operator[](int index) { return events_[index]; }

    /**
     * @brief Gets the number of events
     */
    int Count() const { return count_; }

    /**
     * @brief Gets the event array
     */
    MidiEvent* Data() { return events_; }
    const MidiEvent* Data() const { return events_; }

private:
    MidiEvent events_[MaxEvents];
    int count_;
};

} // namespace MusicEngine::VstBridge

#endif // MIDI_QUEUE_H
