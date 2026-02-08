// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal audio control fluent API.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MusicEngine.Core;
using MusicEngine.Effects.Audio;
using MusicEngine.Effects.Vst;

namespace MusicEngine.Scripting.FluentApi;

/// <summary>
/// Fluent API entry point for audio routing and controls.
/// </summary>
public sealed class AudioControl
{
    private readonly ScriptGlobals _globals;
    public AudioControl(ScriptGlobals globals) => _globals = globals;

    /// <summary>
    /// Master channel controls.
    /// </summary>
    public MasterAudioControl master => new MasterAudioControl(_globals.Engine);
    /// <summary>
    /// Master channel controls.
    /// </summary>
    public MasterAudioControl Master => new MasterAudioControl(_globals.Engine);
    /// <summary>
    /// Controls that apply to all channels.
    /// </summary>
    public AllChannelsControl all => new AllChannelsControl(_globals.Engine);
    /// <summary>
    /// Controls that apply to all channels.
    /// </summary>
    public AllChannelsControl All => new AllChannelsControl(_globals.Engine);
    /// <summary>
    /// Access a specific channel control by index.
    /// </summary>
    public AudioChannelControl channel(int index) => new AudioChannelControl(_globals, index);
    /// <summary>
    /// Access a specific channel control by index.
    /// </summary>
    public AudioChannelControl Channel(int index) => new AudioChannelControl(_globals, index);

    /// <summary>
    /// Create or access a channel control by index.
    /// </summary>
    public AudioChannelControl createchannel(int index) => new AudioChannelControl(_globals, index);

    /// <summary>
    /// Create or access a channel control by index.
    /// </summary>
    public AudioChannelControl CreateChannel(int index) => new AudioChannelControl(_globals, index);

    /// <summary>
    /// Output device utilities.
    /// </summary>
    public OutputDeviceControl output => new OutputDeviceControl(_globals.Engine);
    /// <summary>
    /// Output device utilities.
    /// </summary>
    public OutputDeviceControl Output => new OutputDeviceControl(_globals.Engine);

    /// <summary>
    /// Input device utilities.
    /// </summary>
    public InputDeviceControl input => new InputDeviceControl(_globals.Engine);
    /// <summary>
    /// Input device utilities.
    /// </summary>
    public InputDeviceControl Input => new InputDeviceControl(_globals.Engine);
}

/// <summary>
/// Controls that apply to all channels.
/// </summary>
public sealed class AllChannelsControl
{
    private readonly AudioEngine _engine;
    public AllChannelsControl(AudioEngine engine) => _engine = engine;

    /// <summary>
    /// Set gain for all channels.
    /// </summary>
    public void gain(float value) => _engine.SetAllChannelsGain(value);
    /// <summary>
    /// Set gain for all channels.
    /// </summary>
    public void Gain(float value) => _engine.SetAllChannelsGain(value);
    /// <summary>
    /// Set gain for all channels (double overload).
    /// </summary>
    public void gain(double value) => gain((float)value);
    /// <summary>
    /// Set gain for all channels (double overload).
    /// </summary>
    public void Gain(double value) => gain(value);
}

/// <summary>
/// Controls for the master output.
/// </summary>
public sealed class MasterAudioControl
{
    private readonly AudioEngine _engine;
    private RecordingSession? _lastRecording;
    public MasterAudioControl(AudioEngine engine) => _engine = engine;

    /// <summary>
    /// Set master gain.
    /// </summary>
    public void gain(float value) => _engine.SetAllChannelsGain(value);
    /// <summary>
    /// Set master gain.
    /// </summary>
    public void Gain(float value) => _engine.SetAllChannelsGain(value);
    /// <summary>
    /// Set master gain (double overload).
    /// </summary>
    public void gain(double value) => gain((float)value);
    /// <summary>
    /// Set master gain (double overload).
    /// </summary>
    public void Gain(double value) => gain(value);

