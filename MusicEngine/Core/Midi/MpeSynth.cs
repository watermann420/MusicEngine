// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MIDI handling component.

using System;
using System.Collections.Generic;
using MusicEngine.Core;
using NAudio.Wave;


namespace MusicEngine.Core.Midi;


/// <summary>
/// MPE-enabled synthesizer demonstrating per-note expression capabilities.
/// Features per-voice pitch bend, slide-to-filter, and pressure-to-modulation mappings.
/// </summary>
/// <remarks>
/// This synthesizer serves as a reference implementation for MPE support.
/// It demonstrates:
/// - Per-note pitch bend with 48 semitone range
/// - Slide (CC74) mapped to filter cutoff
/// - Pressure mapped to volume boost and vibrato depth
/// - Per-voice glide/portamento
/// - Smooth expression parameter interpolation
///
/// Usage:
/// 1. Create MpeSynth instance
/// 2. Connect to MpeProcessor.ExpressionChanged event or call methods directly
/// 3. Use as ISampleProvider for audio output
/// </remarks>
public class MpeSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly MpeSynthVoice[] _voices;
    private readonly Dictionary<int, MpeSynthVoice> _noteIdToVoice = new();
    private readonly object _lock = new();
    private readonly MpeProcessor _mpeProcessor;

    /// <summary>
    /// Maximum number of simultaneous voices.
    /// </summary>
    public const int MaxVoices = 16;

    /// <summary>
    /// Gets or sets the synth name.
    /// </summary>
    public string Name { get; set; } = "MpeSynth";

    /// <summary>
    /// Gets the audio format.
    /// </summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Gets the MPE processor for this synth.
    /// </summary>
    public MpeProcessor MpeProcessor => _mpeProcessor;

    /// <summary>
    /// Gets or sets the MPE configuration.
    /// </summary>
    public MpeConfiguration MpeConfiguration
    {
        get => _mpeProcessor.Configuration;
        set => _mpeProcessor.Configuration = value;
    }

    /// <summary>
    /// Gets or sets whether MPE mode is enabled.
    /// </summary>
    public bool MpeEnabled
    {
        get => MpeConfiguration.Enabled;
        set => MpeConfiguration.Enabled = value;
    }

    /// <summary>
    /// Gets or sets the master volume (0-1).
    /// </summary>
    public float Volume { get; set; } = 0.5f;

    /// <summary>
    /// Gets or sets the waveform type for all voices.
    /// </summary>
    public WaveType Waveform { get; set; } = WaveType.Sawtooth;

    /// <summary>
    /// Gets or sets the filter type.
    /// </summary>
    public SynthFilterType FilterType { get; set; } = SynthFilterType.MoogLadder;

    /// <summary>
    /// Gets or sets the base filter cutoff (0-1).
    /// </summary>
    public float FilterCutoff { get; set; } = 0.6f;

    /// <summary>
    /// Gets or sets the filter resonance (0-1).
    /// </summary>
    public float FilterResonance { get; set; } = 0.3f;

    /// <summary>
    /// Gets or sets the filter envelope amount.
    /// </summary>
    public float FilterEnvAmount { get; set; } = 0.4f;

    /// <summary>
    /// Gets or sets how much slide (Y-axis) affects filter cutoff.
    /// </summary>
    public float SlideToFilterAmount { get; set; } = 0.6f;

    /// <summary>
    /// Gets or sets how much pressure (Z-axis) affects volume.
    /// </summary>
    public float PressureToVolumeAmount { get; set; } = 0.3f;

    /// <summary>
    /// Gets or sets how much pressure adds vibrato.
    /// </summary>
    public float PressureToVibratoAmount { get; set; } = 0.15f;

    /// <summary>
    /// Gets or sets the per-voice glide time in seconds.
    /// </summary>
    public float GlideTime { get; set; } = 0f;

    /// <summary>
    /// Gets or sets the amplitude envelope attack time.
    /// </summary>
    public double Attack { get; set; } = 0.01;

    /// <summary>
    /// Gets or sets the amplitude envelope decay time.
    /// </summary>
    public double Decay { get; set; } = 0.2;

    /// <summary>
    /// Gets or sets the amplitude envelope sustain level.
    /// </summary>
    public double Sustain { get; set; } = 0.7;

    /// <summary>
    /// Gets or sets the amplitude envelope release time.
    /// </summary>
    public double Release { get; set; } = 0.3;

    /// <summary>
    /// Gets or sets the pitch bend range in semitones (for non-MPE mode).
    /// </summary>
    public float StandardPitchBendRange { get; set; } = 2f;

    /// <summary>
    /// Current global pitch bend value (-1 to +1) for non-MPE mode.
    /// </summary>
    public float GlobalPitchBend { get; set; }

    /// <summary>
    /// LFO for global vibrato (used when pressure is not active).
    /// </summary>
    public LFO? VibratoLFO { get; set; }

    /// <summary>
    /// Global vibrato depth in semitones.
    /// </summary>
    public float VibratoDepth { get; set; } = 0f;

    /// <summary>
    /// Creates a new MPE-enabled synthesizer.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz, or null for default.</param>
    public MpeSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize MPE processor
        _mpeProcessor = new MpeProcessor(MpeConfiguration.CreateLowerZone());

        // Subscribe to expression changes
        _mpeProcessor.NoteTriggered += OnNoteTriggered;
        _mpeProcessor.NoteReleased += OnNoteReleased;
        _mpeProcessor.ExpressionChanged += OnExpressionChanged;

        // Initialize voices
        _voices = new MpeSynthVoice[MaxVoices];
        for (int i = 0; i < MaxVoices; i++)
        {
            _voices[i] = new MpeSynthVoice(rate, this);
        }
    }

    /// <summary>
    /// Processes a raw MIDI message.
    /// </summary>
    /// <param name="message">The raw MIDI message bytes.</param>
    /// <returns>True if the message was processed.</returns>
    public bool ProcessMidiMessage(byte[] message)
    {
        if (MpeEnabled)
        {
            return _mpeProcessor.ProcessMidiMessage(message);
        }
        else
        {
            // Standard MIDI handling
            return ProcessStandardMidi(message);
        }
    }

    private bool ProcessStandardMidi(byte[] message)
    {
        if (message == null || message.Length < 1) return false;

        int status = message[0];
        int channel = status & 0x0F;
        int messageType = status & 0xF0;

        switch (messageType)
        {
            case 0x90: // Note On
                if (message.Length >= 3)
                {
                    int note = message[1] & 0x7F;
                    int velocity = message[2] & 0x7F;
                    if (velocity == 0)
                        NoteOff(note);
                    else
                        NoteOn(note, velocity);
                    return true;
                }
                break;

            case 0x80: // Note Off
                if (message.Length >= 3)
                {
                    int note = message[1] & 0x7F;
                    NoteOff(note);
                    return true;
                }
                break;

            case 0xE0: // Pitch Bend
                if (message.Length >= 3)
                {
                    int lsb = message[1] & 0x7F;
                    int msb = message[2] & 0x7F;
                    int pitchBend = (msb << 7) | lsb;
                    GlobalPitchBend = (pitchBend - 8192) / 8192f;
                    return true;
                }
                break;
        }

        return false;
    }

    /// <summary>
    /// Triggers a note (standard MIDI interface).
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        if (MpeEnabled)
        {
            // In MPE mode, notes should come through ProcessMidiMessage
            // This is a fallback for direct API calls
            _mpeProcessor.ProcessNoteOn(1, note, velocity);
        }
        else
        {
            // Standard poly mode
            StandardNoteOn(note, velocity);
        }
    }

    /// <summary>
    /// Releases a note (standard MIDI interface).
    /// </summary>
    public void NoteOff(int note)
    {
        if (MpeEnabled)
        {
            // Find and release the note on any channel
            lock (_lock)
            {
                foreach (var voice in _voices)
                {
                    if (voice.IsActive && voice.NoteNumber == note)
                    {
                        voice.Release(0.5f);
                        _noteIdToVoice.Remove(voice.NoteId);
                        break;
                    }
                }
            }
        }
        else
        {
            StandardNoteOff(note);
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
                if (voice.IsActive)
                {
                    voice.Release(0.5f);
                }
            }
            _noteIdToVoice.Clear();
        }

        if (MpeEnabled)
        {
            _mpeProcessor.AllNotesOff();
        }
    }

    /// <summary>
    /// Sets a parameter by name.
    /// </summary>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
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
            case "filterenvamount":
                FilterEnvAmount = Math.Clamp(value, -1f, 1f);
                break;
            case "slidetofilter":
                SlideToFilterAmount = Math.Clamp(value, 0f, 1f);
                break;
            case "pressuretovolume":
                PressureToVolumeAmount = Math.Clamp(value, 0f, 1f);
                break;
            case "pressuretovibrato":
                PressureToVibratoAmount = Math.Clamp(value, 0f, 1f);
                break;
            case "attack":
                Attack = Math.Max(0.001, value);
                break;
            case "decay":
                Decay = Math.Max(0.001, value);
                break;
            case "sustain":
                Sustain = Math.Clamp(value, 0f, 1f);
                break;
            case "release":
                Release = Math.Max(0.001, value);
                break;
            case "glide":
            case "portamento":
                GlideTime = Math.Max(0, value);
                break;
            case "mpeenable":
            case "mpeenabled":
                MpeEnabled = value > 0.5f;
                break;
            case "pitchbendrange":
                StandardPitchBendRange = Math.Clamp(value, 1f, 48f);
                break;
            case "vibrato":
            case "vibratodepth":
                VibratoDepth = value;
                break;
        }
    }

    /// <summary>
    /// Reads audio samples.
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

        // Global pitch modulation (non-MPE or master)
        float globalPitchMod = GlobalPitchBend * StandardPitchBendRange;
        if (VibratoLFO != null && VibratoLFO.Enabled)
        {
            globalPitchMod += (float)(VibratoLFO.GetValue(_waveFormat.SampleRate) * VibratoDepth);
        }

        // Add master pitch bend from MPE processor
        if (MpeEnabled)
        {
            globalPitchMod += _mpeProcessor.MasterPitchBend;
        }

        lock (_lock)
        {
            for (int n = 0; n < count; n += channels)
            {
                float sample = 0f;

                foreach (var voice in _voices)
                {
                    if (!voice.IsActive) continue;

                    // Update voice settings
                    voice.GlobalPitchMod = globalPitchMod;

                    // Process voice
                    sample += voice.Process(deltaTime);
                }

                // Apply master volume
                sample *= Volume;

                // Apply MPE master volume/expression
                if (MpeEnabled)
                {
                    sample *= _mpeProcessor.MasterVolume * _mpeProcessor.MasterExpression;
                }

                // Soft clipping
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

    private void OnNoteTriggered(object? sender, PerNoteExpressionEventArgs e)
    {
        lock (_lock)
        {
            var voice = FindFreeVoice();
            if (voice != null)
            {
                // Apply synth settings to voice
                voice.Waveform = Waveform;
                voice.FilterCutoff = FilterCutoff;
                voice.FilterResonance = FilterResonance;
                voice.FilterType = FilterType;
                voice.FilterEnvAmount = FilterEnvAmount;
                voice.SlideToFilterAmount = SlideToFilterAmount;
                voice.PressureToVolumeAmount = PressureToVolumeAmount;
                voice.PressureToVibratoAmount = PressureToVibratoAmount;
                voice.GlideTime = GlideTime;

                voice.AmpEnvelope.Attack = Attack;
                voice.AmpEnvelope.Decay = Decay;
                voice.AmpEnvelope.Sustain = Sustain;
                voice.AmpEnvelope.Release = Release;

                voice.Trigger(e.Expression);
                _noteIdToVoice[e.Expression.NoteId] = voice;
            }
        }
    }

    private void OnNoteReleased(object? sender, PerNoteExpressionEventArgs e)
    {
        lock (_lock)
        {
            if (_noteIdToVoice.TryGetValue(e.Expression.NoteId, out var voice))
            {
                voice.Release(e.Expression.Lift);
                _noteIdToVoice.Remove(e.Expression.NoteId);
            }
        }
    }

    private void OnExpressionChanged(object? sender, PerNoteExpressionEventArgs e)
    {
        if (e.ChangeType == ExpressionChangeType.NoteOn || e.ChangeType == ExpressionChangeType.NoteOff)
            return;

        lock (_lock)
        {
            if (_noteIdToVoice.TryGetValue(e.Expression.NoteId, out var voice))
            {
                voice.ApplyExpression(e.Expression);
            }
        }
    }

    private void StandardNoteOn(int note, int velocity)
    {
        lock (_lock)
        {
            // Create a simple expression for non-MPE mode
            var expression = new PerNoteExpression(0, note, velocity, (int)StandardPitchBendRange);

            var voice = FindFreeVoice();
            if (voice != null)
            {
                // Apply synth settings
                voice.Waveform = Waveform;
                voice.FilterCutoff = FilterCutoff;
                voice.FilterResonance = FilterResonance;
                voice.FilterType = FilterType;
                voice.FilterEnvAmount = FilterEnvAmount;
                voice.SlideToFilterAmount = 0; // No slide in non-MPE mode
                voice.PressureToVolumeAmount = 0;
                voice.PressureToVibratoAmount = 0;
                voice.GlideTime = GlideTime;

                voice.AmpEnvelope.Attack = Attack;
                voice.AmpEnvelope.Decay = Decay;
                voice.AmpEnvelope.Sustain = Sustain;
                voice.AmpEnvelope.Release = Release;

                voice.Trigger(expression);
                _noteIdToVoice[expression.NoteId] = voice;
            }
        }
    }

    private void StandardNoteOff(int note)
    {
        int noteId = PerNoteExpression.CreateNoteId(0, note);

        lock (_lock)
        {
            if (_noteIdToVoice.TryGetValue(noteId, out var voice))
            {
                voice.Release(0.5f);
                _noteIdToVoice.Remove(noteId);
            }
        }
    }

    private MpeSynthVoice? FindFreeVoice()
    {
        // Find inactive voice
        foreach (var voice in _voices)
        {
            if (!voice.IsActive) return voice;
        }

        // Voice stealing - find oldest releasing voice
        MpeSynthVoice? oldest = null;
        DateTime oldestTime = DateTime.MaxValue;

        foreach (var voice in _voices)
        {
            if (voice.IsReleasing && voice.TriggerTime < oldestTime)
            {
                oldest = voice;
                oldestTime = voice.TriggerTime;
            }
        }

        if (oldest != null) return oldest;

        // Steal oldest active voice
        foreach (var voice in _voices)
        {
            if (voice.TriggerTime < oldestTime)
            {
                oldest = voice;
                oldestTime = voice.TriggerTime;
            }
        }

        return oldest;
    }

    /// <summary>
    /// Creates an MPE synth with pad preset.
    /// </summary>
    public static MpeSynth CreatePadPreset()
    {
        var synth = new MpeSynth
        {
            Name = "MPE Pad",
            Waveform = WaveType.Sawtooth,
            FilterType = SynthFilterType.LowPass,
            FilterCutoff = 0.4f,
            FilterResonance = 0.2f,
            FilterEnvAmount = 0.3f,
            SlideToFilterAmount = 0.7f,
            PressureToVolumeAmount = 0.2f,
            PressureToVibratoAmount = 0.2f,
            Attack = 0.3,
            Decay = 0.5,
            Sustain = 0.8,
            Release = 1.0,
            GlideTime = 0.1f
        };
        synth.MpeEnabled = true;
        return synth;
    }

    /// <summary>
    /// Creates an MPE synth with lead preset.
    /// </summary>
    public static MpeSynth CreateLeadPreset()
    {
        var synth = new MpeSynth
        {
            Name = "MPE Lead",
            Waveform = WaveType.Sawtooth,
            FilterType = SynthFilterType.MoogLadder,
            FilterCutoff = 0.6f,
            FilterResonance = 0.4f,
            FilterEnvAmount = 0.5f,
            SlideToFilterAmount = 0.8f,
            PressureToVolumeAmount = 0.1f,
            PressureToVibratoAmount = 0.3f,
            Attack = 0.01,
            Decay = 0.2,
            Sustain = 0.6,
            Release = 0.2,
            GlideTime = 0.05f
        };
        synth.MpeEnabled = true;
        return synth;
    }

    /// <summary>
    /// Creates an MPE synth with bass preset.
    /// </summary>
    public static MpeSynth CreateBassPreset()
    {
        var synth = new MpeSynth
        {
            Name = "MPE Bass",
            Waveform = WaveType.Square,
            FilterType = SynthFilterType.MoogLadder,
            FilterCutoff = 0.3f,
            FilterResonance = 0.5f,
            FilterEnvAmount = 0.6f,
            SlideToFilterAmount = 0.5f,
            PressureToVolumeAmount = 0.2f,
            PressureToVibratoAmount = 0f,
            Attack = 0.001,
            Decay = 0.15,
            Sustain = 0.4,
            Release = 0.1,
            GlideTime = 0.02f
        };
        synth.MpeEnabled = true;
        return synth;
    }
}


/// <summary>
/// Internal voice class for MpeSynth with full MPE expression support.
/// </summary>
internal class MpeSynthVoice : MpeVoice
{
    private readonly MpeSynth _synth;
    private readonly Random _random = new();
    private double _phase;

    // Filter state for Moog ladder
    private double _filterState1;
    private double _filterState2;
    private double _filterState3;
    private double _filterState4;

    // Vibrato LFO phase
    private double _vibratoPhase;

    /// <summary>
    /// Gets or sets the waveform type.
    /// </summary>
    public WaveType Waveform { get; set; } = WaveType.Sawtooth;

    /// <summary>
    /// Gets or sets the filter type.
    /// </summary>
    public SynthFilterType FilterType { get; set; } = SynthFilterType.MoogLadder;

    /// <summary>
    /// Gets or sets the filter cutoff (0-1).
    /// </summary>
    public float FilterCutoff { get; set; } = 0.6f;

    /// <summary>
    /// Gets or sets the filter resonance (0-1).
    /// </summary>
    public float FilterResonance { get; set; } = 0.3f;

    /// <summary>
    /// Gets or sets the filter envelope amount.
    /// </summary>
    public float FilterEnvAmount { get; set; } = 0.4f;

    /// <summary>
    /// Gets or sets how much slide affects filter cutoff.
    /// </summary>
    public float SlideToFilterAmount { get; set; } = 0.6f;

    /// <summary>
    /// Gets or sets how much pressure affects volume.
    /// </summary>
    public float PressureToVolumeAmount { get; set; } = 0.3f;

    /// <summary>
    /// Gets or sets how much pressure adds vibrato.
    /// </summary>
    public float PressureToVibratoAmount { get; set; } = 0.15f;

    /// <summary>
    /// Gets or sets the global pitch modulation in semitones.
    /// </summary>
    public float GlobalPitchMod { get; set; }

    public MpeSynthVoice(int sampleRate, MpeSynth synth) : base(sampleRate)
    {
        _synth = synth;
    }

    protected override void OnTriggered()
    {
        _phase = 0;
        _vibratoPhase = 0;
        _filterState1 = _filterState2 = _filterState3 = _filterState4 = 0;
    }

    protected override float ProcessSample(double deltaTime, double ampEnvelope, double filterEnvelope)
    {
        // Calculate pressure-based vibrato
        double vibrato = 0;
        if (PressureToVibratoAmount > 0 && Pressure > 0.1f)
        {
            _vibratoPhase += deltaTime * 5.5; // ~5.5 Hz vibrato
            vibrato = Math.Sin(_vibratoPhase * 2 * Math.PI) * Pressure * PressureToVibratoAmount;
        }

        // Calculate frequency with pitch bend and vibrato
        double freq = Frequency;
        freq *= Math.Pow(2.0, (GlobalPitchMod + vibrato) / 12.0);

        // Phase increment
        double phaseInc = 2.0 * Math.PI * freq / SampleRate;
        _phase += phaseInc;
        if (_phase >= 2.0 * Math.PI) _phase -= 2.0 * Math.PI;

        // Generate waveform
        double sample = GenerateWaveform(_phase);

        // Calculate effective filter cutoff
        // Base cutoff + filter envelope + slide modulation
        double effectiveCutoff = FilterCutoff;
        effectiveCutoff += filterEnvelope * FilterEnvAmount;
        effectiveCutoff += (Slide - 0.5f) * 2.0 * SlideToFilterAmount;
        effectiveCutoff = Math.Clamp(effectiveCutoff, 0.01, 0.99);

        // Apply filter
        sample = ApplyFilter(sample, effectiveCutoff);

        // Apply amplitude envelope
        sample *= ampEnvelope;

        // Apply velocity
        sample *= StrikeVelocity;

        // Apply pressure-based volume boost
        if (PressureToVolumeAmount > 0)
        {
            double pressureVolume = 1.0 + Pressure * PressureToVolumeAmount;
            sample *= pressureVolume;
        }

        return (float)sample;
    }

    private double GenerateWaveform(double phase)
    {
        return Waveform switch
        {
            WaveType.Sine => Math.Sin(phase),
            WaveType.Square => phase < Math.PI ? 0.8 : -0.8,
            WaveType.Sawtooth => (phase / Math.PI) - 1.0,
            WaveType.Triangle => phase < Math.PI
                ? (2.0 * phase / Math.PI) - 1.0
                : 3.0 - (2.0 * phase / Math.PI),
            WaveType.Noise => _random.NextDouble() * 2.0 - 1.0,
            _ => Math.Sin(phase)
        };
    }

    private double ApplyFilter(double input, double cutoff)
    {
        if (FilterType == SynthFilterType.None) return input;

        // Map cutoff to frequency
        double freq = 20.0 * Math.Pow(1000.0, cutoff);
        freq = Math.Min(freq, SampleRate * 0.45);

        if (FilterType == SynthFilterType.MoogLadder)
        {
            // Moog ladder filter approximation
            double fc = freq / SampleRate;
            double f = fc * 1.16;
            double fb = FilterResonance * 4.0 * (1.0 - 0.15 * f * f);

            input -= _filterState4 * fb;
            input *= 0.35013 * (f * f) * (f * f);

            _filterState1 = input + 0.3 * _filterState1 + (1 - f) * _filterState1;
            _filterState2 = _filterState1 + 0.3 * _filterState2 + (1 - f) * _filterState2;
            _filterState3 = _filterState2 + 0.3 * _filterState3 + (1 - f) * _filterState3;
            _filterState4 = _filterState3 + 0.3 * _filterState4 + (1 - f) * _filterState4;

            return _filterState4;
        }
        else
        {
            // Simple one-pole lowpass
            double rc = 1.0 / (2.0 * Math.PI * freq);
            double dt = 1.0 / SampleRate;
            double alpha = dt / (rc + dt);

            _filterState1 = _filterState1 + alpha * (input - _filterState1);
            return _filterState1;
        }
    }
}
