// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Oscillator sync mode
/// </summary>
public enum OscSyncMode
{
    Off,
    Hard,   // Slave resets on master zero-crossing
    Soft    // Slave frequency modulated by master
}

/// <summary>
/// Configuration for a single oscillator in the bank
/// </summary>
public class BankOscillatorConfig
{
    public WaveType Waveform { get; set; } = WaveType.Sawtooth;
    public float Level { get; set; } = 1f;
    public float Detune { get; set; } = 0f;       // Cents
    public int Octave { get; set; } = 0;          // -2 to +2
    public int Semitone { get; set; } = 0;        // -12 to +12
    public float PulseWidth { get; set; } = 0.5f; // For PWM
    public float Phase { get; set; } = 0f;        // Initial phase offset (0-1)
    public bool Enabled { get; set; } = true;

    public BankOscillatorConfig Clone()
    {
        return new BankOscillatorConfig
        {
            Waveform = Waveform,
            Level = Level,
            Detune = Detune,
            Octave = Octave,
            Semitone = Semitone,
            PulseWidth = PulseWidth,
            Phase = Phase,
            Enabled = Enabled
        };
    }
}

/// <summary>
/// Multi-oscillator bank with sync, unison, and advanced features
/// </summary>
public class OscillatorBank : ISynth, ISampleProvider
{
    private readonly WaveFormat _waveFormat;
    private readonly object _lock = new();

    // ISynth Name property
    public string Name { get; set; } = "OscillatorBank";

    // Oscillators (up to 8)
    private readonly BankOscillatorConfig[] _oscillators = new BankOscillatorConfig[8];
    private int _activeOscillatorCount = 2;

    // Unison
    public int UnisonVoices { get; set; } = 1;        // 1-8
    public float UnisonDetune { get; set; } = 20f;    // Cents
    public float UnisonSpread { get; set; } = 0.5f;   // Stereo spread

    // Sync
    public OscSyncMode SyncMode { get; set; } = OscSyncMode.Off;
    public int SyncMasterOsc { get; set; } = 0;       // Which oscillator is master
    public int SyncSlaveOsc { get; set; } = 1;        // Which oscillator is slave

    // PWM LFO
    public LFO? PulseWidthLFO { get; set; }
    public float PulseWidthLFODepth { get; set; } = 0f;

    // Global parameters
    public float Volume { get; set; } = 0.7f;
    public float Attack { get; set; } = 0.01f;
    public float Decay { get; set; } = 0.2f;
    public float Sustain { get; set; } = 0.7f;
    public float Release { get; set; } = 0.3f;

    // Voices
    private readonly List<BankVoice> _voices = new();
    private readonly int _maxVoices;

    public WaveFormat WaveFormat => _waveFormat;

    public OscillatorBank(int sampleRate = 44100, int maxVoices = 16)
    {
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
        _maxVoices = maxVoices;

        // Initialize oscillators with defaults
        for (int i = 0; i < 8; i++)
        {
            _oscillators[i] = new BankOscillatorConfig
            {
                Waveform = i == 0 ? WaveType.Sawtooth : WaveType.Square,
                Level = i < 2 ? 1f : 0f,
                Detune = i * 5f,
                Enabled = i < 2
            };
        }
    }

    /// <summary>
    /// Configure an oscillator
    /// </summary>
    public void SetOscillator(int index, BankOscillatorConfig config)
    {
        if (index >= 0 && index < 8)
        {
            _oscillators[index] = config.Clone();
        }
    }

    /// <summary>
    /// Get oscillator configuration
    /// </summary>
    public BankOscillatorConfig GetOscillator(int index)
    {
        if (index >= 0 && index < 8)
        {
            return _oscillators[index].Clone();
        }
        return new BankOscillatorConfig();
    }

    /// <summary>
    /// Set number of active oscillators (1-8)
    /// </summary>
    public void SetOscillatorCount(int count)
    {
        _activeOscillatorCount = Math.Clamp(count, 1, 8);
        for (int i = 0; i < 8; i++)
        {
            _oscillators[i].Enabled = i < _activeOscillatorCount;
        }
    }

