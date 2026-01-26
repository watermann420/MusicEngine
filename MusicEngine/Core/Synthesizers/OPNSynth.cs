// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Synthesizer component.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// YM2612 FM algorithms (0-7 as on the original chip).
/// </summary>
public enum OPNAlgorithm
{
    /// <summary>Algorithm 0: Serial - 4->3->2->1 (out)</summary>
    Serial = 0,
    /// <summary>Algorithm 1: 4->3->2, 1 (parallel output from 1 and 2)</summary>
    Algo1 = 1,
    /// <summary>Algorithm 2: 4->3, 2->1 (parallel)</summary>
    Algo2 = 2,
    /// <summary>Algorithm 3: 4->3->2+1 (op3 modulates op2 and op1)</summary>
    Algo3 = 3,
    /// <summary>Algorithm 4: (4->3)+(2->1)</summary>
    Algo4 = 4,
    /// <summary>Algorithm 5: 4->(3+2+1)</summary>
    Algo5 = 5,
    /// <summary>Algorithm 6: (4->3)+2+1</summary>
    Algo6 = 6,
    /// <summary>Algorithm 7: All parallel - 4+3+2+1</summary>
    Parallel = 7
}

/// <summary>
/// OPN operator parameters (for YM2612 emulation).
/// </summary>
public class OPNOperator
{
    /// <summary>Detune value (0-7).</summary>
    public int Detune { get; set; } = 0;
    /// <summary>Multiplier (0-15, 0 = 0.5x).</summary>
    public int Multiple { get; set; } = 1;
    /// <summary>Total level (attenuation) 0-127, higher = quieter.</summary>
    public int TotalLevel { get; set; } = 0;
    /// <summary>Key scale (0-3).</summary>
    public int KeyScale { get; set; } = 0;
    /// <summary>Attack rate (0-31).</summary>
    public int AttackRate { get; set; } = 31;
    /// <summary>First decay rate (0-31).</summary>
    public int Decay1Rate { get; set; } = 10;
    /// <summary>Second decay rate / sustain rate (0-31).</summary>
    public int Decay2Rate { get; set; } = 5;
    /// <summary>Sustain level (0-15).</summary>
    public int SustainLevel { get; set; } = 5;
    /// <summary>Release rate (0-15).</summary>
    public int ReleaseRate { get; set; } = 7;
    /// <summary>SSG-EG mode (0 = off, 1-8 = various modes).</summary>
    public int SsgEg { get; set; } = 0;
    /// <summary>Amplitude modulation sensitivity (0-3).</summary>
    public int AmSensitivity { get; set; } = 0;
}

/// <summary>
/// OPN channel parameters.
/// </summary>
public class OPNChannel
{
    /// <summary>The 4 operators for this channel.</summary>
    public OPNOperator[] Operators { get; } = new OPNOperator[4];
    /// <summary>FM algorithm (0-7).</summary>
    public OPNAlgorithm Algorithm { get; set; } = OPNAlgorithm.Algo4;
    /// <summary>Feedback amount for operator 1 (0-7).</summary>
    public int Feedback { get; set; } = 0;
    /// <summary>Panning: 0=mute, 1=right, 2=left, 3=center.</summary>
    public int Panning { get; set; } = 3;
    /// <summary>LFO frequency modulation sensitivity (0-7).</summary>
    public int FmSensitivity { get; set; } = 0;
    /// <summary>LFO amplitude modulation sensitivity (0-3).</summary>
    public int AmSensitivity { get; set; } = 0;

    public OPNChannel()
    {
        for (int i = 0; i < 4; i++)
        {
            Operators[i] = new OPNOperator();
        }
    }
}

/// <summary>
/// Internal OPN voice state.
/// </summary>
internal class OPNVoice
{
    private readonly int _sampleRate;
    private readonly OPNSynth _synth;

