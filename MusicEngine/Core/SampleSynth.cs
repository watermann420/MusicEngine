// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Loop mode for sample playback
/// </summary>
public enum SampleLoopMode
{
    /// <summary>One-shot playback, no looping</summary>
    None,
    /// <summary>Loop forward continuously</summary>
    Forward,
    /// <summary>Loop forward then backward (ping-pong)</summary>
    PingPong,
    /// <summary>Play sample in reverse</summary>
    Reverse
}

/// <summary>
/// Filter type for sample playback
/// </summary>
public enum SamplerFilterType
{
    Lowpass,
    Highpass,
    Bandpass
}

/// <summary>
/// Represents a sample zone with mapping, loop points, and settings
/// </summary>
public class SampleZone
{
    /// <summary>Sample audio data (stereo interleaved)</summary>
    public float[] AudioData { get; set; } = Array.Empty<float>();

    /// <summary>Sample name</summary>
    public string Name { get; set; } = "";

    /// <summary>Sample file path</summary>
    public string FilePath { get; set; } = "";

    /// <summary>Sample rate of the loaded audio</summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>Number of channels in the sample</summary>
    public int Channels { get; set; } = 2;

    // Key mapping
    /// <summary>Lowest MIDI note this zone responds to (0-127)</summary>
    public int LowNote { get; set; } = 0;

    /// <summary>Highest MIDI note this zone responds to (0-127)</summary>
    public int HighNote { get; set; } = 127;

    /// <summary>Root note where sample plays at original pitch</summary>
    public int RootNote { get; set; } = 60;

    // Velocity layers
    /// <summary>Minimum velocity this zone responds to (0-127)</summary>
    public int LowVelocity { get; set; } = 0;

    /// <summary>Maximum velocity this zone responds to (0-127)</summary>
    public int HighVelocity { get; set; } = 127;

    // Round-robin
    /// <summary>Round-robin group (0 = no round-robin)</summary>
    public int RoundRobinGroup { get; set; } = 0;

    /// <summary>Round-robin sequence index within group</summary>
    public int RoundRobinIndex { get; set; } = 0;

    // Loop settings
    /// <summary>Loop start position in samples</summary>
    public int LoopStart { get; set; } = 0;

    /// <summary>Loop end position in samples</summary>
    public int LoopEnd { get; set; } = 0;

    /// <summary>Crossfade length in samples for smooth looping</summary>
    public int LoopCrossfade { get; set; } = 0;

    /// <summary>Loop mode</summary>
    public SampleLoopMode LoopMode { get; set; } = SampleLoopMode.None;

    // Zone settings
    /// <summary>Play sample in reverse</summary>
    public bool Reverse { get; set; } = false;

    /// <summary>Volume adjustment (0-2)</summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>Pan (-1 = left, 0 = center, 1 = right)</summary>
    public float Pan { get; set; } = 0f;

    /// <summary>Tuning adjustment in semitones</summary>
    public float Tune { get; set; } = 0f;

    /// <summary>Fine tuning in cents (-100 to 100)</summary>
    public float FineTune { get; set; } = 0f;

    /// <summary>
    /// Check if this zone matches a note and velocity
    /// </summary>
    public bool Matches(int note, int velocity)
    {
        return note >= LowNote && note <= HighNote &&
               velocity >= LowVelocity && velocity <= HighVelocity;
    }

    /// <summary>
    /// Get the total sample length in frames
    /// </summary>
    public int LengthInFrames => Channels > 0 ? AudioData.Length / Channels : 0;
}

/// <summary>
/// Voice state for sample playback with envelopes and modulation
/// </summary>
internal class SamplerVoice
{
    private readonly int _outputSampleRate;
    private readonly SampleSynth _synth;

    public SampleZone? Zone { get; private set; }
    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime TriggerTime { get; private set; }

    // Playback state
    private double _position;
    private double _playbackRate;
    private bool _pingPongForward = true;
    private float _velocityGain;

