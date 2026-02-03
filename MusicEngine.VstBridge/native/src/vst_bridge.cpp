/**
 * @file vst_bridge.cpp
 * @brief C API Implementation for VST Bridge
 */

#include "../include/vst_bridge.h"
#include "../include/vst2_host.h"
#include "../include/vst3_host.h"

#include <cstring>
#include <memory>
#include <unordered_map>
#include <mutex>
#include <string>
#include <algorithm>

using namespace MusicEngine::VstBridge;

// Version info
static constexpr int VSTBRIDGE_VERSION_MAJOR = 1;
static constexpr int VSTBRIDGE_VERSION_MINOR = 0;
static constexpr int VSTBRIDGE_VERSION_PATCH = 0;

/**
 * @brief Bridge instance managing all loaded plugins
 */
struct VstBridgeInstance {
    int sampleRate;
    int blockSize;
    VstBridgeLogCallback logCallback = nullptr;
    std::unordered_map<VstPluginHandle, std::unique_ptr<Vst2Host>> vst2Plugins;
    std::unordered_map<VstPluginHandle, std::unique_ptr<Vst3Host>> vst3Plugins;
    std::mutex pluginMutex;
    char lastError[256] = {0};
    int nextPluginId = 1;

    void Log(int level, const char* message) {
        if (logCallback) {
            logCallback(level, message);
        }
    }

    void SetError(const char* error) {
        strncpy(lastError, error, sizeof(lastError) - 1);
        lastError[sizeof(lastError) - 1] = '\0';
    }
};

/**
 * @brief Plugin wrapper with type info
 */
struct VstPluginInstance {
    VstBridgeInstance* bridge;
    int pluginType; // 2 = VST2, 3 = VST3
    union {
        Vst2Host* vst2;
        Vst3Host* vst3;
    } host;

    Vst2Host* AsVst2() const { return pluginType == VST_PLUGIN_TYPE_VST2 ? host.vst2 : nullptr; }
    Vst3Host* AsVst3() const { return pluginType == VST_PLUGIN_TYPE_VST3 ? host.vst3 : nullptr; }
};

// Helper to check file extension
static bool EndsWith(const std::string& str, const std::string& suffix) {
    if (suffix.size() > str.size()) return false;
    return std::equal(suffix.rbegin(), suffix.rend(), str.rbegin(),
        [](char a, char b) { return tolower(a) == tolower(b); });
}

// =============================================================================
// Initialization
// =============================================================================

VSTBRIDGE_API VstBridgeHandle vstbridge_create(int sampleRate, int blockSize) {
    auto* bridge = new VstBridgeInstance();
    bridge->sampleRate = sampleRate;
    bridge->blockSize = blockSize;
    return bridge;
}

VSTBRIDGE_API void vstbridge_destroy(VstBridgeHandle handle) {
    if (!handle) return;

    auto* bridge = static_cast<VstBridgeInstance*>(handle);

    // Unload all plugins
    {
        std::lock_guard<std::mutex> lock(bridge->pluginMutex);
        bridge->vst2Plugins.clear();
        bridge->vst3Plugins.clear();
    }

    delete bridge;
}

VSTBRIDGE_API void vstbridge_set_sample_rate(VstBridgeHandle handle, int sampleRate) {
    if (!handle) return;

    auto* bridge = static_cast<VstBridgeInstance*>(handle);
    bridge->sampleRate = sampleRate;

    std::lock_guard<std::mutex> lock(bridge->pluginMutex);
    for (auto& [_, plugin] : bridge->vst2Plugins) {
        plugin->SetSampleRate(sampleRate);
    }
    for (auto& [_, plugin] : bridge->vst3Plugins) {
        plugin->SetSampleRate(sampleRate);
    }
}

