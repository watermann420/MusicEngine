// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Synthesizer component.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// SID chip revision with different filter characteristics.
/// </summary>
public enum SIDRevision
{
    /// <summary>6581 - Original chip with darker filter, distortion characteristics.</summary>
    MOS6581,
    /// <summary>8580 - Later revision with cleaner filter, less distortion.</summary>
    MOS8580
}

/// <summary>
/// SID waveform types (can be combined).
/// </summary>
[Flags]
public enum SIDWaveform
{
    /// <summary>No waveform.</summary>
    None = 0,
    /// <summary>Triangle wave.</summary>
    Triangle = 1,
    /// <summary>Sawtooth wave.</summary>
    Sawtooth = 2,
    /// <summary>Pulse/Square wave with variable duty cycle.</summary>
    Pulse = 4,
    /// <summary>Noise (LFSR-based).</summary>
    Noise = 8
}

/// <summary>
/// SID filter modes.
/// </summary>
[Flags]
public enum SIDFilterMode
{
    /// <summary>Filter bypassed.</summary>
    Off = 0,
    /// <summary>Low-pass filter.</summary>
    LowPass = 1,
    /// <summary>Band-pass filter.</summary>
    BandPass = 2,
    /// <summary>High-pass filter.</summary>
    HighPass = 4
}

/// <summary>
/// SID oscillator parameters.
/// </summary>
public class SIDOscillator
{
    /// <summary>Waveform selection.</summary>
    public SIDWaveform Waveform { get; set; } = SIDWaveform.Pulse;
    /// <summary>Pulse width (0-4095, representing duty cycle).</summary>
    public int PulseWidth { get; set; } = 2048;
    /// <summary>Ring modulation enabled (modulates with previous oscillator).</summary>
    public bool RingMod { get; set; } = false;
    /// <summary>Hard sync enabled (syncs to previous oscillator).</summary>
    public bool HardSync { get; set; } = false;
    /// <summary>Route through filter.</summary>
    public bool FilterEnable { get; set; } = true;
    /// <summary>Attack rate (0-15).</summary>
    public int Attack { get; set; } = 2;
    /// <summary>Decay rate (0-15).</summary>
    public int Decay { get; set; } = 4;
    /// <summary>Sustain level (0-15).</summary>
    public int Sustain { get; set; } = 8;
    /// <summary>Release rate (0-15).</summary>
    public int Release { get; set; } = 4;
}

/// <summary>
/// Internal SID voice state.
/// </summary>
internal class SIDVoiceState
{
    private readonly int _sampleRate;
    private readonly SIDSynth _synth;

    // Oscillator state
    private uint _accumulator;
    private uint _shiftRegister = 0x7FFFF8; // LFSR for noise
    private bool _lastMsb;

    // Envelope state
    private double _envelope;
    private int _envStage; // 0=idle, 1=attack, 2=decay, 3=sustain, 4=release
    private double _envCounter;

    // Filter state
    private double _filterLow;
    private double _filterBand;
    private double _filterHigh;

    public int OscIndex { get; }
    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public uint Frequency { get; private set; }

    // For sync/ring mod access
    public uint Accumulator => _accumulator;
    public bool Msb => (_accumulator & 0x800000) != 0;

    public SIDVoiceState(int sampleRate, SIDSynth synth, int oscIndex)
    {
        _sampleRate = sampleRate;
        _synth = synth;
        OscIndex = oscIndex;
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        IsActive = true;
        TriggerTime = DateTime.Now;

        // Convert MIDI note to SID frequency register value
        double freq = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);
        // SID frequency register: F = (Fout * 16777216) / Fclk
        // Fclk for PAL = 985248 Hz, NTSC = 1022727 Hz
        // We use a simplified calculation for our sample rate
        Frequency = (uint)(freq * 16777216.0 / _sampleRate);

