//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Beat-synchronized audio stuttering/repeating effect with tempo sync and pitch shifting.

using NAudio.Wave;

namespace MusicEngine.Core.Effects.Special;

/// <summary>
/// Repeat interval note values for tempo-synchronized repeats.
/// </summary>
public enum RepeatInterval
{
    /// <summary>
    /// Whole note (1 bar in 4/4).
    /// </summary>
    WholeNote = 1,

    /// <summary>
    /// Half note (1/2 bar in 4/4).
    /// </summary>
    HalfNote = 2,

    /// <summary>
    /// Quarter note (1/4 bar, one beat in 4/4).
    /// </summary>
    QuarterNote = 4,

    /// <summary>
    /// Eighth note (1/8 bar).
    /// </summary>
    EighthNote = 8,

    /// <summary>
    /// Sixteenth note (1/16 bar).
    /// </summary>
    SixteenthNote = 16,

    /// <summary>
    /// Thirty-second note (1/32 bar).
    /// </summary>
    ThirtySecondNote = 32,

    /// <summary>
    /// Sixty-fourth note (1/64 bar).
    /// </summary>
    SixtyFourthNote = 64
}

/// <summary>
/// Trigger mode for beat repeat activation.
/// </summary>
public enum BeatRepeatTriggerMode
{
    /// <summary>
    /// Effect is always active when enabled.
    /// </summary>
    Always,

    /// <summary>
    /// Effect triggers based on probability per interval.
    /// </summary>
    Probability,

    /// <summary>
    /// Effect triggers manually via Trigger() method.
    /// </summary>
    Manual,

    /// <summary>
    /// Effect triggers on specific beat positions.
    /// </summary>
    BeatSync
}

/// <summary>
/// Pitch shift mode for repeated slices.
/// </summary>
public enum RepeatPitchMode
{
    /// <summary>
    /// No pitch shifting applied.
    /// </summary>
    None,

    /// <summary>
    /// Pitch increases with each repeat (riser effect).
    /// </summary>
    Up,

    /// <summary>
    /// Pitch decreases with each repeat (downer effect).
    /// </summary>
    Down,

    /// <summary>
    /// Random pitch variation per repeat.
    /// </summary>
    Random
}

/// <summary>
/// Beat repeat effect that creates tempo-synchronized audio stuttering and repeating.
/// Common in electronic music production for glitch, stutter, and build-up effects.
/// </summary>
/// <remarks>
/// The effect captures audio slices based on tempo and repeat interval, then plays
/// them back repeatedly with optional decay, pitch shifting, and probability control.
/// This is similar to effects found in Ableton Live's Beat Repeat or various DJ software.
/// </remarks>
public class BeatRepeat : EffectBase
{
    private const int MaxBufferSeconds = 4;
    private const float TwoPi = MathF.PI * 2f;

    // Audio buffer for captured slices
    private readonly float[] _captureBuffer;
    private readonly int _maxBufferSamples;
    private int _capturePosition;
    private int _sliceLengthSamples;
    private int _playbackPosition;
    private int _currentRepeat;
    private bool _isRepeating;
    private bool _sliceCaptured;

    // Timing
    private double _currentBeatPosition;
    private double _lastTriggerBeat;
    private long _totalSamplesProcessed;
    private int _samplesPerBeat;

    // Pitch shifting state
    private double _pitchPhase;
    private float _currentPitchRatio;

    // Random number generator for probability and random pitch
    private readonly Random _random;

    /// <summary>
    /// Gets or sets the tempo in beats per minute for synchronization.
    /// Range: 20 to 300 BPM.
    /// </summary>
    public float Tempo
    {
        get => GetParameter("Tempo");
        set
        {
            float clamped = Math.Clamp(value, 20f, 300f);
            SetParameter("Tempo", clamped);
            UpdateTimingParameters();
        }
    }

    /// <summary>
    /// Gets or sets the repeat interval (note value for slice length).
    /// </summary>
    public RepeatInterval Interval
    {
        get => (RepeatInterval)(int)GetParameter("Interval");
        set
        {
            SetParameter("Interval", (float)(int)value);
            UpdateTimingParameters();
        }
    }