    /// <summary>
    /// Add an effect to the master chain.
    /// </summary>
    public void effect(IAudioEffect effect) => _engine.AddMasterEffect(effect);
    /// <summary>
    /// Add an effect to the master chain.
    /// </summary>
    public void Effect(IAudioEffect effect) => _engine.AddMasterEffect(effect);
    /// <summary>
    /// Clear all master effects.
    /// </summary>
    public void clearEffects() => _engine.ClearMasterEffects();
    /// <summary>
    /// Clear all master effects.
    /// </summary>
    public void ClearEffects() => _engine.ClearMasterEffects();

    /// <summary>
    /// Recording controls for the master output.
    /// </summary>
    public RecordingControl record => new RecordingControl(
        start: (path, format, options) => _lastRecording = _engine.StartMasterRecording(path, format, options),
        stop: session => _engine.StopMasterRecording(session ?? _lastRecording),
        defaultPath: () => RecordingControl.DefaultPath("master")
    );
    /// <summary>
    /// Recording controls for the master output.
    /// </summary>
    public RecordingControl Record => new RecordingControl(
        start: (path, format, options) => _lastRecording = _engine.StartMasterRecording(path, format, options),
        stop: session => _engine.StopMasterRecording(session ?? _lastRecording),
        defaultPath: () => RecordingControl.DefaultPath("master")
    );

    /// <summary>
    /// Route master output to a virtual device (e.g. VB-CABLE).
    /// </summary>
    public bool virtualout(int deviceIndex) => _engine.StartMasterVirtualOutput(deviceIndex);
    /// <summary>
    /// Route master output to a virtual device (e.g. VB-CABLE).
    /// </summary>
    public bool VirtualOut(int deviceIndex) => _engine.StartMasterVirtualOutput(deviceIndex);
    /// <summary>
    /// Route master output to a virtual device (e.g. VB-CABLE).
    /// </summary>
    public bool virtualout(string deviceName) => _engine.StartMasterVirtualOutput(deviceName);
    /// <summary>
    /// Route master output to a virtual device (e.g. VB-CABLE).
    /// </summary>
    public bool VirtualOut(string deviceName) => _engine.StartMasterVirtualOutput(deviceName);

    /// <summary>
    /// Stop all master virtual outputs.
    /// </summary>
    public void stopvirtualout() => _engine.StopMasterVirtualOutputs();
    /// <summary>
    /// Stop all master virtual outputs.
    /// </summary>
    public void StopVirtualOut() => _engine.StopMasterVirtualOutputs();
}

