using System.Runtime.InteropServices;

namespace MusicEngine.VstBridge;

/// <summary>
/// P/Invoke declarations for the native VST Bridge library.
/// </summary>
internal static class VstBridgeNative
{
    private const string DllName = "MusicEngine.VstBridge.Native";

    #region Error Codes

    public const int VSTBRIDGE_OK = 0;
    public const int VSTBRIDGE_ERROR_INVALID_HANDLE = -1;
    public const int VSTBRIDGE_ERROR_LOAD_FAILED = -2;
    public const int VSTBRIDGE_ERROR_NOT_A_VST = -3;
    public const int VSTBRIDGE_ERROR_INIT_FAILED = -4;
    public const int VSTBRIDGE_ERROR_FILE_NOT_FOUND = -5;
    public const int VSTBRIDGE_ERROR_INVALID_PARAM = -6;
    public const int VSTBRIDGE_ERROR_NOT_SUPPORTED = -7;

    #endregion

    #region Plugin Types

    public const int VST_PLUGIN_TYPE_UNKNOWN = 0;
    public const int VST_PLUGIN_TYPE_VST2 = 2;
    public const int VST_PLUGIN_TYPE_VST3 = 3;

    #endregion

    #region Callbacks

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void LogCallback(int level, [MarshalAs(UnmanagedType.LPStr)] string message);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ParameterCallback(nint plugin, int index, float value);

    #endregion

    #region Initialization

    [DllImport(DllName, EntryPoint = "vstbridge_create", CallingConvention = CallingConvention.Cdecl)]
    public static extern nint Create(int sampleRate, int blockSize);

    [DllImport(DllName, EntryPoint = "vstbridge_destroy", CallingConvention = CallingConvention.Cdecl)]
    public static extern void Destroy(nint handle);