    /// <summary>
    /// Gets or sets the number of times to repeat the captured slice.
    /// Range: 1 to 32.
    /// </summary>
    public int RepeatCount
    {
        get => (int)GetParameter("RepeatCount");
        set => SetParameter("RepeatCount", Math.Clamp(value, 1, 32));
    }

    /// <summary>
    /// Gets or sets the decay amount per repeat (0.0 = no decay, 1.0 = full decay).
    /// Each repeat will be multiplied by (1 - Decay * repeatIndex / RepeatCount).
    /// </summary>
    public float Decay
    {
        get => GetParameter("Decay");
        set => SetParameter("Decay", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the pitch shift amount in semitones per repeat.
    /// Range: -12 to +12 semitones.
    /// </summary>
    public float PitchShift
    {
        get => GetParameter("PitchShift");
        set => SetParameter("PitchShift", Math.Clamp(value, -12f, 12f));
    }

    /// <summary>
    /// Gets or sets the pitch shift mode.
    /// </summary>
    public RepeatPitchMode PitchMode
    {
        get => (RepeatPitchMode)(int)GetParameter("PitchMode");
        set => SetParameter("PitchMode", (float)(int)value);
    }

    /// <summary>
    /// Gets or sets the trigger probability (0.0 to 1.0) when in Probability mode.
    /// </summary>
    public float Probability
    {
        get => GetParameter("Probability");
        set => SetParameter("Probability", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets the trigger mode.
    /// </summary>
    public BeatRepeatTriggerMode TriggerMode
    {
        get => (BeatRepeatTriggerMode)(int)GetParameter("TriggerMode");
        set => SetParameter("TriggerMode", (float)(int)value);
    }

    /// <summary>
    /// Gets or sets which beat positions trigger the effect in BeatSync mode (0-15 for 16th notes in a bar).
    /// Each bit represents a 16th note position.
    /// </summary>
    public int BeatPattern
    {
        get => (int)GetParameter("BeatPattern");
        set => SetParameter("BeatPattern", value & 0xFFFF);
    }

    /// <summary>
    /// Gets or sets the gate amount (0.0 to 1.0) controlling how much of each repeat is audible.
    /// 1.0 = full slice, 0.5 = half slice with silence.
    /// </summary>
    public float Gate
    {
        get => GetParameter("Gate");
        set => SetParameter("Gate", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets or sets whether to apply a fade envelope to each repeat to reduce clicks.
    /// </summary>
    public bool SmoothFades
    {
        get => GetParameter("SmoothFades") > 0.5f;
        set => SetParameter("SmoothFades", value ? 1f : 0f);
    }

    /// <summary>
    /// Gets or sets the variation amount for random timing offsets (0.0 to 1.0).
    /// </summary>
    public float Variation
    {
        get => GetParameter("Variation");
        set => SetParameter("Variation", Math.Clamp(value, 0f, 1f));
    }

    /// <summary>
    /// Gets whether the effect is currently in a repeating state.
    /// </summary>
    public bool IsRepeating => _isRepeating;

    /// <summary>
    /// Gets the current repeat index (0 to RepeatCount-1).
    /// </summary>
    public int CurrentRepeatIndex => _currentRepeat;

    /// <summary>
    /// Gets the current beat position within the bar (0.0 to 4.0 for 4/4 time).
    /// </summary>
    public double CurrentBeatPosition => _currentBeatPosition;

    /// <summary>
    /// Event raised when a new repeat cycle begins.
    /// </summary>
    public event EventHandler? RepeatStarted;

    /// <summary>
    /// Event raised when a repeat cycle completes.
    /// </summary>
    public event EventHandler? RepeatCompleted;

    /// <summary>
    /// Creates a new beat repeat effect.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    public BeatRepeat(ISampleProvider source) : base(source, "Beat Repeat")
    {
        _maxBufferSamples = MaxBufferSeconds * source.WaveFormat.SampleRate * source.WaveFormat.Channels;
        _captureBuffer = new float[_maxBufferSamples];
        _random = new Random();

        // Register parameters with defaults
        RegisterParameter("Tempo", 120f);
        RegisterParameter("Interval", (float)(int)RepeatInterval.SixteenthNote);
        RegisterParameter("RepeatCount", 4);
        RegisterParameter("Decay", 0f);
        RegisterParameter("PitchShift", 0f);
        RegisterParameter("PitchMode", (float)(int)RepeatPitchMode.None);
        RegisterParameter("Probability", 0.5f);
        RegisterParameter("TriggerMode", (float)(int)BeatRepeatTriggerMode.Always);
        RegisterParameter("BeatPattern", 0b1000_1000_1000_1000); // Every quarter note
        RegisterParameter("Gate", 1f);
        RegisterParameter("SmoothFades", 1f);
        RegisterParameter("Variation", 0f);
        RegisterParameter("Mix", 1f);

        // Initialize timing
        UpdateTimingParameters();
        Reset();
    }

    /// <summary>
    /// Manually triggers a beat repeat cycle.
    /// </summary>
    public void Trigger()
    {
        StartRepeatCycle();
    }

    /// <summary>
    /// Stops the current repeat cycle immediately.
    /// </summary>
    public void Stop()
    {
        _isRepeating = false;
        _sliceCaptured = false;
        _currentRepeat = 0;
    }

    /// <summary>
    /// Resets the effect to its initial state.
    /// </summary>
    public void Reset()
    {
        _capturePosition = 0;
        _playbackPosition = 0;
        _currentRepeat = 0;
        _isRepeating = false;
        _sliceCaptured = false;
        _currentBeatPosition = 0;
        _lastTriggerBeat = -1;
        _totalSamplesProcessed = 0;
        _pitchPhase = 0;
        _currentPitchRatio = 1f;
        Array.Clear(_captureBuffer, 0, _captureBuffer.Length);
    }

    /// <summary>
    /// Sets the beat position externally for synchronization with a sequencer.
    /// </summary>
    /// <param name="beatPosition">Beat position (0.0 = start of bar).</param>
    public void SetBeatPosition(double beatPosition)
    {
        _currentBeatPosition = beatPosition;
    }

    /// <summary>
    /// Updates timing parameters based on tempo and interval.
    /// </summary>
    private void UpdateTimingParameters()
    {
        float tempo = Tempo;
        int interval = (int)Interval;

        // Calculate samples per beat
        float beatsPerSecond = tempo / 60f;
        float samplesPerSecondMono = SampleRate;
        _samplesPerBeat = (int)(samplesPerSecondMono / beatsPerSecond);

        // Calculate slice length based on interval (note value)
        // A quarter note = 1 beat, eighth note = 0.5 beats, etc.
        float beatsPerSlice = 4f / interval; // 4 quarter notes per whole note
        _sliceLengthSamples = (int)(beatsPerSlice * _samplesPerBeat) * Channels;

        // Clamp to buffer size
        _sliceLengthSamples = Math.Min(_sliceLengthSamples, _maxBufferSamples);
    }

    /// <summary>
    /// Starts a new repeat cycle by capturing the current audio slice.
    /// </summary>
    private void StartRepeatCycle()
    {
        _isRepeating = true;
        _sliceCaptured = false;
        _capturePosition = 0;
        _playbackPosition = 0;
        _currentRepeat = 0;
        _pitchPhase = 0;

        // Calculate initial pitch ratio based on mode
        UpdatePitchRatio();

        RepeatStarted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Updates the pitch ratio based on current repeat and pitch mode.
    /// </summary>
    private void UpdatePitchRatio()
    {
        RepeatPitchMode mode = PitchMode;
        float pitchShift = PitchShift;
        int repeatCount = RepeatCount;

        switch (mode)
        {
            case RepeatPitchMode.None:
                _currentPitchRatio = 1f;
                break;

            case RepeatPitchMode.Up:
                // Increase pitch with each repeat
                float semitones = pitchShift * _currentRepeat / Math.Max(1, repeatCount - 1);
                _currentPitchRatio = MathF.Pow(2f, semitones / 12f);
                break;

            case RepeatPitchMode.Down:
                // Decrease pitch with each repeat
                semitones = -pitchShift * _currentRepeat / Math.Max(1, repeatCount - 1);
                _currentPitchRatio = MathF.Pow(2f, semitones / 12f);
                break;

            case RepeatPitchMode.Random:
                // Random pitch within range
                float randomSemitones = ((float)_random.NextDouble() * 2f - 1f) * pitchShift;
                _currentPitchRatio = MathF.Pow(2f, randomSemitones / 12f);
                break;
        }
    }

    /// <summary>
    /// Checks if the effect should trigger based on the current mode and beat position.
    /// </summary>
    private bool ShouldTrigger()
    {
        BeatRepeatTriggerMode mode = TriggerMode;

        switch (mode)
        {
            case BeatRepeatTriggerMode.Always:
                return !_isRepeating;

            case BeatRepeatTriggerMode.Probability:
                if (!_isRepeating)
                {
                    // Check at each interval boundary
                    double intervalBeats = 4.0 / (int)Interval;
                    double currentIntervalIndex = Math.Floor(_currentBeatPosition / intervalBeats);
                    double lastIntervalIndex = Math.Floor(_lastTriggerBeat / intervalBeats);

                    if (currentIntervalIndex != lastIntervalIndex || _lastTriggerBeat < 0)
                    {
                        _lastTriggerBeat = _currentBeatPosition;
                        return _random.NextDouble() < Probability;
                    }
                }
                return false;

            case BeatRepeatTriggerMode.Manual:
                // Only triggers via Trigger() method
                return false;

            case BeatRepeatTriggerMode.BeatSync:
                if (!_isRepeating)
                {
                    // Check beat pattern (16 positions per bar)
                    int position = (int)((_currentBeatPosition % 4.0) * 4) % 16;
                    int pattern = BeatPattern;
                    bool shouldTrigger = ((pattern >> position) & 1) == 1;

                    // Prevent retriggering on the same position
                    double intervalBeats = 0.25; // 16th note resolution
                    double currentIntervalIndex = Math.Floor(_currentBeatPosition / intervalBeats);
                    double lastIntervalIndex = Math.Floor(_lastTriggerBeat / intervalBeats);

                    if (shouldTrigger && (currentIntervalIndex != lastIntervalIndex || _lastTriggerBeat < 0))
                    {
                        _lastTriggerBeat = _currentBeatPosition;
                        return true;
                    }
                }
                return false;
        }

        return false;
    }

    /// <inheritdoc />
    protected override void ProcessBuffer(float[] sourceBuffer, float[] destBuffer, int offset, int count)
    {
        int channels = Channels;
        float decay = Decay;
        float gate = Gate;
        bool smoothFades = SmoothFades;
        int repeatCount = RepeatCount;
        float variation = Variation;

        // Calculate beat advancement per sample
        double beatsPerSample = Tempo / 60.0 / SampleRate;

        for (int i = 0; i < count; i += channels)
        {
            // Advance beat position
            _currentBeatPosition += beatsPerSample;
            if (_currentBeatPosition >= 4.0)
            {
                _currentBeatPosition -= 4.0;
            }

            // Check for trigger
            if (ShouldTrigger())
            {
                StartRepeatCycle();
            }

            if (_isRepeating)
            {
                // Capturing phase
                if (!_sliceCaptured)
                {
                    // Capture incoming audio
                    for (int ch = 0; ch < channels; ch++)
                    {
                        if (_capturePosition + ch < _maxBufferSamples)
                        {
                            _captureBuffer[_capturePosition + ch] = sourceBuffer[i + ch];
                        }
                    }
                    _capturePosition += channels;

                    // Check if capture is complete
                    if (_capturePosition >= _sliceLengthSamples)
                    {
                        _sliceCaptured = true;
                        _playbackPosition = 0;
                        _pitchPhase = 0;
                    }

                    // During capture, pass through original audio
                    for (int ch = 0; ch < channels; ch++)
                    {
                        destBuffer[offset + i + ch] = sourceBuffer[i + ch];
                    }
                }
                else
                {
                    // Playback phase - play captured slice with pitch shifting
                    int sliceLength = _sliceLengthSamples;

                    // Apply variation to slice length
                    if (variation > 0f && _currentRepeat > 0)
                    {
                        float variationAmount = (float)(_random.NextDouble() * 2 - 1) * variation * 0.2f;
                        sliceLength = (int)(sliceLength * (1f + variationAmount));
                        sliceLength = Math.Clamp(sliceLength, channels, _capturePosition);
                    }

                    // Calculate gate-limited playback length
                    int gatedLength = (int)(sliceLength * gate);
                    gatedLength = Math.Max(gatedLength, channels);

                    // Calculate decay factor for current repeat
                    float decayFactor = 1f - (decay * _currentRepeat / Math.Max(1f, repeatCount - 1f));
                    decayFactor = Math.Max(0f, decayFactor);

                    // Read from buffer with pitch shifting
                    for (int ch = 0; ch < channels; ch++)
                    {
                        float sample = 0f;

                        if (_playbackPosition < gatedLength)
                        {
                            // Calculate read position with pitch ratio
                            double readPos = _pitchPhase;
                            int readIndex = (int)readPos;
                            float frac = (float)(readPos - readIndex);

                            // Ensure we stay within captured data
                            int idx1 = (readIndex * channels + ch) % _capturePosition;
                            int idx2 = ((readIndex + 1) * channels + ch) % _capturePosition;

                            if (idx1 >= 0 && idx1 < _capturePosition && idx2 >= 0 && idx2 < _capturePosition)
                            {
                                // Linear interpolation for smooth pitch shifting
                                sample = _captureBuffer[idx1] * (1f - frac) + _captureBuffer[idx2] * frac;
                            }

                            // Apply smooth fade envelope if enabled
                            if (smoothFades)
                            {
                                int fadeLength = Math.Min(64, gatedLength / 4);

                                // Fade in
                                if (_playbackPosition < fadeLength)
                                {
                                    sample *= (float)_playbackPosition / fadeLength;
                                }
                                // Fade out
                                else if (_playbackPosition >= gatedLength - fadeLength)
                                {
                                    int fadePos = gatedLength - _playbackPosition;
                                    sample *= (float)fadePos / fadeLength;
                                }
                            }

                            // Apply decay
                            sample *= decayFactor;
                        }

                        destBuffer[offset + i + ch] = sample;
                    }

                    // Advance playback position
                    _playbackPosition += channels;
                    _pitchPhase += _currentPitchRatio;

                    // Check if slice playback is complete
                    if (_playbackPosition >= sliceLength || _pitchPhase * channels >= _capturePosition)
                    {
                        _currentRepeat++;

                        if (_currentRepeat >= repeatCount)
                        {
                            // Repeat cycle complete
                            _isRepeating = false;
                            _sliceCaptured = false;
                            RepeatCompleted?.Invoke(this, EventArgs.Empty);
                        }
                        else
                        {
                            // Start next repeat
                            _playbackPosition = 0;
                            _pitchPhase = 0;
                            UpdatePitchRatio();
                        }
                    }
                }
            }
            else
            {
                // Not repeating - pass through original audio
                for (int ch = 0; ch < channels; ch++)
                {
                    destBuffer[offset + i + ch] = sourceBuffer[i + ch];
                }
            }

            _totalSamplesProcessed++;
        }
    }

    /// <inheritdoc />
    protected override void OnParameterChanged(string name, float value)
    {
        if (name.Equals("Tempo", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Interval", StringComparison.OrdinalIgnoreCase))
        {
            UpdateTimingParameters();
        }
    }

    /// <summary>
    /// Creates a preset for classic stutter effect.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <param name="tempo">The tempo in BPM.</param>
    /// <returns>A configured BeatRepeat instance.</returns>
    public static BeatRepeat CreateStutter(ISampleProvider source, float tempo = 120f)
    {
        var effect = new BeatRepeat(source)
        {
            Tempo = tempo,
            Interval = RepeatInterval.SixteenthNote,
            RepeatCount = 4,
            Decay = 0f,
            PitchMode = RepeatPitchMode.None,
            TriggerMode = BeatRepeatTriggerMode.Always,
            Gate = 1f,
            Mix = 1f
        };
        return effect;
    }

    /// <summary>
    /// Creates a preset for glitch effect with random triggering.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <param name="tempo">The tempo in BPM.</param>
    /// <returns>A configured BeatRepeat instance.</returns>
    public static BeatRepeat CreateGlitch(ISampleProvider source, float tempo = 120f)
    {
        var effect = new BeatRepeat(source)
        {
            Tempo = tempo,
            Interval = RepeatInterval.ThirtySecondNote,
            RepeatCount = 8,
            Decay = 0.3f,
            PitchMode = RepeatPitchMode.Random,
            PitchShift = 5f,
            TriggerMode = BeatRepeatTriggerMode.Probability,
            Probability = 0.3f,
            Variation = 0.2f,
            Gate = 0.8f,
            Mix = 1f
        };
        return effect;
    }

    /// <summary>
    /// Creates a preset for build-up riser effect.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <param name="tempo">The tempo in BPM.</param>
    /// <returns>A configured BeatRepeat instance.</returns>
    public static BeatRepeat CreateRiser(ISampleProvider source, float tempo = 120f)
    {
        var effect = new BeatRepeat(source)
        {
            Tempo = tempo,
            Interval = RepeatInterval.SixteenthNote,
            RepeatCount = 16,
            Decay = 0f,
            PitchMode = RepeatPitchMode.Up,
            PitchShift = 12f,
            TriggerMode = BeatRepeatTriggerMode.Manual,
            Gate = 1f,
            Mix = 1f
        };
        return effect;
    }

    /// <summary>
    /// Creates a preset for descending effect.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <param name="tempo">The tempo in BPM.</param>
    /// <returns>A configured BeatRepeat instance.</returns>
    public static BeatRepeat CreateDowner(ISampleProvider source, float tempo = 120f)
    {
        var effect = new BeatRepeat(source)
        {
            Tempo = tempo,
            Interval = RepeatInterval.EighthNote,
            RepeatCount = 8,
            Decay = 0.5f,
            PitchMode = RepeatPitchMode.Down,
            PitchShift = 12f,
            TriggerMode = BeatRepeatTriggerMode.Manual,
            Gate = 1f,
            Mix = 1f
        };
        return effect;
    }

    /// <summary>
    /// Creates a preset for rhythmic gate effect.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <param name="tempo">The tempo in BPM.</param>
    /// <returns>A configured BeatRepeat instance.</returns>
    public static BeatRepeat CreateRhythmicGate(ISampleProvider source, float tempo = 120f)
    {
        var effect = new BeatRepeat(source)
        {
            Tempo = tempo,
            Interval = RepeatInterval.SixteenthNote,
            RepeatCount = 2,
            Decay = 0f,
            PitchMode = RepeatPitchMode.None,
            TriggerMode = BeatRepeatTriggerMode.BeatSync,
            BeatPattern = 0b1010_1010_1010_1010, // Every other 16th note
            Gate = 0.5f,
            Mix = 1f
        };
        return effect;
    }

    /// <summary>
    /// Creates a preset for tape-stop style repeat.
    /// </summary>
    /// <param name="source">The audio source to process.</param>
    /// <param name="tempo">The tempo in BPM.</param>
    /// <returns>A configured BeatRepeat instance.</returns>
    public static BeatRepeat CreateTapeStyle(ISampleProvider source, float tempo = 120f)
    {
        var effect = new BeatRepeat(source)
        {
            Tempo = tempo,
            Interval = RepeatInterval.QuarterNote,
            RepeatCount = 4,
            Decay = 0.7f,
            PitchMode = RepeatPitchMode.Down,
            PitchShift = 6f,
            TriggerMode = BeatRepeatTriggerMode.Manual,
            Gate = 1f,
            SmoothFades = true,
            Mix = 1f
        };
        return effect;
    }
}
