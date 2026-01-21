//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: DrumKit for easy drum pattern creation with built-in drum sounds.


using System;
using System.Collections.Generic;
using NAudio.Wave;


namespace MusicEngine.Core;


/// <summary>
/// Standard drum kit piece types (GM mapping)
/// </summary>
public enum DrumPiece
{
    /// <summary>Bass drum 1 (MIDI 36)</summary>
    Kick = 36,
    /// <summary>Bass drum 2 (MIDI 35)</summary>
    Kick2 = 35,
    /// <summary>Snare drum (MIDI 38)</summary>
    Snare = 38,
    /// <summary>Electric snare (MIDI 40)</summary>
    Snare2 = 40,
    /// <summary>Side stick/rimshot (MIDI 37)</summary>
    Rimshot = 37,
    /// <summary>Closed hi-hat (MIDI 42)</summary>
    HiHatClosed = 42,
    /// <summary>Open hi-hat (MIDI 46)</summary>
    HiHatOpen = 46,
    /// <summary>Pedal hi-hat (MIDI 44)</summary>
    HiHatPedal = 44,
    /// <summary>Crash cymbal 1 (MIDI 49)</summary>
    Crash = 49,
    /// <summary>Crash cymbal 2 (MIDI 57)</summary>
    Crash2 = 57,
    /// <summary>Ride cymbal (MIDI 51)</summary>
    Ride = 51,
    /// <summary>Ride bell (MIDI 53)</summary>
    RideBell = 53,
    /// <summary>High tom (MIDI 50)</summary>
    TomHigh = 50,
    /// <summary>Mid tom (MIDI 47)</summary>
    TomMid = 47,
    /// <summary>Low tom (MIDI 45)</summary>
    TomLow = 45,
    /// <summary>Floor tom (MIDI 43)</summary>
    TomFloor = 43,
    /// <summary>Clap (MIDI 39)</summary>
    Clap = 39,
    /// <summary>Cowbell (MIDI 56)</summary>
    Cowbell = 56,
    /// <summary>Tambourine (MIDI 54)</summary>
    Tambourine = 54,
    /// <summary>Shaker/maracas (MIDI 70)</summary>
    Shaker = 70,
    /// <summary>Conga high (MIDI 62)</summary>
    CongaHigh = 62,
    /// <summary>Conga low (MIDI 63)</summary>
    CongaLow = 63,
    /// <summary>Bongo high (MIDI 60)</summary>
    BongoHigh = 60,
    /// <summary>Bongo low (MIDI 61)</summary>
    BongoLow = 61
}


/// <summary>
/// A synthesized drum voice
/// </summary>
internal class DrumVoice
{
    private double _phase;
    private double _time;
    private double _frequency;
    private double _pitchDecay;
    private double _amplitude;
    private double _amplitudeDecay;
    private double _noiseLevel;
    private double _noiseDecay;
    private readonly Random _random = new();
    private bool _active;

    public bool IsActive => _active;

    public void Trigger(DrumSynthParams prms, int velocity)
    {
        _frequency = prms.Frequency;
        _pitchDecay = prms.PitchDecay;
        _amplitude = (velocity / 127.0) * prms.Volume;
        _amplitudeDecay = prms.AmpDecay;
        _noiseLevel = prms.NoiseLevel;
        _noiseDecay = prms.NoiseDecay;
        _phase = 0;
        _time = 0;
        _active = true;
    }

    public float Process(double deltaTime)
    {
        if (!_active) return 0;

        _time += deltaTime;

        // Pitch envelope
        double freq = _frequency * Math.Pow(2, -_time * _pitchDecay);

        // Amplitude envelope
        double amp = _amplitude * Math.Exp(-_time * _amplitudeDecay);

        // Noise envelope
        double noise = _noiseLevel * Math.Exp(-_time * _noiseDecay);

        // Check if voice has faded out
        if (amp < 0.001 && noise < 0.001)
        {
            _active = false;
            return 0;
        }

        // Generate tone
        _phase += 2 * Math.PI * freq * deltaTime;
        if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
        double tone = Math.Sin(_phase);

        // Generate noise
        double noiseValue = (_random.NextDouble() * 2 - 1) * noise;

        return (float)(tone * amp + noiseValue);
    }
}


