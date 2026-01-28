// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Polyphonic synthesizer with ADSR, filter, LFO, and effects.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Waveform types for oscillators
/// </summary>
public enum WaveType
{
    Sine,
    Square,
    Sawtooth,
    Triangle,
    Pulse,
    Noise
}

/// <summary>
/// Polyphonic synthesizer with extensive sound design capabilities.
/// Features: Dual oscillators, ADSR envelopes, filter, LFO, and built-in effects.
/// Uses lock-free data structures for minimal MIDI latency.
/// </summary>
public class SimpleSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly ConcurrentDictionary<int, Voice> _voices = new(); // Key = MIDI note (lock-free)
    private readonly ConcurrentQueue<Voice> _voicesToRelease = new(); // Queue for voices to move to releasing
    private readonly List<Voice> _releasingVoices = new(); // Voices in release phase (only accessed by audio thread)
    private readonly object _releaseLock = new(); // Only for releasing voices list
    private int _activeVoiceCount; // Atomic counter for polyphony

    // LFO state
    private float _lfoPhase;

    // Effect buffers
    private readonly float[] _delayBuffer;
    private int _delayWritePos;
    private readonly float[] _reverbBuffer;
    private int _reverbWritePos;
    private const int MaxDelaySamples = 96000;
    private const int ReverbBufferSize = 44100;

    #region ========== OSCILLATOR 1 SETTINGS ==========

    /// <summary>Oscillator 1 waveform type</summary>
    public WaveType Waveform { get; set; } = WaveType.Sawtooth;

    /// <summary>Oscillator 1 octave offset (-3 to +3)</summary>
    public int Osc1Octave { get; set; } = 0;

    /// <summary>Oscillator 1 semitone detune (-12 to +12)</summary>
    public int Osc1Semi { get; set; } = 0;

    /// <summary>Oscillator 1 fine tune in cents (-100 to +100)</summary>
    public float Osc1Fine { get; set; } = 0f;

    /// <summary>Oscillator 1 level (0 to 1)</summary>
    public float Osc1Level { get; set; } = 0.7f;

    /// <summary>Oscillator 1 pulse width for pulse wave (0.1 to 0.9)</summary>
    public float Osc1PulseWidth { get; set; } = 0.5f;

    #endregion

    #region ========== OSCILLATOR 2 SETTINGS ==========

    /// <summary>Oscillator 2 waveform type</summary>
    public WaveType Osc2Waveform { get; set; } = WaveType.Sawtooth;

    /// <summary>Oscillator 2 octave offset (-3 to +3)</summary>
    public int Osc2Octave { get; set; } = 0;

    /// <summary>Oscillator 2 semitone detune (-12 to +12)</summary>
    public int Osc2Semi { get; set; } = 0;

    /// <summary>Oscillator 2 fine tune in cents (-100 to +100)</summary>
    public float Osc2Fine { get; set; } = 7f;

    /// <summary>Oscillator 2 level (0 to 1)</summary>
    public float Osc2Level { get; set; } = 0.5f;

    /// <summary>Oscillator 2 pulse width (0.1 to 0.9)</summary>
    public float Osc2PulseWidth { get; set; } = 0.5f;

    /// <summary>Enable oscillator 2</summary>
    public bool Osc2Enabled { get; set; } = true;

    #endregion

    #region ========== SUB OSCILLATOR & NOISE ==========

    /// <summary>Sub oscillator level (0 to 1) - plays one octave below</summary>
    public float SubOscLevel { get; set; } = 0f;

    /// <summary>Sub oscillator waveform (Sine or Square)</summary>
    public WaveType SubOscWaveform { get; set; } = WaveType.Sine;

    /// <summary>Noise level (0 to 1)</summary>
    public float NoiseLevel { get; set; } = 0f;

    #endregion

    #region ========== FILTER SETTINGS ==========

    /// <summary>Filter cutoff frequency normalized (0 to 1, maps to 20-20000 Hz)</summary>
    public float Cutoff { get; set; } = 0.8f;

    /// <summary>Filter resonance (0 to 1)</summary>
    public float Resonance { get; set; } = 0.2f;

    /// <summary>Filter envelope amount (-1 to 1)</summary>
    public float FilterEnvAmount { get; set; } = 0.3f;

    /// <summary>Filter keyboard tracking (0 to 1)</summary>
    public float FilterKeyTrack { get; set; } = 0.5f;

    /// <summary>Filter drive/saturation (0 to 1)</summary>
    public float FilterDrive { get; set; } = 0f;

    #endregion

    #region ========== AMPLITUDE ENVELOPE (ADSR) ==========

    /// <summary>Amplitude attack time in seconds (0.001 to 10)</summary>
    public float Attack { get; set; } = 0.005f;

    /// <summary>Amplitude decay time in seconds (0.001 to 10)</summary>
    public float Decay { get; set; } = 0.2f;

    /// <summary>Amplitude sustain level (0 to 1)</summary>
    public float Sustain { get; set; } = 0.7f;

    /// <summary>Amplitude release time in seconds (0.001 to 10)</summary>
    public float Release { get; set; } = 0.3f;

    #endregion

    #region ========== FILTER ENVELOPE (ADSR) ==========

    /// <summary>Filter envelope attack time</summary>
    public float FilterAttack { get; set; } = 0.005f;

    /// <summary>Filter envelope decay time</summary>
    public float FilterDecay { get; set; } = 0.3f;

    /// <summary>Filter envelope sustain level</summary>
    public float FilterSustain { get; set; } = 0.4f;

    /// <summary>Filter envelope release time</summary>
    public float FilterRelease { get; set; } = 0.3f;

    #endregion

    #region ========== LFO SETTINGS ==========

    /// <summary>LFO rate in Hz (0.01 to 50)</summary>
    public float LfoRate { get; set; } = 5f;

    /// <summary>LFO waveform</summary>
    public WaveType LfoWaveform { get; set; } = WaveType.Sine;

    /// <summary>LFO to pitch amount in semitones (0 to 12)</summary>
    public float LfoToPitch { get; set; } = 0f;

    /// <summary>LFO to filter cutoff amount (0 to 1)</summary>
    public float LfoToFilter { get; set; } = 0f;

    /// <summary>LFO to amplitude amount (0 to 1)</summary>
    public float LfoToAmp { get; set; } = 0f;

    /// <summary>LFO to pulse width amount (0 to 0.4)</summary>
    public float LfoToPulseWidth { get; set; } = 0f;

    #endregion

    #region ========== MODULATION ==========

    /// <summary>Pitch bend value (-1 to 1)</summary>
    public float PitchBend { get; set; } = 0f;

    /// <summary>Pitch bend range in semitones (1 to 24)</summary>
    public int PitchBendRange { get; set; } = 2;

    /// <summary>Mod wheel value (0 to 1) - controls vibrato</summary>
    public float ModWheel { get; set; } = 0f;

    /// <summary>Vibrato rate in Hz</summary>
    public float VibratoRate { get; set; } = 5f;

    /// <summary>Vibrato depth in semitones</summary>
    public float VibratoDepth { get; set; } = 0.3f;

    /// <summary>Portamento time in seconds (0 = off)</summary>
    public float Portamento { get; set; } = 0f;

    #endregion

    #region ========== UNISON ==========

    /// <summary>Unison voices (1 to 8)</summary>
    public int UnisonVoices { get; set; } = 1;

    /// <summary>Unison detune in cents (0 to 50)</summary>
    public float UnisonDetune { get; set; } = 15f;

    /// <summary>Unison stereo spread (0 to 1)</summary>
    public float UnisonSpread { get; set; } = 0.5f;

    #endregion

    #region ========== EFFECTS ==========

    /// <summary>Delay mix (0 to 1)</summary>
    public float DelayMix { get; set; } = 0f;

    /// <summary>Delay time in milliseconds (1 to 2000)</summary>
    public float DelayTime { get; set; } = 300f;

    /// <summary>Delay feedback (0 to 0.95)</summary>
    public float DelayFeedback { get; set; } = 0.4f;

    /// <summary>Reverb mix (0 to 1)</summary>
    public float ReverbMix { get; set; } = 0.15f;

    /// <summary>Reverb size (0 to 1)</summary>
    public float ReverbSize { get; set; } = 0.5f;

    /// <summary>Reverb damping (0 to 1)</summary>
    public float ReverbDamping { get; set; } = 0.5f;

    #endregion

    #region ========== OUTPUT ==========

    /// <summary>Master volume (0 to 1)</summary>
    public float Volume { get; set; } = 0.7f;

    /// <summary>Pan position (-1 left, 0 center, 1 right)</summary>
    public float Pan { get; set; } = 0f;

    /// <summary>Maximum polyphony (1 to 64)</summary>
    public int MaxPolyphony { get; set; } = 16;

    /// <summary>Velocity sensitivity (0 to 1)</summary>
    public float VelocitySensitivity { get; set; } = 0.7f;

    #endregion

    /// <summary>Synth name for identification</summary>
    public string Name { get; set; } = "SimpleSynth";

    /// <summary>Wave format for audio output</summary>
    public WaveFormat WaveFormat => _waveFormat;

    // Track last played note for portamento
    private float _lastNote = 60f;
    private readonly Random _random = new();

    /// <summary>
    /// Creates a new SimpleSynth instance
    /// </summary>
    /// <param name="sampleRate">Optional sample rate override</param>
    public SimpleSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize effect buffers
        _delayBuffer = new float[MaxDelaySamples];
        _reverbBuffer = new float[ReverbBufferSize];
    }

    /// <summary>
    /// Triggers a note on event (lock-free for MIDI thread)
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        // Clamp values instead of throwing exceptions for faster MIDI handling
        note = Math.Clamp(note, 0, 127);
        velocity = Math.Clamp(velocity, 1, 127);

        // Pre-calculate values
        float vel = 1f - VelocitySensitivity + (velocity / 127f) * VelocitySensitivity;
        int sampleRate = _waveFormat.SampleRate;

        // If note is already playing, retrigger it (lock-free)
        if (_voices.TryGetValue(note, out var existingVoice))
        {
            existingVoice.Retrigger(vel);
            return;
        }

        // Voice stealing: if at max polyphony, steal oldest voice
        int currentCount = Interlocked.CompareExchange(ref _activeVoiceCount, 0, 0);
        if (currentCount >= MaxPolyphony)
        {
            // Find oldest voice
            int oldestNote = -1;
            long oldestTime = long.MaxValue;
            foreach (var kvp in _voices)
            {
                if (kvp.Value.StartTime < oldestTime)
                {
                    oldestTime = kvp.Value.StartTime;
                    oldestNote = kvp.Key;
                }
            }

            if (oldestNote >= 0 && _voices.TryRemove(oldestNote, out var stolen))
            {
                Interlocked.Decrement(ref _activeVoiceCount);
                stolen.TriggerRelease();
                _voicesToRelease.Enqueue(stolen);
            }
        }

        // Determine starting note for portamento
        float startNote = Portamento > 0 && !_voices.IsEmpty
            ? _lastNote
            : note;

        // Create new voice and add atomically
        var voice = new Voice(this, note, vel, startNote, sampleRate);
        if (_voices.TryAdd(note, voice))
        {
            Interlocked.Increment(ref _activeVoiceCount);
            _lastNote = note;
        }
    }

    /// <summary>
    /// Triggers a note off event (lock-free for MIDI thread)
    /// </summary>
    public void NoteOff(int note)
    {
        // Clamp instead of validate
        note = Math.Clamp(note, 0, 127);

        // Remove voice atomically
        if (_voices.TryRemove(note, out var voice))
        {
            Interlocked.Decrement(ref _activeVoiceCount);
            voice.TriggerRelease();
            _voicesToRelease.Enqueue(voice);
        }
    }

    /// <summary>
    /// Stops all playing notes immediately (lock-free)
    /// </summary>
    public void AllNotesOff()
    {
        // Move all active voices to release queue
        foreach (var kvp in _voices)
        {
            if (_voices.TryRemove(kvp.Key, out var voice))
            {
                Interlocked.Decrement(ref _activeVoiceCount);
                voice.TriggerRelease();
                _voicesToRelease.Enqueue(voice);
            }
        }
    }

    /// <summary>
    /// Sets a parameter by name
    /// </summary>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            // Oscillator
            case "waveform": Waveform = (WaveType)(int)Math.Clamp(value, 0, 5); break;
            case "osc1octave": Osc1Octave = (int)Math.Clamp(value, -3, 3); break;
            case "osc1semi": Osc1Semi = (int)Math.Clamp(value, -12, 12); break;
            case "osc1fine": Osc1Fine = Math.Clamp(value, -100, 100); break;
            case "osc1level": Osc1Level = Math.Clamp(value, 0, 1); break;
            case "osc1pulsewidth": Osc1PulseWidth = Math.Clamp(value, 0.1f, 0.9f); break;

            case "osc2waveform": Osc2Waveform = (WaveType)(int)Math.Clamp(value, 0, 5); break;
            case "osc2octave": Osc2Octave = (int)Math.Clamp(value, -3, 3); break;
            case "osc2semi": Osc2Semi = (int)Math.Clamp(value, -12, 12); break;
            case "osc2fine": Osc2Fine = Math.Clamp(value, -100, 100); break;
            case "osc2level": Osc2Level = Math.Clamp(value, 0, 1); break;
            case "osc2enabled": Osc2Enabled = value > 0.5f; break;

            case "subosclevel": SubOscLevel = Math.Clamp(value, 0, 1); break;
            case "noiselevel": NoiseLevel = Math.Clamp(value, 0, 1); break;

            // Filter
            case "cutoff": Cutoff = Math.Clamp(value, 0, 1); break;
            case "resonance": Resonance = Math.Clamp(value, 0, 1); break;
            case "filterenvamount": FilterEnvAmount = Math.Clamp(value, -1, 1); break;
            case "filterkeytrack": FilterKeyTrack = Math.Clamp(value, 0, 1); break;
            case "filterdrive": FilterDrive = Math.Clamp(value, 0, 1); break;

            // Amp Envelope
            case "attack": Attack = Math.Clamp(value, 0.001f, 10); break;
            case "decay": Decay = Math.Clamp(value, 0.001f, 10); break;
            case "sustain": Sustain = Math.Clamp(value, 0, 1); break;
            case "release": Release = Math.Clamp(value, 0.001f, 10); break;

            // Filter Envelope
            case "filterattack": FilterAttack = Math.Clamp(value, 0.001f, 10); break;
            case "filterdecay": FilterDecay = Math.Clamp(value, 0.001f, 10); break;
            case "filtersustain": FilterSustain = Math.Clamp(value, 0, 1); break;
            case "filterrelease": FilterRelease = Math.Clamp(value, 0.001f, 10); break;

            // LFO
            case "lforate": LfoRate = Math.Clamp(value, 0.01f, 50); break;
            case "lfowaveform": LfoWaveform = (WaveType)(int)Math.Clamp(value, 0, 4); break;
            case "lfotopitch": LfoToPitch = Math.Clamp(value, 0, 12); break;
            case "lfotofilter": LfoToFilter = Math.Clamp(value, 0, 1); break;
            case "lfotoamp": LfoToAmp = Math.Clamp(value, 0, 1); break;

            // Modulation
            case "pitchbend": PitchBend = Math.Clamp(value, -1, 1); break;
            case "pitchbendrange": PitchBendRange = (int)Math.Clamp(value, 1, 24); break;
            case "modwheel": ModWheel = Math.Clamp(value, 0, 1); break;
            case "portamento": Portamento = Math.Clamp(value, 0, 2); break;

            // Unison
            case "unisonvoices": UnisonVoices = (int)Math.Clamp(value, 1, 8); break;
            case "unisondetune": UnisonDetune = Math.Clamp(value, 0, 50); break;
            case "unisonspread": UnisonSpread = Math.Clamp(value, 0, 1); break;

            // Effects
            case "delaymix": DelayMix = Math.Clamp(value, 0, 1); break;
            case "delaytime": DelayTime = Math.Clamp(value, 1, 2000); break;
            case "delayfeedback": DelayFeedback = Math.Clamp(value, 0, 0.95f); break;
            case "reverbmix": ReverbMix = Math.Clamp(value, 0, 1); break;
            case "reverbsize": ReverbSize = Math.Clamp(value, 0, 1); break;

            // Output
            case "volume": Volume = Math.Clamp(value, 0, 1); break;
            case "pan": Pan = Math.Clamp(value, -1, 1); break;
            case "maxpolyphony": MaxPolyphony = (int)Math.Clamp(value, 1, 64); break;
            case "velocitysensitivity": VelocitySensitivity = Math.Clamp(value, 0, 1); break;
        }
    }

    /// <summary>
    /// Reads audio samples into the buffer (optimized, minimal locking)
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int sampleRate = _waveFormat.SampleRate;
        int channels = _waveFormat.Channels;
        int samples = count / channels;

        // Clear buffer
        Array.Clear(buffer, offset, count);

        // Move voices from release queue to releasing list (only place we need lock)
        while (_voicesToRelease.TryDequeue(out var voiceToRelease))
        {
            lock (_releaseLock)
            {
                _releasingVoices.Add(voiceToRelease);
            }
        }

        // Process LFO
        float lfoIncrement = LfoRate / sampleRate;

        for (int s = 0; s < samples; s++)
        {
            // Calculate LFO value
            float lfoValue = GenerateWaveform(LfoWaveform, _lfoPhase, 0.5f);
            _lfoPhase += lfoIncrement;
            if (_lfoPhase >= 1f) _lfoPhase -= 1f;

            // Calculate vibrato (mod wheel controlled)
            float vibratoPhase = _lfoPhase * VibratoRate / LfoRate;
            float vibrato = (float)Math.Sin(vibratoPhase * Math.PI * 2) * VibratoDepth * ModWheel;

            // Calculate modulations
            float pitchMod = PitchBend * PitchBendRange + lfoValue * LfoToPitch + vibrato;
            float filterMod = lfoValue * LfoToFilter * 0.5f;
            float ampMod = 1f - (lfoValue * 0.5f + 0.5f) * LfoToAmp;
            float pwMod = lfoValue * LfoToPulseWidth;

            float mixL = 0f;
            float mixR = 0f;

            // Process active voices (ConcurrentDictionary is safe to iterate)
            foreach (var voice in _voices.Values)
            {
                var (left, right) = voice.Process(sampleRate, pitchMod, filterMod, pwMod, this);
                mixL += left * ampMod;
                mixR += right * ampMod;
            }

            // Process releasing voices
            lock (_releaseLock)
            {
                for (int i = _releasingVoices.Count - 1; i >= 0; i--)
                {
                    var voice = _releasingVoices[i];
                    if (voice.IsFinished)
                    {
                        _releasingVoices.RemoveAt(i);
                        continue;
                    }
                    var (left, right) = voice.Process(sampleRate, pitchMod, filterMod, pwMod, this);
                    mixL += left * ampMod;
                    mixR += right * ampMod;
                }
            }

            // Apply effects
            float mono = (mixL + mixR) * 0.5f;

            // Delay
            if (DelayMix > 0.001f)
            {
                int delaySamples = Math.Min((int)(DelayTime * sampleRate / 1000f), MaxDelaySamples - 1);
                int readPos = (_delayWritePos - delaySamples + MaxDelaySamples) % MaxDelaySamples;
                float delayed = _delayBuffer[readPos];
                _delayBuffer[_delayWritePos] = mono + delayed * DelayFeedback;
                _delayWritePos = (_delayWritePos + 1) % MaxDelaySamples;

                mixL += delayed * DelayMix;
                mixR += delayed * DelayMix;
            }

            // Simple reverb
            if (ReverbMix > 0.001f)
            {
                int reverbDelay = (int)(ReverbSize * 15000 + 1000);
                int readPos = (_reverbWritePos - reverbDelay + ReverbBufferSize) % ReverbBufferSize;
                float reverbed = _reverbBuffer[readPos];
                _reverbBuffer[_reverbWritePos] = mono + reverbed * (1f - ReverbDamping) * 0.6f;
                _reverbWritePos = (_reverbWritePos + 1) % ReverbBufferSize;

                mixL += reverbed * ReverbMix;
                mixR += reverbed * ReverbMix;
            }

            // Apply pan
            float panL = Math.Min(1f, 1f - Pan);
            float panR = Math.Min(1f, 1f + Pan);

            // Apply master volume
            mixL *= Volume * panL;
            mixR *= Volume * panR;

            // Soft-clip to prevent harsh digital clipping (causes pops)
            mixL = SoftClip(mixL);
            mixR = SoftClip(mixR);

            // Write to buffer
            int idx = offset + s * channels;
            buffer[idx] = mixL;
            if (channels > 1)
            {
                buffer[idx + 1] = mixR;
            }
        }

        return count;
    }

    private static float GenerateWaveform(WaveType type, float phase, float pulseWidth)
    {
        return type switch
        {
            WaveType.Sine => (float)Math.Sin(phase * Math.PI * 2),
            WaveType.Square => phase < 0.5f ? 1f : -1f,
            WaveType.Sawtooth => 2f * phase - 1f,
            WaveType.Triangle => phase < 0.5f ? 4f * phase - 1f : 3f - 4f * phase,
            WaveType.Pulse => phase < pulseWidth ? 1f : -1f,
            WaveType.Noise => 0f, // Handled separately with random
            _ => 0f
        };
    }

    /// <summary>
    /// Soft-clip function to prevent harsh digital clipping.
    /// Uses tanh for smooth saturation above threshold.
    /// </summary>
    private static float SoftClip(float x)
    {
        const float threshold = 0.8f;
        if (x > threshold)
            return threshold + (1f - threshold) * (float)Math.Tanh((x - threshold) / (1f - threshold));
        if (x < -threshold)
            return -threshold - (1f - threshold) * (float)Math.Tanh((-x - threshold) / (1f - threshold));
        return x;
    }

    /// <summary>
    /// Internal voice class for polyphonic playback
    /// </summary>
    private class Voice
    {
        public int Note { get; }
        public long StartTime { get; }
        public bool IsFinished { get; private set; }

        private readonly SimpleSynth _synth;
        private float _velocity;
        private float _currentNote;
        private float _targetNote;

        // Oscillator phases
        private float _osc1Phase;
        private float _osc2Phase;
        private float _subPhase;

        // Unison phases
        private readonly float[] _unisonPhases = new float[8];
        private readonly float[] _unisonDetunes = new float[8];

        // Envelopes
        private float _ampEnv;
        private float _filterEnv;
        private int _ampStage; // 0=attack, 1=decay, 2=sustain, 3=release
        private int _filterStage;

        // Filter state (stereo one-pole lowpass)
        private float _filterState1; // Left channel
        private float _filterState2; // Right channel
        private float _smoothedCutoff; // Smoothed cutoff to prevent zipper noise

        // Anti-click ramp for voice start/end
        private float _startRamp;
        private float _endRamp = 1f;
        private const float StartRampRate = 0.002f; // ~0.5ms ramp at 44.1kHz (fast but smooth)
        private const float EndRampRate = 0.0005f;  // ~2ms end ramp for smooth cutoff

        private readonly Random _random = new();

        public Voice(SimpleSynth synth, int note, float velocity, float startNote, int sampleRate)
        {
            _synth = synth;
            Note = note;
            _velocity = velocity;
            _targetNote = note;
            _currentNote = startNote;
            StartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Initialize unison detunes
            for (int i = 0; i < 8; i++)
            {
                _unisonDetunes[i] = (i - 3.5f) / 3.5f; // Spread from -1 to +1
            }
        }

        public void Retrigger(float velocity)
        {
            _velocity = velocity;
            _ampStage = 0;
            _filterStage = 0;
            // Keep current envelope values for smooth retrigger (no click)
            // The attack phase will rise from current level, not from 0
            // Only reset if envelope is nearly silent
            if (_ampEnv < 0.02f)
            {
                _ampEnv = 0.001f; // Start just above zero to avoid click
                _filterEnv = 0.001f;
                _startRamp = 0f; // Reset start ramp for new attack
            }
            // Reset end ramp in case voice was fading out
            _endRamp = 1f;
            // Don't reset filter state - keeps continuity
        }

        public void TriggerRelease()
        {
            _ampStage = 3;
            _filterStage = 3;
        }

        public (float left, float right) Process(int sampleRate, float pitchMod, float filterMod, float pwMod, SimpleSynth synth)
        {
            if (IsFinished) return (0, 0);

            // Portamento
            if (synth.Portamento > 0 && Math.Abs(_currentNote - _targetNote) > 0.01f)
            {
                float speed = 12f / (synth.Portamento * sampleRate);
                float diff = _targetNote - _currentNote;
                _currentNote += Math.Sign(diff) * Math.Min(Math.Abs(diff), speed);
            }
            else
            {
                _currentNote = _targetNote;
            }

            // Calculate base frequency
            float baseFreq = 440f * (float)Math.Pow(2, (_currentNote - 69 + pitchMod) / 12f);

            // Calculate oscillator frequencies with octave/semi/fine
            float freq1 = baseFreq * (float)Math.Pow(2, synth.Osc1Octave + synth.Osc1Semi / 12f + synth.Osc1Fine / 1200f);
            float freq2 = baseFreq * (float)Math.Pow(2, synth.Osc2Octave + synth.Osc2Semi / 12f + synth.Osc2Fine / 1200f);
            float subFreq = baseFreq * 0.5f;

            float signal = 0f;
            float signalL = 0f;
            float signalR = 0f;

            // Unison processing
            int unisonCount = Math.Max(1, synth.UnisonVoices);
            float unisonGain = 1f / (float)Math.Sqrt(unisonCount);

            for (int u = 0; u < unisonCount; u++)
            {
                float detuneCents = _unisonDetunes[u] * synth.UnisonDetune;
                float detuneRatio = (float)Math.Pow(2, detuneCents / 1200f);
                float osc1PhaseInc = freq1 * detuneRatio / sampleRate;

                // Oscillator 1 with band-limiting
                float pw1 = Math.Clamp(synth.Osc1PulseWidth + pwMod, 0.1f, 0.9f);
                float osc1 = GenerateOsc(synth.Waveform, _unisonPhases[u], pw1, _random, osc1PhaseInc) * synth.Osc1Level;
                _unisonPhases[u] += osc1PhaseInc;
                if (_unisonPhases[u] >= 1f) _unisonPhases[u] -= 1f;

                // Calculate stereo position for unison
                float unisonPan = (u - (unisonCount - 1) / 2f) / Math.Max(1, (unisonCount - 1) / 2f) * synth.UnisonSpread;
                float panL = Math.Min(1f, 1f - unisonPan);
                float panR = Math.Min(1f, 1f + unisonPan);

                signalL += osc1 * panL * unisonGain;
                signalR += osc1 * panR * unisonGain;
            }

            // Oscillator 2 with band-limiting
            if (synth.Osc2Enabled)
            {
                float osc2PhaseInc = freq2 / sampleRate;
                float pw2 = Math.Clamp(synth.Osc2PulseWidth + pwMod, 0.1f, 0.9f);
                float osc2 = GenerateOsc(synth.Osc2Waveform, _osc2Phase, pw2, _random, osc2PhaseInc) * synth.Osc2Level;
                _osc2Phase += osc2PhaseInc;
                if (_osc2Phase >= 1f) _osc2Phase -= 1f;

                signalL += osc2;
                signalR += osc2;
            }

            // Sub oscillator with band-limiting
            if (synth.SubOscLevel > 0)
            {
                float subPhaseInc = subFreq / sampleRate;
                float sub = GenerateOsc(synth.SubOscWaveform, _subPhase, 0.5f, _random, subPhaseInc) * synth.SubOscLevel;
                _subPhase += subPhaseInc;
                if (_subPhase >= 1f) _subPhase -= 1f;

                signalL += sub;
                signalR += sub;
            }

            // Noise
            if (synth.NoiseLevel > 0)
            {
                float noise = ((float)_random.NextDouble() * 2f - 1f) * synth.NoiseLevel;
                signalL += noise;
                signalR += noise;
            }

            // Process envelopes
            _ampEnv = ProcessEnvelope(_ampStage, _ampEnv, synth.Attack, synth.Decay, synth.Sustain, synth.Release, sampleRate, ref _ampStage);
            _filterEnv = ProcessEnvelope(_filterStage, _filterEnv, synth.FilterAttack, synth.FilterDecay, synth.FilterSustain, synth.FilterRelease, sampleRate, ref _filterStage);

            // Check if voice is finished - use end ramp to prevent pop
            if (_ampStage == 3 && _ampEnv <= 0.001f)
            {
                _endRamp -= EndRampRate;
                if (_endRamp <= 0f)
                {
                    IsFinished = true;
                    return (0, 0);
                }
            }

            // Calculate filter cutoff
            float baseCutoff = synth.Cutoff * synth.Cutoff * 18000f + 20f;
            float keyTrack = (Note - 60) / 60f * synth.FilterKeyTrack;
            float envMod = _filterEnv * synth.FilterEnvAmount;
            float targetCutoff = baseCutoff * (float)Math.Pow(2, keyTrack + envMod * 4 + filterMod * 2);
            targetCutoff = Math.Clamp(targetCutoff, 20f, Math.Min(20000f, sampleRate * 0.45f));

            // Smooth cutoff changes to prevent zipper noise and clicks
            const float cutoffSmoothRate = 0.005f;
            if (_smoothedCutoff == 0f) _smoothedCutoff = targetCutoff; // Initialize on first call
            _smoothedCutoff += (targetCutoff - _smoothedCutoff) * cutoffSmoothRate;
            float cutoffHz = _smoothedCutoff;

            // Simple but stable one-pole lowpass filter
            // This avoids the instability issues of higher-order filters
            float rc = 1f / (2f * (float)Math.PI * cutoffHz);
            float dt = 1f / sampleRate;
            float filterAlpha = dt / (rc + dt);

            // Add resonance as gentle feedback
            float resonanceBoost = 1f + synth.Resonance * 2f;

            // Apply filter drive before filtering
            if (synth.FilterDrive > 0)
            {
                signalL = (float)Math.Tanh(signalL * (1f + synth.FilterDrive * 3f));
                signalR = (float)Math.Tanh(signalR * (1f + synth.FilterDrive * 3f));
            }

            // Filter left channel
            _filterState1 += filterAlpha * (signalL * resonanceBoost - _filterState1);
            signalL = _filterState1;

            // Filter right channel
            _filterState2 += filterAlpha * (signalR * resonanceBoost - _filterState2);
            signalR = _filterState2;

            // Apply amplitude envelope and velocity
            float ampMult = _ampEnv * _velocity;
            signalL *= ampMult;
            signalR *= ampMult;

            // Apply anti-click start ramp
            if (_startRamp < 1f)
            {
                _startRamp = Math.Min(1f, _startRamp + StartRampRate);
                signalL *= _startRamp;
                signalR *= _startRamp;
            }

            // Apply end ramp for smooth voice termination
            if (_endRamp < 1f)
            {
                signalL *= _endRamp;
                signalR *= _endRamp;
            }

            return (signalL, signalR);
        }

        // PolyBLEP for anti-aliased waveforms
        private static float PolyBlep(float t, float dt)
        {
            // t = phase position, dt = phase increment (freq/sampleRate)
            if (t < dt)
            {
                t /= dt;
                return t + t - t * t - 1f;
            }
            else if (t > 1f - dt)
            {
                t = (t - 1f) / dt;
                return t * t + t + t + 1f;
            }
            return 0f;
        }

        private float _lastPhase; // Track for PolyBLEP

        private float GenerateOsc(WaveType type, float phase, float pulseWidth, Random random, float phaseInc)
        {
            float sample;
            float dt = phaseInc; // Normalized frequency

            switch (type)
            {
                case WaveType.Sine:
                    sample = (float)Math.Sin(phase * Math.PI * 2);
                    break;

                case WaveType.Sawtooth:
                    // Band-limited sawtooth using PolyBLEP
                    sample = 2f * phase - 1f;
                    sample -= PolyBlep(phase, dt);
                    break;

                case WaveType.Square:
                    // Band-limited square using PolyBLEP
                    sample = phase < 0.5f ? 0.9f : -0.9f;
                    sample += PolyBlep(phase, dt);
                    sample -= PolyBlep((phase + 0.5f) % 1f, dt);
                    break;

                case WaveType.Pulse:
                    // Band-limited pulse using PolyBLEP
                    sample = phase < pulseWidth ? 0.9f : -0.9f;
                    sample += PolyBlep(phase, dt);
                    sample -= PolyBlep((phase + (1f - pulseWidth)) % 1f, dt);
                    break;

                case WaveType.Triangle:
                    // Triangle derived from integrated square (naturally band-limited)
                    sample = phase < 0.5f ? 4f * phase - 1f : 3f - 4f * phase;
                    break;

                case WaveType.Noise:
                    sample = (float)random.NextDouble() * 2f - 1f;
                    break;

                default:
                    sample = 0f;
                    break;
            }

            return sample;
        }

        private static float ProcessEnvelope(int stage, float current, float attack, float decay,
            float sustain, float release, int sampleRate, ref int stageRef)
        {
            // Use exponential curves for natural-sounding envelopes
            // This prevents clicks and pops at transitions
            // Minimum times prevent too-fast transitions that cause pops
            const float minTime = 0.005f; // 5ms minimum for any stage

            switch (stage)
            {
                case 0: // Attack - exponential rise
                    float attackTime = Math.Max(attack, minTime);
                    float attackRate = 1f / (attackTime * sampleRate);
                    // Smooth asymptotic approach - coefficient controls curve shape
                    float attackTarget = 1.02f; // Slightly overshoot target for smooth arrival
                    current += attackRate * (attackTarget - current) * 6f;
                    if (current >= 0.999f)
                    {
                        current = 1f;
                        stageRef = 1;
                    }
                    break;

                case 1: // Decay - exponential fall to sustain
                    float decayTime = Math.Max(decay, minTime);
                    float decayRate = 1f / (decayTime * sampleRate);
                    // Smooth exponential decay
                    current += decayRate * (sustain - current) * 5f;
                    if (Math.Abs(current - sustain) < 0.002f)
                    {
                        current = sustain;
                        stageRef = 2;
                    }
                    break;

                case 2: // Sustain - smoothly approach sustain level (handles level changes)
                    float sustainDiff = sustain - current;
                    if (Math.Abs(sustainDiff) > 0.001f)
                    {
                        current += sustainDiff * 0.01f; // Gentle smoothing
                    }
                    else
                    {
                        current = sustain;
                    }
                    break;

                case 3: // Release - smooth exponential fall to zero
                    float releaseTime = Math.Max(release, minTime);
                    float releaseRate = 1f / (releaseTime * sampleRate);
                    // Smooth release - never goes negative
                    current *= (1f - releaseRate * 4f);
                    current = Math.Max(0f, current);
                    break;
            }

            return current;
        }
    }
}
