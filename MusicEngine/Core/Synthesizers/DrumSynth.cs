// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Synthesized drum sounds (808/909 style).

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// Drum sound types available in the synthesizer.
/// </summary>
public enum DrumSound
{
    /// <summary>Bass drum / Kick drum.</summary>
    Kick,
    /// <summary>Snare drum.</summary>
    Snare,
    /// <summary>Closed hi-hat.</summary>
    HiHatClosed,
    /// <summary>Open hi-hat.</summary>
    HiHatOpen,
    /// <summary>Hand clap.</summary>
    Clap,
    /// <summary>High tom.</summary>
    TomHigh,
    /// <summary>Mid tom.</summary>
    TomMid,
    /// <summary>Low tom.</summary>
    TomLow,
    /// <summary>Rim shot.</summary>
    Rim,
    /// <summary>Cowbell.</summary>
    Cowbell,
    /// <summary>Crash cymbal.</summary>
    Crash,
    /// <summary>Ride cymbal.</summary>
    Ride
}

/// <summary>
/// Represents a synthesized drum voice with multiple oscillators and envelopes.
/// </summary>
internal class DrumSynthVoice
{
    private readonly int _sampleRate;
    private readonly Random _random;

    // Oscillator state
    private double _phase1;
    private double _phase2;
    private double _noisePhase;

    // Envelope state
    private double _time;
    private bool _isActive;

    // Current sound parameters
    private DrumSynthParameters _params;
    private int _velocity;

    // Choke group handling
    public int ChokeGroup { get; private set; }

    public bool IsActive => _isActive;

    public DrumSynthVoice(int sampleRate)
    {
        _sampleRate = sampleRate;
        _random = new Random();
    }

    public void Trigger(DrumSynthParameters parameters, int velocity, int chokeGroup = 0)
    {
        _params = parameters;
        _velocity = velocity;
        _time = 0;
        _phase1 = 0;
        _phase2 = 0;
        _noisePhase = _random.NextDouble() * Math.PI * 2;
        _isActive = true;
        ChokeGroup = chokeGroup;
    }

    public void Choke()
    {
        _isActive = false;
    }

    public float Process(double deltaTime)
    {
        if (!_isActive) return 0;

        _time += deltaTime;

        // Check if voice has finished
        double maxDuration = Math.Max(
            Math.Max(1.0 / _params.AmpDecay, 1.0 / _params.NoiseDecay),
            Math.Max(1.0 / _params.PitchDecay, 1.0 / _params.ToneDecay)
        ) * 5;

        if (_time > maxDuration)
        {
            _isActive = false;
            return 0;
        }

        float velocityScale = _velocity / 127f;
        float output = 0;

        // Pitch envelope
        double pitchEnv = Math.Exp(-_time * _params.PitchDecay);
        double freq1 = _params.Frequency * (1.0 + _params.PitchEnvAmount * pitchEnv);
        double freq2 = _params.Frequency2 * (1.0 + _params.PitchEnvAmount * pitchEnv);

        // Tone oscillator 1
        if (_params.ToneLevel > 0)
        {
            double toneEnv = Math.Exp(-_time * _params.ToneDecay);
            _phase1 += 2.0 * Math.PI * freq1 / _sampleRate;
            if (_phase1 > 2.0 * Math.PI) _phase1 -= 2.0 * Math.PI;

            double tone1 = Math.Sin(_phase1);

            // Apply distortion/saturation if enabled
            if (_params.Drive > 0)
            {
                tone1 = Math.Tanh(tone1 * (1.0 + _params.Drive * 3.0));
            }

            output += (float)(tone1 * toneEnv * _params.ToneLevel);
        }

        // Tone oscillator 2 (sub/harmonic)
        if (_params.Tone2Level > 0)
        {
            double tone2Env = Math.Exp(-_time * _params.Tone2Decay);
            _phase2 += 2.0 * Math.PI * freq2 / _sampleRate;
            if (_phase2 > 2.0 * Math.PI) _phase2 -= 2.0 * Math.PI;

            double tone2 = Math.Sin(_phase2);
            output += (float)(tone2 * tone2Env * _params.Tone2Level);
        }

        // Click/transient
        if (_params.ClickLevel > 0 && _time < _params.ClickDuration)
        {
            double clickEnv = 1.0 - (_time / _params.ClickDuration);
            double click = _random.NextDouble() * 2.0 - 1.0;
            // Bandpass the click
            click *= Math.Sin(_time * _params.ClickFreq * 2.0 * Math.PI);
            output += (float)(click * clickEnv * clickEnv * _params.ClickLevel);
        }

        // Noise component
        if (_params.NoiseLevel > 0)
        {
            double noiseEnv = Math.Exp(-_time * _params.NoiseDecay);
            double noise = GenerateNoise(_params.NoiseType);

            // Apply noise filter
            noise = ApplyNoiseFilter(noise, _params.NoiseFilterFreq, _params.NoiseFilterQ);

            output += (float)(noise * noiseEnv * _params.NoiseLevel);
        }

        // Amp envelope
        double ampEnv = Math.Exp(-_time * _params.AmpDecay);
        output *= (float)(ampEnv * _params.Volume * velocityScale);

        return output;
    }

