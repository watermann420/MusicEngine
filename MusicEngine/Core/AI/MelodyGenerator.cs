//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: AI-based melody generation using Markov chains and rule-based approaches.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core.AI;

/// <summary>
/// Melody generation style presets.
/// </summary>
public enum MelodyStyle
{
    /// <summary>Pop-style melodies with simple, catchy patterns.</summary>
    Pop,
    /// <summary>Classical-style melodies with stepwise motion and proper voice leading.</summary>
    Classical,
    /// <summary>Jazz-style melodies with chromatic passing tones and extended intervals.</summary>
    Jazz,
    /// <summary>Electronic-style melodies with repetitive motifs and arpeggiated patterns.</summary>
    Electronic,
    /// <summary>Ambient-style melodies with sparse, atmospheric patterns.</summary>
    Ambient,
    /// <summary>Rock-style melodies with pentatonic focus.</summary>
    Rock
}

/// <summary>
/// Melodic contour shape for phrase generation.
/// </summary>
public enum ContourShape
{
    /// <summary>Ascending melodic line.</summary>
    Ascending,
    /// <summary>Descending melodic line.</summary>
    Descending,
    /// <summary>Arc shape - rises then falls.</summary>
    Arc,
    /// <summary>Inverted arc - falls then rises.</summary>
    InvertedArc,
    /// <summary>Flat contour with minimal pitch movement.</summary>
    Flat,
    /// <summary>Wave pattern - oscillating motion.</summary>
    Wave,
    /// <summary>Random contour following no specific shape.</summary>
    Random
}

/// <summary>
/// Configuration for melody generation.
/// </summary>
public class MelodyGeneratorConfig
{
    /// <summary>Root note (MIDI note number).</summary>
    public int RootNote { get; set; } = 60; // C4

    /// <summary>Scale type for melody generation.</summary>
    public ScaleType Scale { get; set; } = ScaleType.Major;

    /// <summary>Chord progression (list of root notes and chord types).</summary>
    public List<(int root, ChordType type)> ChordProgression { get; set; } = new();

    /// <summary>Tempo in BPM.</summary>
    public double Tempo { get; set; } = 120;

    /// <summary>Melody style preset.</summary>
    public MelodyStyle Style { get; set; } = MelodyStyle.Pop;

    /// <summary>Desired melodic contour shape.</summary>
    public ContourShape Contour { get; set; } = ContourShape.Arc;

    /// <summary>Lowest allowed MIDI note.</summary>
    public int MinNote { get; set; } = 48; // C3

    /// <summary>Highest allowed MIDI note.</summary>
    public int MaxNote { get; set; } = 84; // C6

    /// <summary>Number of beats for the melody.</summary>
    public double LengthInBeats { get; set; } = 16;

    /// <summary>Note density (0.0 = sparse, 1.0 = dense).</summary>
    public float Density { get; set; } = 0.5f;

    /// <summary>Rhythmic complexity (0.0 = simple, 1.0 = complex).</summary>
    public float RhythmicComplexity { get; set; } = 0.3f;

    /// <summary>Amount of syncopation (0.0 = none, 1.0 = heavy).</summary>
    public float Syncopation { get; set; } = 0.2f;

    /// <summary>Average velocity for notes.</summary>
    public int BaseVelocity { get; set; } = 80;

    /// <summary>Velocity variation range.</summary>
    public int VelocityVariation { get; set; } = 20;

    /// <summary>Random seed for reproducible generation.</summary>
    public int? Seed { get; set; }
}

/// <summary>
/// AI-based melody generator using Markov chains and rule-based approaches.
/// Generates musical melodies based on key, scale, chord progressions, and style parameters.
/// </summary>
public class MelodyGenerator
{
    private readonly Random _random;
    private readonly Dictionary<MelodyStyle, MarkovChain> _styleChains;
    private readonly Dictionary<MelodyStyle, RhythmPattern[]> _rhythmPatterns;

    /// <summary>
    /// Creates a new melody generator.
    /// </summary>
    public MelodyGenerator()
    {
        _random = new Random();
        _styleChains = InitializeStyleChains();
        _rhythmPatterns = InitializeRhythmPatterns();
    }

    /// <summary>
    /// Creates a new melody generator with a specific seed.
    /// </summary>
    /// <param name="seed">Random seed for reproducible generation.</param>
    public MelodyGenerator(int seed)
    {
        _random = new Random(seed);
        _styleChains = InitializeStyleChains();
        _rhythmPatterns = InitializeRhythmPatterns();
    }

    /// <summary>
    /// Generates a melody pattern based on the configuration.
    /// </summary>
    /// <param name="synth">Synthesizer to use for the pattern.</param>
    /// <param name="config">Melody generation configuration.</param>
    /// <returns>A pattern containing the generated melody.</returns>
    public Pattern Generate(ISynth synth, MelodyGeneratorConfig config)
    {
        if (config.Seed.HasValue)
        {
            // Reseed for reproducibility
            var seededRandom = new Random(config.Seed.Value);
            return GenerateInternal(synth, config, seededRandom);
        }

        return GenerateInternal(synth, config, _random);
    }

