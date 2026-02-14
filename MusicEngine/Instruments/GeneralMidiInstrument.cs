// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal General MIDI instrument wrapper.

using System;
using MusicEngine.Core;
#if WINDOWS
using NAudio.Midi;
using NAudio.Wave;
#endif

namespace MusicEngine.Instruments;

/// <summary>
/// General MIDI program numbers.
/// </summary>
public enum GeneralMidiProgram
{
    AcousticGrandPiano = 0,
    BrightAcousticPiano = 1,
    ElectricGrandPiano = 2,
    HonkyTonkPiano = 3,
    ElectricPiano1 = 4,
    ElectricPiano2 = 5,
    Harpsichord = 6,
    Clavinet = 7,
    Celesta = 8,
    Glockenspiel = 9,
    MusicBox = 10,
    Vibraphone = 11,
    Marimba = 12,
    Xylophone = 13,
    TubularBells = 14,
    Dulcimer = 15,
    DrawbarOrgan = 16,
    PercussiveOrgan = 17,
    RockOrgan = 18,
    ChurchOrgan = 19,
    ReedOrgan = 20,
    Accordion = 21,
    Harmonica = 22,
    TangoAccordion = 23,
    AcousticGuitarNylon = 24,
    AcousticGuitarSteel = 25,
    ElectricGuitarJazz = 26,
    ElectricGuitarClean = 27,
    ElectricGuitarMuted = 28,
    OverdrivenGuitar = 29,
    DistortionGuitar = 30,
    GuitarHarmonics = 31,
    AcousticBass = 32,
    ElectricBassFinger = 33,
    ElectricBassPick = 34,
    FretlessBass = 35,
    SlapBass1 = 36,
    SlapBass2 = 37,
    SynthBass1 = 38,
    SynthBass2 = 39,
    Violin = 40,
    Viola = 41,
    Cello = 42,
    Contrabass = 43,
    TremoloStrings = 44,
    PizzicatoStrings = 45,
    OrchestralHarp = 46,
    Timpani = 47,
    StringEnsemble1 = 48,
    StringEnsemble2 = 49,
    SynthStrings1 = 50,
    SynthStrings2 = 51,
    ChoirAahs = 52,
    VoiceOohs = 53,
    SynthChoir = 54,
    OrchestraHit = 55,
    Trumpet = 56,
    Trombone = 57,
    Tuba = 58,
    MutedTrumpet = 59,
    FrenchHorn = 60,
    BrassSection = 61,
    SynthBrass1 = 62,
    SynthBrass2 = 63,
    SopranoSax = 64,
    AltoSax = 65,
    TenorSax = 66,
    BaritoneSax = 67,
    Oboe = 68,
    EnglishHorn = 69,
    Bassoon = 70,
    Clarinet = 71,
    Piccolo = 72,
    Flute = 73,
    Recorder = 74,
    PanFlute = 75,
    BlownBottle = 76,
    Shakuhachi = 77,
    Whistle = 78,
    Ocarina = 79,
    Lead1Square = 80,
    Lead2Sawtooth = 81,
    Lead3Calliope = 82,
    Lead4Chiff = 83,
    Lead5Charang = 84,
    Lead6Voice = 85,
    Lead7Fifths = 86,
    Lead8BassLead = 87,
    Pad1NewAge = 88,
    Pad2Warm = 89,
    Pad3Polysynth = 90,
    Pad4Choir = 91,
    Pad5Bowed = 92,
    Pad6Metallic = 93,
    Pad7Halo = 94,
    Pad8Sweep = 95,
    FX1Rain = 96,
    FX2Soundtrack = 97,
    FX3Crystal = 98,
    FX4Atmosphere = 99,
    FX5Brightness = 100,
    FX6Goblins = 101,
    FX7Echoes = 102,
    FX8SciFi = 103,
    Sitar = 104,
    Banjo = 105,
    Shamisen = 106,
    Koto = 107,
    Kalimba = 108,
    Bagpipe = 109,
    Fiddle = 110,
    Shanai = 111,
    TinkleBell = 112,
    Agogo = 113,
    SteelDrums = 114,
    Woodblock = 115,
    TaikoDrum = 116,
    MelodicTom = 117,
    SynthDrum = 118,
    ReverseCymbal = 119,
    GuitarFretNoise = 120,
    BreathNoise = 121,
    Seashore = 122,
    BirdTweet = 123,
    TelephoneRing = 124,
    Helicopter = 125,
    Applause = 126,
    Gunshot = 127
}

