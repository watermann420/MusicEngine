/**
 * @file vst2_host.h
 * @brief VST2 Plugin Host Implementation
 */

#ifndef VST2_HOST_H
#define VST2_HOST_H

#include "audio_buffer.h"
#include "midi_queue.h"

#ifdef _WIN32
#include <windows.h>
#endif

// Forward declaration for VST2 AEffect structure
struct AEffect;

namespace MusicEngine::VstBridge {

/**
 * @brief VST2 Plugin Host class
 *
 * Handles loading, communication, and audio processing for VST2 plugins.
 */
class Vst2Host {
public:
    /**
     * @brief Constructs a VST2 host
     * @param sampleRate Initial sample rate
     * @param blockSize Initial block size
     */
    Vst2Host(int sampleRate, int blockSize);

    /**
     * @brief Destructor - unloads any loaded plugin
     */
    ~Vst2Host();

    // Prevent copying
    Vst2Host(const Vst2Host&) = delete;
    Vst2Host& operator=(const Vst2Host&) = delete;

    // Allow moving
    Vst2Host(Vst2Host&& other) noexcept;
    Vst2Host& operator=(Vst2Host&& other) noexcept;

    /**
     * @brief Loads a VST2 plugin from a DLL path
     * @param path Path to the VST2 DLL
     * @return true on success, false on failure
     */
    bool LoadPlugin(const char* path);

    /**
     * @brief Unloads the current plugin
     */
    void UnloadPlugin();

    /**
     * @brief Checks if a plugin is currently loaded
     * @return true if a plugin is loaded
     */
    bool IsLoaded() const { return effect_ != nullptr; }

    // Audio Processing
    void Process(float** inputs, float** outputs, int numSamples);
    void ProcessReplacing(float** inputs, float** outputs, int numSamples);
    void StartProcessing();
    void StopProcessing();
    void Suspend();
    void Resume();

    // Configuration
    void SetSampleRate(int sampleRate);
    void SetBlockSize(int blockSize);
    int GetSampleRate() const { return sampleRate_; }
    int GetBlockSize() const { return blockSize_; }

    // Parameters
    int GetParameterCount() const;
    float GetParameter(int index) const;
    void SetParameter(int index, float value);
    void GetParameterName(int index, char* buffer, int maxLen) const;
    void GetParameterDisplay(int index, char* buffer, int maxLen) const;
    void GetParameterLabel(int index, char* buffer, int maxLen) const;

    // MIDI
    void SendMidi(int deltaFrames, int status, int data1, int data2);
    void SendMidiSysEx(const unsigned char* data, int length);
    void ClearMidi();
    void AllNotesOff();
    MidiQueue& GetMidiQueue() { return midiQueue_; }

    // Plugin Info
    void GetPluginName(char* buffer, int maxLen) const;
    void GetVendorName(char* buffer, int maxLen) const;
    void GetProductName(char* buffer, int maxLen) const;
    int GetVersion() const;
    int GetNumInputs() const;
    int GetNumOutputs() const;
    bool IsSynth() const;
    unsigned int GetUniqueId() const;
    int GetLatency() const;
    int GetTailSize() const;

    // Programs/Presets
    int GetProgramCount() const;
    int GetProgram() const;
    void SetProgram(int index);
    void GetProgramName(int index, char* buffer, int maxLen) const;

    // State
    int GetState(unsigned char* data, int maxLen) const;
    bool SetState(const unsigned char* data, int length);

    // Editor
    bool HasEditor() const;
    void OpenEditor(void* parentWindow);
    void CloseEditor();
    void GetEditorSize(int* width, int* height) const;
    void EditorIdle();
    bool IsEditorOpen() const { return editorOpen_; }

    // Transport
    void SetTransport(double samplePos, double tempo, int timeSigNum, int timeSigDen);
    void SetTransportState(bool playing, bool recording, bool looping);

    // Error handling
    const char* GetLastError() const { return lastError_; }

private:
    // VST2 Host callback (static because VST2 uses C-style callbacks)
    static intptr_t VSTCALLBACK HostCallback(AEffect* effect, int opcode,
        int index, intptr_t value, void* ptr, float opt);

    // Instance callback handler
    intptr_t HandleHostCallback(int opcode, int index, intptr_t value, void* ptr, float opt);

    // Dispatch helper
    intptr_t Dispatch(int opcode, int index = 0, intptr_t value = 0,
        void* ptr = nullptr, float opt = 0.0f) const;

    // Process MIDI events before audio processing
    void ProcessMidiEvents();

private:
    AEffect* effect_ = nullptr;
    HMODULE module_ = nullptr;

    int sampleRate_;
    int blockSize_;
    bool isProcessing_ = false;
    bool editorOpen_ = false;

    // Audio buffers
    AudioBufferPool bufferPool_;
    MidiQueue midiQueue_;

    // MIDI event storage for VST2
    struct VstEvents* vstEvents_ = nullptr;
    static constexpr int MaxMidiEvents = 1024;

    // Transport info
    double samplePosition_ = 0.0;
    double tempo_ = 120.0;
    int timeSigNumerator_ = 4;
    int timeSigDenominator_ = 4;
    bool isPlaying_ = false;
    bool isRecording_ = false;
    bool isLooping_ = false;

    // Error message
    mutable char lastError_[256] = {0};
};

} // namespace MusicEngine::VstBridge

#endif // VST2_HOST_H
