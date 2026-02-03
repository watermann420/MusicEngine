/**
 * @file vst2_host.cpp
 * @brief VST2 Plugin Host Implementation
 *
 * Note: This implementation uses the VST2 AEffect structure directly.
 * For full VST2 SDK support, the official Steinberg VST2 SDK headers
 * would need to be included (aeffect.h, aeffectx.h).
 */

#include "../include/vst2_host.h"

#include <cstring>
#include <cstdio>

#ifdef _WIN32
#include <windows.h>
#endif

// VST2 AEffect structure and opcodes
// These are defined here to avoid requiring the VST2 SDK headers

#pragma pack(push, 8)

// VST2 magic number
constexpr int32_t kVstMagic = 0x56737450; // 'VstP'

// AEffect flags
constexpr int32_t effFlagsHasEditor     = 1 << 0;
constexpr int32_t effFlagsCanReplacing  = 1 << 4;
constexpr int32_t effFlagsProgramChunks = 1 << 5;
constexpr int32_t effFlagsIsSynth       = 1 << 8;
constexpr int32_t effFlagsCanDoubleReplacing = 1 << 12;

// AEffect dispatcher opcodes
enum VstOpcodes {
    effOpen = 0,
    effClose = 1,
    effSetProgram = 2,
    effGetProgram = 3,
    effSetProgramName = 4,
    effGetProgramName = 5,
    effGetParamLabel = 6,
    effGetParamDisplay = 7,
    effGetParamName = 8,
    effSetSampleRate = 10,
    effSetBlockSize = 11,
    effMainsChanged = 12,
    effEditGetRect = 13,
    effEditOpen = 14,
    effEditClose = 15,
    effEditIdle = 19,
    effGetChunk = 23,
    effSetChunk = 24,
    effProcessEvents = 25,
    effCanBeAutomated = 26,
    effGetProgramNameIndexed = 29,
    effGetEffectName = 45,
    effGetVendorString = 47,
    effGetProductString = 48,
    effGetVendorVersion = 49,
    effCanDo = 51,
    effGetTailSize = 52,
    effGetParameterProperties = 56,
    effGetVstVersion = 58,
    effStartProcess = 71,
    effStopProcess = 72,
};

// Host callback opcodes
enum VstHostOpcodes {
    audioMasterAutomate = 0,
    audioMasterVersion = 1,
    audioMasterCurrentId = 2,
    audioMasterIdle = 3,
    audioMasterGetTime = 7,
    audioMasterProcessEvents = 8,
    audioMasterIOChanged = 13,
    audioMasterSizeWindow = 15,
    audioMasterGetSampleRate = 16,
    audioMasterGetBlockSize = 17,
    audioMasterGetInputLatency = 18,
    audioMasterGetOutputLatency = 19,
    audioMasterGetCurrentProcessLevel = 23,
    audioMasterGetAutomationState = 24,
    audioMasterGetVendorString = 32,
    audioMasterGetProductString = 33,
    audioMasterGetVendorVersion = 34,
    audioMasterCanDo = 37,
    audioMasterGetLanguage = 38,
    audioMasterUpdateDisplay = 42,
    audioMasterBeginEdit = 43,
    audioMasterEndEdit = 44,
};

// MIDI event types
constexpr int32_t kVstMidiType = 1;
constexpr int32_t kVstSysExType = 6;

// VstTimeInfo structure (simplified)
struct VstTimeInfo {
    double samplePos;
    double sampleRate;
    double nanoSeconds;
    double ppqPos;
    double tempo;
    double barStartPos;
    double cycleStartPos;
    double cycleEndPos;
    int32_t timeSigNumerator;
    int32_t timeSigDenominator;
    int32_t smpteOffset;
    int32_t smpteFrameRate;
    int32_t samplesToNextClock;
    int32_t flags;
};

// VstTimeInfo flags
constexpr int32_t kVstTransportPlaying = 1 << 1;
constexpr int32_t kVstTransportRecording = 1 << 3;
constexpr int32_t kVstTransportCycleActive = 1 << 2;
constexpr int32_t kVstTempoValid = 1 << 10;
constexpr int32_t kVstTimeSigValid = 1 << 11;

