// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;


namespace MusicEngine.Core;


/// <summary>
/// Click sound type for the metronome.
/// </summary>
public enum ClickSound
{
    /// <summary>Short sine wave click.</summary>
    Click,

    /// <summary>Higher pitched beep sound.</summary>
    Beep,

    /// <summary>Woodblock-style percussive sound.</summary>
    Woodblock,

    /// <summary>User-provided custom sound.</summary>
    Custom
}


/// <summary>
/// Event arguments for metronome click events.
/// </summary>
public class MetronomeClickEventArgs : EventArgs
{
    /// <summary>The beat number within the bar (1-indexed).</summary>
    public int Beat { get; }

    /// <summary>Whether this is the downbeat (first beat of the bar).</summary>
    public bool IsDownbeat { get; }

    /// <summary>Whether this click is during the count-in phase.</summary>
    public bool IsCountIn { get; }

    /// <summary>The current bar number (1-indexed, negative during count-in).</summary>
    public int Bar { get; }

    /// <summary>
    /// Creates new metronome click event arguments.
    /// </summary>
    public MetronomeClickEventArgs(int beat, bool isDownbeat, bool isCountIn, int bar = 0)
    {
        Beat = beat;
        IsDownbeat = isDownbeat;
        IsCountIn = isCountIn;
        Bar = bar;
    }
}


/// <summary>
/// A metronome/click track implementation that provides audible beat markers.
/// Implements ISampleProvider for integration with the audio engine.
/// Supports Sequencer integration with automatic BPM sync and count-in functionality.
/// </summary>
public class Metronome : ISampleProvider, IDisposable
{
    private readonly WaveFormat _waveFormat; // Audio format
    private readonly object _lock = new(); // Thread safety lock

    // Timing state
    private double _samplePosition; // Current position in samples
    private double _samplesPerBeat; // Samples between beats
    private int _currentBeat; // Current beat in the bar (0-indexed)
    private double _clickSamplePosition; // Position within click sound
    private bool _isPlayingClick; // Is a click currently playing?
    private double _lastBeatTriggerPosition; // Prevent double-triggering

    // Click sound generation
    private float[]? _clickSamples; // Pregenerated click samples
    private float[]? _accentClickSamples; // Pregenerated accent click samples
    private float[]? _customSamples; // Custom loaded samples
    private float[]? _customAccentSamples; // Custom accent samples (optional)
    private int _clickDurationSamples; // Duration of click in samples

    // Sequencer reference for sync
    private Sequencer? _sequencer;
    private double _lastSequencerBeat; // Last beat position from sequencer
    private bool _disposed;

    // Count-in state
    private int _countIn; // Number of bars to count in (0, 1, 2, or 4)
    private bool _isCountingIn; // Whether we are currently in count-in phase
    private int _countInBeatsRemaining; // Remaining beats in count-in
    private int _countInCurrentBeat; // Current beat within count-in
    private int _countInCurrentBar; // Current bar within count-in (negative, counting up to 0)

    // Event tracking for sequencer sync
    private bool _isAttachedToSequencer; // Whether attached via AttachToSequencer
    private int _lastClickBeat = -1; // Track last click beat to prevent duplicates

    /// <summary>Gets or sets the BPM (beats per minute). Syncs with Sequencer if available.</summary>
    public double Bpm
    {
        get => _sequencer?.Bpm ?? _bpm;
        set
        {
            _bpm = Math.Max(1.0, value);
            if (_sequencer != null)
            {
                _sequencer.Bpm = _bpm;
            }
            UpdateTimingParameters();
        }
    }
    private double _bpm = 120.0;

