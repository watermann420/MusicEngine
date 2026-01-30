//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Retro 8-bit chiptune synthesizer emulating classic sound chips (NES, C64, Game Boy).
// Features pulse/square waves with variable duty cycle, triangle waves, multiple noise types,
// bit-crushing, hardware-accurate arpeggio, and classic envelope shapes.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// Chiptune waveform types matching classic sound chip capabilities.
/// </summary>
public enum ChipWaveform
{
    /// <summary>Pulse wave with 12.5% duty cycle (NES-style).</summary>
    Pulse12,
    /// <summary>Pulse wave with 25% duty cycle (NES-style).</summary>
    Pulse25,
    /// <summary>Pulse wave with 50% duty cycle (square wave).</summary>
    Pulse50,
    /// <summary>Pulse wave with 75% duty cycle.</summary>
    Pulse75,
    /// <summary>Pulse wave with variable duty cycle (0-100%).</summary>
    PulseVariable,
    /// <summary>Triangle wave (NES APU style, 4-bit quantized).</summary>
    Triangle,
    /// <summary>Noise channel.</summary>
    Noise
}

/// <summary>
/// Noise type for the noise channel, emulating different chip noise generators.
/// </summary>
public enum ChipNoiseType
{
    /// <summary>White noise (15-bit LFSR, NES-style).</summary>
    White,
    /// <summary>Periodic/metallic noise (short loop, NES mode 1).</summary>
    Periodic,
    /// <summary>Buzzy noise (Game Boy style).</summary>
    Buzzy,
    /// <summary>Smooth noise (low-frequency filtered).</summary>
    Smooth
}

/// <summary>
/// Chip emulation mode affecting sound characteristics.
/// </summary>
public enum ChipEmulationMode
{
    /// <summary>Generic chiptune (balanced characteristics).</summary>
    Generic,
    /// <summary>NES/Famicom 2A03 APU style.</summary>
    NES,
    /// <summary>Game Boy DMG style.</summary>
    GameBoy,
    /// <summary>Commodore 64 SID-inspired (simplified).</summary>
    C64
}

/// <summary>
/// Internal voice state for polyphonic chiptune playback.
/// </summary>
internal class ChipVoiceState
{
    private readonly int _sampleRate;
    private readonly ChipTuneSynth _synth;

    // Oscillator state
    private double _phase;
    private uint _lfsr = 0x7FFF; // 15-bit LFSR for noise
    private double _noiseValue;
    private int _noiseCounter;

    // Envelope state
    private double _envelope;
    private int _envStage; // 0=idle, 1=attack, 2=decay, 3=sustain, 4=release
    private double _envTime;

    // Arpeggio state
    private int _arpeggioIndex;
    private double _arpeggioTimer;
    private int[] _arpeggioNotes = Array.Empty<int>();

    // Vibrato/pitch modulation
    private double _vibratoPhase;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public bool IsActive { get; private set; }
    public double Frequency { get; private set; }

    public ChipVoiceState(int sampleRate, ChipTuneSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;
    }

    public void Trigger(int note, int velocity, int[]? arpeggioNotes = null)
    {
        Note = note;
        Velocity = velocity;
        IsActive = true;

        // Convert MIDI note to frequency
        Frequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);

        // Reset envelope
        _envStage = 1; // Attack
        _envTime = 0;
        _envelope = 0;

        // Reset phase for clean attack (except noise)
        if (_synth.Waveform != ChipWaveform.Noise)
        {
            _phase = 0;
        }

        // Setup arpeggio
        _arpeggioNotes = arpeggioNotes ?? Array.Empty<int>();
        _arpeggioIndex = 0;
        _arpeggioTimer = 0;

