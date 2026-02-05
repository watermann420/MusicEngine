// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Managed wrapper for native VST plugin instances.

using System.Runtime.InteropServices;
using System.Text;

namespace MusicEngine.CppLayer;

public sealed class NativeVstPlugin : INativeVstPlugin
{
    private readonly IntPtr _handle;

    internal NativeVstPlugin(IntPtr handle)
    {
        _handle = handle;
    }

    internal IntPtr Handle => _handle;

    public string Name => GetString(VstBridgeNative.me_vst_plugin_get_name);
    public string Vendor => GetString(VstBridgeNative.me_vst_plugin_get_vendor);
    public string Product => GetString(VstBridgeNative.me_vst_plugin_get_product);

    public Version Version
    {
        get
        {
            var text = GetString(VstBridgeNative.me_vst_plugin_get_version);
            return Version.TryParse(text, out var parsed) ? parsed : new Version(1, 0, 0, 0);
        }
    }

    public VstPluginType PluginType => (VstPluginType)VstBridgeNative.me_vst_plugin_get_type(_handle);
    public uint UniqueId => VstBridgeNative.me_vst_plugin_get_unique_id(_handle);
    public int NumInputs => VstBridgeNative.me_vst_plugin_get_num_inputs(_handle);
    public int NumOutputs => VstBridgeNative.me_vst_plugin_get_num_outputs(_handle);
    public bool IsSynth => VstBridgeNative.me_vst_plugin_is_synth(_handle) != 0;
    public int Latency => VstBridgeNative.me_vst_plugin_get_latency(_handle);
    public int TailSize => VstBridgeNative.me_vst_plugin_get_tail_size(_handle);
    public int ParameterCount => VstBridgeNative.me_vst_plugin_get_parameter_count(_handle);
    public bool IsValid => VstBridgeNative.me_vst_plugin_is_valid(_handle) != 0;
    public bool HasEditor => VstBridgeNative.me_vst_plugin_has_editor(_handle) != 0;
    public bool IsEditorOpen => VstBridgeNative.me_vst_plugin_is_editor_open(_handle) != 0;

    public float this[int index]
    {
        get => VstBridgeNative.me_vst_plugin_get_parameter_value(_handle, index);
        set => VstBridgeNative.me_vst_plugin_set_parameter_value(_handle, index, value);
    }

    public VstParameter GetParameter(int index)
    {
        var name = GetParameterString(VstBridgeNative.me_vst_plugin_get_parameter_name, index);
        var label = GetParameterString(VstBridgeNative.me_vst_plugin_get_parameter_label, index);
        var display = GetParameterString(VstBridgeNative.me_vst_plugin_get_parameter_display, index);
        var value = VstBridgeNative.me_vst_plugin_get_parameter_value(_handle, index);
        return new VstParameter(index, name, label, display, value);
    }

    public void Process(float[][] inputs, float[][] outputs, int sampleCount)
    {
        if (inputs.Length == 0 && outputs.Length == 0)
        {
            return;
        }

        var inputHandles = new GCHandle[inputs.Length];
        var outputHandles = new GCHandle[outputs.Length];
        var inputPtrs = new IntPtr[inputs.Length];
        var outputPtrs = new IntPtr[outputs.Length];

        try
        {
            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i] == null)
                {
                    inputPtrs[i] = IntPtr.Zero;
                    continue;
                }

