/**
 * @file vst_bridge.h
 * @brief Public C API for MusicEngine VST Bridge
 *
 * This header defines the public interface for loading and interacting with
 * VST2 and VST3 plugins from managed code via P/Invoke.
 */

#ifndef VST_BRIDGE_H
#define VST_BRIDGE_H

#ifdef _WIN32
    #ifdef VSTBRIDGE_EXPORTS
        #define VSTBRIDGE_API __declspec(dllexport)
    #else
        #define VSTBRIDGE_API __declspec(dllimport)
    #endif
#else
    #define VSTBRIDGE_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* Opaque Handles */
typedef void* VstBridgeHandle;
typedef void* VstPluginHandle;

/* Plugin Types */
#define VST_PLUGIN_TYPE_UNKNOWN 0
#define VST_PLUGIN_TYPE_VST2    2
#define VST_PLUGIN_TYPE_VST3    3

/* Error Codes */
#define VSTBRIDGE_OK                    0
#define VSTBRIDGE_ERROR_INVALID_HANDLE  -1
#define VSTBRIDGE_ERROR_LOAD_FAILED     -2
#define VSTBRIDGE_ERROR_NOT_A_VST       -3
#define VSTBRIDGE_ERROR_INIT_FAILED     -4
#define VSTBRIDGE_ERROR_FILE_NOT_FOUND  -5
#define VSTBRIDGE_ERROR_INVALID_PARAM   -6
#define VSTBRIDGE_ERROR_NOT_SUPPORTED   -7

/* Callback Types */
typedef void (*VstBridgeLogCallback)(int level, const char* message);
typedef void (*VstBridgeParameterCallback)(VstPluginHandle plugin, int index, float value);

/* =============================================================================
 * Initialization
 * ============================================================================= */

/**
 * @brief Creates a new VST Bridge host instance
 * @param sampleRate The audio sample rate (e.g., 44100, 48000)
 * @param blockSize The audio block size in samples (e.g., 256, 512, 1024)
 * @return Handle to the bridge instance, or NULL on failure
 */
VSTBRIDGE_API VstBridgeHandle vstbridge_create(int sampleRate, int blockSize);

/**
 * @brief Destroys a VST Bridge host instance and unloads all plugins
 * @param handle The bridge handle to destroy
 */
VSTBRIDGE_API void vstbridge_destroy(VstBridgeHandle handle);

/**
 * @brief Sets the sample rate for all loaded plugins
 * @param handle The bridge handle
 * @param sampleRate The new sample rate
 */
VSTBRIDGE_API void vstbridge_set_sample_rate(VstBridgeHandle handle, int sampleRate);

/**
 * @brief Sets the block size for all loaded plugins
 * @param handle The bridge handle
 * @param blockSize The new block size in samples
 */
VSTBRIDGE_API void vstbridge_set_block_size(VstBridgeHandle handle, int blockSize);

/**
 * @brief Sets a logging callback for debug messages
 * @param handle The bridge handle
 * @param callback The callback function (NULL to disable)
 */
VSTBRIDGE_API void vstbridge_set_log_callback(VstBridgeHandle handle, VstBridgeLogCallback callback);

/**
 * @brief Gets the last error message
 * @param handle The bridge handle
 * @return Error message string (valid until next call)
 */
VSTBRIDGE_API const char* vstbridge_get_last_error(VstBridgeHandle handle);

/* =============================================================================
 * Plugin Loading
 * ============================================================================= */

/**
 * @brief Loads a VST plugin from a file path
 * @param host The bridge handle
 * @param path Path to the VST plugin (.dll for VST2, .vst3 for VST3)
 * @return Handle to the loaded plugin, or NULL on failure
 */
VSTBRIDGE_API VstPluginHandle vstbridge_load_plugin(VstBridgeHandle host, const char* path);

/**
 * @brief Unloads a VST plugin and frees resources
 * @param plugin The plugin handle to unload
 */
VSTBRIDGE_API void vstbridge_unload_plugin(VstPluginHandle plugin);

/**
 * @brief Gets the plugin type (VST2 or VST3)
 * @param plugin The plugin handle
 * @return VST_PLUGIN_TYPE_VST2 (2) or VST_PLUGIN_TYPE_VST3 (3), or 0 on error
 */
VSTBRIDGE_API int vstbridge_get_plugin_type(VstPluginHandle plugin);

/**
 * @brief Checks if a plugin is currently loaded and valid
 * @param plugin The plugin handle
 * @return 1 if valid, 0 if invalid
 */
VSTBRIDGE_API int vstbridge_is_plugin_valid(VstPluginHandle plugin);