    [DllImport(DllName, EntryPoint = "vstbridge_set_sample_rate", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetSampleRate(nint handle, int sampleRate);

    [DllImport(DllName, EntryPoint = "vstbridge_set_block_size", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetBlockSize(nint handle, int blockSize);

    [DllImport(DllName, EntryPoint = "vstbridge_set_log_callback", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetLogCallback(nint handle, LogCallback? callback);

    [DllImport(DllName, EntryPoint = "vstbridge_get_last_error", CallingConvention = CallingConvention.Cdecl)]
    public static extern nint GetLastError(nint handle);

    #endregion

    #region Plugin Loading

    [DllImport(DllName, EntryPoint = "vstbridge_load_plugin", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern nint LoadPlugin(nint host, string path);

    [DllImport(DllName, EntryPoint = "vstbridge_unload_plugin", CallingConvention = CallingConvention.Cdecl)]
    public static extern void UnloadPlugin(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_get_plugin_type", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetPluginType(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_is_plugin_valid", CallingConvention = CallingConvention.Cdecl)]
    public static extern int IsPluginValid(nint plugin);

    #endregion

    #region Audio Processing

    [DllImport(DllName, EntryPoint = "vstbridge_process", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void Process(nint plugin, float** inputs, float** outputs, int numSamples);

    [DllImport(DllName, EntryPoint = "vstbridge_process_replacing", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void ProcessReplacing(nint plugin, float** inputs, float** outputs, int numSamples);

    [DllImport(DllName, EntryPoint = "vstbridge_process_double", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe int ProcessDouble(nint plugin, double** inputs, double** outputs, int numSamples);

    [DllImport(DllName, EntryPoint = "vstbridge_start_processing", CallingConvention = CallingConvention.Cdecl)]
    public static extern void StartProcessing(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_stop_processing", CallingConvention = CallingConvention.Cdecl)]
    public static extern void StopProcessing(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_suspend", CallingConvention = CallingConvention.Cdecl)]
    public static extern void Suspend(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_resume", CallingConvention = CallingConvention.Cdecl)]
    public static extern void Resume(nint plugin);

    #endregion

    #region Parameters

    [DllImport(DllName, EntryPoint = "vstbridge_get_param_count", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetParamCount(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_get_param", CallingConvention = CallingConvention.Cdecl)]
    public static extern float GetParam(nint plugin, int index);

    [DllImport(DllName, EntryPoint = "vstbridge_set_param", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetParam(nint plugin, int index, float value);

    [DllImport(DllName, EntryPoint = "vstbridge_get_param_name", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void GetParamName(nint plugin, int index, byte* buffer, int maxLen);

    [DllImport(DllName, EntryPoint = "vstbridge_get_param_display", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void GetParamDisplay(nint plugin, int index, byte* buffer, int maxLen);

    [DllImport(DllName, EntryPoint = "vstbridge_get_param_label", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void GetParamLabel(nint plugin, int index, byte* buffer, int maxLen);

    [DllImport(DllName, EntryPoint = "vstbridge_set_param_callback", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetParamCallback(nint plugin, ParameterCallback? callback);

    #endregion

    #region MIDI

    [DllImport(DllName, EntryPoint = "vstbridge_send_midi", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SendMidi(nint plugin, int status, int data1, int data2);

    [DllImport(DllName, EntryPoint = "vstbridge_send_midi_at", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SendMidiAt(nint plugin, int deltaFrames, int status, int data1, int data2);

    [DllImport(DllName, EntryPoint = "vstbridge_send_midi_sysex", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void SendMidiSysEx(nint plugin, byte* data, int length);

    [DllImport(DllName, EntryPoint = "vstbridge_clear_midi", CallingConvention = CallingConvention.Cdecl)]
    public static extern void ClearMidi(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_all_notes_off", CallingConvention = CallingConvention.Cdecl)]
    public static extern void AllNotesOff(nint plugin);

    #endregion

    #region Plugin Info

    [DllImport(DllName, EntryPoint = "vstbridge_get_plugin_name", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void GetPluginName(nint plugin, byte* buffer, int maxLen);

    [DllImport(DllName, EntryPoint = "vstbridge_get_vendor_name", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void GetVendorName(nint plugin, byte* buffer, int maxLen);

    [DllImport(DllName, EntryPoint = "vstbridge_get_product_name", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void GetProductName(nint plugin, byte* buffer, int maxLen);

    [DllImport(DllName, EntryPoint = "vstbridge_get_plugin_version", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetPluginVersion(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_get_num_inputs", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetNumInputs(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_get_num_outputs", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetNumOutputs(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_is_synth", CallingConvention = CallingConvention.Cdecl)]
    public static extern int IsSynth(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_get_unique_id", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint GetUniqueId(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_get_latency", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetLatency(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_get_tail_size", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetTailSize(nint plugin);

    #endregion

    #region Presets / State

    [DllImport(DllName, EntryPoint = "vstbridge_load_preset", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int LoadPreset(nint plugin, string path);

    [DllImport(DllName, EntryPoint = "vstbridge_save_preset", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int SavePreset(nint plugin, string path);

    [DllImport(DllName, EntryPoint = "vstbridge_get_state", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe int GetState(nint plugin, byte* data, int maxLen);

    [DllImport(DllName, EntryPoint = "vstbridge_set_state", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe int SetState(nint plugin, byte* data, int length);

    [DllImport(DllName, EntryPoint = "vstbridge_get_program_count", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetProgramCount(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_get_program", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetProgram(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_set_program", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetProgram(nint plugin, int index);

    [DllImport(DllName, EntryPoint = "vstbridge_get_program_name", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void GetProgramName(nint plugin, int index, byte* buffer, int maxLen);

    #endregion

    #region Editor / GUI

    [DllImport(DllName, EntryPoint = "vstbridge_has_editor", CallingConvention = CallingConvention.Cdecl)]
    public static extern int HasEditor(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_open_editor", CallingConvention = CallingConvention.Cdecl)]
    public static extern void OpenEditor(nint plugin, nint parentWindow);

    [DllImport(DllName, EntryPoint = "vstbridge_close_editor", CallingConvention = CallingConvention.Cdecl)]
    public static extern void CloseEditor(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_get_editor_size", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void GetEditorSize(nint plugin, int* width, int* height);

    [DllImport(DllName, EntryPoint = "vstbridge_editor_idle", CallingConvention = CallingConvention.Cdecl)]
    public static extern void EditorIdle(nint plugin);

    [DllImport(DllName, EntryPoint = "vstbridge_is_editor_open", CallingConvention = CallingConvention.Cdecl)]
    public static extern int IsEditorOpen(nint plugin);

    #endregion

    #region Transport / Timing

    [DllImport(DllName, EntryPoint = "vstbridge_set_transport", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetTransport(nint plugin, double samplePos, double tempo, int timeSigNum, int timeSigDen);

    [DllImport(DllName, EntryPoint = "vstbridge_set_transport_state", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetTransportState(nint plugin, int playing, int recording, int looping);

    #endregion

    #region Utility

    [DllImport(DllName, EntryPoint = "vstbridge_get_version", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetVersion();

    [DllImport(DllName, EntryPoint = "vstbridge_get_version_string", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void GetVersionString(byte* buffer, int maxLen);

    [DllImport(DllName, EntryPoint = "vstbridge_has_vst2_support", CallingConvention = CallingConvention.Cdecl)]
    public static extern int HasVst2Support();

    [DllImport(DllName, EntryPoint = "vstbridge_has_vst3_support", CallingConvention = CallingConvention.Cdecl)]
    public static extern int HasVst3Support();

    #endregion
}