VSTBRIDGE_API void vstbridge_set_block_size(VstBridgeHandle handle, int blockSize) {
    if (!handle) return;

    auto* bridge = static_cast<VstBridgeInstance*>(handle);
    bridge->blockSize = blockSize;

    std::lock_guard<std::mutex> lock(bridge->pluginMutex);
    for (auto& [_, plugin] : bridge->vst2Plugins) {
        plugin->SetBlockSize(blockSize);
    }
    for (auto& [_, plugin] : bridge->vst3Plugins) {
        plugin->SetBlockSize(blockSize);
    }
}

VSTBRIDGE_API void vstbridge_set_log_callback(VstBridgeHandle handle, VstBridgeLogCallback callback) {
    if (!handle) return;
    auto* bridge = static_cast<VstBridgeInstance*>(handle);
    bridge->logCallback = callback;
}

VSTBRIDGE_API const char* vstbridge_get_last_error(VstBridgeHandle handle) {
    if (!handle) return "Invalid handle";
    auto* bridge = static_cast<VstBridgeInstance*>(handle);
    return bridge->lastError;
}

// =============================================================================
// Plugin Loading
// =============================================================================

VSTBRIDGE_API VstPluginHandle vstbridge_load_plugin(VstBridgeHandle host, const char* path) {
    if (!host || !path) return nullptr;

    auto* bridge = static_cast<VstBridgeInstance*>(host);
    std::string pathStr(path);

    std::lock_guard<std::mutex> lock(bridge->pluginMutex);

    // Determine plugin type from extension
    bool isVst3 = EndsWith(pathStr, ".vst3");

    auto* instance = new VstPluginInstance();
    instance->bridge = bridge;

    if (isVst3) {
        // Load VST3
        auto plugin = std::make_unique<Vst3Host>(bridge->sampleRate, bridge->blockSize);
        if (!plugin->LoadPlugin(path)) {
            bridge->SetError(plugin->GetLastError());
            delete instance;
            return nullptr;
        }

        instance->pluginType = VST_PLUGIN_TYPE_VST3;
        instance->host.vst3 = plugin.get();
        bridge->vst3Plugins[instance] = std::move(plugin);
    } else {
        // Load VST2 (assume .dll is VST2)
        auto plugin = std::make_unique<Vst2Host>(bridge->sampleRate, bridge->blockSize);
        if (!plugin->LoadPlugin(path)) {
            bridge->SetError(plugin->GetLastError());
            delete instance;
            return nullptr;
        }

        instance->pluginType = VST_PLUGIN_TYPE_VST2;
        instance->host.vst2 = plugin.get();
        bridge->vst2Plugins[instance] = std::move(plugin);
    }

    bridge->Log(0, ("Loaded plugin: " + pathStr).c_str());
    return instance;
}

VSTBRIDGE_API void vstbridge_unload_plugin(VstPluginHandle plugin) {
    if (!plugin) return;

    auto* instance = static_cast<VstPluginInstance*>(plugin);
    auto* bridge = instance->bridge;

    std::lock_guard<std::mutex> lock(bridge->pluginMutex);

    if (instance->pluginType == VST_PLUGIN_TYPE_VST2) {
        bridge->vst2Plugins.erase(instance);
    } else if (instance->pluginType == VST_PLUGIN_TYPE_VST3) {
        bridge->vst3Plugins.erase(instance);
    }

    delete instance;
}

VSTBRIDGE_API int vstbridge_get_plugin_type(VstPluginHandle plugin) {
    if (!plugin) return VST_PLUGIN_TYPE_UNKNOWN;
    return static_cast<VstPluginInstance*>(plugin)->pluginType;
}

VSTBRIDGE_API int vstbridge_is_plugin_valid(VstPluginHandle plugin) {
    if (!plugin) return 0;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) return vst2->IsLoaded() ? 1 : 0;
    if (auto* vst3 = instance->AsVst3()) return vst3->IsLoaded() ? 1 : 0;
    return 0;
}

// =============================================================================
// Audio Processing
// =============================================================================