        // Reset vibrato
        _vibratoPhase = 0;
    }

    public void Release()
    {
        if (_envStage > 0 && _envStage < 4)
        {
            _envStage = 4; // Release
        }
    }

    public void Stop()
    {
        IsActive = false;
        _envStage = 0;
        _envelope = 0;
    }

    public float Process(double deltaTime)
    {
        if (!IsActive) return 0f;

        // Process envelope
        ProcessEnvelope(deltaTime);

        if (_envStage == 0)
        {
            IsActive = false;
            return 0f;
        }

        // Calculate current frequency (with arpeggio and vibrato)
        double freq = GetModulatedFrequency(deltaTime);

        // Generate waveform
        double sample = GenerateWaveform(freq, deltaTime);

        // Apply bit crushing
        sample = ApplyBitCrush(sample);

        // Apply envelope and velocity
        double velocityGain = Velocity / 127.0;
        float output = (float)(sample * _envelope * velocityGain);

        return output;
    }

    private double GetModulatedFrequency(double deltaTime)
    {
        double freq = Frequency;

        // Arpeggio
        if (_arpeggioNotes.Length > 0 && _synth.ArpeggioSpeed > 0)
        {
            _arpeggioTimer += deltaTime;
            double arpeggioInterval = 1.0 / _synth.ArpeggioSpeed;

            if (_arpeggioTimer >= arpeggioInterval)
            {
                _arpeggioTimer -= arpeggioInterval;
                _arpeggioIndex = (_arpeggioIndex + 1) % _arpeggioNotes.Length;
            }

            int arpeggioNote = Note + _arpeggioNotes[_arpeggioIndex];
            freq = 440.0 * Math.Pow(2.0, (arpeggioNote - 69.0) / 12.0);
        }
        else if (_synth.ArpeggioSpeed > 0 && _synth.ArpeggioSemitones > 0)
        {
            // Simple two-note arpeggio using ArpeggioSemitones
            _arpeggioTimer += deltaTime;
            double arpeggioInterval = 1.0 / _synth.ArpeggioSpeed;

            if (_arpeggioTimer >= arpeggioInterval)
            {
                _arpeggioTimer -= arpeggioInterval;
                _arpeggioIndex = 1 - _arpeggioIndex; // Toggle between 0 and 1
            }

            if (_arpeggioIndex == 1)
            {
                int arpeggioNote = Note + _synth.ArpeggioSemitones;
                freq = 440.0 * Math.Pow(2.0, (arpeggioNote - 69.0) / 12.0);
            }
        }

        // Vibrato
        if (_synth.VibratoDepth > 0 && _synth.VibratoSpeed > 0)
        {
            _vibratoPhase += deltaTime * _synth.VibratoSpeed * 2.0 * Math.PI;
            double vibratoMod = Math.Sin(_vibratoPhase) * _synth.VibratoDepth;
            freq *= Math.Pow(2.0, vibratoMod / 12.0);
        }

        // Pitch slide/bend
        if (_synth.PitchBend != 0)
        {
            freq *= Math.Pow(2.0, _synth.PitchBend / 12.0);
        }

        return freq;
    }

    private double GenerateWaveform(double freq, double deltaTime)
    {
        double phaseIncrement = freq / _sampleRate;

        switch (_synth.Waveform)
        {
            case ChipWaveform.Pulse12:
                return GeneratePulse(0.125, phaseIncrement);

            case ChipWaveform.Pulse25:
                return GeneratePulse(0.25, phaseIncrement);

            case ChipWaveform.Pulse50:
                return GeneratePulse(0.5, phaseIncrement);

            case ChipWaveform.Pulse75:
                return GeneratePulse(0.75, phaseIncrement);

            case ChipWaveform.PulseVariable:
                return GeneratePulse(_synth.PulseWidth, phaseIncrement);

            case ChipWaveform.Triangle:
                return GenerateTriangle(phaseIncrement);

            case ChipWaveform.Noise:
                return GenerateNoise(freq, deltaTime);

            default:
                return 0;
        }
    }

    private double GeneratePulse(double dutyCycle, double phaseIncrement)
    {
        _phase += phaseIncrement;
        if (_phase >= 1.0) _phase -= 1.0;

        // NES-style: no anti-aliasing, hard edges
        return _phase < dutyCycle ? 1.0 : -1.0;
    }

    private double GenerateTriangle(double phaseIncrement)
    {
        _phase += phaseIncrement;
        if (_phase >= 1.0) _phase -= 1.0;

        // NES triangle is 4-bit quantized (16 steps)
        double tri;
        if (_phase < 0.5)
        {
            tri = _phase * 4.0 - 1.0; // -1 to 1 over first half
        }
        else
        {
            tri = 3.0 - _phase * 4.0; // 1 to -1 over second half
        }

        // Quantize to 4-bit (16 levels) for NES authenticity
        if (_synth.EmulationMode == ChipEmulationMode.NES ||
            _synth.EmulationMode == ChipEmulationMode.GameBoy)
        {
            tri = Math.Floor(tri * 7.5 + 0.5) / 7.5;
        }

        return tri;
    }

    private double GenerateNoise(double freq, double deltaTime)
    {
        // Update noise based on frequency
        int samplesPerUpdate = Math.Max(1, (int)(_sampleRate / (freq * 2)));
        _noiseCounter++;

        if (_noiseCounter >= samplesPerUpdate)
        {
            _noiseCounter = 0;
            _noiseValue = GenerateNoiseValue();
        }

        return _noiseValue;
    }

    private double GenerateNoiseValue()
    {
        switch (_synth.NoiseType)
        {
            case ChipNoiseType.White:
                // 15-bit LFSR (NES-style long mode)
                {
                    uint bit = ((_lfsr >> 0) ^ (_lfsr >> 1)) & 1;
                    _lfsr = (_lfsr >> 1) | (bit << 14);
                    return (_lfsr & 1) == 0 ? 1.0 : -1.0;
                }

            case ChipNoiseType.Periodic:
                // 15-bit LFSR with bits 0 and 6 (NES-style short/periodic mode)
                {
                    uint bit = ((_lfsr >> 0) ^ (_lfsr >> 6)) & 1;
                    _lfsr = (_lfsr >> 1) | (bit << 14);
                    return (_lfsr & 1) == 0 ? 1.0 : -1.0;
                }

            case ChipNoiseType.Buzzy:
                // Game Boy style - 7-bit LFSR
                {
                    uint bit = ((_lfsr >> 0) ^ (_lfsr >> 1)) & 1;
                    _lfsr = ((_lfsr >> 1) & 0x3F) | (bit << 6);
                    return (_lfsr & 1) == 0 ? 1.0 : -1.0;
                }

            case ChipNoiseType.Smooth:
                // Filtered noise - interpolated
                {
                    uint bit = ((_lfsr >> 0) ^ (_lfsr >> 1)) & 1;
                    _lfsr = (_lfsr >> 1) | (bit << 14);
                    double target = (_lfsr & 1) == 0 ? 1.0 : -1.0;
                    _noiseValue = _noiseValue * 0.9 + target * 0.1;
                    return _noiseValue;
                }

            default:
                return 0;
        }
    }

    private double ApplyBitCrush(double sample)
    {
        int bitDepth = _synth.BitDepth;
        if (bitDepth >= 16) return sample; // No crushing needed

        // Quantize to specified bit depth
        int levels = 1 << bitDepth;
        double step = 2.0 / levels;
        sample = Math.Floor((sample + 1.0) / step) * step - 1.0;

        return Math.Clamp(sample, -1.0, 1.0);
    }

    private void ProcessEnvelope(double deltaTime)
    {
        _envTime += deltaTime;

        switch (_envStage)
        {
            case 1: // Attack
                if (_synth.Attack <= 0)
                {
                    _envelope = 1.0;
                    _envStage = 2;
                    _envTime = 0;
                }
                else
                {
                    _envelope = _envTime / _synth.Attack;
                    if (_envelope >= 1.0)
                    {
                        _envelope = 1.0;
                        _envStage = 2;
                        _envTime = 0;
                    }
                }
                break;

            case 2: // Decay
                if (_synth.Decay <= 0)
                {
                    _envelope = _synth.Sustain;
                    _envStage = 3;
                }
                else
                {
                    double decayProgress = _envTime / _synth.Decay;
                    _envelope = 1.0 - (1.0 - _synth.Sustain) * decayProgress;
                    if (_envelope <= _synth.Sustain)
                    {
                        _envelope = _synth.Sustain;
                        _envStage = 3;
                    }
                }
                break;

            case 3: // Sustain
                _envelope = _synth.Sustain;
                break;

            case 4: // Release
                if (_synth.Release <= 0)
                {
                    _envelope = 0;
                    _envStage = 0;
                }
                else
                {
                    double releaseProgress = _envTime / _synth.Release;
                    _envelope = _synth.Sustain * (1.0 - releaseProgress);
                    if (_envelope <= 0.001)
                    {
                        _envelope = 0;
                        _envStage = 0;
                    }
                }
                break;
        }
    }
}