#if WINDOWS
/// <summary>
/// Simple General MIDI synth wrapper backed by the system MIDI out device.
/// </summary>
public sealed class GeneralMidiInstrument : ISampleProvider, ISynth, IDisposable
{
    private readonly MidiOut? _midiOut;
    private readonly int _deviceId = -1;
    private readonly bool _available;
    private int _channel;
    private GeneralMidiProgram _program;
    private float _volume = 0.8f;
    private float _pan;
    private float _reverb;
    private float _chorus;
    private float _modWheel;

    /// <summary>
    /// Create a default GM instrument on channel 0.
    /// </summary>
    public GeneralMidiInstrument() : this(GeneralMidiProgram.AcousticGrandPiano, 0)
    {
    }

    /// <summary>
    /// Create a GM instrument with a specific program and channel.
    /// </summary>
    /// <param name="program">Program number.</param>
    /// <param name="channel">MIDI channel (0-15).</param>
    public GeneralMidiInstrument(GeneralMidiProgram program, int channel = 0)
    {
        _program = program;
        _channel = channel;
        Name = $"GM_{program}";

        if (MidiOut.NumberOfDevices == 0)
        {
            _available = false;
        }
        else
        {
            _deviceId = 0;
            _midiOut = MidiOutPool.Rent(_deviceId);
            _available = true;
            SendProgramChange(_program);
            Volume = _volume;
        }

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(Settings.SampleRate, Settings.Channels);
    }

    /// <summary>
    /// Display name for the instrument.
    /// </summary>
    public string Name { get; set; } = "GM";

    /// <summary>
    /// Output format used by the synth (silent provider).
    /// </summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Current program for the instrument.
    /// </summary>
    public GeneralMidiProgram Program
    {
        get => _program;
        set
        {
            _program = value;
            SendProgramChange(_program);
        }
    }

    /// <summary>
    /// MIDI channel (0-15).
    /// </summary>
    public int Channel
    {
        get => _channel;
        set
        {
            _channel = value;
            SendProgramChange(_program);
        }
    }

