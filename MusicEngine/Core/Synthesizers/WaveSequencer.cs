// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Main sequencer for pattern playback and scheduling.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// Transition type between wave sequence steps.
/// </summary>
public enum WaveTransition
{
    /// <summary>Instant step change.</summary>
    Stepped,
    /// <summary>Linear crossfade.</summary>
    Linear,
    /// <summary>Smooth S-curve crossfade.</summary>
    Smooth,
    /// <summary>Exponential crossfade.</summary>
    Exponential
}

/// <summary>
/// A single step in the wave sequence.
/// </summary>
public class WaveSequenceStep
{
    /// <summary>Wavetable slot index (0-7).</summary>
    public int WavetableSlot { get; set; } = 0;
    /// <summary>Position within wavetable (0-1).</summary>
    public float WavePosition { get; set; } = 0f;
    /// <summary>Duration in beats.</summary>
    public float Duration { get; set; } = 1f;
    /// <summary>Level/amplitude for this step (0-1).</summary>
    public float Level { get; set; } = 1f;
    /// <summary>Pitch offset in semitones.</summary>
    public float PitchOffset { get; set; } = 0f;
    /// <summary>Filter cutoff modulation (-1 to 1).</summary>
    public float FilterMod { get; set; } = 0f;
    /// <summary>Pan position (-1 to 1).</summary>
    public float Pan { get; set; } = 0f;
    /// <summary>Gate/trigger for this step (false = rest).</summary>
    public bool Gate { get; set; } = true;

    public WaveSequenceStep() { }

    public WaveSequenceStep(int slot, float position, float duration = 1f)
    {
        WavetableSlot = slot;
        WavePosition = position;
        Duration = duration;
    }
}

/// <summary>
/// A wavetable containing multiple waveforms.
/// </summary>
public class Wavetable
{
    /// <summary>Wavetable name.</summary>
    public string Name { get; set; } = "Wavetable";
    /// <summary>Waveforms in this table (each is 2048 samples).</summary>
    public List<float[]> Waveforms { get; } = new();
    /// <summary>Number of waveforms in the table.</summary>
    public int WaveCount => Waveforms.Count;

    private const int WaveSize = 2048;

    public Wavetable()
    {
        // Initialize with a single sine wave
        AddSineWave();
    }

    /// <summary>
    /// Adds a sine wave to the table.
    /// </summary>
    public void AddSineWave()
    {
        var wave = new float[WaveSize];
        for (int i = 0; i < WaveSize; i++)
        {
            wave[i] = MathF.Sin(i * 2f * MathF.PI / WaveSize);
        }
        Waveforms.Add(wave);
    }

    /// <summary>
    /// Adds a sawtooth wave to the table.
    /// </summary>
    public void AddSawtoothWave()
    {
        var wave = new float[WaveSize];
        for (int i = 0; i < WaveSize; i++)
        {
            wave[i] = 2f * i / WaveSize - 1f;
        }
        Waveforms.Add(wave);
    }

    /// <summary>
    /// Adds a square wave to the table.
    /// </summary>
    public void AddSquareWave()
    {
        var wave = new float[WaveSize];
        for (int i = 0; i < WaveSize; i++)
        {
            wave[i] = i < WaveSize / 2 ? 1f : -1f;
        }
        Waveforms.Add(wave);
    }

    /// <summary>
    /// Adds a triangle wave to the table.
    /// </summary>
    public void AddTriangleWave()
    {
        var wave = new float[WaveSize];
        for (int i = 0; i < WaveSize; i++)
        {
            float t = (float)i / WaveSize;
            wave[i] = t < 0.5f ? (4f * t - 1f) : (3f - 4f * t);
        }
        Waveforms.Add(wave);
    }

    /// <summary>
    /// Adds a PWM wave with specified duty cycle.
    /// </summary>
    public void AddPWMWave(float dutyCycle)
    {
        var wave = new float[WaveSize];
        int threshold = (int)(WaveSize * dutyCycle);
        for (int i = 0; i < WaveSize; i++)
        {
            wave[i] = i < threshold ? 1f : -1f;
        }
        Waveforms.Add(wave);
    }

    /// <summary>
    /// Gets interpolated sample from the wavetable at given position.
    /// </summary>
    public float GetSample(float phase, float position)
    {
        if (Waveforms.Count == 0) return 0f;

        // Clamp position
        position = Math.Clamp(position, 0f, 1f);

        // Calculate which waves to blend
        float waveIndex = position * (Waveforms.Count - 1);
        int waveA = (int)Math.Floor(waveIndex);
        int waveB = Math.Min(waveA + 1, Waveforms.Count - 1);
        float blend = waveIndex - waveA;

        // Get samples from both waves with interpolation
        float sampleA = GetWaveSample(waveA, phase);
        float sampleB = GetWaveSample(waveB, phase);

        // Blend
        return sampleA + (sampleB - sampleA) * blend;
    }

