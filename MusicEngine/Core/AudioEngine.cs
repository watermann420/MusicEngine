// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal audio engine for script-driven playback.

using System;
using System.Buffers;
using System.Collections.Generic;
using MusicEngine.Effects.Audio;
using NAudio.CoreAudioApi;
using NAudio.Midi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicEngine.Core;

/// <summary>
/// Minimal audio engine for script-driven playback, routing, and recording.
/// </summary>
public sealed class AudioEngine : IDisposable
{
    private readonly WaveFormat _waveFormat;
    private readonly MixingSampleProvider _mixer;
    private readonly AudioEffectChain _masterEffects;
    private readonly VolumeSampleProvider _masterVolume;
    private readonly VolumeSampleProvider _transportVolume;
    private readonly RecordingTap _masterTap;
    private readonly ISampleProvider _masterChain;
    private readonly Dictionary<int, AudioChannel> _channels = new();
    private readonly Dictionary<ISampleProvider, AudioChannel> _routing = new();
    private readonly Dictionary<ISampleProvider, ISampleProvider> _normalizedProviders = new();
    private readonly Dictionary<(int Source, int Target), ChannelSend> _channelSends = new();
    private readonly object _routingLock = new();
    private readonly Dictionary<int, MidiIn> _midiInputs = new();
    private readonly List<AudioInput> _audioInputs = new();
    private readonly MidiRouter _midiRouter = new();
    private readonly List<AudioVirtualOutput> _masterVirtualOutputs = new();
    private readonly Dictionary<int, List<AudioVirtualOutput>> _channelVirtualOutputs = new();
    private readonly object _virtualOutputLock = new();
    private IWavePlayer? _output;
    private bool _initialized;
    private bool _outputRunning;
    private bool _editorModeEnabled;

    /// <summary>
    /// Raised when a pattern note is triggered in editor mode.
    /// </summary>
    public event Action<PatternNoteEventInfo>? EditorPatternNote;

    /// <summary>
    /// Raised when a MIDI note is received in editor mode.
    /// </summary>
    public event Action<MidiNoteEventInfo>? EditorMidiNote;

    /// <summary>
    /// Raised when a MIDI device becomes active in editor mode.
    /// </summary>
    public event Action<int>? EditorMidiDeviceActive;

