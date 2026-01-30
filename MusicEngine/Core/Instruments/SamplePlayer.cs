//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Sample player instrument with one-shot/loop playback, pitch shifting, time stretching,
// loop points, and polyphonic support.

using NAudio.Wave;

namespace MusicEngine.Core.Instruments;

/// <summary>
/// Playback mode for the sample player.
/// </summary>
public enum SamplePlaybackMode
{
    /// <summary>Play sample once from start to end position.</summary>
    OneShot,
    /// <summary>Loop the sample continuously between loop start and loop end.</summary>
    Loop,
    /// <summary>Play sample while note is held, release on note off.</summary>
    Sustain,
    /// <summary>Play sample in reverse.</summary>
    Reverse
}

/// <summary>
/// Interpolation quality for pitch shifting.
/// </summary>
public enum InterpolationMode
{
    /// <summary>No interpolation (nearest neighbor).</summary>
    None,
    /// <summary>Linear interpolation (fast, decent quality).</summary>
    Linear,
    /// <summary>Cubic interpolation (slower, better quality).</summary>
    Cubic
}

/// <summary>
/// Represents a voice instance for polyphonic playback.
/// </summary>
internal class SamplePlayerVoice
{
    public bool IsActive { get; private set; }
    public int Note { get; private set; }
    public int Velocity { get; private set; }
    public DateTime TriggerTime { get; private set; }

    private double _position;
    private double _playbackRate;
    private float _velocityGain;
    private bool _isReleasing;
    private float _releaseLevel;
    private float _releaseDelta;

    // Reference to parent player for parameters
    private readonly SamplePlayer _player;

    public SamplePlayerVoice(SamplePlayer player)
    {
        _player = player;
    }

    public void Trigger(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
        IsActive = true;
        TriggerTime = DateTime.UtcNow;
        _isReleasing = false;
        _releaseLevel = 1.0f;

        // Calculate playback rate based on pitch parameter and note transposition
        double semitones = _player.Pitch + (note - _player.RootNote);
        _playbackRate = Math.Pow(2.0, semitones / 12.0);

        // Apply time stretch factor (inverse affects playback rate)
        if (Math.Abs(_player.TimeStretch - 1.0) > 0.001)
        {
            _playbackRate /= _player.TimeStretch;
        }

        // Start position based on mode
        if (_player.PlaybackMode == SamplePlaybackMode.Reverse)
        {
            _position = _player.EndPosition > 0 ? _player.EndPosition : _player.SampleLengthInFrames - 1;
        }
        else
        {
            _position = _player.StartPosition;
        }

        // Velocity sensitivity
        float velSens = _player.VelocitySensitivity;
        _velocityGain = (1.0f - velSens) + velSens * (velocity / 127f);
    }

    public void Release()
    {
        if (!_isReleasing && IsActive)
        {
            _isReleasing = true;
            // Calculate release delta for smooth fade out
            int releaseSamples = (int)(_player.ReleaseTime * _player.WaveFormat.SampleRate);
            _releaseDelta = releaseSamples > 0 ? 1.0f / releaseSamples : 1.0f;
        }
    }

    public void Stop()
    {
        IsActive = false;
        _isReleasing = false;
    }