/// <summary>
/// Controls for a specific audio channel.
/// </summary>
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

    /// <summary>
    /// Set gain for the channel.
    /// </summary>
    public void gain(float value) => _globals.Engine.SetChannelGain(_index, value);
    /// <summary>
    /// Set gain for the channel.
    /// </summary>
    public void Gain(float value) => _globals.Engine.SetChannelGain(_index, value);
    /// <summary>
    /// Set gain for the channel (double overload).
    /// </summary>
    public void gain(double value) => gain((float)value);
    /// <summary>
    /// Set gain for the channel (double overload).
    /// </summary>
    public void Gain(double value) => gain(value);

    /// <summary>
    /// Route a synth to this channel.
    /// </summary>
    public void route(ISynth synth) => _globals.Engine.RouteToChannel(synth, _index);
    /// <summary>
    /// Route a synth to this channel.
    /// </summary>
    public void Route(ISynth synth) => _globals.Engine.RouteToChannel(synth, _index);
    /// <summary>
    /// Route all synth targets from a pattern to this channel.
    /// </summary>
    public void route(Pattern pattern)
    {
        if (pattern == null) return;
        foreach (var target in pattern.SynthTargets)
        {
            _globals.Engine.RouteToChannel(target, _index);
        }
    }
    /// <summary>
    /// Route all synth targets from a pattern to this channel.
    /// </summary>
    public void Route(Pattern pattern) => route(pattern);

    /// <summary>
    /// Add an effect to this channel.
    /// </summary>
    public void effect(IAudioEffect effect) => _globals.Engine.AddChannelEffect(_index, effect);
    /// <summary>
    /// Add an effect to this channel.
    /// </summary>
    public void Effect(IAudioEffect effect) => _globals.Engine.AddChannelEffect(_index, effect);
    /// <summary>
    /// Clear all effects on this channel.
    /// </summary>
    public void clearEffects() => _globals.Engine.ClearChannelEffects(_index);
    /// <summary>
    /// Clear all effects on this channel.
    /// </summary>
    public void ClearEffects() => _globals.Engine.ClearChannelEffects(_index);

    /// <summary>
    /// Recording controls for this channel.
    /// </summary>
    public RecordingControl record => new RecordingControl(
        start: (path, format, options) => _lastRecording = _globals.Engine.StartChannelRecording(_index, path, format, options),
        stop: session => _globals.Engine.StopChannelRecording(_index, session ?? _lastRecording),
        defaultPath: () => RecordingControl.DefaultPath($"ch{_index}")
    );
    /// <summary>
    /// Recording controls for this channel.
    /// </summary>
    public RecordingControl Record => new RecordingControl(
        start: (path, format, options) => _lastRecording = _globals.Engine.StartChannelRecording(_index, path, format, options),
        stop: session => _globals.Engine.StopChannelRecording(_index, session ?? _lastRecording),
        defaultPath: () => RecordingControl.DefaultPath($"ch{_index}")
    );

    /// <summary>
    /// Route this channel to a virtual device (e.g. VB-CABLE).
    /// </summary>
    public bool virtualout(int deviceIndex) => _globals.Engine.StartChannelVirtualOutput(_index, deviceIndex);
    /// <summary>
    /// Route this channel to a virtual device (e.g. VB-CABLE).
    /// </summary>
    public bool VirtualOut(int deviceIndex) => _globals.Engine.StartChannelVirtualOutput(_index, deviceIndex);
    /// <summary>
    /// Route this channel to a virtual device (e.g. VB-CABLE).
    /// </summary>
    public bool virtualout(string deviceName) => _globals.Engine.StartChannelVirtualOutput(_index, deviceName);
    /// <summary>
    /// Route this channel to a virtual device (e.g. VB-CABLE).
    /// </summary>
    public bool VirtualOut(string deviceName) => _globals.Engine.StartChannelVirtualOutput(_index, deviceName);

    /// <summary>
    /// Stop all virtual outputs for this channel.
    /// </summary>
    public void stopvirtualout() => _globals.Engine.StopChannelVirtualOutputs(_index);
    /// <summary>
    /// Stop all virtual outputs for this channel.
    /// </summary>
    public void StopVirtualOut() => _globals.Engine.StopChannelVirtualOutputs(_index);

    /// <summary>
    /// Create and add a VST3 effect to this channel.
    /// </summary>
    public Vst3Effect vsteffect(string name)
    {
        var effect = _globals.CreateVstEffect(name);
        _globals.Engine.AddChannelEffect(_index, effect);
        return effect;
    }

    /// <summary>
    /// Create and add a VST3 effect to this channel.
    /// </summary>
    public Vst3Effect vsteffekt(string name) => vsteffect(name);

    /// <summary>
    /// Create and add a VST3 effect to this channel.
    /// </summary>
    public Vst3Effect VstEffect(string name) => vsteffect(name);
}

/// <summary>
/// Output device helper for listing devices.
/// </summary>
public sealed class OutputDeviceControl
{
    private readonly AudioEngine _engine;
    public OutputDeviceControl(AudioEngine engine) => _engine = engine;

    /// <summary>
    /// List available output devices.
    /// </summary>
    public string[] list()
    {
        var devices = _engine.ListOutputDevices();
        var list = new string[devices.Count];
        for (int i = 0; i < devices.Count; i++)
        {
            list[i] = $"{devices[i].Index}: {devices[i].Name}";
        }
        return list;
    }

    /// <summary>
    /// List available output devices.
    /// </summary>
    public string[] List() => list();
}

/// <summary>
/// Input device helper for listing devices.
/// </summary>
public sealed class InputDeviceControl
{
    private readonly AudioEngine _engine;
    public InputDeviceControl(AudioEngine engine) => _engine = engine;

    /// <summary>
    /// List available input devices.
    /// </summary>
    public string[] list()
    {
        var devices = _engine.ListInputDevices();
        var list = new string[devices.Count];
        for (int i = 0; i < devices.Count; i++)
        {
            list[i] = $"{devices[i].Index}: {devices[i].Name}";
        }
        return list;
    }