    /// <summary>
    /// Create a new engine with an optional sample rate override.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz. Uses <see cref="Settings.SampleRate"/> when null.</param>
    public AudioEngine(int? sampleRate = null)
    {
        var rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);
        _mixer = new MixingSampleProvider(_waveFormat) { ReadFully = true };
        _masterEffects = new AudioEffectChain(_mixer, _waveFormat);
        _masterVolume = new VolumeSampleProvider(_masterEffects) { Volume = 1.0f };
        _transportVolume = new VolumeSampleProvider(_masterVolume) { Volume = 1.0f };
        var dcBlock = new DcBlockingSampleProvider(_transportVolume, 1f, _waveFormat.SampleRate);
        var limiter = new LimiterSampleProvider(dcBlock, 0.95f, _waveFormat.SampleRate, attackMs: 2f, releaseMs: 60f);
        ISampleProvider master = new SoftClipSampleProvider(limiter, 0.99f);
        if (Settings.OutputBitDepth > 0 && Settings.OutputBitDepth < 32)
        {
            master = new BitDepthSampleProvider(master, Settings.OutputBitDepth);
        }
        _masterChain = master;
        _masterTap = new RecordingTap(_masterChain);
        _masterTap.SamplesAvailable += OnMasterSamples;
        _midiRouter.EditorMidiNoteEvent += info => EditorMidiNote?.Invoke(info);
        _midiRouter.EditorMidiDeviceActive += deviceIndex => EditorMidiDeviceActive?.Invoke(deviceIndex);
    }

    /// <summary>
    /// Whether editor mode is currently enabled.
    /// </summary>
    public bool EditorModeEnabled => _editorModeEnabled;

    /// <summary>
    /// Initialize the audio output and start playback.
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;
        _output = new WaveOutEvent
        {
            DesiredLatency = 100,
            NumberOfBuffers = 3
        };
        _output.Init(_masterTap);
        _output.Play();
        _initialized = true;
        _outputRunning = true;
    }

    /// <summary>
    /// Stop audio output without tearing down routing state.
    /// </summary>
    public void SuspendOutput()
    {
        if (_output == null) return;
        _output.Stop();
        _outputRunning = false;
    }

    /// <summary>
    /// Try to suspend output if it is currently running.
    /// </summary>
    /// <returns>True when output was running and has been stopped.</returns>
    public bool TrySuspendOutput()
    {
        if (_output == null || !_outputRunning) return false;
        _output.Stop();
        _outputRunning = false;
        return true;
    }

    /// <summary>
    /// Resume audio output, initializing if needed.
    /// </summary>
    public void ResumeOutput()
    {
        if (!_initialized)
        {
            Initialize();
            return;
        }

        if (_output == null) return;
        if (_outputRunning) return;
        _output.Play();
        _outputRunning = true;
    }

    /// <summary>
    /// Route a provider to channel 1.
    /// </summary>
    /// <param name="provider">Sample provider to route.</param>
    public void AddSampleProvider(ISampleProvider provider)
    {
        RouteToChannel(provider, 1);
    }

    /// <summary>
    /// Route a provider into a specific channel mix.
    /// </summary>
    /// <param name="provider">Sample provider to route.</param>
    /// <param name="channelIndex">1-based channel index.</param>
    public void RouteToChannel(ISampleProvider provider, int channelIndex)
    {
        if (provider == null) return;
        if (channelIndex < 1) channelIndex = 1;

        var normalized = GetOrCreateNormalized(provider);
        lock (_routingLock)
        {
            if (_routing.TryGetValue(provider, out var existing))
            {
                existing.Mixer.RemoveMixerInput(normalized);
                _routing.Remove(provider);
            }

            var channel = GetOrCreateChannel(channelIndex);
            channel.Mixer.AddMixerInput(normalized);
            _routing[provider] = channel;
        }
    }

    /// <summary>
    /// Set master gain for all channels.
    /// </summary>
    /// <param name="value">Gain in [0, 1].</param>
    public void SetAllChannelsGain(float value)
    {
        MasterGain = value;
    }

    /// <summary>
    /// Master output gain in [0, 1].
    /// </summary>
    public float MasterGain
    {
        get => _masterVolume.Volume;
        set => _masterVolume.Volume = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Mute or unmute the transport output.
    /// </summary>
    /// <param name="muted">True to mute, false to restore.</param>
    public void SetTransportMuted(bool muted)
    {
        _transportVolume.Volume = muted ? 0f : 1f;
    }

    /// <summary>
    /// Enable or disable MIDI input processing.
    /// </summary>
    /// <param name="enabled">True to enable MIDI input.</param>
    public void SetMidiEnabled(bool enabled)
    {
        _midiRouter.SetEnabled(enabled);
    }

    /// <summary>
    /// Enable or disable MIDI input processing with optional all-notes-off.
    /// </summary>
    /// <param name="enabled">True to enable MIDI input.</param>
    /// <param name="sendAllNotesOff">Send all-notes-off when disabling.</param>
    public void SetMidiEnabled(bool enabled, bool sendAllNotesOff)
    {
        _midiRouter.SetEnabled(enabled, sendAllNotesOff);
    }

    /// <summary>
    /// Toggle editor mode for patterns and MIDI routing.
    /// </summary>
    /// <param name="enabled">True to enable editor mode.</param>
    public void SetEditorMode(bool enabled)
    {
        _editorModeEnabled = enabled;
        Pattern.SetEditorMode(enabled);
        _midiRouter.SetEditorMode(enabled);
    }

    /// <summary>
    /// Start recording the master output.
    /// </summary>
    /// <param name="path">Target file path.</param>
    /// <param name="format">Optional format override.</param>
    /// <returns>Recording session instance.</returns>
    public RecordingSession StartMasterRecording(string path, string? format = null, RecordingOptions? options = null)
    {
        return _masterTap.StartRecording(path, format, options);
    }

    /// <summary>
    /// Stop master recording.
    /// </summary>
    /// <param name="session">Specific session to stop (optional).</param>
    public void StopMasterRecording(RecordingSession? session = null)
    {
        _masterTap.StopRecording(session);
    }

    /// <summary>
    /// List available output devices for routing (render endpoints).
    /// </summary>
    public IReadOnlyList<AudioOutputDeviceInfo> ListOutputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        var list = new List<AudioOutputDeviceInfo>();
        for (int i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            var format = device.AudioClient.MixFormat;
            list.Add(new AudioOutputDeviceInfo(i, device.ID, device.FriendlyName, format.Channels, format.SampleRate));
        }
        return list;
    }

    /// <summary>
    /// Start a virtual output from the master chain to a render device (e.g. VB-CABLE).
    /// </summary>
    public bool StartMasterVirtualOutput(int deviceIndex, int latencyMs = 80)
    {
        var device = TryGetOutputDevice(deviceIndex);
        if (device == null) return false;
        AddVirtualOutput(_masterVirtualOutputs, device, latencyMs, 0);
        return true;
    }

    /// <summary>
    /// Start a virtual output from the master chain to a render device with channel offset.
    /// </summary>
    public bool StartMasterVirtualOutput(int deviceIndex, int outputChannelOffset, int latencyMs = 80)
    {
        var device = TryGetOutputDevice(deviceIndex);
        if (device == null) return false;
        AddVirtualOutput(_masterVirtualOutputs, device, latencyMs, outputChannelOffset);
        return true;
    }

    /// <summary>
    /// Start a virtual output from the master chain to a render device by name.
    /// </summary>
    public bool StartMasterVirtualOutput(string deviceName, int latencyMs = 80)
    {
        var device = TryGetOutputDevice(deviceName);
        if (device == null) return false;
        AddVirtualOutput(_masterVirtualOutputs, device, latencyMs, 0);
        return true;
    }

    /// <summary>
    /// Start a virtual output from the master chain to a render device by name with channel offset.
    /// </summary>
    public bool StartMasterVirtualOutput(string deviceName, int outputChannelOffset, int latencyMs = 80)
    {
        var device = TryGetOutputDevice(deviceName);
        if (device == null) return false;
        AddVirtualOutput(_masterVirtualOutputs, device, latencyMs, outputChannelOffset);
        return true;
    }

    /// <summary>
    /// Stop all virtual outputs on the master chain.
    /// </summary>
    public void StopMasterVirtualOutputs()
    {
        lock (_virtualOutputLock)
        {
            foreach (var output in _masterVirtualOutputs)
            {
                output.Dispose();
            }
            _masterVirtualOutputs.Clear();
        }
    }

    /// <summary>
    /// Route a channel output into another channel (send).
    /// </summary>
    /// <param name="sourceIndex">Source channel (1-based).</param>
    /// <param name="targetIndex">Target channel (1-based).</param>
    /// <param name="gain">Send gain in [0, 1].</param>
    public void RouteChannelToChannel(int sourceIndex, int targetIndex, float gain = 1f)
    {
        if (sourceIndex < 1) sourceIndex = 1;
        if (targetIndex < 1) targetIndex = 1;
        if (sourceIndex == targetIndex) return;

        var source = GetOrCreateChannel(sourceIndex);
        var target = GetOrCreateChannel(targetIndex);

        var key = (sourceIndex, targetIndex);
        lock (_routingLock)
        {
            if (_channelSends.TryGetValue(key, out var existing))
            {
                target.Mixer.RemoveMixerInput(existing.Volume);
                source.Tap.SamplesAvailable -= existing.Handler;
                _channelSends.Remove(key);
            }

            var buffer = new BufferedWaveProvider(_waveFormat)
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(2)
            };
            var sampleProvider = buffer.ToSampleProvider();
            var volume = new VolumeSampleProvider(sampleProvider)
            {
                Volume = Math.Clamp(gain, 0f, 1f)
            };

            Action<float[], int, int> handler = (data, offset, count) =>
            {
                WriteToBuffer(buffer, data, offset, count);
            };

            source.Tap.SamplesAvailable += handler;
            target.Mixer.AddMixerInput(volume);

            _channelSends[key] = new ChannelSend(sourceIndex, targetIndex, buffer, volume, handler);
        }
    }

    /// <summary>
    /// Update gain for an existing channel send.
    /// </summary>
    public void SetChannelSendGain(int sourceIndex, int targetIndex, float gain)
    {
        var key = (sourceIndex, targetIndex);
        lock (_routingLock)
        {
            if (_channelSends.TryGetValue(key, out var send))
            {
                send.Volume.Volume = Math.Clamp(gain, 0f, 1f);
            }
        }
    }

    /// <summary>
    /// Remove a channel send.
    /// </summary>
    public void UnrouteChannelFromChannel(int sourceIndex, int targetIndex)
    {
        var key = (sourceIndex, targetIndex);
        lock (_routingLock)
        {
            if (!_channelSends.TryGetValue(key, out var send)) return;
            if (_channels.TryGetValue(targetIndex, out var target))
            {
                target.Mixer.RemoveMixerInput(send.Volume);
            }
            if (_channels.TryGetValue(sourceIndex, out var source))
            {
                source.Tap.SamplesAvailable -= send.Handler;
            }
            _channelSends.Remove(key);
        }
    }

    /// <summary>
    /// Clear all sends for a source channel.
    /// </summary>
    public void ClearChannelSends(int sourceIndex)
    {
        if (sourceIndex < 1) sourceIndex = 1;
        List<(int Source, int Target)> keys = new();
        lock (_routingLock)
        {
            foreach (var key in _channelSends.Keys)
            {
                if (key.Source == sourceIndex)
                {
                    keys.Add(key);
                }
            }
        }

        foreach (var key in keys)
        {
            UnrouteChannelFromChannel(key.Source, key.Target);
        }
    }

    /// <summary>
    /// Start a virtual output from a channel to a render device.
    /// </summary>
    public bool StartChannelVirtualOutput(int channelIndex, int deviceIndex, int latencyMs = 80)
    {
        if (channelIndex < 1) channelIndex = 1;
        var device = TryGetOutputDevice(deviceIndex);
        if (device == null) return false;
        var outputs = GetOrCreateChannelVirtualOutputs(channelIndex);
        AddVirtualOutput(outputs, device, latencyMs, 0);
        return true;
    }

    /// <summary>
    /// Start a virtual output from a channel to a render device with channel offset.
    /// </summary>
    public bool StartChannelVirtualOutput(int channelIndex, int deviceIndex, int outputChannelOffset, int latencyMs = 80)
    {
        if (channelIndex < 1) channelIndex = 1;
        var device = TryGetOutputDevice(deviceIndex);
        if (device == null) return false;
        var outputs = GetOrCreateChannelVirtualOutputs(channelIndex);
        AddVirtualOutput(outputs, device, latencyMs, outputChannelOffset);
        return true;
    }

    /// <summary>
    /// Start a virtual output from a channel to a render device by name.
    /// </summary>
    public bool StartChannelVirtualOutput(int channelIndex, string deviceName, int latencyMs = 80)
    {
        if (channelIndex < 1) channelIndex = 1;
        var device = TryGetOutputDevice(deviceName);
        if (device == null) return false;
        var outputs = GetOrCreateChannelVirtualOutputs(channelIndex);
        AddVirtualOutput(outputs, device, latencyMs, 0);
        return true;
    }

    /// <summary>
    /// Start a virtual output from a channel to a render device by name with channel offset.
    /// </summary>
    public bool StartChannelVirtualOutput(int channelIndex, string deviceName, int outputChannelOffset, int latencyMs = 80)
    {
        if (channelIndex < 1) channelIndex = 1;
        var device = TryGetOutputDevice(deviceName);
        if (device == null) return false;
        var outputs = GetOrCreateChannelVirtualOutputs(channelIndex);
        AddVirtualOutput(outputs, device, latencyMs, outputChannelOffset);
        return true;
    }

    /// <summary>
    /// Stop all virtual outputs on a channel.
    /// </summary>
    public void StopChannelVirtualOutputs(int channelIndex)
    {
        if (channelIndex < 1) channelIndex = 1;
        lock (_virtualOutputLock)
        {
            if (_channelVirtualOutputs.TryGetValue(channelIndex, out var outputs))
            {
                foreach (var output in outputs)
                {
                    output.Dispose();
                }
                outputs.Clear();
            }
        }
    }

    /// <summary>
    /// Add an effect to the master chain.
    /// </summary>
    /// <param name="effect">Effect to add.</param>
    public void AddMasterEffect(IAudioEffect effect)
    {
        _masterEffects.AddEffect(effect);
    }

    /// <summary>
    /// Remove all master effects.
    /// </summary>
    public void ClearMasterEffects()
    {
        _masterEffects.ClearEffects();
    }

    /// <summary>
    /// Set the gain for a specific channel.
    /// </summary>
    /// <param name="index">1-based channel index.</param>
    /// <param name="value">Gain in [0, 1].</param>
    public void SetChannelGain(int index, float value)
    {
        if (index < 1) return;
        if (_channels.TryGetValue(index, out var channel))
        {
            channel.Volume.Volume = Math.Clamp(value, 0f, 1f);
        }
    }

    /// <summary>
    /// Add an effect to a channel chain.
    /// </summary>
    /// <param name="index">1-based channel index.</param>
    /// <param name="effect">Effect to add.</param>
    public void AddChannelEffect(int index, IAudioEffect effect)
    {
        if (index < 1) return;
        var channel = GetOrCreateChannel(index);
        channel.Effects.AddEffect(effect);
    }

    /// <summary>
    /// Clear all effects on a channel.
    /// </summary>
    /// <param name="index">1-based channel index.</param>
    public void ClearChannelEffects(int index)
    {
        if (index < 1) return;
        if (_channels.TryGetValue(index, out var channel))
        {
            channel.Effects.ClearEffects();
        }
    }

    /// <summary>
    /// Start recording a specific channel.
    /// </summary>
    /// <param name="index">1-based channel index.</param>
    /// <param name="path">Target file path.</param>
    /// <param name="format">Optional format override.</param>
    /// <returns>Recording session instance.</returns>
    public RecordingSession StartChannelRecording(int index, string path, string? format = null, RecordingOptions? options = null)
    {
        if (index < 1) index = 1;
        var channel = GetOrCreateChannel(index);
        return channel.Tap.StartRecording(path, format, options);
    }

    /// <summary>
    /// Stop channel recording.
    /// </summary>
    /// <param name="index">1-based channel index.</param>
    /// <param name="session">Specific session to stop (optional).</param>
    public void StopChannelRecording(int index, RecordingSession? session = null)
    {
        if (index < 1) index = 1;
        if (_channels.TryGetValue(index, out var channel))
        {
            channel.Tap.StopRecording(session);
        }
    }

    /// <summary>
    /// Route a MIDI input device to a synth.
    /// </summary>
    /// <param name="deviceIndex">MIDI device index.</param>
    /// <param name="synth">Target synth.</param>
    public void RouteMidiInput(int deviceIndex, ISynth synth)
    {
        RouteMidiInput(deviceIndex, -1, synth);
    }

    /// <summary>
    /// Route a MIDI input device channel to a synth.
    /// </summary>
    /// <param name="deviceIndex">MIDI device index.</param>
    /// <param name="channel">MIDI channel (0-15) or -1 for all.</param>
    /// <param name="synth">Target synth.</param>
    public void RouteMidiInput(int deviceIndex, int channel, ISynth synth)
    {
        _midiRouter.Route(deviceIndex, channel, synth);
        EnsureMidiInput(deviceIndex);
    }

    /// <summary>
    /// Map a MIDI controller to a custom action.
    /// </summary>
    /// <param name="deviceIndex">MIDI device index.</param>
    /// <param name="controlId">Control change ID.</param>
    /// <param name="action">Action invoked with normalized value.</param>
    public void MapControlAction(int deviceIndex, int controlId, Action<float> action)
    {
        MapControlAction(deviceIndex, -1, controlId, action);
    }

    /// <summary>
    /// Map a MIDI controller to a custom action for a specific channel.
    /// </summary>
    /// <param name="deviceIndex">MIDI device index.</param>
    /// <param name="channel">MIDI channel (0-15) or -1 for all.</param>
    /// <param name="controlId">Control change ID.</param>
    /// <param name="action">Action invoked with normalized value.</param>
    public void MapControlAction(int deviceIndex, int channel, int controlId, Action<float> action)
    {
        _midiRouter.MapControlAction(deviceIndex, channel, controlId, action);
        EnsureMidiInput(deviceIndex);
    }

    /// <summary>
    /// Clear all MIDI control mappings and routes.
    /// </summary>
    public void ClearMappings()
    {
        _midiRouter.Clear();
    }

    /// <summary>
    /// Snapshot of recent MIDI device activity.
    /// </summary>
    /// <returns>List of activity snapshots.</returns>
    public IReadOnlyList<MidiDeviceActivitySnapshot> GetMidiActivitySnapshot()
    {
        return _midiRouter.GetActivitySnapshot();
    }

    /// <summary>
    /// Remove all routed providers and reset effects/recording state.
    /// </summary>
    public void ClearMixer()
    {
        _mixer.RemoveAllMixerInputs();
        _channels.Clear();
        _routing.Clear();
        _normalizedProviders.Clear();
        _masterEffects.ClearEffects();
        _masterTap.StopAll();
    }

    /// <summary>
    /// Register a pattern so its editor note events are forwarded by the engine.
    /// </summary>
    /// <param name="pattern">Pattern to register for editor events.</param>
    public void RegisterPatternForEditor(Pattern pattern)
    {
        if (pattern == null) return;
        pattern.EditorNoteEvent += OnPatternNoteEvent;
    }

    private void OnPatternNoteEvent(PatternNoteEventInfo info)
    {
        var handler = EditorPatternNote;
        handler?.Invoke(info);
    }

    private void EnsureMidiInput(int deviceIndex)
    {
        if (_midiInputs.ContainsKey(deviceIndex)) return;
        if (deviceIndex < 0 || deviceIndex >= MidiIn.NumberOfDevices) return;

        var midiIn = new MidiIn(deviceIndex);
        midiIn.MessageReceived += (_, args) => HandleMidiMessage(deviceIndex, args);
        midiIn.ErrorReceived += (_, _) => { };
        midiIn.Start();
        _midiInputs[deviceIndex] = midiIn;
    }

    private void HandleMidiMessage(int deviceIndex, MidiInMessageEventArgs args)
    {
        _midiRouter.HandleMidiMessage(deviceIndex, args);
    }

    /// <summary>
    /// Dispose the audio engine and release resources.
    /// </summary>
    public void Dispose()
    {
        SetEditorMode(false);
        ClearAllChannelSends();
        foreach (var midiIn in _midiInputs.Values)
        {
            midiIn.Stop();
            midiIn.Dispose();
        }
        _midiInputs.Clear();

        foreach (var input in _audioInputs)
        {
            input.Dispose();
        }
        _audioInputs.Clear();

        StopMasterVirtualOutputs();
        foreach (var entry in _channelVirtualOutputs.Values)
        {
            foreach (var output in entry)
            {
                output.Dispose();
            }
            entry.Clear();
        }
        _channelVirtualOutputs.Clear();

        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _outputRunning = false;

        MidiOutPool.DisposeAll();
    }

    private AudioChannel GetOrCreateChannel(int index)
    {
        if (_channels.TryGetValue(index, out var existing)) return existing;

        var mixer = new MixingSampleProvider(_waveFormat) { ReadFully = true };
        var effects = new AudioEffectChain(mixer, _waveFormat);
        var volume = new VolumeSampleProvider(effects) { Volume = 1.0f };
        var tap = new RecordingTap(volume);
        tap.SamplesAvailable += (buffer, offset, count) => OnChannelSamples(index, buffer, offset, count);
        _mixer.AddMixerInput(tap);

        var channel = new AudioChannel(index, mixer, effects, volume, tap);
        _channels[index] = channel;
        return channel;
    }

    private static void WriteToBuffer(BufferedWaveProvider buffer, float[] samples, int offset, int count)
    {
        int byteCount = count * sizeof(float);
        var rented = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            Buffer.BlockCopy(samples, offset * sizeof(float), rented, 0, byteCount);
            buffer.AddSamples(rented, 0, byteCount);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private void ClearAllChannelSends()
    {
        List<(int Source, int Target)> keys;
        lock (_routingLock)
        {
            keys = new List<(int, int)>(_channelSends.Keys);
        }

        foreach (var key in keys)
        {
            UnrouteChannelFromChannel(key.Source, key.Target);
        }
    }

    private ISampleProvider GetOrCreateNormalized(ISampleProvider provider)
    {
        if (_normalizedProviders.TryGetValue(provider, out var normalized)) return normalized;

        var current = provider;
        if (current.WaveFormat.SampleRate != _waveFormat.SampleRate)
        {
            current = new WdlResamplingSampleProvider(current, _waveFormat.SampleRate);
        }

        if (current.WaveFormat.Channels != _waveFormat.Channels)
        {
            current = current.WaveFormat.Channels == 1
                ? new MonoToStereoSampleProvider(current)
                : new StereoToMonoSampleProvider(current);
        }

        _normalizedProviders[provider] = current;
        return current;
    }

    private sealed class AudioChannel
    {
        public int Index { get; }
        public MixingSampleProvider Mixer { get; }
        public AudioEffectChain Effects { get; }
        public VolumeSampleProvider Volume { get; }
        public RecordingTap Tap { get; }

        public AudioChannel(int index, MixingSampleProvider mixer, AudioEffectChain effects, VolumeSampleProvider volume,
            RecordingTap tap)
        {
            Index = index;
            Mixer = mixer;
            Effects = effects;
            Volume = volume;
            Tap = tap;
        }
    }

    private sealed class ChannelSend
    {
        public int Source { get; }
        public int Target { get; }
        public BufferedWaveProvider Buffer { get; }
        public VolumeSampleProvider Volume { get; }
        public Action<float[], int, int> Handler { get; }

        public ChannelSend(int source, int target, BufferedWaveProvider buffer, VolumeSampleProvider volume,
            Action<float[], int, int> handler)
        {
            Source = source;
            Target = target;
            Buffer = buffer;
            Volume = volume;
            Handler = handler;
        }
    }

    /// <summary>
    /// List available audio input devices (capture endpoints).
    /// </summary>
    public IReadOnlyList<AudioInputDeviceInfo> ListInputDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        var list = new List<AudioInputDeviceInfo>();
        for (int i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            list.Add(new AudioInputDeviceInfo(i, device.ID, device.FriendlyName));
        }
        return list;
    }

    /// <summary>
    /// Create a live audio input from a capture device index.
    /// </summary>
    public AudioInput CreateInput(int deviceIndex)
    {
        var device = TryGetInputDevice(deviceIndex);
        if (device == null)
        {
            throw new InvalidOperationException($"Audio input device not found: {deviceIndex}");
        }

        var input = new AudioInput(device, deviceIndex);
        _audioInputs.Add(input);
        return input;
    }

    /// <summary>
    /// Create a live audio input from a capture device name.
    /// </summary>
    public AudioInput CreateInput(string deviceName)
    {
        var device = TryGetInputDevice(deviceName);
        if (device == null)
        {
            throw new InvalidOperationException($"Audio input device not found: {deviceName}");
        }

        var input = new AudioInput(device, -1);
        _audioInputs.Add(input);
        return input;
    }

    private void OnMasterSamples(float[] buffer, int offset, int count)
    {
        List<AudioVirtualOutput> outputs;
        lock (_virtualOutputLock)
        {
            if (_masterVirtualOutputs.Count == 0) return;
            outputs = new List<AudioVirtualOutput>(_masterVirtualOutputs);
        }

        foreach (var output in outputs)
        {
            output.Push(buffer, offset, count);
        }
    }

    private void OnChannelSamples(int channelIndex, float[] buffer, int offset, int count)
    {
        List<AudioVirtualOutput>? outputs;
        lock (_virtualOutputLock)
        {
            if (!_channelVirtualOutputs.TryGetValue(channelIndex, out outputs) || outputs.Count == 0)
            {
                return;
            }

            outputs = new List<AudioVirtualOutput>(outputs);
        }

        foreach (var output in outputs)
        {
            output.Push(buffer, offset, count);
        }
    }

    private List<AudioVirtualOutput> GetOrCreateChannelVirtualOutputs(int channelIndex)
    {
        lock (_virtualOutputLock)
        {
            if (_channelVirtualOutputs.TryGetValue(channelIndex, out var outputs))
            {
                return outputs;
            }

            outputs = new List<AudioVirtualOutput>();
            _channelVirtualOutputs[channelIndex] = outputs;
            return outputs;
        }
    }

    private void AddVirtualOutput(List<AudioVirtualOutput> outputs, MMDevice device, int latencyMs, int outputChannelOffset)
    {
        lock (_virtualOutputLock)
        {
            foreach (var existing in outputs)
            {
                if (string.Equals(existing.DeviceId, device.ID, StringComparison.OrdinalIgnoreCase) &&
                    existing.OutputChannelOffset == outputChannelOffset)
                {
                    device.Dispose();
                    return;
                }
            }

            outputs.Add(new AudioVirtualOutput(device, _waveFormat, latencyMs, outputChannelOffset));
        }
    }

    private static MMDevice? TryGetOutputDevice(int index)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        if (index < 0 || index >= devices.Count) return null;
        return devices[index];
    }

    private static MMDevice? TryGetOutputDevice(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        foreach (var device in devices)
        {
            if (device.FriendlyName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return device;
            }
        }
        return null;
    }

    public readonly record struct AudioOutputDeviceInfo(int Index, string Id, string Name, int Channels, int SampleRate);
    public readonly record struct AudioInputDeviceInfo(int Index, string Id, string Name);

    private static MMDevice? TryGetInputDevice(int index)
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        if (index < 0 || index >= devices.Count) return null;
        return devices[index];
    }

    private static MMDevice? TryGetInputDevice(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        foreach (var device in devices)
        {
            if (device.FriendlyName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return device;
            }
        }
        return null;
    }
}
