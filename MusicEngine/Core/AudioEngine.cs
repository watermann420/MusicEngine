// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal audio engine for script-driven playback.

using System;
using System.Collections.Generic;
using NAudio.Midi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicEngine.Core;

public sealed class AudioEngine : IDisposable
{
    private readonly WaveFormat _waveFormat;
    private readonly MixingSampleProvider _mixer;
    private readonly AudioEffectChain _masterEffects;
    private readonly VolumeSampleProvider _masterVolume;
    private readonly RecordingTap _masterTap;
    private readonly ISampleProvider _masterChain;
    private readonly Dictionary<int, AudioChannel> _channels = new();
    private readonly Dictionary<ISampleProvider, AudioChannel> _routing = new();
    private readonly Dictionary<ISampleProvider, ISampleProvider> _normalizedProviders = new();
    private readonly object _routingLock = new();
    private readonly Dictionary<int, MidiIn> _midiInputs = new();
    private readonly MidiRouter _midiRouter = new();
    private IWavePlayer? _output;
    private bool _initialized;

    public AudioEngine(int? sampleRate = null)
    {
        var rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);
        _mixer = new MixingSampleProvider(_waveFormat) { ReadFully = true };
        _masterEffects = new AudioEffectChain(_mixer, _waveFormat);
        _masterVolume = new VolumeSampleProvider(_masterEffects) { Volume = 1.0f };
        var dcBlock = new DcBlockingSampleProvider(_masterVolume, 20f, _waveFormat.SampleRate);
        var limiter = new LimiterSampleProvider(dcBlock, 0.95f, _waveFormat.SampleRate, attackMs: 2f, releaseMs: 60f);
        _masterChain = new SoftClipSampleProvider(limiter, 0.99f);
        _masterTap = new RecordingTap(_masterChain);
    }

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
    }

    public void AddSampleProvider(ISampleProvider provider)
    {
        RouteToChannel(provider, 1);
    }

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

    public void SetAllChannelsGain(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        _masterVolume.Volume = value;
    }

    public RecordingSession StartMasterRecording(string path, string? format = null)
    {
        return _masterTap.StartRecording(path, format);
    }

    public void StopMasterRecording(RecordingSession? session = null)
    {
        _masterTap.StopRecording(session);
    }

    public void AddMasterEffect(IAudioEffect effect)
    {
        _masterEffects.AddEffect(effect);
    }

    public void ClearMasterEffects()
    {
        _masterEffects.ClearEffects();
    }

    public void SetChannelGain(int index, float value)
    {
        if (index < 1) return;
        if (_channels.TryGetValue(index, out var channel))
        {
            channel.Volume.Volume = Math.Clamp(value, 0f, 1f);
        }
    }

    public void AddChannelEffect(int index, IAudioEffect effect)
    {
        if (index < 1) return;
        var channel = GetOrCreateChannel(index);
        channel.Effects.AddEffect(effect);
    }

    public void ClearChannelEffects(int index)
    {
        if (index < 1) return;
        if (_channels.TryGetValue(index, out var channel))
        {
            channel.Effects.ClearEffects();
        }
    }

    public RecordingSession StartChannelRecording(int index, string path, string? format = null)
    {
        if (index < 1) index = 1;
        var channel = GetOrCreateChannel(index);
        return channel.Tap.StartRecording(path, format);
    }

    public void StopChannelRecording(int index, RecordingSession? session = null)
    {
        if (index < 1) index = 1;
        if (_channels.TryGetValue(index, out var channel))
        {
            channel.Tap.StopRecording(session);
        }
    }

    public void RouteMidiInput(int deviceIndex, ISynth synth)
    {
        _midiRouter.Route(deviceIndex, synth);
        EnsureMidiInput(deviceIndex);
    }

    public void MapControlAction(int deviceIndex, int controlId, Action<float> action)
    {
        _midiRouter.MapControlAction(deviceIndex, controlId, action);
        EnsureMidiInput(deviceIndex);
    }

    public void ClearMappings()
    {
        _midiRouter.Clear();
    }

    public void ClearMixer()
    {
        _mixer.RemoveAllMixerInputs();
        _channels.Clear();
        _routing.Clear();
        _normalizedProviders.Clear();
        _masterEffects.ClearEffects();
        _masterTap.StopAll();
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

    public void Dispose()
    {
        foreach (var midiIn in _midiInputs.Values)
        {
            midiIn.Stop();
            midiIn.Dispose();
        }
        _midiInputs.Clear();

        _output?.Stop();
        _output?.Dispose();
        _output = null;

        MidiOutPool.DisposeAll();
    }

    private AudioChannel GetOrCreateChannel(int index)
    {
        if (_channels.TryGetValue(index, out var existing)) return existing;

        var mixer = new MixingSampleProvider(_waveFormat) { ReadFully = true };
        var effects = new AudioEffectChain(mixer, _waveFormat);
        var volume = new VolumeSampleProvider(effects) { Volume = 1.0f };
        var tap = new RecordingTap(volume);
        _mixer.AddMixerInput(tap);

        var channel = new AudioChannel(index, mixer, effects, volume, tap);
        _channels[index] = channel;
        return channel;
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
}
