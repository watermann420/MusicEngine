// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal audio control fluent API.

using MusicEngine.Core;

namespace MusicEngine.Scripting.FluentApi;

public sealed class AudioControl
{
    private readonly ScriptGlobals _globals;
    public AudioControl(ScriptGlobals globals) => _globals = globals;

    public MasterAudioControl master => new MasterAudioControl(_globals.Engine);
    public AllChannelsControl all => new AllChannelsControl(_globals.Engine);
    public AudioChannelControl channel(int index) => new AudioChannelControl(_globals, index);
}

public sealed class AllChannelsControl
{
    private readonly AudioEngine _engine;
    public AllChannelsControl(AudioEngine engine) => _engine = engine;

    public void gain(float value) => _engine.SetAllChannelsGain(value);
    public void gain(double value) => gain((float)value);
}

public sealed class MasterAudioControl
{
    private readonly AudioEngine _engine;
    private RecordingSession? _lastRecording;
    public MasterAudioControl(AudioEngine engine) => _engine = engine;

    public void gain(float value) => _engine.SetAllChannelsGain(value);
    public void gain(double value) => gain((float)value);

    public void effect(IAudioEffect effect) => _engine.AddMasterEffect(effect);
    public void clearEffects() => _engine.ClearMasterEffects();

    public RecordingControl record => new RecordingControl(
        start: (path, format) => _lastRecording = _engine.StartMasterRecording(path, format),
        stop: session => _engine.StopMasterRecording(session ?? _lastRecording)
    );
}

public sealed class AudioChannelControl
{
    private readonly ScriptGlobals _globals;
    private readonly int _index;
    private RecordingSession? _lastRecording;

    public AudioChannelControl(ScriptGlobals globals, int index)
    {
        _globals = globals;
        _index = index < 1 ? 1 : index;
    }

    public void gain(float value) => _globals.Engine.SetChannelGain(_index, value);
    public void gain(double value) => gain((float)value);

    public void route(ISynth synth) => _globals.Engine.RouteToChannel(synth, _index);
    public void route(Pattern pattern)
    {
        if (pattern == null) return;
        foreach (var target in pattern.SynthTargets)
        {
            _globals.Engine.RouteToChannel(target, _index);
        }
    }

    public void effect(IAudioEffect effect) => _globals.Engine.AddChannelEffect(_index, effect);
    public void clearEffects() => _globals.Engine.ClearChannelEffects(_index);

    public RecordingControl record => new RecordingControl(
        start: (path, format) => _lastRecording = _globals.Engine.StartChannelRecording(_index, path, format),
        stop: session => _globals.Engine.StopChannelRecording(_index, session ?? _lastRecording)
    );
}

public sealed class RecordingControl
{
    private readonly Func<string, string?, RecordingSession> _start;
    private readonly Action<RecordingSession?> _stop;

    public RecordingControl(Func<string, string?, RecordingSession> start, Action<RecordingSession?> stop)
    {
        _start = start;
        _stop = stop;
    }

    public RecordingSession start(string path, string? format = null) => _start(path, format);
    public void stop() => _stop(null);
    public void stop(RecordingSession session) => _stop(session);
}
