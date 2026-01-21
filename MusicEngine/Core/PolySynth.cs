//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Polyphonic synthesizer with voice management and voice stealing.


using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;


namespace MusicEngine.Core;


/// <summary>
/// Voice stealing modes for when polyphony limit is reached
/// </summary>
public enum VoiceStealMode
{
    /// <summary>Don't steal - new notes are ignored</summary>
    None,
    /// <summary>Steal the oldest playing voice</summary>
    Oldest,
    /// <summary>Steal the quietest voice</summary>
    Quietest,
    /// <summary>Steal the lowest note</summary>
    Lowest,
    /// <summary>Steal the highest note</summary>
    Highest,
    /// <summary>Steal the same note if playing</summary>
    SameNote
}


/// <summary>
/// Represents a single voice in the polyphonic synth
/// </summary>
internal class Voice
{
    public int Note { get; set; }
    public int Velocity { get; set; }
    public double Frequency { get; set; }
    public double Phase { get; set; }
    public Envelope Envelope { get; }
    public bool IsActive => Envelope.IsActive;
    public bool IsReleasing => Envelope.Stage == EnvelopeStage.Release;
    public DateTime TriggerTime { get; set; }
    public double CurrentAmplitude => Envelope.Value * (Velocity / 127.0);

    public Voice()
    {
        Envelope = new Envelope(0.01, 0.1, 0.7, 0.3);
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        Frequency = 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);
        Phase = 0;
        TriggerTime = DateTime.Now;
        Envelope.Trigger(velocity);
    }

    public void Release()
    {
        Envelope.Release_Gate();
    }

    public void Reset()
    {
        Note = -1;
        Velocity = 0;
        Phase = 0;
        Envelope.Reset();
    }
}


/// <summary>
/// Polyphonic synthesizer with configurable voice count and voice stealing.
/// </summary>
public class PolySynth : ISynth
{
    private readonly Voice[] _voices;
    private readonly WaveFormat _waveFormat;
    private readonly object _lock = new();
    private readonly Dictionary<int, int> _noteToVoice = new(); // Maps note to voice index

    // Oscillator state
    private float _lastFilterOutput;

    /// <summary>
    /// Gets or sets the synth name
    /// </summary>
    public string Name { get; set; } = "PolySynth";

    /// <summary>
    /// Gets the audio wave format
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Gets or sets the waveform type
    /// </summary>
    public WaveType Waveform { get; set; } = WaveType.Sawtooth;

    /// <summary>
    /// Gets or sets the voice stealing mode
    /// </summary>
    public VoiceStealMode StealMode { get; set; } = VoiceStealMode.Oldest;

    /// <summary>
    /// Gets or sets the master volume (0-1)
    /// </summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>
    /// Gets or sets the filter cutoff (0-1)
    /// </summary>
    public float Cutoff { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the filter resonance (0-1)
    /// </summary>
    public float Resonance { get; set; } = 0.0f;

    /// <summary>
    /// Gets or sets the detune amount in cents
    /// </summary>
    public float Detune { get; set; } = 0f;

    /// <summary>
    /// Gets or sets the envelope attack time
    /// </summary>
    public double Attack
    {
        get => _voices[0].Envelope.Attack;
        set { foreach (var v in _voices) v.Envelope.Attack = value; }
    }

    /// <summary>
    /// Gets or sets the envelope decay time
    /// </summary>
    public double Decay
    {
        get => _voices[0].Envelope.Decay;
        set { foreach (var v in _voices) v.Envelope.Decay = value; }
    }

    /// <summary>
    /// Gets or sets the envelope sustain level
    /// </summary>
    public double Sustain
    {
        get => _voices[0].Envelope.Sustain;
        set { foreach (var v in _voices) v.Envelope.Sustain = value; }
    }

    /// <summary>
    /// Gets or sets the envelope release time
    /// </summary>
    public double Release
    {
        get => _voices[0].Envelope.Release;
        set { foreach (var v in _voices) v.Envelope.Release = value; }
    }

    /// <summary>
    /// Gets the maximum number of voices
    /// </summary>
    public int MaxVoices => _voices.Length;

    /// <summary>
    /// Gets the number of currently active voices
    /// </summary>
    public int ActiveVoiceCount
    {
        get
        {
            lock (_lock)
            {
                return _voices.Count(v => v.IsActive);
            }
        }
    }

    /// <summary>
    /// LFO for vibrato modulation
    /// </summary>
    public LFO? VibratoLFO { get; set; }

    /// <summary>
    /// LFO for filter modulation
    /// </summary>
    public LFO? FilterLFO { get; set; }

    /// <summary>
    /// Vibrato depth (in semitones)
    /// </summary>
    public float VibratoDepth { get; set; } = 0f;

    /// <summary>
    /// Filter LFO depth (0-1)
    /// </summary>
    public float FilterLFODepth { get; set; } = 0f;

    /// <summary>
    /// Creates a polyphonic synth with specified number of voices
    /// </summary>
    public PolySynth(int maxVoices = 16, int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        _voices = new Voice[maxVoices];
        for (int i = 0; i < maxVoices; i++)
        {
            _voices[i] = new Voice();
        }
    }

    /// <summary>
    /// Trigger a note
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
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
            if (voiceIndex < 0) return; // No voice available

            // Remove old note mapping if voice was stolen
            var oldMapping = _noteToVoice.FirstOrDefault(kvp => kvp.Value == voiceIndex);
            if (oldMapping.Key != 0)
            {
                _noteToVoice.Remove(oldMapping.Key);
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
            case "waveform":
                Waveform = (WaveType)(int)value;
                break;
            case "cutoff":
                Cutoff = Math.Clamp(value, 0f, 1f);
                break;
            case "resonance":
                Resonance = Math.Clamp(value, 0f, 1f);
                break;
            case "volume":
                Volume = Math.Clamp(value, 0f, 1f);
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
            case "detune":
                Detune = value;
                break;
            case "vibrato":
                VibratoDepth = value;
                break;
        }
    }