    private Pattern GenerateInternal(ISynth synth, MelodyGeneratorConfig config, Random random)
    {
        var pattern = new Pattern(synth)
        {
            LoopLength = config.LengthInBeats,
            Name = $"Generated {config.Style} Melody"
        };

        // Get scale notes
        var scaleNotes = Scale.GetNotes(config.RootNote, config.Scale, 3)
            .Where(n => n >= config.MinNote && n <= config.MaxNote)
            .ToArray();

        if (scaleNotes.Length == 0)
        {
            scaleNotes = Scale.GetNotes(config.RootNote, config.Scale, 5)
                .Where(n => n >= 36 && n <= 96)
                .ToArray();
        }

        // Generate rhythm
        var rhythmPattern = GenerateRhythm(config, random);

        // Generate contour
        var contourValues = GenerateContour(config.Contour, rhythmPattern.Count, random);

        // Get Markov chain for style
        var markovChain = _styleChains[config.Style];

        // Generate notes
        int previousInterval = 0;
        int currentNote = GetStartingNote(scaleNotes, config, random);

        for (int i = 0; i < rhythmPattern.Count; i++)
        {
            var (beat, duration) = rhythmPattern[i];

            // Get chord tones if chord progression is provided
            int[] chordTones = GetChordTonesAtBeat(beat, config);

            // Generate next interval using Markov chain
            int interval = markovChain.GetNextInterval(previousInterval, random);

            // Apply contour influence
            interval = ApplyContour(interval, contourValues[i], config.Style);

            // Calculate next note
            int targetNote = currentNote + interval;

            // Quantize to scale
            targetNote = QuantizeToScale(targetNote, scaleNotes);

            // Apply range constraints
            targetNote = Math.Clamp(targetNote, config.MinNote, config.MaxNote);

            // Prefer chord tones on strong beats
            if (chordTones.Length > 0 && IsStrongBeat(beat))
            {
                targetNote = FindNearestChordTone(targetNote, chordTones, scaleNotes);
            }

            // Generate velocity with variation
            int velocity = GenerateVelocity(beat, config, random);

            // Add note to pattern
            pattern.Note(targetNote, beat, duration, velocity);

            previousInterval = interval;
            currentNote = targetNote;
        }

        return pattern;
    }

    /// <summary>
    /// Generates a variation of an existing pattern.
    /// </summary>
    /// <param name="originalPattern">The pattern to vary.</param>
    /// <param name="variationAmount">Amount of variation (0.0 = identical, 1.0 = completely different).</param>
    /// <param name="config">Configuration for variation constraints.</param>
    /// <returns>A varied version of the pattern.</returns>
    public Pattern GenerateVariation(Pattern originalPattern, float variationAmount, MelodyGeneratorConfig config)
    {
        var variation = new Pattern(originalPattern.Synth)
        {
            LoopLength = originalPattern.LoopLength,
            Name = $"{originalPattern.Name} (Variation)"
        };

        var scaleNotes = Scale.GetNotes(config.RootNote, config.Scale, 3)
            .Where(n => n >= config.MinNote && n <= config.MaxNote)
            .ToArray();

        foreach (var ev in originalPattern.Events)
        {
            double newBeat = ev.Beat;
            int newNote = ev.Note;
            double newDuration = ev.Duration;
            int newVelocity = ev.Velocity;

            // Apply rhythmic variation
            if (_random.NextDouble() < variationAmount * 0.3)
            {
                double beatOffset = (_random.NextDouble() - 0.5) * 0.5 * variationAmount;
                newBeat = Math.Max(0, ev.Beat + beatOffset);
            }

            // Apply pitch variation
            if (_random.NextDouble() < variationAmount * 0.5)
            {
                int pitchOffset = (int)((_random.NextDouble() - 0.5) * 12 * variationAmount);
                newNote = QuantizeToScale(ev.Note + pitchOffset, scaleNotes);
                newNote = Math.Clamp(newNote, config.MinNote, config.MaxNote);
            }

            // Apply duration variation
            if (_random.NextDouble() < variationAmount * 0.2)
            {
                double durationFactor = 1.0 + (_random.NextDouble() - 0.5) * variationAmount;
                newDuration = Math.Max(0.125, ev.Duration * durationFactor);
            }

            // Apply velocity variation
            if (_random.NextDouble() < variationAmount * 0.4)
            {
                int velocityOffset = (int)((_random.NextDouble() - 0.5) * 40 * variationAmount);
                newVelocity = Math.Clamp(ev.Velocity + velocityOffset, 1, 127);
            }

            // Occasionally skip notes for variation
            if (_random.NextDouble() >= variationAmount * 0.1)
            {
                variation.Note(newNote, newBeat, newDuration, newVelocity);
            }
        }

        // Occasionally add new notes
        if (_random.NextDouble() < variationAmount * 0.2)
        {
            int additionalNotes = (int)(originalPattern.Events.Count * variationAmount * 0.1);
            for (int i = 0; i < additionalNotes; i++)
            {
                double beat = _random.NextDouble() * variation.LoopLength;
                int note = scaleNotes[_random.Next(scaleNotes.Length)];
                variation.Note(note, beat, 0.25, config.BaseVelocity);
            }
        }

        return variation;
    }

