#pragma once

#include <cstdint>

#if defined(_WIN32)
    #define ME_VSTBRIDGE_EXPORT __declspec(dllexport)
#else
    #define ME_VSTBRIDGE_EXPORT
#endif

extern "C" {
    ME_VSTBRIDGE_EXPORT int me_vst_is_available();
    ME_VSTBRIDGE_EXPORT int me_vst_get_version(char* buffer, int bufferSize);
    ME_VSTBRIDGE_EXPORT int me_vst_has_vst2();
    ME_VSTBRIDGE_EXPORT int me_vst_has_vst3();

    ME_VSTBRIDGE_EXPORT void* me_vst_host_create(int sampleRate, int blockSize);
    ME_VSTBRIDGE_EXPORT void me_vst_host_destroy(void* host);
    ME_VSTBRIDGE_EXPORT void me_vst_host_set_sample_rate(void* host, int sampleRate);
    ME_VSTBRIDGE_EXPORT void me_vst_host_set_block_size(void* host, int blockSize);
    ME_VSTBRIDGE_EXPORT void* me_vst_host_load_plugin(void* host, const char* path);
    ME_VSTBRIDGE_EXPORT void me_vst_host_unload_plugin(void* host, void* plugin);
    ME_VSTBRIDGE_EXPORT void me_vst_host_unload_all(void* host);
    ME_VSTBRIDGE_EXPORT int me_vst_host_get_last_error(void* host, char* buffer, int bufferSize);

    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_name(void* plugin, char* buffer, int bufferSize);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_vendor(void* plugin, char* buffer, int bufferSize);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_product(void* plugin, char* buffer, int bufferSize);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_version(void* plugin, char* buffer, int bufferSize);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_type(void* plugin);
    ME_VSTBRIDGE_EXPORT uint32_t me_vst_plugin_get_unique_id(void* plugin);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_num_inputs(void* plugin);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_num_outputs(void* plugin);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_is_synth(void* plugin);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_latency(void* plugin);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_tail_size(void* plugin);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_parameter_count(void* plugin);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_is_valid(void* plugin);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_has_editor(void* plugin);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_is_editor_open(void* plugin);

    ME_VSTBRIDGE_EXPORT float me_vst_plugin_get_parameter_value(void* plugin, int index);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_set_parameter_value(void* plugin, int index, float value);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_parameter_name(void* plugin, int index, char* buffer, int bufferSize);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_parameter_label(void* plugin, int index, char* buffer, int bufferSize);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_parameter_display(void* plugin, int index, char* buffer, int bufferSize);

    ME_VSTBRIDGE_EXPORT void me_vst_plugin_process(void* plugin, float** inputs, float** outputs, int numInputs, int numOutputs, int sampleCount);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_send_midi(void* plugin, int status, int data1, int data2);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_send_midi_at(void* plugin, int status, int data1, int data2, int deltaFrames);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_note_on(void* plugin, int channel, int note, int velocity);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_note_off(void* plugin, int channel, int note);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_control_change(void* plugin, int channel, int controller, int value);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_program_change(void* plugin, int channel, int program);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_all_notes_off(void* plugin);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_clear_midi(void* plugin);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_start_processing(void* plugin);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_stop_processing(void* plugin);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_suspend(void* plugin);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_resume(void* plugin);

    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_state_size(void* plugin);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_state(void* plugin, unsigned char* buffer, int bufferSize);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_set_state(void* plugin, const unsigned char* buffer, int bufferSize);

    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_program_count(void* plugin);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_current_program(void* plugin);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_set_current_program(void* plugin, int index);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_program_name(void* plugin, int index, char* buffer, int bufferSize);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_load_preset(void* plugin, const char* path);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_save_preset(void* plugin, const char* path);

    ME_VSTBRIDGE_EXPORT void* me_vst_plugin_open_editor(void* plugin, void* parentWindow);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_close_editor(void* plugin);
    ME_VSTBRIDGE_EXPORT int me_vst_plugin_get_editor_size(void* plugin, int* width, int* height);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_editor_idle(void* plugin);

    ME_VSTBRIDGE_EXPORT void me_vst_plugin_set_transport(void* plugin, double tempo, double ppqPosition, int timeSigNumerator, int timeSigDenominator);
    ME_VSTBRIDGE_EXPORT void me_vst_plugin_set_transport_state(void* plugin, int isPlaying, int isRecording, int isLooping);
}
