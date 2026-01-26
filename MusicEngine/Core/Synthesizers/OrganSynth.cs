// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Synthesizer component.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// Rotary speaker speed mode.
/// </summary>
public enum RotarySpeed
{
    /// <summary>Stopped/brake.</summary>
    Stop,
    /// <summary>Slow/chorale speed (~40 RPM).</summary>
    Slow,
    /// <summary>Fast/tremolo speed (~340 RPM).</summary>
    Fast
}

/// <summary>
/// Percussion harmonic selection.
/// </summary>
public enum PercussionHarmonic
{
    /// <summary>Second harmonic (one octave up).</summary>
    Second,
    /// <summary>Third harmonic (octave + fifth).</summary>
    Third
}

/// <summary>
/// Internal organ voice state.
/// </summary>
internal class OrganVoice
{
    private readonly int _sampleRate;
    private readonly OrganSynth _synth;
    private readonly Random _random;

    // Tonewheel phases (9 drawbars)
    private readonly double[] _tonewheelPhases = new double[9];

    // Key click state
    private double _clickTime;
    private bool _clickActive;

    // Percussion state
    private double _percEnvelope;
    private double _percPhase;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public double BaseFrequency { get; private set; }

    // Hammond drawbar footage ratios
    // 16', 5 1/3', 8', 4', 2 2/3', 2', 1 3/5', 1 1/3', 1'
    private static readonly double[] DrawbarRatios = { 0.5, 1.5, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 8.0 };

    public OrganVoice(int sampleRate, OrganSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;
        _random = new Random();
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        IsActive = true;
        TriggerTime = DateTime.Now;
        BaseFrequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);

        // Reset phases with slight randomization (tonewheel character)
        for (int i = 0; i < 9; i++)
        {
            _tonewheelPhases[i] = _random.NextDouble() * 0.1;
        }

        // Key click
        _clickTime = 0;
        _clickActive = _synth.KeyClickLevel > 0;

        // Percussion
        _percEnvelope = _synth.PercussionEnabled ? 1.0 : 0.0;
        _percPhase = 0;
    }

    public void Release()
    {
        // Organs typically have instant release
        // But we add a tiny fade to avoid clicks
        _clickActive = false;
    }

    public void Stop()
    {
        IsActive = false;
    }

    public float Process(double deltaTime)
    {
        if (!IsActive) return 0f;

        double output = 0;

        // Generate tonewheel harmonics based on drawbar settings
        for (int i = 0; i < 9; i++)
        {
            int drawbarLevel = _synth.Drawbars[i];
            if (drawbarLevel == 0) continue;

            double ratio = DrawbarRatios[i];
            double freq = BaseFrequency * ratio;

            // Skip if above Nyquist
            if (freq > _sampleRate * 0.45) continue;

            // Phase increment
            double phaseInc = freq / _sampleRate;
            _tonewheelPhases[i] += phaseInc;
            if (_tonewheelPhases[i] >= 1.0) _tonewheelPhases[i] -= 1.0;

            // Generate tonewheel sound (sine with slight harmonic content for warmth)
            double phase = _tonewheelPhases[i] * 2.0 * Math.PI;
            double tone = Math.Sin(phase);

            // Add slight crosstalk/leakage for realism
            if (_synth.TonewheelLeakage > 0)
            {
                tone += Math.Sin(phase * 2.0) * _synth.TonewheelLeakage * 0.1;
                tone += Math.Sin(phase * 3.0) * _synth.TonewheelLeakage * 0.05;
            }

            // Drawbar level (0-8 maps to amplitude)
            double level = drawbarLevel / 8.0;
            output += tone * level;
        }

        // Normalize
        output /= 9.0;

        // Add key click
        if (_clickActive && _synth.KeyClickLevel > 0)
        {
            _clickTime += deltaTime;
            if (_clickTime < 0.003) // 3ms click
            {
                double clickEnv = 1.0 - _clickTime / 0.003;
                double click = (_random.NextDouble() * 2.0 - 1.0) * clickEnv * clickEnv;
                output += click * _synth.KeyClickLevel;
            }
            else
            {
                _clickActive = false;
            }
        }

        // Add percussion
        if (_synth.PercussionEnabled && _percEnvelope > 0.001)
        {
            double percRatio = _synth.PercussionHarmonic == PercussionHarmonic.Second ? 2.0 : 3.0;
            double percFreq = BaseFrequency * percRatio;

            if (percFreq < _sampleRate * 0.45)
            {
                _percPhase += percFreq / _sampleRate;
                if (_percPhase >= 1.0) _percPhase -= 1.0;

                double percTone = Math.Sin(_percPhase * 2.0 * Math.PI);

                // Decay percussion
                double decayRate = _synth.PercussionFast ? 15.0 : 8.0;
                _percEnvelope *= Math.Exp(-deltaTime * decayRate);

                output += percTone * _percEnvelope * _synth.PercussionLevel;
            }
        }

        // Apply velocity
        double velocityGain = Velocity / 127.0;
        output *= velocityGain;

        return (float)output;
    }
}

