// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Live audio input source (mic/line-in).

using System;
#if WINDOWS
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
#endif

namespace MusicEngine.Core;

#if WINDOWS
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

    /// <summary>
    /// Create an input from a specific MMDevice and index.
    /// </summary>
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

    /// <summary>
    /// Device index used to create this input.
    /// </summary>
    public int DeviceIndex { get; }
    /// <summary>
    /// Windows device ID string.
    /// </summary>
    public string DeviceId { get; }
    /// <summary>
    /// Friendly device name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Output wave format of the input stream.
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Read audio samples into the buffer.
    /// </summary>
    public int Read(float[] buffer, int offset, int count) => _source.Read(buffer, offset, count);

    /// <summary>
    /// Input gain (0..1).
    /// </summary>
    public float Gain
    {
        get => _gain;
        set
        {
            _gain = Math.Clamp(value, 0f, 1f);
            UpdateVolume();
        }
    }

    /// <summary>
    /// Input gain (lowercase alias).
    /// </summary>
    public float gain
    {
        get => Gain;
        set => Gain = value;
    }

    /// <summary>
    /// Mute input.
    /// </summary>
    public bool Mute
    {
        get => _mute;
        set
        {
            _mute = value;
            UpdateVolume();
        }
    }

    /// <summary>
    /// Mute input (lowercase alias).
    /// </summary>
    public bool mute
    {
        get => Mute;
        set => Mute = value;
    }

    /// <summary>
    /// Volume alias for Gain.
    /// </summary>
    public float Volume
    {
        get => Gain;
        set => Gain = value;
    }

    /// <summary>
    /// Pan (-1..1).
    /// </summary>
    public float Pan
    {
        get => _pan;
        set
        {
            _pan = Math.Clamp(value, -1f, 1f);
            UpdatePan();
        }
    }

    /// <summary>
    /// Mod wheel value (0..1).
    /// </summary>
    public float ModWheel { get; set; }
    /// <summary>
    /// Optional MIDI channel filter (-1 = any).
    /// </summary>
    public int Channel { get; set; } = -1;
    /// <summary>
    /// Reverb send amount.
    /// </summary>
    public float Reverb { get; set; }
    /// <summary>
    /// Chorus send amount.
    /// </summary>
    public float Chorus { get; set; }

    /// <summary>
    /// Stop capture and release resources.
    /// </summary>
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
#endif
