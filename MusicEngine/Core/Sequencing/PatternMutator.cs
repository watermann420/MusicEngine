// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Pattern container for musical events.

namespace MusicEngine.Core.Sequencing;

/// <summary>
/// Types of mutation operations.
/// </summary>
public enum MutationType
{
    /// <summary>Shift notes in time.</summary>
    TimeShift,
    /// <summary>Randomize velocity.</summary>
    VelocityRandomize,
    /// <summary>Change pitch within scale.</summary>
    PitchVariation,
    /// <summary>Add new notes.</summary>
    NoteAddition,
    /// <summary>Remove existing notes.</summary>
    NoteRemoval,
    /// <summary>Reverse the pattern.</summary>
    Reverse,
    /// <summary>Invert the pitches.</summary>
    Invert,
    /// <summary>Retrograde inversion.</summary>
    RetrogradeInvert,
    /// <summary>Augment (double) durations.</summary>
    Augment,
    /// <summary>Diminish (halve) durations.</summary>
    Diminish,
    /// <summary>Shuffle note order.</summary>
    Shuffle
}

/// <summary>
/// Options for pattern mutation.
/// </summary>
public class MutationOptions
{
    /// <summary>Probability of any mutation occurring (0-1).</summary>
    public double MutationProbability { get; set; } = 0.3;

    /// <summary>Time shift range in beats.</summary>
    public double TimeShiftRange { get; set; } = 0.125;

    /// <summary>Probability of time shift per note.</summary>
    public double TimeShiftProbability { get; set; } = 0.2;

    /// <summary>Velocity randomization range.</summary>
    public int VelocityRange { get; set; } = 20;

    /// <summary>Probability of velocity change per note.</summary>
    public double VelocityProbability { get; set; } = 0.3;

    /// <summary>Maximum pitch shift in scale degrees.</summary>
    public int PitchShiftDegrees { get; set; } = 2;

    /// <summary>Probability of pitch shift per note.</summary>
    public double PitchProbability { get; set; } = 0.15;

    /// <summary>Probability of adding a new note.</summary>
    public double AddNoteProbability { get; set; } = 0.1;

    /// <summary>Maximum notes to add per mutation.</summary>
    public int MaxNotesToAdd { get; set; } = 4;

    /// <summary>Probability of removing a note.</summary>
    public double RemoveNoteProbability { get; set; } = 0.08;

    /// <summary>Maximum percentage of notes to remove.</summary>
    public double MaxRemovePercent { get; set; } = 0.25;

    /// <summary>Scale root note for pitch variations.</summary>
    public int ScaleRoot { get; set; } = 60; // C4

    /// <summary>Scale type for pitch variations.</summary>
    public ScaleType ScaleType { get; set; } = ScaleType.Major;

    /// <summary>Preserve note density (add note when removing).</summary>
    public bool PreserveDensity { get; set; } = true;

    /// <summary>Keep notes within this octave range.</summary>
    public int OctaveRange { get; set; } = 2;

    /// <summary>Minimum velocity for generated notes.</summary>
    public int MinVelocity { get; set; } = 40;

    /// <summary>Maximum velocity for generated notes.</summary>
    public int MaxVelocity { get; set; } = 120;
}

/// <summary>
/// Result of a mutation operation.
/// </summary>
public class MutationResult
{
    /// <summary>The mutated pattern.</summary>
    public required Pattern Pattern { get; init; }

    /// <summary>List of mutations applied.</summary>
    public List<MutationType> AppliedMutations { get; } = new();

    /// <summary>Number of notes affected.</summary>
    public int NotesAffected { get; set; }

    /// <summary>Number of notes added.</summary>
    public int NotesAdded { get; set; }

    /// <summary>Number of notes removed.</summary>
    public int NotesRemoved { get; set; }

    /// <summary>Mutation generation number.</summary>
    public int Generation { get; set; }
}

/// <summary>
/// Algorithmic pattern variation generator.
/// Creates musical mutations of patterns using probability-based transformations.
/// </summary>
public class PatternMutator
{
    private readonly Random _random;
    private int _generation;

    /// <summary>Mutation options.</summary>
    public MutationOptions Options { get; set; } = new();

    /// <summary>Current generation number.</summary>
    public int Generation => _generation;

    /// <summary>
    /// Creates a new pattern mutator.
    /// </summary>
    public PatternMutator()
    {
        _random = new Random();
    }

    /// <summary>
    /// Creates a new pattern mutator with a specific seed.
    /// </summary>
    /// <param name="seed">Random seed for reproducibility.</param>
    public PatternMutator(int seed)
    {
        _random = new Random(seed);
    }

