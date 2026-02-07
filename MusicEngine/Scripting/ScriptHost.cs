// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Script host for test_script.csx.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using MusicEngine.Core;
using MusicEngine.Instruments;
using MusicEngine.Scripting.FluentApi;
using MusicEngine.Vst;
using MusicEngine.Timing;

namespace MusicEngine.Scripting;

/// <summary>
/// Script host for executing C# scripts against the engine.
/// </summary>
public sealed class ScriptHost
{
    /// <summary>
    /// When true, VST instances are disposed when clearing script state.
    /// </summary>
    public bool DisposeVstOnClear { get; set; } = false;
    private VstAccess? _vstAccessCache;
    private readonly AudioEngine _engine;
    private readonly Sequencer _sequencer;
    private readonly Vst3Registry? _vstRegistry;
    private readonly HashSet<ISynth> _activeSynths = new();
    private ScriptGlobals? _globalsCache;
    private readonly ScriptOptions _options;
    private readonly object _compileLock = new();
    private Script<object>? _compiledScript;
    private string? _compiledCode;
    private string? _lastExecutedCode;

    /// <summary>
    /// Create a script host bound to an engine and sequencer.
    /// </summary>
    /// <param name="engine">Audio engine instance.</param>
    /// <param name="sequencer">Sequencer instance.</param>
    /// <param name="vstRegistry">Optional VST registry.</param>
    public ScriptHost(AudioEngine engine, Sequencer sequencer, Vst3Registry? vstRegistry = null)
    {
        _engine = engine;
        _sequencer = sequencer;
        _vstRegistry = vstRegistry;
        _options = ScriptOptions.Default
            .WithReferences(typeof(AudioEngine).Assembly, typeof(NAudio.Wave.ISampleProvider).Assembly)
            .WithImports("System", "MusicEngine.Core", "MusicEngine.Instruments", "MusicEngine.Instruments.Modules",
                "MusicEngine.Vst", "MusicEngine.Timing", "System.Collections.Generic");
    }

    /// <summary>
    /// Execute script code without clearing state.
    /// </summary>
    public async Task ExecuteScriptAsync(string code)
    {
        await RunScriptAsync(code, skipIfUnchanged: false, clearState: false);
    }

    /// <summary>
    /// Execute script code only if it has changed.
    /// </summary>
    public async Task<bool> ExecuteScriptIfChangedAsync(string code)
    {
        return await RunScriptAsync(code, skipIfUnchanged: true, clearState: false);
    }

    /// <summary>
    /// Clear state then execute script code.
    /// </summary>
    public async Task<bool> RefreshScriptAsync(string code, bool skipIfUnchanged = true)
    {
        return await RunScriptAsync(code, skipIfUnchanged, clearState: true);
    }

    private async Task<bool> RunScriptAsync(string code, bool skipIfUnchanged, bool clearState)
    {
        if (skipIfUnchanged && string.Equals(_lastExecutedCode, code, StringComparison.Ordinal))
        {
            return false;
        }

        if (clearState)
        {
            ClearState();
        }

        var globals = GetOrCreateGlobals();
        var script = GetOrCompile(code);

        try
        {
            await script.RunAsync(globals);
            _lastExecutedCode = code;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Script Error: {ex.Message}");
            return false;
        }
    }

    private ScriptGlobals GetOrCreateGlobals()
    {
        if (_globalsCache != null) return _globalsCache;

        var globals = new ScriptGlobals
        {
            Engine = _engine,
            Sequencer = _sequencer,
            Host = this,
            VstRegistry = _vstRegistry
        };
        if (_vstAccessCache != null)
        {
            _vstAccessCache.UpdateGlobals(globals);
            globals.SetVstAccess(_vstAccessCache);
        }
        _globalsCache = globals;
        return globals;
    }

    private Script<object> GetOrCompile(string code)
    {
        lock (_compileLock)
        {
            if (_compiledScript != null && string.Equals(_compiledCode, code, StringComparison.Ordinal))
            {
                return _compiledScript;
            }

            _compiledScript = CSharpScript.Create(code, _options, typeof(ScriptGlobals));
            _compiledCode = code;
            return _compiledScript;
        }
    }

    /// <summary>
    /// Clear script state, routing, and mappings.
    /// </summary>
    public void ClearState()
    {
        bool resumeOutput = _engine.TrySuspendOutput();
        _sequencer.ClearPatterns();
        _engine.ClearMappings();
        _engine.ClearMixer();
        _activeSynths.Clear();
        if (_globalsCache != null)
        {
            if (DisposeVstOnClear)
            {
                _globalsCache.vst.KeepInstances = false;
                _globalsCache.vst.Clear();
                _vstAccessCache = null;
            }
            else
            {
                _globalsCache.vst.KeepInstances = true;
                _vstAccessCache = _globalsCache.VstAccessInstance;
            }
            _globalsCache = null;
        }
        if (resumeOutput)
        {
            _engine.ResumeOutput();
        }
    }

