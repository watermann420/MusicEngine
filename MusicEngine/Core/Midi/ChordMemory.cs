// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MIDI handling component.

namespace MusicEngine.Core.Midi;

/// <summary>
/// Strum direction for chord triggering.
/// </summary>
public enum StrumDirection
{
    /// <summary>No strumming, all notes play simultaneously.</summary>
    None,
    /// <summary>Strum from lowest to highest note.</summary>
    Up,
    /// <summary>Strum from highest to lowest note.</summary>
    Down,
    /// <summary>Alternate between up and down.</summary>
    Alternate,
    /// <summary>Random order.</summary>
    Random
}

/// <summary>
/// Represents a stored chord voicing.
/// </summary>
public class ChordVoicing
{
    /// <summary>Unique identifier for this voicing.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Display name for this voicing.</summary>
    public string Name { get; set; } = "";

    /// <summary>Intervals from root note (in semitones).</summary>
    public int[] Intervals { get; set; } = Array.Empty<int>();

    /// <summary>Velocity multipliers for each note (1.0 = original velocity).</summary>
    public float[] VelocityMultipliers { get; set; } = Array.Empty<float>();

    /// <summary>Default inversion (0 = root position).</summary>
    public int DefaultInversion { get; set; }

    /// <summary>Strum direction.</summary>
    public StrumDirection Strum { get; set; } = StrumDirection.None;

    /// <summary>Strum time in milliseconds (total time to strum all notes).</summary>
    public double StrumTimeMs { get; set; } = 30;

    /// <summary>
    /// Creates an empty chord voicing.
    /// </summary>
    public ChordVoicing()
    {
    }

    /// <summary>
    /// Creates a chord voicing from intervals.
    /// </summary>
    /// <param name="name">Display name.</param>
    /// <param name="intervals">Intervals from root in semitones.</param>
    public ChordVoicing(string name, params int[] intervals)
    {
        Name = name;
        Intervals = intervals;
        VelocityMultipliers = new float[intervals.Length];
        for (int i = 0; i < intervals.Length; i++)
        {
            VelocityMultipliers[i] = 1.0f;
        }
    }

    /// <summary>
    /// Gets the chord notes for a given root note.
    /// </summary>
    /// <param name="rootNote">MIDI root note (0-127).</param>
    /// <param name="inversion">Inversion to apply (0 = root position).</param>
    /// <returns>Array of MIDI note numbers.</returns>
    public int[] GetNotes(int rootNote, int inversion = -1)
    {
        if (Intervals.Length == 0) return new[] { rootNote };

        int inv = inversion >= 0 ? inversion : DefaultInversion;
        inv = inv % Intervals.Length;

        var notes = new int[Intervals.Length];
        for (int i = 0; i < Intervals.Length; i++)
        {
            int noteIndex = (i + inv) % Intervals.Length;
            int octaveShift = (i + inv) / Intervals.Length;
            notes[i] = Math.Clamp(rootNote + Intervals[noteIndex] + (octaveShift * 12), 0, 127);
        }

        return notes;
    }

    /// <summary>
    /// Gets velocities for each note based on base velocity.
    /// </summary>
    /// <param name="baseVelocity">Base velocity (0-127).</param>
    /// <returns>Array of velocities for each note.</returns>
    public int[] GetVelocities(int baseVelocity)
    {
        var velocities = new int[Intervals.Length];
        for (int i = 0; i < Intervals.Length; i++)
        {
            float mult = i < VelocityMultipliers.Length ? VelocityMultipliers[i] : 1.0f;
            velocities[i] = Math.Clamp((int)(baseVelocity * mult), 1, 127);
        }
        return velocities;
    }

    /// <summary>
    /// Creates a copy of this voicing.
    /// </summary>
    public ChordVoicing Clone()
    {
        return new ChordVoicing
        {
            Name = Name,
            Intervals = (int[])Intervals.Clone(),
            VelocityMultipliers = (float[])VelocityMultipliers.Clone(),
            DefaultInversion = DefaultInversion,
            Strum = Strum,
            StrumTimeMs = StrumTimeMs
        };
    }
}

/// <summary>
/// Event arguments for chord trigger events.
/// </summary>
public class ChordTriggerEventArgs : EventArgs
{
    /// <summary>Root note that triggered the chord.</summary>
    public required int RootNote { get; init; }

    /// <summary>Original velocity.</summary>
    public required int Velocity { get; init; }

    /// <summary>The voicing that was triggered.</summary>
    public required ChordVoicing Voicing { get; init; }

    /// <summary>The actual notes being played.</summary>
    public required int[] Notes { get; init; }

    /// <summary>Velocities for each note.</summary>
    public required int[] Velocities { get; init; }

    /// <summary>Delay times in milliseconds for each note (for strumming).</summary>
    public required double[] DelayTimesMs { get; init; }
}

/// <summary>
/// Chord memory system for storing and recalling chord voicings.
/// Maps single trigger notes to full chord voicings with inversion and strum options.
/// </summary>
public class ChordMemory : IDisposable
{
    private readonly Dictionary<int, ChordVoicing> _voicings = new();
    private readonly Dictionary<int, ChordVoicing> _rootMappings = new();
    private readonly object _lock = new();
    private readonly Random _random = new();
    private bool _disposed;
    private StrumDirection _lastStrumDirection = StrumDirection.Up;