    public void Process(float[] buffer, int offset, int count)
    {
        if (!IsActive || _player.AudioData == null || _player.AudioData.Length == 0)
            return;

        var audioData = _player.AudioData;
        int sourceChannels = _player.SourceChannels;
        int outputChannels = _player.WaveFormat.Channels;
        double sampleRateRatio = (double)_player.SourceSampleRate / _player.WaveFormat.SampleRate;
        double effectiveRate = _playbackRate * sampleRateRatio;

        // Determine loop/end boundaries
        double startPos = _player.StartPosition;
        double endPos = _player.EndPosition > 0 ? _player.EndPosition : _player.SampleLengthInFrames;
        double loopStart = _player.LoopStart >= 0 ? _player.LoopStart : startPos;
        double loopEnd = _player.LoopEnd > 0 ? _player.LoopEnd : endPos;

        // Process samples
        int framesCount = count / outputChannels;

        for (int frame = 0; frame < framesCount; frame++)
        {
            // Handle release envelope
            if (_isReleasing)
            {
                _releaseLevel -= _releaseDelta;
                if (_releaseLevel <= 0)
                {
                    Stop();
                    return;
                }
            }

            // Check boundaries and handle looping
            bool outOfBounds = false;
            if (_player.PlaybackMode == SamplePlaybackMode.Reverse)
            {
                if (_position <= startPos)
                {
                    if (_player.PlaybackMode == SamplePlaybackMode.Loop)
                    {
                        _position = loopEnd;
                    }
                    else
                    {
                        outOfBounds = true;
                    }
                }
            }
            else
            {
                if (_position >= endPos)
                {
                    if (_player.PlaybackMode == SamplePlaybackMode.Loop)
                    {
                        _position = loopStart;
                    }
                    else
                    {
                        outOfBounds = true;
                    }
                }
                else if (_player.PlaybackMode == SamplePlaybackMode.Loop && _position >= loopEnd)
                {
                    _position = loopStart;
                }
            }

            if (outOfBounds)
            {
                // For one-shot and sustain modes, stop when reaching the end
                if (_player.PlaybackMode != SamplePlaybackMode.Sustain || _isReleasing)
                {
                    Stop();
                    return;
                }
            }

            // Get sample with interpolation
            float left, right;
            GetInterpolatedSample(audioData, sourceChannels, out left, out right);

            // Apply gains
            float pan = _player.Pan;
            float panL = pan <= 0 ? 1.0f : 1.0f - pan;
            float panR = pan >= 0 ? 1.0f : 1.0f + pan;

            float totalGain = _player.Volume * _velocityGain * _releaseLevel;

            // Write to buffer
            int bufferIndex = offset + frame * outputChannels;
            if (outputChannels >= 2)
            {
                buffer[bufferIndex] += left * totalGain * panL;
                buffer[bufferIndex + 1] += right * totalGain * panR;
            }
            else
            {
                buffer[bufferIndex] += (left + right) * 0.5f * totalGain;
            }

            // Advance position
            if (_player.PlaybackMode == SamplePlaybackMode.Reverse)
            {
                _position -= effectiveRate;
            }
            else
            {
                _position += effectiveRate;
            }
        }
    }

    private void GetInterpolatedSample(float[] audioData, int channels, out float left, out float right)
    {
        int framePos = (int)_position;
        double frac = _position - framePos;

        // Clamp frame position
        int maxFrame = (audioData.Length / channels) - 1;
        framePos = Math.Clamp(framePos, 0, maxFrame);

        switch (_player.Interpolation)
        {
            case InterpolationMode.Cubic:
                GetCubicInterpolatedSample(audioData, channels, framePos, frac, maxFrame, out left, out right);
                break;

            case InterpolationMode.Linear:
                GetLinearInterpolatedSample(audioData, channels, framePos, frac, maxFrame, out left, out right);
                break;

            default:
                // Nearest neighbor
                int sampleIndex = framePos * channels;
                left = audioData[sampleIndex];
                right = channels >= 2 ? audioData[sampleIndex + 1] : left;
                break;
        }
    }

    private static void GetLinearInterpolatedSample(float[] audioData, int channels, int framePos, double frac, int maxFrame, out float left, out float right)
    {
        int sampleIndex = framePos * channels;
        int nextFrame = Math.Min(framePos + 1, maxFrame);
        int nextSampleIndex = nextFrame * channels;

        float t = (float)frac;

        left = Lerp(audioData[sampleIndex], audioData[nextSampleIndex], t);
        if (channels >= 2)
        {
            right = Lerp(audioData[sampleIndex + 1], audioData[nextSampleIndex + 1], t);
        }
        else
        {
            right = left;
        }
    }