    // Envelopes
    private readonly Envelope _ampEnvelope;
    private readonly Envelope _filterEnvelope;
    private readonly Envelope _pitchEnvelope;

    // Filter state (per-voice state-variable filter)
    private float _filterLow, _filterBand, _filterHigh;

    public SamplerVoice(int outputSampleRate, SampleSynth synth)
    {
        _outputSampleRate = outputSampleRate;
        _synth = synth;

        _ampEnvelope = new Envelope(0.001, 0.1, 1.0, 0.3);
        _filterEnvelope = new Envelope(0.001, 0.2, 0.5, 0.3);
        _pitchEnvelope = new Envelope(0.001, 0.1, 0.0, 0.1);
    }

    public void Trigger(SampleZone zone, int note, int velocity)
    {
        Zone = zone;
        Note = note;
        Velocity = velocity;
        IsActive = true;
        TriggerTime = DateTime.Now;

        // Calculate playback rate for pitch
        int semitones = note - zone.RootNote;
        double tuningSemitones = zone.Tune + zone.FineTune / 100.0;
        _playbackRate = Math.Pow(2.0, (semitones + tuningSemitones) / 12.0);

        // Adjust for sample rate difference
        _playbackRate *= (double)zone.SampleRate / _outputSampleRate;

        // Start position
        _position = zone.Reverse ? zone.LengthInFrames - 1 : 0;
        _pingPongForward = !zone.Reverse;

        // Velocity scaling
        _velocityGain = velocity / 127f;

        // Reset filter state
        _filterLow = _filterBand = _filterHigh = 0;

        // Copy envelope settings from synth and trigger
        CopyEnvelopeSettings(_ampEnvelope, _synth.AmpEnvelope);
        CopyEnvelopeSettings(_filterEnvelope, _synth.FilterEnvelope);
        CopyEnvelopeSettings(_pitchEnvelope, _synth.PitchEnvelope);

        _ampEnvelope.Trigger(velocity);
        _filterEnvelope.Trigger(velocity);
        _pitchEnvelope.Trigger(velocity);
    }

    private static void CopyEnvelopeSettings(Envelope dest, Envelope src)
    {
        dest.Attack = src.Attack;
        dest.Decay = src.Decay;
        dest.Sustain = src.Sustain;
        dest.Release = src.Release;
        dest.VelocitySensitivity = src.VelocitySensitivity;
    }

    public void Release()
    {
        _ampEnvelope.Release_Gate();
        _filterEnvelope.Release_Gate();
        _pitchEnvelope.Release_Gate();
    }

