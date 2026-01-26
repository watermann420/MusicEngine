// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using NAudio.Wave;
using MusicEngine.Core;

namespace MusicEngine.Infrastructure.DependencyInjection.Interfaces;

/// <summary>
/// Interface for the audio engine.
/// </summary>
public interface IAudioEngine : IDisposable
{
    void Initialize();
    void RouteMidiInput(int deviceIndex, ISynth synth);
    void MapMidiControl(int deviceIndex, int controlNumber, ISynth synth, string parameter);
    void AddSampleProvider(ISampleProvider provider);
    void SetChannelGain(int index, float gain);
    void SetAllChannelsGain(float gain);
    IVstPlugin? LoadVstPlugin(string nameOrPath);
    void UnloadVstPlugin(string name);
    void StartRecording(string outputPath);
    string? StopRecording();
    bool IsRecording { get; }
    VstHost VstHost { get; }
}