/// <summary>
/// Retro 8-bit chiptune synthesizer emulating classic sound chips (NES, C64, Game Boy).
/// Features pulse/square waves with variable duty cycle, triangle waves, multiple noise types,
/// bit-crushing, hardware-accurate arpeggio, and classic envelope shapes.
/// </summary>
public class ChipTuneSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<ChipVoiceState> _voices = new();
    private readonly Dictionary<int, ChipVoiceState> _noteToVoice = new();
    private readonly object _lock = new();
    private const int MaxVoices = 8;

    /// <summary>Gets or sets the synth name.</summary>
    public string Name { get; set; } = "ChipTuneSynth";

    /// <summary>Gets the audio format.</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Gets or sets the master volume (0-1).</summary>
    public float Volume { get; set; } = 0.7f;

    /// <summary>Gets or sets the waveform type.</summary>
    public ChipWaveform Waveform { get; set; } = ChipWaveform.Pulse50;

    /// <summary>Gets or sets the pulse width for variable pulse wave (0-1, default 0.5).</summary>
    public double PulseWidth { get; set; } = 0.5;

    /// <summary>Gets or sets the bit depth for bit-crushing (4-16, default 8).</summary>
    public int BitDepth { get; set; } = 8;

    /// <summary>Gets or sets the noise type.</summary>
    public ChipNoiseType NoiseType { get; set; } = ChipNoiseType.White;

    /// <summary>Gets or sets the chip emulation mode.</summary>
    public ChipEmulationMode EmulationMode { get; set; } = ChipEmulationMode.Generic;

    /// <summary>Gets or sets the arpeggio speed in Hz (0 = disabled).</summary>
    public double ArpeggioSpeed { get; set; } = 0;

    /// <summary>Gets or sets the arpeggio interval in semitones for simple arpeggio.</summary>
    public int ArpeggioSemitones { get; set; } = 0;

    /// <summary>Gets or sets the vibrato depth in semitones.</summary>
    public double VibratoDepth { get; set; } = 0;

    /// <summary>Gets or sets the vibrato speed in Hz.</summary>
    public double VibratoSpeed { get; set; } = 5;

    /// <summary>Gets or sets the pitch bend in semitones.</summary>
    public double PitchBend { get; set; } = 0;

    // ADSR Envelope parameters
    /// <summary>Attack time in seconds.</summary>
    public double Attack { get; set; } = 0.01;

    /// <summary>Decay time in seconds.</summary>
    public double Decay { get; set; } = 0.1;

    /// <summary>Sustain level (0-1).</summary>
    public double Sustain { get; set; } = 0.7;

    /// <summary>Release time in seconds.</summary>
    public double Release { get; set; } = 0.1;

    /// <summary>
    /// Creates a new ChipTuneSynth.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (default: from Settings).</param>
    public ChipTuneSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize voice pool
        for (int i = 0; i < MaxVoices; i++)
        {
            _voices.Add(new ChipVoiceState(rate, this));
        }
    }

    /// <summary>
    /// Triggers a note.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateVelocity(velocity);

        if (velocity == 0)
        {
            NoteOff(note);
            return;
        }

        lock (_lock)
        {
            // Check if note is already playing
            if (_noteToVoice.TryGetValue(note, out var existingVoice))
            {
                existingVoice.Trigger(note, velocity);
                return;
            }

            // Find free voice
            ChipVoiceState? voice = null;
            foreach (var v in _voices)
            {
                if (!v.IsActive)
                {
                    voice = v;
                    break;
                }
            }

            // Voice stealing: find oldest voice
            if (voice == null)
            {
                voice = _voices[0];
                // Remove old note mapping
                int? oldNote = null;
                foreach (var kvp in _noteToVoice)
                {
                    if (kvp.Value == voice)
                    {
                        oldNote = kvp.Key;
                        break;
                    }
                }
                if (oldNote.HasValue)
                {
                    _noteToVoice.Remove(oldNote.Value);
                }
            }

            voice.Trigger(note, velocity);
            _noteToVoice[note] = voice;
        }
    }

    /// <summary>
    /// Triggers a note with custom arpeggio pattern.
    /// </summary>
    /// <param name="note">MIDI note number.</param>
    /// <param name="velocity">Velocity (0-127).</param>
    /// <param name="arpeggioPattern">Array of semitone offsets for arpeggio (e.g., [0, 4, 7] for major chord).</param>
    public void NoteOnWithArpeggio(int note, int velocity, int[] arpeggioPattern)
    {
        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateVelocity(velocity);

        if (velocity == 0)
        {
            NoteOff(note);
            return;
        }

        lock (_lock)
        {
            // Check if note is already playing
            if (_noteToVoice.TryGetValue(note, out var existingVoice))
            {
                existingVoice.Trigger(note, velocity, arpeggioPattern);
                return;
            }

            // Find free voice
            ChipVoiceState? voice = null;
            foreach (var v in _voices)
            {
                if (!v.IsActive)
                {
                    voice = v;
                    break;
                }
            }

            // Voice stealing
            if (voice == null)
            {
                voice = _voices[0];
                int? oldNote = null;
                foreach (var kvp in _noteToVoice)
                {
                    if (kvp.Value == voice)
                    {
                        oldNote = kvp.Key;
                        break;
                    }
                }
                if (oldNote.HasValue)
                {
                    _noteToVoice.Remove(oldNote.Value);
                }
            }

            voice.Trigger(note, velocity, arpeggioPattern);
            _noteToVoice[note] = voice;
        }
    }

    /// <summary>
    /// Releases a note.
    /// </summary>
    public void NoteOff(int note)
    {
        MidiValidation.ValidateNote(note);

        lock (_lock)
        {
            if (_noteToVoice.TryGetValue(note, out var voice))
            {
                voice.Release();
                _noteToVoice.Remove(note);
            }
        }
    }

    /// <summary>
    /// Releases all notes.
    /// </summary>
    public void AllNotesOff()
    {
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                voice.Stop();
            }
            _noteToVoice.Clear();
        }
    }

    /// <summary>
    /// Sets a parameter by name.
    /// </summary>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume":
                Volume = Math.Clamp(value, 0f, 1f);
                break;
            case "waveform":
                Waveform = (ChipWaveform)(int)Math.Clamp(value, 0, 6);
                break;
            case "pulsewidth":
                PulseWidth = Math.Clamp(value, 0.0, 1.0);
                break;
            case "bitdepth":
                BitDepth = Math.Clamp((int)value, 4, 16);
                break;
            case "noisetype":
                NoiseType = (ChipNoiseType)(int)Math.Clamp(value, 0, 3);
                break;
            case "emulationmode":
                EmulationMode = (ChipEmulationMode)(int)Math.Clamp(value, 0, 3);
                break;
            case "arpeggiospeed":
                ArpeggioSpeed = Math.Max(0, value);
                break;
            case "arpeggiosemitones":
                ArpeggioSemitones = Math.Clamp((int)value, 0, 24);
                break;
            case "vibratodepth":
                VibratoDepth = Math.Max(0, value);
                break;
            case "vibratospeed":
                VibratoSpeed = Math.Max(0, value);
                break;
            case "pitchbend":
                PitchBend = Math.Clamp(value, -24.0, 24.0);
                break;
            case "attack":
                Attack = Math.Max(0, value);
                break;
            case "decay":
                Decay = Math.Max(0, value);
                break;
            case "sustain":
                Sustain = Math.Clamp(value, 0.0, 1.0);
                break;
            case "release":
                Release = Math.Max(0, value);
                break;
        }
    }

    /// <summary>
    /// Reads audio samples.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);

        int channels = _waveFormat.Channels;
        double deltaTime = 1.0 / _waveFormat.SampleRate;

        lock (_lock)
        {
            for (int n = 0; n < count; n += channels)
            {
                float sample = 0f;

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

    #region Presets

    /// <summary>Creates a classic NES pulse lead preset.</summary>
    public static ChipTuneSynth CreateNESLead(int? sampleRate = null)
    {
        return new ChipTuneSynth(sampleRate)
        {
            Name = "NES Lead",
            Waveform = ChipWaveform.Pulse25,
            BitDepth = 8,
            EmulationMode = ChipEmulationMode.NES,
            Attack = 0.01,
            Decay = 0.1,
            Sustain = 0.8,
            Release = 0.15
        };
    }

    /// <summary>Creates a classic NES bass preset.</summary>
    public static ChipTuneSynth CreateNESBass(int? sampleRate = null)
    {
        return new ChipTuneSynth(sampleRate)
        {
            Name = "NES Bass",
            Waveform = ChipWaveform.Triangle,
            BitDepth = 4,
            EmulationMode = ChipEmulationMode.NES,
            Attack = 0.005,
            Decay = 0.2,
            Sustain = 0.6,
            Release = 0.1
        };
    }

    /// <summary>Creates a classic NES arpeggio lead preset.</summary>
    public static ChipTuneSynth CreateNESArpeggio(int? sampleRate = null)
    {
        return new ChipTuneSynth(sampleRate)
        {
            Name = "NES Arpeggio",
            Waveform = ChipWaveform.Pulse50,
            BitDepth = 8,
            EmulationMode = ChipEmulationMode.NES,
            ArpeggioSpeed = 20,
            ArpeggioSemitones = 12,
            Attack = 0.005,
            Decay = 0.05,
            Sustain = 0.9,
            Release = 0.1
        };
    }

    /// <summary>Creates a Game Boy style lead preset.</summary>
    public static ChipTuneSynth CreateGameBoyLead(int? sampleRate = null)
    {
        return new ChipTuneSynth(sampleRate)
        {
            Name = "Game Boy Lead",
            Waveform = ChipWaveform.Pulse25,
            BitDepth = 4,
            EmulationMode = ChipEmulationMode.GameBoy,
            Attack = 0.01,
            Decay = 0.15,
            Sustain = 0.7,
            Release = 0.2
        };
    }

    /// <summary>Creates a C64 style bass preset.</summary>
    public static ChipTuneSynth CreateC64Bass(int? sampleRate = null)
    {
        return new ChipTuneSynth(sampleRate)
        {
            Name = "C64 Bass",
            Waveform = ChipWaveform.PulseVariable,
            PulseWidth = 0.3,
            BitDepth = 8,
            EmulationMode = ChipEmulationMode.C64,
            Attack = 0.005,
            Decay = 0.3,
            Sustain = 0.4,
            Release = 0.15
        };
    }

    /// <summary>Creates a noise percussion preset (hi-hat style).</summary>
    public static ChipTuneSynth CreateNoiseHat(int? sampleRate = null)
    {
        return new ChipTuneSynth(sampleRate)
        {
            Name = "Noise Hat",
            Waveform = ChipWaveform.Noise,
            NoiseType = ChipNoiseType.Periodic,
            BitDepth = 8,
            Attack = 0.001,
            Decay = 0.05,
            Sustain = 0.0,
            Release = 0.05
        };
    }

    /// <summary>Creates a noise snare preset.</summary>
    public static ChipTuneSynth CreateNoiseSnare(int? sampleRate = null)
    {
        return new ChipTuneSynth(sampleRate)
        {
            Name = "Noise Snare",
            Waveform = ChipWaveform.Noise,
            NoiseType = ChipNoiseType.White,
            BitDepth = 6,
            Attack = 0.001,
            Decay = 0.15,
            Sustain = 0.0,
            Release = 0.1
        };
    }

    /// <summary>Creates a vibrato lead preset.</summary>
    public static ChipTuneSynth CreateVibratoLead(int? sampleRate = null)
    {
        return new ChipTuneSynth(sampleRate)
        {
            Name = "Vibrato Lead",
            Waveform = ChipWaveform.Pulse50,
            BitDepth = 8,
            EmulationMode = ChipEmulationMode.Generic,
            VibratoDepth = 0.3,
            VibratoSpeed = 6,
            Attack = 0.02,
            Decay = 0.1,
            Sustain = 0.8,
            Release = 0.2
        };
    }

    /// <summary>Creates a lo-fi crushed preset (extreme bit reduction).</summary>
    public static ChipTuneSynth CreateLoFiCrushed(int? sampleRate = null)
    {
        return new ChipTuneSynth(sampleRate)
        {
            Name = "Lo-Fi Crushed",
            Waveform = ChipWaveform.Pulse50,
            BitDepth = 4,
            EmulationMode = ChipEmulationMode.Generic,
            Attack = 0.01,
            Decay = 0.2,
            Sustain = 0.6,
            Release = 0.15
        };
    }

    /// <summary>Creates a duty cycle sweep lead preset.</summary>
    public static ChipTuneSynth CreateDutySweep(int? sampleRate = null)
    {
        return new ChipTuneSynth(sampleRate)
        {
            Name = "Duty Sweep",
            Waveform = ChipWaveform.PulseVariable,
            PulseWidth = 0.1,
            BitDepth = 8,
            EmulationMode = ChipEmulationMode.NES,
            Attack = 0.01,
            Decay = 0.1,
            Sustain = 0.8,
            Release = 0.2
        };
    }

    #endregion
}
