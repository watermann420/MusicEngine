//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Korg Wavestation-style wave sequencing synthesizer with vector crossfading.
// Features up to 64 steps per sequence, 4 wave sequences with XY vector control,
// per-step waveform/pitch/level/pan, multiple loop modes, tempo sync, and modulation.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// Waveform types for wave sequence steps.
/// </summary>
public enum WaveSeqWaveform
{
    /// <summary>Sine wave.</summary>
    Sine,
    /// <summary>Sawtooth wave.</summary>
    Saw,
    /// <summary>Square wave.</summary>
    Square,
    /// <summary>Triangle wave.</summary>
    Triangle,
    /// <summary>Custom wavetable (uses WavetableIndex).</summary>
    CustomWavetable
}

/// <summary>
/// Sequence loop mode.
/// </summary>
public enum WaveSeqLoopMode
{
    /// <summary>Play forward and loop.</summary>
    Forward,
    /// <summary>Play reverse and loop.</summary>
    Reverse,
    /// <summary>Play forward then reverse (ping-pong).</summary>
    PingPong,
    /// <summary>Random step selection.</summary>
    Random,
    /// <summary>One-shot (no loop).</summary>
    OneShot
}

/// <summary>
/// Duration mode for sequence steps.
/// </summary>
public enum WaveSeqDurationMode
{
    /// <summary>Duration in beats (tempo-synced).</summary>
    Beats,
    /// <summary>Duration in milliseconds (free-running).</summary>
    Milliseconds
}

/// <summary>
/// A single step in a wave sequence.
/// </summary>
public class WaveSeqStep
{
    /// <summary>Waveform type for this step.</summary>
    public WaveSeqWaveform Waveform { get; set; } = WaveSeqWaveform.Sine;

    /// <summary>Index into custom wavetable array (when Waveform is CustomWavetable).</summary>
    public int WavetableIndex { get; set; } = 0;

    /// <summary>Position within wavetable (0-1).</summary>
    public float WavetablePosition { get; set; } = 0f;

    /// <summary>Duration value (interpretation depends on DurationMode).</summary>
    public float Duration { get; set; } = 0.25f;

    /// <summary>Pitch offset in semitones (-24 to +24).</summary>
    public float PitchOffset { get; set; } = 0f;

    /// <summary>Level/amplitude (0-1).</summary>
    public float Level { get; set; } = 1.0f;

    /// <summary>Pan position (-1 = left, 0 = center, 1 = right).</summary>
    public float Pan { get; set; } = 0f;

    /// <summary>Crossfade percentage to next step (0-100).</summary>
    public float CrossfadePercent { get; set; } = 0f;

    /// <summary>Whether this step is active (false = rest/skip).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Creates a default wave sequence step.
    /// </summary>
    public WaveSeqStep() { }

    /// <summary>
    /// Creates a wave sequence step with specified parameters.
    /// </summary>
    public WaveSeqStep(WaveSeqWaveform waveform, float duration, float level = 1f)
    {
        Waveform = waveform;
        Duration = duration;
        Level = level;
    }

    /// <summary>
    /// Creates a copy of this step.
    /// </summary>
    public WaveSeqStep Clone()
    {
        return new WaveSeqStep
        {
            Waveform = Waveform,
            WavetableIndex = WavetableIndex,
            WavetablePosition = WavetablePosition,
            Duration = Duration,
            PitchOffset = PitchOffset,
            Level = Level,
            Pan = Pan,
            CrossfadePercent = CrossfadePercent,
            Enabled = Enabled
        };
    }
}

/// <summary>
/// A complete wave sequence containing up to 64 steps.
/// </summary>
public class WaveSequence
{
    /// <summary>Maximum number of steps in a sequence.</summary>
    public const int MaxSteps = 64;

    /// <summary>Sequence name.</summary>
    public string Name { get; set; } = "Sequence";

    /// <summary>Steps in this sequence.</summary>
    public List<WaveSeqStep> Steps { get; } = new();

    /// <summary>Loop mode for this sequence.</summary>
    public WaveSeqLoopMode LoopMode { get; set; } = WaveSeqLoopMode.Forward;

    /// <summary>Duration mode (beats or milliseconds).</summary>
    public WaveSeqDurationMode DurationMode { get; set; } = WaveSeqDurationMode.Beats;

    /// <summary>Loop start step index (0-based).</summary>
    public int LoopStart { get; set; } = 0;

    /// <summary>Loop end step index (0-based, -1 for last step).</summary>
    public int LoopEnd { get; set; } = -1;

    /// <summary>Whether sequence resets on note-on (gate mode).</summary>
    public bool GateMode { get; set; } = true;

    /// <summary>Start step modulation from velocity (0 = no modulation, 1 = full range).</summary>
    public float VelocityStartMod { get; set; } = 0f;

    /// <summary>Start step modulation from mod wheel (0 = no modulation, 1 = full range).</summary>
    public float ModWheelStartMod { get; set; } = 0f;

    /// <summary>
    /// Creates a new empty wave sequence.
    /// </summary>
    public WaveSequence()
    {
        Steps.Add(new WaveSeqStep());
    }

    /// <summary>
    /// Creates a wave sequence with the specified name.
    /// </summary>
    public WaveSequence(string name) : this()
    {
        Name = name;
    }