    public void Process(float[] buffer, int offset, int count, double deltaTime)
    {
        if (!IsActive || Zone == null) return;

        var zone = Zone;
        int channels = zone.Channels;
        float[] audioData = zone.AudioData;

        // Get modulation values
        double pitchEnvValue = _pitchEnvelope.Process(deltaTime);
        double filterEnvValue = _filterEnvelope.Process(deltaTime);
        double ampEnvValue = _ampEnvelope.Process(deltaTime);

        if (!_ampEnvelope.IsActive)
        {
            IsActive = false;
            return;
        }

        // Calculate effective playback rate with pitch modulation
        double pitchModSemitones = _synth.PitchBend * _synth.PitchBendRange;
        pitchModSemitones += pitchEnvValue * _synth.PitchEnvelopeAmount;

        // Add LFO pitch modulation
        if (_synth.PitchLFO != null && _synth.PitchLFO.Enabled)
        {
            pitchModSemitones += _synth.PitchLFO.GetValue(_outputSampleRate) * _synth.PitchLFOAmount;
        }

        double effectiveRate = _playbackRate * Math.Pow(2.0, pitchModSemitones / 12.0);

        // Calculate filter coefficients
        float cutoff = _synth.FilterCutoff;
        cutoff += (float)(filterEnvValue * _synth.FilterEnvelopeAmount);

        if (_synth.FilterLFO != null && _synth.FilterLFO.Enabled)
        {
            cutoff += (float)(_synth.FilterLFO.GetValue(_outputSampleRate) * _synth.FilterLFOAmount);
        }

        cutoff = Math.Clamp(cutoff, 20f, 20000f);
        float f = 2f * MathF.Sin(MathF.PI * cutoff / _outputSampleRate);
        float q = 1f / _synth.FilterResonance;

        // Calculate amplitude modulation
        float ampMod = 1f;
        if (_synth.AmpLFO != null && _synth.AmpLFO.Enabled)
        {
            ampMod = 1f + (float)(_synth.AmpLFO.GetValue(_outputSampleRate) * _synth.AmpLFOAmount);
            ampMod = Math.Max(0f, ampMod);
        }

        // Process samples
        for (int i = 0; i < count; i += 2)
        {
            // Get sample position
            int framePos = (int)_position;
            double frac = _position - framePos;

            // Check bounds
            if (framePos < 0 || framePos >= zone.LengthInFrames)
            {
                HandleLoopEnd(zone);
                if (!IsActive) break;
                framePos = (int)_position;
                frac = _position - framePos;
            }

            // Read sample with linear interpolation
            float left, right;
            int sampleIndex = framePos * channels;

            if (sampleIndex >= 0 && sampleIndex < audioData.Length - channels)
            {
                if (channels >= 2)
                {
                    left = Lerp(audioData[sampleIndex], audioData[sampleIndex + channels], (float)frac);
                    right = Lerp(audioData[sampleIndex + 1], audioData[sampleIndex + channels + 1], (float)frac);
                }
                else
                {
                    float mono = Lerp(audioData[sampleIndex], audioData[sampleIndex + 1], (float)frac);
                    left = right = mono;
                }
            }
            else
            {
                left = right = 0;
            }

            // Apply loop crossfade if needed
            if (zone.LoopMode != SampleLoopMode.None && zone.LoopCrossfade > 0)
            {
                ApplyLoopCrossfade(zone, framePos, ref left, ref right);
            }

            // Apply filter
            if (_synth.FilterEnabled)
            {
                float input = (left + right) * 0.5f;
                _filterLow += f * _filterBand;
                _filterHigh = input - _filterLow - q * _filterBand;
                _filterBand += f * _filterHigh;

                float filtered = _synth.FilterType switch
                {
                    SamplerFilterType.Lowpass => _filterLow,
                    SamplerFilterType.Highpass => _filterHigh,
                    SamplerFilterType.Bandpass => _filterBand,
                    _ => input
                };

                // Mix filtered signal back to stereo
                float filterMix = _synth.FilterMix;
                left = left * (1 - filterMix) + filtered * filterMix;
                right = right * (1 - filterMix) + filtered * filterMix;
            }

            // Apply pan from zone
            float panL = zone.Pan <= 0 ? 1f : 1f - zone.Pan;
            float panR = zone.Pan >= 0 ? 1f : 1f + zone.Pan;

            // Apply all gains
            float gain = zone.Volume * _velocityGain * (float)ampEnvValue * ampMod * _synth.Volume;
            buffer[offset + i] += left * gain * panL;
            buffer[offset + i + 1] += right * gain * panR;

            // Advance position
            if (_pingPongForward)
            {
                _position += effectiveRate;
            }
            else
            {
                _position -= effectiveRate;
            }
        }
    }