    private double GenerateNoise(NoiseType type)
    {
        double white = _random.NextDouble() * 2.0 - 1.0;

        return type switch
        {
            NoiseType.White => white,
            NoiseType.Pink => white * 0.7, // Simplified pink noise approximation
            NoiseType.Metallic => Math.Sin(_noisePhase += 0.1 + white * 0.05) * white,
            _ => white
        };
    }

    private double ApplyNoiseFilter(double input, double freq, double q)
    {
        // Simple one-pole lowpass for noise shaping
        double rc = 1.0 / (2.0 * Math.PI * freq);
        double dt = 1.0 / _sampleRate;
        double alpha = dt / (rc + dt);
        return alpha * input; // Simplified filter
    }
}

/// <summary>
/// Parameters for synthesizing drum sounds.
/// </summary>
internal struct DrumSynthParameters
{
    public double Frequency;
    public double Frequency2;
    public double PitchDecay;
    public double PitchEnvAmount;
    public double ToneLevel;
    public double ToneDecay;
    public double Tone2Level;
    public double Tone2Decay;
    public double ClickLevel;
    public double ClickDuration;
    public double ClickFreq;
    public double NoiseLevel;
    public double NoiseDecay;
    public double NoiseFilterFreq;
    public double NoiseFilterQ;
    public NoiseType NoiseType;
    public double AmpDecay;
    public double Volume;
    public double Drive;
}

internal enum NoiseType
{
    White,
    Pink,
    Metallic
}

