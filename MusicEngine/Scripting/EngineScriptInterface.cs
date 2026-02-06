// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Engine interface for embedding in external projects or game engines.

using System;
using System.Linq;
using System.Threading.Tasks;
using MusicEngine.Core;
using MusicEngine.Vst;

namespace MusicEngine.Scripting;

public sealed class EngineScriptInterfaceOptions
{
    public bool EnableVstScanning { get; set; } = true;
    public bool StartSequencerOnStartup { get; set; } = true;
    public int? SampleRate { get; set; }
}

public interface IEngineScriptInterface : IDisposable
{
    AudioEngine Engine { get; }
    Sequencer Sequencer { get; }
    ScriptHost Host { get; }
    Vst3Registry? VstRegistry { get; }
    bool IsSleeping { get; }

    Task StartupAsync(string? startupScript = null);
    Task RunScriptAsync(string code, bool clearState = true, bool skipIfUnchanged = true);
    EngineStateSnapshot GetStateSnapshot();
    void Sleep();
    void Wake();
}

public sealed class EngineScriptInterface : IEngineScriptInterface
{
    private readonly EngineScriptInterfaceOptions _options;
    private ScriptHost? _host;
    private bool _initialized;
    private bool _sleeping;

    public AudioEngine Engine { get; }
    public Sequencer Sequencer { get; }
    public ScriptHost Host => _host ?? throw new InvalidOperationException("Call StartupAsync before using the host.");
    public Vst3Registry? VstRegistry { get; private set; }
    public bool IsSleeping => _sleeping;

    public EngineScriptInterface(EngineScriptInterfaceOptions? options = null)
    {
        _options = options ?? new EngineScriptInterfaceOptions();
        Engine = new AudioEngine(_options.SampleRate);
        Sequencer = new Sequencer();
    }

    public async Task StartupAsync(string? startupScript = null)
    {
        if (_initialized) return;

        Engine.Initialize();

        if (_options.EnableVstScanning && VstSystem.TryScan(out var scannedRegistry, out _))
        {
            VstRegistry = scannedRegistry;
        }

        _host = new ScriptHost(Engine, Sequencer, VstRegistry);

        if (_options.StartSequencerOnStartup)
        {
            Sequencer.Start();
        }

        _initialized = true;

        if (!string.IsNullOrWhiteSpace(startupScript))
        {
            await _host.ExecuteScriptAsync(startupScript);
        }
    }

    public async Task RunScriptAsync(string code, bool clearState = true, bool skipIfUnchanged = true)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Call StartupAsync before running scripts.");
        }

        if (clearState)
        {
            await Host.RefreshScriptAsync(code, skipIfUnchanged);
            return;
        }

        if (skipIfUnchanged)
        {
            await Host.ExecuteScriptIfChangedAsync(code);
            return;
        }

        await Host.ExecuteScriptAsync(code);
    }

    public EngineStateSnapshot GetStateSnapshot()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Call StartupAsync before reading state.");
        }

        var patterns = Sequencer.Patterns;
        var snapshots = new PatternStateSnapshot[patterns.Count];

        for (int i = 0; i < patterns.Count; i++)
        {
            var pattern = patterns[i];
            var activeNotes = pattern.GetActiveNotesSnapshot();
            var activeNoteSnapshots = new NoteActivitySnapshot[activeNotes.Count];
            for (int n = 0; n < activeNotes.Count; n++)
            {
                var note = activeNotes[n];
                activeNoteSnapshots[n] = new NoteActivitySnapshot
                {
                    Note = note.Note,
                    Velocity = note.Velocity,
                    StartedUtc = note.StartedUtc
                };
            }

            var synthTargets = new string[pattern.SynthTargets.Count];
            for (int t = 0; t < pattern.SynthTargets.Count; t++)
            {
                synthTargets[t] = pattern.SynthTargets[t].GetType().Name;
            }

            var positionInLoop = ComputePositionInLoop(pattern);
            var lastTriggered = pattern.LastTriggeredNote;
            NoteActivitySnapshot? lastTriggeredSnapshot = null;
            if (lastTriggered != null)
            {
                lastTriggeredSnapshot = new NoteActivitySnapshot
                {
                    Note = lastTriggered.Note,
                    Velocity = lastTriggered.Velocity,
                    StartedUtc = lastTriggered.StartedUtc
                };
            }

            snapshots[i] = new PatternStateSnapshot
            {
                Id = pattern.Id,
                Enabled = pattern.Enabled,
                IsLooping = pattern.IsLooping,
                LoopLength = pattern.LoopLength,
                StartBeat = pattern.StartBeat,
                CurrentBeat = pattern.CurrentBeat,
                PositionInLoop = positionInLoop,
                HasActiveNotes = activeNotes.Count > 0,
                ActiveNotes = activeNoteSnapshots,
                LastTriggeredNote = lastTriggeredSnapshot,
                LastTriggeredBeat = pattern.LastTriggeredBeat,
                LastTriggeredUtc = pattern.LastTriggeredUtc,
                SynthTargets = synthTargets
            };
        }

        return new EngineStateSnapshot
        {
            IsSleeping = _sleeping,
            SequencerRunning = Sequencer.IsRunning,
            CurrentBeat = Sequencer.CurrentBeat,
            CurrentTimeSeconds = Sequencer.CurrentTimeSeconds,
            Patterns = snapshots,
            MidiDevices = Engine.GetMidiActivitySnapshot().ToArray()
        };
    }

    public void Sleep()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Call StartupAsync before sleeping.");
        }

        if (_sleeping) return;
        Sequencer.Stop();
        Engine.SuspendOutput();
        _sleeping = true;
    }

    public void Wake()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Call StartupAsync before waking.");
        }

        if (!_sleeping) return;
        Engine.ResumeOutput();
        if (_options.StartSequencerOnStartup)
        {
            Sequencer.Start();
        }
        _sleeping = false;
    }

    public void Dispose()
    {
        Sequencer.Dispose();
        Engine.Dispose();
    }

    private static double? ComputePositionInLoop(Pattern pattern)
    {
        if (pattern.LoopLength <= 0) return null;
        var startBeat = pattern.StartBeat ?? 0.0;
        var relativeBeat = pattern.CurrentBeat - startBeat;
        var mod = relativeBeat % pattern.LoopLength;
        return mod < 0 ? mod + pattern.LoopLength : mod;
    }
}