    private void HandleLoopEnd(SampleZone zone)
    {
        switch (zone.LoopMode)
        {
            case SampleLoopMode.None:
                IsActive = false;
                break;

            case SampleLoopMode.Forward:
                if (_position >= zone.LengthInFrames || _position >= zone.LoopEnd && zone.LoopEnd > 0)
                {
                    _position = zone.LoopStart;
                }
                break;

            case SampleLoopMode.PingPong:
                int loopEnd = zone.LoopEnd > 0 ? zone.LoopEnd : zone.LengthInFrames - 1;
                if (_pingPongForward && _position >= loopEnd)
                {
                    _position = loopEnd;
                    _pingPongForward = false;
                }
                else if (!_pingPongForward && _position <= zone.LoopStart)
                {
                    _position = zone.LoopStart;
                    _pingPongForward = true;
                }
                break;

            case SampleLoopMode.Reverse:
                if (_position < 0)
                {
                    if (zone.LoopEnd > 0 || zone.LoopStart > 0)
                    {
                        _position = zone.LoopEnd > 0 ? zone.LoopEnd : zone.LengthInFrames - 1;
                    }
                    else
                    {
                        IsActive = false;
                    }
                }
                break;
        }
    }

    private void ApplyLoopCrossfade(SampleZone zone, int framePos, ref float left, ref float right)
    {
        int loopEnd = zone.LoopEnd > 0 ? zone.LoopEnd : zone.LengthInFrames - 1;
        int crossfadeStart = loopEnd - zone.LoopCrossfade;

        if (framePos >= crossfadeStart && framePos < loopEnd)
        {
            float fadeProgress = (float)(framePos - crossfadeStart) / zone.LoopCrossfade;
            int loopStartPos = zone.LoopStart + (framePos - crossfadeStart);

            if (loopStartPos < zone.LengthInFrames)
            {
                int sampleIndex = loopStartPos * zone.Channels;
                if (sampleIndex >= 0 && sampleIndex < zone.AudioData.Length - zone.Channels)
                {
                    float crossLeft = zone.AudioData[sampleIndex];
                    float crossRight = zone.Channels >= 2 ? zone.AudioData[sampleIndex + 1] : crossLeft;

                    // Crossfade using equal-power
                    float fadeOut = MathF.Cos(fadeProgress * MathF.PI * 0.5f);
                    float fadeIn = MathF.Sin(fadeProgress * MathF.PI * 0.5f);

                    left = left * fadeOut + crossLeft * fadeIn;
                    right = right * fadeOut + crossRight * fadeIn;
                }
            }
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}

/// <summary>
/// Professional sample-based synthesizer with multi-sample mapping, velocity layers,
/// round-robin, loop modes, envelopes, filters, and LFO modulation.
/// </summary>
public class SampleSynth : ISynth
{
    private readonly WaveFormat _waveFormat;
    private readonly List<SampleZone> _zones = new();
    private readonly List<SamplerVoice> _voices = new();
    private readonly Dictionary<int, int> _roundRobinCounters = new();
    private readonly object _lock = new();

    /// <summary>Synth name</summary>
    public string Name { get; set; } = "SampleSynth";

    /// <summary>Audio format</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Maximum polyphony</summary>
    public int MaxVoices { get; set; } = 32;

    /// <summary>Master volume (0-1)</summary>
    public float Volume { get; set; } = 1.0f;

    // Envelopes
    /// <summary>Amplitude envelope</summary>
    public Envelope AmpEnvelope { get; }

    /// <summary>Filter envelope</summary>
    public Envelope FilterEnvelope { get; }

    /// <summary>Pitch envelope</summary>
    public Envelope PitchEnvelope { get; }

    /// <summary>Pitch envelope amount in semitones</summary>
    public float PitchEnvelopeAmount { get; set; } = 0f;

    // Filter
    /// <summary>Enable filter</summary>
    public bool FilterEnabled { get; set; } = false;

    /// <summary>Filter type</summary>
    public SamplerFilterType FilterType { get; set; } = SamplerFilterType.Lowpass;

    /// <summary>Filter cutoff frequency (20-20000 Hz)</summary>
    public float FilterCutoff { get; set; } = 10000f;

    /// <summary>Filter resonance (0.1-10)</summary>
    public float FilterResonance { get; set; } = 0.707f;

    /// <summary>Filter envelope amount (0-10000)</summary>
    public float FilterEnvelopeAmount { get; set; } = 0f;

    /// <summary>Filter wet/dry mix (0-1)</summary>
    public float FilterMix { get; set; } = 1f;

    // LFOs
    /// <summary>LFO for pitch modulation</summary>
    public LFO? PitchLFO { get; set; }

    /// <summary>Pitch LFO amount in semitones</summary>
    public float PitchLFOAmount { get; set; } = 0f;

    /// <summary>LFO for filter modulation</summary>
    public LFO? FilterLFO { get; set; }

    /// <summary>Filter LFO amount in Hz</summary>
    public float FilterLFOAmount { get; set; } = 0f;

    /// <summary>LFO for amplitude modulation (tremolo)</summary>
    public LFO? AmpLFO { get; set; }

    /// <summary>Amplitude LFO amount (0-1)</summary>
    public float AmpLFOAmount { get; set; } = 0f;

    // MIDI
    /// <summary>Pitch bend range in semitones</summary>
    public float PitchBendRange { get; set; } = 2f;

    /// <summary>Current pitch bend (-1 to 1)</summary>
    public float PitchBend { get; set; } = 0f;

    /// <summary>Mod wheel value (0-1)</summary>
    public float ModWheel { get; set; } = 0f;

    /// <summary>Velocity sensitivity (0-1)</summary>
    public float VelocitySensitivity { get; set; } = 1f;

    /// <summary>
    /// Creates a new SampleSynth
    /// </summary>
    public SampleSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);

        // Initialize envelopes with defaults
        AmpEnvelope = new Envelope(0.001, 0.0, 1.0, 0.1);
        FilterEnvelope = new Envelope(0.01, 0.3, 0.5, 0.2);
        PitchEnvelope = new Envelope(0.001, 0.1, 0.0, 0.1);
    }

