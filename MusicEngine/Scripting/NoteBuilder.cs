// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Fluent note trigger helper.

using System;
using System.Dynamic;
using System.Threading.Tasks;
using MusicEngine.Core;
using MusicEngine.Vst;

namespace MusicEngine.Scripting;

/// <summary>
/// Fluent note trigger helper.
/// </summary>
public sealed class NoteBuilder
{
    private readonly ScriptGlobals _globals;
    private readonly int _note;

    /// <summary>
    /// Create a note builder for a specific MIDI note.
    /// </summary>
    public NoteBuilder(ScriptGlobals globals, int note)
    {
        _globals = globals;
        _note = MidiValidation.ValidateNote(note);
    }

    /// <summary>
    /// Bind this note to a target synth.
    /// </summary>
    public NoteBinding To(ISynth synth) => new NoteBinding(_globals, _note, synth);

    /// <summary>
    /// Bind this note to multiple target synths.
    /// </summary>
    public NoteBinding To(params ISynth[] synths) => new NoteBinding(_globals, _note, synths);

    /// <summary>
    /// Dynamic target resolver (Note(60).to.vital).
    /// </summary>
    public dynamic to => new NoteTargetProxy(_globals, _note);

    /// <summary>
    /// Dynamic target resolver (Note(60).TO.vital).
    /// </summary>
    public dynamic TO => to;
}

/// <summary>
/// A bound note trigger for a specific synth.
/// </summary>
public sealed class NoteBinding
{
    private readonly ScriptGlobals? _globals;
    private readonly int _note;
    private readonly List<ISynth> _targets = new();
    private readonly NoteLoop _loop;

    /// <summary>
    /// Create a binding for a note and target synths.
    /// </summary>
    public NoteBinding(ScriptGlobals? globals, int note, params ISynth[] targets)
    {
        _globals = globals;
        _note = MidiValidation.ValidateNote(note);
        if (targets == null || targets.Length == 0)
        {
            throw new ArgumentException("At least one target synth is required.", nameof(targets));
        }

        foreach (var target in targets)
        {
            AddTarget(target);
        }

        _loop = new NoteLoop(this);
    }

    /// <summary>
    /// Bound MIDI note number.
    /// </summary>
    public int Note => _note;

    /// <summary>
    /// Current targets for this note binding.
    /// </summary>
    public IReadOnlyList<ISynth> Targets => _targets;

    /// <summary>
    /// Loop helper for repeated triggering.
    /// </summary>
    public NoteLoop Loop => _loop;

    /// <summary>
    /// Bind this note to another target synth.
    /// </summary>
    public NoteBinding To(ISynth synth)
    {
        AddTarget(synth);
        return this;
    }

    /// <summary>
    /// Bind this note to multiple target synths.
    /// </summary>
    public NoteBinding To(params ISynth[] synths)
    {
        if (synths == null) return this;
        foreach (var synth in synths)
        {
            AddTarget(synth);
        }
        return this;
    }

    /// <summary>
    /// Dynamic target resolver (Note(60).to.vital.to.piano).
    /// </summary>
    public dynamic to => new NoteChainProxy(this, _globals);

    /// <summary>
    /// Dynamic target resolver (Note(60).TO.vital).
    /// </summary>
    public dynamic TO => to;

    /// <summary>
    /// Trigger note on.
    /// </summary>
    public void On(int velocity = 100)
    {
        velocity = MidiValidation.ValidateVelocity(velocity);
        foreach (var target in _targets)
        {
            target.NoteOn(_note, velocity);
        }
    }

    /// <summary>
    /// Trigger note off.
    /// </summary>
    public void Off()
    {
        foreach (var target in _targets)
        {
            target.NoteOff(_note);
        }
    }

    /// <summary>
    /// Trigger note on (alias).
    /// </summary>
    public void on(int velocity = 100) => On(velocity);

    /// <summary>
    /// Trigger note off (alias).
    /// </summary>
    public void off() => Off();

    /// <summary>
    /// Trigger note on (alias).
    /// </summary>
    public void Trigger(int velocity = 100) => On(velocity);

    /// <summary>
    /// Trigger note on (alias).
    /// </summary>
    public void trigger(int velocity = 100) => On(velocity);

    /// <summary>
    /// Play a note for a duration in milliseconds.
    /// </summary>
    public async Task Play(double duration = 250, int velocity = 100)
    {
        duration = Math.Clamp(duration, 1.0, 60000.0);
        velocity = MidiValidation.ValidateVelocity(velocity);
        foreach (var target in _targets)
        {
            target.NoteOn(_note, velocity);
        }
        await Task.Delay(TimeSpan.FromMilliseconds(duration));
        foreach (var target in _targets)
        {
            target.NoteOff(_note);
        }
    }