    /// <summary>
    /// List available input devices.
    /// </summary>
    public string[] List() => list();
}

/// <summary>
/// Recording controls used by fluent API.
/// </summary>
public sealed class RecordingControl
{
    private readonly Func<string, string?, RecordingOptions?, RecordingSession> _start;
    private readonly Action<RecordingSession?> _stop;
    private readonly Func<string> _defaultPath;
    private string? _lastPath;
    private string? _lastFormat;
    private bool _stopForRender;
    private CancellationTokenSource? _oneShotCts;

    public RecordingControl(Func<string, string?, RecordingOptions?, RecordingSession> start, Action<RecordingSession?> stop, Func<string> defaultPath)
    {
        _start = start;
        _stop = stop;
        _defaultPath = defaultPath;
    }

    /// <summary>
    /// Start a new recording session.
    /// </summary>
    public RecordingSession start() => start(_defaultPath(), null);
    /// <summary>
    /// Start a new recording session.
    /// </summary>
    public RecordingSession Start() => start();
    /// <summary>
    /// Start a new recording session.
    /// </summary>
    public RecordingSession start(string path, string? format = null)
    {
        var resolved = PreparePath(path, format, out var normalizedFormat);
        _lastFormat = normalizedFormat;
        _lastPath = resolved;
        var session = _start(resolved, normalizedFormat, BuildOptions());
        ScheduleOneShotStop();
        return session;
    }
    /// <summary>
    /// Start a new recording session.
    /// </summary>
    public RecordingSession Start(string path, string? format = null) => start(path, format);
    /// <summary>
    /// Stop the last recording session.
    /// </summary>
    public void stop()
    {
        _stop(null);
        CancelOneShot();
        if (!_stopForRender && Loop && !OneShot)
        {
            if (!string.IsNullOrWhiteSpace(_lastPath))
            {
                start(_lastPath, _lastFormat);
            }
        }
    }
    /// <summary>
    /// Stop a specific recording session.
    /// </summary>
    public void stop(RecordingSession session) => _stop(session);

    /// <summary>
    /// Stop the last recording session.
    /// </summary>
    public void Stop() => stop();
    /// <summary>
    /// Stop a specific recording session.
    /// </summary>
    public void Stop(RecordingSession session) => stop(session);

    /// <summary>
    /// Finalize the last recording session (alias for Stop).
    /// </summary>
    public void render()
    {
        _stopForRender = true;
        stop();
        _stopForRender = false;
    }
    /// <summary>
    /// Finalize the last recording session (alias for Stop).
    /// </summary>
    public void Render() => render();

    /// <summary>
    /// Render a one-shot recording to a specific path.
    /// </summary>
    public void render(string path, double? seconds = null, string? format = null)
    {
        var previousOneShot = OneShot;
        var previousDuration = DurationSeconds;
        OneShot = true;
        if (seconds.HasValue)
        {
            DurationSeconds = seconds.Value;
        }
        start(path, format ?? DefaultFormat);
        OneShot = previousOneShot;
        DurationSeconds = previousDuration;
    }
    /// <summary>
    /// Render a one-shot recording to a specific path.
    /// </summary>
    public void Render(string path, double? seconds = null, string? format = null) => render(path, seconds, format);

    /// <summary>
    /// Render a one-shot recording using the default path.
    /// </summary>
    public void render(double seconds)
    {
        render(_defaultPath(), seconds, DefaultFormat);
    }
    /// <summary>
    /// Render a one-shot recording using the default path.
    /// </summary>
    public void Render(double seconds) => render(seconds);

    /// <summary>
    /// Delete the last rendered file (stops if active).
    /// </summary>
    public void delete()
    {
        stop();
        TryDelete(_lastPath);
        _lastPath = null;
    }
    /// <summary>
    /// Delete the last rendered file (stops if active).
    /// </summary>
    public void Delete() => delete();
    /// <summary>
    /// Delete the last rendered file (stops if active).
    /// </summary>
    public void del() => delete();
    /// <summary>
    /// Delete the last rendered file (stops if active).
    /// </summary>
    public void Del() => delete();