        // Reset envelope
        _envStage = 1; // Attack
        _envCounter = 0;
    }

    public void Release()
    {
        if (_envStage > 0 && _envStage < 4)
        {
            _envStage = 4; // Release
        }
    }

    public void Reset()
    {
        _accumulator = 0;
        _envelope = 0;
        _envStage = 0;
        IsActive = false;
    }

    public float Process(double deltaTime, SIDVoiceState? prevVoice)
    {
        if (!IsActive) return 0f;

        var osc = _synth.Oscillators[OscIndex];

        // Update accumulator
        uint prevAcc = _accumulator;
        _accumulator += Frequency;

        // Handle hard sync
        if (osc.HardSync && prevVoice != null)
        {
            bool prevMsb = prevVoice.Msb;
            if (prevMsb && !prevVoice._lastMsb)
            {
                _accumulator = 0;
            }
        }
        _lastMsb = Msb;

        // Generate waveform
        double waveOut = GenerateWaveform(osc, prevVoice);

        // Process envelope
        ProcessEnvelope(osc, deltaTime);

        if (_envStage == 0)
        {
            IsActive = false;
            return 0f;
        }

        // Apply envelope and velocity
        double velocityGain = Velocity / 127.0;
        float output = (float)(waveOut * _envelope * velocityGain);

        return output;
    }

    private double GenerateWaveform(SIDOscillator osc, SIDVoiceState? prevVoice)
    {
        double output = 0;
        int waveCount = 0;

        // Triangle
        if ((osc.Waveform & SIDWaveform.Triangle) != 0)
        {
            uint triAcc = _accumulator;

            // Ring modulation: XOR with previous oscillator MSB
            if (osc.RingMod && prevVoice != null && prevVoice.Msb)
            {
                triAcc ^= 0xFFFFFF;
            }

            // Triangle: fold the accumulator
            double tri = ((triAcc & 0x800000) != 0)
                ? (0xFFFFFF - triAcc) / (double)0x7FFFFF - 1.0
                : triAcc / (double)0x7FFFFF - 1.0;
            output += tri;
            waveCount++;
        }

        // Sawtooth
        if ((osc.Waveform & SIDWaveform.Sawtooth) != 0)
        {
            double saw = _accumulator / (double)0xFFFFFF * 2.0 - 1.0;
            output += saw;
            waveCount++;
        }

        // Pulse
        if ((osc.Waveform & SIDWaveform.Pulse) != 0)
        {
            uint threshold = (uint)(osc.PulseWidth << 12);
            double pulse = (_accumulator >= threshold) ? 1.0 : -1.0;
            output += pulse;
            waveCount++;
        }

        // Noise
        if ((osc.Waveform & SIDWaveform.Noise) != 0)
        {
            // Update LFSR when accumulator bit 19 changes
            if (((_accumulator ^ (_accumulator - Frequency)) & 0x080000) != 0)
            {
                uint bit0 = ((_shiftRegister >> 22) ^ (_shiftRegister >> 17)) & 1;
                _shiftRegister = (_shiftRegister << 1) | bit0;
            }

            // Extract noise bits
            uint noiseBits = ((_shiftRegister >> 22) & 1) |
                            ((_shiftRegister >> 20) & 2) |
                            ((_shiftRegister >> 16) & 4) |
                            ((_shiftRegister >> 13) & 8) |
                            ((_shiftRegister >> 11) & 16) |
                            ((_shiftRegister >> 7) & 32) |
                            ((_shiftRegister >> 4) & 64) |
                            ((_shiftRegister >> 2) & 128);
            double noise = noiseBits / 127.5 - 1.0;
            output += noise;
            waveCount++;
        }

        // Combined waveforms - AND combination (like real SID)
        if (waveCount > 1)
        {
            output /= waveCount; // Simple average for combined waveforms
        }

        return waveCount > 0 ? output : 0;
    }

    private void ProcessEnvelope(SIDOscillator osc, double deltaTime)
    {
        // SID envelope rates (approximation)
        double[] attackRates = { 0.002, 0.008, 0.016, 0.024, 0.038, 0.056, 0.068, 0.080,
                                 0.100, 0.250, 0.500, 0.800, 1.000, 3.000, 5.000, 8.000 };
        double[] decayRates = { 0.006, 0.024, 0.048, 0.072, 0.114, 0.168, 0.204, 0.240,
                               0.300, 0.750, 1.500, 2.400, 3.000, 9.000, 15.000, 24.000 };

        switch (_envStage)
        {
            case 1: // Attack
                double atkTime = attackRates[osc.Attack];
                _envelope += deltaTime / atkTime;
                if (_envelope >= 1.0)
                {
                    _envelope = 1.0;
                    _envStage = 2; // Decay
                }
                break;

            case 2: // Decay
                double dcyTime = decayRates[osc.Decay];
                double susLevel = osc.Sustain / 15.0;
                _envelope -= deltaTime / dcyTime;
                if (_envelope <= susLevel)
                {
                    _envelope = susLevel;
                    _envStage = 3; // Sustain
                }
                break;

            case 3: // Sustain
                _envelope = osc.Sustain / 15.0;
                break;

            case 4: // Release
                double relTime = decayRates[osc.Release];
                _envelope -= deltaTime / relTime;
                if (_envelope <= 0.001)
                {
                    _envelope = 0;
                    _envStage = 0; // Idle
                }
                break;
        }
    }

    public float ApplyFilter(float input, SIDFilterMode mode, double cutoff, double resonance, SIDRevision revision)
    {
        if (mode == SIDFilterMode.Off) return input;

        // State variable filter
        // Cutoff frequency scaling (SID-style)
        double fc = cutoff / 2048.0;

        // Resonance scaling (0-15 to Q factor)
        // 6581 has more resonance
        double q = revision == SIDRevision.MOS6581
            ? 0.707 + resonance * 0.1
            : 0.707 + resonance * 0.07;

        // Filter coefficients
        double f = 2.0 * Math.Sin(Math.PI * fc * 0.5);
        double qInv = 1.0 / q;

        // State variable filter update
        _filterHigh = input - _filterLow - qInv * _filterBand;
        _filterBand += f * _filterHigh;
        _filterLow += f * _filterBand;

        // Mix filter outputs based on mode
        double output = 0;
        if ((mode & SIDFilterMode.LowPass) != 0) output += _filterLow;
        if ((mode & SIDFilterMode.BandPass) != 0) output += _filterBand;
        if ((mode & SIDFilterMode.HighPass) != 0) output += _filterHigh;

        // 6581 distortion
        if (revision == SIDRevision.MOS6581)
        {
            output = Math.Tanh(output * 1.2);
        }

        return (float)output;
    }
}