    /// <summary>
    /// Generates a motif (short melodic phrase) that can be developed.
    /// </summary>
    /// <param name="synth">Synthesizer to use.</param>
    /// <param name="config">Configuration for motif generation.</param>
    /// <param name="motifLength">Length of motif in beats.</param>
    /// <returns>A short melodic motif pattern.</returns>
    public Pattern GenerateMotif(ISynth synth, MelodyGeneratorConfig config, double motifLength = 2.0)
    {
        var modifiedConfig = new MelodyGeneratorConfig
        {
            RootNote = config.RootNote,
            Scale = config.Scale,
            Style = config.Style,
            MinNote = config.MinNote,
            MaxNote = config.MaxNote,
            LengthInBeats = motifLength,
            Density = Math.Min(config.Density + 0.2f, 1f),
            RhythmicComplexity = config.RhythmicComplexity,
            BaseVelocity = config.BaseVelocity,
            VelocityVariation = config.VelocityVariation,
            Contour = ContourShape.Arc,
            Seed = config.Seed
        };

        return Generate(synth, modifiedConfig);
    }

    /// <summary>
    /// Develops a motif through repetition, transposition, and variation.
    /// </summary>
    /// <param name="motif">The original motif to develop.</param>
    /// <param name="developmentLength">Total length in beats.</param>
    /// <param name="config">Configuration for development.</param>
    /// <returns>A developed melody based on the motif.</returns>
    public Pattern DevelopMotif(Pattern motif, double developmentLength, MelodyGeneratorConfig config)
    {
        var developed = new Pattern(motif.Synth)
        {
            LoopLength = developmentLength,
            Name = "Developed Motif"
        };

        double currentBeat = 0;
        int transposition = 0;
        var scaleIntervals = GetScaleIntervals(config.Scale);
        int repetition = 0;

        while (currentBeat < developmentLength)
        {
            foreach (var ev in motif.Events)
            {
                double beat = currentBeat + ev.Beat;
                if (beat >= developmentLength) break;

                // Calculate transposed note
                int transposedNote = TransposeInScale(ev.Note, transposition, config.RootNote, scaleIntervals);
                transposedNote = Math.Clamp(transposedNote, config.MinNote, config.MaxNote);

                // Apply slight variation on later repetitions
                int velocity = ev.Velocity;
                if (repetition > 0 && _random.NextDouble() < 0.2)
                {
                    velocity = Math.Clamp(velocity + _random.Next(-10, 11), 1, 127);
                }

                developed.Note(transposedNote, beat, ev.Duration, velocity);
            }

            currentBeat += motif.LoopLength;
            repetition++;

            // Vary transposition based on chord progression or sequence
            if (config.ChordProgression.Count > 0)
            {
                int chordIndex = repetition % config.ChordProgression.Count;
                transposition = config.ChordProgression[chordIndex].root - config.RootNote;
            }
            else
            {
                // Common development patterns: up a fourth, up a fifth, down a third
                transposition = repetition switch
                {
                    1 => 5,  // Up a fourth
                    2 => 7,  // Up a fifth
                    3 => -3, // Down a third
                    _ => 0
                };
            }
        }

        return developed;
    }

    private List<(double beat, double duration)> GenerateRhythm(MelodyGeneratorConfig config, Random random)
    {
        var rhythm = new List<(double, double)>();
        var patterns = _rhythmPatterns[config.Style];

        double currentBeat = 0;
        int notesPerBar = (int)(4 * config.Density * 2) + 2;

        while (currentBeat < config.LengthInBeats)
        {
            // Select rhythm pattern
            var pattern = patterns[random.Next(patterns.Length)];

            // Apply syncopation
            double beatOffset = 0;
            if (random.NextDouble() < config.Syncopation)
            {
                beatOffset = GetSyncopationOffset(random);
            }

            // Add notes from pattern
            foreach (var (offset, duration) in pattern.Notes)
            {
                double beat = currentBeat + offset + beatOffset;
                if (beat >= config.LengthInBeats) break;

                // Apply complexity - occasionally subdivide or combine notes
                if (config.RhythmicComplexity > 0.5f && random.NextDouble() < config.RhythmicComplexity - 0.5)
                {
                    // Subdivide
                    rhythm.Add((beat, duration / 2));
                    if (beat + duration / 2 < config.LengthInBeats)
                    {
                        rhythm.Add((beat + duration / 2, duration / 2));
                    }
                }
                else
                {
                    rhythm.Add((beat, duration));
                }
            }

            currentBeat += pattern.Length;
        }

        return rhythm;
    }

    private float[] GenerateContour(ContourShape shape, int noteCount, Random random)
    {
        var contour = new float[noteCount];

        for (int i = 0; i < noteCount; i++)
        {
            float position = (float)i / Math.Max(1, noteCount - 1);

            contour[i] = shape switch
            {
                ContourShape.Ascending => position,
                ContourShape.Descending => 1f - position,
                ContourShape.Arc => MathF.Sin(position * MathF.PI),
                ContourShape.InvertedArc => 1f - MathF.Sin(position * MathF.PI),
                ContourShape.Flat => 0.5f,
                ContourShape.Wave => 0.5f + 0.5f * MathF.Sin(position * 4 * MathF.PI),
                ContourShape.Random => (float)random.NextDouble(),
                _ => 0.5f
            };
        }

        return contour;
    }