    /// <summary>
    /// If true, overwrite existing files on Start.
    /// </summary>
    public bool Overwrite { get; set; }

    /// <summary>
    /// Alias for Overwrite.
    /// </summary>
    public bool Override
    {
        get => Overwrite;
        set => Overwrite = value;
    }

    /// <summary>
    /// If true, automatically restart recording after Stop.
    /// </summary>
    public bool Loop { get; set; }

    /// <summary>
    /// If true, Stop happens automatically after DurationSeconds.
    /// </summary>
    public bool OneShot { get; set; }

    /// <summary>
    /// Duration for one-shot recording (seconds). Ignored if <= 0.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Default format used when no format/extension is supplied.
    /// </summary>
    public string? DefaultFormat { get; set; }

    /// <summary>
    /// Target sample rate in Hz (optional).
    /// </summary>
    public int? SampleRate { get; set; }

    /// <summary>
    /// Target channel count (optional).
    /// </summary>
    public int? Channels { get; set; }

    /// <summary>
    /// Target WAV bit depth (16, 24, 32).
    /// </summary>
    public int? WavBitDepth { get; set; }

    /// <summary>
    /// Target bitrate in kbps for compressed formats (mp3/aac/wma).
    /// </summary>
    public int? BitRateKbps { get; set; }

    /// <summary>
    /// Resampler quality (1..60) when resampling is used.
    /// </summary>
    public int? ResamplerQuality { get; set; }

    /// <summary>
    /// Enable or disable overwrite behavior.
    /// </summary>
    public void OverwriteMode(bool enable = true) => Overwrite = enable;

    /// <summary>
    /// Enable or disable loop behavior.
    /// </summary>
    public void LoopMode(bool enable = true) => Loop = enable;

    /// <summary>
    /// Last rendered path (if any).
    /// </summary>
    public string? LastPath => _lastPath;

    internal static string DefaultPath(string tag)
    {
        var folder = Path.Combine(Environment.CurrentDirectory, "Recordings");
        Directory.CreateDirectory(folder);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(folder, $"record_{tag}_{stamp}.wav");
    }

    private string PreparePath(string path, string? format, out string? normalizedFormat)
    {
        normalizedFormat = format ?? DefaultFormat;
        if (!string.IsNullOrWhiteSpace(format))
        {
            normalizedFormat = format.Trim().TrimStart('.').ToLowerInvariant();
            path = EnsureExtension(path, FormatToExtension(normalizedFormat));
        }
        else if (!string.IsNullOrWhiteSpace(DefaultFormat))
        {
            normalizedFormat = DefaultFormat.Trim().TrimStart('.').ToLowerInvariant();
            path = EnsureExtension(path, FormatToExtension(normalizedFormat));
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory);

        if (Overwrite)
        {
            TryDelete(fullPath);
            return fullPath;
        }

        if (!File.Exists(fullPath)) return fullPath;
        return NextAvailablePath(fullPath);
    }

    private static string EnsureExtension(string path, string format)
    {
        var ext = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(ext))
        {
            return $"{path}.{format}";
        }
        return path;
    }

    private static string FormatToExtension(string format)
    {
        if (format.StartsWith("wav", StringComparison.OrdinalIgnoreCase)) return "wav";
        return format;
    }

    private static string NextAvailablePath(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        return path;
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }
    }

    private RecordingOptions BuildOptions()
    {
        return new RecordingOptions
        {
            SampleRate = SampleRate,
            Channels = Channels,
            WavBitDepth = WavBitDepth,
            BitRateKbps = BitRateKbps,
            ResamplerQuality = ResamplerQuality
        };
    }

    private void ScheduleOneShotStop()
    {
        CancelOneShot();
        if (!OneShot || DurationSeconds <= 0) return;
        _oneShotCts = new CancellationTokenSource();
        var token = _oneShotCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(DurationSeconds), token).ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                {
                    render();
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }, token);
    }

    private void CancelOneShot()
    {
        if (_oneShotCts == null) return;
        _oneShotCts.Cancel();
        _oneShotCts.Dispose();
        _oneShotCts = null;
    }
}
