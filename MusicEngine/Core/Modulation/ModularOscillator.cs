// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VCV Rack-style modular oscillator, filter, envelope, and LFO components.

using System;

namespace MusicEngine.Core.Modulation;

/// <summary>
/// A fully modular oscillator inspired by VCV Rack.
/// All parameters (frequency, PWM, level, etc.) can be modulated.
/// </summary>
public class ModularOscillator
{
    private double _phase;
    private double _syncPhase;
    private readonly Random _random = new();
    private readonly int _sampleRate;

    // Parameters
    public ModularParameter Frequency { get; }
    public ModularParameter FineTune { get; }
    public ModularParameter PulseWidth { get; }
    public ModularParameter Level { get; }
    public ModularParameter FM { get; }
    public ModularParameter PM { get; }
    public ModularParameter Waveform { get; }
    public ModularParameter SubOscLevel { get; }
    public ModularParameter SubOscOctave { get; }

    /// <summary>
    /// Whether hard sync is enabled.
    /// </summary>
    public bool HardSync { get; set; }

    /// <summary>
    /// The oscillator to sync to (if hard sync is enabled).
    /// </summary>
    public ModularOscillator? SyncSource { get; set; }

    /// <summary>
    /// Whether this oscillator is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Current phase (0-1).
    /// </summary>
    public double Phase => _phase / (2 * Math.PI);

    public ModularOscillator(int sampleRate)
    {
        _sampleRate = sampleRate;

        // Register all parameters
        Frequency = new ModularParameter("freq", "Frequency", 20, 20000, 440)
        {
            Type = ParameterType.Pitch,
            Unit = "Hz",
            AudioRate = true,
            Description = "Oscillator frequency"
        };

        FineTune = new ModularParameter("fine", "Fine Tune", -100, 100, 0)
        {
            Type = ParameterType.Pitch,
            Unit = "cents",
            Description = "Fine tuning in cents"
        };

        PulseWidth = new ModularParameter("pw", "Pulse Width", 0.01, 0.99, 0.5)
        {
            Type = ParameterType.Oscillator,
            Unit = "%",
            Description = "Pulse width for square wave"
        };

        Level = new ModularParameter("level", "Level", 0, 1, 1)
        {
            Type = ParameterType.Amplitude,
            Description = "Oscillator output level"
        };

        FM = new ModularParameter("fm", "FM Amount", -1, 1, 0)
        {
            Type = ParameterType.Modulation,
            AudioRate = true,
            Description = "Frequency modulation amount"
        };

        PM = new ModularParameter("pm", "PM Amount", -1, 1, 0)
        {
            Type = ParameterType.Modulation,
            AudioRate = true,
            Description = "Phase modulation amount"
        };

        Waveform = new ModularParameter("wave", "Waveform", 0, 4, 0)
        {
            Type = ParameterType.Oscillator,
            Step = 1,
            Description = "0=Sine, 1=Saw, 2=Square, 3=Triangle, 4=Noise"
        };

        SubOscLevel = new ModularParameter("sublevel", "Sub Osc Level", 0, 1, 0)
        {
            Type = ParameterType.Amplitude,
            Description = "Sub-oscillator level"
        };

        SubOscOctave = new ModularParameter("suboct", "Sub Osc Octave", -2, -1, -1)
        {
            Type = ParameterType.Pitch,
            Step = 1,
            Description = "Sub-oscillator octave offset"
        };
    }

    /// <summary>
    /// Gets all parameters for registration.
    /// </summary>
    public ModularParameter[] GetAllParameters()
    {
        return new[]
        {
            Frequency, FineTune, PulseWidth, Level, FM, PM,
            Waveform, SubOscLevel, SubOscOctave
        };
    }