    private int ApplyContour(int interval, float contourValue, MelodyStyle style)
    {
        // Scale contour influence based on style
        float contourInfluence = style switch
        {
            MelodyStyle.Classical => 0.6f,
            MelodyStyle.Jazz => 0.3f,
            MelodyStyle.Pop => 0.5f,
            MelodyStyle.Electronic => 0.4f,
            MelodyStyle.Ambient => 0.7f,
            MelodyStyle.Rock => 0.4f,
            _ => 0.5f
        };

        // Map contour (0-1) to interval adjustment (-4 to +4)
        int contourAdjustment = (int)((contourValue - 0.5f) * 8 * contourInfluence);

        return interval + contourAdjustment;
    }

    private int GetStartingNote(int[] scaleNotes, MelodyGeneratorConfig config, Random random)
    {
        // Prefer starting on root, third, or fifth
        int[] preferredDegrees = { 0, 2, 4 };
        var candidates = scaleNotes.Where((n, i) =>
            preferredDegrees.Contains(i % 7) &&
            n >= config.MinNote &&
            n <= config.MaxNote).ToArray();

        if (candidates.Length > 0)
        {
            return candidates[random.Next(candidates.Length)];
        }

        return scaleNotes[scaleNotes.Length / 2];
    }

    private int QuantizeToScale(int note, int[] scaleNotes)
    {
        int closest = scaleNotes[0];
        int minDistance = Math.Abs(note - closest);

        foreach (var scaleNote in scaleNotes)
        {
            int distance = Math.Abs(note - scaleNote);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = scaleNote;
            }
        }

