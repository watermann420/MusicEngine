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

/// <summary>
/// Configuration options for <see cref="EngineScriptInterface"/>.
/// </summary>
public sealed class EngineScriptInterfaceOptions
{
    /// <summary>
    /// Enable scanning for VST3 plugins on startup.
    /// </summary>
    public bool EnableVstScanning { get; set; } = true;

    /// <summary>
    /// Start the sequencer automatically on startup.
    /// </summary>
    public bool StartSequencerOnStartup { get; set; } = true;

    /// <summary>
    /// Optional sample rate override for the audio engine.
    /// </summary>
    public int? SampleRate { get; set; }
}

/// <summary>
/// Script-oriented engine interface for embedding in host applications.
/// </summary>
public interface IEngineScriptInterface : IDisposable
{
    /// <summary>
    /// Audio engine instance.
    /// </summary>
    AudioEngine Engine { get; }

    /// <summary>
    /// Sequencer instance.
    /// </summary>
    Sequencer Sequencer { get; }

    /// <summary>
    /// Script host used for execution and state.
    /// </summary>
    ScriptHost Host { get; }

    /// <summary>
    /// VST3 registry populated during scanning (if enabled).
    /// </summary>
    Vst3Registry? VstRegistry { get; }

    /// <summary>
    /// Whether the engine is currently sleeping.
    /// </summary>
    bool IsSleeping { get; }

    /// <summary>
    /// Initialize the engine and optional startup script.
    /// </summary>
    /// <param name="startupScript">Optional script to run after initialization.</param>
    Task StartupAsync(string? startupScript = null);

    /// <summary>
    /// Run a script, optionally clearing state and skipping unchanged code.
    /// </summary>
    /// <param name="code">Script source code.</param>
    /// <param name="clearState">Clear script state before running.</param>
    /// <param name="skipIfUnchanged">Skip execution when code is unchanged.</param>
    Task RunScriptAsync(string code, bool clearState = true, bool skipIfUnchanged = true);

    /// <summary>
    /// Snapshot current engine state for inspection or UI.
    /// </summary>
    EngineStateSnapshot GetStateSnapshot();

    /// <summary>
    /// Suspend audio and sequencer processing.
    /// </summary>
    void Sleep();

    /// <summary>
    /// Resume audio and sequencer processing.
    /// </summary>
    void Wake();
}

/// <summary>
/// Script-friendly wrapper around engine, sequencer, and VST registry.
/// </summary>
public sealed class EngineScriptInterface : IEngineScriptInterface
{
    private readonly EngineScriptInterfaceOptions _options;
    private ScriptHost? _host;
    private bool _initialized;
    private bool _sleeping;

    /// <summary>
    /// Audio engine instance.
    /// </summary>
    public AudioEngine Engine { get; }

    /// <summary>
    /// Sequencer instance.
    /// </summary>
    public Sequencer Sequencer { get; }

    /// <summary>
    /// Script host used for execution and state.
    /// </summary>
    public ScriptHost Host => _host ?? throw new InvalidOperationException("Call StartupAsync before using the host.");

    /// <summary>
    /// VST3 registry populated during scanning (if enabled).
    /// </summary>
    public Vst3Registry? VstRegistry { get; private set; }

    /// <summary>
    /// Whether the engine is currently sleeping.
    /// </summary>
    public bool IsSleeping => _sleeping;

    /// <summary>
    /// Create a new interface with optional configuration.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    public EngineScriptInterface(EngineScriptInterfaceOptions? options = null)
    {
        _options = options ?? new EngineScriptInterfaceOptions();
        Engine = new AudioEngine(_options.SampleRate);
        Sequencer = new Sequencer();
    }

    /// <summary>
    /// Initialize the engine, scan for VST3, and run an optional startup script.
    /// </summary>
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

    /// <summary>
    /// Run a script, optionally clearing state and skipping unchanged code.
    /// </summary>
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

    /// <summary>
    /// Snapshot current engine state for inspection or UI.
    /// </summary>
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

    /// <summary>
    /// Suspend audio and sequencer processing.
    /// </summary>
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

    /// <summary>
    /// Resume audio and sequencer processing.
    /// </summary>
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

    /// <summary>
    /// Dispose engine resources.
    /// </summary>
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
