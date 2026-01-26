// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Collections.Generic;
using NAudio.Wave;


namespace MusicEngine.Core;


/// <summary>
/// Vector oscillator waveform types
/// </summary>
public enum VectorWaveform
{
    /// <summary>Pure sine wave</summary>
    Sine,
    /// <summary>Sawtooth wave</summary>
    Saw,
    /// <summary>Square wave</summary>
    Square,
    /// <summary>Triangle wave</summary>
    Triangle,
    /// <summary>White noise</summary>
    Noise
}


/// <summary>
/// Configuration for a single oscillator in the vector grid
/// </summary>
public class VectorOscillator
{
    /// <summary>Waveform type for this oscillator</summary>
    public VectorWaveform Waveform { get; set; } = VectorWaveform.Saw;

    /// <summary>Detune amount in cents (-100 to +100)</summary>
    public float Detune { get; set; } = 0f;

    /// <summary>Octave offset (-2 to +2)</summary>
    public int Octave { get; set; } = 0;

    /// <summary>Level/volume (0-1)</summary>
    public float Level { get; set; } = 1.0f;

    /// <summary>
    /// Creates a new vector oscillator with default settings
    /// </summary>
    public VectorOscillator() { }

    /// <summary>
    /// Creates a new vector oscillator with specified settings
    /// </summary>
    public VectorOscillator(VectorWaveform waveform, float detune = 0f, int octave = 0, float level = 1.0f)
    {
        Waveform = waveform;
        Detune = detune;
        Octave = octave;
        Level = level;
    }
}


/// <summary>
/// Vector envelope point for X/Y automation over time
/// </summary>
public class VectorEnvelopePoint
{
    /// <summary>Time position in seconds from note trigger</summary>
    public double Time { get; set; }

    /// <summary>X position (0-1)</summary>
    public float X { get; set; }

    /// <summary>Y position (0-1)</summary>
    public float Y { get; set; }

    public VectorEnvelopePoint(double time, float x, float y)
    {
        Time = time;
        X = Math.Clamp(x, 0f, 1f);
        Y = Math.Clamp(y, 0f, 1f);
    }
}


/// <summary>
/// Vector envelope for automating X/Y position over time
/// </summary>
public class VectorEnvelope
{
    private readonly List<VectorEnvelopePoint> _points = new();
    private double _currentTime;
    private bool _enabled;

    /// <summary>Whether the vector envelope is enabled</summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>Gets the envelope points</summary>
    public IReadOnlyList<VectorEnvelopePoint> Points => _points;

    /// <summary>
    /// Attack position (start point at time 0)
    /// </summary>
    public (float X, float Y) AttackPosition
    {
        get => _points.Count > 0 ? (_points[0].X, _points[0].Y) : (0.5f, 0.5f);
        set
        {
            if (_points.Count == 0)
                _points.Add(new VectorEnvelopePoint(0, value.X, value.Y));
            else
            {
                _points[0].X = value.X;
                _points[0].Y = value.Y;
                _points[0].Time = 0;
            }
        }
    }

    /// <summary>
    /// Sustain position (final position when envelope completes)
    /// </summary>
    public (float X, float Y) SustainPosition
    {
        get => _points.Count > 1 ? (_points[^1].X, _points[^1].Y) : AttackPosition;
        set
        {
            if (_points.Count == 0)
            {
                _points.Add(new VectorEnvelopePoint(0, 0.5f, 0.5f));
                _points.Add(new VectorEnvelopePoint(1.0, value.X, value.Y));
            }
            else if (_points.Count == 1)
            {
                _points.Add(new VectorEnvelopePoint(1.0, value.X, value.Y));
            }
            else
            {
                _points[^1].X = value.X;
                _points[^1].Y = value.Y;
            }
        }
    }

    /// <summary>
    /// Time in seconds from attack to sustain position
    /// </summary>
    public double EnvelopeTime
    {
        get => _points.Count > 1 ? _points[^1].Time : 0;
        set
        {
            if (_points.Count > 1)
                _points[^1].Time = Math.Max(0, value);
        }
    }