    private float GetWaveSample(int waveIndex, float phase)
    {
        var wave = Waveforms[waveIndex];
        float samplePos = phase * WaveSize;
        int index = (int)samplePos % WaveSize;
        int nextIndex = (index + 1) % WaveSize;
        float frac = samplePos - (int)samplePos;

        return wave[index] + (wave[nextIndex] - wave[index]) * frac;
    }
}

/// <summary>
/// Internal wave sequencer voice state.
/// </summary>
internal class WaveSeqVoice
{
    private readonly int _sampleRate;
    private readonly WaveSequencer _synth;

    // Oscillator state
    private double _phase;

    // Sequence state
    private int _currentStep;
    private double _stepTime;
    private double _stepDuration;
    private float _currentPosition;
    private float _targetPosition;
    private int _currentSlot;
    private int _targetSlot;

    // Envelope
    private readonly Envelope _envelope;

    // Crossfade state
    private double _crossfadeProgress;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public double BaseFrequency { get; private set; }

    public WaveSeqVoice(int sampleRate, WaveSequencer synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;
        _envelope = new Envelope(0.01, 0.1, 0.8, 0.3);
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        IsActive = true;
        TriggerTime = DateTime.Now;
        BaseFrequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);

        // Reset oscillator
        _phase = 0;

        // Reset sequence
        _currentStep = 0;
        _stepTime = 0;
        UpdateStepFromSequence();

        // Copy envelope settings
        _envelope.Attack = _synth.Attack;
        _envelope.Decay = _synth.Decay;
        _envelope.Sustain = _synth.Sustain;
        _envelope.Release = _synth.Release;
        _envelope.Trigger(velocity);
    }

    public void Release()
    {
        _envelope.Release_Gate();
    }

    public (float left, float right) Process(double deltaTime)
    {
        if (!IsActive) return (0f, 0f);

        // Process envelope
        double envValue = _envelope.Process(deltaTime);
        if (!_envelope.IsActive)
        {
            IsActive = false;
            return (0f, 0f);
        }

        // Update sequence timing
        if (_synth.SequenceEnabled && _synth.Steps.Count > 0)
        {
            UpdateSequence(deltaTime);
        }

        // Get current step parameters
        var step = _synth.Steps.Count > 0 ? _synth.Steps[_currentStep] : null;

        float level = step?.Level ?? 1f;
        float pitchOffset = step?.PitchOffset ?? 0f;
        float filterMod = step?.FilterMod ?? 0f;
        float pan = step?.Pan ?? 0f;
        bool gate = step?.Gate ?? true;

        if (!gate)
        {
            return (0f, 0f);
        }

        // Calculate frequency with pitch offset
        double freq = BaseFrequency * Math.Pow(2.0, pitchOffset / 12.0);

        // Update phase
        _phase += freq / _sampleRate;
        if (_phase >= 1.0) _phase -= 1.0;

        // Get wave position (with crossfade)
        float position = _currentPosition;
        int slot = _currentSlot;

        if (_synth.Transition != WaveTransition.Stepped && _crossfadeProgress < 1.0)
        {
            // Interpolate position during crossfade
            float t = (float)ApplyTransitionCurve(_crossfadeProgress);
            position = _currentPosition + (_targetPosition - _currentPosition) * t;

            // If changing slots, crossfade between them
            if (_currentSlot != _targetSlot)
            {
                var wt1 = _synth.Wavetables[_currentSlot];
                var wt2 = _synth.Wavetables[_targetSlot];
                float s1 = wt1.GetSample((float)_phase, _currentPosition);
                float s2 = wt2.GetSample((float)_phase, _targetPosition);
                float sample = s1 + (s2 - s1) * t;

                // Apply envelope, level, velocity
                double velocityGain = Velocity / 127.0;
                sample *= (float)(envValue * velocityGain * level);

                // Apply panning
                float leftGain = pan <= 0 ? 1f : 1f - pan;
                float rightGain = pan >= 0 ? 1f : 1f + pan;

                return (sample * leftGain, sample * rightGain);
            }
        }

        // Get sample from wavetable
        var wavetable = _synth.Wavetables[slot];
        float output = wavetable.GetSample((float)_phase, position);

        // Apply envelope, level, velocity
        double velGain = Velocity / 127.0;
        output *= (float)(envValue * velGain * level);

        // Apply panning
        float lGain = pan <= 0 ? 1f : 1f - pan;
        float rGain = pan >= 0 ? 1f : 1f + pan;

        return (output * lGain, output * rGain);
    }

    private void UpdateSequence(double deltaTime)
    {
        // Calculate step duration in seconds based on tempo
        double beatsPerSecond = _synth.Tempo / 60.0;
        var currentStep = _synth.Steps[_currentStep];
        double targetDuration = currentStep.Duration / beatsPerSecond;

        _stepTime += deltaTime;
        _crossfadeProgress += deltaTime / Math.Max(0.01, _synth.CrossfadeTime);
        _crossfadeProgress = Math.Min(1.0, _crossfadeProgress);

        if (_stepTime >= targetDuration)
        {
            _stepTime -= targetDuration;

            // Move to next step
            if (_synth.SequenceLoop)
            {
                _currentStep = (_currentStep + 1) % _synth.Steps.Count;
            }
            else
            {
                _currentStep = Math.Min(_currentStep + 1, _synth.Steps.Count - 1);
            }

            UpdateStepFromSequence();
        }
    }

    private void UpdateStepFromSequence()
    {
        if (_synth.Steps.Count == 0) return;

        var step = _synth.Steps[_currentStep];

        // Store current as start point for crossfade
        _currentPosition = _targetPosition;
        _currentSlot = _targetSlot;

        // Set new targets
        _targetPosition = step.WavePosition;
        _targetSlot = Math.Clamp(step.WavetableSlot, 0, _synth.Wavetables.Length - 1);

        // Reset crossfade
        _crossfadeProgress = 0;

        // For stepped mode, instant change
        if (_synth.Transition == WaveTransition.Stepped)
        {
            _currentPosition = _targetPosition;
            _currentSlot = _targetSlot;
            _crossfadeProgress = 1.0;
        }
    }

    private double ApplyTransitionCurve(double t)
    {
        return _synth.Transition switch
        {
            WaveTransition.Linear => t,
            WaveTransition.Smooth => (1.0 - Math.Cos(t * Math.PI)) * 0.5,
            WaveTransition.Exponential => t * t,
            _ => t
        };
    }
}