    /// <summary>
    /// Gets the effective loop end index.
    /// </summary>
    public int EffectiveLoopEnd => LoopEnd < 0 ? Steps.Count - 1 : Math.Min(LoopEnd, Steps.Count - 1);

    /// <summary>
    /// Adds a step to the sequence.
    /// </summary>
    public void AddStep(WaveSeqStep step)
    {
        if (Steps.Count < MaxSteps)
        {
            Steps.Add(step);
        }
    }

    /// <summary>
    /// Removes a step at the specified index.
    /// </summary>
    public void RemoveStep(int index)
    {
        if (index >= 0 && index < Steps.Count)
        {
            Steps.RemoveAt(index);
        }
    }

    /// <summary>
    /// Clears all steps and adds a default step.
    /// </summary>
    public void Clear()
    {
        Steps.Clear();
        Steps.Add(new WaveSeqStep());
    }

    /// <summary>
    /// Gets the calculated start step based on velocity and mod wheel modulation.
    /// </summary>
    public int GetModulatedStartStep(int velocity, float modWheel)
    {
        if (Steps.Count <= 1) return 0;

        float velocityMod = (velocity / 127f) * VelocityStartMod * (Steps.Count - 1);
        float modWheelMod = modWheel * ModWheelStartMod * (Steps.Count - 1);
        int startStep = (int)(velocityMod + modWheelMod);

        return Math.Clamp(startStep, 0, Steps.Count - 1);
    }
}

/// <summary>
/// Custom wavetable for wave sequence steps.
/// </summary>
public class WaveSeqWavetable
{
    /// <summary>Size of each waveform frame.</summary>
    public const int FrameSize = 2048;

    /// <summary>Wavetable name.</summary>
    public string Name { get; set; } = "Wavetable";

    /// <summary>Waveform frames in this table.</summary>
    public List<float[]> Frames { get; } = new();

    /// <summary>
    /// Creates an empty wavetable.
    /// </summary>
    public WaveSeqWavetable() { }

    /// <summary>
    /// Creates a wavetable with the specified name.
    /// </summary>
    public WaveSeqWavetable(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Generates and adds a sine wave frame.
    /// </summary>
    public void AddSineWave()
    {
        var frame = new float[FrameSize];
        for (int i = 0; i < FrameSize; i++)
        {
            frame[i] = MathF.Sin(i * 2f * MathF.PI / FrameSize);
        }
        Frames.Add(frame);
    }

    /// <summary>
    /// Generates and adds a sawtooth wave frame.
    /// </summary>
    public void AddSawWave()
    {
        var frame = new float[FrameSize];
        for (int i = 0; i < FrameSize; i++)
        {
            frame[i] = 2f * i / FrameSize - 1f;
        }
        Frames.Add(frame);
    }

    /// <summary>
    /// Generates and adds a square wave frame.
    /// </summary>
    public void AddSquareWave()
    {
        var frame = new float[FrameSize];
        for (int i = 0; i < FrameSize; i++)
        {
            frame[i] = i < FrameSize / 2 ? 1f : -1f;
        }
        Frames.Add(frame);
    }

    /// <summary>
    /// Generates and adds a triangle wave frame.
    /// </summary>
    public void AddTriangleWave()
    {
        var frame = new float[FrameSize];
        for (int i = 0; i < FrameSize; i++)
        {
            float t = (float)i / FrameSize;
            frame[i] = t < 0.5f ? (4f * t - 1f) : (3f - 4f * t);
        }
        Frames.Add(frame);
    }

    /// <summary>
    /// Adds a custom waveform frame.
    /// </summary>
    public void AddFrame(float[] samples)
    {
        if (samples == null || samples.Length == 0) return;

        var frame = new float[FrameSize];
        float ratio = (float)samples.Length / FrameSize;

        for (int i = 0; i < FrameSize; i++)
        {
            float srcIndex = i * ratio;
            int idx = (int)srcIndex;
            float frac = srcIndex - idx;

            if (idx + 1 < samples.Length)
            {
                frame[i] = samples[idx] * (1 - frac) + samples[idx + 1] * frac;
            }
            else
            {
                frame[i] = samples[idx % samples.Length];
            }
        }

        Frames.Add(frame);
    }

    /// <summary>
    /// Gets an interpolated sample from the wavetable.
    /// </summary>
    public float GetSample(float phase, float position)
    {
        if (Frames.Count == 0) return 0f;

        position = Math.Clamp(position, 0f, 1f);

        float frameIndex = position * (Frames.Count - 1);
        int frame1 = (int)frameIndex;
        int frame2 = Math.Min(frame1 + 1, Frames.Count - 1);
        float frameMix = frameIndex - frame1;

        float samplePos = phase * FrameSize;
        int idx1 = (int)samplePos % FrameSize;
        int idx2 = (idx1 + 1) % FrameSize;
        float sampleMix = samplePos - (int)samplePos;

        float s1 = Frames[frame1][idx1] * (1 - sampleMix) + Frames[frame1][idx2] * sampleMix;
        float s2 = Frames[frame2][idx1] * (1 - sampleMix) + Frames[frame2][idx2] * sampleMix;

        return s1 * (1 - frameMix) + s2 * frameMix;
    }
}

/// <summary>
/// Internal voice state for WaveSequencerSynth.
/// </summary>
internal class WaveSeqSynthVoice
{
    private readonly int _sampleRate;
    private readonly WaveSequencerSynth _synth;

    private double _phase;