    /// <summary>
    /// Creates a default vector envelope
    /// </summary>
    public VectorEnvelope()
    {
        // Default: center position, no movement
        _points.Add(new VectorEnvelopePoint(0, 0.5f, 0.5f));
        _points.Add(new VectorEnvelopePoint(1.0, 0.5f, 0.5f));
        _enabled = false;
    }

    /// <summary>
    /// Creates a vector envelope with attack and sustain positions
    /// </summary>
    public VectorEnvelope(float attackX, float attackY, float sustainX, float sustainY, double time)
    {
        _points.Add(new VectorEnvelopePoint(0, attackX, attackY));
        _points.Add(new VectorEnvelopePoint(time, sustainX, sustainY));
        _enabled = true;
    }

    /// <summary>
    /// Add an intermediate point to the envelope
    /// </summary>
    public void AddPoint(double time, float x, float y)
    {
        var point = new VectorEnvelopePoint(time, x, y);

        // Insert in time order
        int insertIndex = _points.Count;
        for (int i = 0; i < _points.Count; i++)
        {
            if (_points[i].Time > time)
            {
                insertIndex = i;
                break;
            }
        }

        _points.Insert(insertIndex, point);
    }

    /// <summary>
    /// Clear all points and reset to default
    /// </summary>
    public void Clear()
    {
        _points.Clear();
        _points.Add(new VectorEnvelopePoint(0, 0.5f, 0.5f));
        _points.Add(new VectorEnvelopePoint(1.0, 0.5f, 0.5f));
    }

    /// <summary>
    /// Reset the envelope playback position
    /// </summary>
    public void Reset()
    {
        _currentTime = 0;
    }

    /// <summary>
    /// Process the envelope and get current X/Y position
    /// </summary>
    public (float X, float Y) Process(double deltaTime)
    {
        if (!_enabled || _points.Count == 0)
            return (0.5f, 0.5f);

        _currentTime += deltaTime;

        // Find surrounding points
        VectorEnvelopePoint? prev = null;
        VectorEnvelopePoint? next = null;

        for (int i = 0; i < _points.Count; i++)
        {
            if (_points[i].Time <= _currentTime)
            {
                prev = _points[i];
            }
            else
            {
                next = _points[i];
                break;
            }
        }

        // If past the end, return sustain position
        if (next == null)
        {
            return (_points[^1].X, _points[^1].Y);
        }

        // If before start, return attack position
        if (prev == null)
        {
            return (_points[0].X, _points[0].Y);
        }

        // Interpolate between points
        double segmentDuration = next.Time - prev.Time;
        if (segmentDuration <= 0)
        {
            return (prev.X, prev.Y);
        }

        double t = (_currentTime - prev.Time) / segmentDuration;
        t = Math.Clamp(t, 0, 1);

        // Use smooth interpolation (cosine)
        double smoothT = (1.0 - Math.Cos(t * Math.PI)) / 2.0;

        float x = (float)(prev.X + (next.X - prev.X) * smoothT);
        float y = (float)(prev.Y + (next.Y - prev.Y) * smoothT);

        return (x, y);
    }
}


/// <summary>
/// Internal voice for vector synth
/// </summary>
internal class VectorVoice
{
    private readonly int _sampleRate;
    private readonly VectorSynth _synth;
    private readonly Envelope _ampEnv;
    private readonly VectorEnvelope _vectorEnv;
    private readonly double[] _phases = new double[4]; // Phase for each oscillator
    private readonly Random _noiseRandom = new();

    // Filter state
    private float _filterState1;
    private float _filterState2;

    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public double BaseFrequency { get; private set; }
    public DateTime TriggerTime { get; private set; }
    public bool IsActive => _ampEnv.IsActive;
    public bool IsReleasing => _ampEnv.Stage == EnvelopeStage.Release;
    public double CurrentAmplitude => _ampEnv.Value * (Velocity / 127.0);

    public VectorVoice(int sampleRate, VectorSynth synth)
    {
        _sampleRate = sampleRate;
        _synth = synth;
        _ampEnv = new Envelope(0.01, 0.1, 0.7, 0.3);
        _vectorEnv = new VectorEnvelope();
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        BaseFrequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);
        TriggerTime = DateTime.Now;