VSTBRIDGE_API void vstbridge_process(VstPluginHandle plugin,
    float** inputs, float** outputs, int numSamples) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->Process(inputs, outputs, numSamples);
    else if (auto* vst3 = instance->AsVst3()) vst3->Process(inputs, outputs, numSamples);
}

VSTBRIDGE_API void vstbridge_process_replacing(VstPluginHandle plugin,
    float** inputs, float** outputs, int numSamples) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->ProcessReplacing(inputs, outputs, numSamples);
    else if (auto* vst3 = instance->AsVst3()) vst3->ProcessReplacing(inputs, outputs, numSamples);
}

VSTBRIDGE_API int vstbridge_process_double(VstPluginHandle plugin,
    double** inputs, double** outputs, int numSamples) {
    if (!plugin) return VSTBRIDGE_ERROR_INVALID_HANDLE;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst3 = instance->AsVst3()) {
        return vst3->ProcessDouble(inputs, outputs, numSamples) ?
            VSTBRIDGE_OK : VSTBRIDGE_ERROR_NOT_SUPPORTED;
    }
    return VSTBRIDGE_ERROR_NOT_SUPPORTED; // VST2 doesn't support double
}

VSTBRIDGE_API void vstbridge_start_processing(VstPluginHandle plugin) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->StartProcessing();
    else if (auto* vst3 = instance->AsVst3()) vst3->StartProcessing();
}

VSTBRIDGE_API void vstbridge_stop_processing(VstPluginHandle plugin) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->StopProcessing();
    else if (auto* vst3 = instance->AsVst3()) vst3->StopProcessing();
}

VSTBRIDGE_API void vstbridge_suspend(VstPluginHandle plugin) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->Suspend();
    else if (auto* vst3 = instance->AsVst3()) vst3->Suspend();
}

VSTBRIDGE_API void vstbridge_resume(VstPluginHandle plugin) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->Resume();
    else if (auto* vst3 = instance->AsVst3()) vst3->Resume();
}

// =============================================================================
// Parameters
// =============================================================================

VSTBRIDGE_API int vstbridge_get_param_count(VstPluginHandle plugin) {
    if (!plugin) return -1;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) return vst2->GetParameterCount();
    if (auto* vst3 = instance->AsVst3()) return vst3->GetParameterCount();
    return -1;
}

VSTBRIDGE_API float vstbridge_get_param(VstPluginHandle plugin, int index) {
    if (!plugin) return -1.0f;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) return vst2->GetParameter(index);
    if (auto* vst3 = instance->AsVst3()) return vst3->GetParameter(index);
    return -1.0f;
}

VSTBRIDGE_API void vstbridge_set_param(VstPluginHandle plugin, int index, float value) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->SetParameter(index, value);
    else if (auto* vst3 = instance->AsVst3()) vst3->SetParameter(index, value);
}

VSTBRIDGE_API void vstbridge_get_param_name(VstPluginHandle plugin, int index,
    char* buffer, int maxLen) {
    if (!plugin || !buffer || maxLen <= 0) return;
    buffer[0] = '\0';
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->GetParameterName(index, buffer, maxLen);
    else if (auto* vst3 = instance->AsVst3()) vst3->GetParameterName(index, buffer, maxLen);
}

VSTBRIDGE_API void vstbridge_get_param_display(VstPluginHandle plugin, int index,
    char* buffer, int maxLen) {
    if (!plugin || !buffer || maxLen <= 0) return;
    buffer[0] = '\0';
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->GetParameterDisplay(index, buffer, maxLen);
    else if (auto* vst3 = instance->AsVst3()) vst3->GetParameterDisplay(index, buffer, maxLen);
}