    /// <summary>
    /// Creates a new pattern mutator with options.
    /// </summary>
    /// <param name="options">Mutation options.</param>
    public PatternMutator(MutationOptions options)
    {
        _random = new Random();
        Options = options ?? new MutationOptions();
    }

    /// <summary>
    /// Mutates a pattern with current options.
    /// </summary>
    /// <param name="pattern">The pattern to mutate.</param>
    /// <returns>Mutation result with the new pattern.</returns>
    public MutationResult Mutate(Pattern pattern)
    {
        return Mutate(pattern, Options);
    }

    /// <summary>
    /// Mutates a pattern with specified options.
    /// </summary>
    /// <param name="pattern">The pattern to mutate.</param>
    /// <param name="options">Mutation options.</param>
    /// <returns>Mutation result with the new pattern.</returns>
    public MutationResult Mutate(Pattern pattern, MutationOptions options)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        _generation++;

        // Create a copy of the pattern
        var mutated = new Pattern(pattern.Synth)
        {
            Name = $"{pattern.Name} (Gen {_generation})",
            LoopLength = pattern.LoopLength,
            IsLooping = pattern.IsLooping,
            Enabled = pattern.Enabled
        };

        foreach (var e in pattern.Events)
        {
            mutated.Events.Add(new NoteEvent
            {
                Note = e.Note,
                Velocity = e.Velocity,
                Beat = e.Beat,
                Duration = e.Duration
            });
        }

        var result = new MutationResult
        {
            Pattern = mutated,
            Generation = _generation
        };

        // Apply mutations based on probability
        if (_random.NextDouble() < options.MutationProbability)
        {
            // Time shift
            if (options.TimeShiftProbability > 0)
            {
                int affected = ApplyTimeShift(mutated.Events, options);
                if (affected > 0)
                {
                    result.AppliedMutations.Add(MutationType.TimeShift);
                    result.NotesAffected += affected;
                }
            }

            // Velocity randomization
            if (options.VelocityProbability > 0)
            {
                int affected = ApplyVelocityRandomization(mutated.Events, options);
                if (affected > 0)
                {
                    result.AppliedMutations.Add(MutationType.VelocityRandomize);
                    result.NotesAffected += affected;
                }
            }

            // Pitch variation
            if (options.PitchProbability > 0)
            {
                int affected = ApplyPitchVariation(mutated.Events, options);
                if (affected > 0)
                {
                    result.AppliedMutations.Add(MutationType.PitchVariation);
                    result.NotesAffected += affected;
                }
            }

            // Note removal
            if (options.RemoveNoteProbability > 0)
            {
                int removed = ApplyNoteRemoval(mutated.Events, options);
                if (removed > 0)
                {
                    result.AppliedMutations.Add(MutationType.NoteRemoval);
                    result.NotesRemoved = removed;
                }
            }

            // Note addition
            if (options.AddNoteProbability > 0)
            {
                int added = ApplyNoteAddition(mutated, options);
                if (added > 0)
                {
                    result.AppliedMutations.Add(MutationType.NoteAddition);
                    result.NotesAdded = added;
                }
            }
        }

        // Sort by beat
        mutated.Events.Sort((a, b) => a.Beat.CompareTo(b.Beat));