    /// <summary>
    /// Configure supersaw preset
    /// </summary>
    public void SetupSupersaw(int voices = 7, float detune = 30f)
    {
        UnisonVoices = voices;
        UnisonDetune = detune;
        UnisonSpread = 0.8f;

        _activeOscillatorCount = 1;
        _oscillators[0] = new BankOscillatorConfig
        {
            Waveform = WaveType.Sawtooth,
            Level = 1f,
            Enabled = true
        };

        for (int i = 1; i < 8; i++)
        {
            _oscillators[i].Enabled = false;
        }
    }

    public void NoteOn(int note, int velocity)
    {
        lock (_lock)
        {
            var existing = _voices.FirstOrDefault(v => v.Note == note && v.IsActive);
            if (existing != null)
            {
                existing.Velocity = velocity;
                existing.Envelope.Trigger(velocity);
                return;
            }

            if (_voices.Count(v => v.IsActive) >= _maxVoices)
            {
                var oldest = _voices.Where(v => v.IsActive).OrderBy(v => v.StartTime).FirstOrDefault();
                if (oldest != null) oldest.IsActive = false;
            }

            double baseFreq = 440.0 * Math.Pow(2.0, (note - 69) / 12.0);

            var voice = new BankVoice
            {
                Note = note,
                Velocity = velocity,
                BaseFrequency = baseFreq,
                IsActive = true,
                StartTime = DateTime.Now.Ticks,
                OscPhases = new double[8],
                UnisonPhases = new double[8, 8], // [oscillator, unison voice]
                Envelope = new Envelope
                {
                    Attack = Attack,
                    Decay = Decay,
                    Sustain = Sustain,
                    Release = Release
                }
            };

            // Initialize phases with random offset for natural sound
            var random = new Random();
            for (int o = 0; o < 8; o++)
            {
                voice.OscPhases[o] = _oscillators[o].Phase * 2 * Math.PI;
                for (int u = 0; u < 8; u++)
                {
                    voice.UnisonPhases[o, u] = random.NextDouble() * 2 * Math.PI;
                }
            }

            voice.Envelope.Trigger(velocity);
            _voices.Add(voice);
        }
    }

    public void NoteOff(int note)
    {
        lock (_lock)
        {
            foreach (var voice in _voices.Where(v => v.Note == note && v.IsActive))
            {
                voice.Envelope.Release_Gate();
            }
        }
    }