VSTBRIDGE_API void vstbridge_get_param_label(VstPluginHandle plugin, int index,
    char* buffer, int maxLen) {
    if (!plugin || !buffer || maxLen <= 0) return;
    buffer[0] = '\0';
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->GetParameterLabel(index, buffer, maxLen);
    else if (auto* vst3 = instance->AsVst3()) vst3->GetParameterLabel(index, buffer, maxLen);
}

VSTBRIDGE_API void vstbridge_set_param_callback(VstPluginHandle plugin,
    VstBridgeParameterCallback callback) {
    // TODO: Implement parameter change callbacks
    (void)plugin;
    (void)callback;
}

// =============================================================================
// MIDI
// =============================================================================

VSTBRIDGE_API void vstbridge_send_midi(VstPluginHandle plugin,
    int status, int data1, int data2) {
    vstbridge_send_midi_at(plugin, 0, status, data1, data2);
}

VSTBRIDGE_API void vstbridge_send_midi_at(VstPluginHandle plugin,
    int deltaFrames, int status, int data1, int data2) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->SendMidi(deltaFrames, status, data1, data2);
    else if (auto* vst3 = instance->AsVst3()) vst3->SendMidi(deltaFrames, status, data1, data2);
}

VSTBRIDGE_API void vstbridge_send_midi_sysex(VstPluginHandle plugin,
    const unsigned char* data, int length) {
    if (!plugin || !data || length <= 0) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->SendMidiSysEx(data, length);
    else if (auto* vst3 = instance->AsVst3()) vst3->SendMidiSysEx(data, length);
}

VSTBRIDGE_API void vstbridge_clear_midi(VstPluginHandle plugin) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->ClearMidi();
    else if (auto* vst3 = instance->AsVst3()) vst3->ClearMidi();
}

VSTBRIDGE_API void vstbridge_all_notes_off(VstPluginHandle plugin) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->AllNotesOff();
    else if (auto* vst3 = instance->AsVst3()) vst3->AllNotesOff();
}

// =============================================================================
// Plugin Info
// =============================================================================

VSTBRIDGE_API void vstbridge_get_plugin_name(VstPluginHandle plugin,
    char* buffer, int maxLen) {
    if (!plugin || !buffer || maxLen <= 0) return;
    buffer[0] = '\0';
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->GetPluginName(buffer, maxLen);
    else if (auto* vst3 = instance->AsVst3()) vst3->GetPluginName(buffer, maxLen);
}

VSTBRIDGE_API void vstbridge_get_vendor_name(VstPluginHandle plugin,
    char* buffer, int maxLen) {
    if (!plugin || !buffer || maxLen <= 0) return;
    buffer[0] = '\0';
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->GetVendorName(buffer, maxLen);
    else if (auto* vst3 = instance->AsVst3()) vst3->GetVendorName(buffer, maxLen);
}

VSTBRIDGE_API void vstbridge_get_product_name(VstPluginHandle plugin,
    char* buffer, int maxLen) {
    if (!plugin || !buffer || maxLen <= 0) return;
    buffer[0] = '\0';
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->GetProductName(buffer, maxLen);
    else if (auto* vst3 = instance->AsVst3()) vst3->GetProductName(buffer, maxLen);
}

VSTBRIDGE_API int vstbridge_get_plugin_version(VstPluginHandle plugin) {
    if (!plugin) return 0;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) return vst2->GetVersion();
    if (auto* vst3 = instance->AsVst3()) return vst3->GetVersion();
    return 0;
}

VSTBRIDGE_API int vstbridge_get_num_inputs(VstPluginHandle plugin) {
    if (!plugin) return 0;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) return vst2->GetNumInputs();
    if (auto* vst3 = instance->AsVst3()) return vst3->GetNumInputs();
    return 0;
}

VSTBRIDGE_API int vstbridge_get_num_outputs(VstPluginHandle plugin) {
    if (!plugin) return 0;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) return vst2->GetNumOutputs();
    if (auto* vst3 = instance->AsVst3()) return vst3->GetNumOutputs();
    return 0;
}

