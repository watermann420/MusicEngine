// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Synthesizer component.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// Electric piano model type.
/// </summary>
public enum EPianoModel
{
    /// <summary>Rhodes Mark I style - warm, bell-like tone.</summary>
    RhodesMarkI,
    /// <summary>Rhodes Mark II style - brighter, more bark.</summary>
    RhodesMarkII,
    /// <summary>Rhodes Suitcase - with built-in tremolo.</summary>
    RhodesSuitcase,
    /// <summary>Wurlitzer 200A style - reedy, growly tone.</summary>
    Wurlitzer,
    /// <summary>CP70/80 style electric grand.</summary>
    ElectricGrand
}

/// <summary>
/// Internal electric piano voice state.
/// </summary>
internal class EPianoVoice
{
    private readonly int _sampleRate;
    private readonly EPianoSynth _synth;
    private readonly Random _random;

    // Tine/tone bar model
    private double _tinePhase;
    private double _toneBarPhase;
    private double _tineEnvelope;
    private double _toneBarEnvelope;

    // Hammer noise
    private double _hammerEnvelope;

    // Overtones
    private readonly double[] _overtonePhases = new double[8];
    private readonly double[] _overtoneEnvelopes = new double[8];

    // Release resonance
    private bool _isReleasing;
    private double _releaseEnvelope;
    private double _releaseTime;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public double BaseFrequency { get; private set; }

    public EPianoVoice(int sampleRate, EPianoSynth synth)
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

        // Reset phases
        _tinePhase = 0;
        _toneBarPhase = _random.NextDouble() * 0.1;

        for (int i = 0; i < _overtonePhases.Length; i++)
        {
            _overtonePhases[i] = _random.NextDouble() * 0.05;
            _overtoneEnvelopes[i] = 1.0;
        }

        // Initialize envelopes based on velocity
        double velocityNorm = velocity / 127.0;

        // Higher velocity = more tine, brighter tone
        _tineEnvelope = 1.0;
        _toneBarEnvelope = 1.0;
        _hammerEnvelope = velocityNorm * velocityNorm; // Quadratic for punch