    /// <summary>Whether chord memory is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Global inversion offset (added to each voicing's default inversion).</summary>
    public int GlobalInversion { get; set; }

    /// <summary>Global strum override (None = use voicing's strum setting).</summary>
    public StrumDirection? GlobalStrumOverride { get; set; }

    /// <summary>Global strum time multiplier.</summary>
    public double StrumTimeMultiplier { get; set; } = 1.0;

    /// <summary>Base octave for learning chords (C4 = 60).</summary>
    public int BaseOctave { get; set; } = 60;

    /// <summary>Number of stored voicings.</summary>
    public int VoicingCount => _voicings.Count;

    /// <summary>Number of root note mappings.</summary>
    public int MappingCount => _rootMappings.Count;

    /// <summary>Fired when a chord is triggered.</summary>
    public event EventHandler<ChordTriggerEventArgs>? ChordTriggered;

    /// <summary>
    /// Creates a new chord memory system.
    /// </summary>
    public ChordMemory()
    {
        LoadBuiltInVoicings();
    }

    /// <summary>
    /// Stores a chord voicing.
    /// </summary>
    /// <param name="voicing">The voicing to store.</param>
    public void StoreVoicing(ChordVoicing voicing)
    {
        if (voicing == null)
            throw new ArgumentNullException(nameof(voicing));

        lock (_lock)
        {
            _voicings[voicing.Id.GetHashCode()] = voicing;
        }
    }

    /// <summary>
    /// Stores a chord voicing and maps it to a root note.
    /// </summary>
    /// <param name="rootNote">Root note (0-11 for note class, or full MIDI note).</param>
    /// <param name="voicing">The voicing to store and map.</param>
    public void StoreVoicing(int rootNote, ChordVoicing voicing)
    {
        if (voicing == null)
            throw new ArgumentNullException(nameof(voicing));

        int noteClass = rootNote % 12;

        lock (_lock)
        {
            _voicings[voicing.Id.GetHashCode()] = voicing;
            _rootMappings[noteClass] = voicing;
        }
    }

    /// <summary>
    /// Maps a root note to an existing voicing.
    /// </summary>
    /// <param name="rootNote">Root note (0-11 for note class, or full MIDI note).</param>
    /// <param name="voicing">The voicing to map.</param>
    public void MapToRoot(int rootNote, ChordVoicing voicing)
    {
        if (voicing == null)
            throw new ArgumentNullException(nameof(voicing));

        int noteClass = rootNote % 12;

        lock (_lock)
        {
            _rootMappings[noteClass] = voicing;
        }
    }

    /// <summary>
    /// Gets the voicing mapped to a root note.
    /// </summary>
    /// <param name="rootNote">Root note (any MIDI note).</param>
    /// <returns>The mapped voicing, or null if none.</returns>
    public ChordVoicing? GetVoicing(int rootNote)
    {
        int noteClass = rootNote % 12;

        lock (_lock)
        {
            return _rootMappings.TryGetValue(noteClass, out var voicing) ? voicing : null;
        }
    }

