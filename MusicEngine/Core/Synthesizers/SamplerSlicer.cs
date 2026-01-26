//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: REX-style beat slicer synthesizer with MIDI-triggered slice playback.

using NAudio.Wave;
using MusicEngine.Core.Synthesizers.Slicer;

namespace MusicEngine.Core.Synthesizers;

/// <summary>
/// Playback mode for slices.
/// </summary>
public enum SlicePlayMode
{
    /// <summary>Play slice once from start to end.</summary>
    OneShot,
    /// <summary>Loop the slice continuously.</summary>
    Loop,
    /// <summary>Play while note is held, stop on release.</summary>
    Gate
}

/// <summary>
/// REX-style beat slicer that divides audio into slices at transient points
/// and allows each slice to be triggered independently via MIDI notes.
/// Supports various slice detection modes, per-slice pitch/gain/reverse,
/// and multiple playback modes.
/// </summary>
public class SamplerSlicer : ISynth, ISampleProvider
{
    private float[]? _audioData;
    private int _audioSampleRate;
    private readonly List<Slice> _slices = new();
    private readonly List<SliceVoice> _activeVoices = new();
    private readonly List<SliceVoice> _voicePool = new();
    private readonly object _lock = new();
    private readonly SliceDetector _detector = new();

    /// <summary>
    /// Output audio format.
    /// </summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Synth name.
    /// </summary>
    public string Name { get; set; } = "SamplerSlicer";

    /// <summary>
    /// Number of slices in the current audio.
    /// </summary>
    public int SliceCount
    {
        get
        {
            lock (_lock) return _slices.Count;
        }
    }

    /// <summary>
    /// Gets a read-only list of all slices.
    /// </summary>
    public IReadOnlyList<Slice> Slices
    {
        get
        {
            lock (_lock) return _slices.ToArray();
        }
    }

    /// <summary>
    /// Gets or sets the playback mode for slices.
    /// </summary>
    public SlicePlayMode PlayMode { get; set; } = SlicePlayMode.OneShot;

    /// <summary>
    /// Whether to quantize slice playback to tempo grid.
    /// </summary>
    public bool QuantizeToTempo { get; set; }

    /// <summary>
    /// Current BPM for tempo-related features.
    /// </summary>
    public double Bpm { get; set; } = 120;

    /// <summary>
    /// Master volume (0.0 to 2.0).
    /// </summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>
    /// Maximum polyphony (simultaneous voices).
    /// </summary>
    public int MaxVoices { get; set; } = 32;

    /// <summary>
    /// Velocity sensitivity (0.0 to 1.0).
    /// </summary>
    public float VelocitySensitivity { get; set; } = 0.5f;

    /// <summary>
    /// Attack time for amplitude envelope in seconds.
    /// </summary>
    public double AttackTime { get; set; } = 0.001;

    /// <summary>
    /// Release time for amplitude envelope in seconds.
    /// </summary>
    public double ReleaseTime { get; set; } = 0.01;

    /// <summary>
    /// Crossfade time in samples for slice boundaries to reduce clicks.
    /// </summary>
    public int CrossfadeSamples { get; set; } = 64;

    /// <summary>
    /// Gets the underlying slice detector for configuration.
    /// </summary>
    public SliceDetector Detector => _detector;

    /// <summary>
    /// Event raised when slices are detected/updated.
    /// </summary>
    public event EventHandler? SlicesChanged;

