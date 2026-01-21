//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: FM Synthesizer with 6 operators and DX7-style algorithms.


using System;
using System.Collections.Generic;
using NAudio.Wave;


namespace MusicEngine.Core;


/// <summary>
/// Operator waveform types for FM synthesis
/// </summary>
public enum FMWaveform
{
    Sine,
    Triangle,
    Sawtooth,
    Square,
    Feedback // Self-modulating sine
}


/// <summary>
/// FM operator configuration
/// </summary>
public class FMOperator
{
    /// <summary>Frequency ratio relative to base frequency</summary>
    public float Ratio { get; set; } = 1.0f;

    /// <summary>Fixed frequency in Hz (0 = use ratio)</summary>
    public float FixedFrequency { get; set; } = 0f;

    /// <summary>Output level (0-1)</summary>
    public float Level { get; set; } = 1.0f;

    /// <summary>Fine detune in cents (-100 to 100)</summary>
    public float Detune { get; set; } = 0f;

    /// <summary>Self-feedback amount (0-1)</summary>
    public float Feedback { get; set; } = 0f;

    /// <summary>Waveform type</summary>
    public FMWaveform Waveform { get; set; } = FMWaveform.Sine;

    /// <summary>Velocity sensitivity (0-1)</summary>
    public float VelocitySensitivity { get; set; } = 0.5f;

    /// <summary>Key scaling - higher notes = faster envelope</summary>
    public float KeyScaling { get; set; } = 0f;

    /// <summary>Amplitude envelope</summary>
    public Envelope Envelope { get; set; }

    /// <summary>Whether this operator is a carrier (outputs audio)</summary>
    public bool IsCarrier { get; set; } = false;

    public FMOperator()
    {
        Envelope = new Envelope(0.01, 0.1, 0.7, 0.3);
    }
}


/// <summary>
/// FM synthesis algorithm defining operator connections.
/// Based on DX7 algorithms.
/// </summary>
public enum FMAlgorithm
{
    /// <summary>Algorithm 1: 6→5→4→3→2→1 (series)</summary>
    Stack6 = 1,
    /// <summary>Algorithm 2: (6→5)+(4→3→2→1)</summary>
    Split2_4 = 2,
    /// <summary>Algorithm 3: (6→5→4)+(3→2→1)</summary>
    Split3_3 = 3,
    /// <summary>Algorithm 4: (6→5)+(4→3)+(2→1)</summary>
    Triple = 4,
    /// <summary>Algorithm 5: 6→5→(4→3→2→1)</summary>
    ModSplit = 5,
    /// <summary>Algorithm 6: (6→5→4→3)+(2→1)</summary>
    Split4_2 = 6,
    /// <summary>Algorithm 7: 6→(5+4+3)→2→1</summary>
    TripleMod = 7,
    /// <summary>Algorithm 8: 4→3→2→1, 6→5→1 (dual path)</summary>
    DualPath = 8,
    /// <summary>Algorithm 9: All parallel carriers</summary>
    AllParallel = 9,
    /// <summary>Algorithm 10: 6→5→4, 3→2→1</summary>
    DualStack = 10,
    /// <summary>Algorithm 11: (6→5)→4→3→2→1</summary>
    StackWithFB = 11,
    /// <summary>Algorithm 12: 6→5→4→(3+2+1)</summary>
    OneToThree = 12,
    /// <summary>Algorithm 13: 6→(5+4)→(3+2+1)</summary>
    TwoToThree = 13,
    /// <summary>Algorithm 14: (6+5)→(4+3)→(2+1)</summary>
    TwoByTwo = 14,
    /// <summary>Algorithm 15: 6→5, 4→3, 2→1 (three pairs)</summary>
    ThreePairs = 15,
    /// <summary>Algorithm 16: Classic electric piano</summary>
    EPiano = 16,
    /// <summary>Algorithm 17: Classic brass</summary>
    Brass = 17,
    /// <summary>Algorithm 18: Bells/chimes</summary>
    Bells = 18,
    /// <summary>Algorithm 19: Organ-style</summary>
    Organ = 19,
    /// <summary>Algorithm 20: Bass</summary>
    Bass = 20
}