        _isReleasing = false;
        _releaseEnvelope = 1.0;
        _releaseTime = 0;
    }

    public void Release()
    {
        _isReleasing = true;
        _releaseTime = 0;
    }

    public float Process(double deltaTime)
    {
        if (!IsActive) return 0f;

        double velocityNorm = Velocity / 127.0;
        double output = 0;

        // Get model-specific parameters
        double tineLevel, toneBarLevel, hammerLevel;
        double tineDecay, toneBarDecay, overtonesDecay;
        double barkAmount;
        GetModelParameters(out tineLevel, out toneBarLevel, out hammerLevel,
                          out tineDecay, out toneBarDecay, out overtonesDecay, out barkAmount);

        // Velocity affects timbre - harder hits are brighter
        double brightnessFromVelocity = 0.5 + velocityNorm * 0.5;
        tineLevel *= brightnessFromVelocity;

        // Key scaling - higher notes decay faster
        double keyScale = 1.0 + (Note - 60) / 60.0 * 0.5;

        // Process release
        if (_isReleasing)
        {
            _releaseTime += deltaTime;
            double releaseRate = 6.0 * keyScale; // Faster release for higher notes
            _releaseEnvelope = Math.Exp(-_releaseTime * releaseRate);

            if (_releaseEnvelope < 0.001)
            {
                IsActive = false;
                return 0f;
            }
        }

        // Tine (primary tone generator)
        _tinePhase += BaseFrequency / _sampleRate;
        if (_tinePhase >= 1.0) _tinePhase -= 1.0;

        // Tine produces complex waveform with slight asymmetry
        double tine = Math.Sin(_tinePhase * 2.0 * Math.PI);
        // Add asymmetry (bell-like quality)
        tine += Math.Sin(_tinePhase * 4.0 * Math.PI) * 0.3 * brightnessFromVelocity;
        tine += Math.Sin(_tinePhase * 6.0 * Math.PI) * 0.15 * brightnessFromVelocity;

        // Decay tine envelope
        double effectiveTineDecay = tineDecay * keyScale;
        _tineEnvelope *= Math.Exp(-deltaTime * effectiveTineDecay);
        output += tine * _tineEnvelope * tineLevel;

        // Tone bar (resonator)
        _toneBarPhase += BaseFrequency / _sampleRate;
        if (_toneBarPhase >= 1.0) _toneBarPhase -= 1.0;

        double toneBar = Math.Sin(_toneBarPhase * 2.0 * Math.PI);

        // Decay tone bar (slower than tine)
        _toneBarEnvelope *= Math.Exp(-deltaTime * toneBarDecay * keyScale);
        output += toneBar * _toneBarEnvelope * toneBarLevel;

        // Hammer/click noise (attack transient)
        if (_hammerEnvelope > 0.001)
        {
            double noise = _random.NextDouble() * 2.0 - 1.0;
            // Bandpass the noise around a high frequency
            double hammerTone = Math.Sin(_tinePhase * 16.0 * Math.PI) * noise;
            output += hammerTone * _hammerEnvelope * hammerLevel;
            _hammerEnvelope *= Math.Exp(-deltaTime * 100.0); // Very fast decay
        }

        // Overtones (bell-like)
        double[] overtoneRatios = { 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0 };
        double[] overtoneAmps = { 0.3, 0.2, 0.15, 0.1, 0.08, 0.06, 0.04, 0.03 };

        for (int i = 0; i < _overtonePhases.Length; i++)
        {
            double freq = BaseFrequency * overtoneRatios[i];
            if (freq > _sampleRate * 0.45) continue;

            _overtonePhases[i] += freq / _sampleRate;
            if (_overtonePhases[i] >= 1.0) _overtonePhases[i] -= 1.0;

            double overtone = Math.Sin(_overtonePhases[i] * 2.0 * Math.PI);

            // Higher overtones decay faster
            double overtoneDecayRate = overtonesDecay * (1.0 + i * 0.5) * keyScale;
            _overtoneEnvelopes[i] *= Math.Exp(-deltaTime * overtoneDecayRate);

            output += overtone * _overtoneEnvelopes[i] * overtoneAmps[i] * brightnessFromVelocity;
        }

        // "Bark" - the characteristic growl on hard hits
        if (barkAmount > 0 && velocityNorm > 0.6)
        {
            double barkLevel = (velocityNorm - 0.6) / 0.4 * barkAmount;
            double bark = Math.Sin(_tinePhase * 2.0 * Math.PI);
            bark = Math.Sign(bark) * Math.Pow(Math.Abs(bark), 0.7); // Soft clipping
            output += bark * _tineEnvelope * barkLevel * 0.3;
        }

        // Apply velocity
        output *= velocityNorm;

        // Apply release envelope
        output *= _releaseEnvelope;

        return (float)output;
    }

    private void GetModelParameters(out double tineLevel, out double toneBarLevel, out double hammerLevel,
                                    out double tineDecay, out double toneBarDecay, out double overtonesDecay,
                                    out double barkAmount)
    {
        switch (_synth.Model)
        {
            case EPianoModel.RhodesMarkI:
                tineLevel = 0.6;
                toneBarLevel = 0.4;
                hammerLevel = 0.3;
                tineDecay = 3.0;
                toneBarDecay = 1.5;
                overtonesDecay = 4.0;
                barkAmount = 0.3;
                break;

            case EPianoModel.RhodesMarkII:
                tineLevel = 0.7;
                toneBarLevel = 0.3;
                hammerLevel = 0.4;
                tineDecay = 4.0;
                toneBarDecay = 2.0;
                overtonesDecay = 5.0;
                barkAmount = 0.5;
                break;

            case EPianoModel.RhodesSuitcase:
                tineLevel = 0.55;
                toneBarLevel = 0.45;
                hammerLevel = 0.25;
                tineDecay = 2.5;
                toneBarDecay = 1.2;
                overtonesDecay = 3.5;
                barkAmount = 0.2;
                break;

            case EPianoModel.Wurlitzer:
                tineLevel = 0.8; // Reed-like
                toneBarLevel = 0.2;
                hammerLevel = 0.5;
                tineDecay = 5.0;
                toneBarDecay = 3.0;
                overtonesDecay = 6.0;
                barkAmount = 0.7; // More growl
                break;

            case EPianoModel.ElectricGrand:
                tineLevel = 0.4;
                toneBarLevel = 0.6; // More string-like
                hammerLevel = 0.6;
                tineDecay = 2.0;
                toneBarDecay = 1.0;
                overtonesDecay = 2.5;
                barkAmount = 0.1;
                break;

            default:
                tineLevel = 0.6;
                toneBarLevel = 0.4;
                hammerLevel = 0.3;
                tineDecay = 3.0;
                toneBarDecay = 1.5;
                overtonesDecay = 4.0;
                barkAmount = 0.3;
                break;
        }
    }
}