/// <summary>
/// Commodore 64 SID chip emulation (MOS 6581/8580).
/// Features 3 oscillators with pulse/saw/tri/noise, ring modulation, hard sync, multimode filter.
/// </summary>
public class SIDSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly SIDVoiceState[] _voiceStates;
    private readonly Dictionary<int, int> _noteToOsc = new();
    private readonly object _lock = new();
    private int _nextOscIndex = 0;

    /// <summary>Gets or sets the synth name.</summary>
    public string Name { get; set; } = "SIDSynth";

    /// <summary>Gets the audio format.</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Gets or sets the master volume (0-1).</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>Gets or sets the SID chip revision.</summary>
    public SIDRevision Revision { get; set; } = SIDRevision.MOS6581;

    /// <summary>The 3 SID oscillators.</summary>
    public SIDOscillator[] Oscillators { get; } = new SIDOscillator[3];

    // Filter parameters
    /// <summary>Filter cutoff frequency (0-2047).</summary>
    public int FilterCutoff { get; set; } = 1024;
    /// <summary>Filter resonance (0-15).</summary>
    public int FilterResonance { get; set; } = 8;
    /// <summary>Filter mode (lowpass, bandpass, highpass, or combinations).</summary>
    public SIDFilterMode FilterMode { get; set; } = SIDFilterMode.LowPass;

    /// <summary>
    /// Creates a new SID synth.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (default: from Settings).</param>
    public SIDSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize oscillators
        for (int i = 0; i < 3; i++)
        {
            Oscillators[i] = new SIDOscillator();
        }

        // Initialize voice states
        _voiceStates = new SIDVoiceState[3];
        for (int i = 0; i < 3; i++)
        {
            _voiceStates[i] = new SIDVoiceState(rate, this, i);
        }
    }

    /// <summary>
    /// Triggers a note.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        if (velocity == 0)
        {
            NoteOff(note);
            return;
        }

        lock (_lock)
        {
            // Find free oscillator or steal oldest
            int oscIndex = -1;

            // Check if note already playing
            if (_noteToOsc.TryGetValue(note, out int existingOsc))
            {
                _voiceStates[existingOsc].Trigger(note, velocity);
                return;
            }

            // Find free voice
            for (int i = 0; i < 3; i++)
            {
                if (!_voiceStates[i].IsActive)
                {
                    oscIndex = i;
                    break;
                }
            }

            // Round-robin if all busy
            if (oscIndex == -1)
            {
                oscIndex = _nextOscIndex;
                _nextOscIndex = (_nextOscIndex + 1) % 3;

                // Remove old note mapping
                int oldNote = _voiceStates[oscIndex].Note;
                _noteToOsc.Remove(oldNote);
            }

            _voiceStates[oscIndex].Trigger(note, velocity);
            _noteToOsc[note] = oscIndex;
        }
    }

    /// <summary>
    /// Releases a note.
    /// </summary>
    public void NoteOff(int note)
    {
        lock (_lock)
        {
            if (_noteToOsc.TryGetValue(note, out int oscIndex))
            {
                _voiceStates[oscIndex].Release();
                _noteToOsc.Remove(note);
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
            for (int i = 0; i < 3; i++)
            {
                _voiceStates[i].Release();
            }
            _noteToOsc.Clear();
        }
    }

    /// <summary>
    /// Sets a parameter by name.
    /// </summary>
    public void SetParameter(string name, float value)
    {
        var parts = name.ToLowerInvariant().Split('_');

        // Oscillator-specific parameters (osc0_waveform, osc1_pulsewidth, etc.)
        if (parts.Length >= 2 && parts[0].StartsWith("osc") && int.TryParse(parts[0].Substring(3), out int oscIdx))
        {
            if (oscIdx < 0 || oscIdx >= 3) return;
            var osc = Oscillators[oscIdx];

            switch (parts[1])
            {
                case "waveform": osc.Waveform = (SIDWaveform)(int)value; break;
                case "pulsewidth": osc.PulseWidth = Math.Clamp((int)value, 0, 4095); break;
                case "ringmod": osc.RingMod = value > 0.5f; break;
                case "hardsync": osc.HardSync = value > 0.5f; break;
                case "filter": osc.FilterEnable = value > 0.5f; break;
                case "attack": osc.Attack = Math.Clamp((int)value, 0, 15); break;
                case "decay": osc.Decay = Math.Clamp((int)value, 0, 15); break;
                case "sustain": osc.Sustain = Math.Clamp((int)value, 0, 15); break;
                case "release": osc.Release = Math.Clamp((int)value, 0, 15); break;
            }
            return;
        }

        // Global parameters
        switch (name.ToLowerInvariant())
        {
            case "volume": Volume = Math.Clamp(value, 0f, 1f); break;
            case "revision": Revision = (SIDRevision)(int)value; break;
            case "filtercutoff": FilterCutoff = Math.Clamp((int)value, 0, 2047); break;
            case "filterresonance": FilterResonance = Math.Clamp((int)value, 0, 15); break;
            case "filtermode": FilterMode = (SIDFilterMode)(int)value; break;
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
                float filtered = 0f;
                float unfiltered = 0f;

                // Process each oscillator
                for (int i = 0; i < 3; i++)
                {
                    var voice = _voiceStates[i];
                    var prevVoice = i > 0 ? _voiceStates[i - 1] : _voiceStates[2];

                    float oscOut = voice.Process(deltaTime, prevVoice);

                    if (Oscillators[i].FilterEnable)
                    {
                        filtered += oscOut;
                    }
                    else
                    {
                        unfiltered += oscOut;
                    }
                }

                // Apply filter to filtered sum
                if (filtered != 0f && FilterMode != SIDFilterMode.Off)
                {
                    filtered = _voiceStates[0].ApplyFilter(
                        filtered,
                        FilterMode,
                        FilterCutoff,
                        FilterResonance,
                        Revision
                    );
                }

                // Mix filtered and unfiltered
                float sample = filtered + unfiltered;

                // Apply volume and soft clipping
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

    /// <summary>Creates a classic C64 bass preset.</summary>
    public static SIDSynth CreateC64Bass()
    {
        var synth = new SIDSynth { Name = "C64 Bass", Revision = SIDRevision.MOS6581 };

        synth.Oscillators[0].Waveform = SIDWaveform.Pulse;
        synth.Oscillators[0].PulseWidth = 2048;
        synth.Oscillators[0].Attack = 0;
        synth.Oscillators[0].Decay = 6;
        synth.Oscillators[0].Sustain = 4;
        synth.Oscillators[0].Release = 2;

        synth.FilterCutoff = 512;
        synth.FilterResonance = 10;
        synth.FilterMode = SIDFilterMode.LowPass;

        return synth;
    }

    /// <summary>Creates an arpeggiated lead preset.</summary>
    public static SIDSynth CreateArpLead()
    {
        var synth = new SIDSynth { Name = "Arp Lead", Revision = SIDRevision.MOS6581 };

        synth.Oscillators[0].Waveform = SIDWaveform.Sawtooth;
        synth.Oscillators[0].Attack = 0;
        synth.Oscillators[0].Decay = 4;
        synth.Oscillators[0].Sustain = 8;
        synth.Oscillators[0].Release = 3;

        synth.FilterCutoff = 1200;
        synth.FilterResonance = 8;
        synth.FilterMode = SIDFilterMode.LowPass;

        return synth;
    }

    /// <summary>Creates a ring modulation bell preset.</summary>
    public static SIDSynth CreateRingBell()
    {
        var synth = new SIDSynth { Name = "Ring Bell", Revision = SIDRevision.MOS8580 };

        synth.Oscillators[0].Waveform = SIDWaveform.Triangle;
        synth.Oscillators[0].RingMod = true;
        synth.Oscillators[0].Attack = 0;
        synth.Oscillators[0].Decay = 8;
        synth.Oscillators[0].Sustain = 0;
        synth.Oscillators[0].Release = 6;

        synth.Oscillators[1].Waveform = SIDWaveform.Triangle;
        synth.Oscillators[1].Attack = 0;
        synth.Oscillators[1].Decay = 6;
        synth.Oscillators[1].Sustain = 0;
        synth.Oscillators[1].Release = 4;

        synth.FilterMode = SIDFilterMode.Off;

        return synth;
    }

    /// <summary>Creates a sync lead preset.</summary>
    public static SIDSynth CreateSyncLead()
    {
        var synth = new SIDSynth { Name = "Sync Lead", Revision = SIDRevision.MOS6581 };

        synth.Oscillators[0].Waveform = SIDWaveform.Sawtooth;
        synth.Oscillators[0].HardSync = true;
        synth.Oscillators[0].Attack = 2;
        synth.Oscillators[0].Decay = 6;
        synth.Oscillators[0].Sustain = 6;
        synth.Oscillators[0].Release = 4;

        synth.Oscillators[1].Waveform = SIDWaveform.Sawtooth;
        synth.Oscillators[1].Attack = 2;
        synth.Oscillators[1].Decay = 6;
        synth.Oscillators[1].Sustain = 6;
        synth.Oscillators[1].Release = 4;
        synth.Oscillators[1].FilterEnable = false;

        synth.FilterCutoff = 1500;
        synth.FilterResonance = 6;
        synth.FilterMode = SIDFilterMode.LowPass;

        return synth;
    }

    /// <summary>Creates a noise percussion preset.</summary>
    public static SIDSynth CreateNoiseHit()
    {
        var synth = new SIDSynth { Name = "Noise Hit", Revision = SIDRevision.MOS6581 };

        synth.Oscillators[0].Waveform = SIDWaveform.Noise;
        synth.Oscillators[0].Attack = 0;
        synth.Oscillators[0].Decay = 3;
        synth.Oscillators[0].Sustain = 0;
        synth.Oscillators[0].Release = 2;

        synth.FilterCutoff = 800;
        synth.FilterResonance = 12;
        synth.FilterMode = SIDFilterMode.BandPass;

        return synth;
    }

    #endregion
}
