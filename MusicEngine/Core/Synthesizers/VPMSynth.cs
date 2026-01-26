// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Synthesizer component.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// CZ-style phase distortion waveform types.
/// </summary>
public enum VPMWaveform
{
    /// <summary>Sawtooth wave.</summary>
    Sawtooth,
    /// <summary>Square wave.</summary>
    Square,
    /// <summary>Pulse wave (narrow).</summary>
    Pulse,
    /// <summary>Double sine (octave).</summary>
    DoubleSine,
    /// <summary>Saw-pulse hybrid.</summary>
    SawPulse,
    /// <summary>Resonant waveform 1 (peak at start).</summary>
    Resonant1,
    /// <summary>Resonant waveform 2 (peak at middle).</summary>
    Resonant2,
    /// <summary>Resonant waveform 3 (peak at end).</summary>
    Resonant3
}

/// <summary>
/// VPM envelope stage data.
/// </summary>
public class VPMEnvelopeStage
{
    /// <summary>Rate for this stage (0-99, higher = faster).</summary>
    public int Rate { get; set; } = 50;
    /// <summary>Level for this stage (0-99).</summary>
    public int Level { get; set; } = 99;

    public VPMEnvelopeStage() { }
    public VPMEnvelopeStage(int rate, int level)
    {
        Rate = rate;
        Level = level;
    }
}

/// <summary>
/// VPM 8-stage envelope (CZ-style).
/// </summary>
public class VPMEnvelope
{
    /// <summary>The 8 envelope stages.</summary>
    public VPMEnvelopeStage[] Stages { get; } = new VPMEnvelopeStage[8];
    /// <summary>Sustain point (which stage to hold at, 0-7).</summary>
    public int SustainPoint { get; set; } = 3;
    /// <summary>End point (which stage to stop at, 0-7).</summary>
    public int EndPoint { get; set; } = 7;

    public VPMEnvelope()
    {
        for (int i = 0; i < 8; i++)
        {
            Stages[i] = new VPMEnvelopeStage(50, i < 4 ? 99 - i * 25 : 0);
        }
    }

    /// <summary>
    /// Sets a simple ADSR-style envelope.
    /// </summary>
    public void SetADSR(int attack, int decay, int sustain, int release)
    {
        // Attack
        Stages[0] = new VPMEnvelopeStage(attack, 99);
        // Decay
        Stages[1] = new VPMEnvelopeStage(decay, sustain);
        // Sustain (hold)
        Stages[2] = new VPMEnvelopeStage(99, sustain);
        Stages[3] = new VPMEnvelopeStage(99, sustain);
        SustainPoint = 3;
        // Release
        Stages[4] = new VPMEnvelopeStage(release, 0);
        Stages[5] = new VPMEnvelopeStage(99, 0);
        Stages[6] = new VPMEnvelopeStage(99, 0);
        Stages[7] = new VPMEnvelopeStage(99, 0);
        EndPoint = 4;
    }
}

/// <summary>
/// Internal VPM voice state.
/// </summary>
internal class VPMVoice
{
    private readonly int _sampleRate;
    private readonly VPMSynth _synth;

    // Oscillator state
    private double _phase1;
    private double _phase2;

    // Envelope state
    private double _dcaValue;
    private double _dcwValue;
    private double _dcoValue;
    private int _dcaStage;
    private int _dcwStage;
    private int _dcoStage;
    private double _dcaProgress;
    private double _dcwProgress;
    private double _dcoProgress;
    private double _dcaStartLevel;
    private double _dcwStartLevel;
    private double _dcoStartLevel;
    private bool _gateOn;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public double BaseFrequency { get; private set; }

    public VPMVoice(int sampleRate, VPMSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        IsActive = true;
        TriggerTime = DateTime.Now;
        BaseFrequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);

        // Reset phases
        _phase1 = 0;
        _phase2 = 0;