        return closest;
    }

    private int[] GetChordTonesAtBeat(double beat, MelodyGeneratorConfig config)
    {
        if (config.ChordProgression.Count == 0)
        {
            return Array.Empty<int>();
        }

        // Assume each chord lasts one bar (4 beats)
        int chordIndex = (int)(beat / 4) % config.ChordProgression.Count;
        var (root, chordType) = config.ChordProgression[chordIndex];

        return Chord.GetNotes(root, chordType);
    }

    private bool IsStrongBeat(double beat)
    {
        double beatInBar = beat % 4;
        return beatInBar == 0 || beatInBar == 2;
    }

    private int FindNearestChordTone(int note, int[] chordTones, int[] scaleNotes)
    {
        // Expand chord tones across octaves
        var expandedChordTones = new List<int>();
        foreach (var chordTone in chordTones)
        {
            for (int octave = -2; octave <= 2; octave++)
            {
                int expanded = chordTone + (octave * 12);
                if (expanded >= 0 && expanded <= 127)
                {
                    expandedChordTones.Add(expanded);
                }
            }
        }

        int closest = note;
        int minDistance = int.MaxValue;

        foreach (var chordTone in expandedChordTones)
        {
            int distance = Math.Abs(note - chordTone);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = chordTone;
            }
        }

        // If chord tone is too far, stick with original (quantized to scale)
        if (minDistance > 4)
        {
            return QuantizeToScale(note, scaleNotes);
        }

        return closest;
    }

    private int GenerateVelocity(double beat, MelodyGeneratorConfig config, Random random)
    {
        int velocity = config.BaseVelocity;

        // Add variation
        velocity += random.Next(-config.VelocityVariation, config.VelocityVariation + 1);

        // Accent strong beats
        if (IsStrongBeat(beat))
        {
            velocity += 10;
        }
        else if (beat % 1 == 0.5)
        {
            velocity -= 5;
        }

        return Math.Clamp(velocity, 1, 127);
    }

    private double GetSyncopationOffset(Random random)
    {
        // Common syncopation offsets
        double[] offsets = { 0.5, -0.5, 0.25, -0.25, 0.75 };
        return offsets[random.Next(offsets.Length)];
    }

    private int[] GetScaleIntervals(ScaleType scale)
    {
        return scale switch
        {
            ScaleType.Major => new[] { 0, 2, 4, 5, 7, 9, 11 },
            ScaleType.NaturalMinor => new[] { 0, 2, 3, 5, 7, 8, 10 },
            ScaleType.HarmonicMinor => new[] { 0, 2, 3, 5, 7, 8, 11 },
            ScaleType.PentatonicMajor => new[] { 0, 2, 4, 7, 9 },
            ScaleType.PentatonicMinor => new[] { 0, 3, 5, 7, 10 },
            ScaleType.Blues => new[] { 0, 3, 5, 6, 7, 10 },
            ScaleType.Dorian => new[] { 0, 2, 3, 5, 7, 9, 10 },
            ScaleType.Mixolydian => new[] { 0, 2, 4, 5, 7, 9, 10 },
            _ => new[] { 0, 2, 4, 5, 7, 9, 11 }
        };
    }

    private int TransposeInScale(int note, int semitones, int root, int[] scaleIntervals)
    {
        // Simple transposition - can be enhanced for diatonic transposition
        return note + semitones;
    }

    private Dictionary<MelodyStyle, MarkovChain> InitializeStyleChains()
    {
        return new Dictionary<MelodyStyle, MarkovChain>
        {
            { MelodyStyle.Pop, CreatePopChain() },
            { MelodyStyle.Classical, CreateClassicalChain() },
            { MelodyStyle.Jazz, CreateJazzChain() },
            { MelodyStyle.Electronic, CreateElectronicChain() },
            { MelodyStyle.Ambient, CreateAmbientChain() },
            { MelodyStyle.Rock, CreateRockChain() }
        };
    }

    private MarkovChain CreatePopChain()
    {
        // Pop melodies favor stepwise motion and small leaps
        var chain = new MarkovChain();

        // From unison (0)
        chain.AddTransition(0, 0, 0.1f);
        chain.AddTransition(0, 1, 0.25f);
        chain.AddTransition(0, 2, 0.25f);
        chain.AddTransition(0, -1, 0.15f);
        chain.AddTransition(0, -2, 0.15f);
        chain.AddTransition(0, 3, 0.05f);
        chain.AddTransition(0, -3, 0.05f);

        // From step up (1-2)
        chain.AddTransition(1, 0, 0.15f);
        chain.AddTransition(1, 1, 0.2f);
        chain.AddTransition(1, 2, 0.15f);
        chain.AddTransition(1, -1, 0.25f);
        chain.AddTransition(1, -2, 0.2f);
        chain.AddTransition(1, -3, 0.05f);

        chain.AddTransition(2, 0, 0.15f);
        chain.AddTransition(2, 1, 0.15f);
        chain.AddTransition(2, -1, 0.25f);
        chain.AddTransition(2, -2, 0.25f);
        chain.AddTransition(2, -3, 0.15f);
        chain.AddTransition(2, 2, 0.05f);

        // From step down (-1, -2)
        chain.AddTransition(-1, 0, 0.15f);
        chain.AddTransition(-1, 1, 0.25f);
        chain.AddTransition(-1, 2, 0.2f);
        chain.AddTransition(-1, -1, 0.2f);
        chain.AddTransition(-1, -2, 0.15f);
        chain.AddTransition(-1, 3, 0.05f);

        chain.AddTransition(-2, 0, 0.15f);
        chain.AddTransition(-2, 1, 0.25f);
        chain.AddTransition(-2, 2, 0.25f);
        chain.AddTransition(-2, -1, 0.15f);
        chain.AddTransition(-2, -2, 0.1f);
        chain.AddTransition(-2, 3, 0.1f);

        // From leap (3+)
        chain.AddTransition(3, -1, 0.3f);
        chain.AddTransition(3, -2, 0.3f);
        chain.AddTransition(3, 0, 0.2f);
        chain.AddTransition(3, 1, 0.1f);
        chain.AddTransition(3, -3, 0.1f);

        chain.AddTransition(-3, 1, 0.3f);
        chain.AddTransition(-3, 2, 0.3f);
        chain.AddTransition(-3, 0, 0.2f);
        chain.AddTransition(-3, -1, 0.1f);
        chain.AddTransition(-3, 3, 0.1f);

        return chain;
    }

    private MarkovChain CreateClassicalChain()
    {
        // Classical melodies favor stepwise motion with prepared leaps
        var chain = new MarkovChain();

        chain.AddTransition(0, 1, 0.3f);
        chain.AddTransition(0, 2, 0.2f);
        chain.AddTransition(0, -1, 0.3f);
        chain.AddTransition(0, -2, 0.15f);
        chain.AddTransition(0, 0, 0.05f);

        chain.AddTransition(1, -1, 0.3f);
        chain.AddTransition(1, 1, 0.25f);
        chain.AddTransition(1, -2, 0.2f);
        chain.AddTransition(1, 0, 0.15f);
        chain.AddTransition(1, 2, 0.1f);

        chain.AddTransition(2, -1, 0.35f);
        chain.AddTransition(2, -2, 0.25f);
        chain.AddTransition(2, 1, 0.2f);
        chain.AddTransition(2, 0, 0.15f);
        chain.AddTransition(2, -3, 0.05f);

        chain.AddTransition(-1, 1, 0.3f);
        chain.AddTransition(-1, -1, 0.25f);
        chain.AddTransition(-1, 2, 0.2f);
        chain.AddTransition(-1, 0, 0.15f);
        chain.AddTransition(-1, -2, 0.1f);

        chain.AddTransition(-2, 1, 0.35f);
        chain.AddTransition(-2, 2, 0.25f);
        chain.AddTransition(-2, -1, 0.2f);
        chain.AddTransition(-2, 0, 0.15f);
        chain.AddTransition(-2, 3, 0.05f);

        chain.AddTransition(3, -1, 0.4f);
        chain.AddTransition(3, -2, 0.35f);
        chain.AddTransition(3, 0, 0.15f);
        chain.AddTransition(3, 1, 0.1f);

        chain.AddTransition(-3, 1, 0.4f);
        chain.AddTransition(-3, 2, 0.35f);
        chain.AddTransition(-3, 0, 0.15f);
        chain.AddTransition(-3, -1, 0.1f);

        return chain;
    }

    private MarkovChain CreateJazzChain()
    {
        // Jazz melodies use more leaps and chromatic motion
        var chain = new MarkovChain();

        chain.AddTransition(0, 1, 0.15f);
        chain.AddTransition(0, 2, 0.15f);
        chain.AddTransition(0, 3, 0.15f);
        chain.AddTransition(0, 4, 0.1f);
        chain.AddTransition(0, -1, 0.15f);
        chain.AddTransition(0, -2, 0.15f);
        chain.AddTransition(0, -3, 0.1f);
        chain.AddTransition(0, 5, 0.05f);

        chain.AddTransition(1, -2, 0.2f);
        chain.AddTransition(1, 1, 0.15f);
        chain.AddTransition(1, -1, 0.2f);
        chain.AddTransition(1, 2, 0.15f);
        chain.AddTransition(1, 3, 0.15f);
        chain.AddTransition(1, -3, 0.1f);
        chain.AddTransition(1, 0, 0.05f);

        chain.AddTransition(2, -1, 0.2f);
        chain.AddTransition(2, -2, 0.2f);
        chain.AddTransition(2, 1, 0.15f);
        chain.AddTransition(2, -3, 0.15f);
        chain.AddTransition(2, 3, 0.1f);
        chain.AddTransition(2, -4, 0.1f);
        chain.AddTransition(2, 0, 0.1f);

        chain.AddTransition(-1, 2, 0.2f);
        chain.AddTransition(-1, 1, 0.2f);
        chain.AddTransition(-1, -1, 0.15f);
        chain.AddTransition(-1, -2, 0.15f);
        chain.AddTransition(-1, 3, 0.15f);
        chain.AddTransition(-1, -3, 0.1f);
        chain.AddTransition(-1, 0, 0.05f);

        chain.AddTransition(-2, 1, 0.2f);
        chain.AddTransition(-2, 2, 0.2f);
        chain.AddTransition(-2, 3, 0.15f);
        chain.AddTransition(-2, -1, 0.15f);
        chain.AddTransition(-2, 4, 0.1f);
        chain.AddTransition(-2, -2, 0.1f);
        chain.AddTransition(-2, 0, 0.1f);

        chain.AddTransition(3, -2, 0.25f);
        chain.AddTransition(3, -3, 0.2f);
        chain.AddTransition(3, 1, 0.15f);
        chain.AddTransition(3, -1, 0.2f);
        chain.AddTransition(3, -4, 0.1f);
        chain.AddTransition(3, 2, 0.1f);

        chain.AddTransition(-3, 2, 0.25f);
        chain.AddTransition(-3, 3, 0.2f);
        chain.AddTransition(-3, 1, 0.2f);
        chain.AddTransition(-3, -1, 0.15f);
        chain.AddTransition(-3, 4, 0.1f);
        chain.AddTransition(-3, -2, 0.1f);

        return chain;
    }

    private MarkovChain CreateElectronicChain()
    {
        // Electronic melodies favor arpeggiated patterns and octave jumps
        var chain = new MarkovChain();

        chain.AddTransition(0, 3, 0.2f);
        chain.AddTransition(0, 4, 0.15f);
        chain.AddTransition(0, 7, 0.15f);
        chain.AddTransition(0, -3, 0.15f);
        chain.AddTransition(0, -4, 0.1f);
        chain.AddTransition(0, 12, 0.1f);
        chain.AddTransition(0, -12, 0.1f);
        chain.AddTransition(0, 0, 0.05f);

        chain.AddTransition(3, -3, 0.25f);
        chain.AddTransition(3, 4, 0.2f);
        chain.AddTransition(3, -4, 0.15f);
        chain.AddTransition(3, 0, 0.15f);
        chain.AddTransition(3, 7, 0.15f);
        chain.AddTransition(3, -7, 0.1f);

        chain.AddTransition(4, -4, 0.25f);
        chain.AddTransition(4, 3, 0.2f);
        chain.AddTransition(4, -3, 0.15f);
        chain.AddTransition(4, 0, 0.15f);
        chain.AddTransition(4, -7, 0.15f);
        chain.AddTransition(4, 7, 0.1f);

        chain.AddTransition(-3, 3, 0.25f);
        chain.AddTransition(-3, 4, 0.2f);
        chain.AddTransition(-3, -4, 0.15f);
        chain.AddTransition(-3, 0, 0.15f);
        chain.AddTransition(-3, 7, 0.15f);
        chain.AddTransition(-3, -7, 0.1f);

        chain.AddTransition(-4, 4, 0.25f);
        chain.AddTransition(-4, 3, 0.2f);
        chain.AddTransition(-4, -3, 0.15f);
        chain.AddTransition(-4, 0, 0.15f);
        chain.AddTransition(-4, 7, 0.15f);
        chain.AddTransition(-4, -7, 0.1f);

        chain.AddTransition(7, -7, 0.3f);
        chain.AddTransition(7, -3, 0.2f);
        chain.AddTransition(7, -4, 0.2f);
        chain.AddTransition(7, 0, 0.15f);
        chain.AddTransition(7, 5, 0.15f);

        chain.AddTransition(-7, 7, 0.3f);
        chain.AddTransition(-7, 3, 0.2f);
        chain.AddTransition(-7, 4, 0.2f);
        chain.AddTransition(-7, 0, 0.15f);
        chain.AddTransition(-7, -5, 0.15f);

        return chain;
    }

    private MarkovChain CreateAmbientChain()
    {
        // Ambient melodies favor slow, sustained notes with wide intervals
        var chain = new MarkovChain();

        chain.AddTransition(0, 0, 0.2f);
        chain.AddTransition(0, 5, 0.15f);
        chain.AddTransition(0, 7, 0.15f);
        chain.AddTransition(0, -5, 0.15f);
        chain.AddTransition(0, -7, 0.1f);
        chain.AddTransition(0, 12, 0.1f);
        chain.AddTransition(0, 2, 0.1f);
        chain.AddTransition(0, -2, 0.05f);

        chain.AddTransition(5, -5, 0.25f);
        chain.AddTransition(5, 2, 0.2f);
        chain.AddTransition(5, -2, 0.15f);
        chain.AddTransition(5, 0, 0.2f);
        chain.AddTransition(5, 7, 0.1f);
        chain.AddTransition(5, -7, 0.1f);

        chain.AddTransition(7, -7, 0.25f);
        chain.AddTransition(7, -5, 0.2f);
        chain.AddTransition(7, -2, 0.15f);
        chain.AddTransition(7, 0, 0.2f);
        chain.AddTransition(7, 5, 0.1f);
        chain.AddTransition(7, 2, 0.1f);

        chain.AddTransition(-5, 5, 0.25f);
        chain.AddTransition(-5, 2, 0.2f);
        chain.AddTransition(-5, -2, 0.15f);
        chain.AddTransition(-5, 0, 0.2f);
        chain.AddTransition(-5, 7, 0.1f);
        chain.AddTransition(-5, -7, 0.1f);

        chain.AddTransition(-7, 7, 0.25f);
        chain.AddTransition(-7, 5, 0.2f);
        chain.AddTransition(-7, 2, 0.15f);
        chain.AddTransition(-7, 0, 0.2f);
        chain.AddTransition(-7, -5, 0.1f);
        chain.AddTransition(-7, -2, 0.1f);

        chain.AddTransition(12, -12, 0.4f);
        chain.AddTransition(12, -7, 0.25f);
        chain.AddTransition(12, -5, 0.2f);
        chain.AddTransition(12, 0, 0.15f);

        return chain;
    }

    private MarkovChain CreateRockChain()
    {
        // Rock melodies favor pentatonic patterns
        var chain = new MarkovChain();

        chain.AddTransition(0, 2, 0.2f);
        chain.AddTransition(0, 3, 0.2f);
        chain.AddTransition(0, -2, 0.15f);
        chain.AddTransition(0, -3, 0.15f);
        chain.AddTransition(0, 5, 0.1f);
        chain.AddTransition(0, -5, 0.1f);
        chain.AddTransition(0, 0, 0.1f);

        chain.AddTransition(2, -2, 0.25f);
        chain.AddTransition(2, 1, 0.2f);
        chain.AddTransition(2, -1, 0.15f);
        chain.AddTransition(2, 3, 0.15f);
        chain.AddTransition(2, -3, 0.15f);
        chain.AddTransition(2, 0, 0.1f);

        chain.AddTransition(3, -3, 0.25f);
        chain.AddTransition(3, -2, 0.2f);
        chain.AddTransition(3, 2, 0.15f);
        chain.AddTransition(3, -1, 0.15f);
        chain.AddTransition(3, 0, 0.15f);
        chain.AddTransition(3, 4, 0.1f);

        chain.AddTransition(-2, 2, 0.25f);
        chain.AddTransition(-2, 3, 0.2f);
        chain.AddTransition(-2, -1, 0.15f);
        chain.AddTransition(-2, 1, 0.15f);
        chain.AddTransition(-2, -3, 0.15f);
        chain.AddTransition(-2, 0, 0.1f);

        chain.AddTransition(-3, 3, 0.25f);
        chain.AddTransition(-3, 2, 0.2f);
        chain.AddTransition(-3, 1, 0.15f);
        chain.AddTransition(-3, -2, 0.15f);
        chain.AddTransition(-3, 0, 0.15f);
        chain.AddTransition(-3, -4, 0.1f);

        chain.AddTransition(5, -5, 0.3f);
        chain.AddTransition(5, -3, 0.25f);
        chain.AddTransition(5, -2, 0.2f);
        chain.AddTransition(5, 0, 0.15f);
        chain.AddTransition(5, 2, 0.1f);

        chain.AddTransition(-5, 5, 0.3f);
        chain.AddTransition(-5, 3, 0.25f);
        chain.AddTransition(-5, 2, 0.2f);
        chain.AddTransition(-5, 0, 0.15f);
        chain.AddTransition(-5, -2, 0.1f);

        return chain;
    }

    private Dictionary<MelodyStyle, RhythmPattern[]> InitializeRhythmPatterns()
    {
        return new Dictionary<MelodyStyle, RhythmPattern[]>
        {
            { MelodyStyle.Pop, GetPopRhythms() },
            { MelodyStyle.Classical, GetClassicalRhythms() },
            { MelodyStyle.Jazz, GetJazzRhythms() },
            { MelodyStyle.Electronic, GetElectronicRhythms() },
            { MelodyStyle.Ambient, GetAmbientRhythms() },
            { MelodyStyle.Rock, GetRockRhythms() }
        };
    }

    private RhythmPattern[] GetPopRhythms()
    {
        return new[]
        {
            new RhythmPattern(2.0, new[] { (0.0, 0.5), (0.5, 0.5), (1.0, 0.5), (1.5, 0.5) }),
            new RhythmPattern(2.0, new[] { (0.0, 1.0), (1.0, 0.5), (1.5, 0.5) }),
            new RhythmPattern(2.0, new[] { (0.0, 0.75), (0.75, 0.25), (1.0, 1.0) }),
            new RhythmPattern(1.0, new[] { (0.0, 0.5), (0.5, 0.5) }),
            new RhythmPattern(1.0, new[] { (0.0, 0.25), (0.25, 0.25), (0.5, 0.5) })
        };
    }

    private RhythmPattern[] GetClassicalRhythms()
    {
        return new[]
        {
            new RhythmPattern(2.0, new[] { (0.0, 1.0), (1.0, 1.0) }),
            new RhythmPattern(2.0, new[] { (0.0, 0.5), (0.5, 0.5), (1.0, 1.0) }),
            new RhythmPattern(1.0, new[] { (0.0, 0.5), (0.5, 0.25), (0.75, 0.25) }),
            new RhythmPattern(2.0, new[] { (0.0, 1.5), (1.5, 0.5) }),
            new RhythmPattern(3.0, new[] { (0.0, 1.0), (1.0, 1.0), (2.0, 1.0) })
        };
    }

    private RhythmPattern[] GetJazzRhythms()
    {
        return new[]
        {
            new RhythmPattern(2.0, new[] { (0.0, 0.67), (0.67, 0.33), (1.0, 0.67), (1.67, 0.33) }),
            new RhythmPattern(2.0, new[] { (0.0, 0.5), (0.5, 0.25), (0.75, 0.25), (1.0, 1.0) }),
            new RhythmPattern(1.0, new[] { (0.0, 0.33), (0.33, 0.33), (0.67, 0.33) }),
            new RhythmPattern(2.0, new[] { (0.5, 0.5), (1.0, 0.5), (1.5, 0.5) }),
            new RhythmPattern(2.0, new[] { (0.0, 1.5), (1.5, 0.25), (1.75, 0.25) })
        };
    }

    private RhythmPattern[] GetElectronicRhythms()
    {
        return new[]
        {
            new RhythmPattern(1.0, new[] { (0.0, 0.25), (0.25, 0.25), (0.5, 0.25), (0.75, 0.25) }),
            new RhythmPattern(2.0, new[] { (0.0, 0.25), (0.5, 0.25), (1.0, 0.25), (1.5, 0.25) }),
            new RhythmPattern(1.0, new[] { (0.0, 0.125), (0.125, 0.125), (0.25, 0.25), (0.5, 0.5) }),
            new RhythmPattern(2.0, new[] { (0.0, 0.5), (0.5, 0.5), (1.25, 0.25), (1.5, 0.5) }),
            new RhythmPattern(0.5, new[] { (0.0, 0.125), (0.125, 0.125), (0.25, 0.125), (0.375, 0.125) })
        };
    }

    private RhythmPattern[] GetAmbientRhythms()
    {
        return new[]
        {
            new RhythmPattern(4.0, new[] { (0.0, 4.0) }),
            new RhythmPattern(4.0, new[] { (0.0, 2.0), (2.0, 2.0) }),
            new RhythmPattern(8.0, new[] { (0.0, 4.0), (4.0, 4.0) }),
            new RhythmPattern(4.0, new[] { (0.0, 3.0), (3.0, 1.0) }),
            new RhythmPattern(6.0, new[] { (0.0, 2.0), (2.0, 2.0), (4.0, 2.0) })
        };
    }

    private RhythmPattern[] GetRockRhythms()
    {
        return new[]
        {
            new RhythmPattern(2.0, new[] { (0.0, 0.5), (0.5, 0.5), (1.0, 1.0) }),
            new RhythmPattern(2.0, new[] { (0.0, 1.0), (1.0, 0.5), (1.5, 0.5) }),
            new RhythmPattern(1.0, new[] { (0.0, 0.25), (0.25, 0.25), (0.5, 0.5) }),
            new RhythmPattern(2.0, new[] { (0.0, 0.5), (1.0, 0.5), (1.5, 0.5) }),
            new RhythmPattern(4.0, new[] { (0.0, 1.0), (1.0, 1.0), (2.0, 1.0), (3.0, 1.0) })
        };
    }

    /// <summary>
    /// Simple Markov chain for interval transitions.
    /// </summary>
    private class MarkovChain
    {
        private readonly Dictionary<int, List<(int nextInterval, float probability)>> _transitions = new();

        public void AddTransition(int fromInterval, int toInterval, float probability)
        {
            // Normalize intervals to a reasonable range
            fromInterval = NormalizeInterval(fromInterval);

            if (!_transitions.ContainsKey(fromInterval))
            {
                _transitions[fromInterval] = new List<(int, float)>();
            }

            _transitions[fromInterval].Add((toInterval, probability));
        }

        public int GetNextInterval(int currentInterval, Random random)
        {
            currentInterval = NormalizeInterval(currentInterval);

            if (!_transitions.TryGetValue(currentInterval, out var transitions))
            {
                // Fall back to 0 if no transitions defined
                if (!_transitions.TryGetValue(0, out transitions))
                {
                    return 0;
                }
            }

            // Weighted random selection
            float totalProb = transitions.Sum(t => t.probability);
            float roll = (float)random.NextDouble() * totalProb;
            float cumulative = 0;

            foreach (var (nextInterval, prob) in transitions)
            {
                cumulative += prob;
                if (roll <= cumulative)
                {
                    return nextInterval;
                }
            }

            return transitions.Last().nextInterval;
        }

        private int NormalizeInterval(int interval)
        {
            // Map large intervals to smaller equivalents for transition lookup
            if (interval > 7) return 7;
            if (interval < -7) return -7;
            return interval;
        }
    }

    /// <summary>
    /// Rhythm pattern definition.
    /// </summary>
    private class RhythmPattern
    {
        public double Length { get; }
        public (double offset, double duration)[] Notes { get; }

        public RhythmPattern(double length, (double, double)[] notes)
        {
            Length = length;
            Notes = notes;
        }
    }
}