/// <summary>
/// Parameters for synthesized drum sounds
/// </summary>
internal struct DrumSynthParams
{
    public double Frequency;
    public double PitchDecay;
    public double Volume;
    public double AmpDecay;
    public double NoiseLevel;
    public double NoiseDecay;

    public static DrumSynthParams Kick => new()
    {
        Frequency = 150,
        PitchDecay = 40,
        Volume = 1.0,
        AmpDecay = 8,
        NoiseLevel = 0.1,
        NoiseDecay = 50
    };

    public static DrumSynthParams Snare => new()
    {
        Frequency = 200,
        PitchDecay = 30,
        Volume = 0.8,
        AmpDecay = 15,
        NoiseLevel = 0.6,
        NoiseDecay = 12
    };

    public static DrumSynthParams HiHat => new()
    {
        Frequency = 8000,
        PitchDecay = 0,
        Volume = 0.3,
        AmpDecay = 40,
        NoiseLevel = 0.8,
        NoiseDecay = 30
    };

    public static DrumSynthParams OpenHiHat => new()
    {
        Frequency = 7000,
        PitchDecay = 0,
        Volume = 0.35,
        AmpDecay = 8,
        NoiseLevel = 0.7,
        NoiseDecay = 6
    };

    public static DrumSynthParams Clap => new()
    {
        Frequency = 1000,
        PitchDecay = 5,
        Volume = 0.6,
        AmpDecay = 20,
        NoiseLevel = 0.9,
        NoiseDecay = 18
    };

    public static DrumSynthParams Tom => new()
    {
        Frequency = 100,
        PitchDecay = 15,
        Volume = 0.8,
        AmpDecay = 10,
        NoiseLevel = 0.15,
        NoiseDecay = 20
    };

    public static DrumSynthParams Rimshot => new()
    {
        Frequency = 800,
        PitchDecay = 50,
        Volume = 0.5,
        AmpDecay = 50,
        NoiseLevel = 0.4,
        NoiseDecay = 40
    };

    public static DrumSynthParams Crash => new()
    {
        Frequency = 5000,
        PitchDecay = 0.5,
        Volume = 0.5,
        AmpDecay = 2,
        NoiseLevel = 0.9,
        NoiseDecay = 1.5
    };

    public static DrumSynthParams Ride => new()
    {
        Frequency = 6000,
        PitchDecay = 0.3,
        Volume = 0.4,
        AmpDecay = 3,
        NoiseLevel = 0.5,
        NoiseDecay = 2.5
    };

    public static DrumSynthParams Cowbell => new()
    {
        Frequency = 560,
        PitchDecay = 0,
        Volume = 0.5,
        AmpDecay = 15,
        NoiseLevel = 0.1,
        NoiseDecay = 30
    };
}


