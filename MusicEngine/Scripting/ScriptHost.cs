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

public sealed class ScriptHost
{
    private readonly AudioEngine _engine;
    private readonly Sequencer _sequencer;
    private readonly Vst3Registry? _vstRegistry;
    private ScriptGlobals? _globalsCache;
    private readonly ScriptOptions _options;
    private readonly object _compileLock = new();
    private Script<object>? _compiledScript;
    private string? _compiledCode;
    private string? _lastExecutedCode;

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

    public async Task ExecuteScriptAsync(string code)
    {
        await RunScriptAsync(code, skipIfUnchanged: false, clearState: false);
    }

    public async Task<bool> ExecuteScriptIfChangedAsync(string code)
    {
        return await RunScriptAsync(code, skipIfUnchanged: true, clearState: false);
    }

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

    public void ClearState()
    {
        _sequencer.ClearPatterns();
        _engine.ClearMappings();
        _engine.ClearMixer();
        _globalsCache?.vst.Clear();
        _globalsCache = null;
    }

    public bool TryOpenVstEditor(string name)
    {
        if (_globalsCache == null) return false;
        return _globalsCache.vst.TryOpenEditor(name);
    }
}

public sealed class ScriptGlobals
{
    public AudioEngine Engine { get; set; } = null!;
    public Sequencer Sequencer { get; set; } = null!;
    public ScriptHost? Host { get; set; }
    public Vst3Registry? VstRegistry { get; set; }
    public TimingMaster Timing => Sequencer.Timing;

    private SimpleSynth? _synth;

    public SimpleSynth CreateSynth()
    {
        var synth = new SimpleSynth();
        Engine.AddSampleProvider(synth);
        return synth;
    }

    public GeneralMidiInstrument CreateGeneralMidi()
    {
        var instrument = new GeneralMidiInstrument();
        Engine.AddSampleProvider(instrument);
        return instrument;
    }

    public Pattern CreatePattern() => CreatePattern(_synth ??= CreateSynth());

    public Pattern CreatePattern(ISynth synth)
    {
        var pattern = new Pattern(synth);
        pattern.Sequencer = Sequencer;
        return pattern;
    }

    public Vst3Instrument CreateVst(string name) => vst.Get(name);
    public Vst3Effect CreateVstEffect(string name) => vst.GetEffect(name);
    public AudioClip CreateAudioClip(string path) => new AudioClip(path);

    public AudioControl audio => new AudioControl(this);
    public MidiControl midi => new MidiControl(this);
    public dynamic vst => _vstAccess ??= new VstAccess(this);
    public RandomSource Random { get; } = new RandomSource();

    private VstAccess? _vstAccess;

    internal void RouteMidi(int deviceIndex, ISynth synth) => Engine.RouteMidiInput(deviceIndex, synth);

    internal void MapControlAction(int deviceIndex, int controlId, Action<float> action)
        => Engine.MapControlAction(deviceIndex, controlId, action);
}
