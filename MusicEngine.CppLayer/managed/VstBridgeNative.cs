// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Native VST bridge P/Invoke declarations.

using System.Runtime.InteropServices;
using System.Text;

namespace MusicEngine.CppLayer;

internal static class VstBridgeNative
{
    internal const string DllName = "MusicEngine.CppLayer.Native";

    internal static bool IsAvailable()
    {
        if (!NativeLibrary.TryLoad(DllName, out var handle))
        {
            return false;
        }

        NativeLibrary.Free(handle);
        return true;
    }

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_is_available();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_get_version(StringBuilder buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_has_vst2();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_has_vst3();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr me_vst_host_create(int sampleRate, int blockSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_host_destroy(IntPtr host);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_host_set_sample_rate(IntPtr host, int sampleRate);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_host_set_block_size(IntPtr host, int blockSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr me_vst_host_load_plugin(IntPtr host, [MarshalAs(UnmanagedType.LPStr)] string path);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_host_unload_plugin(IntPtr host, IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_host_unload_all(IntPtr host);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_host_get_last_error(IntPtr host, StringBuilder buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_name(IntPtr plugin, StringBuilder buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_vendor(IntPtr plugin, StringBuilder buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_product(IntPtr plugin, StringBuilder buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_version(IntPtr plugin, StringBuilder buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_type(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint me_vst_plugin_get_unique_id(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_num_inputs(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_num_outputs(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_is_synth(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_latency(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_tail_size(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_parameter_count(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_is_valid(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_has_editor(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_is_editor_open(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern float me_vst_plugin_get_parameter_value(IntPtr plugin, int index);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_set_parameter_value(IntPtr plugin, int index, float value);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_parameter_name(IntPtr plugin, int index, StringBuilder buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_parameter_label(IntPtr plugin, int index, StringBuilder buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_parameter_display(IntPtr plugin, int index, StringBuilder buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_process(IntPtr plugin, IntPtr[] inputs, IntPtr[] outputs, int numInputs, int numOutputs, int sampleCount);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_send_midi(IntPtr plugin, int status, int data1, int data2);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_send_midi_at(IntPtr plugin, int status, int data1, int data2, int deltaFrames);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_note_on(IntPtr plugin, int channel, int note, int velocity);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_note_off(IntPtr plugin, int channel, int note);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_control_change(IntPtr plugin, int channel, int controller, int value);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_program_change(IntPtr plugin, int channel, int program);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_all_notes_off(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_clear_midi(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_start_processing(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_stop_processing(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_suspend(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_resume(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_state_size(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_state(IntPtr plugin, byte[] buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_set_state(IntPtr plugin, byte[] buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_program_count(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_current_program(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_set_current_program(IntPtr plugin, int index);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_program_name(IntPtr plugin, int index, StringBuilder buffer, int bufferSize);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_load_preset(IntPtr plugin, [MarshalAs(UnmanagedType.LPStr)] string path);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_save_preset(IntPtr plugin, [MarshalAs(UnmanagedType.LPStr)] string path);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr me_vst_plugin_open_editor(IntPtr plugin, IntPtr parentWindow);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_close_editor(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int me_vst_plugin_get_editor_size(IntPtr plugin, out int width, out int height);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_editor_idle(IntPtr plugin);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_set_transport(IntPtr plugin, double tempo, double ppqPosition, int timeSigNumerator, int timeSigDenominator);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void me_vst_plugin_set_transport_state(IntPtr plugin, int isPlaying, int isRecording, int isLooping);
}