    /// <summary>
    /// Main volume in [0, 1].
    /// </summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = value;
            if (!_available || _midiOut == null) return;
            var midiVolume = (int)(Math.Clamp(_volume, 0f, 1f) * 127f);
            _midiOut.Send(new ControlChangeEvent(0, GetMidiChannel() + 1, MidiController.MainVolume, midiVolume).GetAsShortMessage());
        }
    }

    /// <summary>
    /// Pan in [-1, 1].
    /// </summary>
    public float Pan
    {
        get => _pan;
        set
        {
            _pan = value;
            if (!_available || _midiOut == null) return;
            var panValue = (int)((Math.Clamp(_pan, -1f, 1f) + 1f) * 63.5f);
            _midiOut.Send(new ControlChangeEvent(0, GetMidiChannel() + 1, MidiController.Pan, panValue).GetAsShortMessage());
        }
    }

    /// <summary>
    /// Reverb send in [0, 1].
    /// </summary>
    public float Reverb
    {
        get => _reverb;
        set
        {
            _reverb = value;
            if (!_available || _midiOut == null) return;
            var reverbValue = (int)(Math.Clamp(_reverb, 0f, 1f) * 127f);
            _midiOut.Send(new ControlChangeEvent(0, GetMidiChannel() + 1, (MidiController)91, reverbValue).GetAsShortMessage());
        }
    }

    /// <summary>
    /// Chorus send in [0, 1].
    /// </summary>
    public float Chorus
    {
        get => _chorus;
        set
        {
            _chorus = value;
            if (!_available || _midiOut == null) return;
            var chorusValue = (int)(Math.Clamp(_chorus, 0f, 1f) * 127f);
            _midiOut.Send(new ControlChangeEvent(0, GetMidiChannel() + 1, (MidiController)93, chorusValue).GetAsShortMessage());
        }
    }

    /// <summary>
    /// Modulation wheel value in [0, 1].
    /// </summary>
    public float ModWheel
    {
        get => _modWheel;
        set
        {
            _modWheel = value;
            if (!_available || _midiOut == null) return;
            var modulation = (int)(Math.Clamp(_modWheel, 0f, 1f) * 127f);
            _midiOut.Send(new ControlChangeEvent(0, GetMidiChannel() + 1, MidiController.Modulation, modulation).GetAsShortMessage());
        }
    }

    /// <summary>
    /// Send pitch bend in [-1, 1].
    /// </summary>
    public void PitchBend(float bend)
    {
        if (!_available || _midiOut == null) return;
        var clamped = Math.Clamp(bend, -1f, 1f);
        var pitchValue = (int)((clamped + 1f) * 8191.5f);
        _midiOut.Send(new PitchWheelChangeEvent(0, GetMidiChannel() + 1, pitchValue).GetAsShortMessage());
    }

    /// <summary>
    /// Trigger a MIDI note-on.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        if (!_available || _midiOut == null) return;
        note = Math.Clamp(note, 0, 127);
        velocity = Math.Clamp(velocity, 0, 127);
        _midiOut.Send(new NoteOnEvent(0, GetMidiChannel() + 1, note, velocity, 0).GetAsShortMessage());
    }

    /// <summary>
    /// Trigger a MIDI note-off.
    /// </summary>
    public void NoteOff(int note)
    {
        if (!_available || _midiOut == null) return;
        note = Math.Clamp(note, 0, 127);
        _midiOut.Send(new NoteOnEvent(0, GetMidiChannel() + 1, note, 0, 0).GetAsShortMessage());
    }

    /// <summary>
    /// Send all-notes-off to the device.
    /// </summary>
    public void AllNotesOff()
    {
        if (!_available || _midiOut == null) return;
        _midiOut.Send(new ControlChangeEvent(0, GetMidiChannel() + 1, MidiController.AllNotesOff, 0).GetAsShortMessage());
    }

    /// <summary>
    /// Set a named parameter (volume, pan, reverb, chorus, modulation, pitchbend).
    /// </summary>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume":
                Volume = value;
                break;
            case "pan":
                Pan = value;
                break;
            case "reverb":
                Reverb = value;
                break;
            case "chorus":
                Chorus = value;
                break;
            case "modulation":
                ModWheel = value;
                break;
            case "pitchbend":
                PitchBend(value);
                break;
        }
    }

    /// <summary>
    /// Create a parameter setter for mapping automation values.
    /// </summary>
    public Action<float> Param(string name, float min = 0f, float max = 1f)
    {
        return value =>
        {
            var scaled = min + value * (max - min);
            SetParameter(name, scaled);
        };
    }

    /// <summary>
    /// Read audio samples (silence for MIDI-only instruments).
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        return count;
    }

    /// <summary>
    /// Release MIDI resources and stop all notes.
    /// </summary>
    public void Dispose()
    {
        AllNotesOff();
        if (_midiOut != null && _deviceId >= 0)
        {
            MidiOutPool.Return(_deviceId);
        }
    }

    private void SendProgramChange(GeneralMidiProgram program)
    {
        if (!_available || _midiOut == null) return;
        _midiOut.Send(new PatchChangeEvent(0, GetMidiChannel() + 1, (int)program).GetAsShortMessage());
    }

    private int GetMidiChannel() => Math.Clamp(_channel, 0, 15);
}
#endif