/// <summary>
/// FM Synthesizer with 6 operators and configurable algorithms.
/// Based on Yamaha DX7 architecture.
/// </summary>
public class FMSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<FMVoice> _voices = new();
    private readonly Dictionary<int, FMVoice> _noteToVoice = new();
    private readonly object _lock = new();

    /// <summary>The 6 operators</summary>
    public FMOperator[] Operators { get; }

    /// <summary>Current algorithm</summary>
    public FMAlgorithm Algorithm { get; set; } = FMAlgorithm.Stack6;

    /// <summary>Synth name</summary>
    public string Name { get; set; } = "FMSynth";

    /// <summary>Audio format</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Maximum polyphony</summary>
    public int MaxVoices { get; set; } = 16;

    /// <summary>Master volume</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>Global feedback amount modifier</summary>
    public float FeedbackAmount { get; set; } = 1.0f;

    /// <summary>Pitch bend range in semitones</summary>
    public float PitchBendRange { get; set; } = 2.0f;

    /// <summary>Current pitch bend (-1 to 1)</summary>
    public float PitchBend { get; set; } = 0f;

    /// <summary>LFO for vibrato</summary>
    public LFO? VibratoLFO { get; set; }

    /// <summary>Vibrato depth in semitones</summary>
    public float VibratoDepth { get; set; } = 0f;

    /// <summary>
    /// Creates an FM synth with 6 operators
    /// </summary>
    public FMSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize 6 operators
        Operators = new FMOperator[6];
        for (int i = 0; i < 6; i++)
        {
            Operators[i] = new FMOperator
            {
                Ratio = 1.0f,
                Level = i == 0 ? 1.0f : 0.5f, // First operator louder
                IsCarrier = i == 0 // Default: only first is carrier
            };
        }

        // Default to a simple algorithm
        SetAlgorithm(FMAlgorithm.Stack6);
    }

    /// <summary>
    /// Set the FM algorithm
    /// </summary>
    public void SetAlgorithm(FMAlgorithm algorithm)
    {
        Algorithm = algorithm;

        // Reset carrier status
        foreach (var op in Operators)
        {
            op.IsCarrier = false;
        }

        // Set carriers based on algorithm
        switch (algorithm)
        {
            case FMAlgorithm.Stack6:
                Operators[0].IsCarrier = true;
                break;
            case FMAlgorithm.Split2_4:
                Operators[0].IsCarrier = true;
                Operators[4].IsCarrier = true;
                break;
            case FMAlgorithm.Split3_3:
                Operators[0].IsCarrier = true;
                Operators[3].IsCarrier = true;
                break;
            case FMAlgorithm.Triple:
                Operators[0].IsCarrier = true;
                Operators[2].IsCarrier = true;
                Operators[4].IsCarrier = true;
                break;
            case FMAlgorithm.AllParallel:
                foreach (var op in Operators) op.IsCarrier = true;
                break;
            case FMAlgorithm.DualStack:
                Operators[0].IsCarrier = true;
                Operators[3].IsCarrier = true;
                break;
            case FMAlgorithm.OneToThree:
                Operators[0].IsCarrier = true;
                Operators[1].IsCarrier = true;
                Operators[2].IsCarrier = true;
                break;
            case FMAlgorithm.TwoToThree:
                Operators[0].IsCarrier = true;
                Operators[1].IsCarrier = true;
                Operators[2].IsCarrier = true;
                break;
            case FMAlgorithm.ThreePairs:
                Operators[0].IsCarrier = true;
                Operators[2].IsCarrier = true;
                Operators[4].IsCarrier = true;
                break;
            case FMAlgorithm.EPiano:
            case FMAlgorithm.Brass:
            case FMAlgorithm.Bells:
            case FMAlgorithm.Bass:
                Operators[0].IsCarrier = true;
                Operators[1].IsCarrier = true;
                break;
            case FMAlgorithm.Organ:
                Operators[0].IsCarrier = true;
                Operators[1].IsCarrier = true;
                Operators[2].IsCarrier = true;
                break;
            default:
                Operators[0].IsCarrier = true;
                break;
        }
    }

    /// <summary>
    /// Configure an operator
    /// </summary>
    public void SetOperator(int index, float ratio, float level, float detune = 0f,
                            float feedback = 0f, FMWaveform waveform = FMWaveform.Sine)
    {
        if (index < 0 || index >= 6) return;

        var op = Operators[index];
        op.Ratio = ratio;
        op.Level = level;
        op.Detune = detune;
        op.Feedback = feedback;
        op.Waveform = waveform;
    }

    /// <summary>
    /// Set operator envelope
    /// </summary>
    public void SetOperatorEnvelope(int index, double attack, double decay, double sustain, double release)
    {
        if (index < 0 || index >= 6) return;

        var env = Operators[index].Envelope;
        env.Attack = attack;
        env.Decay = decay;
        env.Sustain = sustain;
        env.Release = release;
    }

    /// <summary>
    /// Trigger a note
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        lock (_lock)
        {
            if (_noteToVoice.TryGetValue(note, out var existingVoice))
            {
                existingVoice.Trigger(note, velocity);
                return;
            }

            FMVoice? voice = null;

            // Find inactive voice
            foreach (var v in _voices)
            {
                if (!v.IsActive)
                {
                    voice = v;
                    break;
                }
            }

            // Create new voice if needed
            if (voice == null && _voices.Count < MaxVoices)
            {
                voice = new FMVoice(_waveFormat.SampleRate, this);
                _voices.Add(voice);
            }

            // Voice stealing
            if (voice == null && _voices.Count > 0)
            {
                voice = _voices[0];
                DateTime oldest = voice.TriggerTime;
                foreach (var v in _voices)
                {
                    if (v.TriggerTime < oldest)
                    {
                        oldest = v.TriggerTime;
                        voice = v;
                    }
                }

                int oldNote = voice.Note;
                _noteToVoice.Remove(oldNote);
            }

            if (voice != null)
            {
                voice.Trigger(note, velocity);
                _noteToVoice[note] = voice;
            }
        }
    }

    /// <summary>
    /// Release a note
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
    /// Release all notes
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
    /// Set parameter by name
    /// </summary>
    public void SetParameter(string name, float value)
    {
        var parts = name.ToLowerInvariant().Split('_');

        // Check for operator-specific parameters (e.g., "op1_ratio")
        if (parts.Length >= 2 && parts[0].StartsWith("op") && int.TryParse(parts[0].Substring(2), out int opIndex))
        {
            opIndex--; // Convert to 0-based
            if (opIndex >= 0 && opIndex < 6)
            {
                var op = Operators[opIndex];
                switch (parts[1])
                {
                    case "ratio": op.Ratio = value; break;
                    case "level": op.Level = Math.Clamp(value, 0f, 1f); break;
                    case "detune": op.Detune = value; break;
                    case "feedback": op.Feedback = Math.Clamp(value, 0f, 1f); break;
                    case "attack": op.Envelope.Attack = value; break;
                    case "decay": op.Envelope.Decay = value; break;
                    case "sustain": op.Envelope.Sustain = value; break;
                    case "release": op.Envelope.Release = value; break;
                    case "velocity": op.VelocitySensitivity = Math.Clamp(value, 0f, 1f); break;
                }
                return;
            }
        }

        // Global parameters
        switch (name.ToLowerInvariant())
        {
            case "volume":
                Volume = Math.Clamp(value, 0f, 1f);
                break;
            case "algorithm":
                SetAlgorithm((FMAlgorithm)(int)value);
                break;
            case "feedback":
                FeedbackAmount = Math.Clamp(value, 0f, 2f);
                break;
            case "pitchbend":
                PitchBend = Math.Clamp(value, -1f, 1f);
                break;
            case "pitchbendrange":
                PitchBendRange = value;
                break;
            case "vibratodepth":
                VibratoDepth = value;
                break;
        }
    }

    /// <summary>
    /// Read audio samples
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            buffer[offset + i] = 0;
        }

        int channels = _waveFormat.Channels;
        double deltaTime = 1.0 / _waveFormat.SampleRate;

        // Calculate pitch modulation
        float pitchMod = PitchBend * PitchBendRange;
        if (VibratoLFO != null && VibratoLFO.Enabled)
        {
            pitchMod += (float)(VibratoLFO.GetValue(_waveFormat.SampleRate) * VibratoDepth);
        }

        lock (_lock)
        {
            for (int n = 0; n < count; n += channels)
            {
                float sample = 0f;

                foreach (var voice in _voices)
                {
                    if (!voice.IsActive) continue;
                    sample += voice.Process(deltaTime, pitchMod);
                }

                // Apply volume and soft clipping
                sample *= Volume;
                sample = MathF.Tanh(sample);

                // Output to all channels
                for (int c = 0; c < channels; c++)
                {
                    buffer[offset + n + c] = sample;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Create a classic electric piano preset
    /// </summary>
    public static FMSynth CreateEPianoPreset()
    {
        var synth = new FMSynth();
        synth.Name = "E-Piano";
        synth.SetAlgorithm(FMAlgorithm.EPiano);

        // Carrier 1 (fundamental)
        synth.SetOperator(0, 1.0f, 0.8f);
        synth.SetOperatorEnvelope(0, 0.001, 1.5, 0.0, 0.5);

        // Carrier 2 (octave)
        synth.SetOperator(1, 2.0f, 0.3f);
        synth.SetOperatorEnvelope(1, 0.001, 1.0, 0.0, 0.3);

        // Modulator for carrier 1
        synth.SetOperator(2, 1.0f, 0.5f);
        synth.SetOperatorEnvelope(2, 0.001, 0.5, 0.0, 0.2);

        // Modulator for carrier 2
        synth.SetOperator(3, 14.0f, 0.3f); // Inharmonic for bell-like tone
        synth.SetOperatorEnvelope(3, 0.001, 0.3, 0.0, 0.1);

        return synth;
    }

    /// <summary>
    /// Create a brass preset
    /// </summary>
    public static FMSynth CreateBrassPreset()
    {
        var synth = new FMSynth();
        synth.Name = "Brass";
        synth.SetAlgorithm(FMAlgorithm.Brass);

        synth.SetOperator(0, 1.0f, 0.9f);
        synth.SetOperatorEnvelope(0, 0.1, 0.2, 0.8, 0.2);

        synth.SetOperator(1, 1.0f, 0.7f);
        synth.SetOperatorEnvelope(1, 0.1, 0.2, 0.8, 0.2);

        synth.SetOperator(2, 1.0f, 0.6f);
        synth.SetOperatorEnvelope(2, 0.05, 0.3, 0.5, 0.1);

        synth.SetOperator(3, 2.0f, 0.5f);
        synth.SetOperatorEnvelope(3, 0.05, 0.2, 0.3, 0.1);

        return synth;
    }

    /// <summary>
    /// Create a bell preset
    /// </summary>
    public static FMSynth CreateBellPreset()
    {
        var synth = new FMSynth();
        synth.Name = "Bell";
        synth.SetAlgorithm(FMAlgorithm.Bells);

        // Inharmonic ratios for metallic bell sound
        synth.SetOperator(0, 1.0f, 0.8f);
        synth.SetOperatorEnvelope(0, 0.001, 3.0, 0.0, 2.0);

        synth.SetOperator(1, 3.5f, 0.6f);
        synth.SetOperatorEnvelope(1, 0.001, 2.0, 0.0, 1.5);

        synth.SetOperator(2, 1.0f, 0.7f);
        synth.SetOperatorEnvelope(2, 0.001, 1.0, 0.0, 0.5);

        synth.SetOperator(3, 7.0f, 0.5f);
        synth.SetOperatorEnvelope(3, 0.001, 0.5, 0.0, 0.3);

        synth.SetOperator(4, 11.0f, 0.3f);
        synth.SetOperatorEnvelope(4, 0.001, 0.3, 0.0, 0.2);

        return synth;
    }

    /// <summary>
    /// Create a bass preset
    /// </summary>
    public static FMSynth CreateBassPreset()
    {
        var synth = new FMSynth();
        synth.Name = "FM Bass";
        synth.SetAlgorithm(FMAlgorithm.Bass);

        synth.SetOperator(0, 1.0f, 0.9f);
        synth.SetOperatorEnvelope(0, 0.001, 0.2, 0.4, 0.1);

        synth.SetOperator(1, 0.5f, 0.5f); // Sub oscillator
        synth.SetOperatorEnvelope(1, 0.001, 0.15, 0.3, 0.1);

        synth.SetOperator(2, 1.0f, 0.8f);
        synth.SetOperatorEnvelope(2, 0.001, 0.1, 0.0, 0.05);

        synth.SetOperator(3, 2.0f, 0.6f);
        synth.SetOperatorEnvelope(3, 0.001, 0.08, 0.0, 0.05);

        return synth;
    }

    /// <summary>
    /// Create an organ preset
    /// </summary>
    public static FMSynth CreateOrganPreset()
    {
        var synth = new FMSynth();
        synth.Name = "Organ";
        synth.SetAlgorithm(FMAlgorithm.Organ);

        // Drawbar-like harmonic ratios
        synth.SetOperator(0, 0.5f, 0.7f); // 16'
        synth.SetOperatorEnvelope(0, 0.01, 0.01, 1.0, 0.05);

        synth.SetOperator(1, 1.0f, 0.9f); // 8'
        synth.SetOperatorEnvelope(1, 0.01, 0.01, 1.0, 0.05);

        synth.SetOperator(2, 2.0f, 0.5f); // 4'
        synth.SetOperatorEnvelope(2, 0.01, 0.01, 1.0, 0.05);

        synth.SetOperator(3, 1.5f, 0.3f); // 5 1/3' (quint)
        synth.SetOperatorEnvelope(3, 0.01, 0.01, 1.0, 0.05);

        synth.SetOperator(4, 3.0f, 0.4f); // 2 2/3'
        synth.SetOperatorEnvelope(4, 0.01, 0.01, 1.0, 0.05);

        synth.SetOperator(5, 4.0f, 0.3f); // 2'
        synth.SetOperatorEnvelope(5, 0.01, 0.01, 1.0, 0.05);

        return synth;
    }
}


/// <summary>
/// Internal voice for FM synthesis
/// </summary>
internal class FMVoice
{
    private readonly int _sampleRate;
    private readonly FMSynth _synth;
    private readonly double[] _phases = new double[6];
    private readonly double[] _lastOutputs = new double[6];
    private readonly Envelope[] _envelopes = new Envelope[6];

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public double BaseFrequency { get; private set; }
    public DateTime TriggerTime { get; private set; }

    public bool IsActive
    {
        get
        {
            foreach (var env in _envelopes)
            {
                if (env.IsActive) return true;
            }
            return false;
        }
    }

    public FMVoice(int sampleRate, FMSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;

        for (int i = 0; i < 6; i++)
        {
            _envelopes[i] = new Envelope(0.01, 0.1, 0.7, 0.3);
        }
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        BaseFrequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);
        TriggerTime = DateTime.Now;

        // Reset phases
        for (int i = 0; i < 6; i++)
        {
            _phases[i] = 0;
            _lastOutputs[i] = 0;
        }

        // Copy envelope settings and trigger
        for (int i = 0; i < 6; i++)
        {
            var srcEnv = _synth.Operators[i].Envelope;
            var op = _synth.Operators[i];

            // Apply key scaling to envelope times
            float keyScale = 1.0f + op.KeyScaling * (note - 60) / 60.0f;
            keyScale = Math.Max(0.1f, keyScale);

            _envelopes[i].Attack = srcEnv.Attack / keyScale;
            _envelopes[i].Decay = srcEnv.Decay / keyScale;
            _envelopes[i].Sustain = srcEnv.Sustain;
            _envelopes[i].Release = srcEnv.Release / keyScale;

            // Apply velocity sensitivity
            int effectiveVelocity = (int)(velocity * op.VelocitySensitivity +
                                          127 * (1.0f - op.VelocitySensitivity));
            _envelopes[i].Trigger(effectiveVelocity);
        }
    }

    public void Release()
    {
        for (int i = 0; i < 6; i++)
        {
            _envelopes[i].Release_Gate();
        }
    }

    public float Process(double deltaTime, float pitchMod)
    {
        // Process all envelopes
        double[] envValues = new double[6];
        for (int i = 0; i < 6; i++)
        {
            envValues[i] = _envelopes[i].Process(deltaTime);
        }

        // Calculate modulated frequency
        double freq = BaseFrequency * Math.Pow(2.0, pitchMod / 12.0);

        // Process operators based on algorithm
        double[] outputs = new double[6];

        // Process in reverse order (modulators before carriers)
        for (int i = 5; i >= 0; i--)
        {
            var op = _synth.Operators[i];
            if (op.Level <= 0) continue;

            // Calculate operator frequency
            double opFreq = op.FixedFrequency > 0 ? op.FixedFrequency : freq * op.Ratio;
            opFreq *= Math.Pow(2.0, op.Detune / 1200.0);

            // Get modulation input based on algorithm
            double modInput = GetModulationInput(i, outputs);

            // Add self-feedback
            double feedback = _lastOutputs[i] * op.Feedback * _synth.FeedbackAmount * Math.PI;

            // Calculate phase with modulation
            double phase = _phases[i] + modInput + feedback;

            // Generate waveform
            double sample = GenerateWaveform(phase, op.Waveform);

            // Apply envelope and level
            double output = sample * envValues[i] * op.Level;

            outputs[i] = output;
            _lastOutputs[i] = output;

            // Update phase
            _phases[i] += 2.0 * Math.PI * opFreq / _sampleRate;
            if (_phases[i] >= 2.0 * Math.PI)
                _phases[i] -= 2.0 * Math.PI;
        }

        // Sum carrier outputs
        float result = 0f;
        for (int i = 0; i < 6; i++)
        {
            if (_synth.Operators[i].IsCarrier)
            {
                result += (float)outputs[i];
            }
        }

        return result;
    }

    private double GetModulationInput(int opIndex, double[] outputs)
    {
        double mod = 0;

        switch (_synth.Algorithm)
        {
            case FMAlgorithm.Stack6:
                // 6→5→4→3→2→1
                if (opIndex < 5) mod = outputs[opIndex + 1] * Math.PI * 2;
                break;

            case FMAlgorithm.Split2_4:
                // (6→5) + (4→3→2→1)
                if (opIndex == 4) mod = outputs[5] * Math.PI * 2;
                else if (opIndex < 3) mod = outputs[opIndex + 1] * Math.PI * 2;
                break;

            case FMAlgorithm.Split3_3:
                // (6→5→4) + (3→2→1)
                if (opIndex == 4 || opIndex == 5) mod = opIndex < 5 ? outputs[opIndex + 1] * Math.PI * 2 : 0;
                else if (opIndex < 2) mod = outputs[opIndex + 1] * Math.PI * 2;
                break;

            case FMAlgorithm.Triple:
                // (6→5) + (4→3) + (2→1)
                if (opIndex == 4) mod = outputs[5] * Math.PI * 2;
                else if (opIndex == 2) mod = outputs[3] * Math.PI * 2;
                else if (opIndex == 0) mod = outputs[1] * Math.PI * 2;
                break;

            case FMAlgorithm.AllParallel:
                // All carriers, no modulation between operators
                break;

            case FMAlgorithm.DualStack:
                // (6→5→4) + (3→2→1)
                if (opIndex >= 3 && opIndex < 5) mod = outputs[opIndex + 1] * Math.PI * 2;
                else if (opIndex < 2) mod = outputs[opIndex + 1] * Math.PI * 2;
                break;

            case FMAlgorithm.OneToThree:
                // 6→5→4→(3+2+1)
                if (opIndex >= 3 && opIndex < 5) mod = outputs[opIndex + 1] * Math.PI * 2;
                else if (opIndex < 3) mod = outputs[3] * Math.PI * 2;
                break;

            case FMAlgorithm.ThreePairs:
                // (6→5) + (4→3) + (2→1)
                if (opIndex % 2 == 0 && opIndex < 5)
                    mod = outputs[opIndex + 1] * Math.PI * 2;
                break;

            case FMAlgorithm.EPiano:
            case FMAlgorithm.Brass:
            case FMAlgorithm.Bass:
                // Two carriers with modulators
                if (opIndex == 0) mod = outputs[2] * Math.PI * 2;
                else if (opIndex == 1) mod = outputs[3] * Math.PI * 2;
                else if (opIndex == 2) mod = outputs[4] * Math.PI * 2;
                else if (opIndex == 3) mod = outputs[5] * Math.PI * 2;
                break;

            case FMAlgorithm.Bells:
                // Complex modulation for metallic sounds
                if (opIndex == 0) mod = (outputs[2] + outputs[3]) * Math.PI;
                else if (opIndex == 1) mod = (outputs[4] + outputs[5]) * Math.PI;
                break;

            case FMAlgorithm.Organ:
                // Mostly parallel with slight cross-modulation
                if (opIndex < 3) mod = outputs[opIndex + 3] * Math.PI * 0.5;
                break;

            default:
                if (opIndex < 5) mod = outputs[opIndex + 1] * Math.PI * 2;
                break;
        }

        return mod;
    }

    private double GenerateWaveform(double phase, FMWaveform waveform)
    {
        // Normalize phase
        phase = phase % (2 * Math.PI);
        if (phase < 0) phase += 2 * Math.PI;

        return waveform switch
        {
            FMWaveform.Sine => Math.Sin(phase),
            FMWaveform.Triangle => phase < Math.PI
                ? (2.0 * phase / Math.PI - 1.0)
                : (3.0 - 2.0 * phase / Math.PI),
            FMWaveform.Sawtooth => (phase / Math.PI) - 1.0,
            FMWaveform.Square => phase < Math.PI ? 1.0 : -1.0,
            FMWaveform.Feedback => Math.Sin(phase), // Handled separately with feedback
            _ => Math.Sin(phase)
        };
    }
}