    private readonly int[] _currentSteps = new int[4];
    private readonly double[] _stepTimes = new double[4];
    private readonly int[] _pingPongDirections = new int[4];
    private readonly Random _random = new();

    private readonly float[] _crossfadeProgress = new float[4];
    private readonly WaveSeqStep?[] _prevSteps = new WaveSeqStep?[4];

    private readonly Envelope _ampEnvelope;
    private readonly Envelope _filterEnvelope;

    private float _filterState1;
    private float _filterState2;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public double BaseFrequency { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public bool IsActive => _ampEnvelope.IsActive;
    public bool IsReleasing => _ampEnvelope.Stage == EnvelopeStage.Release;
    public double CurrentAmplitude => _ampEnvelope.Value * (Velocity / 127.0);
    public float ModWheel { get; set; } = 0f;

    public WaveSeqSynthVoice(int sampleRate, WaveSequencerSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;
        _ampEnvelope = new Envelope(0.01, 0.1, 0.8, 0.3);
        _filterEnvelope = new Envelope(0.01, 0.2, 0.5, 0.3);

        for (int i = 0; i < 4; i++)
        {
            _pingPongDirections[i] = 1;
        }
    }

    public void Trigger(int note, int velocity, float modWheel)
    {
        Note = note;
        Velocity = velocity;
        ModWheel = modWheel;
        BaseFrequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);
        TriggerTime = DateTime.Now;

        _phase = 0;

        _ampEnvelope.Attack = _synth.AmpAttack;
        _ampEnvelope.Decay = _synth.AmpDecay;
        _ampEnvelope.Sustain = _synth.AmpSustain;
        _ampEnvelope.Release = _synth.AmpRelease;

        _filterEnvelope.Attack = _synth.FilterEnvAttack;
        _filterEnvelope.Decay = _synth.FilterEnvDecay;
        _filterEnvelope.Sustain = _synth.FilterEnvSustain;
        _filterEnvelope.Release = _synth.FilterEnvRelease;

        for (int i = 0; i < 4; i++)
        {
            var seq = GetSequence(i);
            if (seq != null && seq.GateMode)
            {
                _currentSteps[i] = seq.GetModulatedStartStep(velocity, modWheel);
            }
            _stepTimes[i] = 0;
            _pingPongDirections[i] = 1;
            _crossfadeProgress[i] = 1f;
            _prevSteps[i] = null;
        }

        _filterState1 = 0;
        _filterState2 = 0;

        _ampEnvelope.Trigger(velocity);
        _filterEnvelope.Trigger(velocity);
    }

    public void Release()
    {
        _ampEnvelope.Release_Gate();
        _filterEnvelope.Release_Gate();
    }

    public void Reset()
    {
        Note = -1;
        Velocity = 0;
        _phase = 0;
        _ampEnvelope.Reset();
        _filterEnvelope.Reset();

        for (int i = 0; i < 4; i++)
        {
            _currentSteps[i] = 0;
            _stepTimes[i] = 0;
            _pingPongDirections[i] = 1;
        }
    }

    private WaveSequence? GetSequence(int index)
    {
        return index switch
        {
            0 => _synth.SequenceA,
            1 => _synth.SequenceB,
            2 => _synth.SequenceC,
            3 => _synth.SequenceD,
            _ => null
        };
    }

