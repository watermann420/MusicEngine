namespace MusicEngine.VstBridge;

/// <summary>
/// Interface for native VST plugin instances.
/// </summary>
public interface INativeVstPlugin : IDisposable
{
    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the vendor name.
    /// </summary>
    string Vendor { get; }

    /// <summary>
    /// Gets the product name.
    /// </summary>
    string Product { get; }

    /// <summary>
    /// Gets the plugin version.
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Gets the plugin type (VST2 or VST3).
    /// </summary>
    VstPluginType PluginType { get; }

    /// <summary>
    /// Gets the plugin unique ID.
    /// </summary>
    uint UniqueId { get; }

    /// <summary>
    /// Gets the number of audio inputs.
    /// </summary>
    int NumInputs { get; }

    /// <summary>
    /// Gets the number of audio outputs.
    /// </summary>
    int NumOutputs { get; }

    /// <summary>
    /// Gets whether the plugin is a synthesizer.
    /// </summary>
    bool IsSynth { get; }

    /// <summary>
    /// Gets the plugin latency in samples.
    /// </summary>
    int Latency { get; }

    /// <summary>
    /// Gets the tail size in samples.
    /// </summary>
    int TailSize { get; }

    /// <summary>
    /// Gets the number of parameters.
    /// </summary>
    int ParameterCount { get; }

    /// <summary>
    /// Gets whether the plugin is valid and loaded.
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// Gets whether the plugin has an editor.
    /// </summary>
    bool HasEditor { get; }

    /// <summary>
    /// Gets whether the editor is currently open.
    /// </summary>
    bool IsEditorOpen { get; }

    /// <summary>
    /// Gets or sets a parameter value.
    /// </summary>
    float this[int parameterIndex] { get; set; }

    /// <summary>
    /// Gets the parameter information.
    /// </summary>
    VstParameter GetParameter(int index);

    /// <summary>
    /// Processes audio through the plugin.
    /// </summary>
    void Process(float[][] inputs, float[][] outputs, int numSamples);

    /// <summary>
    /// Sends a MIDI message to the plugin.
    /// </summary>
    void SendMidi(int status, int data1, int data2);

    /// <summary>
    /// Sends a MIDI message with timing offset.
    /// </summary>
    void SendMidiAt(int deltaFrames, int status, int data1, int data2);

    /// <summary>
    /// Sends a MIDI note on message.
    /// </summary>
    void NoteOn(int channel, int note, int velocity);

    /// <summary>
    /// Sends a MIDI note off message.
    /// </summary>
    void NoteOff(int channel, int note, int velocity = 0);

    /// <summary>
    /// Sends a MIDI control change message.
    /// </summary>
    void ControlChange(int channel, int cc, int value);

    /// <summary>
    /// Sends a MIDI program change message.
    /// </summary>
    void ProgramChange(int channel, int program);

    /// <summary>
    /// Sends all notes off on all channels.
    /// </summary>
    void AllNotesOff();

    /// <summary>
    /// Clears all pending MIDI events.
    /// </summary>
    void ClearMidi();

    /// <summary>
    /// Starts audio processing.
    /// </summary>
    void StartProcessing();

    /// <summary>
    /// Stops audio processing.
    /// </summary>
    void StopProcessing();

    /// <summary>
    /// Suspends the plugin.
    /// </summary>
    void Suspend();

    /// <summary>
    /// Resumes the plugin.
    /// </summary>
    void Resume();

    /// <summary>
    /// Gets the plugin state as a byte array.
    /// </summary>
    byte[]? GetState();

    /// <summary>
    /// Sets the plugin state from a byte array.
    /// </summary>
    bool SetState(byte[] data);

    /// <summary>
    /// Gets the number of programs.
    /// </summary>
    int ProgramCount { get; }

    /// <summary>
    /// Gets or sets the current program index.
    /// </summary>
    int CurrentProgram { get; set; }

    /// <summary>
    /// Gets a program name.
    /// </summary>
    string GetProgramName(int index);

    /// <summary>
    /// Opens the plugin editor.
    /// </summary>
    void OpenEditor(nint parentWindow);

    /// <summary>
    /// Closes the plugin editor.
    /// </summary>
    void CloseEditor();

    /// <summary>
    /// Gets the editor size.
    /// </summary>
    (int Width, int Height) GetEditorSize();

    /// <summary>
    /// Called periodically for editor updates.
    /// </summary>
    void EditorIdle();

    /// <summary>
    /// Sets the transport position.
    /// </summary>
    void SetTransport(double samplePos, double tempo, int timeSigNum = 4, int timeSigDen = 4);

    /// <summary>
    /// Sets the transport state.
    /// </summary>
    void SetTransportState(bool playing, bool recording = false, bool looping = false);
}

/// <summary>
/// VST plugin type.
/// </summary>
public enum VstPluginType
{
    Unknown = 0,
    Vst2 = 2,
    Vst3 = 3
}

/// <summary>
/// VST parameter information.
/// </summary>
public readonly record struct VstParameter(
    int Index,
    string Name,
    string DisplayValue,
    string Label,
    float Value
);