    /// <summary>Gets or sets the volume level (0.0 to 1.0).</summary>
    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 1f);
    }
    private float _volume = 0.7f;

    /// <summary>Gets or sets whether the metronome is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the beats per bar (time signature numerator, default 4).</summary>
    public int BeatsPerBar
    {
        get => _beatsPerBar;
        set => _beatsPerBar = Math.Max(1, Math.Min(16, value));
    }
    private int _beatsPerBar = 4;

    /// <summary>Gets or sets whether the first beat of each bar is accented (louder click on beat 1).</summary>
    public bool AccentFirstBeat { get; set; } = true;

    /// <summary>Gets or sets the volume multiplier for the accented first beat (default 1.5).</summary>
    public float AccentVolume
    {
        get => _accentVolume;
        set => _accentVolume = Math.Max(1.0f, Math.Min(3.0f, value));
    }
    private float _accentVolume = 1.5f;

    /// <summary>Gets or sets the click sound type.</summary>
    public ClickSound ClickSound
    {
        get => _clickSound;
        set
        {
            if (_clickSound != value)
            {
                _clickSound = value;
                if (value != ClickSound.Custom)
                {
                    GenerateClickSamples();
                }
            }
        }
    }
    private ClickSound _clickSound = ClickSound.Click;

    /// <summary>Gets or sets the click duration in milliseconds (default 15ms).</summary>
    public double ClickDurationMs
    {
        get => _clickDurationMs;
        set
        {
            _clickDurationMs = Math.Max(5.0, Math.Min(100.0, value));
            GenerateClickSamples();
        }
    }
    private double _clickDurationMs = 15.0;

    /// <summary>Gets or sets the base frequency for the click sound in Hz (default 1000 Hz).</summary>
    public double ClickFrequency
    {
        get => _clickFrequency;
        set
        {
            _clickFrequency = Math.Max(200.0, Math.Min(4000.0, value));
            GenerateClickSamples();
        }
    }
    private double _clickFrequency = 1000.0;

    /// <summary>Gets or sets the frequency for the accented beat in Hz (default 1500 Hz, different pitch for accent).</summary>
    public double AccentFrequency
    {
        get => _accentFrequency;
        set
        {
            _accentFrequency = Math.Max(200.0, Math.Min(6000.0, value));
            GenerateClickSamples();
        }
    }
    private double _accentFrequency = 1500.0;

    /// <summary>Gets the current beat within the bar (1-indexed for display).</summary>
    public int CurrentBeatDisplay => _currentBeat + 1;

    /// <summary>Gets or sets the associated sequencer for timing integration.</summary>
    public Sequencer? Sequencer
    {
        get => _sequencer;
        set
        {
            // Detach from old sequencer if attached
            if (_isAttachedToSequencer && _sequencer != null)
            {
                DetachFromSequencer();
            }

            _sequencer = value;
            if (_sequencer != null)
            {
                _bpm = _sequencer.Bpm;
                UpdateTimingParameters();
            }
        }
    }

    /// <summary>
    /// Gets or sets the number of count-in bars before playback starts.
    /// Valid values are 0, 1, 2, or 4. Default is 0 (no count-in).
    /// </summary>
    public int CountIn
    {
        get => _countIn;
        set
        {
            // Only allow valid count-in values
            _countIn = value switch
            {
                0 => 0,
                1 => 1,
                2 => 2,
                >= 4 => 4,
                _ => 0
            };
        }
    }

    /// <summary>Gets whether the metronome is currently in count-in phase.</summary>
    public bool IsCountingIn => _isCountingIn;

    /// <summary>Gets whether the metronome is attached to a sequencer via AttachToSequencer.</summary>
    public bool IsAttachedToSequencer => _isAttachedToSequencer;

    /// <summary>Fired when a metronome click occurs.</summary>
    public event EventHandler<MetronomeClickEventArgs>? MetronomeClick;

    /// <summary>Fired when the count-in phase is complete and playback is about to start.</summary>
    public event EventHandler? CountInComplete;

    /// <summary>ISampleProvider implementation - returns the wave format.</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>
    /// Creates a new metronome with default settings.
    /// </summary>
    public Metronome() : this(null, null) { }

    /// <summary>
    /// Creates a new metronome with optional sample rate and sequencer.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (defaults to Settings.SampleRate).</param>
    /// <param name="sequencer">Optional sequencer to sync with for accurate beat timing.</param>
    public Metronome(int? sampleRate = null, Sequencer? sequencer = null)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels);
        _sequencer = sequencer;

        if (_sequencer != null)
        {
            _bpm = _sequencer.Bpm;
        }

        UpdateTimingParameters();
        GenerateClickSamples();
    }

    /// <summary>
    /// Loads a custom click sound from a file.
    /// Supports WAV and other common audio formats.
    /// </summary>
    /// <param name="path">Path to the audio file for the custom sound.</param>
    public void LoadCustomSound(string path)
    {
        LoadCustomSound(path, null);
    }

    /// <summary>
    /// Loads custom click sounds from files.
    /// Supports WAV and other common audio formats.
    /// </summary>
    /// <param name="path">Path to the audio file for normal clicks.</param>
    /// <param name="accentPath">Optional path to a separate accent sound file.</param>
    public void LoadCustomSound(string path, string? accentPath)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new ArgumentException("Invalid or non-existent audio file path.", nameof(path));
        }

        lock (_lock)
        {
            _customSamples = LoadAudioFile(path);
            _customAccentSamples = !string.IsNullOrWhiteSpace(accentPath) && File.Exists(accentPath)
                ? LoadAudioFile(accentPath)
                : null;

            _clickSound = ClickSound.Custom;
        }
    }

    /// <summary>
    /// Loads an audio file and returns it as a float sample array.
    /// </summary>
    private float[] LoadAudioFile(string path)
    {
        using var reader = new AudioFileReader(path);

        // Resample if necessary
        ISampleProvider source = reader;
        if (reader.WaveFormat.SampleRate != _waveFormat.SampleRate)
        {
            source = new WdlResamplingSampleProvider(reader, _waveFormat.SampleRate);
        }

        // Convert to mono if stereo for simpler processing
        if (source.WaveFormat.Channels > 1)
        {
            source = new StereoToMonoSampleProvider(source);
        }

        // Read all samples into a list
        var samples = new List<float>();
        var buffer = new float[1024];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                samples.Add(buffer[i]);
            }
        }

        return samples.ToArray();
    }

    /// <summary>
    /// Updates timing parameters based on BPM and sample rate.
    /// </summary>
    private void UpdateTimingParameters()
    {
        double effectiveBpm = _sequencer?.Bpm ?? _bpm;
        double secondsPerBeat = 60.0 / effectiveBpm;
        _samplesPerBeat = secondsPerBeat * _waveFormat.SampleRate;
    }

    /// <summary>
    /// Generates the click samples based on current settings.
    /// Creates short sine wave bursts with different pitches for normal and accented beats.
    /// </summary>
    private void GenerateClickSamples()
    {
        lock (_lock)
        {
            _clickDurationSamples = (int)(_clickDurationMs / 1000.0 * _waveFormat.SampleRate);

            switch (_clickSound)
            {
                case ClickSound.Click:
                    _clickSamples = GenerateSineClick(_clickFrequency, _clickDurationSamples);
                    _accentClickSamples = GenerateSineClick(_accentFrequency, _clickDurationSamples);
                    break;

                case ClickSound.Beep:
                    _clickSamples = GenerateBeepClick(_clickFrequency, _clickDurationSamples);
                    _accentClickSamples = GenerateBeepClick(_accentFrequency, _clickDurationSamples);
                    break;

                case ClickSound.Woodblock:
                    _clickSamples = GenerateWoodblockClick(_clickFrequency, _clickDurationSamples);
                    _accentClickSamples = GenerateWoodblockClick(_accentFrequency * 1.2, (int)(_clickDurationSamples * 1.1));
                    break;

                case ClickSound.Custom:
                    // Custom samples are loaded via LoadCustomSound
                    break;
            }
        }
    }

    /// <summary>
    /// Generates a simple sine wave click with envelope (short sine wave burst).
    /// </summary>
    private float[] GenerateSineClick(double frequency, int durationSamples)
    {
        var samples = new float[durationSamples];
        double phaseIncrement = 2.0 * Math.PI * frequency / _waveFormat.SampleRate;
        double phase = 0;

        for (int i = 0; i < durationSamples; i++)
        {
            // Apply envelope (quick attack, exponential decay)
            double envelope = Math.Exp(-5.0 * i / durationSamples);

            // Generate sine wave burst
            samples[i] = (float)(Math.Sin(phase) * envelope);
            phase += phaseIncrement;
            if (phase > 2.0 * Math.PI) phase -= 2.0 * Math.PI;
        }

        return samples;
    }

    /// <summary>
    /// Generates a beep click with slight harmonics for a sharper sound.
    /// </summary>
    private float[] GenerateBeepClick(double frequency, int durationSamples)
    {
        var samples = new float[durationSamples];
        double phaseIncrement1 = 2.0 * Math.PI * frequency / _waveFormat.SampleRate;
        double phaseIncrement2 = 2.0 * Math.PI * frequency * 2.0 / _waveFormat.SampleRate; // First harmonic
        double phase1 = 0;
        double phase2 = 0;

        for (int i = 0; i < durationSamples; i++)
        {
            // Apply envelope
            double envelope = Math.Exp(-4.0 * i / durationSamples);

            // Generate fundamental + harmonic
            double fundamental = Math.Sin(phase1);
            double harmonic = Math.Sin(phase2) * 0.3;

            samples[i] = (float)((fundamental + harmonic) * envelope * 0.8);

            phase1 += phaseIncrement1;
            phase2 += phaseIncrement2;
            if (phase1 > 2.0 * Math.PI) phase1 -= 2.0 * Math.PI;
            if (phase2 > 2.0 * Math.PI) phase2 -= 2.0 * Math.PI;
        }

        return samples;
    }

    /// <summary>
    /// Generates a woodblock-style percussive click with multiple harmonics and fast decay.
    /// </summary>
    private float[] GenerateWoodblockClick(double frequency, int durationSamples)
    {
        var samples = new float[durationSamples];
        double f1 = frequency;
        double f2 = frequency * 2.7; // Inharmonic partial
        double f3 = frequency * 4.2; // Higher inharmonic partial

        double phase1 = 0;
        double phase2 = 0;
        double phase3 = 0;

        double inc1 = 2.0 * Math.PI * f1 / _waveFormat.SampleRate;
        double inc2 = 2.0 * Math.PI * f2 / _waveFormat.SampleRate;
        double inc3 = 2.0 * Math.PI * f3 / _waveFormat.SampleRate;

        for (int i = 0; i < durationSamples; i++)
        {
            // Fast exponential decay characteristic of woodblock
            double envelope = Math.Exp(-8.0 * i / durationSamples);

            // Percussive attack transient
            double attack = i < durationSamples / 20 ? 1.0 : 0.0;

            // Combine partials
            double fundamental = Math.Sin(phase1) * 0.6;
            double partial2 = Math.Sin(phase2) * 0.3;
            double partial3 = Math.Sin(phase3) * 0.1;

            samples[i] = (float)((fundamental + partial2 + partial3 + attack * 0.2) * envelope * 0.7);

            phase1 += inc1;
            phase2 += inc2;
            phase3 += inc3;
            if (phase1 > 2.0 * Math.PI) phase1 -= 2.0 * Math.PI;
            if (phase2 > 2.0 * Math.PI) phase2 -= 2.0 * Math.PI;
            if (phase3 > 2.0 * Math.PI) phase3 -= 2.0 * Math.PI;
        }

        return samples;
    }

    /// <summary>
    /// Resets the metronome to the beginning of a bar.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _samplePosition = 0;
            _currentBeat = 0;
            _clickSamplePosition = 0;
            _isPlayingClick = false;
            _lastSequencerBeat = 0;
            _lastBeatTriggerPosition = -1;
            _isCountingIn = false;
            _countInBeatsRemaining = 0;
            _countInCurrentBeat = 0;
            _countInCurrentBar = 0;
            _lastClickBeat = -1;
        }
    }

    /// <summary>
    /// Attaches the metronome to a sequencer for automatic synchronization.
    /// The metronome will automatically sync BPM and start/stop with the sequencer.
    /// </summary>
    /// <param name="sequencer">The sequencer to attach to.</param>
    public void AttachToSequencer(Sequencer sequencer)
    {
        ArgumentNullException.ThrowIfNull(sequencer);

        lock (_lock)
        {
            // Detach from previous sequencer if any
            if (_isAttachedToSequencer && _sequencer != null)
            {
                DetachFromSequencerInternal();
            }

            _sequencer = sequencer;
            _bpm = sequencer.Bpm;
            UpdateTimingParameters();

            // Subscribe to sequencer events
            sequencer.BpmChanged += OnSequencerBpmChanged;
            sequencer.PlaybackStarted += OnSequencerPlaybackStarted;
            sequencer.PlaybackStopped += OnSequencerPlaybackStopped;
            sequencer.BeatChanged += OnSequencerBeatChanged;

            _isAttachedToSequencer = true;
        }
    }

    /// <summary>
    /// Detaches the metronome from the currently attached sequencer.
    /// </summary>
    public void DetachFromSequencer()
    {
        lock (_lock)
        {
            DetachFromSequencerInternal();
        }
    }

    /// <summary>
    /// Internal method to detach from sequencer (must be called within lock).
    /// </summary>
    private void DetachFromSequencerInternal()
    {
        if (_sequencer != null && _isAttachedToSequencer)
        {
            _sequencer.BpmChanged -= OnSequencerBpmChanged;
            _sequencer.PlaybackStarted -= OnSequencerPlaybackStarted;
            _sequencer.PlaybackStopped -= OnSequencerPlaybackStopped;
            _sequencer.BeatChanged -= OnSequencerBeatChanged;
        }

        _isAttachedToSequencer = false;
        _isCountingIn = false;
        _countInBeatsRemaining = 0;
    }

    /// <summary>
    /// Starts the count-in phase. Called internally when sequencer starts with CountIn > 0.
    /// </summary>
    internal void StartCountIn()
    {
        if (_countIn <= 0) return;

        lock (_lock)
        {
            _isCountingIn = true;
            _countInBeatsRemaining = _countIn * _beatsPerBar;
            _countInCurrentBeat = 0;
            _countInCurrentBar = -_countIn; // Start at negative bar number
            _lastClickBeat = -1;

            // Reset position for count-in
            _samplePosition = 0;
            _lastBeatTriggerPosition = -1;
        }
    }

    /// <summary>
    /// Handles BPM changes from the attached sequencer.
    /// </summary>
    private void OnSequencerBpmChanged(object? sender, ParameterChangedEventArgs e)
    {
        lock (_lock)
        {
            _bpm = (double)e.NewValue;
            UpdateTimingParameters();
        }
    }

    /// <summary>
    /// Handles playback start from the attached sequencer.
    /// </summary>
    private void OnSequencerPlaybackStarted(object? sender, PlaybackStateEventArgs e)
    {
        // Count-in is handled by the sequencer's Start() method via EnableMetronome
        // This handler is for sync when metronome is attached but sequencer controls start
        if (!_isCountingIn)
        {
            Reset();
        }
    }

    /// <summary>
    /// Handles playback stop from the attached sequencer.
    /// </summary>
    private void OnSequencerPlaybackStopped(object? sender, PlaybackStateEventArgs e)
    {
        lock (_lock)
        {
            _isCountingIn = false;
            _countInBeatsRemaining = 0;
            _lastClickBeat = -1;
        }
    }

    /// <summary>
    /// Handles beat changes from the attached sequencer to trigger clicks.
    /// </summary>
    private void OnSequencerBeatChanged(object? sender, BeatChangedEventArgs e)
    {
        if (!Enabled) return;

        lock (_lock)
        {
            // Calculate current beat in bar
            int beatInBar = (int)Math.Floor(e.CyclePosition) % _beatsPerBar;
            int absoluteBeat = (int)Math.Floor(e.CurrentBeat);

            // Prevent duplicate clicks on the same beat
            if (absoluteBeat == _lastClickBeat) return;
            _lastClickBeat = absoluteBeat;

            // Calculate bar number
            int bar = (int)Math.Floor(e.CurrentBeat / _beatsPerBar) + 1;

            // Trigger click
            bool isDownbeat = beatInBar == 0;
            TriggerClick(beatInBar, isDownbeat, false, bar);
        }
    }

    /// <summary>
    /// Triggers a metronome click and fires the MetronomeClick event.
    /// </summary>
    private void TriggerClick(int beat, bool isDownbeat, bool isCountIn, int bar)
    {
        // Start playing the click sound
        _isPlayingClick = true;
        _clickSamplePosition = 0;
        _currentBeat = beat;

        // Fire the event
        MetronomeClick?.Invoke(this, new MetronomeClickEventArgs(
            beat + 1, // 1-indexed for display
            isDownbeat,
            isCountIn,
            bar
        ));
    }

    /// <summary>
    /// Processes count-in beats and returns true when count-in is complete.
    /// </summary>
    internal bool ProcessCountIn(double deltaBeats)
    {
        if (!_isCountingIn || _countIn <= 0) return true;

        lock (_lock)
        {
            // Track beat transitions during count-in
            double oldPosition = _samplePosition / _samplesPerBeat;
            double newPosition = oldPosition + deltaBeats;

            int oldBeat = (int)Math.Floor(oldPosition);
            int newBeat = (int)Math.Floor(newPosition);

            // Check for beat transition
            if (newBeat > oldBeat || (oldPosition == 0 && _countInCurrentBeat == 0))
            {
                int beatInBar = _countInCurrentBeat % _beatsPerBar;
                bool isDownbeat = beatInBar == 0;

                TriggerClick(beatInBar, isDownbeat, true, _countInCurrentBar);

                _countInCurrentBeat++;
                _countInBeatsRemaining--;

                // Update bar number at bar boundaries
                if (_countInCurrentBeat % _beatsPerBar == 0)
                {
                    _countInCurrentBar++;
                }

                // Check if count-in is complete
                if (_countInBeatsRemaining <= 0)
                {
                    _isCountingIn = false;
                    CountInComplete?.Invoke(this, EventArgs.Empty);
                    Reset();
                    return true; // Count-in complete
                }
            }

            _samplePosition = newPosition * _samplesPerBeat;
            return false; // Still counting in
        }
    }

    /// <summary>
    /// Reads audio samples into the buffer. Implements ISampleProvider.Read.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        // Clear buffer
        for (int i = 0; i < count; i++)
        {
            buffer[offset + i] = 0;
        }

        if (!Enabled)
        {
            return count;
        }

        lock (_lock)
        {
            // Sync with sequencer if available for accurate beat timing
            if (_sequencer != null && _sequencer.IsRunning)
            {
                SyncWithSequencer();
            }

            int channels = _waveFormat.Channels;
            int sampleFrames = count / channels;

            for (int frame = 0; frame < sampleFrames; frame++)
            {
                // Check if we should start a new click based on beat position
                if (!_isPlayingClick)
                {
                    double beatPosition = _samplePosition / _samplesPerBeat;
                    double beatFraction = beatPosition - Math.Floor(beatPosition);
                    double currentBeatFloor = Math.Floor(beatPosition);

                    // Start click at the beginning of each beat (avoid double-triggering)
                    if ((beatFraction < 0.01 || _samplePosition == 0) && currentBeatFloor != _lastBeatTriggerPosition)
                    {
                        _isPlayingClick = true;
                        _clickSamplePosition = 0;
                        _currentBeat = ((int)currentBeatFloor) % _beatsPerBar;
                        _lastBeatTriggerPosition = currentBeatFloor;
                    }
                }

                // Generate click sound if playing
                float sample = 0;
                if (_isPlayingClick)
                {
                    sample = GetClickSample();
                }

                // Apply volume
                sample *= _volume;

                // Write to all channels
                for (int ch = 0; ch < channels; ch++)
                {
                    int bufferIndex = offset + frame * channels + ch;
                    if (bufferIndex < buffer.Length)
                    {
                        buffer[bufferIndex] = sample;
                    }
                }

                // Advance sample position (if not synced to sequencer)
                if (_sequencer == null || !_sequencer.IsRunning)
                {
                    _samplePosition++;
                    if (_samplePosition >= _samplesPerBeat * _beatsPerBar)
                    {
                        _samplePosition = 0;
                        _lastBeatTriggerPosition = -1; // Reset trigger guard on bar loop
                    }
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Synchronizes metronome position with the sequencer's CurrentBeat.
    /// </summary>
    private void SyncWithSequencer()
    {
        if (_sequencer == null) return;

        // Update BPM if changed
        if (Math.Abs(_sequencer.Bpm - _bpm) > 0.001)
        {
            _bpm = _sequencer.Bpm;
            UpdateTimingParameters();
        }

        // Sync beat position with Sequencer.CurrentBeat
        double sequencerBeat = _sequencer.CurrentBeat;
        if (Math.Abs(sequencerBeat - _lastSequencerBeat) > 0.001)
        {
            _samplePosition = (sequencerBeat % _beatsPerBar) * _samplesPerBeat;
            _lastSequencerBeat = sequencerBeat;
        }
    }

    /// <summary>
    /// Gets the current click sample and advances the click position.
    /// Uses different pitch for accented beat.
    /// </summary>
    private float GetClickSample()
    {
        bool isAccent = AccentFirstBeat && _currentBeat == 0;
        float[]? clickBuffer;

        if (_clickSound == ClickSound.Custom)
        {
            clickBuffer = isAccent && _customAccentSamples != null
                ? _customAccentSamples
                : _customSamples;
        }
        else
        {
            // Different pitch for accented beat
            clickBuffer = isAccent ? _accentClickSamples : _clickSamples;
        }

        if (clickBuffer == null || _clickSamplePosition >= clickBuffer.Length)
        {
            _isPlayingClick = false;
            return 0;
        }

        float sample = clickBuffer[(int)_clickSamplePosition];

        // Apply accent volume (louder click on beat 1)
        if (isAccent)
        {
            sample *= _accentVolume;
        }

        _clickSamplePosition++;

        if (_clickSamplePosition >= clickBuffer.Length)
        {
            _isPlayingClick = false;
        }

        return sample;
    }

    /// <summary>
    /// Disposes of resources used by the metronome.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            // Detach from sequencer first
            if (_isAttachedToSequencer)
            {
                DetachFromSequencerInternal();
            }

            _clickSamples = null;
            _accentClickSamples = null;
            _customSamples = null;
            _customAccentSamples = null;
        }

        GC.SuppressFinalize(this);
    }

    ~Metronome()
    {
        Dispose();
    }
}