    /// <summary>
    /// Sets the frequency from a MIDI note.
    /// </summary>
    public void SetNote(int note)
    {
        var freq = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);
        Frequency.BaseValue = freq;
    }

    /// <summary>
    /// Resets the oscillator phase.
    /// </summary>
    public void Reset()
    {
        _phase = 0;
        _syncPhase = 0;
    }

    /// <summary>
    /// Processes one sample.
    /// </summary>
    public double Process(double fmInput = 0, double pmInput = 0)
    {
        if (!Enabled) return 0;

        // Get modulated parameter values
        var baseFreq = Frequency.Process();
        var fineTune = FineTune.Process();
        var pw = PulseWidth.Process();
        var level = Level.Process();
        var fmAmount = FM.Process();
        var pmAmount = PM.Process();
        var wave = (int)Math.Round(Waveform.Process());
        var subLevel = SubOscLevel.Process();
        var subOct = (int)SubOscOctave.Process();

        // Apply fine tuning
        var freq = baseFreq * Math.Pow(2.0, fineTune / 1200.0);

        // Apply FM
        if (Math.Abs(fmAmount) > 0.001 && Math.Abs(fmInput) > 0.001)
        {
            freq *= 1.0 + (fmInput * fmAmount);
        }

        freq = Math.Clamp(freq, 0.1, _sampleRate / 2.0);

        // Phase increment
        var phaseInc = 2.0 * Math.PI * freq / _sampleRate;

        // Apply PM
        var phase = _phase;
        if (Math.Abs(pmAmount) > 0.001 && Math.Abs(pmInput) > 0.001)
        {
            phase += pmInput * pmAmount * Math.PI;
        }

        // Hard sync check
        if (HardSync && SyncSource != null && SyncSource.Phase < 0.01)
        {
            _phase = 0;
            phase = 0;
        }

        // Normalize phase
        while (phase >= 2 * Math.PI) phase -= 2 * Math.PI;
        while (phase < 0) phase += 2 * Math.PI;

        // Generate main waveform
        var sample = GenerateWaveform(phase, (WaveType)wave, pw);

        // Generate sub oscillator
        if (subLevel > 0.001)
        {
            var subFreq = freq * Math.Pow(2.0, subOct);
            _syncPhase += 2.0 * Math.PI * subFreq / _sampleRate;
            if (_syncPhase >= 2 * Math.PI) _syncPhase -= 2 * Math.PI;

            var subSample = Math.Sin(_syncPhase); // Sub is always sine
            sample = sample * (1 - subLevel) + subSample * subLevel;
        }

        // Advance phase
        _phase += phaseInc;
        if (_phase >= 2 * Math.PI) _phase -= 2 * Math.PI;

        return sample * level;
    }

    private double GenerateWaveform(double phase, WaveType type, double pw)
    {
        return type switch
        {
            WaveType.Sine => Math.Sin(phase),
            WaveType.Sawtooth => (phase / Math.PI) - 1.0,
            WaveType.Square => phase < (2 * Math.PI * pw) ? 1.0 : -1.0,
            WaveType.Triangle => phase < Math.PI
                ? (2.0 * phase / Math.PI) - 1.0
                : 3.0 - (2.0 * phase / Math.PI),
            WaveType.Noise => _random.NextDouble() * 2.0 - 1.0,
            _ => Math.Sin(phase)
        };
    }
}

/// <summary>
/// A fully modular filter inspired by VCV Rack.
/// </summary>
public class ModularFilter
{
    private double _state1, _state2, _state3, _state4;
    private double _lastInput;
    private readonly int _sampleRate;

    // Parameters
    public ModularParameter Cutoff { get; }
    public ModularParameter Resonance { get; }
    public ModularParameter Drive { get; }
    public ModularParameter EnvAmount { get; }
    public ModularParameter KeyTrack { get; }
    public ModularParameter FilterType { get; }

    /// <summary>
    /// Whether the filter is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public ModularFilter(int sampleRate)
    {
        _sampleRate = sampleRate;

        Cutoff = new ModularParameter("cutoff", "Cutoff", 20, 20000, 1000)
        {
            Type = ParameterType.Filter,
            Unit = "Hz",
            AudioRate = true,
            Description = "Filter cutoff frequency"
        };

        Resonance = new ModularParameter("reso", "Resonance", 0, 1, 0)
        {
            Type = ParameterType.Filter,
            Description = "Filter resonance"
        };

        Drive = new ModularParameter("drive", "Drive", 1, 10, 1)
        {
            Type = ParameterType.Filter,
            Description = "Input drive/saturation"
        };

        EnvAmount = new ModularParameter("envamt", "Env Amount", -1, 1, 0)
        {
            Type = ParameterType.Filter,
            Description = "Envelope modulation amount"
        };

        KeyTrack = new ModularParameter("keytrack", "Key Track", 0, 1, 0)
        {
            Type = ParameterType.Filter,
            Description = "Keyboard tracking amount"
        };

        FilterType = new ModularParameter("type", "Filter Type", 0, 4, 0)
        {
            Type = ParameterType.Filter,
            Step = 1,
            Description = "0=LP, 1=HP, 2=BP, 3=Notch, 4=Moog"
        };
    }

    /// <summary>
    /// Gets all parameters for registration.
    /// </summary>
    public ModularParameter[] GetAllParameters()
    {
        return new[] { Cutoff, Resonance, Drive, EnvAmount, KeyTrack, FilterType };
    }