                inputHandles[i] = GCHandle.Alloc(inputs[i], GCHandleType.Pinned);
                inputPtrs[i] = inputHandles[i].AddrOfPinnedObject();
            }

            for (int i = 0; i < outputs.Length; i++)
            {
                if (outputs[i] == null)
                {
                    outputPtrs[i] = IntPtr.Zero;
                    continue;
                }

                outputHandles[i] = GCHandle.Alloc(outputs[i], GCHandleType.Pinned);
                outputPtrs[i] = outputHandles[i].AddrOfPinnedObject();
            }

            VstBridgeNative.me_vst_plugin_process(_handle, inputPtrs, outputPtrs, inputs.Length, outputs.Length, sampleCount);
        }
        finally
        {
            for (int i = 0; i < inputHandles.Length; i++)
            {
                if (inputHandles[i].IsAllocated)
                {
                    inputHandles[i].Free();
                }
            }

            for (int i = 0; i < outputHandles.Length; i++)
            {
                if (outputHandles[i].IsAllocated)
                {
                    outputHandles[i].Free();
                }
            }
        }
    }

    public void SendMidi(int status, int data1, int data2)
    {
        VstBridgeNative.me_vst_plugin_send_midi(_handle, status, data1, data2);
    }

    public void SendMidiAt(int status, int data1, int data2, int deltaFrames)
    {
        VstBridgeNative.me_vst_plugin_send_midi_at(_handle, status, data1, data2, deltaFrames);
    }

    public void NoteOn(int channel, int note, int velocity)
    {
        VstBridgeNative.me_vst_plugin_note_on(_handle, channel, note, velocity);
    }

    public void NoteOff(int channel, int note)
    {
        VstBridgeNative.me_vst_plugin_note_off(_handle, channel, note);
    }

    public void ControlChange(int channel, int controller, int value)
    {
        VstBridgeNative.me_vst_plugin_control_change(_handle, channel, controller, value);
    }

    public void ProgramChange(int channel, int program)
    {
        VstBridgeNative.me_vst_plugin_program_change(_handle, channel, program);
    }

    public void AllNotesOff()
    {
        VstBridgeNative.me_vst_plugin_all_notes_off(_handle);
    }

    public void ClearMidi()
    {
        VstBridgeNative.me_vst_plugin_clear_midi(_handle);
    }

    public void StartProcessing()
    {
        VstBridgeNative.me_vst_plugin_start_processing(_handle);
    }

    public void StopProcessing()
    {
        VstBridgeNative.me_vst_plugin_stop_processing(_handle);
    }

    public void Suspend()
    {
        VstBridgeNative.me_vst_plugin_suspend(_handle);
    }

    public void Resume()
    {
        VstBridgeNative.me_vst_plugin_resume(_handle);
    }

    public byte[] GetState()
    {
        int size = VstBridgeNative.me_vst_plugin_get_state_size(_handle);
        if (size <= 0)
        {
            return Array.Empty<byte>();
        }

        var buffer = new byte[size];
        int written = VstBridgeNative.me_vst_plugin_get_state(_handle, buffer, buffer.Length);
        if (written <= 0)
        {
            return Array.Empty<byte>();
        }

        if (written == buffer.Length)
        {
            return buffer;
        }

        var trimmed = new byte[written];
        Array.Copy(buffer, trimmed, written);
        return trimmed;
    }

    public void SetState(byte[] state)
    {
        if (state.Length == 0)
        {
            return;
        }

        VstBridgeNative.me_vst_plugin_set_state(_handle, state, state.Length);
    }

    public int ProgramCount => VstBridgeNative.me_vst_plugin_get_program_count(_handle);

    public int CurrentProgram
    {
        get => VstBridgeNative.me_vst_plugin_get_current_program(_handle);
        set => VstBridgeNative.me_vst_plugin_set_current_program(_handle, value);
    }

    public string GetProgramName(int index)
    {
        var buffer = new StringBuilder(256);
        VstBridgeNative.me_vst_plugin_get_program_name(_handle, index, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    public bool LoadPreset(string path)
    {
        return VstBridgeNative.me_vst_plugin_load_preset(_handle, path) != 0;
    }

    public bool SavePreset(string path)
    {
        return VstBridgeNative.me_vst_plugin_save_preset(_handle, path) != 0;
    }

    public IntPtr OpenEditor(IntPtr parentWindow)
    {
        return VstBridgeNative.me_vst_plugin_open_editor(_handle, parentWindow);
    }

    public void CloseEditor()
    {
        VstBridgeNative.me_vst_plugin_close_editor(_handle);
    }

    public (int width, int height) GetEditorSize()
    {
        return VstBridgeNative.me_vst_plugin_get_editor_size(_handle, out int width, out int height) != 0
            ? (width, height)
            : (0, 0);
    }

    public void EditorIdle()
    {
        VstBridgeNative.me_vst_plugin_editor_idle(_handle);
    }

    public void SetTransport(double tempo, double ppqPosition, int timeSigNumerator, int timeSigDenominator)
    {
        VstBridgeNative.me_vst_plugin_set_transport(_handle, tempo, ppqPosition, timeSigNumerator, timeSigDenominator);
    }

    public void SetTransportState(bool isPlaying, bool isRecording, bool isLooping)
    {
        VstBridgeNative.me_vst_plugin_set_transport_state(
            _handle,
            isPlaying ? 1 : 0,
            isRecording ? 1 : 0,
            isLooping ? 1 : 0);
    }

    private string GetString(Func<IntPtr, StringBuilder, int, int> getter)
    {
        var buffer = new StringBuilder(256);
        getter(_handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private string GetParameterString(Func<IntPtr, int, StringBuilder, int, int> getter, int index)
    {
        var buffer = new StringBuilder(256);
        getter(_handle, index, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    public void Dispose()
    {
    }
}
