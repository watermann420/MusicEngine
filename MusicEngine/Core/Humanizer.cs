//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Humanizer that adds natural timing and velocity variations to MIDI patterns.

namespace MusicEngine.Core;

/// <summary>
/// Humanization mode for different styles
/// </summary>
public enum HumanizeMode
{
    /// <summary>Subtle humanization for tight performances</summary>
    Subtle,
    /// <summary>Natural feel for acoustic instruments</summary>
    Natural,
    /// <summary>Loose/laid-back feel</summary>
    Loose,
    /// <summary>Drunk/sloppy timing</summary>
    Drunk,
    /// <summary>Custom settings</summary>
    Custom
}

/// <summary>
/// Humanizer that adds natural timing and velocity variations to MIDI patterns.
/// Makes programmed sequences sound more human and less mechanical.
/// </summary>
public class Humanizer
{
    /// <summary>Humanization mode preset</summary>
    public HumanizeMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            ApplyModePreset(value);
        }
    }
    private HumanizeMode _mode = HumanizeMode.Natural;

    /// <summary>Timing variation in milliseconds (0-100)</summary>
    public float TimingAmount { get; set; } = 10f;

    /// <summary>Velocity variation amount (0-50)</summary>
    public int VelocityAmount { get; set; } = 15;

    /// <summary>Duration variation as percentage (0-0.5)</summary>
    public float DurationAmount { get; set; } = 0.1f;

    /// <summary>Timing bias: negative = behind beat, positive = ahead (-1 to 1)</summary>
    public float TimingBias { get; set; } = 0f;

    /// <summary>Velocity bias: negative = softer, positive = harder (-1 to 1)</summary>
    public float VelocityBias { get; set; } = 0f;

    /// <summary>Preserve accents (reduce humanization on high velocity notes)</summary>
    public bool PreserveAccents { get; set; } = true;

    /// <summary>Accent velocity threshold (notes above this are accents)</summary>
    public int AccentThreshold { get; set; } = 100;

    /// <summary>Downbeat strength (reduce timing variation on beat 1)</summary>
    public float DownbeatStrength { get; set; } = 0.5f;

    /// <summary>Seed for reproducible randomization (null = random)</summary>
    public int? Seed
    {
        get => _seed;
        set
        {
            _seed = value;
            if (value.HasValue)
            {
                _random = new Random(value.Value);
            }
        }
    }
    private int? _seed;
    private Random _random;

    public Humanizer()
    {
        _random = new Random();
        ApplyModePreset(HumanizeMode.Natural);
    }

    public Humanizer(HumanizeMode mode)
    {
        _random = new Random();
        Mode = mode;
    }

    private void ApplyModePreset(HumanizeMode mode)
    {
        switch (mode)
        {
            case HumanizeMode.Subtle:
                TimingAmount = 5f;
                VelocityAmount = 8;
                DurationAmount = 0.05f;
                TimingBias = 0f;
                VelocityBias = 0f;
                break;

            case HumanizeMode.Natural:
                TimingAmount = 12f;
                VelocityAmount = 15;
                DurationAmount = 0.1f;
                TimingBias = -0.1f; // Slightly behind
                VelocityBias = 0f;
                break;

            case HumanizeMode.Loose:
                TimingAmount = 25f;
                VelocityAmount = 20;
                DurationAmount = 0.15f;
                TimingBias = -0.2f; // More behind the beat
                VelocityBias = -0.1f;
                break;

            case HumanizeMode.Drunk:
                TimingAmount = 50f;
                VelocityAmount = 30;
                DurationAmount = 0.25f;
                TimingBias = 0f;
                VelocityBias = 0f;
                break;

            case HumanizeMode.Custom:
                // Keep current settings
                break;
        }
    }

    /// <summary>
    /// Humanize a single note event
    /// </summary>
    public NoteEvent HumanizeNote(NoteEvent note, double bpm, int beatPosition = 0)
    {
        // Calculate accent factor (reduce humanization for accents)
        float accentFactor = 1f;
        if (PreserveAccents && note.Velocity >= AccentThreshold)
        {
            accentFactor = 0.3f;
        }

        // Calculate downbeat factor (reduce timing variation on beat 1)
        float downbeatFactor = 1f;
        if (beatPosition == 0)
        {
            downbeatFactor = 1f - DownbeatStrength;
        }

        // Humanize timing
        double timingVariation = GetGaussianRandom() * TimingAmount * accentFactor * downbeatFactor;
        timingVariation += TimingBias * TimingAmount;

        // Convert ms to beats
        double msPerBeat = 60000.0 / bpm;
        double beatVariation = timingVariation / msPerBeat;

        double newStartBeat = note.Beat + beatVariation;
        if (newStartBeat < 0) newStartBeat = 0;

        // Humanize velocity
        int velocityVariation = (int)(GetGaussianRandom() * VelocityAmount * accentFactor);
        velocityVariation += (int)(VelocityBias * VelocityAmount);

        int newVelocity = note.Velocity + velocityVariation;
        newVelocity = Math.Clamp(newVelocity, 1, 127);

        // Humanize duration
        double durationVariation = GetGaussianRandom() * DurationAmount * accentFactor;
        double newDuration = note.Duration * (1 + durationVariation);
        if (newDuration < 0.01) newDuration = 0.01;

        return new NoteEvent { Note = note.Note, Velocity = newVelocity, Beat = newStartBeat, Duration = newDuration };
    }

    /// <summary>
    /// Humanize an entire pattern
    /// </summary>
    public Pattern HumanizePattern(Pattern pattern, double bpm)
    {
        var humanizedPattern = new Pattern(pattern.Synth)
        {
            Name = pattern.Name + " (Humanized)",
            LoopLength = pattern.LoopLength
        };

        foreach (var note in pattern.Events)
        {
            // Determine beat position (0-3 for 4/4)
            int beatPosition = (int)(note.Beat % 4);
            var humanizedNote = HumanizeNote(note, bpm, beatPosition);
            humanizedPattern.Events.Add(humanizedNote);
        }

        return humanizedPattern;
    }

    /// <summary>
    /// Humanize a list of note events
    /// </summary>
    public List<NoteEvent> HumanizeNotes(IEnumerable<NoteEvent> notes, double bpm)
    {
        var result = new List<NoteEvent>();

        foreach (var note in notes)
        {
            int beatPosition = (int)(note.Beat % 4);
            result.Add(HumanizeNote(note, bpm, beatPosition));
        }

        return result;
    }

    /// <summary>
    /// Apply groove-aware humanization (less on strong beats)
    /// </summary>
    public Pattern HumanizeWithGroove(Pattern pattern, double bpm, int[] strongBeats)
    {
        var humanizedPattern = new Pattern(pattern.Synth)
        {
            Name = pattern.Name + " (Groove Humanized)",
            LoopLength = pattern.LoopLength
        };

        foreach (var note in pattern.Events)
        {
            int beatPosition = (int)(note.Beat % 4);
            bool isStrongBeat = strongBeats.Contains(beatPosition);

            // Temporarily adjust settings for strong beats
            float originalTiming = TimingAmount;
            int originalVelocity = VelocityAmount;

            if (isStrongBeat)
            {
                TimingAmount *= 0.3f;
                VelocityAmount = (int)(VelocityAmount * 0.5f);
            }

            var humanizedNote = HumanizeNote(note, bpm, beatPosition);
            humanizedPattern.Events.Add(humanizedNote);

            // Restore settings
            TimingAmount = originalTiming;
            VelocityAmount = originalVelocity;
        }

        return humanizedPattern;
    }

    /// <summary>
    /// Gaussian random number with mean 0 and standard deviation 1
    /// </summary>
    private double GetGaussianRandom()
    {
        // Box-Muller transform
        double u1 = 1.0 - _random.NextDouble();
        double u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    /// <summary>
    /// Reset random generator (for reproducible results with seed)
    /// </summary>
    public void Reset()
    {
        if (_seed.HasValue)
        {
            _random = new Random(_seed.Value);
        }
        else
        {
            _random = new Random();
        }
    }

    #region Presets

    /// <summary>Create humanizer for acoustic drums</summary>
    public static Humanizer CreateForDrums()
    {
        return new Humanizer
        {
            Mode = HumanizeMode.Custom,
            TimingAmount = 8f,
            VelocityAmount = 20,
            DurationAmount = 0.05f,
            TimingBias = 0f,
            VelocityBias = 0f,
            PreserveAccents = true,
            AccentThreshold = 110,
            DownbeatStrength = 0.7f
        };
    }

    /// <summary>Create humanizer for piano/keys</summary>
    public static Humanizer CreateForPiano()
    {
        return new Humanizer
        {
            Mode = HumanizeMode.Custom,
            TimingAmount = 15f,
            VelocityAmount = 18,
            DurationAmount = 0.12f,
            TimingBias = -0.15f, // Slightly behind
            VelocityBias = 0f,
            PreserveAccents = true,
            AccentThreshold = 100,
            DownbeatStrength = 0.4f
        };
    }

    /// <summary>Create humanizer for bass</summary>
    public static Humanizer CreateForBass()
    {
        return new Humanizer
        {
            Mode = HumanizeMode.Custom,
            TimingAmount = 6f,
            VelocityAmount = 10,
            DurationAmount = 0.08f,
            TimingBias = 0.05f, // Slightly ahead (driving)
            VelocityBias = 0f,
            PreserveAccents = false,
            DownbeatStrength = 0.8f
        };
    }

    /// <summary>Create humanizer for strings/pads</summary>
    public static Humanizer CreateForStrings()
    {
        return new Humanizer
        {
            Mode = HumanizeMode.Custom,
            TimingAmount = 20f,
            VelocityAmount = 12,
            DurationAmount = 0.15f,
            TimingBias = -0.1f,
            VelocityBias = 0f,
            PreserveAccents = false,
            DownbeatStrength = 0.3f
        };
    }

    #endregion
}
