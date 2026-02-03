/**
 * @file vst3_host.h
 * @brief VST3 Plugin Host Implementation
 */

#ifndef VST3_HOST_H
#define VST3_HOST_H

#include "audio_buffer.h"
#include "midi_queue.h"

#ifdef _WIN32
#include <windows.h>
#endif

#include <string>
#include <memory>
#include <vector>

// Forward declarations for VST3 SDK types
namespace Steinberg {
    class IPluginFactory;
    class FUnknown;

    namespace Vst {
        class IComponent;
        class IAudioProcessor;
        class IEditController;
        class IPlugView;
        struct ProcessData;
        struct ParameterInfo;
    }
}

namespace MusicEngine::VstBridge {

/**
 * @brief VST3 Plugin Host class
 *
 * Handles loading, communication, and audio processing for VST3 plugins.
 * Implements the VST3 host application interfaces.
 */
class Vst3Host {
public:
    /**
     * @brief Constructs a VST3 host
     * @param sampleRate Initial sample rate
     * @param blockSize Initial block size
     */
    Vst3Host(int sampleRate, int blockSize);

    /**
     * @brief Destructor - unloads any loaded plugin
     */
    ~Vst3Host();

    // Prevent copying
    Vst3Host(const Vst3Host&) = delete;
    Vst3Host& operator=(const Vst3Host&) = delete;

    // Allow moving
    Vst3Host(Vst3Host&& other) noexcept;
    Vst3Host& operator=(Vst3Host&& other) noexcept;

    /**
     * @brief Loads a VST3 plugin from a bundle path
     * @param path Path to the VST3 bundle (.vst3 directory or file)
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
    bool IsLoaded() const { return component_ != nullptr; }

    // Audio Processing
    void Process(float** inputs, float** outputs, int numSamples);
    void ProcessReplacing(float** inputs, float** outputs, int numSamples);
    bool ProcessDouble(double** inputs, double** outputs, int numSamples);
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

    // Programs/Presets (Unit Programs in VST3)
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

    // VST3 specific
    bool SupportsDoublePrecision() const { return supportsDouble_; }

private:
    // Internal helper methods
    bool InitializeComponent();
    bool InitializeController();
    bool SetupProcessing();
    void ConnectComponents();
    void SyncControllerToComponent();

    // Process context setup
    void SetupProcessContext();

    // MIDI to VST3 event conversion
    void ProcessMidiEvents();

    // Parameter ID mapping (VST3 uses ParamIDs, not indices)
    struct ParameterMapping {
        uint32_t id;
        std::string name;
        std::string units;
        double defaultValue;
        int32_t stepCount;
    };
    void BuildParameterMapping();

private:
    // VST3 interfaces
    HMODULE module_ = nullptr;
    Steinberg::IPluginFactory* factory_ = nullptr;
    Steinberg::Vst::IComponent* component_ = nullptr;
    Steinberg::Vst::IAudioProcessor* processor_ = nullptr;
    Steinberg::Vst::IEditController* controller_ = nullptr;
    Steinberg::Vst::IPlugView* view_ = nullptr;

    // Configuration
    int sampleRate_;
    int blockSize_;
    bool isProcessing_ = false;
    bool editorOpen_ = false;
    bool supportsDouble_ = false;

    // Audio buffers
    AudioBufferPool bufferPool_;
    MidiQueue midiQueue_;

    // Parameter mapping
    std::vector<ParameterMapping> parameters_;

    // Plugin info (cached)
    std::string pluginName_;
    std::string vendorName_;
    std::string productName_;
    int version_ = 0;
    int numInputs_ = 0;
    int numOutputs_ = 0;
    bool isSynth_ = false;
    unsigned int uniqueId_ = 0;

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

    // VST3 process data (pre-allocated)
    struct ProcessDataImpl;
    std::unique_ptr<ProcessDataImpl> processData_;
};

} // namespace MusicEngine::VstBridge

#endif // VST3_HOST_H