    /// <summary>
    /// Load a sample from file
    /// </summary>
    public SampleZone? LoadSample(string path, int rootNote = 60)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"[SampleSynth] Sample not found: {path}");
            return null;
        }

        try
        {
            using var reader = new AudioFileReader(path);
            var sampleData = new List<float>();
            var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
            int samplesRead;

            while ((samplesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    sampleData.Add(buffer[i]);
                }
            }

            // Convert to stereo if mono
            float[] audioData;
            int channels;

            if (reader.WaveFormat.Channels == 1)
            {
                audioData = new float[sampleData.Count * 2];
                for (int i = 0; i < sampleData.Count; i++)
                {
                    audioData[i * 2] = sampleData[i];
                    audioData[i * 2 + 1] = sampleData[i];
                }
                channels = 2;
            }
            else
            {
                audioData = sampleData.ToArray();
                channels = reader.WaveFormat.Channels;
            }

            var zone = new SampleZone
            {
                Name = Path.GetFileNameWithoutExtension(path),
                FilePath = path,
                AudioData = audioData,
                SampleRate = reader.WaveFormat.SampleRate,
                Channels = channels,
                RootNote = rootNote,
                LoopEnd = audioData.Length / channels
            };

            lock (_lock)
            {
                _zones.Add(zone);
            }

            Console.WriteLine($"[SampleSynth] Loaded: {zone.Name} ({zone.LengthInFrames} frames, root={rootNote})");
            return zone;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SampleSynth] Error loading {path}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Add a zone directly
    /// </summary>
    public void AddZone(SampleZone zone)
    {
        lock (_lock)
        {
            _zones.Add(zone);
        }
    }

    /// <summary>
    /// Remove a zone
    /// </summary>
    public bool RemoveZone(SampleZone zone)
    {
        lock (_lock)
        {
            return _zones.Remove(zone);
        }
    }

    /// <summary>
    /// Clear all zones
    /// </summary>
    public void ClearZones()
    {
        lock (_lock)
        {
            _zones.Clear();
        }
    }

    /// <summary>
    /// Get all zones
    /// </summary>
    public IReadOnlyList<SampleZone> Zones
    {
        get
        {
            lock (_lock)
            {
                return _zones.ToArray();
            }
        }
    }

    /// <summary>
    /// Find matching zone for a note/velocity with round-robin support
    /// </summary>
    private SampleZone? FindZone(int note, int velocity)
    {
        lock (_lock)
        {
            // Find all matching zones
            var matches = _zones.Where(z => z.Matches(note, velocity)).ToList();
            if (matches.Count == 0) return null;

            // Group by round-robin group
            var grouped = matches.GroupBy(z => z.RoundRobinGroup).ToList();

            // If there's a round-robin group (group > 0), use round-robin selection
            var rrGroup = grouped.FirstOrDefault(g => g.Key > 0);
            if (rrGroup != null && rrGroup.Count() > 1)
            {
                int group = rrGroup.Key;
                if (!_roundRobinCounters.TryGetValue(group, out int counter))
                {
                    counter = 0;
                }

                var rrZones = rrGroup.OrderBy(z => z.RoundRobinIndex).ToList();
                var zone = rrZones[counter % rrZones.Count];

                _roundRobinCounters[group] = (counter + 1) % rrZones.Count;
                return zone;
            }

            // Otherwise return first match
            return matches[0];
        }
    }

    private SamplerVoice? GetFreeVoice()
    {
        lock (_lock)
        {
            // Find inactive voice
            foreach (var voice in _voices)
            {
                if (!voice.IsActive) return voice;
            }

            // Create new voice if under limit
            if (_voices.Count < MaxVoices)
            {
                var voice = new SamplerVoice(_waveFormat.SampleRate, this);
                _voices.Add(voice);
                return voice;
            }

            // Voice stealing - steal oldest
            SamplerVoice? oldest = null;
            DateTime oldestTime = DateTime.MaxValue;

            foreach (var voice in _voices)
            {
                if (voice.TriggerTime < oldestTime)
                {
                    oldestTime = voice.TriggerTime;
                    oldest = voice;
                }
            }

            return oldest;
        }
    }

    #region ISynth Implementation

    public void NoteOn(int note, int velocity)
    {
        var zone = FindZone(note, velocity);
        if (zone == null) return;

        var voice = GetFreeVoice();
        if (voice == null) return;

        voice.Trigger(zone, note, velocity);
    }

    public void NoteOff(int note)
    {
        lock (_lock)
        {
            foreach (var voice in _voices.Where(v => v.IsActive && v.Note == note))
            {
                voice.Release();
            }
        }
    }

    public void AllNotesOff()
    {
        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                voice.Release();
            }
        }
    }

    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume":
                Volume = Math.Clamp(value, 0f, 2f);
                break;
            case "pitchbend":
                PitchBend = Math.Clamp(value, -1f, 1f);
                break;
            case "pitchbendrange":
                PitchBendRange = Math.Clamp(value, 0f, 24f);
                break;
            case "modwheel":
                ModWheel = Math.Clamp(value, 0f, 1f);
                break;

            // Amp envelope
            case "amp_attack":
                AmpEnvelope.Attack = value;
                break;
            case "amp_decay":
                AmpEnvelope.Decay = value;
                break;
            case "amp_sustain":
                AmpEnvelope.Sustain = Math.Clamp(value, 0f, 1f);
                break;
            case "amp_release":
                AmpEnvelope.Release = value;
                break;

            // Filter
            case "filter_enabled":
                FilterEnabled = value > 0.5f;
                break;
            case "filter_cutoff":
                FilterCutoff = Math.Clamp(value, 20f, 20000f);
                break;
            case "filter_resonance":
                FilterResonance = Math.Clamp(value, 0.1f, 10f);
                break;
            case "filter_env_amount":
                FilterEnvelopeAmount = value;
                break;

            // Filter envelope
            case "filter_attack":
                FilterEnvelope.Attack = value;
                break;
            case "filter_decay":
                FilterEnvelope.Decay = value;
                break;
            case "filter_sustain":
                FilterEnvelope.Sustain = Math.Clamp(value, 0f, 1f);
                break;
            case "filter_release":
                FilterEnvelope.Release = value;
                break;

            // Pitch
            case "pitch_env_amount":
                PitchEnvelopeAmount = value;
                break;
            case "pitch_attack":
                PitchEnvelope.Attack = value;
                break;
            case "pitch_decay":
                PitchEnvelope.Decay = value;
                break;

            // LFO amounts
            case "pitch_lfo_amount":
                PitchLFOAmount = value;
                break;
            case "filter_lfo_amount":
                FilterLFOAmount = value;
                break;
            case "amp_lfo_amount":
                AmpLFOAmount = value;
                break;
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        // Clear buffer
        Array.Clear(buffer, offset, count);

        double deltaTime = 1.0 / _waveFormat.SampleRate;

        lock (_lock)
        {
            foreach (var voice in _voices)
            {
                if (voice.IsActive)
                {
                    voice.Process(buffer, offset, count, deltaTime);
                }
            }
        }

        return count;
    }

    #endregion

    #region Presets

    /// <summary>
    /// Create a preset configured for piano samples
    /// </summary>
    public static SampleSynth CreatePianoPreset()
    {
        var synth = new SampleSynth
        {
            Name = "Piano",
            VelocitySensitivity = 0.8f
        };

        synth.AmpEnvelope.Attack = 0.001;
        synth.AmpEnvelope.Decay = 2.0;
        synth.AmpEnvelope.Sustain = 0.0;
        synth.AmpEnvelope.Release = 0.5;

        return synth;
    }

    /// <summary>
    /// Create a preset configured for pad samples
    /// </summary>
    public static SampleSynth CreatePadPreset()
    {
        var synth = new SampleSynth
        {
            Name = "Pad",
            FilterEnabled = true,
            FilterCutoff = 2000f,
            FilterEnvelopeAmount = 3000f
        };

        synth.AmpEnvelope.Attack = 0.5;
        synth.AmpEnvelope.Decay = 1.0;
        synth.AmpEnvelope.Sustain = 0.8;
        synth.AmpEnvelope.Release = 1.5;

        synth.FilterEnvelope.Attack = 0.8;
        synth.FilterEnvelope.Decay = 1.5;
        synth.FilterEnvelope.Sustain = 0.3;
        synth.FilterEnvelope.Release = 1.0;

        // Add vibrato
        synth.PitchLFO = new LFO(LfoWaveform.Sine, 5.0) { Depth = 1.0f };
        synth.PitchLFOAmount = 0.1f;

        return synth;
    }

    /// <summary>
    /// Create a preset configured for drum kit
    /// </summary>
    public static SampleSynth CreateDrumKitPreset()
    {
        var synth = new SampleSynth
        {
            Name = "DrumKit",
            VelocitySensitivity = 1.0f
        };

        synth.AmpEnvelope.Attack = 0.0;
        synth.AmpEnvelope.Decay = 0.0;
        synth.AmpEnvelope.Sustain = 1.0;
        synth.AmpEnvelope.Release = 0.1;

        return synth;
    }

    /// <summary>
    /// Create a preset configured for looping ambient textures
    /// </summary>
    public static SampleSynth CreateAmbientPreset()
    {
        var synth = new SampleSynth
        {
            Name = "Ambient",
            FilterEnabled = true,
            FilterCutoff = 5000f,
            FilterResonance = 1.5f
        };

        synth.AmpEnvelope.Attack = 2.0;
        synth.AmpEnvelope.Decay = 0.0;
        synth.AmpEnvelope.Sustain = 1.0;
        synth.AmpEnvelope.Release = 3.0;

        // Slow filter LFO
        synth.FilterLFO = new LFO(LfoWaveform.Sine, 0.2) { Depth = 1.0f };
        synth.FilterLFOAmount = 2000f;

        return synth;
    }

    #endregion
}
