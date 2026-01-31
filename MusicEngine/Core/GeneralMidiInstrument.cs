// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: General MIDI instrument via system MIDI.

using System;
using System.Collections.Generic;
using NAudio.Midi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicEngine.Core;

/// <summary>
/// General MIDI instruments available on Windows
/// These are the 128 standard GM instruments
/// </summary>
public enum GeneralMidiProgram
{
    // Piano (0-7)
    AcousticGrandPiano = 0,
    BrightAcousticPiano = 1,
    ElectricGrandPiano = 2,
    HonkyTonkPiano = 3,
    ElectricPiano1 = 4,
    ElectricPiano2 = 5,
    Harpsichord = 6,
    Clavinet = 7,

    // Chromatic Percussion (8-15)
    Celesta = 8,
    Glockenspiel = 9,
    MusicBox = 10,
    Vibraphone = 11,
    Marimba = 12,
    Xylophone = 13,
    TubularBells = 14,
    Dulcimer = 15,

    // Organ (16-23)
    DrawbarOrgan = 16,
    PercussiveOrgan = 17,
    RockOrgan = 18,
    ChurchOrgan = 19,
    ReedOrgan = 20,
    Accordion = 21,
    Harmonica = 22,
    TangoAccordion = 23,

    // Guitar (24-31)
    AcousticGuitarNylon = 24,
    AcousticGuitarSteel = 25,
    ElectricGuitarJazz = 26,
    ElectricGuitarClean = 27,
    ElectricGuitarMuted = 28,
    OverdrivenGuitar = 29,
    DistortionGuitar = 30,
    GuitarHarmonics = 31,

    // Bass (32-39)
    AcousticBass = 32,
    ElectricBassFinger = 33,
    ElectricBassPick = 34,
    FretlessBass = 35,
    SlapBass1 = 36,
    SlapBass2 = 37,
    SynthBass1 = 38,
    SynthBass2 = 39,

    // Strings (40-47)
    Violin = 40,
    Viola = 41,
    Cello = 42,
    Contrabass = 43,
    TremoloStrings = 44,
    PizzicatoStrings = 45,
    OrchestralHarp = 46,
    Timpani = 47,

    // Ensemble (48-55)
    StringEnsemble1 = 48,
    StringEnsemble2 = 49,
    SynthStrings1 = 50,
    SynthStrings2 = 51,
    ChoirAahs = 52,
    VoiceOohs = 53,
    SynthVoice = 54,
    OrchestraHit = 55,

    // Brass (56-63)
    Trumpet = 56,
    Trombone = 57,
    Tuba = 58,
    MutedTrumpet = 59,
    FrenchHorn = 60,
    BrassSection = 61,
    SynthBrass1 = 62,
    SynthBrass2 = 63,

    // Reed (64-71)
    SopranoSax = 64,
    AltoSax = 65,
    TenorSax = 66,
    BaritoneSax = 67,
    Oboe = 68,
    EnglishHorn = 69,
    Bassoon = 70,
    Clarinet = 71,

    // Pipe (72-79)
    Piccolo = 72,
    Flute = 73,
    Recorder = 74,
    PanFlute = 75,
    BlownBottle = 76,
    Shakuhachi = 77,
    Whistle = 78,
    Ocarina = 79,

    // Synth Lead (80-87)
    Lead1Square = 80,
    Lead2Sawtooth = 81,
    Lead3Calliope = 82,
    Lead4Chiff = 83,
    Lead5Charang = 84,
    Lead6Voice = 85,
    Lead7Fifths = 86,
    Lead8BassLead = 87,

    // Synth Pad (88-95)
    Pad1NewAge = 88,
    Pad2Warm = 89,
    Pad3Polysynth = 90,
    Pad4Choir = 91,
    Pad5Bowed = 92,
    Pad6Metallic = 93,
    Pad7Halo = 94,
    Pad8Sweep = 95,

