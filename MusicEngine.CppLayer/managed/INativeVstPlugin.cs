// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann
// Description: Interface for native VST plugin instances.

namespace MusicEngine.CppLayer;

public interface INativeVstPlugin : IDisposable
{
    string Name { get; }
    string Vendor { get; }
    string Product { get; }
    Version Version { get; }
    VstPluginType PluginType { get; }
    uint UniqueId { get; }
    int NumInputs { get; }
    int NumOutputs { get; }
    bool IsSynth { get; }
    int Latency { get; }
    int TailSize { get; }
    int ParameterCount { get; }
    bool IsValid { get; }
    bool HasEditor { get; }
    bool IsEditorOpen { get; }
    float this[int index] { get; set; }

    VstParameter GetParameter(int index);
    void Process(float[][] inputs, float[][] outputs, int sampleCount);
    void SendMidi(int status, int data1, int data2);
    void SendMidiAt(int status, int data1, int data2, int deltaFrames);
    void NoteOn(int channel, int note, int velocity);
    void NoteOff(int channel, int note);
    void ControlChange(int channel, int controller, int value);
    void ProgramChange(int channel, int program);
    void AllNotesOff();
    void ClearMidi();
    void StartProcessing();
    void StopProcessing();
    void Suspend();
    void Resume();
    byte[] GetState();
    void SetState(byte[] state);
    int ProgramCount { get; }
    int CurrentProgram { get; set; }
    string GetProgramName(int index);
    bool LoadPreset(string path);
    bool SavePreset(string path);
    IntPtr OpenEditor(IntPtr parentWindow);
    void CloseEditor();
    (int width, int height) GetEditorSize();
    void EditorIdle();
    void SetTransport(double tempo, double ppqPosition, int timeSigNumerator, int timeSigDenominator);
    void SetTransportState(bool isPlaying, bool isRecording, bool isLooping);
}