        // Reset envelopes
        _dcaStage = 0;
        _dcwStage = 0;
        _dcoStage = 0;
        _dcaProgress = 0;
        _dcwProgress = 0;
        _dcoProgress = 0;
        _dcaValue = _synth.DcaEnvelope.Stages[0].Level / 99.0;
        _dcwValue = _synth.DcwEnvelope.Stages[0].Level / 99.0;
        _dcoValue = _synth.DcoEnvelope.Stages[0].Level / 99.0;
        _dcaStartLevel = 0;
        _dcwStartLevel = _dcwValue;
        _dcoStartLevel = _dcoValue;
        _gateOn = true;
    }

    public void Release()
    {
        _gateOn = false;

        // Move to release stages (after sustain point)
        var dcaEnv = _synth.DcaEnvelope;
        var dcwEnv = _synth.DcwEnvelope;
        var dcoEnv = _synth.DcoEnvelope;

        if (_dcaStage <= dcaEnv.SustainPoint)
        {
            _dcaStage = dcaEnv.SustainPoint + 1;
            _dcaProgress = 0;
            _dcaStartLevel = _dcaValue;
        }

        if (_dcwStage <= dcwEnv.SustainPoint)
        {
            _dcwStage = dcwEnv.SustainPoint + 1;
            _dcwProgress = 0;
            _dcwStartLevel = _dcwValue;
        }

        if (_dcoStage <= dcoEnv.SustainPoint)
        {
            _dcoStage = dcoEnv.SustainPoint + 1;
            _dcoProgress = 0;
            _dcoStartLevel = _dcoValue;
        }
    }

    public float Process(double deltaTime)
    {
        if (!IsActive) return 0f;

        // Process envelopes
        ProcessEnvelope(ref _dcaValue, ref _dcaStage, ref _dcaProgress, ref _dcaStartLevel,
                       _synth.DcaEnvelope, deltaTime);
        ProcessEnvelope(ref _dcwValue, ref _dcwStage, ref _dcwProgress, ref _dcwStartLevel,
                       _synth.DcwEnvelope, deltaTime);
        ProcessEnvelope(ref _dcoValue, ref _dcoStage, ref _dcoProgress, ref _dcoStartLevel,
                       _synth.DcoEnvelope, deltaTime);

        // Check if done
        if (_dcaStage >= _synth.DcaEnvelope.EndPoint && _dcaValue < 0.001)
        {
            IsActive = false;
            return 0f;
        }

        // Calculate frequency with DCO (pitch) envelope
        double pitchMod = (_dcoValue - 0.5) * 2.0 * _synth.DcoDepth; // -1 to +1 range
        double freq = BaseFrequency * Math.Pow(2.0, pitchMod);

        // Apply detune
        double freq2 = freq * Math.Pow(2.0, _synth.Detune / 1200.0);

        // Generate phase distortion waveforms
        double output1 = GeneratePDWaveform(ref _phase1, freq, _synth.Waveform1, _dcwValue);
        double output2 = 0;

        if (_synth.Line2Enable)
        {
            output2 = GeneratePDWaveform(ref _phase2, freq2, _synth.Waveform2, _dcwValue);
        }

        // Mix lines based on mode
        double output;
        switch (_synth.LineMode)
        {
            case VPMLineMode.Mix:
                output = (output1 + output2 * _synth.Line2Level) / (1.0 + _synth.Line2Level);
                break;
            case VPMLineMode.RingMod:
                output = output1 * output2;
                break;
            case VPMLineMode.Sync:
                // Simplified sync - reset phase2 when phase1 crosses
                if (_phase1 < 0.01)
                    _phase2 = 0;
                output = (output1 + output2 * _synth.Line2Level) / (1.0 + _synth.Line2Level);
                break;
            default:
                output = output1;
                break;
        }

        // Apply DCA (amplitude) envelope and velocity
        double velocityGain = Velocity / 127.0;
        output *= _dcaValue * velocityGain;

        return (float)output;
    }

    private double GeneratePDWaveform(ref double phase, double freq, VPMWaveform waveform, double dcw)
    {
        // Update phase
        double phaseInc = freq / _sampleRate;
        phase += phaseInc;
        if (phase >= 1.0) phase -= 1.0;

        // DCW controls the "cutoff" - amount of phase distortion
        // At dcw=0, all waveforms become sine
        // At dcw=1, full phase distortion

        double t = phase; // 0 to 1
        double distortedPhase;

        switch (waveform)
        {
            case VPMWaveform.Sawtooth:
                // Phase distortion for sawtooth
                if (t < 0.5 * (1.0 - dcw * 0.5))
                {
                    distortedPhase = t / (1.0 - dcw * 0.5);
                }
                else
                {
                    distortedPhase = 0.5 + (t - 0.5 * (1.0 - dcw * 0.5)) / (1.0 + dcw * 0.5) * 0.5;
                }
                return Math.Sin(distortedPhase * 2.0 * Math.PI);

            case VPMWaveform.Square:
                // Square wave via phase distortion
                double threshold = 0.5 - dcw * 0.4;
                if (t < threshold)
                {
                    distortedPhase = t / threshold * 0.25;
                }
                else if (t < 0.5)
                {
                    distortedPhase = 0.25 + (t - threshold) / (0.5 - threshold) * 0.25;
                }
                else if (t < 0.5 + threshold)
                {
                    distortedPhase = 0.5 + (t - 0.5) / threshold * 0.25;
                }
                else
                {
                    distortedPhase = 0.75 + (t - 0.5 - threshold) / (0.5 - threshold) * 0.25;
                }
                return Math.Sin(distortedPhase * 2.0 * Math.PI);

            case VPMWaveform.Pulse:
                // Narrow pulse
                double pulseWidth = 0.1 + (1.0 - dcw) * 0.4;
                if (t < pulseWidth)
                {
                    distortedPhase = t / pulseWidth * 0.5;
                }
                else
                {
                    distortedPhase = 0.5 + (t - pulseWidth) / (1.0 - pulseWidth) * 0.5;
                }
                return Math.Sin(distortedPhase * 2.0 * Math.PI);

            case VPMWaveform.DoubleSine:
                // Octave-up effect
                double speed = 1.0 + dcw;
                distortedPhase = t * speed;
                if (distortedPhase >= 1.0) distortedPhase -= Math.Floor(distortedPhase);
                return Math.Sin(distortedPhase * 2.0 * Math.PI);

            case VPMWaveform.SawPulse:
                // Hybrid sawtooth-pulse
                if (t < 0.25 * (1.0 - dcw * 0.5))
                {
                    distortedPhase = t / (0.25 * (1.0 - dcw * 0.5)) * 0.5;
                }
                else
                {
                    distortedPhase = 0.5 + (t - 0.25 * (1.0 - dcw * 0.5)) / (1.0 - 0.25 * (1.0 - dcw * 0.5)) * 0.5;
                }
                return Math.Sin(distortedPhase * 2.0 * Math.PI);

            case VPMWaveform.Resonant1:
                // Resonant peak at start - like lowpass filter with resonance
                double res1 = 1.0 + dcw * 3.0;
                distortedPhase = t;
                double env1 = Math.Exp(-t * res1 * 2.0);
                return Math.Sin(distortedPhase * 2.0 * Math.PI) * (1.0 - dcw * 0.5 + env1 * dcw * 0.5);

            case VPMWaveform.Resonant2:
                // Resonant peak in middle
                double res2 = 1.0 + dcw * 3.0;
                double mid = Math.Abs(t - 0.5) * 2.0;
                double env2 = Math.Exp(-mid * res2 * 2.0);
                return Math.Sin(t * 2.0 * Math.PI) * (1.0 - dcw * 0.3 + env2 * dcw * 0.3);

            case VPMWaveform.Resonant3:
                // Resonant peak at end
                double res3 = 1.0 + dcw * 3.0;
                double env3 = Math.Exp(-(1.0 - t) * res3 * 2.0);
                return Math.Sin(t * 2.0 * Math.PI) * (1.0 - dcw * 0.5 + env3 * dcw * 0.5);

            default:
                return Math.Sin(t * 2.0 * Math.PI);
        }
    }

    private void ProcessEnvelope(ref double value, ref int stage, ref double progress, ref double startLevel,
                                  VPMEnvelope envelope, double deltaTime)
    {
        if (stage >= 8) return;

        var currentStage = envelope.Stages[stage];
        double targetLevel = currentStage.Level / 99.0;

        // Rate to time conversion (higher rate = faster)
        double rateTime = (100 - currentStage.Rate) / 99.0 * 2.0 + 0.01;

        progress += deltaTime / rateTime;

        if (progress >= 1.0)
        {
            // Move to next stage
            value = targetLevel;
            startLevel = value;
            progress = 0;

            // Check sustain point
            if (_gateOn && stage == envelope.SustainPoint)
            {
                // Hold at sustain
                return;
            }

            stage++;
            if (stage >= envelope.EndPoint)
            {
                stage = envelope.EndPoint;
            }
        }
        else
        {
            // Interpolate
            value = startLevel + (targetLevel - startLevel) * progress;
        }
    }
}