VSTBRIDGE_API int vstbridge_is_synth(VstPluginHandle plugin) {
    if (!plugin) return 0;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) return vst2->IsSynth() ? 1 : 0;
    if (auto* vst3 = instance->AsVst3()) return vst3->IsSynth() ? 1 : 0;
    return 0;
}

VSTBRIDGE_API unsigned int vstbridge_get_unique_id(VstPluginHandle plugin) {
    if (!plugin) return 0;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) return vst2->GetUniqueId();
    if (auto* vst3 = instance->AsVst3()) return vst3->GetUniqueId();
    return 0;
}

VSTBRIDGE_API int vstbridge_get_latency(VstPluginHandle plugin) {
    if (!plugin) return 0;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) return vst2->GetLatency();
    if (auto* vst3 = instance->AsVst3()) return vst3->GetLatency();
    return 0;
}

VSTBRIDGE_API int vstbridge_get_tail_size(VstPluginHandle plugin) {
    if (!plugin) return 0;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) return vst2->GetTailSize();
    if (auto* vst3 = instance->AsVst3()) return vst3->GetTailSize();
    return 0;
}

// =============================================================================
// Presets / State
// =============================================================================

VSTBRIDGE_API int vstbridge_load_preset(VstPluginHandle plugin, const char* path) {
    if (!plugin || !path) return VSTBRIDGE_ERROR_INVALID_PARAM;
    // TODO: Implement preset loading
    return VSTBRIDGE_ERROR_NOT_SUPPORTED;
}

VSTBRIDGE_API int vstbridge_save_preset(VstPluginHandle plugin, const char* path) {
    if (!plugin || !path) return VSTBRIDGE_ERROR_INVALID_PARAM;
    // TODO: Implement preset saving
    return VSTBRIDGE_ERROR_NOT_SUPPORTED;
}

VSTBRIDGE_API int vstbridge_get_state(VstPluginHandle plugin,
    unsigned char* data, int maxLen) {
    if (!plugin) return -1;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) return vst2->GetState(data, maxLen);
    if (auto* vst3 = instance->AsVst3()) return vst3->GetState(data, maxLen);
    return -1;
}

VSTBRIDGE_API int vstbridge_set_state(VstPluginHandle plugin,
    const unsigned char* data, int length) {
    if (!plugin || !data || length <= 0) return VSTBRIDGE_ERROR_INVALID_PARAM;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2())
        return vst2->SetState(data, length) ? VSTBRIDGE_OK : VSTBRIDGE_ERROR_INIT_FAILED;
    if (auto* vst3 = instance->AsVst3())
        return vst3->SetState(data, length) ? VSTBRIDGE_OK : VSTBRIDGE_ERROR_INIT_FAILED;
    return VSTBRIDGE_ERROR_INVALID_HANDLE;
}

VSTBRIDGE_API int vstbridge_get_program_count(VstPluginHandle plugin) {
    if (!plugin) return 0;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) return vst2->GetProgramCount();
    if (auto* vst3 = instance->AsVst3()) return vst3->GetProgramCount();
    return 0;
}

VSTBRIDGE_API int vstbridge_get_program(VstPluginHandle plugin) {
    if (!plugin) return 0;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) return vst2->GetProgram();
    if (auto* vst3 = instance->AsVst3()) return vst3->GetProgram();
    return 0;
}

VSTBRIDGE_API void vstbridge_set_program(VstPluginHandle plugin, int index) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->SetProgram(index);
    else if (auto* vst3 = instance->AsVst3()) vst3->SetProgram(index);
}

VSTBRIDGE_API void vstbridge_get_program_name(VstPluginHandle plugin, int index,
    char* buffer, int maxLen) {
    if (!plugin || !buffer || maxLen <= 0) return;
    buffer[0] = '\0';
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->GetProgramName(index, buffer, maxLen);
    else if (auto* vst3 = instance->AsVst3()) vst3->GetProgramName(index, buffer, maxLen);
}