        return result;
    }

    /// <summary>
    /// Applies a specific mutation type to a pattern.
    /// </summary>
    /// <param name="pattern">The pattern to mutate.</param>
    /// <param name="mutationType">The type of mutation to apply.</param>
    /// <returns>The mutated pattern.</returns>
    public Pattern ApplyMutation(Pattern pattern, MutationType mutationType)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));

        var mutated = new Pattern(pattern.Synth)
        {
            Name = $"{pattern.Name} ({mutationType})",
            LoopLength = pattern.LoopLength,
            IsLooping = pattern.IsLooping,
            Enabled = pattern.Enabled
        };

        foreach (var e in pattern.Events)
        {
            mutated.Events.Add(new NoteEvent
            {
                Note = e.Note,
                Velocity = e.Velocity,
                Beat = e.Beat,
                Duration = e.Duration
            });
        }

        switch (mutationType)
        {
            case MutationType.Reverse:
                Reverse(mutated);
                break;

            case MutationType.Invert:
                Invert(mutated, Options.ScaleRoot);
                break;

            case MutationType.RetrogradeInvert:
                Reverse(mutated);
                Invert(mutated, Options.ScaleRoot);
                break;

            case MutationType.Augment:
                Augment(mutated);
                break;

            case MutationType.Diminish:
                Diminish(mutated);
                break;

            case MutationType.Shuffle:
                Shuffle(mutated);
                break;

            case MutationType.TimeShift:
                ApplyTimeShift(mutated.Events, Options);
                break;

            case MutationType.VelocityRandomize:
                ApplyVelocityRandomization(mutated.Events, Options);
                break;

            case MutationType.PitchVariation:
                ApplyPitchVariation(mutated.Events, Options);
                break;

            case MutationType.NoteAddition:
                ApplyNoteAddition(mutated, Options);
                break;

            case MutationType.NoteRemoval:
                ApplyNoteRemoval(mutated.Events, Options);
                break;
        }

        return mutated;
    }

    /// <summary>
    /// Creates multiple variations of a pattern.
    /// </summary>
    /// <param name="pattern">The source pattern.</param>
    /// <param name="count">Number of variations to create.</param>
    /// <returns>List of mutation results.</returns>
    public List<MutationResult> CreateVariations(Pattern pattern, int count)
    {
        var results = new List<MutationResult>();

        for (int i = 0; i < count; i++)
        {
            results.Add(Mutate(pattern));
        }

        return results;
    }

    /// <summary>
    /// Evolves a pattern over multiple generations.
    /// </summary>
    /// <param name="pattern">Starting pattern.</param>
    /// <param name="generations">Number of generations.</param>
    /// <returns>List of patterns for each generation.</returns>
    public List<Pattern> Evolve(Pattern pattern, int generations)
    {
        var evolution = new List<Pattern> { pattern };
        var current = pattern;

        for (int i = 0; i < generations; i++)
        {
            var result = Mutate(current);
            evolution.Add(result.Pattern);
            current = result.Pattern;
        }

        return evolution;
    }

    private int ApplyTimeShift(List<NoteEvent> events, MutationOptions options)
    {
        int affected = 0;

        foreach (var e in events)
        {
            if (_random.NextDouble() < options.TimeShiftProbability)
            {
                double shift = (_random.NextDouble() * 2 - 1) * options.TimeShiftRange;
                e.Beat = Math.Max(0, e.Beat + shift);
                affected++;
            }
        }

        return affected;
    }

    private int ApplyVelocityRandomization(List<NoteEvent> events, MutationOptions options)
    {
        int affected = 0;

        foreach (var e in events)
        {
            if (_random.NextDouble() < options.VelocityProbability)
            {
                int change = _random.Next(-options.VelocityRange, options.VelocityRange + 1);
                e.Velocity = Math.Clamp(e.Velocity + change, options.MinVelocity, options.MaxVelocity);
                affected++;
            }
        }

        return affected;
    }

    private int ApplyPitchVariation(List<NoteEvent> events, MutationOptions options)
    {
        int affected = 0;
        var scaleNotes = Scale.GetNotes(options.ScaleRoot % 12, options.ScaleType, 10);

        foreach (var e in events)
        {
            if (_random.NextDouble() < options.PitchProbability)
            {
                // Find current scale degree
                int currentIndex = -1;
                int minDist = int.MaxValue;

                for (int i = 0; i < scaleNotes.Length; i++)
                {
                    int dist = Math.Abs(scaleNotes[i] - e.Note);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        currentIndex = i;
                    }
                }

                if (currentIndex >= 0)
                {
                    // Shift by scale degrees
                    int degreeShift = _random.Next(-options.PitchShiftDegrees, options.PitchShiftDegrees + 1);
                    int newIndex = Math.Clamp(currentIndex + degreeShift, 0, scaleNotes.Length - 1);

                    // Keep within octave range
                    int basePitch = options.ScaleRoot - (options.OctaveRange * 6);
                    int maxPitch = options.ScaleRoot + (options.OctaveRange * 6);

                    int newNote = scaleNotes[newIndex];
                    if (newNote >= basePitch && newNote <= maxPitch)
                    {
                        e.Note = newNote;
                        affected++;
                    }
                }
            }
        }

        return affected;
    }

    private int ApplyNoteRemoval(List<NoteEvent> events, MutationOptions options)
    {
        int maxRemove = (int)(events.Count * options.MaxRemovePercent);
        int toRemove = 0;

        for (int i = 0; i < events.Count && toRemove < maxRemove; i++)
        {
            if (_random.NextDouble() < options.RemoveNoteProbability)
            {
                toRemove++;
            }
        }

        if (toRemove == 0) return 0;

        // Remove random notes
        var indices = new List<int>();
        while (indices.Count < toRemove && indices.Count < events.Count - 1)
        {
            int idx = _random.Next(events.Count);
            if (!indices.Contains(idx))
            {
                indices.Add(idx);
            }
        }

        foreach (int idx in indices.OrderByDescending(x => x))
        {
            events.RemoveAt(idx);
        }

        return indices.Count;
    }

    private int ApplyNoteAddition(Pattern pattern, MutationOptions options)
    {
        int added = 0;
        var scaleNotes = Scale.GetNotes(options.ScaleRoot % 12, options.ScaleType, 10);

        for (int i = 0; i < options.MaxNotesToAdd; i++)
        {
            if (_random.NextDouble() < options.AddNoteProbability)
            {
                // Generate random position
                double beat = _random.NextDouble() * pattern.LoopLength;

                // Pick random scale note within range
                int basePitch = options.ScaleRoot - (options.OctaveRange * 6);
                int maxPitch = options.ScaleRoot + (options.OctaveRange * 6);

                var validNotes = scaleNotes.Where(n => n >= basePitch && n <= maxPitch).ToArray();
                if (validNotes.Length == 0) continue;

                int note = validNotes[_random.Next(validNotes.Length)];

                // Generate velocity
                int velocity = _random.Next(options.MinVelocity, options.MaxVelocity + 1);

                // Generate duration (quantized to common values)
                double[] durations = { 0.125, 0.25, 0.5, 0.75, 1.0 };
                double duration = durations[_random.Next(durations.Length)];

                pattern.Events.Add(new NoteEvent
                {
                    Note = note,
                    Velocity = velocity,
                    Beat = beat,
                    Duration = duration
                });

                added++;
            }
        }

        return added;
    }

    private void Reverse(Pattern pattern)
    {
        double loopLength = pattern.LoopLength;

        foreach (var e in pattern.Events)
        {
            e.Beat = loopLength - e.Beat - e.Duration;
            if (e.Beat < 0) e.Beat = 0;
        }

        pattern.Events.Sort((a, b) => a.Beat.CompareTo(b.Beat));
    }

    private void Invert(Pattern pattern, int axisNote)
    {
        foreach (var e in pattern.Events)
        {
            int interval = e.Note - axisNote;
            e.Note = Math.Clamp(axisNote - interval, 0, 127);
        }
    }

    private void Augment(Pattern pattern)
    {
        foreach (var e in pattern.Events)
        {
            e.Beat *= 2;
            e.Duration *= 2;
        }
        pattern.LoopLength *= 2;
    }

    private void Diminish(Pattern pattern)
    {
        foreach (var e in pattern.Events)
        {
            e.Beat /= 2;
            e.Duration /= 2;
        }
        pattern.LoopLength /= 2;
    }

    private void Shuffle(Pattern pattern)
    {
        // Get all beats
        var beats = pattern.Events.Select(e => e.Beat).Distinct().OrderBy(b => b).ToList();

        // Shuffle the notes at each beat position
        var byBeat = pattern.Events.GroupBy(e => e.Beat).ToList();

        var shuffledBeats = beats.OrderBy(_ => _random.Next()).ToList();

        int beatIndex = 0;
        foreach (var group in byBeat.OrderBy(g => g.Key))
        {
            foreach (var e in group)
            {
                e.Beat = shuffledBeats[beatIndex];
            }
            beatIndex++;
        }

        pattern.Events.Sort((a, b) => a.Beat.CompareTo(b.Beat));
    }

    /// <summary>
    /// Resets the generation counter.
    /// </summary>
    public void Reset()
    {
        _generation = 0;
    }

    #region Static Factory Methods

    /// <summary>
    /// Creates a subtle mutator (small variations).
    /// </summary>
    public static PatternMutator CreateSubtle()
    {
        return new PatternMutator(new MutationOptions
        {
            MutationProbability = 0.2,
            TimeShiftRange = 0.0625,
            TimeShiftProbability = 0.1,
            VelocityRange = 10,
            VelocityProbability = 0.2,
            PitchShiftDegrees = 1,
            PitchProbability = 0.05,
            AddNoteProbability = 0.02,
            RemoveNoteProbability = 0.02
        });
    }

    /// <summary>
    /// Creates a moderate mutator (balanced variations).
    /// </summary>
    public static PatternMutator CreateModerate()
    {
        return new PatternMutator(new MutationOptions
        {
            MutationProbability = 0.4,
            TimeShiftRange = 0.125,
            TimeShiftProbability = 0.2,
            VelocityRange = 20,
            VelocityProbability = 0.3,
            PitchShiftDegrees = 2,
            PitchProbability = 0.1,
            AddNoteProbability = 0.08,
            RemoveNoteProbability = 0.06
        });
    }

    /// <summary>
    /// Creates an aggressive mutator (dramatic variations).
    /// </summary>
    public static PatternMutator CreateAggressive()
    {
        return new PatternMutator(new MutationOptions
        {
            MutationProbability = 0.7,
            TimeShiftRange = 0.25,
            TimeShiftProbability = 0.4,
            VelocityRange = 40,
            VelocityProbability = 0.5,
            PitchShiftDegrees = 4,
            PitchProbability = 0.25,
            AddNoteProbability = 0.2,
            MaxNotesToAdd = 8,
            RemoveNoteProbability = 0.15,
            MaxRemovePercent = 0.4
        });
    }

    #endregion
}