    /// <summary>
    /// Resets the filter state.
    /// </summary>
    public void Reset()
    {
        _state1 = _state2 = _state3 = _state4 = 0;
        _lastInput = 0;
    }

    /// <summary>
    /// Processes one sample through the filter.
    /// </summary>
    public double Process(double input, double envValue = 0, int noteNumber = 60)
    {
        if (!Enabled) return input;

        // Get modulated parameters
        var baseCutoff = Cutoff.Process();
        var reso = Resonance.Process();
        var drive = Drive.Process();
        var envAmt = EnvAmount.Process();
        var keyTrack = KeyTrack.Process();
        var type = (int)Math.Round(FilterType.Process());

        // Apply envelope modulation
        var cutoff = baseCutoff;
        if (Math.Abs(envAmt) > 0.001)
        {
            var envMod = envValue * envAmt * 10000; // Scale to frequency range
            cutoff += envMod;
        }

        // Apply keyboard tracking
        if (keyTrack > 0.001)
        {
            var noteFreq = 440.0 * Math.Pow(2.0, (noteNumber - 69.0) / 12.0);
            cutoff += (noteFreq - 440.0) * keyTrack;
        }

        cutoff = Math.Clamp(cutoff, 20, _sampleRate * 0.45);

        // Apply drive
        input = Math.Tanh(input * drive);

        // Process based on filter type
        return type switch
        {
            0 => ProcessLowPass(input, cutoff, reso),
            1 => ProcessHighPass(input, cutoff, reso),
            2 => ProcessBandPass(input, cutoff, reso),
            3 => ProcessNotch(input, cutoff, reso),
            4 => ProcessMoog(input, cutoff, reso),
            _ => ProcessLowPass(input, cutoff, reso)
        };
    }

    private double ProcessLowPass(double input, double cutoff, double reso)
    {
        var rc = 1.0 / (2.0 * Math.PI * cutoff);
        var dt = 1.0 / _sampleRate;
        var a = dt / (rc + dt);

        // Two-pole with resonance
        _state1 += a * (input - _state1 + reso * (_state1 - _state2) * 4);
        _state2 += a * (_state1 - _state2);

        return _state2;
    }

    private double ProcessHighPass(double input, double cutoff, double reso)
    {
        var rc = 1.0 / (2.0 * Math.PI * cutoff);
        var dt = 1.0 / _sampleRate;
        var a = rc / (rc + dt);

        var output = a * (_state1 + input - _lastInput);
        _state1 = output + reso * (output - _state2) * 2;
        _state2 = _state1;
        _lastInput = input;

        return output;
    }

    private double ProcessBandPass(double input, double cutoff, double reso)
    {
        var lp = ProcessLowPass(input, cutoff * 1.5, reso * 0.5);
        var hp = ProcessHighPass(lp, cutoff * 0.7, reso * 0.5);
        return hp * 2; // Boost to compensate for narrower band
    }

    private double ProcessNotch(double input, double cutoff, double reso)
    {
        var lp = ProcessLowPass(input, cutoff * 0.7, reso);
        var hp = ProcessHighPass(input, cutoff * 1.3, reso);
        return (lp + hp) * 0.5;
    }

    private double ProcessMoog(double input, double cutoff, double reso)
    {
        var fc = cutoff / _sampleRate;
        var f = fc * 1.16;
        var fb = reso * 4.0 * (1.0 - 0.15 * f * f);

        input -= _state4 * fb;
        input *= 0.35013 * (f * f) * (f * f);

        _state1 = input + 0.3 * _state1 + (1 - f) * _state1;
        _state2 = _state1 + 0.3 * _state2 + (1 - f) * _state2;
        _state3 = _state2 + 0.3 * _state3 + (1 - f) * _state3;
        _state4 = _state3 + 0.3 * _state4 + (1 - f) * _state4;

        return _state4;
    }
}

/// <summary>
/// A fully modular envelope generator.
/// </summary>
public class ModularEnvelope : IModulationSource
{
    private double _value;
    private EnvelopeStage _stage = EnvelopeStage.Idle;
    private double _timeInStage;
    private double _releaseStartValue;
    private readonly int _sampleRate;

    public string Id { get; }
    public string Name { get; set; }
    public bool IsBipolar => false;

    // Parameters
    public ModularParameter Attack { get; }
    public ModularParameter Decay { get; }
    public ModularParameter Sustain { get; }
    public ModularParameter Release { get; }
    public ModularParameter AttackCurve { get; }
    public ModularParameter DecayCurve { get; }
    public ModularParameter ReleaseCurve { get; }

