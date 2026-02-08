// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Script host for test_script.csx.

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using MusicEngine.Core;
using MusicEngine.Effects.Vst;
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
    private string? _compiledFilePath;
    private string? _lastExecutedCode;
    private readonly string? _scriptFilePath;
    private readonly Dictionary<string, string> _moduleCodeCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ScriptLibrary _library;

    /// <summary>
    /// Create a script host bound to an engine and sequencer.
    /// </summary>
    /// <param name="engine">Audio engine instance.</param>
    /// <param name="sequencer">Sequencer instance.</param>
    /// <param name="vstRegistry">Optional VST registry.</param>
    public ScriptHost(AudioEngine engine, Sequencer sequencer, Vst3Registry? vstRegistry = null,
        string? scriptFilePath = null)
    {
        _engine = engine;
        _sequencer = sequencer;
        _vstRegistry = vstRegistry;
        _scriptFilePath = scriptFilePath;
        _library = new ScriptLibrary(this);
        _options = ScriptOptions.Default
            .WithReferences(typeof(AudioEngine).Assembly, typeof(NAudio.Wave.ISampleProvider).Assembly)
            .WithImports("System", "MusicEngine.Core", "MusicEngine.Instruments", "MusicEngine.Instruments.Modules",
                "MusicEngine.Vst", "MusicEngine.Effects.Audio", "MusicEngine.Effects.Midi",
                "MusicEngine.Effects.Vst", "MusicEngine.Effects.Modulation", "MusicEngine.Timing",
                "System.Collections.Generic");
        if (!string.IsNullOrWhiteSpace(_scriptFilePath))
        {
            _options = _options.WithFilePath(_scriptFilePath);
        }
    }

    /// <summary>
    /// Execute script code without clearing state.
    /// </summary>
    public async Task ExecuteScriptAsync(string code)
    {
        await RunScriptAsync(code, skipIfUnchanged: false, clearState: false, filePath: _scriptFilePath,
            cacheKey: null);
    }

    /// <summary>
    /// Execute script code only if it has changed.
    /// </summary>
    public async Task<bool> ExecuteScriptIfChangedAsync(string code)
    {
        return await RunScriptAsync(code, skipIfUnchanged: true, clearState: false, filePath: _scriptFilePath,
            cacheKey: null);
    }

    /// <summary>
    /// Clear state then execute script code.
    /// </summary>
    public async Task<bool> RefreshScriptAsync(string code, bool skipIfUnchanged = true)
    {
        return await RunScriptAsync(code, skipIfUnchanged, clearState: true, filePath: _scriptFilePath,
            cacheKey: null);
    }

    internal async Task<bool> ExecuteModuleAsync(string scriptName, bool skipIfUnchanged = true)
    {
        if (string.IsNullOrWhiteSpace(scriptName))
        {
            Console.WriteLine("Script Error: module name is empty.");
            return false;
        }

        var path = ResolveScriptPath(scriptName);
        if (path == null)
        {
            Console.WriteLine($"Script Error: module not found: {scriptName}");
            return false;
        }

        var code = File.ReadAllText(path);
        return await RunScriptAsync(code, skipIfUnchanged, clearState: false, filePath: path, cacheKey: path);
    }

    private async Task<bool> RunScriptAsync(string code, bool skipIfUnchanged, bool clearState, string? filePath,
        string? cacheKey)
    {
        if (skipIfUnchanged)
        {
            if (cacheKey == null && string.Equals(_lastExecutedCode, code, StringComparison.Ordinal))
            {
                return false;
            }

            if (cacheKey != null && _moduleCodeCache.TryGetValue(cacheKey, out var cached) &&
                string.Equals(cached, code, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (clearState)
        {
            ClearState();
        }

        var globals = GetOrCreateGlobals();
        var script = GetOrCompile(code, filePath);

        try
        {
            _globalsCache?.vst.BeginScriptRun();
            await script.RunAsync(globals);
            if (cacheKey == null)
            {
                _lastExecutedCode = code;
            }
            else
            {
                _moduleCodeCache[cacheKey] = code;
            }
            _globalsCache?.vst.PruneUnusedStates();
            return true;
        }
        catch (Exception ex)
        {
            if (ex is CompilationErrorException compilationError)
            {
                LogCompilationErrors(compilationError);
            }
            else
            {
                LogRuntimeError(ex);
            }
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
            VstRegistry = _vstRegistry,
            ScriptFilePath = _scriptFilePath
        };
        globals.SetLibrary(_library);
        if (_vstAccessCache != null)
        {
            _vstAccessCache.UpdateGlobals(globals);
            globals.SetVstAccess(_vstAccessCache);
        }
        _globalsCache = globals;
        return globals;
    }

    private Script<object> GetOrCompile(string code, string? filePath)
    {
        lock (_compileLock)
        {
            if (_compiledScript != null && string.Equals(_compiledCode, code, StringComparison.Ordinal) &&
                string.Equals(_compiledFilePath, filePath, StringComparison.Ordinal))
            {
                return _compiledScript;
            }

            var options = _options;
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                options = options.WithFilePath(filePath);
            }
            _compiledScript = CSharpScript.Create(code, options, typeof(ScriptGlobals));
            _compiledCode = code;
            _compiledFilePath = filePath;
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
        _moduleCodeCache.Clear();
        _library.Clear();
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
    /// Persist VST state for all cached instances.
    /// </summary>
    public void SaveVstState()
    {
        _globalsCache?.vst.SaveAllStates();
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

    internal string? ResolveScriptPath(string name)
    {
        var fileName = name.EndsWith(".csx", StringComparison.OrdinalIgnoreCase) ? name : $"{name}.csx";
        if (Path.IsPathRooted(fileName))
        {
            return File.Exists(fileName) ? fileName : null;
        }

        var baseDir = !string.IsNullOrWhiteSpace(_scriptFilePath)
            ? Path.GetDirectoryName(_scriptFilePath)
            : AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            return null;
        }

        var scriptsDir = Path.Combine(baseDir, "Scripts");
        var scriptPath = Path.Combine(scriptsDir, fileName);
        if (File.Exists(scriptPath))
        {
            return scriptPath;
        }

        scriptPath = Path.Combine(baseDir, fileName);
        return File.Exists(scriptPath) ? scriptPath : null;
    }

    private void LogCompilationErrors(CompilationErrorException error)
    {
        Console.WriteLine("Script Error: compilation failed.");
        foreach (var diagnostic in error.Diagnostics)
        {
            var lineSpan = diagnostic.Location.GetLineSpan();
            var line = lineSpan.StartLinePosition.Line + 1;
            var column = lineSpan.StartLinePosition.Character + 1;
            var path = string.IsNullOrWhiteSpace(lineSpan.Path) ? _scriptFilePath : lineSpan.Path;
            var location = string.IsNullOrWhiteSpace(path)
                ? $"line {line}, col {column}"
                : $"{path}:{line}:{column}";
            Console.WriteLine($"  {diagnostic.Severity} {diagnostic.Id}: {diagnostic.GetMessage()} ({location})");
        }
    }

    private void LogRuntimeError(Exception error)
    {
        Console.WriteLine($"Script Error: {error.GetType().Name}: {error.Message}");
        var location = TryFindStackLocation(error);
        if (!string.IsNullOrWhiteSpace(location))
        {
            Console.WriteLine($"  at {location}");
        }
    }

    private static string? TryFindStackLocation(Exception error)
    {
        if (string.IsNullOrWhiteSpace(error.StackTrace)) return null;
        var match = Regex.Match(error.StackTrace, @"in (.*):line (\d+)");
        if (!match.Success) return null;
        var path = match.Groups[1].Value.Trim();
        var line = match.Groups[2].Value.Trim();
        return string.IsNullOrWhiteSpace(path) ? null : $"{path}:{line}";
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
    /// Optional script file path for per-script storage.
    /// </summary>
    public string? ScriptFilePath { get; set; }
    /// <summary>
    /// Timing master from the sequencer.
    /// </summary>
    public TimingMaster Timing => Sequencer.Timing;

    private SimpleSynth? _synth;
    private ScriptLibrary? _library;

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
    /// Create and route a sampler instrument.
    /// </summary>
    public SamplerInstrument CreateSampler()
    {
        var sampler = new SamplerInstrument();
        Engine.AddSampleProvider(sampler);
        Host?.RegisterSynth(sampler);
        return sampler;
    }

    /// <summary>
    /// Create and route an audio deck.
    /// </summary>
    public AudioDeck CreateDeck(string name)
    {
        var deck = new AudioDeck(name);
        Engine.AddSampleProvider(deck);
        return deck;
    }

    /// <summary>
    /// Create a time master controller.
    /// </summary>
    public TimeMasterController CreateTimeMaster() => new TimeMasterController();

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
      /// Create a new VST3 instrument by name.
      /// </summary>
      public IVstInstrument CreateVst(string name) => vst.Create(name);
      /// <summary>
      /// Create a new VST3 instrument by name.
      /// </summary>
      public IVstInstrument Vst(string name) => CreateVst(name);
    /// <summary>
    /// Create and route a sampler instrument.
    /// </summary>
    public SamplerInstrument Sampler() => CreateSampler();
    /// <summary>
    /// Create and route an audio deck.
    /// </summary>
    public AudioDeck Deck(string name) => CreateDeck(name);
    /// <summary>
    /// Create a time master controller.
    /// </summary>
    public TimeMasterController TimeMaster() => CreateTimeMaster();
    /// <summary>
    /// Create a new VST3 effect by name.
    /// </summary>
    public Vst3Effect CreateVstEffect(string name) => vst.CreateEffect(name);
    /// <summary>
    /// Load an audio clip from disk.
    /// </summary>
    public AudioClip CreateAudioClip(string path) => new AudioClip(path);

    /// <summary>
    /// Shared script library (dynamic).
    /// </summary>
    public dynamic File => _library ??= new ScriptLibrary(Host ?? throw new InvalidOperationException("Host missing."));

    /// <summary>
    /// Shared script library (typed access).
    /// </summary>
    public ScriptLibrary Library => _library ??= new ScriptLibrary(Host ?? throw new InvalidOperationException("Host missing."));

    /// <summary>
    /// Load and run a module script by name.
    /// </summary>
    public Task<bool> Use(string name)
    {
        if (Host == null) return Task.FromResult(false);
        return Host.ExecuteModuleAsync(name);
    }

    /// <summary>
    /// Fluent audio control API (case-insensitive proxy).
    /// </summary>
    public dynamic audio => _audioProxy ??= new CaseInsensitiveProxy(_audioControl ??= new AudioControl(this));
    /// <summary>
    /// Fluent audio control API (case-insensitive proxy).
    /// </summary>
    public dynamic Audio => audio;
    /// <summary>
    /// Fluent audio control API (case-insensitive proxy).
    /// </summary>
    public dynamic AUDIO => audio;
    /// <summary>
    /// Fluent MIDI control API (case-insensitive proxy).
    /// </summary>
    public dynamic midi => _midiProxy ??= new CaseInsensitiveProxy(_midiControl ??= new MidiControl(this));
    /// <summary>
    /// Fluent MIDI control API (case-insensitive proxy).
    /// </summary>
    public dynamic Midi => midi;
    /// <summary>
    /// Fluent MIDI control API (case-insensitive proxy).
    /// </summary>
    public dynamic MIDI => midi;
    /// <summary>
    /// Dynamic VST access API.
    /// </summary>
    public dynamic vst => _vstAccess ??= new VstAccess(this);
    /// <summary>
    /// Case-insensitive root for scripting APIs.
    /// </summary>
    public dynamic Music => _musicProxy ??= new CaseInsensitiveProxy(this);
    /// <summary>
    /// Case-insensitive root for scripting APIs.
    /// </summary>
    public dynamic music => Music;
    /// <summary>
    /// Case-insensitive root for scripting APIs.
    /// </summary>
    public dynamic MUSIC => Music;
    /// <summary>
    /// Random source helper for scripts.
    /// </summary>
    public RandomSource Random { get; } = new RandomSource();

    /// <summary>
    /// Shared MIDI mapping helper for scripts.
    /// </summary>
    public MidiMap MidiMap => _midiMap ??= new MidiMap();
    /// <summary>
    /// Shared MIDI mapping helper for scripts.
    /// </summary>
    public MidiMap Map => MidiMap;

    private VstAccess? _vstAccess;
    private MidiMap? _midiMap;
    private AudioControl? _audioControl;
    private MidiControl? _midiControl;
    private CaseInsensitiveProxy? _audioProxy;
    private CaseInsensitiveProxy? _midiProxy;
    private CaseInsensitiveProxy? _musicProxy;

    internal VstAccess? VstAccessInstance => _vstAccess;

    internal void SetLibrary(ScriptLibrary library)
    {
        _library = library;
    }

    internal void SetVstAccess(VstAccess access)
    {
        _vstAccess = access;
    }

    internal void RouteMidi(int deviceIndex, ISynth synth) => Engine.RouteMidiInput(deviceIndex, synth);

    internal void RouteMidi(int deviceIndex, int channel, ISynth synth)
        => Engine.RouteMidiInput(deviceIndex, channel, synth);

    internal void MapControlAction(int deviceIndex, int controlId, Action<float> action)
        => Engine.MapControlAction(deviceIndex, controlId, action);

    internal void MapControlAction(int deviceIndex, int channel, int controlId, Action<float> action)
        => Engine.MapControlAction(deviceIndex, channel, controlId, action);
}