    public (float left, float right) Process(double deltaTime)
    {
        if (!IsActive) return (0f, 0f);

        double ampEnv = _ampEnvelope.Process(deltaTime);
        double filterEnv = _filterEnvelope.Process(deltaTime);

        if (_ampEnvelope.Stage == EnvelopeStage.Idle) return (0f, 0f);

        float speedMod = 1f;
        if (_synth.SpeedLFO != null && _synth.SpeedLFO.Enabled)
        {
            double lfoValue = _synth.SpeedLFO.GetValue(_sampleRate);
            speedMod = 1f + (float)lfoValue * _synth.SpeedLFODepth;
            speedMod = Math.Max(0.1f, speedMod);
        }

        float[] sequenceSamples = new float[4];
        float[] sequencePans = new float[4];

        for (int seqIdx = 0; seqIdx < 4; seqIdx++)
        {
            var seq = GetSequence(seqIdx);
            if (seq == null || seq.Steps.Count == 0)
            {
                sequenceSamples[seqIdx] = 0f;
                sequencePans[seqIdx] = 0f;
                continue;
            }

            UpdateSequence(seqIdx, deltaTime, speedMod);

            var currentStep = seq.Steps[_currentSteps[seqIdx]];
            float sample = 0f;
            float pan = currentStep.Pan;

            if (currentStep.Enabled)
            {
                float waveformSample = GenerateWaveform(currentStep, (float)_phase);

                if (_crossfadeProgress[seqIdx] < 1f && _prevSteps[seqIdx] != null)
                {
                    float prevSample = GenerateWaveform(_prevSteps[seqIdx]!, (float)_phase);
                    float crossfade = SmoothStep(_crossfadeProgress[seqIdx]);
                    waveformSample = prevSample * (1 - crossfade) + waveformSample * crossfade;
                    pan = _prevSteps[seqIdx]!.Pan * (1 - crossfade) + currentStep.Pan * crossfade;
                }

                sample = waveformSample * currentStep.Level;
            }

            sequenceSamples[seqIdx] = sample;
            sequencePans[seqIdx] = pan;
        }

        double avgFreq = BaseFrequency;
        int activeSeqs = 0;
        for (int i = 0; i < 4; i++)
        {
            var seq = GetSequence(i);
            if (seq != null && seq.Steps.Count > 0)
            {
                var step = seq.Steps[_currentSteps[i]];
                avgFreq += BaseFrequency * Math.Pow(2.0, step.PitchOffset / 12.0);
                activeSeqs++;
            }
        }
        if (activeSeqs > 0)
        {
            avgFreq = avgFreq / (activeSeqs + 1);
        }

        _phase += avgFreq / _sampleRate;
        if (_phase >= 1.0) _phase -= 1.0;

        float x = _synth.VectorX;
        float y = _synth.VectorY;

        float gainA = (1f - x) * (1f - y);
        float gainB = x * (1f - y);
        float gainC = (1f - x) * y;
        float gainD = x * y;

        float mixedSample = sequenceSamples[0] * gainA +
                           sequenceSamples[1] * gainB +
                           sequenceSamples[2] * gainC +
                           sequenceSamples[3] * gainD;

        float mixedPan = sequencePans[0] * gainA +
                        sequencePans[1] * gainB +
                        sequencePans[2] * gainC +
                        sequencePans[3] * gainD;

        float cutoff = _synth.FilterCutoff;
        cutoff += (float)filterEnv * _synth.FilterEnvAmount;
        cutoff = Math.Clamp(cutoff, 0f, 1f);

        if (cutoff < 0.99f)
        {
            float freq = 20f * MathF.Pow(1000f, cutoff);
            freq = MathF.Min(freq, _sampleRate * 0.45f);

            float rc = 1f / (2f * MathF.PI * freq);
            float dt = 1f / _sampleRate;
            float alpha = dt / (rc + dt);

            mixedSample += (mixedSample - _filterState1) * _synth.FilterResonance * 0.5f;
            _filterState1 += alpha * (mixedSample - _filterState1);
            mixedSample = _filterState1;
        }

        float velocityGain = Velocity / 127f;
        mixedSample *= (float)(ampEnv * velocityGain);

        float leftGain = mixedPan <= 0 ? 1f : 1f - mixedPan;
        float rightGain = mixedPan >= 0 ? 1f : 1f + mixedPan;

        return (mixedSample * leftGain, mixedSample * rightGain);
    }

    private void UpdateSequence(int seqIdx, double deltaTime, float speedMod)
    {
        var seq = GetSequence(seqIdx);
        if (seq == null || seq.Steps.Count == 0) return;

        var currentStep = seq.Steps[_currentSteps[seqIdx]];

        double stepDuration;
        if (seq.DurationMode == WaveSeqDurationMode.Beats)
        {
            double beatsPerSecond = _synth.Tempo / 60.0;
            stepDuration = currentStep.Duration / beatsPerSecond / speedMod;
        }
        else
        {
            stepDuration = currentStep.Duration / 1000.0 / speedMod;
        }

        if (_crossfadeProgress[seqIdx] < 1f)
        {
            float crossfadeDuration = stepDuration > 0 ? (float)(currentStep.CrossfadePercent / 100f * stepDuration) : 0f;
            if (crossfadeDuration > 0)
            {
                _crossfadeProgress[seqIdx] += (float)(deltaTime / crossfadeDuration);
            }
            else
            {
                _crossfadeProgress[seqIdx] = 1f;
            }
        }

        _stepTimes[seqIdx] += deltaTime;

        if (_stepTimes[seqIdx] >= stepDuration)
        {
            _stepTimes[seqIdx] -= stepDuration;

            _prevSteps[seqIdx] = currentStep.Clone();
            _crossfadeProgress[seqIdx] = 0f;

            AdvanceStep(seqIdx, seq);
        }
    }

    private void AdvanceStep(int seqIdx, WaveSequence seq)
    {
        int loopStart = Math.Clamp(seq.LoopStart, 0, seq.Steps.Count - 1);
        int loopEnd = seq.EffectiveLoopEnd;

        switch (seq.LoopMode)
        {
            case WaveSeqLoopMode.Forward:
                _currentSteps[seqIdx]++;
                if (_currentSteps[seqIdx] > loopEnd)
                {
                    _currentSteps[seqIdx] = loopStart;
                }
                break;

            case WaveSeqLoopMode.Reverse:
                _currentSteps[seqIdx]--;
                if (_currentSteps[seqIdx] < loopStart)
                {
                    _currentSteps[seqIdx] = loopEnd;
                }
                break;

            case WaveSeqLoopMode.PingPong:
                _currentSteps[seqIdx] += _pingPongDirections[seqIdx];
                if (_currentSteps[seqIdx] >= loopEnd)
                {
                    _currentSteps[seqIdx] = loopEnd;
                    _pingPongDirections[seqIdx] = -1;
                }
                else if (_currentSteps[seqIdx] <= loopStart)
                {
                    _currentSteps[seqIdx] = loopStart;
                    _pingPongDirections[seqIdx] = 1;
                }
                break;

            case WaveSeqLoopMode.Random:
                int range = loopEnd - loopStart + 1;
                _currentSteps[seqIdx] = loopStart + _random.Next(range);
                break;

            case WaveSeqLoopMode.OneShot:
                if (_currentSteps[seqIdx] < seq.Steps.Count - 1)
                {
                    _currentSteps[seqIdx]++;
                }
                break;
        }

        _currentSteps[seqIdx] = Math.Clamp(_currentSteps[seqIdx], 0, seq.Steps.Count - 1);
    }