/// <summary>
/// A drum kit with synthesized drum sounds.
/// Can be used standalone or with samples.
/// </summary>
public class DrumKit : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<DrumVoice> _voices = new();
    private readonly Dictionary<int, DrumSynthParams> _drumMap = new();
    private readonly SampleInstrument? _sampleKit;
    private readonly object _lock = new();
    private const int MaxVoices = 32;

    /// <summary>
    /// Gets or sets the drum kit name
    /// </summary>
    public string Name { get; set; } = "DrumKit";

    /// <summary>
    /// Gets the audio format
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Gets or sets the master volume (0-1)
    /// </summary>
    public float Volume { get; set; } = 0.8f;

    /// <summary>
    /// Gets or sets whether to use synthesized sounds (true) or samples (false)
    /// </summary>
    public bool UseSynthesis { get; set; } = true;

    /// <summary>
    /// Creates a new drum kit with synthesized sounds
    /// </summary>
    public DrumKit(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize voices
        for (int i = 0; i < MaxVoices; i++)
        {
            _voices.Add(new DrumVoice());
        }

        // Set up default drum mappings
        InitializeDefaultMappings();
    }

    /// <summary>
    /// Creates a drum kit that can also use samples
    /// </summary>
    public DrumKit(SampleInstrument sampleKit, int? sampleRate = null) : this(sampleRate)
    {
        _sampleKit = sampleKit;
    }

    /// <summary>
    /// Initialize default drum mappings
    /// </summary>
    private void InitializeDefaultMappings()
    {
        // Kicks
        _drumMap[(int)DrumPiece.Kick] = DrumSynthParams.Kick;
        _drumMap[(int)DrumPiece.Kick2] = DrumSynthParams.Kick;

        // Snares
        _drumMap[(int)DrumPiece.Snare] = DrumSynthParams.Snare;
        _drumMap[(int)DrumPiece.Snare2] = DrumSynthParams.Snare;
        _drumMap[(int)DrumPiece.Rimshot] = DrumSynthParams.Rimshot;
        _drumMap[(int)DrumPiece.Clap] = DrumSynthParams.Clap;

        // Hi-hats
        _drumMap[(int)DrumPiece.HiHatClosed] = DrumSynthParams.HiHat;
        _drumMap[(int)DrumPiece.HiHatOpen] = DrumSynthParams.OpenHiHat;
        _drumMap[(int)DrumPiece.HiHatPedal] = DrumSynthParams.HiHat;

        // Cymbals
        _drumMap[(int)DrumPiece.Crash] = DrumSynthParams.Crash;
        _drumMap[(int)DrumPiece.Crash2] = DrumSynthParams.Crash;
        _drumMap[(int)DrumPiece.Ride] = DrumSynthParams.Ride;
        _drumMap[(int)DrumPiece.RideBell] = DrumSynthParams.Ride;

        // Toms
        var tomHigh = DrumSynthParams.Tom;
        tomHigh.Frequency = 180;
        _drumMap[(int)DrumPiece.TomHigh] = tomHigh;

        var tomMid = DrumSynthParams.Tom;
        tomMid.Frequency = 130;
        _drumMap[(int)DrumPiece.TomMid] = tomMid;

        var tomLow = DrumSynthParams.Tom;
        tomLow.Frequency = 100;
        _drumMap[(int)DrumPiece.TomLow] = tomLow;

        var tomFloor = DrumSynthParams.Tom;
        tomFloor.Frequency = 80;
        _drumMap[(int)DrumPiece.TomFloor] = tomFloor;

        // Percussion
        _drumMap[(int)DrumPiece.Cowbell] = DrumSynthParams.Cowbell;
    }

    /// <summary>
    /// Play a drum piece
    /// </summary>
    public void Play(DrumPiece piece, int velocity = 100)
    {
        NoteOn((int)piece, velocity);
    }

    /// <summary>
    /// Trigger a drum sound by MIDI note
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        // If using samples and sample exists, use it
        if (!UseSynthesis && _sampleKit != null)
        {
            _sampleKit.NoteOn(note, velocity);
            return;
        }

        lock (_lock)
        {
            // Find the drum parameters
            if (!_drumMap.TryGetValue(note, out var prms))
            {
                // Default to a generic drum sound
                prms = DrumSynthParams.Tom;
            }

            // Find a free voice
            DrumVoice? voice = null;
            foreach (var v in _voices)
            {
                if (!v.IsActive)
                {
                    voice = v;
                    break;
                }
            }

            // If no free voice, steal the oldest one (first in list that's active)
            if (voice == null)
            {
                voice = _voices[0];
            }

            voice.Trigger(prms, velocity);
        }
    }

    /// <summary>
    /// Note off (drums typically ignore this)
    /// </summary>
    public void NoteOff(int note)
    {
        // Drums are typically one-shot, so we don't need to handle NoteOff
        // But if using samples, forward it
        _sampleKit?.NoteOff(note);
    }

    /// <summary>
    /// Stop all playing sounds
    /// </summary>
    public void AllNotesOff()
    {
        lock (_lock)
        {
            // Voices will naturally fade out, but we can speed it up
            // by just letting them continue
        }
        _sampleKit?.AllNotesOff();
    }

    /// <summary>
    /// Set a parameter
    /// </summary>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume":
                Volume = Math.Clamp(value, 0f, 1f);
                break;
            case "synthesis":
                UseSynthesis = value > 0.5f;
                break;
        }
    }

    /// <summary>
    /// Read audio samples
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        // Clear buffer
        for (int i = 0; i < count; i++)
        {
            buffer[offset + i] = 0;
        }

        int channels = _waveFormat.Channels;
        double deltaTime = 1.0 / _waveFormat.SampleRate;

        lock (_lock)
        {
            for (int n = 0; n < count; n += channels)
            {
                float sample = 0;

                foreach (var voice in _voices)
                {
                    if (voice.IsActive)
                    {
                        sample += voice.Process(deltaTime);
                    }
                }

                // Apply master volume
                sample *= Volume;

                // Output to all channels
                for (int c = 0; c < channels; c++)
                {
                    if (offset + n + c < buffer.Length)
                    {
                        buffer[offset + n + c] = sample;
                    }
                }
            }
        }

        // If using samples, mix in sample output
        if (!UseSynthesis && _sampleKit != null)
        {
            var sampleBuffer = new float[count];
            _sampleKit.Read(sampleBuffer, 0, count);

            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] += sampleBuffer[i] * Volume;
            }
        }

        return count;
    }

    /// <summary>
    /// Create a simple drum pattern
    /// </summary>
    public Pattern CreatePattern(double loopLength = 4.0)
    {
        var pattern = new Pattern(this);
        pattern.LoopLength = loopLength;
        return pattern;
    }

    /// <summary>
    /// Add a drum hit to a pattern
    /// </summary>
    public static void AddHit(Pattern pattern, DrumPiece piece, double beat, int velocity = 100)
    {
        pattern.Note((int)piece, beat, 0.1, velocity);
    }

    /// <summary>
    /// Create a basic 4/4 rock beat
    /// </summary>
    public Pattern CreateRockBeat()
    {
        var pattern = CreatePattern(4.0);

        // Kick on 1 and 3
        AddHit(pattern, DrumPiece.Kick, 0);
        AddHit(pattern, DrumPiece.Kick, 2);

        // Snare on 2 and 4
        AddHit(pattern, DrumPiece.Snare, 1);
        AddHit(pattern, DrumPiece.Snare, 3);

        // Hi-hat on every eighth
        for (double beat = 0; beat < 4; beat += 0.5)
        {
            AddHit(pattern, DrumPiece.HiHatClosed, beat, 80);
        }

        return pattern;
    }

    /// <summary>
    /// Create a basic house beat
    /// </summary>
    public Pattern CreateHouseBeat()
    {
        var pattern = CreatePattern(4.0);

        // Four on the floor kick
        for (int i = 0; i < 4; i++)
        {
            AddHit(pattern, DrumPiece.Kick, i);
        }

        // Clap/snare on 2 and 4
        AddHit(pattern, DrumPiece.Clap, 1);
        AddHit(pattern, DrumPiece.Clap, 3);

        // Open hi-hat on off-beats
        for (double beat = 0.5; beat < 4; beat += 1.0)
        {
            AddHit(pattern, DrumPiece.HiHatOpen, beat, 70);
        }

        return pattern;
    }

    /// <summary>
    /// Create a trap beat
    /// </summary>
    public Pattern CreateTrapBeat()
    {
        var pattern = CreatePattern(4.0);

        // Kick pattern
        AddHit(pattern, DrumPiece.Kick, 0);
        AddHit(pattern, DrumPiece.Kick, 0.75);
        AddHit(pattern, DrumPiece.Kick, 2.5);

        // Snare on 2 and 4
        AddHit(pattern, DrumPiece.Snare, 1);
        AddHit(pattern, DrumPiece.Snare, 3);

        // Rapid hi-hats
        for (double beat = 0; beat < 4; beat += 0.25)
        {
            int vel = beat % 0.5 == 0 ? 90 : 60;
            AddHit(pattern, DrumPiece.HiHatClosed, beat, vel);
        }

        return pattern;
    }

    /// <summary>
    /// Create a breakbeat pattern
    /// </summary>
    public Pattern CreateBreakbeat()
    {
        var pattern = CreatePattern(4.0);

        // Syncopated kick
        AddHit(pattern, DrumPiece.Kick, 0);
        AddHit(pattern, DrumPiece.Kick, 1.25);
        AddHit(pattern, DrumPiece.Kick, 2);
        AddHit(pattern, DrumPiece.Kick, 3.5);

        // Snare
        AddHit(pattern, DrumPiece.Snare, 1);
        AddHit(pattern, DrumPiece.Snare, 2.75);
        AddHit(pattern, DrumPiece.Snare, 3.25);

        // Hi-hats with variation
        for (double beat = 0; beat < 4; beat += 0.5)
        {
            if ((int)(beat * 2) % 3 != 0)
            {
                AddHit(pattern, DrumPiece.HiHatClosed, beat, 80);
            }
        }

        return pattern;
    }
}