    /// <summary>
    /// Try to open a VST editor by name if already loaded.
    /// </summary>
    public bool TryOpenVstEditor(string name)
    {
        if (_globalsCache == null) return false;
        return _globalsCache.vst.TryOpenEditor(name);
    }

    /// <summary>
    /// Reset VST state in the script globals.
    /// </summary>
    public void ResetVstState()
    {
        _globalsCache?.vst.ResetState();
    }

    /// <summary>
    /// Mute or unmute the transport output.
    /// </summary>
    public void SetTransportMuted(bool muted)
    {
        _engine.SetTransportMuted(muted);
    }

    /// <summary>
    /// Enable or disable MIDI input.
    /// </summary>
    public void SetMidiEnabled(bool enabled)
    {
        _engine.SetMidiEnabled(enabled, sendAllNotesOff: false);
    }

    /// <summary>
    /// Start the sequencer if not running.
    /// </summary>
    public void StartSequencer()
    {
        if (!_sequencer.IsRunning)
        {
            _sequencer.Start();
        }
    }

    /// <summary>
    /// Stop the sequencer if running.
    /// </summary>
    public void StopSequencer()
    {
        if (_sequencer.IsRunning)
        {
            _sequencer.Stop();
        }
    }

    internal void RegisterSynth(ISynth synth)
    {
        if (synth == null) return;
        _activeSynths.Add(synth);
    }

    /// <summary>
    /// Send all-notes-off to active non-VST synths.
    /// </summary>
    public void AllNotesOff()
    {
        foreach (var synth in _activeSynths)
        {
            if (synth is MusicEngine.Vst.Vst3Instrument)
            {
                continue;
            }
            synth.AllNotesOff();
        }
    }
}

/// <summary>
/// Globals exposed to scripts for building synths, patterns, and routing.
/// </summary>
public sealed class ScriptGlobals
{
    /// <summary>
    /// Audio engine instance.
    /// </summary>
    public AudioEngine Engine { get; set; } = null!;
    /// <summary>
    /// Sequencer instance.
    /// </summary>
    public Sequencer Sequencer { get; set; } = null!;
    /// <summary>
    /// Script host instance.
    /// </summary>
    public ScriptHost? Host { get; set; }
    /// <summary>
    /// VST registry if scanning is enabled.
    /// </summary>
    public Vst3Registry? VstRegistry { get; set; }
    /// <summary>
    /// Timing master from the sequencer.
    /// </summary>
    public TimingMaster Timing => Sequencer.Timing;

    private SimpleSynth? _synth;

    /// <summary>
    /// Create and route a SimpleSynth instance.
    /// </summary>
    public SimpleSynth CreateSynth()
    {
        var synth = new SimpleSynth();
        Engine.AddSampleProvider(synth);
        Host?.RegisterSynth(synth);
        return synth;
    }

    /// <summary>
    /// Create and route a General MIDI instrument.
    /// </summary>
    public GeneralMidiInstrument CreateGeneralMidi()
    {
        var instrument = new GeneralMidiInstrument();
        Engine.AddSampleProvider(instrument);
        Host?.RegisterSynth(instrument);
        return instrument;
    }

    /// <summary>
    /// Create a pattern using the last created synth.
    /// </summary>
    public Pattern CreatePattern() => CreatePattern(_synth ??= CreateSynth());

    /// <summary>
    /// Create a pattern targeting a specific synth.
    /// </summary>
    public Pattern CreatePattern(ISynth synth)
    {
        var pattern = new Pattern(synth);
        pattern.Sequencer = Sequencer;
        Engine.RegisterPatternForEditor(pattern);
        return pattern;
    }

    /// <summary>
    /// Create or reuse a VST3 instrument by name.
    /// </summary>
    public Vst3Instrument CreateVst(string name) => vst.Get(name);
    /// <summary>
    /// Create or reuse a VST3 effect by name.
    /// </summary>
    public Vst3Effect CreateVstEffect(string name) => vst.GetEffect(name);
    /// <summary>
    /// Load an audio clip from disk.
    /// </summary>
    public AudioClip CreateAudioClip(string path) => new AudioClip(path);

    /// <summary>
    /// Fluent audio control API.
    /// </summary>
    public AudioControl audio => new AudioControl(this);
    /// <summary>
    /// Fluent MIDI control API.
    /// </summary>
    public MidiControl midi => new MidiControl(this);
    /// <summary>
    /// Dynamic VST access API.
    /// </summary>
    public dynamic vst => _vstAccess ??= new VstAccess(this);
    /// <summary>
    /// Random source helper for scripts.
    /// </summary>
    public RandomSource Random { get; } = new RandomSource();

    private VstAccess? _vstAccess;

    internal VstAccess? VstAccessInstance => _vstAccess;

    internal void SetVstAccess(VstAccess access)
    {
        _vstAccess = access;
    }

    internal void RouteMidi(int deviceIndex, ISynth synth) => Engine.RouteMidiInput(deviceIndex, synth);

    internal void MapControlAction(int deviceIndex, int controlId, Action<float> action)
        => Engine.MapControlAction(deviceIndex, controlId, action);
}