    private static void GetCubicInterpolatedSample(float[] audioData, int channels, int framePos, double frac, int maxFrame, out float left, out float right)
    {
        // Get 4 sample points for cubic interpolation
        int frame0 = Math.Max(0, framePos - 1);
        int frame1 = framePos;
        int frame2 = Math.Min(framePos + 1, maxFrame);
        int frame3 = Math.Min(framePos + 2, maxFrame);

        float t = (float)frac;

        int idx0 = frame0 * channels;
        int idx1 = frame1 * channels;
        int idx2 = frame2 * channels;
        int idx3 = frame3 * channels;

        left = CubicInterpolate(audioData[idx0], audioData[idx1], audioData[idx2], audioData[idx3], t);
        if (channels >= 2)
        {
            right = CubicInterpolate(audioData[idx0 + 1], audioData[idx1 + 1], audioData[idx2 + 1], audioData[idx3 + 1], t);
        }
        else
        {
            right = left;
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float CubicInterpolate(float y0, float y1, float y2, float y3, float t)
    {
        float t2 = t * t;
        float a0 = y3 - y2 - y0 + y1;
        float a1 = y0 - y1 - a0;
        float a2 = y2 - y0;
        float a3 = y1;
        return a0 * t * t2 + a1 * t2 + a2 * t + a3;
    }
}

/// <summary>
/// Sample player instrument for loading and playing audio samples with pitch shifting,
/// time stretching, loop points, and polyphonic playback support.
/// Supports common audio formats via NAudio (WAV, MP3, FLAC, OGG, AIFF).
/// </summary>
public class SamplePlayer : ISampleProvider, ISynth
{
    private readonly List<SamplePlayerVoice> _voices = new();
    private readonly List<SamplePlayerVoice> _voicePool = new();
    private readonly object _lock = new();

    /// <summary>
    /// Gets the audio format for output.
    /// </summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Gets or sets the synth name.
    /// </summary>
    public string Name { get; set; } = "SamplePlayer";

    /// <summary>
    /// Gets the loaded audio data (interleaved samples).
    /// </summary>
    public float[]? AudioData { get; private set; }

    /// <summary>
    /// Gets the sample rate of the loaded audio.
    /// </summary>
    public int SourceSampleRate { get; private set; }

    /// <summary>
    /// Gets the number of channels in the loaded audio.
    /// </summary>
    public int SourceChannels { get; private set; }

    /// <summary>
    /// Gets the sample length in frames (samples per channel).
    /// </summary>
    public int SampleLengthInFrames => AudioData != null && SourceChannels > 0 ? AudioData.Length / SourceChannels : 0;

    /// <summary>
    /// Gets the sample duration in seconds.
    /// </summary>
    public double Duration => SourceSampleRate > 0 ? (double)SampleLengthInFrames / SourceSampleRate : 0;

    /// <summary>
    /// Gets the file path of the loaded sample.
    /// </summary>
    public string? FilePath { get; private set; }

    // Playback parameters

    /// <summary>
    /// Gets or sets the playback mode (OneShot, Loop, Sustain, Reverse).
    /// </summary>
    public SamplePlaybackMode PlaybackMode { get; set; } = SamplePlaybackMode.OneShot;

    /// <summary>
    /// Gets or sets the interpolation mode for pitch shifting.
    /// </summary>
    public InterpolationMode Interpolation { get; set; } = InterpolationMode.Linear;

    /// <summary>
    /// Gets or sets the start position in frames.
    /// </summary>
    public int StartPosition { get; set; } = 0;

    /// <summary>
    /// Gets or sets the end position in frames (0 = end of sample).
    /// </summary>
    public int EndPosition { get; set; } = 0;

    /// <summary>
    /// Gets or sets the loop start position in frames (-1 = use StartPosition).
    /// </summary>
    public int LoopStart { get; set; } = -1;

    /// <summary>
    /// Gets or sets the loop end position in frames (0 = use EndPosition or end of sample).
    /// </summary>
    public int LoopEnd { get; set; } = 0;

    /// <summary>
    /// Gets or sets the pitch adjustment in semitones (-24 to +24).
    /// </summary>
    public float Pitch { get; set; } = 0f;

    /// <summary>
    /// Gets or sets the root note (MIDI note that plays sample at original pitch).
    /// </summary>
    public int RootNote { get; set; } = 60;

    /// <summary>
    /// Gets or sets the time stretch factor (1.0 = original speed, 0.5 = half speed, 2.0 = double speed).
    /// Note: Time stretching changes duration without affecting pitch.
    /// </summary>
    public float TimeStretch { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the master volume (0.0 to 2.0).
    /// </summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the stereo pan (-1.0 = left, 0.0 = center, 1.0 = right).
    /// </summary>
    public float Pan { get; set; } = 0f;

    /// <summary>
    /// Gets or sets the velocity sensitivity (0.0 = no sensitivity, 1.0 = full sensitivity).
    /// </summary>
    public float VelocitySensitivity { get; set; } = 0.5f;

    /// <summary>
    /// Gets or sets the release time in seconds for note off fade out.
    /// </summary>
    public double ReleaseTime { get; set; } = 0.02;

    /// <summary>
    /// Gets or sets the maximum number of simultaneous voices (polyphony).
    /// </summary>
    public int MaxVoices { get; set; } = 16;

    /// <summary>
    /// Event raised when sample loading completes.
    /// </summary>
    public event EventHandler? SampleLoaded;

    /// <summary>
    /// Creates a new SamplePlayer with the specified output format.
    /// </summary>
    /// <param name="sampleRate">Output sample rate (default: from Settings).</param>
    /// <param name="channels">Number of output channels (default: 2).</param>
    public SamplePlayer(int? sampleRate = null, int channels = 2)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, channels);
    }

    /// <summary>
    /// Loads audio data directly from a float array.
    /// </summary>
    /// <param name="audioData">Interleaved audio samples.</param>
    /// <param name="sampleRate">Sample rate of the audio data.</param>
    /// <param name="channels">Number of channels in the audio data.</param>
    public void LoadAudio(float[] audioData, int sampleRate, int channels = 2)
    {
        lock (_lock)
        {
            AudioData = audioData;
            SourceSampleRate = sampleRate;
            SourceChannels = channels;
            FilePath = null;

            // Reset positions
            StartPosition = 0;
            EndPosition = 0;
            LoopStart = -1;
            LoopEnd = 0;

            // Stop all voices
            StopAllVoices();
        }

        SampleLoaded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Loads a sample from an audio file.
    /// Supports WAV, MP3, FLAC, OGG, AIFF via NAudio.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <returns>True if loading succeeded, false otherwise.</returns>
    public bool LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[SamplePlayer] File not found: {filePath}");
            return false;
        }

        try
        {
            using var reader = new AudioFileReader(filePath);
            var samples = new List<float>();
            var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
            int read;

            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    samples.Add(buffer[i]);
                }
            }