    private float GenerateWaveform(WaveSeqStep step, float phase)
    {
        return step.Waveform switch
        {
            WaveSeqWaveform.Sine => MathF.Sin(phase * 2f * MathF.PI),
            WaveSeqWaveform.Saw => 2f * phase - 1f,
            WaveSeqWaveform.Square => phase < 0.5f ? 1f : -1f,
            WaveSeqWaveform.Triangle => phase < 0.5f ? (4f * phase - 1f) : (3f - 4f * phase),
            WaveSeqWaveform.CustomWavetable => GetWavetableSample(step, phase),
            _ => MathF.Sin(phase * 2f * MathF.PI)
        };
    }

    private float GetWavetableSample(WaveSeqStep step, float phase)
    {
        if (step.WavetableIndex < 0 || step.WavetableIndex >= _synth.Wavetables.Count)
        {
            return 0f;
        }

        var wavetable = _synth.Wavetables[step.WavetableIndex];
        return wavetable.GetSample(phase, step.WavetablePosition);
    }

    private static float SmoothStep(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}

/// <summary>
/// Korg Wavestation-style wave sequencing synthesizer with vector crossfading.
/// Features up to 64 steps per sequence, 4 wave sequences with XY vector control,
/// per-step waveform/pitch/level/pan, multiple loop modes, tempo sync, and modulation.
/// </summary>
public class WaveSequencerSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly WaveSeqSynthVoice[] _voices;
    private readonly Dictionary<int, int> _noteToVoice = new();
    private readonly object _lock = new();

    /// <summary>Gets or sets the synth name.</summary>
    public string Name { get; set; } = "WaveSequencerSynth";

    /// <summary>Gets the audio format.</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Maximum number of polyphonic voices.</summary>
    public int MaxVoices => _voices.Length;

    /// <summary>Gets the number of currently active voices.</summary>
    public int ActiveVoiceCount
    {
        get
        {
            lock (_lock)
            {
                int count = 0;
                foreach (var voice in _voices)
                {
                    if (voice.IsActive) count++;
                }
                return count;
            }
        }
    }

    /// <summary>Voice stealing mode.</summary>
    public VoiceStealMode StealMode { get; set; } = VoiceStealMode.Oldest;

    /// <summary>Wave sequence A (top-left in vector grid).</summary>
    public WaveSequence SequenceA { get; } = new("Sequence A");

    /// <summary>Wave sequence B (top-right in vector grid).</summary>
    public WaveSequence SequenceB { get; } = new("Sequence B");

    /// <summary>Wave sequence C (bottom-left in vector grid).</summary>
    public WaveSequence SequenceC { get; } = new("Sequence C");

    /// <summary>Wave sequence D (bottom-right in vector grid).</summary>
    public WaveSequence SequenceD { get; } = new("Sequence D");

    /// <summary>Custom wavetables for use in sequences.</summary>
    public List<WaveSeqWavetable> Wavetables { get; } = new();

    private float _vectorX = 0f;
    private float _vectorY = 0f;