    // Operator state
    private readonly double[] _phases = new double[4];
    private readonly double[] _envelopes = new double[4];
    private readonly int[] _envStages = new int[4]; // 0=idle, 1=attack, 2=decay1, 3=decay2, 4=release
    private readonly double[] _lastOutputs = new double[4];
    private double _feedback1, _feedback2;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public double BaseFrequency { get; private set; }
    public int ChannelIndex { get; private set; }

    public OPNVoice(int sampleRate, OPNSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;
    }

    public void Trigger(int note, int velocity, int channelIndex)
    {
        Note = note;
        Velocity = velocity;
        ChannelIndex = channelIndex;
        IsActive = true;
        TriggerTime = DateTime.Now;
        BaseFrequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);

        // Reset phases and start attack
        for (int i = 0; i < 4; i++)
        {
            _phases[i] = 0;
            _envelopes[i] = 0;
            _envStages[i] = 1; // Attack
            _lastOutputs[i] = 0;
        }
        _feedback1 = _feedback2 = 0;
    }

    public void Release()
    {
        for (int i = 0; i < 4; i++)
        {
            _envStages[i] = 4; // Release
        }
    }

    public (float left, float right) Process(double deltaTime)
    {
        if (!IsActive) return (0f, 0f);

        var channel = _synth.Channels[ChannelIndex];
        var ops = channel.Operators;

        // Process envelopes
        bool anyActive = false;
        for (int i = 0; i < 4; i++)
        {
            ProcessEnvelope(i, ops[i], deltaTime);
            if (_envStages[i] != 0) anyActive = true;
        }

        if (!anyActive)
        {
            IsActive = false;
            return (0f, 0f);
        }

        // Calculate operator frequencies with detune and multiple
        double[] freqs = new double[4];
        for (int i = 0; i < 4; i++)
        {
            double mult = ops[i].Multiple == 0 ? 0.5 : ops[i].Multiple;
            double detune = GetDetuneValue(ops[i].Detune) * BaseFrequency / 1000.0;
            freqs[i] = BaseFrequency * mult + detune;
        }

        // Calculate operator outputs based on algorithm
        double[] outputs = new double[4];

        // Process operators based on algorithm
        // OPN algorithms work with op4 -> op3 -> op2 -> op1 (op1 is output)
        // In YM2612 numbering: op1 = slot 1, op2 = slot 3, op3 = slot 2, op4 = slot 4
        // We simplify to standard 0-3 indexing

        double output = ProcessAlgorithm(channel, freqs, outputs, deltaTime);

        // Apply velocity
        double velocityGain = Velocity / 127.0;
        output *= velocityGain;

        // Apply LFO (simplified)
        if (_synth.LfoEnabled && channel.AmSensitivity > 0)
        {
            double lfoAm = Math.Sin(_synth.LfoPhase) * 0.5 + 0.5;
            double amDepth = channel.AmSensitivity / 3.0 * 0.5;
            output *= 1.0 - (lfoAm * amDepth);
        }

        // Panning
        float left = 0f, right = 0f;
        float monoOut = (float)output;

        if ((channel.Panning & 2) != 0) left = monoOut;  // Left
        if ((channel.Panning & 1) != 0) right = monoOut; // Right

        return (left, right);
    }

    private void ProcessEnvelope(int opIndex, OPNOperator op, double deltaTime)
    {
        // YM2612-style envelope with attack -> decay1 -> decay2 -> release
        // Rates are logarithmic

        double rate;
        switch (_envStages[opIndex])
        {
            case 0: // Idle
                return;

            case 1: // Attack
                rate = GetEnvelopeRate(op.AttackRate);
                _envelopes[opIndex] += rate * deltaTime * 4.0;
                if (_envelopes[opIndex] >= 1.0)
                {
                    _envelopes[opIndex] = 1.0;
                    _envStages[opIndex] = 2; // Decay1
                }
                break;

            case 2: // Decay 1
                rate = GetEnvelopeRate(op.Decay1Rate);
                double sustainLevel = 1.0 - (op.SustainLevel / 15.0);
                _envelopes[opIndex] -= rate * deltaTime;
                if (_envelopes[opIndex] <= sustainLevel)
                {
                    _envelopes[opIndex] = sustainLevel;
                    _envStages[opIndex] = 3; // Decay2/Sustain
                }
                break;

            case 3: // Decay 2 (sustain decay)
                rate = GetEnvelopeRate(op.Decay2Rate);
                _envelopes[opIndex] -= rate * deltaTime * 0.1;
                if (_envelopes[opIndex] <= 0)
                {
                    _envelopes[opIndex] = 0;
                    _envStages[opIndex] = 0;
                }
                break;

            case 4: // Release
                rate = GetEnvelopeRate(op.ReleaseRate * 2);
                _envelopes[opIndex] -= rate * deltaTime * 2.0;
                if (_envelopes[opIndex] <= 0)
                {
                    _envelopes[opIndex] = 0;
                    _envStages[opIndex] = 0;
                }
                break;
        }
    }

    private double GetEnvelopeRate(int rate)
    {
        // Convert YM2612 rate to time constant
        if (rate == 0) return 0;
        return Math.Pow(2.0, (rate - 15) / 4.0) * 10.0;
    }

    private double GetDetuneValue(int detune)
    {
        // YM2612 detune table (simplified)
        double[] detuneTable = { 0, 0.053, 0.106, 0.159, 0, -0.053, -0.106, -0.159 };
        return detuneTable[detune & 7];
    }

    private double ProcessAlgorithm(OPNChannel channel, double[] freqs, double[] outputs, double deltaTime)
    {
        var ops = channel.Operators;

        // Calculate total levels (attenuation)
        double[] levels = new double[4];
        for (int i = 0; i < 4; i++)
        {
            levels[i] = Math.Pow(10.0, -ops[i].TotalLevel / 20.0) * _envelopes[i];
        }

        // Feedback for operator 1 (index 0)
        double fb = 0;
        if (channel.Feedback > 0)
        {
            double fbAmount = Math.Pow(2.0, channel.Feedback - 1) / 16.0;
            fb = (_feedback1 + _feedback2) * 0.5 * fbAmount * Math.PI;
        }

        // Process operators according to algorithm
        // Op indices: 0=op1, 1=op2, 2=op3, 3=op4
        double result = 0;

        switch (channel.Algorithm)
        {
            case OPNAlgorithm.Serial: // 4->3->2->1
                outputs[3] = ProcessOperator(3, freqs[3], 0, levels[3], deltaTime);
                outputs[2] = ProcessOperator(2, freqs[2], outputs[3] * Math.PI, levels[2], deltaTime);
                outputs[1] = ProcessOperator(1, freqs[1], outputs[2] * Math.PI, levels[1], deltaTime);
                outputs[0] = ProcessOperator(0, freqs[0], outputs[1] * Math.PI + fb, levels[0], deltaTime);
                result = outputs[0];
                break;

            case OPNAlgorithm.Algo1: // 4->3->2 + 1
                outputs[3] = ProcessOperator(3, freqs[3], 0, levels[3], deltaTime);
                outputs[2] = ProcessOperator(2, freqs[2], outputs[3] * Math.PI, levels[2], deltaTime);
                outputs[1] = ProcessOperator(1, freqs[1], outputs[2] * Math.PI, levels[1], deltaTime);
                outputs[0] = ProcessOperator(0, freqs[0], fb, levels[0], deltaTime);
                result = outputs[1] + outputs[0];
                break;

            case OPNAlgorithm.Algo2: // 4->3, 2->1
                outputs[3] = ProcessOperator(3, freqs[3], 0, levels[3], deltaTime);
                outputs[2] = ProcessOperator(2, freqs[2], outputs[3] * Math.PI, levels[2], deltaTime);
                outputs[1] = ProcessOperator(1, freqs[1], 0, levels[1], deltaTime);
                outputs[0] = ProcessOperator(0, freqs[0], outputs[1] * Math.PI + fb, levels[0], deltaTime);
                result = outputs[2] + outputs[0];
                break;

            case OPNAlgorithm.Algo3: // 4->3->(2+1)
                outputs[3] = ProcessOperator(3, freqs[3], 0, levels[3], deltaTime);
                outputs[2] = ProcessOperator(2, freqs[2], outputs[3] * Math.PI, levels[2], deltaTime);
                outputs[1] = ProcessOperator(1, freqs[1], outputs[2] * Math.PI, levels[1], deltaTime);
                outputs[0] = ProcessOperator(0, freqs[0], outputs[2] * Math.PI + fb, levels[0], deltaTime);
                result = outputs[1] + outputs[0];
                break;

            case OPNAlgorithm.Algo4: // (4->3) + (2->1)
                outputs[3] = ProcessOperator(3, freqs[3], 0, levels[3], deltaTime);
                outputs[2] = ProcessOperator(2, freqs[2], outputs[3] * Math.PI, levels[2], deltaTime);
                outputs[1] = ProcessOperator(1, freqs[1], 0, levels[1], deltaTime);
                outputs[0] = ProcessOperator(0, freqs[0], outputs[1] * Math.PI + fb, levels[0], deltaTime);
                result = outputs[2] + outputs[0];
                break;

            case OPNAlgorithm.Algo5: // 4 -> (3+2+1)
                outputs[3] = ProcessOperator(3, freqs[3], 0, levels[3], deltaTime);
                outputs[2] = ProcessOperator(2, freqs[2], outputs[3] * Math.PI, levels[2], deltaTime);
                outputs[1] = ProcessOperator(1, freqs[1], outputs[3] * Math.PI, levels[1], deltaTime);
                outputs[0] = ProcessOperator(0, freqs[0], outputs[3] * Math.PI + fb, levels[0], deltaTime);
                result = outputs[2] + outputs[1] + outputs[0];
                break;

            case OPNAlgorithm.Algo6: // (4->3) + 2 + 1
                outputs[3] = ProcessOperator(3, freqs[3], 0, levels[3], deltaTime);
                outputs[2] = ProcessOperator(2, freqs[2], outputs[3] * Math.PI, levels[2], deltaTime);
                outputs[1] = ProcessOperator(1, freqs[1], 0, levels[1], deltaTime);
                outputs[0] = ProcessOperator(0, freqs[0], fb, levels[0], deltaTime);
                result = outputs[2] + outputs[1] + outputs[0];
                break;

            case OPNAlgorithm.Parallel: // 4 + 3 + 2 + 1
                outputs[3] = ProcessOperator(3, freqs[3], 0, levels[3], deltaTime);
                outputs[2] = ProcessOperator(2, freqs[2], 0, levels[2], deltaTime);
                outputs[1] = ProcessOperator(1, freqs[1], 0, levels[1], deltaTime);
                outputs[0] = ProcessOperator(0, freqs[0], fb, levels[0], deltaTime);
                result = outputs[3] + outputs[2] + outputs[1] + outputs[0];
                break;
        }

        // Update feedback
        _feedback2 = _feedback1;
        _feedback1 = outputs[0];

        return result;
    }

    private double ProcessOperator(int index, double freq, double modulation, double level, double deltaTime)
    {
        if (level <= 0.0001) return 0;

        double phaseInc = freq / _sampleRate;
        _phases[index] += phaseInc;
        if (_phases[index] >= 1.0) _phases[index] -= 1.0;

        double phase = _phases[index] * 2.0 * Math.PI + modulation;
        double output = Math.Sin(phase) * level;

        _lastOutputs[index] = output;
        return output;
    }
}