    /// <summary>
    /// Current envelope value (0-1).
    /// </summary>
    public double Value => _value;

    /// <summary>
    /// Current envelope stage.
    /// </summary>
    public EnvelopeStage Stage => _stage;

    /// <summary>
    /// Whether the envelope is active.
    /// </summary>
    public bool IsActive => _stage != EnvelopeStage.Idle;

    public ModularEnvelope(string id, string name, int sampleRate)
    {
        Id = id;
        Name = name;
        _sampleRate = sampleRate;

        Attack = new ModularParameter($"{id}.attack", "Attack", 0.001, 10, 0.01)
        {
            Type = ParameterType.Envelope,
            Unit = "s",
            Description = "Attack time"
        };

        Decay = new ModularParameter($"{id}.decay", "Decay", 0.001, 10, 0.1)
        {
            Type = ParameterType.Envelope,
            Unit = "s",
            Description = "Decay time"
        };

        Sustain = new ModularParameter($"{id}.sustain", "Sustain", 0, 1, 0.7)
        {
            Type = ParameterType.Envelope,
            Description = "Sustain level"
        };

        Release = new ModularParameter($"{id}.release", "Release", 0.001, 10, 0.3)
        {
            Type = ParameterType.Envelope,
            Unit = "s",
            Description = "Release time"
        };

        AttackCurve = new ModularParameter($"{id}.acurve", "Attack Curve", -1, 1, 0)
        {
            Type = ParameterType.Envelope,
            Description = "Attack curve shape"
        };

        DecayCurve = new ModularParameter($"{id}.dcurve", "Decay Curve", -1, 1, 0)
        {
            Type = ParameterType.Envelope,
            Description = "Decay curve shape"
        };

        ReleaseCurve = new ModularParameter($"{id}.rcurve", "Release Curve", -1, 1, 0)
        {
            Type = ParameterType.Envelope,
            Description = "Release curve shape"
        };
    }

    /// <summary>
    /// Gets all parameters.
    /// </summary>
    public ModularParameter[] GetAllParameters()
    {
        return new[] { Attack, Decay, Sustain, Release, AttackCurve, DecayCurve, ReleaseCurve };
    }

    /// <summary>
    /// Triggers the envelope.
    /// </summary>
    public void Trigger()
    {
        _stage = EnvelopeStage.Attack;
        _timeInStage = 0;
    }

    /// <summary>
    /// Releases the envelope.
    /// </summary>
    public void ReleaseGate()
    {
        if (_stage != EnvelopeStage.Idle)
        {
            _stage = EnvelopeStage.Release;
            _releaseStartValue = _value;
            _timeInStage = 0;
        }
    }

    /// <summary>
    /// Resets the envelope.
    /// </summary>
    public void Reset()
    {
        _stage = EnvelopeStage.Idle;
        _value = 0;
        _timeInStage = 0;
    }

    /// <summary>
    /// Processes one sample.
    /// </summary>
    public double Process()
    {
        var dt = 1.0 / _sampleRate;
        _timeInStage += dt;

        var attack = Attack.Process();
        var decay = Decay.Process();
        var sustain = Sustain.Process();
        var release = Release.Process();
        var aCurve = AttackCurve.Process();
        var dCurve = DecayCurve.Process();
        var rCurve = ReleaseCurve.Process();

        switch (_stage)
        {
            case EnvelopeStage.Attack:
                var attackProgress = Math.Clamp(_timeInStage / attack, 0, 1);
                _value = ApplyCurve(attackProgress, aCurve);
                if (_timeInStage >= attack)
                {
                    _stage = EnvelopeStage.Decay;
                    _timeInStage = 0;
                }
                break;

            case EnvelopeStage.Decay:
                var decayProgress = Math.Clamp(_timeInStage / decay, 0, 1);
                _value = 1.0 - (1.0 - sustain) * ApplyCurve(decayProgress, dCurve);
                if (_timeInStage >= decay)
                {
                    _stage = EnvelopeStage.Sustain;
                    _value = sustain;
                }
                break;

            case EnvelopeStage.Sustain:
                _value = sustain;
                break;

            case EnvelopeStage.Release:
                var releaseProgress = Math.Clamp(_timeInStage / release, 0, 1);
                _value = _releaseStartValue * (1.0 - ApplyCurve(releaseProgress, rCurve));
                if (_timeInStage >= release)
                {
                    _stage = EnvelopeStage.Idle;
                    _value = 0;
                }
                break;

            case EnvelopeStage.Idle:
            default:
                _value = 0;
                break;
        }

        return _value;
    }