    public void AllNotesOff()
    {
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                voice.Envelope.Release_Gate();
                voice.IsActive = false;
            }
            _voices.Clear();
        }
    }

    public void SetParameter(string name, float value)
    {
        var parts = name.ToLowerInvariant().Split('_');

        if (parts.Length == 2 && parts[0].StartsWith("osc") && int.TryParse(parts[0].Substring(3), out int oscIndex))
        {
            oscIndex--; // Convert to 0-based
            if (oscIndex >= 0 && oscIndex < 8)
            {
                switch (parts[1])
                {
                    case "level": _oscillators[oscIndex].Level = Math.Clamp(value, 0f, 1f); break;
                    case "detune": _oscillators[oscIndex].Detune = value; break;
                    case "octave": _oscillators[oscIndex].Octave = (int)Math.Clamp(value, -2, 2); break;
                    case "semitone": _oscillators[oscIndex].Semitone = (int)Math.Clamp(value, -12, 12); break;
                    case "pulsewidth": _oscillators[oscIndex].PulseWidth = Math.Clamp(value, 0.01f, 0.99f); break;
                    case "waveform": _oscillators[oscIndex].Waveform = (WaveType)(int)value; break;
                    case "enabled": _oscillators[oscIndex].Enabled = value > 0.5f; break;
                }
            }
        }
        else
        {
            switch (name.ToLowerInvariant())
            {
                case "volume": Volume = Math.Clamp(value, 0f, 1f); break;
                case "attack": Attack = value; break;
                case "decay": Decay = value; break;
                case "sustain": Sustain = Math.Clamp(value, 0f, 1f); break;
                case "release": Release = value; break;
                case "unisonvoices": UnisonVoices = Math.Clamp((int)value, 1, 8); break;
                case "unisondetune": UnisonDetune = value; break;
                case "unisonspread": UnisonSpread = Math.Clamp(value, 0f, 1f); break;
                case "oscillatorcount": SetOscillatorCount((int)value); break;
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++) buffer[offset + i] = 0;

        int channels = _waveFormat.Channels;
        double sampleRate = _waveFormat.SampleRate;
        double deltaTime = 1.0 / sampleRate;

        // Get PWM LFO
        float pwmMod = 0;
        if (PulseWidthLFO != null && PulseWidthLFO.Enabled)
        {
            pwmMod = (float)PulseWidthLFO.GetValue(_waveFormat.SampleRate) * PulseWidthLFODepth;
        }

        lock (_lock)
        {
            _voices.RemoveAll(v => !v.IsActive && v.Envelope.Stage == EnvelopeStage.Idle);

            foreach (var voice in _voices.ToList())
            {
                for (int n = 0; n < count; n += channels)
                {
                    double envValue = voice.Envelope.Process(deltaTime);

                    if (voice.Envelope.Stage == EnvelopeStage.Idle)
                    {
                        voice.IsActive = false;
                        break;
                    }

                    float sampleL = 0;
                    float sampleR = 0;

                    // Store master phase for sync
                    double masterPhase = 0;
                    bool masterZeroCross = false;

                    // Process each oscillator
                    for (int osc = 0; osc < _activeOscillatorCount; osc++)
                    {
                        var config = _oscillators[osc];
                        if (!config.Enabled || config.Level <= 0) continue;

                        // Calculate oscillator frequency
                        double oscFreq = voice.BaseFrequency;
                        oscFreq *= Math.Pow(2.0, config.Octave);
                        oscFreq *= Math.Pow(2.0, config.Semitone / 12.0);
                        oscFreq *= Math.Pow(2.0, config.Detune / 1200.0);

                        float pulseWidth = Math.Clamp(config.PulseWidth + pwmMod, 0.01f, 0.99f);

                        // Process unison voices
                        for (int u = 0; u < UnisonVoices; u++)
                        {
                            double unisonFreq = oscFreq;

                            // Apply unison detune
                            if (UnisonVoices > 1)
                            {
                                double detuneOffset = (u - (UnisonVoices - 1) / 2.0) * UnisonDetune / (UnisonVoices - 1);
                                unisonFreq *= Math.Pow(2.0, detuneOffset / 1200.0);
                            }

                            double phaseInc = 2.0 * Math.PI * unisonFreq / sampleRate;

                            // Get current phase
                            double phase = voice.UnisonPhases[osc, u];

                            // Handle sync
                            if (SyncMode != OscSyncMode.Off && osc == SyncSlaveOsc)
                            {
                                if (SyncMode == OscSyncMode.Hard && masterZeroCross)
                                {
                                    phase = 0;
                                }
                                else if (SyncMode == OscSyncMode.Soft)
                                {
                                    // Soft sync: modulate frequency
                                    phaseInc *= 1.0 + 0.5 * Math.Sin(masterPhase);
                                }
                            }

                            // Generate sample
                            float sample = GenerateSample(phase, config.Waveform, pulseWidth);
                            sample *= config.Level / UnisonVoices;

                            // Stereo spread
                            float pan = 0;
                            if (UnisonVoices > 1)
                            {
                                pan = ((float)u / (UnisonVoices - 1) * 2 - 1) * UnisonSpread;
                            }

                            float panL = (float)Math.Cos((pan + 1) * Math.PI / 4);
                            float panR = (float)Math.Sin((pan + 1) * Math.PI / 4);

                            sampleL += sample * panL;
                            sampleR += sample * panR;

                            // Track master for sync
                            if (osc == SyncMasterOsc && u == 0)
                            {
                                double prevPhase = voice.UnisonPhases[osc, u];
                                masterPhase = phase;
                                masterZeroCross = prevPhase > Math.PI && phase < Math.PI;
                            }

                            // Advance phase
                            phase += phaseInc;
                            while (phase > 2 * Math.PI) phase -= 2 * Math.PI;
                            voice.UnisonPhases[osc, u] = phase;
                        }
                    }

                    // Apply envelope, velocity, and volume
                    float gain = (float)(envValue * (voice.Velocity / 127.0) * Volume);
                    sampleL *= gain;
                    sampleR *= gain;

                    // Output
                    if (offset + n < buffer.Length)
                        buffer[offset + n] += sampleL;
                    if (channels > 1 && offset + n + 1 < buffer.Length)
                        buffer[offset + n + 1] += sampleR;
                }
            }
        }

        return count;
    }

    private float GenerateSample(double phase, WaveType waveform, float pulseWidth)
    {
        double normalizedPhase = phase / (2 * Math.PI);

        return waveform switch
        {
            WaveType.Sine => (float)Math.Sin(phase),

            WaveType.Square => normalizedPhase < pulseWidth ? 1f : -1f,

            WaveType.Sawtooth => (float)(2.0 * normalizedPhase - 1.0),

            WaveType.Triangle => normalizedPhase < 0.5
                ? (float)(4.0 * normalizedPhase - 1.0)
                : (float)(3.0 - 4.0 * normalizedPhase),

            WaveType.Noise => (float)(new Random((int)(phase * 10000)).NextDouble() * 2 - 1),

            _ => (float)Math.Sin(phase)
        };
    }

    /// <summary>
    /// Create preset configurations
    /// </summary>
    public static OscillatorBank CreatePreset(string presetName, int sampleRate = 44100)
    {
        var bank = new OscillatorBank(sampleRate);

        switch (presetName.ToLowerInvariant())
        {
            case "supersaw":
                bank.SetupSupersaw(7, 25f);
                bank.Attack = 0.01f;
                bank.Decay = 0.3f;
                bank.Sustain = 0.7f;
                bank.Release = 0.4f;
                break;

            case "fatbass":
                bank.SetOscillatorCount(2);
                bank.SetOscillator(0, new BankOscillatorConfig
                {
                    Waveform = WaveType.Sawtooth,
                    Level = 1f,
                    Octave = -1
                });
                bank.SetOscillator(1, new BankOscillatorConfig
                {
                    Waveform = WaveType.Square,
                    Level = 0.7f,
                    Octave = -1,
                    Detune = 5f
                });
                bank.UnisonVoices = 2;
                bank.UnisonDetune = 10f;
                bank.Attack = 0.005f;
                bank.Decay = 0.2f;
                bank.Sustain = 0.5f;
                bank.Release = 0.15f;
                break;

            case "sync lead":
                bank.SetOscillatorCount(2);
                bank.SetOscillator(0, new BankOscillatorConfig
                {
                    Waveform = WaveType.Sawtooth,
                    Level = 0.5f
                });
                bank.SetOscillator(1, new BankOscillatorConfig
                {
                    Waveform = WaveType.Sawtooth,
                    Level = 1f,
                    Octave = 1
                });
                bank.SyncMode = OscSyncMode.Hard;
                bank.SyncMasterOsc = 0;
                bank.SyncSlaveOsc = 1;
                bank.Attack = 0.01f;
                bank.Release = 0.2f;
                break;

            case "detuned":
                bank.SetOscillatorCount(3);
                bank.SetOscillator(0, new BankOscillatorConfig
                {
                    Waveform = WaveType.Sawtooth,
                    Level = 1f,
                    Detune = -10f
                });
                bank.SetOscillator(1, new BankOscillatorConfig
                {
                    Waveform = WaveType.Sawtooth,
                    Level = 1f,
                    Detune = 0f
                });
                bank.SetOscillator(2, new BankOscillatorConfig
                {
                    Waveform = WaveType.Sawtooth,
                    Level = 1f,
                    Detune = 10f
                });
                break;

            case "pwm pad":
                bank.SetOscillatorCount(1);
                bank.SetOscillator(0, new BankOscillatorConfig
                {
                    Waveform = WaveType.Square,
                    Level = 1f,
                    PulseWidth = 0.5f
                });
                bank.UnisonVoices = 4;
                bank.UnisonDetune = 15f;
                bank.UnisonSpread = 0.7f;
                bank.PulseWidthLFO = new LFO { Frequency = 0.5, Enabled = true };
                bank.PulseWidthLFODepth = 0.3f;
                bank.Attack = 0.5f;
                bank.Decay = 0.3f;
                bank.Sustain = 0.8f;
                bank.Release = 1f;
                break;
        }

        return bank;
    }

    private class BankVoice
    {
        public int Note { get; set; }
        public int Velocity { get; set; }
        public double BaseFrequency { get; set; }
        public bool IsActive { get; set; }
        public long StartTime { get; set; }
        public double[] OscPhases { get; set; } = new double[8];
        public double[,] UnisonPhases { get; set; } = new double[8, 8];
        public Envelope Envelope { get; set; } = new();
    }
}