/// <summary>
/// Fully synthesized drum machine with kick, snare, hi-hat, clap, and tom sounds.
/// Each drum sound has multiple parameters for detailed sound design.
/// Triggered via MIDI notes (C1=36=Kick, D1=38=Snare, etc.).
/// </summary>
public class DrumSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<DrumSynthVoice> _voices = new();
    private readonly Dictionary<int, DrumSynthParameters> _drumMap = new();
    private readonly Dictionary<int, int> _chokeGroups = new();
    private readonly object _lock = new();
    private const int MaxVoices = 32;

    // MIDI note mappings (GM-compatible)
    private const int NoteKick = 36;        // C1
    private const int NoteRim = 37;         // C#1
    private const int NoteSnare = 38;       // D1
    private const int NoteClap = 39;        // D#1
    private const int NoteSnare2 = 40;      // E1
    private const int NoteHiHatClosed = 42; // F#1
    private const int NoteHiHatPedal = 44;  // G#1
    private const int NoteHiHatOpen = 46;   // A#1
    private const int NoteTomLow = 45;      // A1
    private const int NoteTomMid = 47;      // B1
    private const int NoteCrash = 49;       // C#2
    private const int NoteTomHigh = 50;     // D2
    private const int NoteRide = 51;        // D#2
    private const int NoteCowbell = 56;     // G#2

    /// <summary>
    /// Gets or sets the synth name.
    /// </summary>
    public string Name { get; set; } = "DrumSynth";

    /// <summary>
    /// Gets the audio format.
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Gets or sets the master volume (0-1).
    /// </summary>
    public float Volume { get; set; } = 0.8f;

    // Kick drum parameters
    /// <summary>Kick drum pitch in Hz (default: 55).</summary>
    public float KickPitch { get; set; } = 55f;
    /// <summary>Kick pitch envelope decay rate (default: 30).</summary>
    public float KickPitchDecay { get; set; } = 30f;
    /// <summary>Kick pitch envelope amount (default: 3.0).</summary>
    public float KickPitchAmount { get; set; } = 3f;
    /// <summary>Kick click level (default: 0.3).</summary>
    public float KickClick { get; set; } = 0.3f;
    /// <summary>Kick sub oscillator level (default: 0.5).</summary>
    public float KickSub { get; set; } = 0.5f;
    /// <summary>Kick decay time (default: 8).</summary>
    public float KickDecay { get; set; } = 8f;
    /// <summary>Kick drive/saturation (default: 0.2).</summary>
    public float KickDrive { get; set; } = 0.2f;

    // Snare parameters
    /// <summary>Snare body tone frequency (default: 200).</summary>
    public float SnareBody { get; set; } = 200f;
    /// <summary>Snare snap/noise level (default: 0.6).</summary>
    public float SnareSnap { get; set; } = 0.6f;
    /// <summary>Snare tone level (default: 0.5).</summary>
    public float SnareTone { get; set; } = 0.5f;
    /// <summary>Snare decay time (default: 15).</summary>
    public float SnareDecay { get; set; } = 15f;

    // Hi-hat parameters
    /// <summary>Hi-hat metallic tone frequency (default: 8000).</summary>
    public float HiHatTone { get; set; } = 8000f;
    /// <summary>Closed hi-hat decay (default: 40).</summary>
    public float HiHatClosedDecay { get; set; } = 40f;
    /// <summary>Open hi-hat decay (default: 8).</summary>
    public float HiHatOpenDecay { get; set; } = 8f;

    // Clap parameters
    /// <summary>Clap noise decay (default: 18).</summary>
    public float ClapDecay { get; set; } = 18f;
    /// <summary>Number of clap layers (default: 4).</summary>
    public int ClapLayers { get; set; } = 4;

    // Tom parameters
    /// <summary>Base tom frequency (default: 100).</summary>
    public float TomPitch { get; set; } = 100f;
    /// <summary>Tom decay time (default: 10).</summary>
    public float TomDecay { get; set; } = 10f;

    /// <summary>
    /// Creates a new drum synthesizer.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (default: from Settings).</param>
    public DrumSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize voices
        for (int i = 0; i < MaxVoices; i++)
        {
            _voices.Add(new DrumSynthVoice(rate));
        }

        // Set up choke groups (hi-hats choke each other)
        _chokeGroups[NoteHiHatClosed] = 1;
        _chokeGroups[NoteHiHatOpen] = 1;
        _chokeGroups[NoteHiHatPedal] = 1;

        InitializeDrumMap();
    }

    private void InitializeDrumMap()
    {
        // Kick drum
        _drumMap[NoteKick] = CreateKickParams();

        // Snare drums
        _drumMap[NoteSnare] = CreateSnareParams();
        _drumMap[NoteSnare2] = CreateSnareParams();

        // Rim shot
        _drumMap[NoteRim] = CreateRimParams();

        // Clap
        _drumMap[NoteClap] = CreateClapParams();

        // Hi-hats
        _drumMap[NoteHiHatClosed] = CreateHiHatClosedParams();
        _drumMap[NoteHiHatOpen] = CreateHiHatOpenParams();
        _drumMap[NoteHiHatPedal] = CreateHiHatClosedParams();

        // Toms
        _drumMap[NoteTomHigh] = CreateTomParams(1.5);
        _drumMap[NoteTomMid] = CreateTomParams(1.0);
        _drumMap[NoteTomLow] = CreateTomParams(0.7);

        // Cymbals
        _drumMap[NoteCrash] = CreateCrashParams();
        _drumMap[NoteRide] = CreateRideParams();

        // Cowbell
        _drumMap[NoteCowbell] = CreateCowbellParams();
    }

    private DrumSynthParameters CreateKickParams()
    {
        return new DrumSynthParameters
        {
            Frequency = KickPitch,
            Frequency2 = KickPitch * 0.5, // Sub octave
            PitchDecay = KickPitchDecay,
            PitchEnvAmount = KickPitchAmount,
            ToneLevel = 1.0,
            ToneDecay = KickDecay,
            Tone2Level = KickSub,
            Tone2Decay = KickDecay * 0.8,
            ClickLevel = KickClick,
            ClickDuration = 0.003,
            ClickFreq = 3000,
            NoiseLevel = 0.1,
            NoiseDecay = 50,
            NoiseFilterFreq = 200,
            NoiseFilterQ = 1.0,
            NoiseType = NoiseType.White,
            AmpDecay = KickDecay,
            Volume = 1.0,
            Drive = KickDrive
        };
    }

    private DrumSynthParameters CreateSnareParams()
    {
        return new DrumSynthParameters
        {
            Frequency = SnareBody,
            Frequency2 = SnareBody * 1.5,
            PitchDecay = 20,
            PitchEnvAmount = 0.5,
            ToneLevel = SnareTone,
            ToneDecay = SnareDecay,
            Tone2Level = SnareTone * 0.3,
            Tone2Decay = SnareDecay * 0.8,
            ClickLevel = 0.4,
            ClickDuration = 0.002,
            ClickFreq = 4000,
            NoiseLevel = SnareSnap,
            NoiseDecay = 12,
            NoiseFilterFreq = 6000,
            NoiseFilterQ = 0.7,
            NoiseType = NoiseType.White,
            AmpDecay = SnareDecay,
            Volume = 0.9,
            Drive = 0
        };
    }

    private DrumSynthParameters CreateRimParams()
    {
        return new DrumSynthParameters
        {
            Frequency = 800,
            Frequency2 = 1200,
            PitchDecay = 50,
            PitchEnvAmount = 0.2,
            ToneLevel = 0.6,
            ToneDecay = 50,
            Tone2Level = 0.4,
            Tone2Decay = 60,
            ClickLevel = 0.8,
            ClickDuration = 0.001,
            ClickFreq = 5000,
            NoiseLevel = 0.3,
            NoiseDecay = 40,
            NoiseFilterFreq = 4000,
            NoiseFilterQ = 2.0,
            NoiseType = NoiseType.White,
            AmpDecay = 50,
            Volume = 0.6,
            Drive = 0
        };
    }

    private DrumSynthParameters CreateClapParams()
    {
        return new DrumSynthParameters
        {
            Frequency = 1000,
            Frequency2 = 0,
            PitchDecay = 5,
            PitchEnvAmount = 0,
            ToneLevel = 0.1,
            ToneDecay = 30,
            Tone2Level = 0,
            Tone2Decay = 0,
            ClickLevel = 0,
            ClickDuration = 0,
            ClickFreq = 0,
            NoiseLevel = 0.9,
            NoiseDecay = ClapDecay,
            NoiseFilterFreq = 2000,
            NoiseFilterQ = 0.5,
            NoiseType = NoiseType.White,
            AmpDecay = ClapDecay,
            Volume = 0.7,
            Drive = 0
        };
    }

    private DrumSynthParameters CreateHiHatClosedParams()
    {
        return new DrumSynthParameters
        {
            Frequency = HiHatTone,
            Frequency2 = HiHatTone * 1.414,
            PitchDecay = 0,
            PitchEnvAmount = 0,
            ToneLevel = 0.2,
            ToneDecay = HiHatClosedDecay,
            Tone2Level = 0.15,
            Tone2Decay = HiHatClosedDecay * 0.9,
            ClickLevel = 0.3,
            ClickDuration = 0.001,
            ClickFreq = 10000,
            NoiseLevel = 0.8,
            NoiseDecay = HiHatClosedDecay,
            NoiseFilterFreq = 12000,
            NoiseFilterQ = 0.3,
            NoiseType = NoiseType.Metallic,
            AmpDecay = HiHatClosedDecay,
            Volume = 0.4,
            Drive = 0
        };
    }

    private DrumSynthParameters CreateHiHatOpenParams()
    {
        return new DrumSynthParameters
        {
            Frequency = HiHatTone,
            Frequency2 = HiHatTone * 1.414,
            PitchDecay = 0,
            PitchEnvAmount = 0,
            ToneLevel = 0.25,
            ToneDecay = HiHatOpenDecay,
            Tone2Level = 0.2,
            Tone2Decay = HiHatOpenDecay * 0.9,
            ClickLevel = 0.2,
            ClickDuration = 0.001,
            ClickFreq = 10000,
            NoiseLevel = 0.7,
            NoiseDecay = HiHatOpenDecay,
            NoiseFilterFreq = 10000,
            NoiseFilterQ = 0.4,
            NoiseType = NoiseType.Metallic,
            AmpDecay = HiHatOpenDecay,
            Volume = 0.45,
            Drive = 0
        };
    }

    private DrumSynthParameters CreateTomParams(double pitchMultiplier)
    {
        return new DrumSynthParameters
        {
            Frequency = TomPitch * pitchMultiplier,
            Frequency2 = TomPitch * pitchMultiplier * 0.5,
            PitchDecay = 15,
            PitchEnvAmount = 1.5,
            ToneLevel = 0.9,
            ToneDecay = TomDecay,
            Tone2Level = 0.3,
            Tone2Decay = TomDecay * 0.7,
            ClickLevel = 0.2,
            ClickDuration = 0.002,
            ClickFreq = 2000,
            NoiseLevel = 0.15,
            NoiseDecay = 20,
            NoiseFilterFreq = 1000,
            NoiseFilterQ = 1.0,
            NoiseType = NoiseType.White,
            AmpDecay = TomDecay,
            Volume = 0.85,
            Drive = 0
        };
    }

    private DrumSynthParameters CreateCrashParams()
    {
        return new DrumSynthParameters
        {
            Frequency = 5000,
            Frequency2 = 7000,
            PitchDecay = 0.5,
            PitchEnvAmount = 0,
            ToneLevel = 0.2,
            ToneDecay = 2,
            Tone2Level = 0.15,
            Tone2Decay = 2.5,
            ClickLevel = 0.3,
            ClickDuration = 0.002,
            ClickFreq = 8000,
            NoiseLevel = 0.9,
            NoiseDecay = 1.5,
            NoiseFilterFreq = 8000,
            NoiseFilterQ = 0.3,
            NoiseType = NoiseType.Metallic,
            AmpDecay = 1.5,
            Volume = 0.5,
            Drive = 0
        };
    }

    private DrumSynthParameters CreateRideParams()
    {
        return new DrumSynthParameters
        {
            Frequency = 6000,
            Frequency2 = 8500,
            PitchDecay = 0.3,
            PitchEnvAmount = 0,
            ToneLevel = 0.35,
            ToneDecay = 3,
            Tone2Level = 0.25,
            Tone2Decay = 3.5,
            ClickLevel = 0.4,
            ClickDuration = 0.001,
            ClickFreq = 9000,
            NoiseLevel = 0.5,
            NoiseDecay = 2.5,
            NoiseFilterFreq = 7000,
            NoiseFilterQ = 0.5,
            NoiseType = NoiseType.Metallic,
            AmpDecay = 2.5,
            Volume = 0.45,
            Drive = 0
        };
    }

    private DrumSynthParameters CreateCowbellParams()
    {
        return new DrumSynthParameters
        {
            Frequency = 560,
            Frequency2 = 845,
            PitchDecay = 0,
            PitchEnvAmount = 0,
            ToneLevel = 0.7,
            ToneDecay = 15,
            Tone2Level = 0.5,
            Tone2Decay = 12,
            ClickLevel = 0.2,
            ClickDuration = 0.001,
            ClickFreq = 2000,
            NoiseLevel = 0.1,
            NoiseDecay = 30,
            NoiseFilterFreq = 1000,
            NoiseFilterQ = 2.0,
            NoiseType = NoiseType.White,
            AmpDecay = 15,
            Volume = 0.5,
            Drive = 0.1
        };
    }

    /// <summary>
    /// Triggers a drum sound by MIDI note number.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        if (velocity == 0)
        {
            NoteOff(note);
            return;
        }

        // Update parameters based on current settings
        UpdateDrumParameters();

        lock (_lock)
        {
            // Check for choke group
            if (_chokeGroups.TryGetValue(note, out int chokeGroup))
            {
                foreach (var v in _voices)
                {
                    if (v.IsActive && v.ChokeGroup == chokeGroup)
                    {
                        v.Choke();
                    }
                }
            }

            // Find drum parameters
            if (!_drumMap.TryGetValue(note, out var prms))
            {
                // Default to tom-like sound for unknown notes
                prms = CreateTomParams(1.0);
            }

            // Find a free voice
            DrumSynthVoice? voice = null;
            foreach (var v in _voices)
            {
                if (!v.IsActive)
                {
                    voice = v;
                    break;
                }
            }

            // Voice stealing if needed
            voice ??= _voices[0];

            int cg = _chokeGroups.TryGetValue(note, out int g) ? g : 0;
            voice.Trigger(prms, velocity, cg);
        }
    }

    /// <summary>
    /// Note off (drums typically ignore this, but used for choke groups).
    /// </summary>
    public void NoteOff(int note)
    {
        // Drums are one-shot, but hi-hats can be choked
        if (_chokeGroups.TryGetValue(note, out int chokeGroup))
        {
            lock (_lock)
            {
                foreach (var v in _voices)
                {
                    if (v.IsActive && v.ChokeGroup == chokeGroup)
                    {
                        v.Choke();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Stops all playing sounds.
    /// </summary>
    public void AllNotesOff()
    {
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                voice.Choke();
            }
        }
    }

    /// <summary>
    /// Sets a parameter by name.
    /// </summary>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume": Volume = Math.Clamp(value, 0f, 1f); break;
            case "kickpitch": KickPitch = Math.Clamp(value, 20f, 200f); break;
            case "kickpitchdecay": KickPitchDecay = Math.Clamp(value, 1f, 100f); break;
            case "kickpitchamount": KickPitchAmount = Math.Clamp(value, 0f, 10f); break;
            case "kickclick": KickClick = Math.Clamp(value, 0f, 1f); break;
            case "kicksub": KickSub = Math.Clamp(value, 0f, 1f); break;
            case "kickdecay": KickDecay = Math.Clamp(value, 1f, 50f); break;
            case "kickdrive": KickDrive = Math.Clamp(value, 0f, 1f); break;
            case "snarebody": SnareBody = Math.Clamp(value, 100f, 400f); break;
            case "snaresnap": SnareSnap = Math.Clamp(value, 0f, 1f); break;
            case "snaretone": SnareTone = Math.Clamp(value, 0f, 1f); break;
            case "snaredecay": SnareDecay = Math.Clamp(value, 5f, 50f); break;
            case "hihattone": HiHatTone = Math.Clamp(value, 4000f, 16000f); break;
            case "hihatcloseddecay": HiHatClosedDecay = Math.Clamp(value, 10f, 100f); break;
            case "hihatopendecay": HiHatOpenDecay = Math.Clamp(value, 2f, 30f); break;
            case "clapdecay": ClapDecay = Math.Clamp(value, 5f, 50f); break;
            case "claplayers": ClapLayers = Math.Clamp((int)value, 1, 8); break;
            case "tompitch": TomPitch = Math.Clamp(value, 50f, 300f); break;
            case "tomdecay": TomDecay = Math.Clamp(value, 3f, 30f); break;
        }
    }

    private void UpdateDrumParameters()
    {
        _drumMap[NoteKick] = CreateKickParams();
        _drumMap[NoteSnare] = CreateSnareParams();
        _drumMap[NoteSnare2] = CreateSnareParams();
        _drumMap[NoteClap] = CreateClapParams();
        _drumMap[NoteHiHatClosed] = CreateHiHatClosedParams();
        _drumMap[NoteHiHatOpen] = CreateHiHatOpenParams();
        _drumMap[NoteHiHatPedal] = CreateHiHatClosedParams();
        _drumMap[NoteTomHigh] = CreateTomParams(1.5);
        _drumMap[NoteTomMid] = CreateTomParams(1.0);
        _drumMap[NoteTomLow] = CreateTomParams(0.7);
    }

    /// <summary>
    /// Reads audio samples.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        // Clear buffer
        Array.Clear(buffer, offset, count);

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

                // Apply master volume and soft clipping
                sample *= Volume;
                sample = MathF.Tanh(sample);

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

        return count;
    }

    /// <summary>
    /// Plays a drum sound by type.
    /// </summary>
    /// <param name="sound">The drum sound to play.</param>
    /// <param name="velocity">Velocity (0-127).</param>
    public void Play(DrumSound sound, int velocity = 100)
    {
        int note = sound switch
        {
            DrumSound.Kick => NoteKick,
            DrumSound.Snare => NoteSnare,
            DrumSound.HiHatClosed => NoteHiHatClosed,
            DrumSound.HiHatOpen => NoteHiHatOpen,
            DrumSound.Clap => NoteClap,
            DrumSound.TomHigh => NoteTomHigh,
            DrumSound.TomMid => NoteTomMid,
            DrumSound.TomLow => NoteTomLow,
            DrumSound.Rim => NoteRim,
            DrumSound.Cowbell => NoteCowbell,
            DrumSound.Crash => NoteCrash,
            DrumSound.Ride => NoteRide,
            _ => NoteKick
        };

        NoteOn(note, velocity);
    }

    /// <summary>
    /// Creates a preset: 808-style drum kit.
    /// </summary>
    public static DrumSynth Create808Preset(int? sampleRate = null)
    {
        var synth = new DrumSynth(sampleRate)
        {
            Name = "808",
            KickPitch = 45f,
            KickPitchDecay = 25f,
            KickPitchAmount = 4f,
            KickClick = 0.2f,
            KickSub = 0.7f,
            KickDecay = 6f,
            KickDrive = 0.1f,
            SnareBody = 180f,
            SnareSnap = 0.7f,
            SnareTone = 0.4f,
            SnareDecay = 12f,
            HiHatTone = 9000f,
            HiHatClosedDecay = 50f,
            HiHatOpenDecay = 10f,
            ClapDecay = 20f
        };
        return synth;
    }

    /// <summary>
    /// Creates a preset: 909-style drum kit.
    /// </summary>
    public static DrumSynth Create909Preset(int? sampleRate = null)
    {
        var synth = new DrumSynth(sampleRate)
        {
            Name = "909",
            KickPitch = 55f,
            KickPitchDecay = 35f,
            KickPitchAmount = 3f,
            KickClick = 0.4f,
            KickSub = 0.4f,
            KickDecay = 8f,
            KickDrive = 0.3f,
            SnareBody = 220f,
            SnareSnap = 0.5f,
            SnareTone = 0.6f,
            SnareDecay = 18f,
            HiHatTone = 7500f,
            HiHatClosedDecay = 35f,
            HiHatOpenDecay = 6f,
            ClapDecay = 15f
        };
        return synth;
    }

    /// <summary>
    /// Creates a preset: Acoustic-style drum kit.
    /// </summary>
    public static DrumSynth CreateAcousticPreset(int? sampleRate = null)
    {
        var synth = new DrumSynth(sampleRate)
        {
            Name = "Acoustic",
            KickPitch = 65f,
            KickPitchDecay = 20f,
            KickPitchAmount = 2f,
            KickClick = 0.5f,
            KickSub = 0.3f,
            KickDecay = 10f,
            KickDrive = 0f,
            SnareBody = 250f,
            SnareSnap = 0.4f,
            SnareTone = 0.7f,
            SnareDecay = 20f,
            HiHatTone = 6000f,
            HiHatClosedDecay = 30f,
            HiHatOpenDecay = 5f,
            TomPitch = 120f,
            TomDecay = 12f
        };
        return synth;
    }
}