// VstMidiEvent
struct VstMidiEvent {
    int32_t type;
    int32_t byteSize;
    int32_t deltaFrames;
    int32_t flags;
    int32_t noteLength;
    int32_t noteOffset;
    char midiData[4];
    char detune;
    char noteOffVelocity;
    char reserved1;
    char reserved2;
};

// VstEvents header
struct VstEvents {
    int32_t numEvents;
    intptr_t reserved;
    VstMidiEvent* events[1]; // Variable size array
};

// AEffect structure
struct AEffect {
    int32_t magic;
    intptr_t (VSTCALLBACK *dispatcher)(AEffect*, int32_t, int32_t, intptr_t, void*, float);
    void (VSTCALLBACK *process)(AEffect*, float**, float**, int32_t);
    void (VSTCALLBACK *setParameter)(AEffect*, int32_t, float);
    float (VSTCALLBACK *getParameter)(AEffect*, int32_t);
    int32_t numPrograms;
    int32_t numParams;
    int32_t numInputs;
    int32_t numOutputs;
    int32_t flags;
    intptr_t resvd1;
    intptr_t resvd2;
    int32_t initialDelay;
    int32_t realQualities;
    int32_t offQualities;
    float ioRatio;
    void* object;
    void* user;
    int32_t uniqueID;
    int32_t version;
    void (VSTCALLBACK *processReplacing)(AEffect*, float**, float**, int32_t);
    void (VSTCALLBACK *processDoubleReplacing)(AEffect*, double**, double**, int32_t);
    char future[56];
};

#pragma pack(pop)

// VST plugin main entry point signature
typedef AEffect* (VSTCALLBACK *VstPluginMain)(intptr_t (VSTCALLBACK *)(AEffect*, int32_t, int32_t, intptr_t, void*, float));