    /// <summary>
    /// Creates a new SamplerSlicer with the specified format.
    /// </summary>
    /// <param name="sampleRate">Output sample rate (default: from Settings).</param>
    /// <param name="channels">Number of output channels (default: 2).</param>
    public SamplerSlicer(int? sampleRate = null, int channels = 2)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, channels);
    }

    /// <summary>
    /// Loads audio data directly from a float array.
    /// </summary>
    /// <param name="audioData">Mono audio samples.</param>
    /// <param name="sampleRate">Sample rate of the audio data.</param>
    public void LoadAudio(float[] audioData, int? sampleRate = null)
    {
        lock (_lock)
        {
            _audioData = audioData;
            _audioSampleRate = sampleRate ?? WaveFormat.SampleRate;
            _slices.Clear();
            _activeVoices.Clear();
        }

        SlicesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Loads audio from a file.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    public void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Audio file not found: {filePath}");
        }

        using var reader = new AudioFileReader(filePath);
        var samples = new List<float>();
        var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
        int read;

        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            // Convert to mono if stereo
            if (reader.WaveFormat.Channels == 2)
            {
                for (int i = 0; i < read; i += 2)
                {
                    samples.Add((buffer[i] + buffer[i + 1]) * 0.5f);
                }
            }
            else
            {
                for (int i = 0; i < read; i++)
                {
                    samples.Add(buffer[i]);
                }
            }
        }

        LoadAudio(samples.ToArray(), reader.WaveFormat.SampleRate);
    }

    /// <summary>
    /// Automatically detects and creates slices using the specified mode.
    /// </summary>
    /// <param name="mode">Slice detection mode.</param>
    /// <param name="bpm">BPM for beat-based slicing.</param>
    /// <param name="beatsPerSlice">Beats per slice for beat mode.</param>
    /// <param name="sliceCount">Number of slices for equal mode.</param>
    public void AutoSlice(SliceMode mode, double? bpm = null, int beatsPerSlice = 1, int sliceCount = 16)
    {
        if (_audioData == null || _audioData.Length == 0) return;

        double useBpm = bpm ?? Bpm;

        lock (_lock)
        {
            _slices.Clear();
            var detected = _detector.DetectSlices(_audioData, _audioSampleRate, mode,
                useBpm, beatsPerSlice, sliceCount);
            _slices.AddRange(detected);

            // Snap to zero crossings to reduce clicks
            _detector.SnapToZeroCrossings(_slices, _audioData);
        }

        SlicesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Adds a slice manually at the specified sample range.
    /// </summary>
    /// <param name="startSample">Start position in samples.</param>
    /// <param name="endSample">End position in samples.</param>
    /// <returns>The created slice.</returns>
    public Slice AddSlice(long startSample, long endSample)
    {
        Slice slice;
        lock (_lock)
        {
            int index = _slices.Count;
            slice = new Slice(index, startSample, endSample);
            _slices.Add(slice);
        }

        SlicesChanged?.Invoke(this, EventArgs.Empty);
        return slice;
    }

    /// <summary>
    /// Removes a slice by index.
    /// </summary>
    /// <param name="index">Index of the slice to remove.</param>
    /// <returns>True if removed, false if index out of range.</returns>
    public bool RemoveSlice(int index)
    {
        bool removed;
        lock (_lock)
        {
            if (index < 0 || index >= _slices.Count)
            {
                return false;
            }

            _slices.RemoveAt(index);

            // Re-index remaining slices
            for (int i = index; i < _slices.Count; i++)
            {
                // Create new slice with updated index
                var old = _slices[i];
                _slices[i] = new Slice(i, old.StartSample, old.EndSample)
                {
                    Gain = old.Gain,
                    Pitch = old.Pitch,
                    Reverse = old.Reverse,
                    MidiNote = old.MidiNote,
                    Name = old.Name
                };
            }

            removed = true;
        }

        SlicesChanged?.Invoke(this, EventArgs.Empty);
        return removed;
    }

    /// <summary>
    /// Clears all slices.
    /// </summary>
    public void ClearSlices()
    {
        lock (_lock)
        {
            _slices.Clear();
            _activeVoices.Clear();
        }

        SlicesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Assigns MIDI notes to slices sequentially starting from the specified note.
    /// </summary>
    /// <param name="startNote">First MIDI note (default: 36 = C1).</param>
    public void AssignMidiNotes(int startNote = 36)
    {
        lock (_lock)
        {
            for (int i = 0; i < _slices.Count; i++)
            {
                _slices[i].MidiNote = startNote + i;
            }
        }
    }

    /// <summary>
    /// Gets a slice by its assigned MIDI note.
    /// </summary>
    /// <param name="midiNote">MIDI note number.</param>
    /// <returns>The slice assigned to this note, or null if not found.</returns>
    public Slice? GetSliceByNote(int midiNote)
    {
        lock (_lock)
        {
            return _slices.FirstOrDefault(s => s.MidiNote == midiNote);
        }
    }

    /// <summary>
    /// Gets a slice by index.
    /// </summary>
    /// <param name="index">Slice index.</param>
    /// <returns>The slice, or null if index out of range.</returns>
    public Slice? GetSlice(int index)
    {
        lock (_lock)
        {
            return index >= 0 && index < _slices.Count ? _slices[index] : null;
        }
    }

    #region ISynth Implementation

    /// <summary>
    /// Triggers a slice by MIDI note.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        if (_audioData == null || velocity == 0)
        {
            NoteOff(note);
            return;
        }

        var slice = GetSliceByNote(note);
        if (slice == null) return;

        lock (_lock)
        {
            // Get or create voice
            var voice = GetFreeVoice();
            if (voice == null) return;

            voice.Trigger(slice, velocity);
            _activeVoices.Add(voice);
        }
    }

    /// <summary>
    /// Releases a slice by MIDI note.
    /// </summary>
    public void NoteOff(int note)
    {
        lock (_lock)
        {
            foreach (var voice in _activeVoices.Where(v => v.Slice?.MidiNote == note))
            {
                voice.Release();
            }
        }
    }

    /// <summary>
    /// Stops all playing slices.
    /// </summary>
    public void AllNotesOff()
    {
        lock (_lock)
        {
            foreach (var voice in _activeVoices)
            {
                voice.Release();
            }
        }
    }

    /// <summary>
    /// Sets a synth parameter by name.
    /// </summary>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume":
                Volume = Math.Clamp(value, 0f, 2f);
                break;
            case "velocity_sensitivity":
                VelocitySensitivity = Math.Clamp(value, 0f, 1f);
                break;
            case "attack":
                AttackTime = Math.Max(0.0, value);
                break;
            case "release":
                ReleaseTime = Math.Max(0.0, value);
                break;
            case "crossfade":
                CrossfadeSamples = Math.Max(0, (int)value);
                break;
            case "bpm":
                Bpm = Math.Clamp(value, 20, 300);
                break;
            case "transient_threshold":
                _detector.TransientThreshold = Math.Clamp(value, 0f, 1f);
                break;
            case "sensitivity":
                _detector.Sensitivity = Math.Clamp(value, 0.1f, 10f);
                break;
        }
    }

    #endregion

    #region ISampleProvider Implementation

    /// <summary>
    /// Reads audio samples, mixing all active voices.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        // Clear buffer
        Array.Clear(buffer, offset, count);

        if (_audioData == null) return count;

        lock (_lock)
        {
            // Process all active voices
            for (int i = _activeVoices.Count - 1; i >= 0; i--)
            {
                var voice = _activeVoices[i];

                if (!voice.IsActive)
                {
                    _activeVoices.RemoveAt(i);
                    _voicePool.Add(voice);
                    continue;
                }

                voice.Process(buffer, offset, count, _audioData, WaveFormat.Channels,
                    Volume, VelocitySensitivity, CrossfadeSamples,
                    (float)AttackTime, (float)ReleaseTime, WaveFormat.SampleRate,
                    _audioSampleRate, PlayMode);
            }
        }

        return count;
    }

    #endregion

    #region Voice Management

    private SliceVoice? GetFreeVoice()
    {
        // Try to get from pool
        if (_voicePool.Count > 0)
        {
            var voice = _voicePool[_voicePool.Count - 1];
            _voicePool.RemoveAt(_voicePool.Count - 1);
            return voice;
        }

        // Create new voice if under limit
        if (_activeVoices.Count < MaxVoices)
        {
            return new SliceVoice();
        }

        // Steal oldest voice
        if (_activeVoices.Count > 0)
        {
            var oldest = _activeVoices[0];
            oldest.Release();
            return oldest;
        }

        return null;
    }

    #endregion

    #region SliceVoice

    /// <summary>
    /// Internal voice for playing a single slice.
    /// </summary>
    private class SliceVoice
    {
        public Slice? Slice { get; private set; }
        public bool IsActive { get; private set; }

        private double _position;
        private int _velocity;
        private float _gain;

        // Envelope state
        private enum EnvelopeState { Attack, Sustain, Release, Idle }
        private EnvelopeState _envState = EnvelopeState.Idle;
        private float _envLevel;
        private float _envDelta;

        public void Trigger(Slice slice, int velocity)
        {
            Slice = slice;
            _velocity = velocity;
            _gain = slice.Gain;
            IsActive = true;

            // Start position
            _position = slice.Reverse ? slice.LengthSamples - 1 : 0;

            // Start envelope in attack
            _envState = EnvelopeState.Attack;
            _envLevel = 0;
        }

        public void Release()
        {
            if (_envState != EnvelopeState.Idle)
            {
                _envState = EnvelopeState.Release;
            }
        }

        public void Process(float[] buffer, int offset, int count,
            float[] audioData, int outputChannels,
            float masterVolume, float velocitySensitivity, int crossfadeSamples,
            float attackTime, float releaseTime, int outputSampleRate,
            int audioSampleRate, SlicePlayMode playMode)
        {
            if (!IsActive || Slice == null) return;

            // Calculate playback rate (for pitch and sample rate conversion)
            double playbackRate = Slice.Pitch * ((double)audioSampleRate / outputSampleRate);
            if (Slice.Reverse) playbackRate = -playbackRate;

            // Calculate envelope rates
            float attackRate = attackTime > 0 ? 1.0f / (attackTime * outputSampleRate) : 1.0f;
            float releaseRate = releaseTime > 0 ? 1.0f / (releaseTime * outputSampleRate) : 1.0f;

            // Velocity gain
            float velGain = 1.0f - velocitySensitivity + velocitySensitivity * (_velocity / 127f);

            // Process samples
            int framesCount = count / outputChannels;

            for (int frame = 0; frame < framesCount; frame++)
            {
                // Process envelope
                switch (_envState)
                {
                    case EnvelopeState.Attack:
                        _envLevel += attackRate;
                        if (_envLevel >= 1.0f)
                        {
                            _envLevel = 1.0f;
                            _envState = EnvelopeState.Sustain;
                        }
                        break;

                    case EnvelopeState.Release:
                        _envLevel -= releaseRate;
                        if (_envLevel <= 0)
                        {
                            _envLevel = 0;
                            _envState = EnvelopeState.Idle;
                            IsActive = false;
                            return;
                        }
                        break;
                }

                // Get sample position in source audio
                long samplePos = Slice.StartSample + (long)_position;
                double frac = _position - Math.Floor(_position);

                // Check if we're at the end of the slice
                if (!Slice.Reverse && _position >= Slice.LengthSamples)
                {
                    HandleSliceEnd(playMode);
                    if (!IsActive) return;
                }
                else if (Slice.Reverse && _position < 0)
                {
                    HandleSliceEnd(playMode);
                    if (!IsActive) return;
                }

                // Read sample with linear interpolation
                float sample = 0;
                if (samplePos >= 0 && samplePos < audioData.Length - 1)
                {
                    sample = Lerp(audioData[samplePos], audioData[samplePos + 1], (float)frac);
                }
                else if (samplePos >= 0 && samplePos < audioData.Length)
                {
                    sample = audioData[samplePos];
                }

                // Apply crossfade at slice boundaries
                if (crossfadeSamples > 0)
                {
                    float fadeGain = 1.0f;

                    // Fade in at start
                    if (_position < crossfadeSamples)
                    {
                        fadeGain = (float)_position / crossfadeSamples;
                    }
                    // Fade out at end
                    else if (_position > Slice.LengthSamples - crossfadeSamples)
                    {
                        fadeGain = (float)(Slice.LengthSamples - _position) / crossfadeSamples;
                    }

                    sample *= Math.Max(0, fadeGain);
                }

                // Apply gains
                float finalGain = sample * _gain * velGain * masterVolume * _envLevel;

                // Write to all output channels
                int bufferPos = offset + frame * outputChannels;
                for (int ch = 0; ch < outputChannels; ch++)
                {
                    buffer[bufferPos + ch] += finalGain;
                }

                // Advance position
                _position += Math.Abs(playbackRate);
                if (Slice.Reverse)
                {
                    _position = Slice.LengthSamples - 1 - (Slice.LengthSamples - 1 - _position);
                }
            }
        }

        private void HandleSliceEnd(SlicePlayMode playMode)
        {
            switch (playMode)
            {
                case SlicePlayMode.OneShot:
                    _envState = EnvelopeState.Release;
                    break;

                case SlicePlayMode.Loop:
                    _position = Slice!.Reverse ? Slice.LengthSamples - 1 : 0;
                    break;

                case SlicePlayMode.Gate:
                    if (_envState == EnvelopeState.Sustain)
                    {
                        _position = Slice!.Reverse ? Slice.LengthSamples - 1 : 0;
                    }
                    else
                    {
                        _envState = EnvelopeState.Release;
                    }
                    break;
            }
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Plays all slices in sequence at the current BPM.
    /// Used for previewing the sliced audio.
    /// </summary>
    /// <param name="startSlice">Index of first slice to play.</param>
    /// <param name="endSlice">Index of last slice to play (inclusive, -1 for all).</param>
    public void PlaySequence(int startSlice = 0, int endSlice = -1)
    {
        // This would typically be handled by a sequencer
        // For now, trigger the first slice
        lock (_lock)
        {
            if (startSlice >= 0 && startSlice < _slices.Count)
            {
                var slice = _slices[startSlice];
                if (slice.MidiNote >= 0)
                {
                    NoteOn(slice.MidiNote, 100);
                }
            }
        }
    }

    /// <summary>
    /// Triggers a slice by index (not MIDI note).
    /// </summary>
    /// <param name="sliceIndex">Index of the slice to trigger.</param>
    /// <param name="velocity">Velocity (0-127).</param>
    public void TriggerSlice(int sliceIndex, int velocity = 100)
    {
        if (_audioData == null) return;

        Slice? slice;
        lock (_lock)
        {
            if (sliceIndex < 0 || sliceIndex >= _slices.Count) return;
            slice = _slices[sliceIndex];

            var voice = GetFreeVoice();
            if (voice == null) return;

            voice.Trigger(slice, velocity);
            _activeVoices.Add(voice);
        }
    }

    /// <summary>
    /// Gets the total duration of all slices in seconds.
    /// </summary>
    public double TotalDuration
    {
        get
        {
            if (_audioData == null) return 0;
            return (double)_audioData.Length / _audioSampleRate;
        }
    }

    #endregion
}