/* =============================================================================
 * Audio Processing
 * ============================================================================= */

/**
 * @brief Processes audio through the plugin (non-replacing)
 * @param plugin The plugin handle
 * @param inputs Array of input channel pointers
 * @param outputs Array of output channel pointers
 * @param numSamples Number of samples to process
 */
VSTBRIDGE_API void vstbridge_process(VstPluginHandle plugin,
    float** inputs, float** outputs, int numSamples);

/**
 * @brief Processes audio through the plugin (replacing, in-place)
 * @param plugin The plugin handle
 * @param inputs Array of input channel pointers (may be modified)
 * @param outputs Array of output channel pointers
 * @param numSamples Number of samples to process
 */
VSTBRIDGE_API void vstbridge_process_replacing(VstPluginHandle plugin,
    float** inputs, float** outputs, int numSamples);

/**
 * @brief Processes audio with double precision (if supported)
 * @param plugin The plugin handle
 * @param inputs Array of input channel pointers (double)
 * @param outputs Array of output channel pointers (double)
 * @param numSamples Number of samples to process
 * @return VSTBRIDGE_OK on success, VSTBRIDGE_ERROR_NOT_SUPPORTED if not available
 */
VSTBRIDGE_API int vstbridge_process_double(VstPluginHandle plugin,
    double** inputs, double** outputs, int numSamples);

/**
 * @brief Starts processing (called before audio begins)
 * @param plugin The plugin handle
 */
VSTBRIDGE_API void vstbridge_start_processing(VstPluginHandle plugin);

/**
 * @brief Stops processing (called when audio stops)
 * @param plugin The plugin handle
 */
VSTBRIDGE_API void vstbridge_stop_processing(VstPluginHandle plugin);

/**
 * @brief Suspends the plugin
 * @param plugin The plugin handle
 */
VSTBRIDGE_API void vstbridge_suspend(VstPluginHandle plugin);

/**
 * @brief Resumes the plugin
 * @param plugin The plugin handle
 */
VSTBRIDGE_API void vstbridge_resume(VstPluginHandle plugin);

/* =============================================================================
 * Parameters
 * ============================================================================= */

/**
 * @brief Gets the number of parameters
 * @param plugin The plugin handle
 * @return Number of parameters, or -1 on error
 */
VSTBRIDGE_API int vstbridge_get_param_count(VstPluginHandle plugin);

/**
 * @brief Gets a parameter value
 * @param plugin The plugin handle
 * @param index Parameter index
 * @return Parameter value (0.0 to 1.0), or -1.0 on error
 */
VSTBRIDGE_API float vstbridge_get_param(VstPluginHandle plugin, int index);

/**
 * @brief Sets a parameter value
 * @param plugin The plugin handle
 * @param index Parameter index
 * @param value Parameter value (0.0 to 1.0)
 */
VSTBRIDGE_API void vstbridge_set_param(VstPluginHandle plugin, int index, float value);

/**
 * @brief Gets a parameter name
 * @param plugin The plugin handle
 * @param index Parameter index
 * @param buffer Buffer to receive the name
 * @param maxLen Maximum buffer length
 */
VSTBRIDGE_API void vstbridge_get_param_name(VstPluginHandle plugin, int index,
    char* buffer, int maxLen);

/**
 * @brief Gets a parameter display value (formatted string)
 * @param plugin The plugin handle
 * @param index Parameter index
 * @param buffer Buffer to receive the display value
 * @param maxLen Maximum buffer length
 */
VSTBRIDGE_API void vstbridge_get_param_display(VstPluginHandle plugin, int index,
    char* buffer, int maxLen);

/**
 * @brief Gets a parameter label (units)
 * @param plugin The plugin handle
 * @param index Parameter index
 * @param buffer Buffer to receive the label
 * @param maxLen Maximum buffer length
 */
VSTBRIDGE_API void vstbridge_get_param_label(VstPluginHandle plugin, int index,
    char* buffer, int maxLen);

/**
 * @brief Sets a callback for parameter changes from the plugin
 * @param plugin The plugin handle
 * @param callback The callback function (NULL to disable)
 */
VSTBRIDGE_API void vstbridge_set_param_callback(VstPluginHandle plugin,
    VstBridgeParameterCallback callback);

/* =============================================================================
 * MIDI
 * ============================================================================= */

/**
 * @brief Sends a MIDI message to the plugin
 * @param plugin The plugin handle
 * @param status MIDI status byte
 * @param data1 MIDI data byte 1
 * @param data2 MIDI data byte 2
 */
VSTBRIDGE_API void vstbridge_send_midi(VstPluginHandle plugin,
    int status, int data1, int data2);

