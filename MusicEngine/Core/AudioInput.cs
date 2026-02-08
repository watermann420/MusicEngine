// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Live audio input source (mic/line-in).

using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicEngine.Core;

/// <summary>
/// Live audio input source (mic/line-in).
/// </summary>
public sealed class AudioInput : ISampleProvider, IInstrumentControls, IDisposable
{
    private readonly WasapiCapture _capture;
    private readonly BufferedWaveProvider _buffer;
    private readonly ISampleProvider _source;
    private readonly PanningSampleProvider? _panProvider;
    private readonly VolumeSampleProvider _volumeProvider;
    private float _gain = 1f;
    private bool _mute;
    private float _pan;

    public AudioInput(MMDevice device, int deviceIndex)
    {
        DeviceIndex = deviceIndex;
        DeviceId = device.ID;
        Name = device.FriendlyName;

        _capture = new WasapiCapture(device);
        _capture.ShareMode = AudioClientShareMode.Shared;
        _buffer = new BufferedWaveProvider(_capture.WaveFormat)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromSeconds(2)
        };
        _capture.DataAvailable += (_, e) =>
        {
            if (e.BytesRecorded > 0)
            {
                _buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
            }
        };
        _capture.StartRecording();

        ISampleProvider current = _buffer.ToSampleProvider();
        if (current.WaveFormat.Channels == 1)
        {
            current = new MonoToStereoSampleProvider(current);
        }

        if (current.WaveFormat.Channels == 2)
        {
            _panProvider = new PanningSampleProvider(current);
            _volumeProvider = new VolumeSampleProvider(_panProvider);
        }
        else
        {
            _volumeProvider = new VolumeSampleProvider(current);
        }

        _source = _volumeProvider;
        UpdateVolume();
        UpdatePan();
    }

    public int DeviceIndex { get; }
    public string DeviceId { get; }
    public string Name { get; set; }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count) => _source.Read(buffer, offset, count);

    public float Gain
    {
        get => _gain;
        set
        {
            _gain = Math.Clamp(value, 0f, 1f);
            UpdateVolume();
        }
    }

    public float gain
    {
        get => Gain;
        set => Gain = value;
    }

    public bool Mute
    {
        get => _mute;
        set
        {
            _mute = value;
            UpdateVolume();
        }
    }

    public bool mute
    {
        get => Mute;
        set => Mute = value;
    }

    public float Volume
    {
        get => Gain;
        set => Gain = value;
    }

    public float Pan
    {
        get => _pan;
        set
        {
            _pan = Math.Clamp(value, -1f, 1f);
            UpdatePan();
        }
    }

    public float ModWheel { get; set; }
    public int Channel { get; set; } = -1;
    public float Reverb { get; set; }
    public float Chorus { get; set; }

    public void Dispose()
    {
        _capture.StopRecording();
        _capture.Dispose();
    }

    private void UpdateVolume()
    {
        _volumeProvider.Volume = _mute ? 0f : _gain;
    }

    private void UpdatePan()
    {
        if (_panProvider != null)
        {
            _panProvider.Pan = _pan;
        }
    }
}