    /// <summary>
    /// Play a note for a duration in milliseconds (alias).
    /// </summary>
    public Task Paly(double duration = 250, int velocity = 100) => Play(duration, velocity);

    /// <summary>
    /// Play a note for a duration in milliseconds (alias).
    /// </summary>
    public Task play(double duration = 250, int velocity = 100) => Play(duration, velocity);

    /// <summary>
    /// Play a note for a duration in milliseconds (alias).
    /// </summary>
    public Task paly(double duration = 250, int velocity = 100) => Play(duration, velocity);

    private void AddTarget(ISynth synth)
    {
        if (synth == null) throw new ArgumentNullException(nameof(synth));
        if (!_targets.Contains(synth))
        {
            _targets.Add(synth);
        }
    }
}

internal sealed class NoteTargetProxy : DynamicObject
{
    private readonly ScriptGlobals _globals;
    private readonly int _note;

    /// <summary>
    /// Create a dynamic target resolver for a note.
    /// </summary>
    public NoteTargetProxy(ScriptGlobals globals, int note)
    {
        _globals = globals;
        _note = note;
    }

    /// <summary>
    /// Resolve a member name into a synth binding.
    /// </summary>
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        result = Resolve(binder.Name);
        return result != null;
    }

    private NoteBinding? Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        if (_globals.Host != null && _globals.Host.TryResolveVstInstrument(name, out var vst))
        {
            return new NoteBinding(_globals, _note, vst);
        }

        var fromLibrary = _globals.Library.Get(name);
        if (fromLibrary is ISynth synth)
        {
            return new NoteBinding(_globals, _note, synth);
        }

        return null;
    }
}

internal sealed class NoteChainProxy : DynamicObject
{
    private readonly NoteBinding _binding;
    private readonly ScriptGlobals? _globals;

    /// <summary>
    /// Create a chainable dynamic target resolver.
    /// </summary>
    public NoteChainProxy(NoteBinding binding, ScriptGlobals? globals)
    {
        _binding = binding;
        _globals = globals;
    }

    /// <summary>
    /// Resolve a member name into another target in the chain.
    /// </summary>
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        result = Resolve(binder.Name);
        return result != null;
    }

    private NoteBinding? Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        if (_globals?.Host != null && _globals.Host.TryResolveVstInstrument(name, out var vst))
        {
            _binding.To(vst);
            return _binding;
        }

        var fromLibrary = _globals?.Library.Get(name);
        if (fromLibrary is ISynth synth)
        {
            _binding.To(synth);
            return _binding;
        }

        return null;
    }
}

/// <summary>
/// Loop helper for repeated note triggering.
/// </summary>
public sealed class NoteLoop
{
    private readonly NoteBinding _binding;
    private readonly object _lock = new();
    private System.Threading.Timer? _timer;
    private int _bpm = 120;
    private int _gateMs = 120;

    /// <summary>
    /// Create a loop helper for a bound note.
    /// </summary>
    public NoteLoop(NoteBinding binding)
    {
        _binding = binding;
    }

    /// <summary>
    /// Current BPM for the loop.
    /// </summary>
    public int Bpm => _bpm;
    /// <summary>
    /// Current gate length in milliseconds.
    /// </summary>
    public int GateMs => _gateMs;
    /// <summary>
    /// True while the loop is active.
    /// </summary>
    public bool Active => _timer != null;

    /// <summary>
    /// Set BPM and restart the loop.
    /// </summary>
    public NoteLoop Speed(int bpm)
    {
        _bpm = Math.Clamp(bpm, 1, 1200);
        Start();
        return this;
    }

    /// <summary>
    /// Set gate length in milliseconds.
    /// </summary>
    public NoteLoop Gate(int ms)
    {
        _gateMs = Math.Clamp(ms, 1, 60000);
        return this;
    }

    /// <summary>
    /// Start looping.
    /// </summary>
    public NoteLoop Start()
    {
        lock (_lock)
        {
            var intervalMs = Math.Max(1, (int)Math.Round(60000.0 / _bpm));
            _timer?.Dispose();
            _timer = new System.Threading.Timer(_ => TriggerOnce(intervalMs), null, 0, intervalMs);
        }
        return this;
    }

    /// <summary>
    /// Stop looping.
    /// </summary>
    public NoteLoop Stop()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
        }
        return this;
    }

    private async void TriggerOnce(int intervalMs)
    {
        var gate = Math.Clamp(_gateMs, 1, intervalMs);
        _binding.On();
        await Task.Delay(TimeSpan.FromMilliseconds(gate));
        _binding.Off();
    }
}