/// <summary>
/// Tonewheel organ synthesizer with rotary speaker simulation.
/// Features 9 drawbars (Hammond style), percussion, key click, Leslie rotary speaker.
/// </summary>
public class OrganSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<OrganVoice> _voices = new();
    private readonly Dictionary<int, OrganVoice> _noteToVoice = new();
    private readonly object _lock = new();

    // Rotary speaker state
    private double _rotorPhase;
    private double _rotorSpeed;
    private double _targetRotorSpeed;
    private double _hornPhase;
    private double _hornSpeed;
    private double _targetHornSpeed;
    private readonly double[] _delayBuffer;
    private int _delayIndex;

    /// <summary>Gets or sets the synth name.</summary>
    public string Name { get; set; } = "OrganSynth";

    /// <summary>Gets the audio format.</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Gets or sets the master volume (0-1).</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>Gets or sets the maximum number of voices.</summary>
    public int MaxVoices { get; set; } = 61; // Full organ keyboard

    /// <summary>The 9 drawbar levels (0-8 each).</summary>
    public int[] Drawbars { get; } = new int[9];

    // Percussion
    /// <summary>Enable percussion.</summary>
    public bool PercussionEnabled { get; set; } = true;
    /// <summary>Percussion harmonic (2nd or 3rd).</summary>
    public PercussionHarmonic PercussionHarmonic { get; set; } = PercussionHarmonic.Second;
    /// <summary>Percussion level (0-1).</summary>
    public float PercussionLevel { get; set; } = 0.5f;
    /// <summary>Fast percussion decay.</summary>
    public bool PercussionFast { get; set; } = false;

    // Key click
    /// <summary>Key click level (0-1).</summary>
    public float KeyClickLevel { get; set; } = 0.3f;

    // Tonewheel character
    /// <summary>Tonewheel leakage/crosstalk (0-1).</summary>
    public float TonewheelLeakage { get; set; } = 0.2f;

    // Rotary speaker
    /// <summary>Enable rotary speaker effect.</summary>
    public bool RotaryEnabled { get; set; } = true;
    /// <summary>Current rotary speed setting.</summary>
    public RotarySpeed RotarySpeedSetting { get; set; } = RotarySpeed.Slow;
    /// <summary>Rotary effect mix (0-1).</summary>
    public float RotaryMix { get; set; } = 0.8f;
    /// <summary>Rotary horn level (0-1).</summary>
    public float HornLevel { get; set; } = 0.7f;
    /// <summary>Rotary drum level (0-1).</summary>
    public float DrumLevel { get; set; } = 0.5f;

    // Rotary speed constants
    private const double SlowRotorRPM = 40.0;
    private const double FastRotorRPM = 340.0;
    private const double SlowHornRPM = 48.0;
    private const double FastHornRPM = 400.0;
    private const double RotorAccel = 1.5; // Time to accelerate in seconds
    private const double HornAccel = 0.5;

    /// <summary>
    /// Creates a new OrganSynth.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (default: from Settings).</param>
    public OrganSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize drawbars to classic "888000000" setting
        Drawbars[0] = 8; // 16'
        Drawbars[1] = 8; // 5 1/3'
        Drawbars[2] = 8; // 8'
        Drawbars[3] = 0; // 4'
        Drawbars[4] = 0; // 2 2/3'
        Drawbars[5] = 0; // 2'
        Drawbars[6] = 0; // 1 3/5'
        Drawbars[7] = 0; // 1 1/3'
        Drawbars[8] = 0; // 1'

        // Initialize delay buffer for rotary effect
        _delayBuffer = new double[(int)(rate * 0.05)]; // 50ms max delay

        // Initialize rotary speeds
        _rotorSpeed = SlowRotorRPM / 60.0 * 2.0 * Math.PI;
        _targetRotorSpeed = _rotorSpeed;
        _hornSpeed = SlowHornRPM / 60.0 * 2.0 * Math.PI;
        _targetHornSpeed = _hornSpeed;
    }

    /// <summary>
    /// Sets all drawbars from a string (e.g., "888000000").
    /// </summary>
    public void SetDrawbars(string preset)
    {
        for (int i = 0; i < Math.Min(preset.Length, 9); i++)
        {
            if (char.IsDigit(preset[i]))
            {
                Drawbars[i] = Math.Min(8, preset[i] - '0');
            }
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
                voice.Stop(); // Organs have instant release
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
        // Check for drawbar parameters (d0-d8)
        if (name.ToLowerInvariant().StartsWith("d") && name.Length == 2 &&
            int.TryParse(name.Substring(1), out int drawbarIndex))
        {
            if (drawbarIndex >= 0 && drawbarIndex < 9)
            {
                Drawbars[drawbarIndex] = Math.Clamp((int)value, 0, 8);
            }
            return;
        }

        switch (name.ToLowerInvariant())
        {
            case "volume": Volume = Math.Clamp(value, 0f, 1f); break;

            case "percussionenabled": PercussionEnabled = value > 0.5f; break;
            case "percussionharmonic": PercussionHarmonic = (PercussionHarmonic)(int)value; break;
            case "percussionlevel": PercussionLevel = Math.Clamp(value, 0f, 1f); break;
            case "percussionfast": PercussionFast = value > 0.5f; break;

            case "keyclicklevel": KeyClickLevel = Math.Clamp(value, 0f, 1f); break;
            case "tonewheelleakage": TonewheelLeakage = Math.Clamp(value, 0f, 1f); break;

            case "rotaryenabled": RotaryEnabled = value > 0.5f; break;
            case "rotaryspeed": SetRotarySpeed((RotarySpeed)(int)value); break;
            case "rotarymix": RotaryMix = Math.Clamp(value, 0f, 1f); break;
            case "hornlevel": HornLevel = Math.Clamp(value, 0f, 1f); break;
            case "drumlevel": DrumLevel = Math.Clamp(value, 0f, 1f); break;
        }
    }

    /// <summary>
    /// Sets the rotary speaker speed.
    /// </summary>
    public void SetRotarySpeed(RotarySpeed speed)
    {
        RotarySpeedSetting = speed;

        switch (speed)
        {
            case RotarySpeed.Stop:
                _targetRotorSpeed = 0;
                _targetHornSpeed = 0;
                break;
            case RotarySpeed.Slow:
                _targetRotorSpeed = SlowRotorRPM / 60.0 * 2.0 * Math.PI;
                _targetHornSpeed = SlowHornRPM / 60.0 * 2.0 * Math.PI;
                break;
            case RotarySpeed.Fast:
                _targetRotorSpeed = FastRotorRPM / 60.0 * 2.0 * Math.PI;
                _targetHornSpeed = FastHornRPM / 60.0 * 2.0 * Math.PI;
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

                // Apply rotary speaker effect
                float leftOut, rightOut;
                if (RotaryEnabled && RotaryMix > 0)
                {
                    (leftOut, rightOut) = ProcessRotary(sample, deltaTime);
                    float dry = sample * (1f - RotaryMix);
                    leftOut = dry + leftOut * RotaryMix;
                    rightOut = dry + rightOut * RotaryMix;
                }
                else
                {
                    leftOut = rightOut = sample;
                }

                // Apply volume and soft clipping
                leftOut *= Volume;
                rightOut *= Volume;
                leftOut = MathF.Tanh(leftOut);
                rightOut = MathF.Tanh(rightOut);

                // Output stereo
                if (channels >= 2)
                {
                    buffer[offset + n] = leftOut;
                    buffer[offset + n + 1] = rightOut;
                }
                else
                {
                    buffer[offset + n] = (leftOut + rightOut) * 0.5f;
                }
            }
        }

        return count;
    }

    private (float left, float right) ProcessRotary(float input, double deltaTime)
    {
        // Accelerate/decelerate to target speed
        double rotorAccelRate = 1.0 / RotorAccel;
        double hornAccelRate = 1.0 / HornAccel;

        if (_rotorSpeed < _targetRotorSpeed)
            _rotorSpeed = Math.Min(_rotorSpeed + rotorAccelRate * deltaTime * FastRotorRPM / 60.0 * 2.0 * Math.PI, _targetRotorSpeed);
        else if (_rotorSpeed > _targetRotorSpeed)
            _rotorSpeed = Math.Max(_rotorSpeed - rotorAccelRate * deltaTime * FastRotorRPM / 60.0 * 2.0 * Math.PI * 0.5, _targetRotorSpeed);

        if (_hornSpeed < _targetHornSpeed)
            _hornSpeed = Math.Min(_hornSpeed + hornAccelRate * deltaTime * FastHornRPM / 60.0 * 2.0 * Math.PI, _targetHornSpeed);
        else if (_hornSpeed > _targetHornSpeed)
            _hornSpeed = Math.Max(_hornSpeed - hornAccelRate * deltaTime * FastHornRPM / 60.0 * 2.0 * Math.PI * 0.3, _targetHornSpeed);

        // Update phases
        _rotorPhase += _rotorSpeed * deltaTime;
        if (_rotorPhase >= 2.0 * Math.PI) _rotorPhase -= 2.0 * Math.PI;

        _hornPhase += _hornSpeed * deltaTime;
        if (_hornPhase >= 2.0 * Math.PI) _hornPhase -= 2.0 * Math.PI;

        // Horn (high frequencies) - amplitude and frequency modulation
        double hornAmp = 0.5 + 0.5 * Math.Sin(_hornPhase);
        double hornFreqMod = 1.0 + 0.002 * Math.Sin(_hornPhase); // Doppler
        double hornPan = Math.Sin(_hornPhase);

        // Drum (low frequencies) - amplitude modulation
        double drumAmp = 0.5 + 0.5 * Math.Sin(_rotorPhase);
        double drumPan = Math.Sin(_rotorPhase);

        // Split signal (simplified crossover)
        // In reality, we'd use proper filters
        float highFreq = input * HornLevel;
        float lowFreq = input * DrumLevel;

        // Apply modulation
        float hornOut = (float)(highFreq * hornAmp);
        float drumOut = (float)(lowFreq * drumAmp);

        // Combine with panning
        float left = (float)(hornOut * (0.5 - hornPan * 0.5) + drumOut * (0.5 - drumPan * 0.5));
        float right = (float)(hornOut * (0.5 + hornPan * 0.5) + drumOut * (0.5 + drumPan * 0.5));

        return (left, right);
    }

    private OrganVoice? GetFreeVoice()
    {
        foreach (var voice in _voices)
        {
            if (!voice.IsActive) return voice;
        }

        if (_voices.Count < MaxVoices)
        {
            var voice = new OrganVoice(_waveFormat.SampleRate, this);
            _voices.Add(voice);
            return voice;
        }

        // Voice stealing (oldest)
        OrganVoice? oldest = null;
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

    /// <summary>Creates a classic Hammond B3 preset.</summary>
    public static OrganSynth CreateHammondB3()
    {
        var synth = new OrganSynth { Name = "Hammond B3" };
        synth.SetDrawbars("888000000");
        synth.PercussionEnabled = true;
        synth.PercussionHarmonic = PercussionHarmonic.Second;
        synth.PercussionFast = false;
        synth.PercussionLevel = 0.5f;
        synth.KeyClickLevel = 0.3f;
        synth.RotaryEnabled = true;
        synth.SetRotarySpeed(RotarySpeed.Slow);
        return synth;
    }

    /// <summary>Creates a full organ preset.</summary>
    public static OrganSynth CreateFullOrgan()
    {
        var synth = new OrganSynth { Name = "Full Organ" };
        synth.SetDrawbars("888888888");
        synth.PercussionEnabled = false;
        synth.KeyClickLevel = 0.2f;
        synth.RotaryEnabled = true;
        synth.SetRotarySpeed(RotarySpeed.Slow);
        return synth;
    }

    /// <summary>Creates a gospel/jazz organ preset.</summary>
    public static OrganSynth CreateGospelOrgan()
    {
        var synth = new OrganSynth { Name = "Gospel Organ" };
        synth.SetDrawbars("888800000");
        synth.PercussionEnabled = true;
        synth.PercussionHarmonic = PercussionHarmonic.Third;
        synth.PercussionFast = true;
        synth.PercussionLevel = 0.6f;
        synth.KeyClickLevel = 0.4f;
        synth.RotaryEnabled = true;
        synth.SetRotarySpeed(RotarySpeed.Fast);
        return synth;
    }

    /// <summary>Creates a rock organ preset.</summary>
    public static OrganSynth CreateRockOrgan()
    {
        var synth = new OrganSynth { Name = "Rock Organ" };
        synth.SetDrawbars("886400000");
        synth.PercussionEnabled = true;
        synth.PercussionHarmonic = PercussionHarmonic.Second;
        synth.PercussionFast = true;
        synth.PercussionLevel = 0.7f;
        synth.KeyClickLevel = 0.5f;
        synth.RotaryEnabled = true;
        synth.SetRotarySpeed(RotarySpeed.Fast);
        return synth;
    }

    /// <summary>Creates a ballad organ preset.</summary>
    public static OrganSynth CreateBalladOrgan()
    {
        var synth = new OrganSynth { Name = "Ballad Organ" };
        synth.SetDrawbars("808808000");
        synth.PercussionEnabled = false;
        synth.KeyClickLevel = 0.1f;
        synth.RotaryEnabled = true;
        synth.SetRotarySpeed(RotarySpeed.Slow);
        return synth;
    }

    /// <summary>Creates a theatre organ preset.</summary>
    public static OrganSynth CreateTheatreOrgan()
    {
        var synth = new OrganSynth { Name = "Theatre Organ" };
        synth.SetDrawbars("848484848");
        synth.PercussionEnabled = false;
        synth.KeyClickLevel = 0.0f;
        synth.TonewheelLeakage = 0.0f;
        synth.RotaryEnabled = false;
        return synth;
    }

    #endregion
}