    /// <summary>Vector X position (0-1). 0 = left (A/C), 1 = right (B/D).</summary>
    public float VectorX
    {
        get => _vectorX;
        set => _vectorX = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Vector Y position (0-1). 0 = top (A/B), 1 = bottom (C/D).</summary>
    public float VectorY
    {
        get => _vectorY;
        set => _vectorY = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Tempo in BPM for beat-synced duration mode.</summary>
    public float Tempo { get; set; } = 120f;

    /// <summary>Whether to sync sequences to external tempo.</summary>
    public bool TempoSync { get; set; } = true;

    /// <summary>Amplitude envelope attack time in seconds.</summary>
    public double AmpAttack { get; set; } = 0.01;

    /// <summary>Amplitude envelope decay time in seconds.</summary>
    public double AmpDecay { get; set; } = 0.1;

    /// <summary>Amplitude envelope sustain level (0-1).</summary>
    public double AmpSustain { get; set; } = 0.8;

    /// <summary>Amplitude envelope release time in seconds.</summary>
    public double AmpRelease { get; set; } = 0.3;

    /// <summary>Filter cutoff frequency (0-1).</summary>
    public float FilterCutoff { get; set; } = 1.0f;

    /// <summary>Filter resonance (0-1).</summary>
    public float FilterResonance { get; set; } = 0f;

    /// <summary>Filter envelope attack time in seconds.</summary>
    public double FilterEnvAttack { get; set; } = 0.01;

    /// <summary>Filter envelope decay time in seconds.</summary>
    public double FilterEnvDecay { get; set; } = 0.2;

    /// <summary>Filter envelope sustain level (0-1).</summary>
    public double FilterEnvSustain { get; set; } = 0.5;

    /// <summary>Filter envelope release time in seconds.</summary>
    public double FilterEnvRelease { get; set; } = 0.3;

    /// <summary>Filter envelope modulation amount (-1 to 1).</summary>
    public float FilterEnvAmount { get; set; } = 0f;

    /// <summary>LFO for modulating sequence speed.</summary>
    public LFO? SpeedLFO { get; set; }

    /// <summary>Depth of speed LFO modulation (0-1).</summary>
    public float SpeedLFODepth { get; set; } = 0f;

    private float _modWheel = 0f;

    /// <summary>Current mod wheel position (0-1).</summary>
    public float ModWheel
    {
        get => _modWheel;
        set => _modWheel = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Master volume (0-1).</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>
    /// Creates a new WaveSequencerSynth.
    /// </summary>
    public WaveSequencerSynth(int maxVoices = 8, int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        _voices = new WaveSeqSynthVoice[maxVoices];
        for (int i = 0; i < maxVoices; i++)
        {
            _voices[i] = new WaveSeqSynthVoice(rate, this);
        }

        InitializeDefaultWavetables();
        InitializeDefaultSequences();
    }

    private void InitializeDefaultWavetables()
    {
        var basicTable = new WaveSeqWavetable("Basic Waveforms");
        basicTable.AddSineWave();
        basicTable.AddTriangleWave();
        basicTable.AddSawWave();
        basicTable.AddSquareWave();
        Wavetables.Add(basicTable);

        var pwmTable = new WaveSeqWavetable("PWM");
        for (int i = 1; i <= 8; i++)
        {
            var frame = new float[WaveSeqWavetable.FrameSize];
            float dutyCycle = i / 9f;
            int threshold = (int)(WaveSeqWavetable.FrameSize * dutyCycle);
            for (int j = 0; j < WaveSeqWavetable.FrameSize; j++)
            {
                frame[j] = j < threshold ? 1f : -1f;
            }
            pwmTable.Frames.Add(frame);
        }
        Wavetables.Add(pwmTable);
    }

    private void InitializeDefaultSequences()
    {
        SequenceA.Clear();
        SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Sine, 0.25f) { CrossfadePercent = 50 });
        SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Sine, 0.25f) { PitchOffset = 7, CrossfadePercent = 50 });
        SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Sine, 0.25f) { PitchOffset = 12, CrossfadePercent = 50 });
        SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Sine, 0.25f) { PitchOffset = 7, CrossfadePercent = 50 });

        SequenceB.Clear();
        SequenceB.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.5f) { Level = 1f });
        SequenceB.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.5f) { Level = 0.7f, Pan = -0.5f });
        SequenceB.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.5f) { Level = 0.5f, Pan = 0.5f });
        SequenceB.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.5f) { Level = 0.7f });

        SequenceC.Clear();
        for (int i = 0; i < 8; i++)
        {
            SequenceC.AddStep(new WaveSeqStep(WaveSeqWaveform.Square, 0.125f)
            {
                PitchOffset = i * 2 - 7,
                CrossfadePercent = 75
            });
        }

        SequenceD.Clear();
        SequenceD.AddStep(new WaveSeqStep(WaveSeqWaveform.Sine, 0.25f));
        SequenceD.AddStep(new WaveSeqStep(WaveSeqWaveform.Triangle, 0.25f));
        SequenceD.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.25f));
        SequenceD.AddStep(new WaveSeqStep(WaveSeqWaveform.Square, 0.25f));
    }

    /// <summary>Triggers a note.</summary>
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
            if (_noteToVoice.TryGetValue(note, out int existingVoice))
            {
                _voices[existingVoice].Trigger(note, velocity, _modWheel);
                return;
            }

            int voiceIndex = FindFreeVoice(note);
            if (voiceIndex < 0) return;

            int? oldNote = null;
            foreach (var kvp in _noteToVoice)
            {
                if (kvp.Value == voiceIndex)
                {
                    oldNote = kvp.Key;
                    break;
                }
            }
            if (oldNote.HasValue)
            {
                _noteToVoice.Remove(oldNote.Value);
            }

            _voices[voiceIndex].Trigger(note, velocity, _modWheel);
            _noteToVoice[note] = voiceIndex;
        }
    }

    /// <summary>Releases a note.</summary>
    public void NoteOff(int note)
    {
        MidiValidation.ValidateNote(note);

        lock (_lock)
        {
            if (_noteToVoice.TryGetValue(note, out int voiceIndex))
            {
                _voices[voiceIndex].Release();
                _noteToVoice.Remove(note);
            }
        }
    }

    /// <summary>Releases all notes.</summary>
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

    /// <summary>Sets a parameter by name.</summary>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume":
                Volume = Math.Clamp(value, 0f, 1f);
                break;
            case "vectorx":
            case "x":
                VectorX = value;
                break;
            case "vectory":
            case "y":
                VectorY = value;
                break;
            case "tempo":
            case "bpm":
                Tempo = Math.Clamp(value, 20f, 300f);
                break;
            case "temposync":
                TempoSync = value > 0.5f;
                break;
            case "attack":
            case "ampattack":
                AmpAttack = Math.Max(0.001, value);
                break;
            case "decay":
            case "ampdecay":
                AmpDecay = Math.Max(0.001, value);
                break;
            case "sustain":
            case "ampsustain":
                AmpSustain = Math.Clamp(value, 0, 1);
                break;
            case "release":
            case "amprelease":
                AmpRelease = Math.Max(0.001, value);
                break;
            case "filtercutoff":
            case "cutoff":
                FilterCutoff = Math.Clamp(value, 0f, 1f);
                break;
            case "filterresonance":
            case "resonance":
                FilterResonance = Math.Clamp(value, 0f, 1f);
                break;
            case "filterenvattack":
                FilterEnvAttack = Math.Max(0.001, value);
                break;
            case "filterenvdecay":
                FilterEnvDecay = Math.Max(0.001, value);
                break;
            case "filterenvsustain":
                FilterEnvSustain = Math.Clamp(value, 0, 1);
                break;
            case "filterenvrelease":
                FilterEnvRelease = Math.Max(0.001, value);
                break;
            case "filterenvamount":
                FilterEnvAmount = Math.Clamp(value, -1f, 1f);
                break;
            case "speedlfodepth":
                SpeedLFODepth = Math.Clamp(value, 0f, 1f);
                break;
            case "modwheel":
                ModWheel = value;
                break;
        }
    }

    /// <summary>Reads audio samples.</summary>
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
                    if (!voice.IsActive) continue;

                    var (left, right) = voice.Process(deltaTime);
                    leftSum += left;
                    rightSum += right;
                }

                leftSum *= Volume;
                rightSum *= Volume;

                leftSum = MathF.Tanh(leftSum);
                rightSum = MathF.Tanh(rightSum);

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

    private int FindFreeVoice(int newNote)
    {
        for (int i = 0; i < _voices.Length; i++)
        {
            if (!_voices[i].IsActive)
            {
                return i;
            }
        }

        return StealMode switch
        {
            VoiceStealMode.None => -1,
            VoiceStealMode.Oldest => FindOldestVoice(),
            VoiceStealMode.Quietest => FindQuietestVoice(),
            VoiceStealMode.Lowest => FindLowestVoice(),
            VoiceStealMode.Highest => FindHighestVoice(),
            VoiceStealMode.SameNote => FindSameNoteVoice(newNote),
            _ => -1
        };
    }

    private int FindOldestVoice()
    {
        int oldest = 0;
        DateTime oldestTime = _voices[0].TriggerTime;

        for (int i = 1; i < _voices.Length; i++)
        {
            if (_voices[i].TriggerTime < oldestTime)
            {
                oldest = i;
                oldestTime = _voices[i].TriggerTime;
            }
        }

        return oldest;
    }

    private int FindQuietestVoice()
    {
        int quietest = 0;
        double quietestAmp = _voices[0].CurrentAmplitude;

        for (int i = 1; i < _voices.Length; i++)
        {
            if (_voices[i].CurrentAmplitude < quietestAmp)
            {
                quietest = i;
                quietestAmp = _voices[i].CurrentAmplitude;
            }
        }

        return quietest;
    }

    private int FindLowestVoice()
    {
        int lowest = 0;
        int lowestNote = _voices[0].Note;

        for (int i = 1; i < _voices.Length; i++)
        {
            if (_voices[i].Note < lowestNote)
            {
                lowest = i;
                lowestNote = _voices[i].Note;
            }
        }

        return lowest;
    }

    private int FindHighestVoice()
    {
        int highest = 0;
        int highestNote = _voices[0].Note;

        for (int i = 1; i < _voices.Length; i++)
        {
            if (_voices[i].Note > highestNote)
            {
                highest = i;
                highestNote = _voices[i].Note;
            }
        }

        return highest;
    }

    private int FindSameNoteVoice(int note)
    {
        for (int i = 0; i < _voices.Length; i++)
        {
            if (_voices[i].Note == note)
            {
                return i;
            }
        }

        return FindOldestVoice();
    }

    #region Presets

    /// <summary>Creates a classic Wavestation-style pad preset.</summary>
    public static WaveSequencerSynth CreateWavestationPad()
    {
        var synth = new WaveSequencerSynth { Name = "Wavestation Pad" };

        synth.AmpAttack = 0.5;
        synth.AmpDecay = 0.5;
        synth.AmpSustain = 0.9;
        synth.AmpRelease = 1.5;

        synth.FilterCutoff = 0.6f;
        synth.FilterEnvAmount = 0.3f;
        synth.FilterEnvAttack = 0.3;
        synth.FilterEnvDecay = 0.5;
        synth.FilterEnvSustain = 0.4;

        synth.Tempo = 30f;

        synth.VectorX = 0.5f;
        synth.VectorY = 0.5f;

        synth.SequenceA.Clear();
        synth.SequenceA.LoopMode = WaveSeqLoopMode.Forward;
        for (int i = 0; i < 8; i++)
        {
            synth.SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Sine, 2f)
            {
                PitchOffset = (i % 4) * 3,
                CrossfadePercent = 80
            });
        }

        synth.SequenceB.Clear();
        synth.SequenceB.LoopMode = WaveSeqLoopMode.PingPong;
        for (int i = 0; i < 6; i++)
        {
            synth.SequenceB.AddStep(new WaveSeqStep(WaveSeqWaveform.Triangle, 3f)
            {
                Level = 0.5f + i * 0.1f,
                Pan = (i - 2.5f) * 0.3f,
                CrossfadePercent = 90
            });
        }

        return synth;
    }

    /// <summary>Creates a rhythmic sequence preset.</summary>
    public static WaveSequencerSynth CreateRhythmicSequence()
    {
        var synth = new WaveSequencerSynth { Name = "Rhythmic Sequence" };

        synth.AmpAttack = 0.005;
        synth.AmpDecay = 0.15;
        synth.AmpSustain = 0.3;
        synth.AmpRelease = 0.1;

        synth.FilterCutoff = 0.8f;
        synth.FilterResonance = 0.3f;

        synth.Tempo = 140f;

        synth.VectorX = 0f;
        synth.VectorY = 0f;

        synth.SequenceA.Clear();
        synth.SequenceA.LoopMode = WaveSeqLoopMode.Forward;
        synth.SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.125f) { Level = 1f });
        synth.SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.125f) { Level = 0.5f, Enabled = false });
        synth.SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.125f) { Level = 0.7f });
        synth.SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.125f) { Level = 0.3f });
        synth.SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Square, 0.125f) { Level = 0.9f, PitchOffset = 12 });
        synth.SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.125f) { Level = 0.4f });
        synth.SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.125f) { Level = 0.6f });
        synth.SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Square, 0.125f) { Level = 0.8f, PitchOffset = 7 });

        return synth;
    }

    /// <summary>Creates a vector sweep preset that moves through all 4 sequences.</summary>
    public static WaveSequencerSynth CreateVectorSweep()
    {
        var synth = new WaveSequencerSynth { Name = "Vector Sweep" };

        synth.AmpAttack = 0.2;
        synth.AmpDecay = 0.3;
        synth.AmpSustain = 0.7;
        synth.AmpRelease = 0.8;

        synth.Tempo = 80f;

        synth.VectorX = 0f;
        synth.VectorY = 0f;

        synth.SequenceA.Clear();
        synth.SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Sine, 1f) { CrossfadePercent = 50 });
        synth.SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Sine, 1f) { PitchOffset = 5, CrossfadePercent = 50 });

        synth.SequenceB.Clear();
        synth.SequenceB.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.5f) { CrossfadePercent = 30 });
        synth.SequenceB.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.5f) { PitchOffset = -12, CrossfadePercent = 30 });
        synth.SequenceB.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.5f) { PitchOffset = 12, CrossfadePercent = 30 });

        synth.SequenceC.Clear();
        synth.SequenceC.AddStep(new WaveSeqStep(WaveSeqWaveform.Square, 0.75f) { Level = 0.8f });
        synth.SequenceC.AddStep(new WaveSeqStep(WaveSeqWaveform.Square, 0.75f) { Level = 0.6f, PitchOffset = 7 });

        synth.SequenceD.Clear();
        synth.SequenceD.AddStep(new WaveSeqStep(WaveSeqWaveform.Triangle, 0.25f) { CrossfadePercent = 100 });
        synth.SequenceD.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.25f) { CrossfadePercent = 100 });
        synth.SequenceD.AddStep(new WaveSeqStep(WaveSeqWaveform.Square, 0.25f) { CrossfadePercent = 100 });
        synth.SequenceD.AddStep(new WaveSeqStep(WaveSeqWaveform.Sine, 0.25f) { CrossfadePercent = 100 });

        return synth;
    }

    /// <summary>Creates a random/chaotic texture preset.</summary>
    public static WaveSequencerSynth CreateRandomTexture()
    {
        var synth = new WaveSequencerSynth { Name = "Random Texture" };

        synth.AmpAttack = 0.1;
        synth.AmpDecay = 0.2;
        synth.AmpSustain = 0.6;
        synth.AmpRelease = 0.5;

        synth.FilterCutoff = 0.5f;
        synth.FilterResonance = 0.2f;

        synth.Tempo = 100f;

        synth.VectorX = 0.5f;
        synth.VectorY = 0.5f;

        var waveforms = new[] { WaveSeqWaveform.Sine, WaveSeqWaveform.Triangle, WaveSeqWaveform.Saw, WaveSeqWaveform.Square };
        var random = new Random(42);

        foreach (var seq in new[] { synth.SequenceA, synth.SequenceB, synth.SequenceC, synth.SequenceD })
        {
            seq.Clear();
            seq.LoopMode = WaveSeqLoopMode.Random;

            for (int i = 0; i < 16; i++)
            {
                seq.AddStep(new WaveSeqStep
                {
                    Waveform = waveforms[random.Next(4)],
                    Duration = 0.125f + (float)random.NextDouble() * 0.375f,
                    PitchOffset = random.Next(-12, 13),
                    Level = 0.3f + (float)random.NextDouble() * 0.7f,
                    Pan = (float)(random.NextDouble() * 2 - 1) * 0.6f,
                    CrossfadePercent = random.Next(0, 80)
                });
            }
        }

        return synth;
    }

    /// <summary>Creates an arpeggio-style preset with velocity modulating start position.</summary>
    public static WaveSequencerSynth CreateArpeggioSequence()
    {
        var synth = new WaveSequencerSynth { Name = "Arpeggio Sequence" };

        synth.AmpAttack = 0.003;
        synth.AmpDecay = 0.2;
        synth.AmpSustain = 0.2;
        synth.AmpRelease = 0.15;

        synth.FilterCutoff = 0.3f;
        synth.FilterEnvAmount = 0.5f;
        synth.FilterEnvAttack = 0.01;
        synth.FilterEnvDecay = 0.15;
        synth.FilterEnvSustain = 0.1;

        synth.Tempo = 160f;

        synth.VectorX = 0f;
        synth.VectorY = 0f;

        synth.SequenceA.Clear();
        synth.SequenceA.VelocityStartMod = 0.5f;
        synth.SequenceA.GateMode = true;

        int[] pitches = { 0, 4, 7, 12, 16, 19, 24, 19, 16, 12, 7, 4 };
        foreach (int pitch in pitches)
        {
            synth.SequenceA.AddStep(new WaveSeqStep(WaveSeqWaveform.Saw, 0.125f)
            {
                PitchOffset = pitch,
                Level = 0.8f,
                CrossfadePercent = 10
            });
        }

        return synth;
    }

    #endregion
}