    /// <summary>
    /// Gets all stored voicings.
    /// </summary>
    public IReadOnlyCollection<ChordVoicing> GetAllVoicings()
    {
        lock (_lock)
        {
            return _voicings.Values.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Clears the mapping for a root note.
    /// </summary>
    /// <param name="rootNote">Root note to clear.</param>
    public void ClearMapping(int rootNote)
    {
        int noteClass = rootNote % 12;

        lock (_lock)
        {
            _rootMappings.Remove(noteClass);
        }
    }

    /// <summary>
    /// Clears all mappings.
    /// </summary>
    public void ClearAllMappings()
    {
        lock (_lock)
        {
            _rootMappings.Clear();
        }
    }

    /// <summary>
    /// Processes an input note and returns chord notes.
    /// </summary>
    /// <param name="inputNote">Input MIDI note.</param>
    /// <param name="velocity">Input velocity.</param>
    /// <returns>Chord trigger info, or null if no chord mapped.</returns>
    public ChordTriggerEventArgs? ProcessNoteOn(int inputNote, int velocity)
    {
        if (!Enabled) return null;

        var voicing = GetVoicing(inputNote);
        if (voicing == null) return null;

        int inversion = voicing.DefaultInversion + GlobalInversion;
        var notes = voicing.GetNotes(inputNote, inversion);
        var velocities = voicing.GetVelocities(velocity);

        // Calculate strum delays
        var strum = GlobalStrumOverride ?? voicing.Strum;
        var delays = CalculateStrumDelays(notes.Length, voicing.StrumTimeMs * StrumTimeMultiplier, strum);

        var args = new ChordTriggerEventArgs
        {
            RootNote = inputNote,
            Velocity = velocity,
            Voicing = voicing,
            Notes = notes,
            Velocities = velocities,
            DelayTimesMs = delays
        };

        ChordTriggered?.Invoke(this, args);
        return args;
    }

    /// <summary>
    /// Learns a chord from a set of notes and maps it to the lowest note.
    /// </summary>
    /// <param name="notes">The notes that make up the chord.</param>
    /// <param name="name">Optional name for the voicing.</param>
    /// <returns>The learned voicing.</returns>
    public ChordVoicing LearnChord(int[] notes, string? name = null)
    {
        if (notes == null || notes.Length == 0)
            throw new ArgumentException("Notes array cannot be empty.", nameof(notes));

        var sorted = notes.OrderBy(n => n).ToArray();
        int root = sorted[0];

        var intervals = new int[sorted.Length];
        for (int i = 0; i < sorted.Length; i++)
        {
            intervals[i] = sorted[i] - root;
        }

        var voicing = new ChordVoicing
        {
            Name = name ?? $"Learned Chord ({notes.Length} notes)",
            Intervals = intervals,
            VelocityMultipliers = Enumerable.Repeat(1.0f, intervals.Length).ToArray()
        };

        StoreVoicing(root, voicing);
        return voicing;
    }

    private double[] CalculateStrumDelays(int noteCount, double totalTimeMs, StrumDirection direction)
    {
        var delays = new double[noteCount];
        if (noteCount <= 1 || direction == StrumDirection.None)
        {
            return delays; // All zeros
        }

        double interval = totalTimeMs / (noteCount - 1);

        switch (direction)
        {
            case StrumDirection.Up:
                for (int i = 0; i < noteCount; i++)
                {
                    delays[i] = i * interval;
                }
                break;

            case StrumDirection.Down:
                for (int i = 0; i < noteCount; i++)
                {
                    delays[i] = (noteCount - 1 - i) * interval;
                }
                break;

            case StrumDirection.Alternate:
                _lastStrumDirection = _lastStrumDirection == StrumDirection.Up
                    ? StrumDirection.Down
                    : StrumDirection.Up;
                return CalculateStrumDelays(noteCount, totalTimeMs, _lastStrumDirection);

            case StrumDirection.Random:
                var indices = Enumerable.Range(0, noteCount).OrderBy(_ => _random.Next()).ToArray();
                for (int i = 0; i < noteCount; i++)
                {
                    delays[indices[i]] = i * interval;
                }
                break;
        }

        return delays;
    }

    private void LoadBuiltInVoicings()
    {
        // Major triads
        StoreVoicing(new ChordVoicing("Major", 0, 4, 7));
        StoreVoicing(new ChordVoicing("Minor", 0, 3, 7));
        StoreVoicing(new ChordVoicing("Diminished", 0, 3, 6));
        StoreVoicing(new ChordVoicing("Augmented", 0, 4, 8));

        // 7th chords
        StoreVoicing(new ChordVoicing("Major 7", 0, 4, 7, 11));
        StoreVoicing(new ChordVoicing("Minor 7", 0, 3, 7, 10));
        StoreVoicing(new ChordVoicing("Dominant 7", 0, 4, 7, 10));
        StoreVoicing(new ChordVoicing("Diminished 7", 0, 3, 6, 9));

        // Sus chords
        StoreVoicing(new ChordVoicing("Sus2", 0, 2, 7));
        StoreVoicing(new ChordVoicing("Sus4", 0, 5, 7));

        // Power chord
        StoreVoicing(new ChordVoicing("Power", 0, 7, 12));

        // Extended chords
        StoreVoicing(new ChordVoicing("Add9", 0, 4, 7, 14));
        StoreVoicing(new ChordVoicing("Major 9", 0, 4, 7, 11, 14));
        StoreVoicing(new ChordVoicing("Minor 9", 0, 3, 7, 10, 14));
    }

    #region Preset Voicings

    /// <summary>
    /// Creates a close-voiced major triad.
    /// </summary>
    public static ChordVoicing MajorTriad() => new("Major Triad", 0, 4, 7);

    /// <summary>
    /// Creates a close-voiced minor triad.
    /// </summary>
    public static ChordVoicing MinorTriad() => new("Minor Triad", 0, 3, 7);

    /// <summary>
    /// Creates an open-voiced major chord (root-5th-10th).
    /// </summary>
    public static ChordVoicing OpenMajor() => new("Open Major", 0, 7, 16);

    /// <summary>
    /// Creates a piano-style jazz voicing.
    /// </summary>
    public static ChordVoicing JazzVoicing() => new("Jazz Voicing", 0, 4, 10, 14, 17);

    /// <summary>
    /// Creates a guitar-style power chord with octave.
    /// </summary>
    public static ChordVoicing PowerChord() => new("Power Chord", 0, 7, 12);

    /// <summary>
    /// Creates an organ-style chord with doubled octaves.
    /// </summary>
    public static ChordVoicing OrganChord() => new("Organ Chord", -12, 0, 4, 7, 12);

    /// <summary>
    /// Creates a pad-style wide voicing.
    /// </summary>
    public static ChordVoicing PadVoicing() => new("Pad Voicing", 0, 7, 12, 16, 19);

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            _voicings.Clear();
            _rootMappings.Clear();
        }

        GC.SuppressFinalize(this);
    }
}