/// <summary>
/// Electric piano synthesizer with Rhodes/Wurlitzer physical modeling.
/// Features tine/tone bar modeling, velocity-dependent timbre, tremolo, and built-in effects.
/// </summary>
public class EPianoSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<EPianoVoice> _voices = new();
    private readonly Dictionary<int, EPianoVoice> _noteToVoice = new();
    private readonly object _lock = new();

    // Tremolo state
    private double _tremoloPhase;

    // Chorus state
    private readonly double[] _chorusDelayBuffer;
    private int _chorusWriteIndex;
    private double _chorusLfoPhase;

    // Phaser state
    private readonly double[] _phaserAllpass = new double[6];
    private double _phaserLfoPhase;

    /// <summary>Gets or sets the synth name.</summary>
    public string Name { get; set; } = "EPianoSynth";

    /// <summary>Gets the audio format.</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Gets or sets the master volume (0-1).</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>Gets or sets the maximum number of voices.</summary>
    public int MaxVoices { get; set; } = 16;

    /// <summary>Gets or sets the electric piano model.</summary>
    public EPianoModel Model { get; set; } = EPianoModel.RhodesMarkI;

    // Tremolo (built-in to Rhodes Suitcase)
    /// <summary>Enable tremolo effect.</summary>
    public bool TremoloEnabled { get; set; } = false;
    /// <summary>Tremolo rate in Hz.</summary>
    public float TremoloRate { get; set; } = 5.5f;
    /// <summary>Tremolo depth (0-1).</summary>
    public float TremoloDepth { get; set; } = 0.5f;
    /// <summary>Tremolo stereo spread.</summary>
    public float TremoloStereo { get; set; } = 0.3f;

    // Chorus effect
    /// <summary>Enable chorus effect.</summary>
    public bool ChorusEnabled { get; set; } = false;
    /// <summary>Chorus rate in Hz.</summary>
    public float ChorusRate { get; set; } = 0.8f;
    /// <summary>Chorus depth.</summary>
    public float ChorusDepth { get; set; } = 0.5f;
    /// <summary>Chorus mix (0-1).</summary>
    public float ChorusMix { get; set; } = 0.5f;

    // Phaser effect
    /// <summary>Enable phaser effect.</summary>
    public bool PhaserEnabled { get; set; } = false;
    /// <summary>Phaser rate in Hz.</summary>
    public float PhaserRate { get; set; } = 0.5f;
    /// <summary>Phaser depth.</summary>
    public float PhaserDepth { get; set; } = 0.7f;
    /// <summary>Phaser feedback.</summary>
    public float PhaserFeedback { get; set; } = 0.5f;
    /// <summary>Phaser mix (0-1).</summary>
    public float PhaserMix { get; set; } = 0.5f;

    // Tone controls
    /// <summary>Bass level adjustment.</summary>
    public float Bass { get; set; } = 0.5f;
    /// <summary>Treble level adjustment.</summary>
    public float Treble { get; set; } = 0.5f;

    // Drive/saturation
    /// <summary>Drive/overdrive amount (0-1).</summary>
    public float Drive { get; set; } = 0f;

    /// <summary>
    /// Creates a new EPianoSynth.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (default: from Settings).</param>
    public EPianoSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize chorus delay buffer (max 30ms)
        _chorusDelayBuffer = new double[(int)(rate * 0.03)];
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
            case "model": Model = (EPianoModel)(int)value; break;

            case "tremoloenabled": TremoloEnabled = value > 0.5f; break;
            case "tremolorate": TremoloRate = Math.Clamp(value, 0.1f, 20f); break;
            case "tremolodepth": TremoloDepth = Math.Clamp(value, 0f, 1f); break;
            case "tremolostereo": TremoloStereo = Math.Clamp(value, 0f, 1f); break;

            case "chorusenabled": ChorusEnabled = value > 0.5f; break;
            case "chorusrate": ChorusRate = Math.Clamp(value, 0.1f, 5f); break;
            case "chorusdepth": ChorusDepth = Math.Clamp(value, 0f, 1f); break;
            case "chorusmix": ChorusMix = Math.Clamp(value, 0f, 1f); break;

            case "phaserenabled": PhaserEnabled = value > 0.5f; break;
            case "phaserrate": PhaserRate = Math.Clamp(value, 0.1f, 5f); break;
            case "phaserdepth": PhaserDepth = Math.Clamp(value, 0f, 1f); break;
            case "phaserfeedback": PhaserFeedback = Math.Clamp(value, 0f, 0.95f); break;
            case "phasermix": PhaserMix = Math.Clamp(value, 0f, 1f); break;

            case "bass": Bass = Math.Clamp(value, 0f, 1f); break;
            case "treble": Treble = Math.Clamp(value, 0f, 1f); break;
            case "drive": Drive = Math.Clamp(value, 0f, 1f); break;
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

                // Apply drive/saturation
                if (Drive > 0)
                {
                    float driveAmount = 1f + Drive * 5f;
                    sample = MathF.Tanh(sample * driveAmount) / MathF.Tanh(driveAmount);
                }

                // Apply effects
                float leftOut = sample;
                float rightOut = sample;

                // Tremolo
                if (TremoloEnabled)
                {
                    _tremoloPhase += TremoloRate * deltaTime * 2.0 * Math.PI;
                    if (_tremoloPhase >= 2.0 * Math.PI) _tremoloPhase -= 2.0 * Math.PI;

                    double tremoloMod = 1.0 - TremoloDepth * 0.5 * (1.0 + Math.Sin(_tremoloPhase));
                    double tremoloModR = 1.0 - TremoloDepth * 0.5 * (1.0 + Math.Sin(_tremoloPhase + TremoloStereo * Math.PI));

                    leftOut *= (float)tremoloMod;
                    rightOut *= (float)tremoloModR;
                }

                // Chorus
                if (ChorusEnabled)
                {
                    _chorusLfoPhase += ChorusRate * deltaTime * 2.0 * Math.PI;
                    if (_chorusLfoPhase >= 2.0 * Math.PI) _chorusLfoPhase -= 2.0 * Math.PI;

                    // Write to delay buffer
                    _chorusDelayBuffer[_chorusWriteIndex] = sample;
                    _chorusWriteIndex = (_chorusWriteIndex + 1) % _chorusDelayBuffer.Length;

                    // Calculate delay time with modulation
                    double delayMs = 7.0 + 5.0 * ChorusDepth * Math.Sin(_chorusLfoPhase);
                    int delaySamples = (int)(delayMs * _waveFormat.SampleRate / 1000.0);

                    int readIndex = (_chorusWriteIndex - delaySamples + _chorusDelayBuffer.Length) % _chorusDelayBuffer.Length;
                    float delayed = (float)_chorusDelayBuffer[readIndex];

                    leftOut = leftOut * (1f - ChorusMix * 0.5f) + delayed * ChorusMix;
                    rightOut = rightOut * (1f - ChorusMix * 0.5f) + delayed * ChorusMix;
                }

                // Phaser
                if (PhaserEnabled)
                {
                    _phaserLfoPhase += PhaserRate * deltaTime * 2.0 * Math.PI;
                    if (_phaserLfoPhase >= 2.0 * Math.PI) _phaserLfoPhase -= 2.0 * Math.PI;

                    // Calculate allpass coefficient from LFO
                    double minFreq = 200.0;
                    double maxFreq = 4000.0;
                    double freq = minFreq + (maxFreq - minFreq) * (0.5 + 0.5 * Math.Sin(_phaserLfoPhase)) * PhaserDepth;
                    double coef = (1.0 - Math.Tan(Math.PI * freq / _waveFormat.SampleRate)) /
                                  (1.0 + Math.Tan(Math.PI * freq / _waveFormat.SampleRate));

                    // Process through allpass chain
                    double wet = sample + PhaserFeedback * _phaserAllpass[5];
                    for (int i = 0; i < 6; i++)
                    {
                        double input = wet;
                        wet = coef * (input - _phaserAllpass[i]) + _phaserAllpass[i];
                        _phaserAllpass[i] = (float)wet;
                        wet = input;
                    }

                    float phasedL = (float)(sample * (1f - PhaserMix) + wet * PhaserMix);
                    leftOut = leftOut * (1f - PhaserMix) + phasedL * PhaserMix;
                    rightOut = rightOut * (1f - PhaserMix) + phasedL * PhaserMix;
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

    private EPianoVoice? GetFreeVoice()
    {
        foreach (var voice in _voices)
        {
            if (!voice.IsActive) return voice;
        }

        if (_voices.Count < MaxVoices)
        {
            var voice = new EPianoVoice(_waveFormat.SampleRate, this);
            _voices.Add(voice);
            return voice;
        }

        // Voice stealing
        EPianoVoice? oldest = null;
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

    /// <summary>Creates a classic Rhodes Mark I preset.</summary>
    public static EPianoSynth CreateRhodesClassic()
    {
        var synth = new EPianoSynth
        {
            Name = "Rhodes Classic",
            Model = EPianoModel.RhodesMarkI,
            TremoloEnabled = false,
            ChorusEnabled = true,
            ChorusRate = 0.6f,
            ChorusDepth = 0.3f,
            ChorusMix = 0.3f
        };
        return synth;
    }

    /// <summary>Creates a Rhodes Suitcase preset with tremolo.</summary>
    public static EPianoSynth CreateRhodesSuitcase()
    {
        var synth = new EPianoSynth
        {
            Name = "Rhodes Suitcase",
            Model = EPianoModel.RhodesSuitcase,
            TremoloEnabled = true,
            TremoloRate = 5.5f,
            TremoloDepth = 0.6f,
            TremoloStereo = 0.5f
        };
        return synth;
    }

    /// <summary>Creates a Wurlitzer preset.</summary>
    public static EPianoSynth CreateWurlitzer()
    {
        var synth = new EPianoSynth
        {
            Name = "Wurlitzer",
            Model = EPianoModel.Wurlitzer,
            TremoloEnabled = true,
            TremoloRate = 6f,
            TremoloDepth = 0.4f
        };
        return synth;
    }

    /// <summary>Creates an electric grand preset.</summary>
    public static EPianoSynth CreateElectricGrand()
    {
        var synth = new EPianoSynth
        {
            Name = "Electric Grand",
            Model = EPianoModel.ElectricGrand,
            ChorusEnabled = true,
            ChorusRate = 0.5f,
            ChorusDepth = 0.2f,
            ChorusMix = 0.2f
        };
        return synth;
    }

    /// <summary>Creates a driven Rhodes preset.</summary>
    public static EPianoSynth CreateDrivenRhodes()
    {
        var synth = new EPianoSynth
        {
            Name = "Driven Rhodes",
            Model = EPianoModel.RhodesMarkII,
            Drive = 0.4f,
            PhaserEnabled = true,
            PhaserRate = 0.4f,
            PhaserDepth = 0.6f,
            PhaserFeedback = 0.3f,
            PhaserMix = 0.4f
        };
        return synth;
    }

    /// <summary>Creates a soft ballad Rhodes preset.</summary>
    public static EPianoSynth CreateBalladRhodes()
    {
        var synth = new EPianoSynth
        {
            Name = "Ballad Rhodes",
            Model = EPianoModel.RhodesMarkI,
            Treble = 0.4f,
            ChorusEnabled = true,
            ChorusRate = 0.3f,
            ChorusDepth = 0.4f,
            ChorusMix = 0.4f
        };
        return synth;
    }

    #endregion
}