    /// <summary>
    /// Read audio samples
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        // Clear buffer
        for (int n = 0; n < count; n++)
        {
            buffer[offset + n] = 0;
        }

        int channels = _waveFormat.Channels;
        double sampleRate = _waveFormat.SampleRate;
        double deltaTime = 1.0 / sampleRate;

        // Get LFO values for this buffer (use center value for simplicity)
        double vibratoMod = 0;
        double filterMod = 0;

        lock (_lock)
        {

            if (VibratoLFO != null && VibratoLFO.Enabled)
            {
                vibratoMod = VibratoLFO.GetValue(_waveFormat.SampleRate) * VibratoDepth / 12.0;
            }

            if (FilterLFO != null && FilterLFO.Enabled)
            {
                filterMod = FilterLFO.GetValue(_waveFormat.SampleRate) * FilterLFODepth;
            }

            foreach (var voice in _voices)
            {
                if (!voice.IsActive) continue;

                // Calculate frequency with vibrato
                double frequency = voice.Frequency;
                if (vibratoMod != 0)
                {
                    frequency *= Math.Pow(2.0, vibratoMod);
                }

                // Apply detune
                if (Detune != 0)
                {
                    frequency *= Math.Pow(2.0, Detune / 1200.0);
                }

                double phaseIncrement = 2.0 * Math.PI * frequency / sampleRate;

                for (int n = 0; n < count; n += channels)
                {
                    // Process envelope
                    double envelopeValue = voice.Envelope.Process(deltaTime);

                    if (voice.Envelope.Stage == EnvelopeStage.Idle) break;

                    // Generate waveform
                    float sample = GenerateSample(voice.Phase, Waveform);

                    // Apply envelope and velocity
                    sample *= (float)(envelopeValue * (voice.Velocity / 127.0) * Volume);

                    // Output to all channels
                    for (int c = 0; c < channels; c++)
                    {
                        if (offset + n + c < buffer.Length)
                        {
                            buffer[offset + n + c] += sample;
                        }
                    }

                    // Increment phase
                    voice.Phase += phaseIncrement;
                    if (voice.Phase > 2.0 * Math.PI)
                    {
                        voice.Phase -= 2.0 * Math.PI;
                    }
                }
            }
        }

        // Apply filter
        float effectiveCutoff = Math.Clamp(Cutoff + (float)filterMod, 0f, 1f);
        if (effectiveCutoff < 1.0f)
        {
            ApplyFilter(buffer, offset, count, effectiveCutoff);
        }

        return count;
    }

    /// <summary>
    /// Generate a waveform sample
    /// </summary>
    private float GenerateSample(double phase, WaveType waveform)
    {
        return waveform switch
        {
            WaveType.Sine => (float)Math.Sin(phase),
            WaveType.Square => phase < Math.PI ? 1.0f : -1.0f,
            WaveType.Sawtooth => (float)(2.0 * (phase / (2.0 * Math.PI)) - 1.0),
            WaveType.Triangle => phase < Math.PI
                ? (float)(2.0 * (phase / Math.PI) - 1.0)
                : (float)(3.0 - 2.0 * (phase / Math.PI)),
            WaveType.Noise => (float)(new Random().NextDouble() * 2.0 - 1.0),
            _ => 0
        };
    }

    /// <summary>
    /// Apply low-pass filter
    /// </summary>
    private void ApplyFilter(float[] buffer, int offset, int count, float cutoff)
    {
        float alpha = cutoff * cutoff * 0.5f;

        for (int i = 0; i < count; i++)
        {
            _lastFilterOutput += alpha * (buffer[offset + i] - _lastFilterOutput);
            buffer[offset + i] = _lastFilterOutput;
        }
    }

    /// <summary>
    /// Find a free voice or steal one
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
}