    // Synth Effects (96-103)
    FX1Rain = 96,
    FX2Soundtrack = 97,
    FX3Crystal = 98,
    FX4Atmosphere = 99,
    FX5Brightness = 100,
    FX6Goblins = 101,
    FX7Echoes = 102,
    FX8SciFi = 103,

    // Ethnic (104-111)
    Sitar = 104,
    Banjo = 105,
    Shamisen = 106,
    Koto = 107,
    Kalimba = 108,
    BagPipe = 109,
    Fiddle = 110,
    Shanai = 111,

    // Percussive (112-119)
    TinkleBell = 112,
    Agogo = 113,
    SteelDrums = 114,
    Woodblock = 115,
    TaikoDrum = 116,
    MelodicTom = 117,
    SynthDrum = 118,
    ReverseCymbal = 119,

    // Sound Effects (120-127)
    GuitarFretNoise = 120,
    BreathNoise = 121,
    Seashore = 122,
    BirdTweet = 123,
    TelephoneRing = 124,
    Helicopter = 125,
    Applause = 126,
    Gunshot = 127
}

/// <summary>
/// General MIDI instrument that uses Windows built-in synthesizer.
/// Supports all 128 GM instruments and full MIDI control.
/// </summary>
public class GeneralMidiInstrument : ISampleProvider, ISynth, IDisposable
{
    private readonly MidiOut? _midiOut;
    private readonly int _deviceId = -1;
    private int _channel;
    private float _pan;
    private float _reverb;
    private float _chorus;
    private float _modWheel;
    private float _pitchBend;
    private GeneralMidiProgram _program;
    private readonly SignalGenerator _signalGenerator; // Dummy signal for ISampleProvider
    private float _volume = 1.0f;
    private bool _disposed;
    private bool _midiAvailable;
    private static bool _deviceListPrinted = false;
    private static readonly object _midiLock = new();

    /// <summary>Parameterloser Konstruktor: Acoustic Grand, Channel 0, Standard-Defaults.</summary>
    public GeneralMidiInstrument() : this(GeneralMidiProgram.AcousticGrandPiano, 0)
    {
        ApplyDefaults();
    }