        // Copy envelope settings from synth
        _ampEnv.Attack = _synth.Attack;
        _ampEnv.Decay = _synth.Decay;
        _ampEnv.Sustain = _synth.Sustain;
        _ampEnv.Release = _synth.Release;

        // Copy vector envelope settings
        _vectorEnv.Clear();
        _vectorEnv.AttackPosition = _synth.VectorEnvelope.AttackPosition;
        _vectorEnv.SustainPosition = _synth.VectorEnvelope.SustainPosition;
        _vectorEnv.EnvelopeTime = _synth.VectorEnvelope.EnvelopeTime;
        _vectorEnv.Enabled = _synth.VectorEnvelope.Enabled;

        // Reset phases
        for (int i = 0; i < 4; i++)
        {
            _phases[i] = 0;
        }

        // Reset filter
        _filterState1 = 0;
        _filterState2 = 0;

        // Reset envelopes
        _vectorEnv.Reset();
        _ampEnv.Trigger(velocity);
    }

    public void Release()
    {
        _ampEnv.Release_Gate();
    }

    public void Reset()
    {
        Note = -1;
        Velocity = 0;
        for (int i = 0; i < 4; i++)
        {
            _phases[i] = 0;
        }
        _ampEnv.Reset();
        _vectorEnv.Reset();
    }

    public (float left, float right) Process(double deltaTime)
    {
        if (!IsActive) return (0f, 0f);

        double ampEnv = _ampEnv.Process(deltaTime);
        if (_ampEnv.Stage == EnvelopeStage.Idle) return (0f, 0f);

        // Get vector position (from envelope or manual)
        float vectorX, vectorY;
        if (_synth.VectorEnvelope.Enabled)
        {
            var envPos = _vectorEnv.Process(deltaTime);
            vectorX = envPos.X;
            vectorY = envPos.Y;
        }
        else
        {
            vectorX = _synth.VectorX;
            vectorY = _synth.VectorY;
        }

        // Calculate crossfade gains for each oscillator
        // A (0,0), B (1,0), C (0,1), D (1,1)
        float gainA = (1f - vectorX) * (1f - vectorY);
        float gainB = vectorX * (1f - vectorY);
        float gainC = (1f - vectorX) * vectorY;
        float gainD = vectorX * vectorY;

        // Generate samples from each oscillator
        float sampleA = GenerateOscillatorSample(0, _synth.OscillatorA, deltaTime);
        float sampleB = GenerateOscillatorSample(1, _synth.OscillatorB, deltaTime);
        float sampleC = GenerateOscillatorSample(2, _synth.OscillatorC, deltaTime);
        float sampleD = GenerateOscillatorSample(3, _synth.OscillatorD, deltaTime);

        // Mix oscillators according to vector position
        float mixedSample = sampleA * gainA * _synth.OscillatorA.Level +
                           sampleB * gainB * _synth.OscillatorB.Level +
                           sampleC * gainC * _synth.OscillatorC.Level +
                           sampleD * gainD * _synth.OscillatorD.Level;

        // Apply filter
        float cutoff = _synth.FilterCutoff;
        float resonance = _synth.FilterResonance;

        if (cutoff < 0.99f)
        {
            float freq = 20f * MathF.Pow(1000f, cutoff);
            freq = MathF.Min(freq, _sampleRate * 0.45f);

            float rc = 1f / (2f * MathF.PI * freq);
            float dt = 1f / _sampleRate;
            float a = dt / (rc + dt);

            // State variable filter with resonance
            mixedSample += (mixedSample - _filterState1) * resonance * 0.5f;
            _filterState1 = _filterState1 + a * (mixedSample - _filterState1);
            mixedSample = _filterState1;
        }

        // Apply envelope and velocity
        float envGain = (float)(ampEnv * (Velocity / 127.0));
        mixedSample *= envGain;

        // Output as stereo (centered for now, can add stereo spread later)
        return (mixedSample, mixedSample);
    }

    private float GenerateOscillatorSample(int oscIndex, VectorOscillator osc, double deltaTime)
    {
        // Calculate frequency with octave and detune
        double freq = BaseFrequency * Math.Pow(2.0, osc.Octave);
        freq *= Math.Pow(2.0, osc.Detune / 1200.0);

        // Update phase
        double phaseInc = 2.0 * Math.PI * freq / _sampleRate;
        _phases[oscIndex] += phaseInc;
        if (_phases[oscIndex] >= 2.0 * Math.PI)
            _phases[oscIndex] -= 2.0 * Math.PI;

        double phase = _phases[oscIndex];

        // Generate waveform
        return osc.Waveform switch
        {
            VectorWaveform.Sine => (float)Math.Sin(phase),
            VectorWaveform.Saw => (float)(2.0 * (phase / (2.0 * Math.PI)) - 1.0),
            VectorWaveform.Square => phase < Math.PI ? 1.0f : -1.0f,
            VectorWaveform.Triangle => phase < Math.PI
                ? (float)(2.0 * (phase / Math.PI) - 1.0)
                : (float)(3.0 - 2.0 * (phase / Math.PI)),
            VectorWaveform.Noise => (float)(_noiseRandom.NextDouble() * 2.0 - 1.0),
            _ => 0f
        };
    }
}