/**
 * @brief Sends a MIDI message with delta time
 * @param plugin The plugin handle
 * @param deltaFrames Sample offset from block start
 * @param status MIDI status byte
 * @param data1 MIDI data byte 1
 * @param data2 MIDI data byte 2
 */
VSTBRIDGE_API void vstbridge_send_midi_at(VstPluginHandle plugin,
    int deltaFrames, int status, int data1, int data2);

/**
 * @brief Sends a SysEx message to the plugin
 * @param plugin The plugin handle
 * @param data SysEx data (including F0 and F7)
 * @param length Data length in bytes
 */
VSTBRIDGE_API void vstbridge_send_midi_sysex(VstPluginHandle plugin,
    const unsigned char* data, int length);

/**
 * @brief Clears all pending MIDI events
 * @param plugin The plugin handle
 */
VSTBRIDGE_API void vstbridge_clear_midi(VstPluginHandle plugin);

/**
 * @brief Sends all notes off to the plugin
 * @param plugin The plugin handle
 */
VSTBRIDGE_API void vstbridge_all_notes_off(VstPluginHandle plugin);

/* =============================================================================
 * Plugin Info
 * ============================================================================= */

/**
 * @brief Gets the plugin name
 * @param plugin The plugin handle
 * @param buffer Buffer to receive the name
 * @param maxLen Maximum buffer length
 */
VSTBRIDGE_API void vstbridge_get_plugin_name(VstPluginHandle plugin,
    char* buffer, int maxLen);

/**
 * @brief Gets the plugin vendor name
 * @param plugin The plugin handle
 * @param buffer Buffer to receive the name
 * @param maxLen Maximum buffer length
 */
VSTBRIDGE_API void vstbridge_get_vendor_name(VstPluginHandle plugin,
    char* buffer, int maxLen);

/**
 * @brief Gets the plugin product name
 * @param plugin The plugin handle
 * @param buffer Buffer to receive the name
 * @param maxLen Maximum buffer length
 */
VSTBRIDGE_API void vstbridge_get_product_name(VstPluginHandle plugin,
    char* buffer, int maxLen);

/**
 * @brief Gets the plugin version
 * @param plugin The plugin handle
 * @return Version number
 */
VSTBRIDGE_API int vstbridge_get_plugin_version(VstPluginHandle plugin);

/**
 * @brief Gets the number of audio inputs
 * @param plugin The plugin handle
 * @return Number of input channels
 */
VSTBRIDGE_API int vstbridge_get_num_inputs(VstPluginHandle plugin);

/**
 * @brief Gets the number of audio outputs
 * @param plugin The plugin handle
 * @return Number of output channels
 */
VSTBRIDGE_API int vstbridge_get_num_outputs(VstPluginHandle plugin);

/**
 * @brief Checks if the plugin is a synthesizer
 * @param plugin The plugin handle
 * @return 1 if synth, 0 if effect
 */
VSTBRIDGE_API int vstbridge_is_synth(VstPluginHandle plugin);

/**
 * @brief Gets the plugin unique ID
 * @param plugin The plugin handle
 * @return Unique ID (for VST2) or hash of FUID (for VST3)
 */
VSTBRIDGE_API unsigned int vstbridge_get_unique_id(VstPluginHandle plugin);

/**
 * @brief Gets the plugin latency in samples
 * @param plugin The plugin handle
 * @return Latency in samples
 */
VSTBRIDGE_API int vstbridge_get_latency(VstPluginHandle plugin);

/**
 * @brief Gets the tail size in samples (reverb tail, etc.)
 * @param plugin The plugin handle
 * @return Tail size in samples
 */
VSTBRIDGE_API int vstbridge_get_tail_size(VstPluginHandle plugin);

/* =============================================================================
 * Presets / State
 * ============================================================================= */

/**
 * @brief Loads a preset from file
 * @param plugin The plugin handle
 * @param path Path to the preset file (.fxp, .fxb, .vstpreset)
 * @return VSTBRIDGE_OK on success, error code on failure
 */
VSTBRIDGE_API int vstbridge_load_preset(VstPluginHandle plugin, const char* path);

/**
 * @brief Saves a preset to file
 * @param plugin The plugin handle
 * @param path Path to save the preset
 * @return VSTBRIDGE_OK on success, error code on failure
 */
VSTBRIDGE_API int vstbridge_save_preset(VstPluginHandle plugin, const char* path);

/**
 * @brief Gets the plugin state as a byte array
 * @param plugin The plugin handle
 * @param data Buffer to receive state data (NULL to query size)
 * @param maxLen Maximum buffer length
 * @return Actual state size, or -1 on error
 */