/// <summary>
/// YM2612 FM chip emulation (Sega Genesis/Mega Drive sound).
/// Features 6 channels, 4 operators each, authentic FM algorithms, and LFO.
/// </summary>
public class OPNSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<OPNVoice> _voices = new();
    private readonly Dictionary<int, OPNVoice> _noteToVoice = new();
    private readonly object _lock = new();

    /// <summary>Gets or sets the synth name.</summary>
    public string Name { get; set; } = "OPNSynth";

    /// <summary>Gets the audio format.</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Gets or sets the master volume (0-1).</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>Gets or sets the maximum number of voices.</summary>
    public int MaxVoices { get; set; } = 6;

    /// <summary>The 6 FM channels.</summary>
    public OPNChannel[] Channels { get; } = new OPNChannel[6];

    /// <summary>Gets or sets the active channel for note input (0-5).</summary>
    public int ActiveChannel { get; set; } = 0;

    // LFO
    /// <summary>LFO enabled.</summary>
    public bool LfoEnabled { get; set; } = false;
    /// <summary>LFO frequency (0-7, maps to Hz).</summary>
    public int LfoFrequency { get; set; } = 0;
    /// <summary>Current LFO phase.</summary>
    internal double LfoPhase { get; private set; }

    // LFO frequency table (Hz)
    private static readonly double[] LfoFrequencies = { 3.98, 5.56, 6.02, 6.37, 6.88, 9.63, 48.1, 72.2 };

    /// <summary>
    /// Creates a new OPN synth (YM2612 emulation).
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (default: from Settings).</param>
    public OPNSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize channels with default settings
        for (int i = 0; i < 6; i++)
        {
            Channels[i] = new OPNChannel();
            // Set default algorithm 4 (common for Genesis)
            Channels[i].Algorithm = OPNAlgorithm.Algo4;
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
            if (_noteToVoice.TryGetValue(note, out var existingVoice))
            {
                existingVoice.Trigger(note, velocity, ActiveChannel);
                return;
            }

            var voice = GetFreeVoice();
            if (voice == null) return;

            voice.Trigger(note, velocity, ActiveChannel);
            _noteToVoice[note] = voice;
        }
    }

    /// <summary>
    /// Releases a note.
    /// </summary>
    public void NoteOff(int note)
    {
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
                voice.Release();
            }
            _noteToVoice.Clear();
        }
    }

    /// <summary>
    /// Sets a parameter by name.
    /// </summary>
    public void SetParameter(string name, float value)
    {
        var parts = name.ToLowerInvariant().Split('_');

        // Channel-specific parameters (ch0_algorithm, ch0_op1_tl, etc.)
        if (parts.Length >= 2 && parts[0].StartsWith("ch") && int.TryParse(parts[0].Substring(2), out int chIdx))
        {
            if (chIdx < 0 || chIdx >= 6) return;
            var channel = Channels[chIdx];

            if (parts.Length == 2)
            {
                switch (parts[1])
                {
                    case "algorithm": channel.Algorithm = (OPNAlgorithm)(int)value; break;
                    case "feedback": channel.Feedback = Math.Clamp((int)value, 0, 7); break;
                    case "panning": channel.Panning = Math.Clamp((int)value, 0, 3); break;
                    case "fmsens": channel.FmSensitivity = Math.Clamp((int)value, 0, 7); break;
                    case "amsens": channel.AmSensitivity = Math.Clamp((int)value, 0, 3); break;
                }
            }
            else if (parts.Length >= 3 && parts[1].StartsWith("op") && int.TryParse(parts[1].Substring(2), out int opIdx))
            {
                if (opIdx < 0 || opIdx >= 4) return;
                var op = channel.Operators[opIdx];

                switch (parts[2])
                {
                    case "dt": op.Detune = Math.Clamp((int)value, 0, 7); break;
                    case "mul": op.Multiple = Math.Clamp((int)value, 0, 15); break;
                    case "tl": op.TotalLevel = Math.Clamp((int)value, 0, 127); break;
                    case "ks": op.KeyScale = Math.Clamp((int)value, 0, 3); break;
                    case "ar": op.AttackRate = Math.Clamp((int)value, 0, 31); break;
                    case "d1r": op.Decay1Rate = Math.Clamp((int)value, 0, 31); break;
                    case "d2r": op.Decay2Rate = Math.Clamp((int)value, 0, 31); break;
                    case "sl": op.SustainLevel = Math.Clamp((int)value, 0, 15); break;
                    case "rr": op.ReleaseRate = Math.Clamp((int)value, 0, 15); break;
                    case "ssg": op.SsgEg = Math.Clamp((int)value, 0, 8); break;
                }
            }
            return;
        }

        // Global parameters
        switch (name.ToLowerInvariant())
        {
            case "volume": Volume = Math.Clamp(value, 0f, 1f); break;
            case "activechannel": ActiveChannel = Math.Clamp((int)value, 0, 5); break;
            case "lfoenabled": LfoEnabled = value > 0.5f; break;
            case "lfofrequency": LfoFrequency = Math.Clamp((int)value, 0, 7); break;
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
                // Update LFO
                if (LfoEnabled)
                {
                    LfoPhase += LfoFrequencies[LfoFrequency] * deltaTime * 2.0 * Math.PI;
                    if (LfoPhase >= 2.0 * Math.PI) LfoPhase -= 2.0 * Math.PI;
                }

                float leftSum = 0f;
                float rightSum = 0f;

                foreach (var voice in _voices)
                {
                    if (voice.IsActive)
                    {
                        var (left, right) = voice.Process(deltaTime);
                        leftSum += left;
                        rightSum += right;
                    }
                }

                // Apply volume and soft clipping
                leftSum *= Volume;
                rightSum *= Volume;
                leftSum = MathF.Tanh(leftSum);
                rightSum = MathF.Tanh(rightSum);

                // Output stereo
                if (channels >= 2)
                {
                    buffer[offset + n] = leftSum;
                    buffer[offset + n + 1] = rightSum;
                }
                else
                {
                    buffer[offset + n] = (leftSum + rightSum) * 0.5f;
                }
            }
        }

        return count;
    }

    private OPNVoice? GetFreeVoice()
    {
        foreach (var voice in _voices)
        {
            if (!voice.IsActive) return voice;
        }

        if (_voices.Count < MaxVoices)
        {
            var voice = new OPNVoice(_waveFormat.SampleRate, this);
            _voices.Add(voice);
            return voice;
        }

        // Voice stealing
        OPNVoice? oldest = null;
        DateTime oldestTime = DateTime.MaxValue;
        foreach (var voice in _voices)
        {
            if (voice.TriggerTime < oldestTime)
            {
                oldestTime = voice.TriggerTime;
                oldest = voice;
            }
        }

        if (oldest != null)
        {
            _noteToVoice.Remove(oldest.Note);
        }

        return oldest;
    }

    #region Presets

    /// <summary>Creates a Sonic-style bright lead preset.</summary>
    public static OPNSynth CreateSonicLead()
    {
        var synth = new OPNSynth { Name = "Sonic Lead" };
        var ch = synth.Channels[0];
        ch.Algorithm = OPNAlgorithm.Algo4;
        ch.Feedback = 5;

        // Carrier 1
        ch.Operators[0].Multiple = 1;
        ch.Operators[0].TotalLevel = 20;
        ch.Operators[0].AttackRate = 31;
        ch.Operators[0].Decay1Rate = 15;
        ch.Operators[0].SustainLevel = 3;
        ch.Operators[0].ReleaseRate = 8;

        // Modulator 1
        ch.Operators[1].Multiple = 1;
        ch.Operators[1].TotalLevel = 35;
        ch.Operators[1].AttackRate = 31;
        ch.Operators[1].Decay1Rate = 10;

        // Carrier 2
        ch.Operators[2].Multiple = 2;
        ch.Operators[2].TotalLevel = 25;
        ch.Operators[2].AttackRate = 31;
        ch.Operators[2].Decay1Rate = 12;
        ch.Operators[2].SustainLevel = 4;
        ch.Operators[2].ReleaseRate = 8;

        // Modulator 2
        ch.Operators[3].Multiple = 4;
        ch.Operators[3].TotalLevel = 40;
        ch.Operators[3].AttackRate = 31;
        ch.Operators[3].Decay1Rate = 8;

        return synth;
    }

    /// <summary>Creates a Genesis bass preset.</summary>
    public static OPNSynth CreateGenesisBass()
    {
        var synth = new OPNSynth { Name = "Genesis Bass" };
        var ch = synth.Channels[0];
        ch.Algorithm = OPNAlgorithm.Serial;
        ch.Feedback = 3;

        ch.Operators[0].Multiple = 1;
        ch.Operators[0].TotalLevel = 15;
        ch.Operators[0].AttackRate = 31;
        ch.Operators[0].Decay1Rate = 8;
        ch.Operators[0].SustainLevel = 2;
        ch.Operators[0].ReleaseRate = 6;

        ch.Operators[1].Multiple = 1;
        ch.Operators[1].TotalLevel = 30;
        ch.Operators[1].AttackRate = 31;
        ch.Operators[1].Decay1Rate = 12;

        ch.Operators[2].Multiple = 0; // 0.5x
        ch.Operators[2].TotalLevel = 50;
        ch.Operators[2].AttackRate = 31;
        ch.Operators[2].Decay1Rate = 15;

        ch.Operators[3].Multiple = 1;
        ch.Operators[3].TotalLevel = 45;
        ch.Operators[3].AttackRate = 31;
        ch.Operators[3].Decay1Rate = 10;

        return synth;
    }

    /// <summary>Creates a FM piano preset.</summary>
    public static OPNSynth CreateFMPiano()
    {
        var synth = new OPNSynth { Name = "FM Piano" };
        var ch = synth.Channels[0];
        ch.Algorithm = OPNAlgorithm.Algo4;
        ch.Feedback = 2;

        ch.Operators[0].Multiple = 1;
        ch.Operators[0].TotalLevel = 20;
        ch.Operators[0].AttackRate = 31;
        ch.Operators[0].Decay1Rate = 6;
        ch.Operators[0].SustainLevel = 0;
        ch.Operators[0].Decay2Rate = 3;
        ch.Operators[0].ReleaseRate = 6;

        ch.Operators[1].Multiple = 14;
        ch.Operators[1].TotalLevel = 55;
        ch.Operators[1].AttackRate = 31;
        ch.Operators[1].Decay1Rate = 4;

        ch.Operators[2].Multiple = 1;
        ch.Operators[2].TotalLevel = 30;
        ch.Operators[2].AttackRate = 31;
        ch.Operators[2].Decay1Rate = 5;
        ch.Operators[2].SustainLevel = 0;
        ch.Operators[2].ReleaseRate = 5;

        ch.Operators[3].Multiple = 1;
        ch.Operators[3].TotalLevel = 50;
        ch.Operators[3].AttackRate = 31;
        ch.Operators[3].Decay1Rate = 6;

        return synth;
    }

    /// <summary>Creates a brass preset.</summary>
    public static OPNSynth CreateBrass()
    {
        var synth = new OPNSynth { Name = "OPN Brass" };
        var ch = synth.Channels[0];
        ch.Algorithm = OPNAlgorithm.Algo2;
        ch.Feedback = 4;

        for (int i = 0; i < 4; i++)
        {
            ch.Operators[i].Multiple = i == 0 ? 1 : (i == 2 ? 1 : 2);
            ch.Operators[i].TotalLevel = i < 2 ? 25 : 40;
            ch.Operators[i].AttackRate = 25;
            ch.Operators[i].Decay1Rate = 8;
            ch.Operators[i].SustainLevel = 5;
            ch.Operators[i].ReleaseRate = 6;
        }

        return synth;
    }

    #endregion
}