// =============================================================================
// Editor / GUI
// =============================================================================

VSTBRIDGE_API int vstbridge_has_editor(VstPluginHandle plugin) {
    if (!plugin) return 0;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) return vst2->HasEditor() ? 1 : 0;
    if (auto* vst3 = instance->AsVst3()) return vst3->HasEditor() ? 1 : 0;
    return 0;
}

VSTBRIDGE_API void vstbridge_open_editor(VstPluginHandle plugin, void* parentWindow) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->OpenEditor(parentWindow);
    else if (auto* vst3 = instance->AsVst3()) vst3->OpenEditor(parentWindow);
}

VSTBRIDGE_API void vstbridge_close_editor(VstPluginHandle plugin) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->CloseEditor();
    else if (auto* vst3 = instance->AsVst3()) vst3->CloseEditor();
}

VSTBRIDGE_API void vstbridge_get_editor_size(VstPluginHandle plugin,
    int* width, int* height) {
    if (!plugin || !width || !height) return;
    *width = 0;
    *height = 0;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->GetEditorSize(width, height);
    else if (auto* vst3 = instance->AsVst3()) vst3->GetEditorSize(width, height);
}

VSTBRIDGE_API void vstbridge_editor_idle(VstPluginHandle plugin) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->EditorIdle();
    else if (auto* vst3 = instance->AsVst3()) vst3->EditorIdle();
}

VSTBRIDGE_API int vstbridge_is_editor_open(VstPluginHandle plugin) {
    if (!plugin) return 0;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) return vst2->IsEditorOpen() ? 1 : 0;
    if (auto* vst3 = instance->AsVst3()) return vst3->IsEditorOpen() ? 1 : 0;
    return 0;
}

// =============================================================================
// Transport / Timing
// =============================================================================

VSTBRIDGE_API void vstbridge_set_transport(VstPluginHandle plugin,
    double samplePos, double tempo, int timeSigNum, int timeSigDen) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2()) vst2->SetTransport(samplePos, tempo, timeSigNum, timeSigDen);
    else if (auto* vst3 = instance->AsVst3()) vst3->SetTransport(samplePos, tempo, timeSigNum, timeSigDen);
}

VSTBRIDGE_API void vstbridge_set_transport_state(VstPluginHandle plugin,
    int playing, int recording, int looping) {
    if (!plugin) return;
    auto* instance = static_cast<VstPluginInstance*>(plugin);
    if (auto* vst2 = instance->AsVst2())
        vst2->SetTransportState(playing != 0, recording != 0, looping != 0);
    else if (auto* vst3 = instance->AsVst3())
        vst3->SetTransportState(playing != 0, recording != 0, looping != 0);
}

// =============================================================================
// Utility
// =============================================================================

VSTBRIDGE_API int vstbridge_get_version(void) {
    return (VSTBRIDGE_VERSION_MAJOR << 16) |
           (VSTBRIDGE_VERSION_MINOR << 8) |
           VSTBRIDGE_VERSION_PATCH;
}

VSTBRIDGE_API void vstbridge_get_version_string(char* buffer, int maxLen) {
    if (!buffer || maxLen <= 0) return;
    snprintf(buffer, maxLen, "%d.%d.%d",
        VSTBRIDGE_VERSION_MAJOR, VSTBRIDGE_VERSION_MINOR, VSTBRIDGE_VERSION_PATCH);
}

VSTBRIDGE_API int vstbridge_has_vst2_support(void) {
#ifdef VST2_SDK_AVAILABLE
    return 1;
#else
    return 1; // We support VST2 through AEffect structure
#endif
}

VSTBRIDGE_API int vstbridge_has_vst3_support(void) {
#ifdef VST3_SDK_AVAILABLE
    return 1;
#else
    return 0; // VST3 requires SDK
#endif
}