VSTBRIDGE_API int vstbridge_get_state(VstPluginHandle plugin,
    unsigned char* data, int maxLen);

/**
 * @brief Sets the plugin state from a byte array
 * @param plugin The plugin handle
 * @param data State data buffer
 * @param length Data length
 * @return VSTBRIDGE_OK on success, error code on failure
 */
VSTBRIDGE_API int vstbridge_set_state(VstPluginHandle plugin,
    const unsigned char* data, int length);

/**
 * @brief Gets the number of programs (presets)
 * @param plugin The plugin handle
 * @return Number of programs
 */
VSTBRIDGE_API int vstbridge_get_program_count(VstPluginHandle plugin);

/**
 * @brief Gets the current program index
 * @param plugin The plugin handle
 * @return Current program index
 */
VSTBRIDGE_API int vstbridge_get_program(VstPluginHandle plugin);

/**
 * @brief Sets the current program
 * @param plugin The plugin handle
 * @param index Program index
 */
VSTBRIDGE_API void vstbridge_set_program(VstPluginHandle plugin, int index);

/**
 * @brief Gets a program name
 * @param plugin The plugin handle
 * @param index Program index
 * @param buffer Buffer to receive the name
 * @param maxLen Maximum buffer length
 */
VSTBRIDGE_API void vstbridge_get_program_name(VstPluginHandle plugin, int index,
    char* buffer, int maxLen);

/* =============================================================================
 * Editor / GUI
 * ============================================================================= */

/**
 * @brief Checks if the plugin has an editor
 * @param plugin The plugin handle
 * @return 1 if has editor, 0 if not
 */
VSTBRIDGE_API int vstbridge_has_editor(VstPluginHandle plugin);

/**
 * @brief Opens the plugin editor
 * @param plugin The plugin handle
 * @param parentWindow Platform-specific parent window handle (HWND on Windows)
 */
VSTBRIDGE_API void vstbridge_open_editor(VstPluginHandle plugin, void* parentWindow);

/**
 * @brief Closes the plugin editor
 * @param plugin The plugin handle
 */
VSTBRIDGE_API void vstbridge_close_editor(VstPluginHandle plugin);

/**
 * @brief Gets the editor window size
 * @param plugin The plugin handle
 * @param width Pointer to receive width
 * @param height Pointer to receive height
 */
VSTBRIDGE_API void vstbridge_get_editor_size(VstPluginHandle plugin,
    int* width, int* height);

/**
 * @brief Notifies the editor of idle time (call periodically)
 * @param plugin The plugin handle
 */
VSTBRIDGE_API void vstbridge_editor_idle(VstPluginHandle plugin);

/**
 * @brief Checks if the editor is currently open
 * @param plugin The plugin handle
 * @return 1 if open, 0 if closed
 */
VSTBRIDGE_API int vstbridge_is_editor_open(VstPluginHandle plugin);

/* =============================================================================
 * Transport / Timing
 * ============================================================================= */

/**
 * @brief Sets the transport position
 * @param plugin The plugin handle
 * @param samplePos Position in samples
 * @param tempo Tempo in BPM
 * @param timeSigNum Time signature numerator
 * @param timeSigDen Time signature denominator
 */
VSTBRIDGE_API void vstbridge_set_transport(VstPluginHandle plugin,
    double samplePos, double tempo, int timeSigNum, int timeSigDen);

/**
 * @brief Sets the transport play state
 * @param plugin The plugin handle
 * @param playing 1 if playing, 0 if stopped
 * @param recording 1 if recording, 0 if not
 * @param looping 1 if looping, 0 if not
 */
VSTBRIDGE_API void vstbridge_set_transport_state(VstPluginHandle plugin,
    int playing, int recording, int looping);

/* =============================================================================
 * Utility
 * ============================================================================= */

/**
 * @brief Gets the VST Bridge library version
 * @return Version as packed integer (major.minor.patch)
 */
VSTBRIDGE_API int vstbridge_get_version(void);

/**
 * @brief Gets the VST Bridge library version as string
 * @param buffer Buffer to receive version string
 * @param maxLen Maximum buffer length
 */
VSTBRIDGE_API void vstbridge_get_version_string(char* buffer, int maxLen);

/**
 * @brief Checks if VST2 support is available
 * @return 1 if available, 0 if not
 */
VSTBRIDGE_API int vstbridge_has_vst2_support(void);

/**
 * @brief Checks if VST3 support is available
 * @return 1 if available, 0 if not
 */
VSTBRIDGE_API int vstbridge_has_vst3_support(void);

#ifdef __cplusplus
}
#endif

#endif /* VST_BRIDGE_H */