namespace MusicEngine::VstBridge {

// Static time info for all instances
static VstTimeInfo s_timeInfo = {};

Vst2Host::Vst2Host(int sampleRate, int blockSize)
    : sampleRate_(sampleRate)
    , blockSize_(blockSize)
    , bufferPool_(8, blockSize, 4)
    , midiQueue_(MaxMidiEvents) {

    // Allocate VstEvents structure for MIDI
    size_t eventsSize = sizeof(VstEvents) + sizeof(VstMidiEvent*) * MaxMidiEvents;
    vstEvents_ = reinterpret_cast<VstEvents*>(new char[eventsSize]);
    std::memset(vstEvents_, 0, eventsSize);
}

Vst2Host::~Vst2Host() {
    UnloadPlugin();
    delete[] reinterpret_cast<char*>(vstEvents_);
}

Vst2Host::Vst2Host(Vst2Host&& other) noexcept
    : effect_(other.effect_)
    , module_(other.module_)
    , sampleRate_(other.sampleRate_)
    , blockSize_(other.blockSize_)
    , isProcessing_(other.isProcessing_)
    , editorOpen_(other.editorOpen_)
    , bufferPool_(std::move(other.bufferPool_))
    , midiQueue_(other.midiQueue_.Capacity())
    , vstEvents_(other.vstEvents_) {

    other.effect_ = nullptr;
    other.module_ = nullptr;
    other.vstEvents_ = nullptr;
    std::memcpy(lastError_, other.lastError_, sizeof(lastError_));
}

Vst2Host& Vst2Host::operator=(Vst2Host&& other) noexcept {
    if (this != &other) {
        UnloadPlugin();
        delete[] reinterpret_cast<char*>(vstEvents_);

        effect_ = other.effect_;
        module_ = other.module_;
        sampleRate_ = other.sampleRate_;
        blockSize_ = other.blockSize_;
        isProcessing_ = other.isProcessing_;
        editorOpen_ = other.editorOpen_;
        vstEvents_ = other.vstEvents_;

        other.effect_ = nullptr;
        other.module_ = nullptr;
        other.vstEvents_ = nullptr;
        std::memcpy(lastError_, other.lastError_, sizeof(lastError_));
    }
    return *this;
}

bool Vst2Host::LoadPlugin(const char* path) {
    if (effect_) {
        UnloadPlugin();
    }

#ifdef _WIN32
    // Load the DLL
    module_ = LoadLibraryA(path);
    if (!module_) {
        snprintf(lastError_, sizeof(lastError_), "Failed to load DLL: %s (error %lu)",
            path, GetLastError());
        return false;
    }

    // Find the VST entry point
    VstPluginMain pluginMain = nullptr;

    // Try different entry point names
    pluginMain = reinterpret_cast<VstPluginMain>(GetProcAddress(module_, "VSTPluginMain"));
    if (!pluginMain) {
        pluginMain = reinterpret_cast<VstPluginMain>(GetProcAddress(module_, "main"));
    }
    if (!pluginMain) {
        pluginMain = reinterpret_cast<VstPluginMain>(GetProcAddress(module_, "MAIN"));
    }

    if (!pluginMain) {
        snprintf(lastError_, sizeof(lastError_), "No VST entry point found in: %s", path);
        FreeLibrary(module_);
        module_ = nullptr;
        return false;
    }

    // Create the plugin effect
    effect_ = pluginMain(&Vst2Host::HostCallback);
    if (!effect_) {
        snprintf(lastError_, sizeof(lastError_), "VST plugin main returned null: %s", path);
        FreeLibrary(module_);
        module_ = nullptr;
        return false;
    }

    // Verify magic number
    if (effect_->magic != kVstMagic) {
        snprintf(lastError_, sizeof(lastError_), "Invalid VST magic number: %s", path);
        effect_ = nullptr;
        FreeLibrary(module_);
        module_ = nullptr;
        return false;
    }

    // Store reference to this host in the effect
    effect_->user = this;

    // Initialize the plugin
    Dispatch(effOpen);
    SetSampleRate(sampleRate_);
    SetBlockSize(blockSize_);

    // Resize buffers for this plugin's channel count
    bufferPool_.Resize(
        std::max(effect_->numInputs, effect_->numOutputs),
        blockSize_
    );

    return true;
#else
    snprintf(lastError_, sizeof(lastError_), "VST2 loading not implemented on this platform");
    return false;
#endif
}

void Vst2Host::UnloadPlugin() {
    if (!effect_) return;

    if (editorOpen_) {
        CloseEditor();
    }

    if (isProcessing_) {
        StopProcessing();
    }

    Suspend();
    Dispatch(effClose);

    effect_ = nullptr;

#ifdef _WIN32
    if (module_) {
        FreeLibrary(module_);
        module_ = nullptr;
    }
#endif
}

intptr_t VSTCALLBACK Vst2Host::HostCallback(AEffect* effect, int opcode,
    int index, intptr_t value, void* ptr, float opt) {

    Vst2Host* host = effect ? static_cast<Vst2Host*>(effect->user) : nullptr;

    if (host) {
        return host->HandleHostCallback(opcode, index, value, ptr, opt);
    }

    // Default handling for callbacks during initialization
    switch (opcode) {
        case audioMasterVersion:
            return 2400; // VST 2.4
        case audioMasterGetVendorString:
            if (ptr) strcpy(static_cast<char*>(ptr), "MusicEngine");
            return 1;
        case audioMasterGetProductString:
            if (ptr) strcpy(static_cast<char*>(ptr), "MusicEngine VstBridge");
            return 1;
        case audioMasterGetVendorVersion:
            return 1000;
        default:
            return 0;
    }
}

intptr_t Vst2Host::HandleHostCallback(int opcode, int index, intptr_t value,
    void* ptr, float opt) {

    switch (opcode) {
        case audioMasterVersion:
            return 2400;

        case audioMasterGetTime:
            s_timeInfo.samplePos = samplePosition_;
            s_timeInfo.sampleRate = sampleRate_;
            s_timeInfo.tempo = tempo_;
            s_timeInfo.timeSigNumerator = timeSigNumerator_;
            s_timeInfo.timeSigDenominator = timeSigDenominator_;
            s_timeInfo.flags = kVstTempoValid | kVstTimeSigValid;
            if (isPlaying_) s_timeInfo.flags |= kVstTransportPlaying;
            if (isRecording_) s_timeInfo.flags |= kVstTransportRecording;
            if (isLooping_) s_timeInfo.flags |= kVstTransportCycleActive;
            return reinterpret_cast<intptr_t>(&s_timeInfo);

        case audioMasterGetSampleRate:
            return static_cast<intptr_t>(sampleRate_);

        case audioMasterGetBlockSize:
            return static_cast<intptr_t>(blockSize_);

        case audioMasterGetVendorString:
            if (ptr) strcpy(static_cast<char*>(ptr), "MusicEngine");
            return 1;

        case audioMasterGetProductString:
            if (ptr) strcpy(static_cast<char*>(ptr), "MusicEngine VstBridge");
            return 1;

        case audioMasterGetVendorVersion:
            return 1000;

        case audioMasterAutomate:
            // Parameter automation from plugin
            // Could trigger callback here
            return 1;

        case audioMasterIdle:
            // Idle request
            return 1;

        case audioMasterIOChanged:
            // Plugin I/O configuration changed
            return 1;

        case audioMasterSizeWindow:
            // Editor size change request
            return 1;

        case audioMasterGetCurrentProcessLevel:
            return isProcessing_ ? 2 : 1; // 2 = realtime, 1 = user interface

        case audioMasterCanDo:
            if (ptr) {
                const char* canDo = static_cast<const char*>(ptr);
                if (strcmp(canDo, "sendVstEvents") == 0) return 1;
                if (strcmp(canDo, "sendVstMidiEvent") == 0) return 1;
                if (strcmp(canDo, "sendVstTimeInfo") == 0) return 1;
                if (strcmp(canDo, "receiveVstEvents") == 0) return 1;
                if (strcmp(canDo, "receiveVstMidiEvent") == 0) return 1;
                if (strcmp(canDo, "sizeWindow") == 0) return 1;
            }
            return 0;

        default:
            return 0;
    }
}

intptr_t Vst2Host::Dispatch(int opcode, int index, intptr_t value,
    void* ptr, float opt) const {
    if (!effect_ || !effect_->dispatcher) return 0;
    return effect_->dispatcher(effect_, opcode, index, value, ptr, opt);
}

void Vst2Host::Process(float** inputs, float** outputs, int numSamples) {
    if (!effect_ || !effect_->process) return;

    ProcessMidiEvents();
    effect_->process(effect_, inputs, outputs, numSamples);
    samplePosition_ += numSamples;
}

void Vst2Host::ProcessReplacing(float** inputs, float** outputs, int numSamples) {
    if (!effect_) return;

    ProcessMidiEvents();

    if (effect_->processReplacing) {
        effect_->processReplacing(effect_, inputs, outputs, numSamples);
    } else if (effect_->process) {
        effect_->process(effect_, inputs, outputs, numSamples);
    }

    samplePosition_ += numSamples;
}

void Vst2Host::ProcessMidiEvents() {
    if (!effect_) return;

    // Collect MIDI events from queue
    static VstMidiEvent midiEventStorage[MaxMidiEvents];
    int eventCount = 0;

    MidiEvent event;
    while (eventCount < MaxMidiEvents && midiQueue_.Pop(event)) {
        VstMidiEvent& vstEvent = midiEventStorage[eventCount];
        std::memset(&vstEvent, 0, sizeof(VstMidiEvent));
        vstEvent.type = kVstMidiType;
        vstEvent.byteSize = sizeof(VstMidiEvent);
        vstEvent.deltaFrames = event.deltaFrames;
        vstEvent.midiData[0] = static_cast<char>(event.status);
        vstEvent.midiData[1] = static_cast<char>(event.data1);
        vstEvent.midiData[2] = static_cast<char>(event.data2);

        vstEvents_->events[eventCount] = &midiEventStorage[eventCount];
        eventCount++;
    }

    if (eventCount > 0) {
        vstEvents_->numEvents = eventCount;
        Dispatch(effProcessEvents, 0, 0, vstEvents_);
    }
}

void Vst2Host::StartProcessing() {
    if (!effect_) return;
    Dispatch(effStartProcess);
    isProcessing_ = true;
}

void Vst2Host::StopProcessing() {
    if (!effect_) return;
    Dispatch(effStopProcess);
    isProcessing_ = false;
}

void Vst2Host::Suspend() {
    if (!effect_) return;
    Dispatch(effMainsChanged, 0, 0);
}

void Vst2Host::Resume() {
    if (!effect_) return;
    Dispatch(effMainsChanged, 0, 1);
}

void Vst2Host::SetSampleRate(int sampleRate) {
    sampleRate_ = sampleRate;
    if (effect_) {
        Dispatch(effSetSampleRate, 0, 0, nullptr, static_cast<float>(sampleRate));
    }
}

void Vst2Host::SetBlockSize(int blockSize) {
    blockSize_ = blockSize;
    if (effect_) {
        Dispatch(effSetBlockSize, 0, blockSize);
    }
    bufferPool_.Resize(bufferPool_.MaxChannels(), blockSize);
}

int Vst2Host::GetParameterCount() const {
    return effect_ ? effect_->numParams : 0;
}

float Vst2Host::GetParameter(int index) const {
    if (!effect_ || !effect_->getParameter) return 0.0f;
    if (index < 0 || index >= effect_->numParams) return 0.0f;
    return effect_->getParameter(effect_, index);
}

void Vst2Host::SetParameter(int index, float value) {
    if (!effect_ || !effect_->setParameter) return;
    if (index < 0 || index >= effect_->numParams) return;
    effect_->setParameter(effect_, index, value);
}

void Vst2Host::GetParameterName(int index, char* buffer, int maxLen) const {
    if (!buffer || maxLen <= 0) return;
    buffer[0] = '\0';
    if (!effect_ || index < 0 || index >= effect_->numParams) return;
    Dispatch(effGetParamName, index, 0, buffer);
    buffer[maxLen - 1] = '\0';
}

void Vst2Host::GetParameterDisplay(int index, char* buffer, int maxLen) const {
    if (!buffer || maxLen <= 0) return;
    buffer[0] = '\0';
    if (!effect_ || index < 0 || index >= effect_->numParams) return;
    Dispatch(effGetParamDisplay, index, 0, buffer);
    buffer[maxLen - 1] = '\0';
}

void Vst2Host::GetParameterLabel(int index, char* buffer, int maxLen) const {
    if (!buffer || maxLen <= 0) return;
    buffer[0] = '\0';
    if (!effect_ || index < 0 || index >= effect_->numParams) return;
    Dispatch(effGetParamLabel, index, 0, buffer);
    buffer[maxLen - 1] = '\0';
}

void Vst2Host::SendMidi(int deltaFrames, int status, int data1, int data2) {
    midiQueue_.Push(deltaFrames, static_cast<uint8_t>(status),
        static_cast<uint8_t>(data1), static_cast<uint8_t>(data2));
}

void Vst2Host::SendMidiSysEx(const unsigned char* data, int length) {
    midiQueue_.PushSysEx(0, data, length);
}

void Vst2Host::ClearMidi() {
    midiQueue_.Clear();
}

void Vst2Host::AllNotesOff() {
    midiQueue_.AllNotesOff();
}

void Vst2Host::GetPluginName(char* buffer, int maxLen) const {
    if (!buffer || maxLen <= 0) return;
    buffer[0] = '\0';
    if (!effect_) return;
    Dispatch(effGetEffectName, 0, 0, buffer);
    buffer[maxLen - 1] = '\0';
}

void Vst2Host::GetVendorName(char* buffer, int maxLen) const {
    if (!buffer || maxLen <= 0) return;
    buffer[0] = '\0';
    if (!effect_) return;
    Dispatch(effGetVendorString, 0, 0, buffer);
    buffer[maxLen - 1] = '\0';
}

void Vst2Host::GetProductName(char* buffer, int maxLen) const {
    if (!buffer || maxLen <= 0) return;
    buffer[0] = '\0';
    if (!effect_) return;
    Dispatch(effGetProductString, 0, 0, buffer);
    buffer[maxLen - 1] = '\0';
}

int Vst2Host::GetVersion() const {
    return effect_ ? effect_->version : 0;
}

int Vst2Host::GetNumInputs() const {
    return effect_ ? effect_->numInputs : 0;
}

int Vst2Host::GetNumOutputs() const {
    return effect_ ? effect_->numOutputs : 0;
}

bool Vst2Host::IsSynth() const {
    return effect_ ? (effect_->flags & effFlagsIsSynth) != 0 : false;
}

unsigned int Vst2Host::GetUniqueId() const {
    return effect_ ? static_cast<unsigned int>(effect_->uniqueID) : 0;
}

int Vst2Host::GetLatency() const {
    return effect_ ? effect_->initialDelay : 0;
}

int Vst2Host::GetTailSize() const {
    if (!effect_) return 0;
    return static_cast<int>(Dispatch(effGetTailSize));
}

int Vst2Host::GetProgramCount() const {
    return effect_ ? effect_->numPrograms : 0;
}

int Vst2Host::GetProgram() const {
    if (!effect_) return 0;
    return static_cast<int>(Dispatch(effGetProgram));
}

void Vst2Host::SetProgram(int index) {
    if (!effect_ || index < 0 || index >= effect_->numPrograms) return;
    Dispatch(effSetProgram, 0, index);
}

void Vst2Host::GetProgramName(int index, char* buffer, int maxLen) const {
    if (!buffer || maxLen <= 0) return;
    buffer[0] = '\0';
    if (!effect_ || index < 0 || index >= effect_->numPrograms) return;
    Dispatch(effGetProgramNameIndexed, index, 0, buffer);
    buffer[maxLen - 1] = '\0';
}

int Vst2Host::GetState(unsigned char* data, int maxLen) const {
    if (!effect_) return -1;
    if (!(effect_->flags & effFlagsProgramChunks)) return -1;

    void* chunk = nullptr;
    intptr_t size = Dispatch(effGetChunk, 0, 0, &chunk);

    if (size <= 0 || !chunk) return -1;
    if (!data) return static_cast<int>(size); // Query size only

    int copySize = std::min(static_cast<int>(size), maxLen);
    std::memcpy(data, chunk, copySize);
    return copySize;
}

bool Vst2Host::SetState(const unsigned char* data, int length) {
    if (!effect_ || !data || length <= 0) return false;
    if (!(effect_->flags & effFlagsProgramChunks)) return false;

    return Dispatch(effSetChunk, 0, length, const_cast<unsigned char*>(data)) != 0;
}

bool Vst2Host::HasEditor() const {
    return effect_ ? (effect_->flags & effFlagsHasEditor) != 0 : false;
}

void Vst2Host::OpenEditor(void* parentWindow) {
    if (!effect_ || !HasEditor() || editorOpen_) return;
    Dispatch(effEditOpen, 0, 0, parentWindow);
    editorOpen_ = true;
}

void Vst2Host::CloseEditor() {
    if (!effect_ || !editorOpen_) return;
    Dispatch(effEditClose);
    editorOpen_ = false;
}

void Vst2Host::GetEditorSize(int* width, int* height) const {
    if (!width || !height) return;
    *width = 0;
    *height = 0;

    if (!effect_ || !HasEditor()) return;

    struct ERect { int16_t top, left, bottom, right; };
    ERect* rect = nullptr;
    if (Dispatch(effEditGetRect, 0, 0, &rect) && rect) {
        *width = rect->right - rect->left;
        *height = rect->bottom - rect->top;
    }
}

void Vst2Host::EditorIdle() {
    if (!effect_ || !editorOpen_) return;
    Dispatch(effEditIdle);
}

void Vst2Host::SetTransport(double samplePos, double tempo, int timeSigNum, int timeSigDen) {
    samplePosition_ = samplePos;
    tempo_ = tempo;
    timeSigNumerator_ = timeSigNum;
    timeSigDenominator_ = timeSigDen;
}

void Vst2Host::SetTransportState(bool playing, bool recording, bool looping) {
    isPlaying_ = playing;
    isRecording_ = recording;
    isLooping_ = looping;
}

} // namespace MusicEngine::VstBridge