/// <summary>
/// Vector Synthesizer with 4 oscillators arranged in a 2D grid.
/// The X/Y position controls crossfading between the oscillators:
/// - X=0, Y=0: 100% Oscillator A
/// - X=1, Y=0: 100% Oscillator B
/// - X=0, Y=1: 100% Oscillator C
/// - X=1, Y=1: 100% Oscillator D
/// - X=0.5, Y=0.5: Equal mix of all 4
/// </summary>
public class VectorSynth : ISynth
{
    private readonly VectorVoice[] _voices;
    private readonly WaveFormat _waveFormat;
    private readonly object _lock = new();
    private readonly Dictionary<int, int> _noteToVoice = new();

    /// <summary>Synth name for identification</summary>
    public string Name { get; set; } = "VectorSynth";

    /// <summary>Audio format</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Maximum number of voices</summary>
    public int MaxVoices => _voices.Length;

    /// <summary>Number of currently active voices</summary>
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

    /// <summary>Voice stealing mode</summary>
    public VoiceStealMode StealMode { get; set; } = VoiceStealMode.Oldest;

    // Vector position (0-1)
    private float _vectorX = 0.5f;
    private float _vectorY = 0.5f;

    /// <summary>
    /// X position in the vector grid (0-1).
    /// 0 = full left (A/C), 1 = full right (B/D)
    /// </summary>
    public float VectorX
    {
        get => _vectorX;
        set => _vectorX = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Y position in the vector grid (0-1).
    /// 0 = top (A/B), 1 = bottom (C/D)
    /// </summary>
    public float VectorY
    {
        get => _vectorY;
        set => _vectorY = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Oscillator A (top-left, X=0, Y=0)</summary>
    public VectorOscillator OscillatorA { get; } = new(VectorWaveform.Saw);

    /// <summary>Oscillator B (top-right, X=1, Y=0)</summary>
    public VectorOscillator OscillatorB { get; } = new(VectorWaveform.Square);

    /// <summary>Oscillator C (bottom-left, X=0, Y=1)</summary>
    public VectorOscillator OscillatorC { get; } = new(VectorWaveform.Triangle);

    /// <summary>Oscillator D (bottom-right, X=1, Y=1)</summary>
    public VectorOscillator OscillatorD { get; } = new(VectorWaveform.Sine);

    /// <summary>Vector envelope for X/Y automation</summary>
    public VectorEnvelope VectorEnvelope { get; } = new();

    /// <summary>Master volume (0-1)</summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>Filter cutoff (0-1)</summary>
    public float FilterCutoff { get; set; } = 1.0f;

    /// <summary>Filter resonance (0-1)</summary>
    public float FilterResonance { get; set; } = 0f;

    // ADSR envelope parameters
    private double _attack = 0.01;
    private double _decay = 0.1;
    private double _sustain = 0.7;
    private double _release = 0.3;

    /// <summary>Amplitude envelope attack time in seconds</summary>
    public double Attack
    {
        get => _attack;
        set => _attack = Math.Max(0.001, value);
    }

    /// <summary>Amplitude envelope decay time in seconds</summary>
    public double Decay
    {
        get => _decay;
        set => _decay = Math.Max(0.001, value);
    }

    /// <summary>Amplitude envelope sustain level (0-1)</summary>
    public double Sustain
    {
        get => _sustain;
        set => _sustain = Math.Clamp(value, 0, 1);
    }

    /// <summary>Amplitude envelope release time in seconds</summary>
    public double Release
    {
        get => _release;
        set => _release = Math.Max(0.001, value);
    }

    /// <summary>
    /// Creates a new vector synthesizer
    /// </summary>
    /// <param name="maxVoices">Maximum polyphony (default 16)</param>
    /// <param name="sampleRate">Sample rate (uses Settings.SampleRate if null)</param>
    public VectorSynth(int maxVoices = 16, int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        _voices = new VectorVoice[maxVoices];
        for (int i = 0; i < maxVoices; i++)
        {
            _voices[i] = new VectorVoice(rate, this);
        }
    }

    /// <summary>
    /// Trigger a note
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateVelocity(velocity);

        lock (_lock)
        {
            // Check if this note is already playing
            if (_noteToVoice.TryGetValue(note, out int existingVoice))
            {
                // Retrigger the same voice
                _voices[existingVoice].Trigger(note, velocity);
                return;
            }

            // Find a free voice
            int voiceIndex = FindFreeVoice(note);
            if (voiceIndex < 0) return;

            // Remove old note mapping if voice was stolen
            int? oldNoteToRemove = null;
            foreach (var kvp in _noteToVoice)
            {
                if (kvp.Value == voiceIndex)
                {
                    oldNoteToRemove = kvp.Key;
                    break;
                }
            }
            if (oldNoteToRemove.HasValue)
            {
                _noteToVoice.Remove(oldNoteToRemove.Value);
            }

            // Trigger the voice
            _voices[voiceIndex].Trigger(note, velocity);
            _noteToVoice[note] = voiceIndex;
        }
    }

    /// <summary>
    /// Release a note
    /// </summary>
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
    /// Set a parameter by name
    /// </summary>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "vectorx":
            case "x":
                VectorX = value;
                break;
            case "vectory":
            case "y":
                VectorY = value;
                break;
            case "volume":
                Volume = Math.Clamp(value, 0f, 1f);
                break;
            case "cutoff":
            case "filtercutoff":
                FilterCutoff = Math.Clamp(value, 0f, 1f);
                break;
            case "resonance":
            case "filterresonance":
                FilterResonance = Math.Clamp(value, 0f, 1f);
                break;
            case "attack":
                Attack = value;
                break;
            case "decay":
                Decay = value;
                break;
            case "sustain":
                Sustain = value;
                break;
            case "release":
                Release = value;
                break;

            // Oscillator A parameters
            case "osca.waveform":
            case "a.waveform":
                OscillatorA.Waveform = (VectorWaveform)(int)Math.Clamp(value, 0, 4);
                break;
            case "osca.detune":
            case "a.detune":
                OscillatorA.Detune = Math.Clamp(value, -100f, 100f);
                break;
            case "osca.octave":
            case "a.octave":
                OscillatorA.Octave = Math.Clamp((int)value, -2, 2);
                break;
            case "osca.level":
            case "a.level":
                OscillatorA.Level = Math.Clamp(value, 0f, 1f);
                break;

            // Oscillator B parameters
            case "oscb.waveform":
            case "b.waveform":
                OscillatorB.Waveform = (VectorWaveform)(int)Math.Clamp(value, 0, 4);
                break;
            case "oscb.detune":
            case "b.detune":
                OscillatorB.Detune = Math.Clamp(value, -100f, 100f);
                break;
            case "oscb.octave":
            case "b.octave":
                OscillatorB.Octave = Math.Clamp((int)value, -2, 2);
                break;
            case "oscb.level":
            case "b.level":
                OscillatorB.Level = Math.Clamp(value, 0f, 1f);
                break;

            // Oscillator C parameters
            case "oscc.waveform":
            case "c.waveform":
                OscillatorC.Waveform = (VectorWaveform)(int)Math.Clamp(value, 0, 4);
                break;
            case "oscc.detune":
            case "c.detune":
                OscillatorC.Detune = Math.Clamp(value, -100f, 100f);
                break;
            case "oscc.octave":
            case "c.octave":
                OscillatorC.Octave = Math.Clamp((int)value, -2, 2);
                break;
            case "oscc.level":
            case "c.level":
                OscillatorC.Level = Math.Clamp(value, 0f, 1f);
                break;

            // Oscillator D parameters
            case "oscd.waveform":
            case "d.waveform":
                OscillatorD.Waveform = (VectorWaveform)(int)Math.Clamp(value, 0, 4);
                break;
            case "oscd.detune":
            case "d.detune":
                OscillatorD.Detune = Math.Clamp(value, -100f, 100f);
                break;
            case "oscd.octave":
            case "d.octave":
                OscillatorD.Octave = Math.Clamp((int)value, -2, 2);
                break;
            case "oscd.level":
            case "d.level":
                OscillatorD.Level = Math.Clamp(value, 0f, 1f);
                break;

            // Vector envelope
            case "vectorenv.enabled":
                VectorEnvelope.Enabled = value > 0.5f;
                break;
            case "vectorenv.time":
                VectorEnvelope.EnvelopeTime = value;
                break;
        }
    }

    /// <summary>
    /// Read audio samples
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        // Clear buffer
        for (int i = 0; i < count; i++)
        {
            buffer[offset + i] = 0;
        }

        int channels = _waveFormat.Channels;
        double deltaTime = 1.0 / _waveFormat.SampleRate;

        lock (_lock)
        {
            for (int n = 0; n < count; n += channels)
            {
                float leftSample = 0f;
                float rightSample = 0f;

                foreach (var voice in _voices)
                {
                    if (!voice.IsActive) continue;

                    var (left, right) = voice.Process(deltaTime);
                    leftSample += left;
                    rightSample += right;
                }

                // Apply volume
                leftSample *= Volume;
                rightSample *= Volume;

                // Soft clipping to prevent harsh distortion
                leftSample = MathF.Tanh(leftSample);
                rightSample = MathF.Tanh(rightSample);

                // Output
                if (channels >= 2)
                {
                    buffer[offset + n] = leftSample;
                    buffer[offset + n + 1] = rightSample;
                }
                else
                {
                    buffer[offset + n] = (leftSample + rightSample) * 0.5f;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Find a free voice or steal one based on StealMode
    /// </summary>
    private int FindFreeVoice(int newNote)
    {
        // First, look for an inactive voice
        for (int i = 0; i < _voices.Length; i++)
        {
            if (!_voices[i].IsActive)
            {
                return i;
            }
        }

        // All voices are active - apply stealing strategy
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

        // Fall back to oldest if no same note found
        return FindOldestVoice();
    }

    /// <summary>
    /// Create a preset: Classic vector pad with sweeping motion
    /// </summary>
    public static VectorSynth CreatePadPreset()
    {
        var synth = new VectorSynth();
        synth.Name = "Vector Pad";

        // Set up oscillators with different timbres
        synth.OscillatorA.Waveform = VectorWaveform.Sine;
        synth.OscillatorA.Octave = 0;

        synth.OscillatorB.Waveform = VectorWaveform.Triangle;
        synth.OscillatorB.Detune = 5f;

        synth.OscillatorC.Waveform = VectorWaveform.Saw;
        synth.OscillatorC.Octave = -1;

        synth.OscillatorD.Waveform = VectorWaveform.Square;
        synth.OscillatorD.Detune = -5f;

        // Slow attack/release for pad sound
        synth.Attack = 0.5;
        synth.Decay = 0.5;
        synth.Sustain = 0.8;
        synth.Release = 1.5;

        // Set up vector envelope to sweep from corner to corner
        synth.VectorEnvelope.AttackPosition = (0.1f, 0.1f);
        synth.VectorEnvelope.SustainPosition = (0.9f, 0.9f);
        synth.VectorEnvelope.EnvelopeTime = 3.0;
        synth.VectorEnvelope.Enabled = true;

        synth.FilterCutoff = 0.7f;

        return synth;
    }

    /// <summary>
    /// Create a preset: Aggressive vector lead
    /// </summary>
    public static VectorSynth CreateLeadPreset()
    {
        var synth = new VectorSynth();
        synth.Name = "Vector Lead";

        // Set up oscillators with bright, cutting timbres
        synth.OscillatorA.Waveform = VectorWaveform.Saw;
        synth.OscillatorA.Octave = 0;

        synth.OscillatorB.Waveform = VectorWaveform.Saw;
        synth.OscillatorB.Detune = 7f;

        synth.OscillatorC.Waveform = VectorWaveform.Square;
        synth.OscillatorC.Octave = 0;

        synth.OscillatorD.Waveform = VectorWaveform.Saw;
        synth.OscillatorD.Octave = 1;
        synth.OscillatorD.Detune = -7f;

        // Quick attack for leads
        synth.Attack = 0.01;
        synth.Decay = 0.2;
        synth.Sustain = 0.7;
        synth.Release = 0.2;

        // Start centered
        synth.VectorX = 0.5f;
        synth.VectorY = 0.5f;
        synth.VectorEnvelope.Enabled = false;

        synth.FilterCutoff = 0.85f;
        synth.FilterResonance = 0.2f;

        return synth;
    }

    /// <summary>
    /// Create a preset: Evolving texture
    /// </summary>
    public static VectorSynth CreateTexturePreset()
    {
        var synth = new VectorSynth();
        synth.Name = "Vector Texture";

        // Mix of different timbres
        synth.OscillatorA.Waveform = VectorWaveform.Triangle;
        synth.OscillatorA.Octave = 0;
        synth.OscillatorA.Detune = 3f;

        synth.OscillatorB.Waveform = VectorWaveform.Noise;
        synth.OscillatorB.Level = 0.3f;

        synth.OscillatorC.Waveform = VectorWaveform.Sine;
        synth.OscillatorC.Octave = 1;

        synth.OscillatorD.Waveform = VectorWaveform.Saw;
        synth.OscillatorD.Octave = -1;
        synth.OscillatorD.Detune = -3f;

        // Very slow envelope for texture
        synth.Attack = 1.0;
        synth.Decay = 1.0;
        synth.Sustain = 0.6;
        synth.Release = 2.0;

        // Circular motion in vector space
        synth.VectorEnvelope.Clear();
        synth.VectorEnvelope.AttackPosition = (0f, 0.5f);
        synth.VectorEnvelope.AddPoint(1.0, 0.5f, 0f);
        synth.VectorEnvelope.AddPoint(2.0, 1f, 0.5f);
        synth.VectorEnvelope.AddPoint(3.0, 0.5f, 1f);
        synth.VectorEnvelope.SustainPosition = (0f, 0.5f);
        synth.VectorEnvelope.EnvelopeTime = 4.0;
        synth.VectorEnvelope.Enabled = true;

        synth.FilterCutoff = 0.5f;
        synth.FilterResonance = 0.3f;

        return synth;
    }

    /// <summary>
    /// Create a preset: Bass with character
    /// </summary>
    public static VectorSynth CreateBassPreset()
    {
        var synth = new VectorSynth();
        synth.Name = "Vector Bass";

        // Heavy bass oscillators
        synth.OscillatorA.Waveform = VectorWaveform.Sine;
        synth.OscillatorA.Octave = -1;

        synth.OscillatorB.Waveform = VectorWaveform.Square;
        synth.OscillatorB.Octave = -1;

        synth.OscillatorC.Waveform = VectorWaveform.Saw;
        synth.OscillatorC.Octave = 0;
        synth.OscillatorC.Detune = -10f;

        synth.OscillatorD.Waveform = VectorWaveform.Saw;
        synth.OscillatorD.Octave = 0;
        synth.OscillatorD.Detune = 10f;

        // Punchy envelope
        synth.Attack = 0.005;
        synth.Decay = 0.3;
        synth.Sustain = 0.5;
        synth.Release = 0.15;

        // Vector envelope adds harmonic movement
        synth.VectorEnvelope.AttackPosition = (0.7f, 0.7f);
        synth.VectorEnvelope.SustainPosition = (0.2f, 0.2f);
        synth.VectorEnvelope.EnvelopeTime = 0.3;
        synth.VectorEnvelope.Enabled = true;

        synth.FilterCutoff = 0.4f;
        synth.FilterResonance = 0.4f;

        return synth;
    }
}