/// <summary>
/// Line combination modes.
/// </summary>
public enum VPMLineMode
{
    /// <summary>Mix both lines.</summary>
    Mix,
    /// <summary>Ring modulation.</summary>
    RingMod,
    /// <summary>Hard sync line 2 to line 1.</summary>
    Sync
}

/// <summary>
/// Casio CZ style phase modulation (VPM) synthesizer.
/// Features phase distortion waveforms, 8 waveform types, DCW envelope, DCO pitch envelope.
/// </summary>
public class VPMSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<VPMVoice> _voices = new();
    private readonly Dictionary<int, VPMVoice> _noteToVoice = new();
    private readonly object _lock = new();

    /// <summary>Gets or sets the synth name.</summary>
    public string Name { get; set; } = "VPMSynth";

    /// <summary>Gets the audio format.</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Gets or sets the master volume (0-1).</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>Gets or sets the maximum number of voices.</summary>
    public int MaxVoices { get; set; } = 8;

    // Line 1 parameters
    /// <summary>Waveform for line 1.</summary>
    public VPMWaveform Waveform1 { get; set; } = VPMWaveform.Sawtooth;

    // Line 2 parameters
    /// <summary>Enable line 2.</summary>
    public bool Line2Enable { get; set; } = false;
    /// <summary>Waveform for line 2.</summary>
    public VPMWaveform Waveform2 { get; set; } = VPMWaveform.Sawtooth;
    /// <summary>Line 2 level (0-1).</summary>
    public float Line2Level { get; set; } = 0.5f;
    /// <summary>Line combination mode.</summary>
    public VPMLineMode LineMode { get; set; } = VPMLineMode.Mix;

    // Tuning
    /// <summary>Detune in cents.</summary>
    public float Detune { get; set; } = 0f;

    // Envelopes
    /// <summary>DCA (amplitude) envelope.</summary>
    public VPMEnvelope DcaEnvelope { get; } = new VPMEnvelope();
    /// <summary>DCW (wave shape/cutoff) envelope.</summary>
    public VPMEnvelope DcwEnvelope { get; } = new VPMEnvelope();
    /// <summary>DCO (pitch) envelope.</summary>
    public VPMEnvelope DcoEnvelope { get; } = new VPMEnvelope();

    /// <summary>DCO (pitch) envelope depth in octaves.</summary>
    public float DcoDepth { get; set; } = 0f;

    /// <summary>
    /// Creates a new VPM synth.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (default: from Settings).</param>
    public VPMSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize default envelopes
        DcaEnvelope.SetADSR(80, 50, 70, 40);
        DcwEnvelope.SetADSR(90, 40, 50, 30);
        DcoEnvelope.SetADSR(99, 99, 50, 99);
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
                existingVoice.Trigger(note, velocity);
                return;
            }

            var voice = GetFreeVoice();
            if (voice == null) return;

            voice.Trigger(note, velocity);
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
        switch (name.ToLowerInvariant())
        {
            case "volume": Volume = Math.Clamp(value, 0f, 1f); break;
            case "waveform1": Waveform1 = (VPMWaveform)(int)value; break;
            case "line2enable": Line2Enable = value > 0.5f; break;
            case "waveform2": Waveform2 = (VPMWaveform)(int)value; break;
            case "line2level": Line2Level = Math.Clamp(value, 0f, 1f); break;
            case "linemode": LineMode = (VPMLineMode)(int)value; break;
            case "detune": Detune = Math.Clamp(value, -100f, 100f); break;
            case "dcodepth": DcoDepth = Math.Clamp(value, 0f, 4f); break;

            // Simple ADSR for each envelope
            case "dcaattack": DcaEnvelope.Stages[0].Rate = Math.Clamp((int)value, 0, 99); break;
            case "dcadecay": DcaEnvelope.Stages[1].Rate = Math.Clamp((int)value, 0, 99); break;
            case "dcasustain": DcaEnvelope.Stages[1].Level = Math.Clamp((int)value, 0, 99);
                              DcaEnvelope.Stages[2].Level = Math.Clamp((int)value, 0, 99);
                              DcaEnvelope.Stages[3].Level = Math.Clamp((int)value, 0, 99); break;
            case "dcarelease": DcaEnvelope.Stages[4].Rate = Math.Clamp((int)value, 0, 99); break;

            case "dcwattack": DcwEnvelope.Stages[0].Rate = Math.Clamp((int)value, 0, 99); break;
            case "dcwdecay": DcwEnvelope.Stages[1].Rate = Math.Clamp((int)value, 0, 99); break;
            case "dcwsustain": DcwEnvelope.Stages[1].Level = Math.Clamp((int)value, 0, 99);
                              DcwEnvelope.Stages[2].Level = Math.Clamp((int)value, 0, 99);
                              DcwEnvelope.Stages[3].Level = Math.Clamp((int)value, 0, 99); break;
            case "dcwrelease": DcwEnvelope.Stages[4].Rate = Math.Clamp((int)value, 0, 99); break;
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

    private VPMVoice? GetFreeVoice()
    {
        foreach (var voice in _voices)
        {
            if (!voice.IsActive) return voice;
        }

        if (_voices.Count < MaxVoices)
        {
            var voice = new VPMVoice(_waveFormat.SampleRate, this);
            _voices.Add(voice);
            return voice;
        }

        // Voice stealing
        VPMVoice? oldest = null;
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

    /// <summary>Creates a classic CZ bass preset.</summary>
    public static VPMSynth CreateCZBass()
    {
        var synth = new VPMSynth { Name = "CZ Bass" };
        synth.Waveform1 = VPMWaveform.Resonant1;
        synth.DcaEnvelope.SetADSR(95, 60, 50, 50);
        synth.DcwEnvelope.SetADSR(99, 40, 20, 30);
        return synth;
    }

    /// <summary>Creates a CZ organ preset.</summary>
    public static VPMSynth CreateCZOrgan()
    {
        var synth = new VPMSynth { Name = "CZ Organ" };
        synth.Waveform1 = VPMWaveform.Square;
        synth.Line2Enable = true;
        synth.Waveform2 = VPMWaveform.Square;
        synth.Line2Level = 0.5f;
        synth.LineMode = VPMLineMode.Mix;
        synth.Detune = 0f;
        synth.DcaEnvelope.SetADSR(99, 99, 99, 60);
        synth.DcwEnvelope.SetADSR(99, 99, 80, 50);
        return synth;
    }

    /// <summary>Creates a CZ strings preset.</summary>
    public static VPMSynth CreateCZStrings()
    {
        var synth = new VPMSynth { Name = "CZ Strings" };
        synth.Waveform1 = VPMWaveform.Sawtooth;
        synth.Line2Enable = true;
        synth.Waveform2 = VPMWaveform.Sawtooth;
        synth.Line2Level = 0.7f;
        synth.Detune = 8f;
        synth.DcaEnvelope.SetADSR(30, 50, 80, 40);
        synth.DcwEnvelope.SetADSR(20, 40, 60, 30);
        return synth;
    }

    /// <summary>Creates a CZ bells preset.</summary>
    public static VPMSynth CreateCZBells()
    {
        var synth = new VPMSynth { Name = "CZ Bells" };
        synth.Waveform1 = VPMWaveform.Resonant2;
        synth.Line2Enable = true;
        synth.Waveform2 = VPMWaveform.DoubleSine;
        synth.Line2Level = 0.4f;
        synth.LineMode = VPMLineMode.RingMod;
        synth.DcaEnvelope.SetADSR(99, 30, 0, 20);
        synth.DcwEnvelope.SetADSR(99, 20, 30, 15);
        return synth;
    }

    /// <summary>Creates a CZ sync lead preset.</summary>
    public static VPMSynth CreateCZSyncLead()
    {
        var synth = new VPMSynth { Name = "CZ Sync Lead" };
        synth.Waveform1 = VPMWaveform.SawPulse;
        synth.Line2Enable = true;
        synth.Waveform2 = VPMWaveform.Sawtooth;
        synth.Line2Level = 0.6f;
        synth.LineMode = VPMLineMode.Sync;
        synth.Detune = 700f; // Large detune for sync effect
        synth.DcaEnvelope.SetADSR(90, 50, 70, 40);
        synth.DcwEnvelope.SetADSR(70, 40, 40, 30);
        synth.DcoDepth = 0.5f;
        return synth;
    }

    #endregion
}