    private double ApplyCurve(double value, double curve)
    {
        if (Math.Abs(curve) < 0.01) return value;

        if (curve > 0)
        {
            // Exponential
            return Math.Pow(value, 1.0 + curve * 3);
        }
        else
        {
            // Logarithmic
            return 1.0 - Math.Pow(1.0 - value, 1.0 - curve * 3);
        }
    }

    public double GetValue() => _value;
    public double GetValueAtSample(int sampleOffset) => _value;
}

/// <summary>
/// A fully modular LFO.
/// </summary>
public class ModularLFO : IModulationSource
{
    private double _phase;
    private readonly Random _random = new();
    private double _randomValue;
    private double _smoothedRandomValue;
    private readonly int _sampleRate;

    public string Id { get; }
    public string Name { get; set; }
    public bool IsBipolar => true;

    // Parameters
    public ModularParameter Rate { get; }
    public ModularParameter Depth { get; }
    public ModularParameter Waveform { get; }
    public ModularParameter Phase { get; }
    public ModularParameter Offset { get; }

    /// <summary>
    /// Whether the LFO is tempo-synced.
    /// </summary>
    public bool TempoSync { get; set; }

    /// <summary>
    /// External tempo in BPM (for sync).
    /// </summary>
    public double Tempo { get; set; } = 120;

    /// <summary>
    /// Whether the LFO is running.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Current output value (-1 to 1).
    /// </summary>
    public double Value { get; private set; }

    public ModularLFO(string id, string name, int sampleRate)
    {
        Id = id;
        Name = name;
        _sampleRate = sampleRate;

        Rate = new ModularParameter($"{id}.rate", "Rate", 0.01, 100, 1)
        {
            Type = ParameterType.LFO,
            Unit = "Hz",
            Description = "LFO frequency"
        };

        Depth = new ModularParameter($"{id}.depth", "Depth", 0, 1, 1)
        {
            Type = ParameterType.LFO,
            Description = "Modulation depth"
        };

        Waveform = new ModularParameter($"{id}.wave", "Waveform", 0, 5, 0)
        {
            Type = ParameterType.LFO,
            Step = 1,
            Description = "0=Sine, 1=Tri, 2=Saw, 3=Square, 4=S&H, 5=Smooth"
        };

        Phase = new ModularParameter($"{id}.phase", "Phase", 0, 1, 0)
        {
            Type = ParameterType.LFO,
            Description = "Phase offset"
        };

        Offset = new ModularParameter($"{id}.offset", "Offset", -1, 1, 0)
        {
            Type = ParameterType.LFO,
            Description = "DC offset"
        };
    }

    /// <summary>
    /// Gets all parameters.
    /// </summary>
    public ModularParameter[] GetAllParameters()
    {
        return new[] { Rate, Depth, Waveform, Phase, Offset };
    }

    /// <summary>
    /// Resets the LFO.
    /// </summary>
    public void Reset()
    {
        _phase = 0;
    }

    /// <summary>
    /// Processes one sample.
    /// </summary>
    public double Process()
    {
        if (!Enabled)
        {
            Value = 0;
            return 0;
        }

        var rate = Rate.Process();
        var depth = Depth.Process();
        var wave = (int)Math.Round(Waveform.Process());
        var phaseOffset = Phase.Process();
        var offset = Offset.Process();

        // Calculate frequency (with tempo sync option)
        var freq = TempoSync ? rate * Tempo / 60.0 : rate;

        // Advance phase
        _phase += freq / _sampleRate;
        if (_phase >= 1.0)
        {
            _phase -= 1.0;
            _randomValue = _random.NextDouble() * 2 - 1;
        }

        var phase = (_phase + phaseOffset) % 1.0;

        // Generate waveform
        double sample = wave switch
        {
            0 => Math.Sin(phase * 2 * Math.PI), // Sine
            1 => phase < 0.5 ? phase * 4 - 1 : 3 - phase * 4, // Triangle
            2 => phase * 2 - 1, // Saw
            3 => phase < 0.5 ? 1 : -1, // Square
            4 => _randomValue, // Sample & Hold
            5 => ProcessSmooth(), // Smooth random
            _ => Math.Sin(phase * 2 * Math.PI)
        };

        Value = sample * depth + offset;
        return Value;
    }

    private double ProcessSmooth()
    {
        _smoothedRandomValue += (_randomValue - _smoothedRandomValue) * 0.01;
        return _smoothedRandomValue;
    }

    public double GetValue() => Value;
    public double GetValueAtSample(int sampleOffset) => Value;
}