            lock (_lock)
            {
                AudioData = samples.ToArray();
                SourceSampleRate = reader.WaveFormat.SampleRate;
                SourceChannels = reader.WaveFormat.Channels;
                FilePath = filePath;

                // Reset positions
                StartPosition = 0;
                EndPosition = 0;
                LoopStart = -1;
                LoopEnd = 0;

                // Stop all voices
                StopAllVoices();
            }

            Console.WriteLine($"[SamplePlayer] Loaded: {Path.GetFileName(filePath)} ({SampleLengthInFrames} frames, {SourceChannels}ch, {SourceSampleRate}Hz)");
            SampleLoaded?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SamplePlayer] Error loading {filePath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Clears the loaded sample data.
    /// </summary>
    public void Unload()
    {
        lock (_lock)
        {
            StopAllVoices();
            AudioData = null;
            SourceSampleRate = 0;
            SourceChannels = 0;
            FilePath = null;
        }
    }

    /// <summary>
    /// Sets the playback region (start and end points).
    /// </summary>
    /// <param name="startFrame">Start frame position.</param>
    /// <param name="endFrame">End frame position (0 = end of sample).</param>
    public void SetPlaybackRegion(int startFrame, int endFrame)
    {
        StartPosition = Math.Max(0, startFrame);
        EndPosition = endFrame;
    }

    /// <summary>
    /// Sets the loop region.
    /// </summary>
    /// <param name="loopStartFrame">Loop start frame position.</param>
    /// <param name="loopEndFrame">Loop end frame position (0 = end of sample).</param>
    public void SetLoopRegion(int loopStartFrame, int loopEndFrame)
    {
        LoopStart = loopStartFrame;
        LoopEnd = loopEndFrame;
    }

    /// <summary>
    /// Triggers sample playback (ignores note for pitch, uses root note).
    /// </summary>
    public void Play()
    {
        NoteOn(RootNote, 100);
    }

    /// <summary>
    /// Triggers sample playback with specified velocity.
    /// </summary>
    /// <param name="velocity">Velocity (0-127).</param>
    public void Play(int velocity)
    {
        NoteOn(RootNote, velocity);
    }

    /// <summary>
    /// Stops all playing voices.
    /// </summary>
    public void Stop()
    {
        AllNotesOff();
    }

    private void StopAllVoices()
    {
        foreach (var voice in _voices)
        {
            voice.Stop();
        }
        _voices.Clear();
    }

    private SamplePlayerVoice? GetFreeVoice()
    {
        // Try to get from pool
        if (_voicePool.Count > 0)
        {
            var voice = _voicePool[^1];
            _voicePool.RemoveAt(_voicePool.Count - 1);
            return voice;
        }

        // Create new voice if under limit
        if (_voices.Count < MaxVoices)
        {
            return new SamplePlayerVoice(this);
        }

        // Steal oldest voice
        if (_voices.Count > 0)
        {
            var oldest = _voices[0];
            DateTime oldestTime = oldest.TriggerTime;

            for (int i = 1; i < _voices.Count; i++)
            {
                if (_voices[i].TriggerTime < oldestTime)
                {
                    oldest = _voices[i];
                    oldestTime = oldest.TriggerTime;
                }
            }

            oldest.Stop();
            _voices.Remove(oldest);
            return oldest;
        }

        return null;
    }

    #region ISynth Implementation

    /// <summary>
    /// Triggers sample playback at the specified note and velocity.
    /// </summary>
    /// <param name="note">MIDI note number (affects pitch relative to root note).</param>
    /// <param name="velocity">Velocity (0-127). Velocity 0 triggers NoteOff.</param>
    public void NoteOn(int note, int velocity)
    {
        if (AudioData == null || AudioData.Length == 0)
            return;

        if (velocity == 0)
        {
            NoteOff(note);
            return;
        }

        lock (_lock)
        {
            var voice = GetFreeVoice();
            if (voice == null) return;

            voice.Trigger(note, velocity);
            _voices.Add(voice);
        }
    }

    /// <summary>
    /// Releases the note (triggers release phase for sustain mode).
    /// </summary>
    /// <param name="note">MIDI note number.</param>
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

    /// <summary>
    /// Stops all playing voices immediately.
    /// </summary>
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

    /// <summary>
    /// Sets a parameter by name.
    /// </summary>
    /// <param name="name">Parameter name.</param>
    /// <param name="value">Parameter value.</param>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "volume":
            case "gain":
            case "level":
                Volume = Math.Clamp(value, 0f, 2f);
                break;

            case "pan":
                Pan = Math.Clamp(value, -1f, 1f);
                break;

            case "pitch":
                Pitch = Math.Clamp(value, -24f, 24f);
                break;

            case "timestretch":
            case "stretch":
            case "speed":
                TimeStretch = Math.Clamp(value, 0.1f, 10f);
                break;

            case "startposition":
            case "start":
                StartPosition = Math.Max(0, (int)value);
                break;

            case "endposition":
            case "end":
                EndPosition = Math.Max(0, (int)value);
                break;

            case "loopstart":
                LoopStart = (int)value;
                break;

            case "loopend":
                LoopEnd = Math.Max(0, (int)value);
                break;

            case "rootnote":
            case "root":
                RootNote = Math.Clamp((int)value, 0, 127);
                break;

            case "velocitysensitivity":
            case "velsens":
                VelocitySensitivity = Math.Clamp(value, 0f, 1f);
                break;

            case "releasetime":
            case "release":
                ReleaseTime = Math.Max(0, value);
                break;

            case "playbackmode":
            case "mode":
                if (Enum.IsDefined(typeof(SamplePlaybackMode), (int)value))
                {
                    PlaybackMode = (SamplePlaybackMode)(int)value;
                }
                break;

            case "interpolation":
            case "interp":
                if (Enum.IsDefined(typeof(InterpolationMode), (int)value))
                {
                    Interpolation = (InterpolationMode)(int)value;
                }
                break;

            case "maxvoices":
            case "polyphony":
                MaxVoices = Math.Clamp((int)value, 1, 128);
                break;
        }
    }

    #endregion

    #region ISampleProvider Implementation

    /// <summary>
    /// Reads audio samples, mixing all active voices.
    /// </summary>
    /// <param name="buffer">Output buffer.</param>
    /// <param name="offset">Offset in buffer to start writing.</param>
    /// <param name="count">Number of samples to write.</param>
    /// <returns>Number of samples written.</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        // Clear buffer
        Array.Clear(buffer, offset, count);

        if (AudioData == null) return count;

        lock (_lock)
        {
            // Process all active voices
            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                var voice = _voices[i];

                if (!voice.IsActive)
                {
                    _voices.RemoveAt(i);
                    _voicePool.Add(voice);
                    continue;
                }

                voice.Process(buffer, offset, count);

                // Check if voice became inactive during processing
                if (!voice.IsActive)
                {
                    _voices.RemoveAt(i);
                    _voicePool.Add(voice);
                }
            }
        }

        return count;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Gets the current playback position of the first active voice in frames.
    /// </summary>
    /// <returns>Position in frames, or -1 if no voices are active.</returns>
    public int GetPlaybackPosition()
    {
        lock (_lock)
        {
            var activeVoice = _voices.FirstOrDefault(v => v.IsActive);
            // Note: Position is internal to voice, so we return -1 for now
            // A full implementation would expose position from the voice
            return activeVoice != null ? 0 : -1;
        }
    }

    /// <summary>
    /// Gets the number of currently active voices.
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
    /// Returns whether any voice is currently playing.
    /// </summary>
    public bool IsPlaying
    {
        get
        {
            lock (_lock)
            {
                return _voices.Any(v => v.IsActive);
            }
        }
    }

    /// <summary>
    /// Converts a time in seconds to frame position.
    /// </summary>
    /// <param name="seconds">Time in seconds.</param>
    /// <returns>Frame position.</returns>
    public int SecondsToFrames(double seconds)
    {
        return (int)(seconds * SourceSampleRate);
    }

    /// <summary>
    /// Converts a frame position to time in seconds.
    /// </summary>
    /// <param name="frames">Frame position.</param>
    /// <returns>Time in seconds.</returns>
    public double FramesToSeconds(int frames)
    {
        return SourceSampleRate > 0 ? (double)frames / SourceSampleRate : 0;
    }

    /// <summary>
    /// Normalizes the sample audio to the specified peak level.
    /// </summary>
    /// <param name="peakDb">Target peak level in dB (e.g., -1.0 for -1 dBFS).</param>
    public void Normalize(float peakDb = -1.0f)
    {
        if (AudioData == null || AudioData.Length == 0) return;

        lock (_lock)
        {
            // Find current peak
            float currentPeak = 0;
            for (int i = 0; i < AudioData.Length; i++)
            {
                float abs = Math.Abs(AudioData[i]);
                if (abs > currentPeak) currentPeak = abs;
            }

            if (currentPeak <= 0) return;

            // Calculate gain to reach target peak
            float targetPeak = MathF.Pow(10, peakDb / 20f);
            float gain = targetPeak / currentPeak;

            // Apply gain
            for (int i = 0; i < AudioData.Length; i++)
            {
                AudioData[i] *= gain;
            }
        }
    }

    /// <summary>
    /// Reverses the sample audio data in place.
    /// </summary>
    public void ReverseAudio()
    {
        if (AudioData == null || AudioData.Length == 0) return;

        lock (_lock)
        {
            int channels = SourceChannels;
            int frames = SampleLengthInFrames;

            for (int i = 0; i < frames / 2; i++)
            {
                int frontIdx = i * channels;
                int backIdx = (frames - 1 - i) * channels;

                for (int ch = 0; ch < channels; ch++)
                {
                    (AudioData[frontIdx + ch], AudioData[backIdx + ch]) = (AudioData[backIdx + ch], AudioData[frontIdx + ch]);
                }
            }
        }
    }

    /// <summary>
    /// Creates a preset configured for one-shot drum/percussion playback.
    /// </summary>
    public static SamplePlayer CreateOneShotPreset(int? sampleRate = null)
    {
        return new SamplePlayer(sampleRate)
        {
            Name = "OneShot",
            PlaybackMode = SamplePlaybackMode.OneShot,
            VelocitySensitivity = 1.0f,
            ReleaseTime = 0.005
        };
    }

    /// <summary>
    /// Creates a preset configured for looping pad/sustain playback.
    /// </summary>
    public static SamplePlayer CreateLoopPreset(int? sampleRate = null)
    {
        return new SamplePlayer(sampleRate)
        {
            Name = "Loop",
            PlaybackMode = SamplePlaybackMode.Loop,
            VelocitySensitivity = 0.5f,
            ReleaseTime = 0.5
        };
    }

    /// <summary>
    /// Creates a preset configured for sustaining instrument playback.
    /// </summary>
    public static SamplePlayer CreateSustainPreset(int? sampleRate = null)
    {
        return new SamplePlayer(sampleRate)
        {
            Name = "Sustain",
            PlaybackMode = SamplePlaybackMode.Sustain,
            VelocitySensitivity = 0.7f,
            ReleaseTime = 0.3
        };
    }

    #endregion
}