    /// <summary>
    /// Creates a new General MIDI instrument using Windows built-in synthesizer
    /// </summary>
    /// <param name="program">The GM instrument to use</param>
    /// <param name="channel">MIDI channel (0-15, default 0)</param>
    public GeneralMidiInstrument(GeneralMidiProgram program, int channel = 0)
    {
        _program = program;
        _channel = Math.Clamp(channel, 0, 15);
        Name = $"GM_{program}";

        // Try to find and open (or reuse) a MIDI device
        try
        {
            lock (_midiLock)
            {
                int midiDeviceCount = MidiOut.NumberOfDevices;

                if (midiDeviceCount == 0)
                {
                    Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                    Console.WriteLine("║  WARNING: No MIDI device found!                                ║");
                    Console.WriteLine("║  General MIDI instruments will be muted.                       ║");
                    Console.WriteLine("║                                                                ║");
                    Console.WriteLine("║  Possible solutions:                                           ║");
                    Console.WriteLine("║  1. Install Windows MIDI Synthesizer                           ║");
                    Console.WriteLine("║  2. Install virtual MIDI device (e.g. VirtualMIDISynth)        ║");
                    Console.WriteLine("║  3. Configure FL Studio MIDI output                            ║");
                    Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
                    _midiAvailable = false;
                    _midiOut = null;
                }
                else
                {
                    // Print available devices once
                    if (!_deviceListPrinted)
                    {
                        Console.WriteLine($"\n═══ Available MIDI Devices ({midiDeviceCount}) ═══");
                        for (int i = 0; i < midiDeviceCount; i++)
                        {
                            var caps = MidiOut.DeviceInfo(i);
                            Console.WriteLine($"  [{i}] {caps.ProductName}");
                        }
                        Console.WriteLine();
                        _deviceListPrinted = true;
                    }

                    // Build ordered candidate list (GS wavetable first, then other synth/midi, then rest)
                    var preferred = new List<int>();
                    var secondary = new List<int>();
                    for (int i = 0; i < midiDeviceCount; i++)
                    {
                        var caps = MidiOut.DeviceInfo(i);
                        string productName = caps.ProductName.ToLowerInvariant();

                        if (productName.Contains("microsoft") && productName.Contains("wavetable"))
                        {
                            preferred.Add(i);
                        }
                        else if (productName.Contains("synth") || productName.Contains("midi"))
                        {
                            secondary.Add(i);
                        }
                    }

                    var candidates = new List<int>();
                    candidates.AddRange(preferred);
                    candidates.AddRange(secondary);
                    for (int i = 0; i < midiDeviceCount; i++)
                    {
                        if (!candidates.Contains(i)) candidates.Add(i);
                    }

                    // Try devices until one opens (skip devices that are already allocated)
                    foreach (var deviceId in candidates)
                    {
                        try
                        {
                            _midiOut = MidiOutPool.Rent(deviceId);
                            _deviceId = deviceId;
                            _midiAvailable = true;
                            break;
                        }
                        catch (Exception ex) when (ex.Message.Contains("AlreadyAllocated", StringComparison.OrdinalIgnoreCase))
                        {
                            continue; // try next device
                        }
                    }

                    if (!_midiAvailable)
                    {
                        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
                        Console.WriteLine("║  ERROR: Could not open any MIDI device (all allocated).        ║");
                        Console.WriteLine("║  Close other apps using MIDI Out and reload.                   ║");
                        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
                    }
                }

                // Set the program (instrument) if MIDI is available
                if (_midiAvailable && _midiOut != null)
                {
                    var programChange = new PatchChangeEvent(0, _channel + 1, (int)program);
                    _midiOut.Send(programChange.GetAsShortMessage());
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  ERROR: Could not open MIDI device!                            ║");
            Console.WriteLine($"║  {ex.Message.PadRight(62)} ║");
            Console.WriteLine("║                                                                ║");
            Console.WriteLine("║  General MIDI instruments will be muted.                       ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            _midiAvailable = false;
            _midiOut = null;
        }

        // Create dummy signal generator for ISampleProvider compatibility
        _signalGenerator = new SignalGenerator(Settings.SampleRate, Settings.Channels)
        {
            Gain = 0, // Silent - actual sound comes from MIDI
            Frequency = 440,
            Type = SignalGeneratorType.Sin
        };

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(Settings.SampleRate, Settings.Channels);
    }

    /// <summary>
    /// Gets the current MIDI program (instrument)
    /// </summary>
    public GeneralMidiProgram Program
    {
        get => _program;
        set
        {
            _program = value;
            if (_midiAvailable && _midiOut != null)
            {
                var pc = new PatchChangeEvent(0, _channel + 1, (int)_program);
                _midiOut.Send(pc.GetAsShortMessage());
            }
        }
    }

    /// <summary>
    /// Gets or sets the MIDI channel (0-15). Sends Program Change on update.
    /// </summary>
    public int Channel
    {
        get => _channel;
        set
        {
            _channel = Math.Clamp(value, 0, 15);
            if (_midiAvailable && _midiOut != null)
            {
                var programChange = new PatchChangeEvent(0, _channel + 1, (int)_program);
                _midiOut.Send(programChange.GetAsShortMessage());
            }
        }
    }

    /// <inheritdoc />
    public string Name { get; set; }

    /// <inheritdoc />
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Volume control (0.0 - 1.0)
    /// Note: This controls MIDI volume, not audio output volume
    /// </summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);

            if (!_midiAvailable || _midiOut == null) return;

            // Send MIDI volume control change (CC 7)
            int midiVolume = (int)(_volume * 127);
            var msg = new ControlChangeEvent(0, _channel + 1, MidiController.MainVolume, midiVolume);
            _midiOut.Send(msg.GetAsShortMessage());
        }
    }

    /// <inheritdoc />
    public void NoteOn(int noteNumber, int velocity)
    {
        if (!_midiAvailable || _midiOut == null) return;

        noteNumber = Math.Clamp(noteNumber, 0, 127);
        velocity = Math.Clamp(velocity, 0, 127);

        var noteOn = new NoteOnEvent(0, _channel + 1, noteNumber, velocity, 0);
        _midiOut.Send(noteOn.GetAsShortMessage());
    }

    /// <inheritdoc />
    public void NoteOff(int noteNumber)
    {
        if (!_midiAvailable || _midiOut == null) return;

        noteNumber = Math.Clamp(noteNumber, 0, 127);

        var noteOff = new NoteOnEvent(0, _channel + 1, noteNumber, 0, 0);
        _midiOut.Send(noteOff.GetAsShortMessage());
    }

    /// <inheritdoc />
    public void AllNotesOff()
    {
        if (!_midiAvailable || _midiOut == null) return;

        // Send All Notes Off MIDI message (CC 123)
        var msg = new ControlChangeEvent(0, _channel + 1, MidiController.AllNotesOff, 0);
        _midiOut.Send(msg.GetAsShortMessage());
    }

    /// <inheritdoc />
    public void SetParameter(string name, float value)
    {
        if (!_midiAvailable || _midiOut == null) return;

        // Map common parameters to MIDI controls
        switch (name.ToLowerInvariant())
        {
            case "volume":
                Volume = value;
                break;

            case "pan":
                // MIDI pan: 0 = left, 64 = center, 127 = right
                Pan = value;
                break;

            case "expression":
                int expression = (int)(Math.Clamp(value, 0f, 1f) * 127);
                _midiOut.Send(new ControlChangeEvent(0, _channel + 1, MidiController.Expression, expression).GetAsShortMessage());
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

            case "sustain":
                int sustain = value > 0.5f ? 127 : 0;
                _midiOut.Send(new ControlChangeEvent(0, _channel + 1, MidiController.Sustain, sustain).GetAsShortMessage());
                break;
        }
    }

    /// <inheritdoc />
    public float GetParameter(string name)
    {
        // MIDI doesn't support reading parameters, return defaults
        return name.ToLowerInvariant() switch
        {
            "volume" => _volume,
            _ => 0f
        };
    }

    /// <summary>
    /// Sends a pitch bend message
    /// </summary>
    /// <param name="bend">Pitch bend amount (-1.0 to 1.0, where 0 is center)</param>
    public void PitchBend(float bend)
    {
        if (!_midiAvailable || _midiOut == null) return;

        bend = Math.Clamp(bend, -1f, 1f);
        // MIDI pitch bend: 0 = -2 semitones, 8192 = center, 16383 = +2 semitones
        int pitchValue = (int)((bend + 1f) * 8191.5f);
        var msg = new PitchWheelChangeEvent(0, _channel + 1, pitchValue);
        _midiOut.Send(msg.GetAsShortMessage());
    }

    /// <summary>
    /// Sends a MIDI control change message
    /// </summary>
    /// <param name="controller">MIDI controller number (0-127)</param>
    /// <param name="value">Controller value (0-127)</param>
    public void SendControlChange(int controller, int value)
    {
        if (!_midiAvailable || _midiOut == null) return;

        controller = Math.Clamp(controller, 0, 127);
        value = Math.Clamp(value, 0, 127);
        var msg = new ControlChangeEvent(0, _channel + 1, (MidiController)controller, value);
        _midiOut.Send(msg.GetAsShortMessage());
    }

    /// <inheritdoc />
    public int Read(float[] buffer, int offset, int count)
    {
        // Return silence - actual audio output is through MIDI device
        // This allows the instrument to be added to the mixer for compatibility
        Array.Clear(buffer, offset, count);
        return count;
    }

    /// <summary>
    /// Disposes the MIDI device
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        AllNotesOff();
        if (_midiOut != null && _deviceId >= 0)
        {
            lock (_midiLock)
            {
                MidiOutPool.Return(_deviceId);
            }
        }
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    // ==== Convenience factory to keep script logic minimal ====
    public static GeneralMidiInstrument FromName(
        string name = "piano",
        int channel = 0,
        float volume = 0.75f,
        float pan = 0f,
        float reverb = 0.15f,
        float chorus = 0.05f,
        float modWheel = 0f,
        float pitchBend = 0f)
    {
        var program = MapNameToProgram(name);
        var inst = new GeneralMidiInstrument(program, channel);
        inst.Name = $"GM_{program}";
        inst.Volume = Math.Clamp(volume, 0f, 1f);
        inst.SetParameter("pan", Math.Clamp(pan, -1f, 1f));
        inst.SetParameter("reverb", Math.Clamp(reverb, 0f, 1f));
        inst.SetParameter("chorus", Math.Clamp(chorus, 0f, 1f));
        inst.SetParameter("modulation", Math.Clamp(modWheel, 0f, 1f));
        inst.PitchBend(Math.Clamp(pitchBend, -1f, 1f));
        return inst;
    }


    /// <summary>Pan: -1 (left) .. +1 (right)</summary>
    public float Pan
    {
        get => _pan;
        set
        {
            _pan = Math.Clamp(value, -1f, 1f);
            if (!_midiAvailable || _midiOut == null) return;
            int panValue = (int)((_pan + 1f) * 63.5f);
            _midiOut.Send(new ControlChangeEvent(0, _channel + 1, MidiController.Pan, panValue).GetAsShortMessage());
        }
    }

    /// <summary>Reverb send 0..1</summary>
    public float Reverb
    {
        get => _reverb;
        set
        {
            _reverb = Math.Clamp(value, 0f, 1f);
            if (!_midiAvailable || _midiOut == null) return;
            int reverb = (int)(_reverb * 127);
            _midiOut.Send(new ControlChangeEvent(0, _channel + 1, (MidiController)91, reverb).GetAsShortMessage());
        }
    }

    /// <summary>Chorus send 0..1</summary>
    public float Chorus
    {
        get => _chorus;
        set
        {
            _chorus = Math.Clamp(value, 0f, 1f);
            if (!_midiAvailable || _midiOut == null) return;
            int chorus = (int)(_chorus * 127);
            _midiOut.Send(new ControlChangeEvent(0, _channel + 1, (MidiController)93, chorus).GetAsShortMessage());
        }
    }

    /// <summary>Mod wheel 0..1</summary>
    public float ModWheel
    {
        get => _modWheel;
        set
        {
            _modWheel = Math.Clamp(value, 0f, 1f);
            if (!_midiAvailable || _midiOut == null) return;
            int modulation = (int)(_modWheel * 127);
            _midiOut.Send(new ControlChangeEvent(0, _channel + 1, MidiController.Modulation, modulation).GetAsShortMessage());
        }
    }


    /// <summary>Sets the GM program by friendly name (e.g., "piano", "organ").</summary>
    public void Instrument(string name)
    {
        Program = MapNameToProgram(name);
        Name = $"GM_{Program}";
    }

    private void ApplyDefaults()
    {
        Volume = 0.8f;
        Pan = 0f;
        Reverb = 0f;  // effects off by default
        Chorus = 0f;
        ModWheel = 0f;
        PitchBend(0f);
        Channel = 0;
        Instrument("piano");
    }

    private static GeneralMidiProgram MapNameToProgram(string name)
    {
        var n = (name ?? string.Empty).Trim().ToLowerInvariant();
        return n switch
        {
            "piano" or "acoustic" or "grand" => GeneralMidiProgram.AcousticGrandPiano,
            "epiano" or "electricpiano" or "rhodes" => GeneralMidiProgram.ElectricPiano1,
            "organ" => GeneralMidiProgram.DrawbarOrgan,
            "strings" => GeneralMidiProgram.StringEnsemble1,
            "pad" => GeneralMidiProgram.Pad2Warm,
            "bass" => GeneralMidiProgram.AcousticBass,
            "guitar" => GeneralMidiProgram.ElectricGuitarClean,
            "lead" => GeneralMidiProgram.Lead2Sawtooth,
            "choir" => GeneralMidiProgram.ChoirAahs,
            _ => GeneralMidiProgram.AcousticGrandPiano
        };
    }
}
