// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: MIDI send helper with random input and modulation utilities.

using System;
using System.Threading.Tasks;
using MusicEngine.Core;

namespace MusicEngine.Effects.Midi;

/// <summary>
/// MIDI send helper with random input and modulation utilities.
/// </summary>
public sealed class MidiSend
{
    private readonly Random _random = new();
    private readonly MidiEffectChain _effects = new();
    private readonly AudioEngine? _engine;

    public int DeviceIndex { get; }
    public int Channel { get; }
    public ISynth Synth { get; }

    public MidiSend(int deviceIndex, int channel, ISynth synth, AudioEngine? engine = null)
    {
        DeviceIndex = deviceIndex;
        Channel = channel;
        Synth = synth;
        _engine = engine;
    }

    /// <summary>
    /// Add a MIDI effect to this send.
    /// </summary>
    public void AddEffect(IMidiEffect effect) => _effects.Add(effect);

    /// <summary>
    /// Clear all MIDI effects.
    /// </summary>
    public void ClearEffects() => _effects.Clear();

    /// <summary>
    /// Enable or disable this specific route.
    /// </summary>
    public void Active(bool enabled, bool sendAllNotesOff = true)
    {
        _engine?.SetMidiRouteEnabled(DeviceIndex, Channel, Synth, enabled, sendAllNotesOff);
    }

    /// <summary>
    /// Enable this specific route.
    /// </summary>
    public void Enable(bool sendAllNotesOff = true) => Active(true, sendAllNotesOff);

    /// <summary>
    /// Disable this specific route.
    /// </summary>
    public void Disable(bool sendAllNotesOff = true) => Active(false, sendAllNotesOff);

    /// <summary>
    /// Trigger a note on.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        if (_effects.ProcessNoteOn(ref note, ref velocity))
        {
            Synth.NoteOn(note, velocity);
        }
    }

    /// <summary>
    /// Trigger a note off.
    /// </summary>
    public void NoteOff(int note)
    {
        if (_effects.ProcessNoteOff(ref note))
        {
            Synth.NoteOff(note);
        }
    }

    /// <summary>
    /// Stop all notes.
    /// </summary>
    public void AllNotesOff() => Synth.AllNotesOff();

    /// <summary>
    /// Generate random note input (fire-and-forget).
    /// </summary>
    public void GenerateRandomInput(int noteCount = 16, int minNote = 36, int maxNote = 84, int minVelocity = 40,
        int maxVelocity = 120, double minDurationSeconds = 0.05, double maxDurationSeconds = 0.25,
        double gapSeconds = 0.02, int? seed = null)
    {
        _ = GenerateRandomInputAsync(noteCount, minNote, maxNote, minVelocity, maxVelocity,
            minDurationSeconds, maxDurationSeconds, gapSeconds, seed);
    }

    /// <summary>
    /// Generate random note input (awaitable).
    /// </summary>
    public async Task GenerateRandomInputAsync(int noteCount = 16, int minNote = 36, int maxNote = 84,
        int minVelocity = 40, int maxVelocity = 120, double minDurationSeconds = 0.05,
        double maxDurationSeconds = 0.25, double gapSeconds = 0.02, int? seed = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : _random;
        minNote = Math.Clamp(minNote, 0, 127);
        maxNote = Math.Clamp(maxNote, 0, 127);
        if (maxNote < minNote) (minNote, maxNote) = (maxNote, minNote);

        for (int i = 0; i < noteCount; i++)
        {
            int note = rng.Next(minNote, maxNote + 1);
            int velocity = rng.Next(minVelocity, maxVelocity + 1);
            var duration = rng.NextDouble() * (maxDurationSeconds - minDurationSeconds) + minDurationSeconds;

            NoteOn(note, velocity);
            await Task.Delay(TimeSpan.FromSeconds(duration));
            NoteOff(note);

            if (gapSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(gapSeconds));
            }
        }
    }

    /// <summary>
    /// Apply a simple LFO to a named parameter.
    /// </summary>
    public void Lfo(string parameter, float min, float max, double hz = 2.0, double durationSeconds = 5.0)
    {
        _ = LfoAsync(parameter, min, max, hz, durationSeconds);
    }

    /// <summary>
    /// Apply a simple LFO to a named parameter (awaitable).
    /// </summary>
    public async Task LfoAsync(string parameter, float min, float max, double hz = 2.0, double durationSeconds = 5.0)
    {
        if (string.IsNullOrWhiteSpace(parameter)) return;
        if (hz <= 0 || durationSeconds <= 0) return;

        var totalMs = durationSeconds * 1000.0;
        var stepMs = 10.0;
        var steps = Math.Max(1, (int)Math.Round(totalMs / stepMs));
        var minVal = Math.Min(min, max);
        var maxVal = Math.Max(min, max);

        for (int i = 0; i < steps; i++)
        {
            double t = i * stepMs / 1000.0;
            float phase = (float)(t * hz * Math.PI * 2.0);
            float value = (float)((Math.Sin(phase) * 0.5 + 0.5) * (maxVal - minVal) + minVal);
            Synth.SetParameter(parameter, value);
            await Task.Delay(TimeSpan.FromMilliseconds(stepMs));
        }
    }
}