/// <summary>
/// Wave sequencer synthesizer with multi-wavetable morphing and step sequencing.
/// Features wavetable slots with morphing, step sequencer for wave position, per-step modulation.
/// </summary>
public class WaveSequencer : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<WaveSeqVoice> _voices = new();
    private readonly Dictionary<int, WaveSeqVoice> _noteToVoice = new();
    private readonly object _lock = new();

    /// <summary>Gets or sets the synth name.</summary>
    public string Name { get; set; } = "WaveSequencer";

    /// <summary>Gets the audio format.</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Gets or sets the master volume (0-1).</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>Gets or sets the maximum number of voices.</summary>
    public int MaxVoices { get; set; } = 8;

    /// <summary>The 8 wavetable slots.</summary>
    public Wavetable[] Wavetables { get; } = new Wavetable[8];

    /// <summary>The wave sequence steps.</summary>
    public List<WaveSequenceStep> Steps { get; } = new();

    /// <summary>Enable/disable sequence playback.</summary>
    public bool SequenceEnabled { get; set; } = true;

    /// <summary>Loop the sequence.</summary>
    public bool SequenceLoop { get; set; } = true;

    /// <summary>Tempo in BPM for sequence timing.</summary>
    public float Tempo { get; set; } = 120f;

    /// <summary>Transition type between steps.</summary>
    public WaveTransition Transition { get; set; } = WaveTransition.Smooth;

    /// <summary>Crossfade time in seconds.</summary>
    public float CrossfadeTime { get; set; } = 0.1f;

    // Envelope
    /// <summary>Amp envelope attack.</summary>
    public double Attack { get; set; } = 0.01;
    /// <summary>Amp envelope decay.</summary>
    public double Decay { get; set; } = 0.1;
    /// <summary>Amp envelope sustain.</summary>
    public double Sustain { get; set; } = 0.8;
    /// <summary>Amp envelope release.</summary>
    public double Release { get; set; } = 0.3;

    /// <summary>
    /// Creates a new WaveSequencer.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (default: from Settings).</param>
    public WaveSequencer(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize wavetables with basic waveforms
        for (int i = 0; i < 8; i++)
        {
            Wavetables[i] = new Wavetable { Name = $"Wavetable {i + 1}" };
        }

        // Default wavetable 0: Basic waves
        Wavetables[0].Waveforms.Clear();
        Wavetables[0].AddSineWave();
        Wavetables[0].AddTriangleWave();
        Wavetables[0].AddSawtoothWave();
        Wavetables[0].AddSquareWave();

        // Default wavetable 1: PWM variations
        Wavetables[1].Waveforms.Clear();
        for (float pw = 0.1f; pw <= 0.9f; pw += 0.1f)
        {
            Wavetables[1].AddPWMWave(pw);
        }

        // Default sequence
        Steps.Add(new WaveSequenceStep(0, 0f, 1f));
        Steps.Add(new WaveSequenceStep(0, 0.33f, 1f));
        Steps.Add(new WaveSequenceStep(0, 0.66f, 1f));
        Steps.Add(new WaveSequenceStep(0, 1f, 1f));
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
            case "tempo": Tempo = Math.Clamp(value, 20f, 300f); break;
            case "transition": Transition = (WaveTransition)(int)value; break;
            case "crossfadetime": CrossfadeTime = Math.Clamp(value, 0.001f, 2f); break;
            case "sequenceenabled": SequenceEnabled = value > 0.5f; break;
            case "sequenceloop": SequenceLoop = value > 0.5f; break;
            case "attack": Attack = value; break;
            case "decay": Decay = value; break;
            case "sustain": Sustain = value; break;
            case "release": Release = value; break;
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

    private WaveSeqVoice? GetFreeVoice()
    {
        foreach (var voice in _voices)
        {
            if (!voice.IsActive) return voice;
        }

        if (_voices.Count < MaxVoices)
        {
            var voice = new WaveSeqVoice(_waveFormat.SampleRate, this);
            _voices.Add(voice);
            return voice;
        }

        // Voice stealing
        WaveSeqVoice? oldest = null;
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

    /// <summary>
    /// Clears the sequence.
    /// </summary>
    public void ClearSequence()
    {
        Steps.Clear();
    }

    /// <summary>
    /// Adds a step to the sequence.
    /// </summary>
    public void AddStep(int slot, float position, float duration = 1f)
    {
        Steps.Add(new WaveSequenceStep(slot, position, duration));
    }

    #region Presets

    /// <summary>Creates a morphing pad preset.</summary>
    public static WaveSequencer CreateMorphPad()
    {
        var synth = new WaveSequencer { Name = "Morph Pad" };
        synth.Tempo = 30f;
        synth.Transition = WaveTransition.Smooth;
        synth.CrossfadeTime = 0.5f;
        synth.Attack = 0.5;
        synth.Decay = 0.5;
        synth.Sustain = 0.9;
        synth.Release = 1.0;

        synth.Steps.Clear();
        synth.AddStep(0, 0f, 4f);
        synth.AddStep(0, 0.5f, 4f);
        synth.AddStep(0, 1f, 4f);
        synth.AddStep(0, 0.5f, 4f);

        return synth;
    }

    /// <summary>Creates an arpeggio sequence preset.</summary>
    public static WaveSequencer CreateArpSequence()
    {
        var synth = new WaveSequencer { Name = "Arp Sequence" };
        synth.Tempo = 140f;
        synth.Transition = WaveTransition.Stepped;
        synth.Attack = 0.01;
        synth.Decay = 0.2;
        synth.Sustain = 0.5;
        synth.Release = 0.2;

        synth.Steps.Clear();
        synth.AddStep(0, 0f, 0.5f);
        synth.AddStep(0, 0.33f, 0.5f);
        synth.AddStep(0, 0.66f, 0.5f);
        synth.AddStep(0, 1f, 0.5f);
        synth.AddStep(0, 0.66f, 0.5f);
        synth.AddStep(0, 0.33f, 0.5f);

        return synth;
    }

    /// <summary>Creates a PWM sweep preset.</summary>
    public static WaveSequencer CreatePWMSweep()
    {
        var synth = new WaveSequencer { Name = "PWM Sweep" };
        synth.Tempo = 60f;
        synth.Transition = WaveTransition.Linear;
        synth.CrossfadeTime = 0.2f;
        synth.Attack = 0.1;
        synth.Decay = 0.3;
        synth.Sustain = 0.7;
        synth.Release = 0.5;

        synth.Steps.Clear();
        for (int i = 0; i < 8; i++)
        {
            synth.AddStep(1, i / 7f, 1f);
        }

        return synth;
    }

    /// <summary>Creates a rhythmic texture preset.</summary>
    public static WaveSequencer CreateRhythmicTexture()
    {
        var synth = new WaveSequencer { Name = "Rhythmic Texture" };
        synth.Tempo = 120f;
        synth.Transition = WaveTransition.Smooth;
        synth.CrossfadeTime = 0.05f;
        synth.Attack = 0.01;
        synth.Decay = 0.1;
        synth.Sustain = 0.6;
        synth.Release = 0.3;

        synth.Steps.Clear();
        synth.Steps.Add(new WaveSequenceStep(0, 0f, 0.25f) { Level = 1f });
        synth.Steps.Add(new WaveSequenceStep(0, 0.5f, 0.25f) { Level = 0.7f });
        synth.Steps.Add(new WaveSequenceStep(0, 1f, 0.25f) { Level = 0.5f });
        synth.Steps.Add(new WaveSequenceStep(0, 0.5f, 0.25f) { Level = 0.7f });

        return synth;
    }

    #endregion
}
