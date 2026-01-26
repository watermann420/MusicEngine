// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Collections.Generic;
using System.Linq;


namespace MusicEngine.Core;


/// <summary>
/// Represents a named Euclidean rhythm preset with common world music patterns.
/// </summary>
public readonly struct EuclideanPreset
{
    /// <summary>Number of steps in the pattern.</summary>
    public int Steps { get; init; }

    /// <summary>Number of pulses (hits) in the pattern.</summary>
    public int Pulses { get; init; }

    /// <summary>Rotation offset for the pattern.</summary>
    public int Rotation { get; init; }

    /// <summary>Name of the rhythm pattern.</summary>
    public string Name { get; init; }

    /// <summary>Cultural or musical origin of the pattern.</summary>
    public string Origin { get; init; }

    public EuclideanPreset(int steps, int pulses, int rotation, string name, string origin)
    {
        Steps = steps;
        Pulses = pulses;
        Rotation = rotation;
        Name = name;
        Origin = origin;
    }
}


/// <summary>
/// Represents a layer in a multi-layer Euclidean rhythm composition.
/// </summary>
public class EuclideanLayer
{
    /// <summary>Number of steps in this layer's pattern.</summary>
    public int Steps { get; set; }

    /// <summary>Number of pulses in this layer's pattern.</summary>
    public int Pulses { get; set; }

    /// <summary>Rotation offset for this layer.</summary>
    public int Rotation { get; set; }

    /// <summary>MIDI note number for this layer (e.g., kick=36, snare=38, hihat=42).</summary>
    public int Note { get; set; }

    /// <summary>Velocity for notes in this layer (0-127).</summary>
    public int Velocity { get; set; } = 100;

    /// <summary>Duration of each step in beats.</summary>
    public double StepLength { get; set; } = 0.25;

    /// <summary>Name of this layer for identification.</summary>
    public string Name { get; set; } = "";

    public EuclideanLayer() { }

    public EuclideanLayer(int steps, int pulses, int note, int velocity = 100, int rotation = 0, double stepLength = 0.25, string name = "")
    {
        Steps = steps;
        Pulses = pulses;
        Rotation = rotation;
        Note = note;
        Velocity = velocity;
        StepLength = stepLength;
        Name = name;
    }
}


/// <summary>
/// Generates Euclidean rhythms using Bjorklund's algorithm.
/// Euclidean rhythms distribute N pulses as evenly as possible over M steps.
/// Many traditional world music rhythms are Euclidean patterns.
/// </summary>
/// <example>
/// <code>
/// // Generate a classic Tresillo pattern (3 pulses over 8 steps)
/// bool[] tresillo = EuclideanRhythm.GeneratePattern(8, 3);
/// // Result: [true, false, false, true, false, false, true, false]
/// // Visual: [x . . x . . x .]
///
/// // Create a pattern with notes
/// var kickPattern = EuclideanRhythm.GenerateNotePattern(8, 3, 0, 36, 0.5, 100, synth);
///
/// // Use presets
/// var preset = EuclideanRhythm.Presets.Tresillo;
/// bool[] pattern = EuclideanRhythm.GeneratePattern(preset);
///
/// // Combine multiple layers (kick, snare, hihat)
/// var drumPattern = EuclideanRhythm.CombineLayers(synth,
///     new EuclideanLayer(8, 3, 36, 100, 0, 0.5, "Kick"),
///     new EuclideanLayer(8, 2, 38, 90, 4, 0.5, "Snare"),
///     new EuclideanLayer(8, 5, 42, 70, 0, 0.5, "HiHat")
/// );
/// </code>
/// </example>
public static class EuclideanRhythm
{
    #region Core Generation Methods

    /// <summary>
    /// Generates a Euclidean rhythm pattern using Bjorklund's algorithm.
    /// </summary>
    /// <param name="steps">Total number of steps in the pattern.</param>
    /// <param name="pulses">Number of pulses (hits) to distribute.</param>
    /// <param name="rotation">Optional rotation offset (shifts pattern left by N steps).</param>
    /// <returns>Boolean array where true indicates a pulse/hit.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when steps or pulses are invalid.</exception>
    /// <example>
    /// <code>
    /// // Classic Tresillo: 3 pulses over 8 steps
    /// bool[] tresillo = EuclideanRhythm.GeneratePattern(8, 3);
    /// // Result: [x . . x . . x .]
    ///
    /// // Cinquillo: 5 pulses over 8 steps
    /// bool[] cinquillo = EuclideanRhythm.GeneratePattern(8, 5);
    /// // Result: [x . x x . x x .]
    /// </code>
    /// </example>
    public static bool[] GeneratePattern(int steps, int pulses, int rotation = 0)
    {
        if (steps <= 0)
            throw new ArgumentOutOfRangeException(nameof(steps), "Steps must be greater than 0.");
        if (pulses < 0)
            throw new ArgumentOutOfRangeException(nameof(pulses), "Pulses cannot be negative.");
        if (pulses > steps)
            throw new ArgumentOutOfRangeException(nameof(pulses), "Pulses cannot exceed steps.");

        // Edge cases
        if (pulses == 0)
            return new bool[steps];
        if (pulses == steps)
            return Enumerable.Repeat(true, steps).ToArray();

        // Bjorklund's algorithm
        var pattern = BjorklundAlgorithm(steps, pulses);

        // Apply rotation if specified
        if (rotation != 0)
        {
            pattern = RotatePattern(pattern, rotation);
        }

        return pattern;
    }

    /// <summary>
    /// Generates a Euclidean rhythm pattern from a preset.
    /// </summary>
    /// <param name="preset">The Euclidean preset to use.</param>
    /// <returns>Boolean array where true indicates a pulse/hit.</returns>
    public static bool[] GeneratePattern(EuclideanPreset preset)
    {
        return GeneratePattern(preset.Steps, preset.Pulses, preset.Rotation);
    }

    /// <summary>
    /// Implements Bjorklund's algorithm for distributing pulses evenly across steps.
    /// This is based on Euclid's algorithm for computing the greatest common divisor.
    /// </summary>
    private static bool[] BjorklundAlgorithm(int steps, int pulses)
    {
        // Initialize groups: pulses as [true], rests as [false]
        var groups = new List<List<bool>>();

        for (int i = 0; i < pulses; i++)
            groups.Add(new List<bool> { true });

        int remainder = steps - pulses;
        for (int i = 0; i < remainder; i++)
            groups.Add(new List<bool> { false });

        // Iteratively distribute remainders
        while (remainder > 1)
        {
            int numToDistribute = Math.Min(pulses, remainder);

            for (int i = 0; i < numToDistribute; i++)
            {
                // Take from the end and append to the front groups
                var last = groups[groups.Count - 1];
                groups.RemoveAt(groups.Count - 1);
                groups[i].AddRange(last);
            }

            // Update counts for next iteration
            int newPulses = numToDistribute;
            remainder = pulses - numToDistribute;
            if (remainder < 0) remainder = groups.Count - newPulses;
            pulses = newPulses;

            // Recalculate remainder based on current group state
            int frontGroupCount = 0;
            int frontGroupLength = groups[0].Count;
            foreach (var g in groups)
            {
                if (g.Count == frontGroupLength)
                    frontGroupCount++;
                else
                    break;
            }
            remainder = groups.Count - frontGroupCount;
            pulses = frontGroupCount;
        }

        // Flatten groups into final pattern
        var result = new List<bool>();
        foreach (var group in groups)
        {
            result.AddRange(group);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Rotates a pattern by the specified number of steps (left rotation).
    /// </summary>
    /// <param name="pattern">The pattern to rotate.</param>
    /// <param name="rotation">Number of steps to rotate (positive = left, negative = right).</param>
    /// <returns>Rotated pattern.</returns>
    public static bool[] RotatePattern(bool[] pattern, int rotation)
    {
        if (pattern.Length == 0) return pattern;

        // Normalize rotation to positive value within bounds
        rotation = ((rotation % pattern.Length) + pattern.Length) % pattern.Length;

        if (rotation == 0) return (bool[])pattern.Clone();

        var result = new bool[pattern.Length];
        for (int i = 0; i < pattern.Length; i++)
        {
            result[i] = pattern[(i + rotation) % pattern.Length];
        }

        return result;
    }

    #endregion

    #region Pattern to Note Conversion

    /// <summary>
    /// Generates a Pattern with NoteEvents from a Euclidean rhythm.
    /// </summary>
    /// <param name="steps">Total number of steps in the pattern.</param>
    /// <param name="pulses">Number of pulses to distribute.</param>
    /// <param name="rotation">Rotation offset.</param>
    /// <param name="note">MIDI note number for the pulses.</param>
    /// <param name="stepLength">Duration of each step in beats.</param>
    /// <param name="velocity">Velocity for the notes (0-127).</param>
    /// <param name="synth">Synthesizer to use for the pattern.</param>
    /// <returns>A Pattern containing the Euclidean rhythm as NoteEvents.</returns>
    /// <example>
    /// <code>
    /// // Create a kick drum pattern with Tresillo rhythm
    /// var kickPattern = EuclideanRhythm.GenerateNotePattern(
    ///     steps: 8,
    ///     pulses: 3,
    ///     rotation: 0,
    ///     note: 36,        // C1 (kick)
    ///     stepLength: 0.5, // 8th notes
    ///     velocity: 100,
    ///     synth: drumSynth
    /// );
    /// </code>
    /// </example>
    public static Pattern GenerateNotePattern(int steps, int pulses, int rotation, int note, double stepLength, int velocity, ISynth synth)
    {
        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateVelocity(velocity);

        var boolPattern = GeneratePattern(steps, pulses, rotation);
        var pattern = new Pattern(synth)
        {
            LoopLength = steps * stepLength,
            IsLooping = true
        };

        for (int i = 0; i < boolPattern.Length; i++)
        {
            if (boolPattern[i])
            {
                pattern.Events.Add(new NoteEvent
                {
                    Beat = i * stepLength,
                    Note = note,
                    Velocity = velocity,
                    Duration = stepLength * 0.9 // Slight gap between notes
                });
            }
        }

        return pattern;
    }

    /// <summary>
    /// Generates a Pattern from a Euclidean preset.
    /// </summary>
    /// <param name="preset">The preset to use.</param>
    /// <param name="note">MIDI note number.</param>
    /// <param name="stepLength">Duration of each step in beats.</param>
    /// <param name="velocity">Velocity for the notes.</param>
    /// <param name="synth">Synthesizer to use.</param>
    /// <returns>A Pattern containing the Euclidean rhythm.</returns>
    public static Pattern GenerateNotePattern(EuclideanPreset preset, int note, double stepLength, int velocity, ISynth synth)
    {
        return GenerateNotePattern(preset.Steps, preset.Pulses, preset.Rotation, note, stepLength, velocity, synth);
    }

    #endregion

    #region Multi-Layer Support

    /// <summary>
    /// Combines multiple Euclidean layers into a single pattern.
    /// Useful for creating drum patterns with kick, snare, hihat, etc.
    /// </summary>
    /// <param name="synth">Synthesizer to use for all layers.</param>
    /// <param name="layers">Array of Euclidean layers to combine.</param>
    /// <returns>A single Pattern containing all layers' notes.</returns>
    /// <example>
    /// <code>
    /// // Create a polyrhythmic drum pattern
    /// var drumPattern = EuclideanRhythm.CombineLayers(drumSynth,
    ///     new EuclideanLayer(16, 4, 36, 110, 0, 0.25, "Kick"),    // 4 kicks over 16 steps
    ///     new EuclideanLayer(16, 3, 38, 100, 4, 0.25, "Snare"),   // 3 snares, rotated
    ///     new EuclideanLayer(16, 7, 42, 80, 0, 0.25, "HiHat"),    // 7 hihats
    ///     new EuclideanLayer(16, 5, 46, 60, 2, 0.25, "Open HH")   // 5 open hihats, rotated
    /// );
    /// </code>
    /// </example>
    public static Pattern CombineLayers(ISynth synth, params EuclideanLayer[] layers)
    {
        if (layers.Length == 0)
            throw new ArgumentException("At least one layer is required.", nameof(layers));

        // Find the longest loop length
        double maxLoopLength = layers.Max(l => l.Steps * l.StepLength);

        var pattern = new Pattern(synth)
        {
            LoopLength = maxLoopLength,
            IsLooping = true,
            Name = "Euclidean Layers"
        };

        foreach (var layer in layers)
        {
            var boolPattern = GeneratePattern(layer.Steps, layer.Pulses, layer.Rotation);

            for (int i = 0; i < boolPattern.Length; i++)
            {
                if (boolPattern[i])
                {
                    pattern.Events.Add(new NoteEvent
                    {
                        Beat = i * layer.StepLength,
                        Note = layer.Note,
                        Velocity = layer.Velocity,
                        Duration = layer.StepLength * 0.9
                    });
                }
            }
        }

        // Sort events by beat position
        pattern.Events = pattern.Events.OrderBy(e => e.Beat).ThenBy(e => e.Note).ToList();

        return pattern;
    }

    /// <summary>
    /// Creates a list of patterns, one for each Euclidean layer.
    /// Useful when each layer needs a separate synth or track.
    /// </summary>
    /// <param name="synthFactory">Function to create a synth for each layer.</param>
    /// <param name="layers">Array of Euclidean layers.</param>
    /// <returns>List of patterns, one per layer.</returns>
    public static List<Pattern> CreateLayeredPatterns(Func<EuclideanLayer, ISynth> synthFactory, params EuclideanLayer[] layers)
    {
        var patterns = new List<Pattern>();

        foreach (var layer in layers)
        {
            var synth = synthFactory(layer);
            var pattern = GenerateNotePattern(
                layer.Steps,
                layer.Pulses,
                layer.Rotation,
                layer.Note,
                layer.StepLength,
                layer.Velocity,
                synth
            );
            pattern.Name = layer.Name;
            patterns.Add(pattern);
        }

        return patterns;
    }

    #endregion

    #region Pattern Analysis

    /// <summary>
    /// Converts a boolean pattern to a visual string representation.
    /// </summary>
    /// <param name="pattern">The boolean pattern.</param>
    /// <param name="pulseChar">Character for pulses (default 'x').</param>
    /// <param name="restChar">Character for rests (default '.').</param>
    /// <returns>String representation of the pattern.</returns>
    public static string PatternToString(bool[] pattern, char pulseChar = 'x', char restChar = '.')
    {
        return string.Join(" ", pattern.Select(p => p ? pulseChar : restChar));
    }

    /// <summary>
    /// Calculates the inter-onset intervals (IOIs) of a pattern.
    /// IOIs represent the distances between consecutive pulses.
    /// </summary>
    /// <param name="pattern">The boolean pattern.</param>
    /// <returns>Array of intervals (in steps) between consecutive pulses.</returns>
    public static int[] GetInterOnsetIntervals(bool[] pattern)
    {
        var pulseIndices = new List<int>();
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i])
                pulseIndices.Add(i);
        }

        if (pulseIndices.Count <= 1)
            return Array.Empty<int>();

        var intervals = new int[pulseIndices.Count];
        for (int i = 0; i < pulseIndices.Count - 1; i++)
        {
            intervals[i] = pulseIndices[i + 1] - pulseIndices[i];
        }
        // Wrap-around interval
        intervals[pulseIndices.Count - 1] = pattern.Length - pulseIndices[pulseIndices.Count - 1] + pulseIndices[0];

        return intervals;
    }

    /// <summary>
    /// Checks if a pattern is maximally even (Euclidean).
    /// A maximally even pattern has inter-onset intervals differing by at most 1.
    /// </summary>
    /// <param name="pattern">The pattern to check.</param>
    /// <returns>True if the pattern is maximally even.</returns>
    public static bool IsMaximallyEven(bool[] pattern)
    {
        var iois = GetInterOnsetIntervals(pattern);
        if (iois.Length == 0) return true;

        int min = iois.Min();
        int max = iois.Max();
        return max - min <= 1;
    }

    /// <summary>
    /// Counts the number of pulses (true values) in a pattern.
    /// </summary>
    public static int CountPulses(bool[] pattern)
    {
        return pattern.Count(p => p);
    }

    #endregion

    #region Common Presets

    /// <summary>
    /// Common Euclidean rhythm presets from world music traditions.
    /// </summary>
    public static class Presets
    {
        /// <summary>E(3,8) - Cuban Tresillo rhythm, foundation of Afro-Cuban music.</summary>
        public static readonly EuclideanPreset Tresillo = new(8, 3, 0, "Tresillo", "Cuban");

        /// <summary>E(5,8) - Cuban Cinquillo rhythm, common in son and salsa.</summary>
        public static readonly EuclideanPreset Cinquillo = new(8, 5, 0, "Cinquillo", "Cuban");

        /// <summary>E(3,4) - Basic Cumbia rhythm.</summary>
        public static readonly EuclideanPreset Cumbia = new(4, 3, 0, "Cumbia", "Colombian");

        /// <summary>E(2,3) - Common in West African music.</summary>
        public static readonly EuclideanPreset WestAfrican2_3 = new(3, 2, 0, "West African 2/3", "West African");

        /// <summary>E(3,7) - Turkish Aksak rhythm.</summary>
        public static readonly EuclideanPreset Aksak = new(7, 3, 0, "Aksak", "Turkish");

        /// <summary>E(4,7) - Common in Bulgarian folk music.</summary>
        public static readonly EuclideanPreset Bulgarian4_7 = new(7, 4, 0, "Bulgarian 4/7", "Bulgarian");

        /// <summary>E(5,7) - Found in many African traditions.</summary>
        public static readonly EuclideanPreset African5_7 = new(7, 5, 0, "African 5/7", "African");

        /// <summary>E(7,12) - West African bell pattern.</summary>
        public static readonly EuclideanPreset BellPattern = new(12, 7, 0, "Bell Pattern", "West African");

        /// <summary>E(5,12) - Standard clave pattern.</summary>
        public static readonly EuclideanPreset Clave = new(12, 5, 0, "Clave", "Afro-Cuban");

        /// <summary>E(7,16) - Brazilian Samba rhythm.</summary>
        public static readonly EuclideanPreset Samba = new(16, 7, 0, "Samba", "Brazilian");

        /// <summary>E(9,16) - Common in Afro-Brazilian music.</summary>
        public static readonly EuclideanPreset AfroBrazilian = new(16, 9, 0, "Afro-Brazilian", "Brazilian");

        /// <summary>E(5,16) - Bossa Nova rhythm.</summary>
        public static readonly EuclideanPreset BossaNova = new(16, 5, 0, "Bossa Nova", "Brazilian");

        /// <summary>E(4,9) - Turkish Aksak in 9/8 time.</summary>
        public static readonly EuclideanPreset Aksak9 = new(9, 4, 0, "Aksak 9/8", "Turkish");

        /// <summary>E(5,9) - Arabic Sama'i rhythm.</summary>
        public static readonly EuclideanPreset Samai = new(9, 5, 0, "Sama'i", "Arabic");

        /// <summary>E(2,5) - Khafif-e-ramal (Persian music).</summary>
        public static readonly EuclideanPreset KhafifRamal = new(5, 2, 0, "Khafif-e-ramal", "Persian");

        /// <summary>E(4,11) - Frank Zappa's "Outside Now" rhythm.</summary>
        public static readonly EuclideanPreset OutsideNow = new(11, 4, 0, "Outside Now", "Frank Zappa");

        /// <summary>E(6,13) - Ruchenitza Bulgarian folk rhythm.</summary>
        public static readonly EuclideanPreset Ruchenitza = new(13, 6, 0, "Ruchenitza", "Bulgarian");

        /// <summary>Gets all available presets.</summary>
        public static IEnumerable<EuclideanPreset> All => new[]
        {
            Tresillo, Cinquillo, Cumbia, WestAfrican2_3, Aksak, Bulgarian4_7,
            African5_7, BellPattern, Clave, Samba, AfroBrazilian, BossaNova,
            Aksak9, Samai, KhafifRamal, OutsideNow, Ruchenitza
        };
    }

    #endregion

    #region Drum Pattern Helpers

    /// <summary>
    /// Standard General MIDI drum note numbers.
    /// </summary>
    public static class DrumNotes
    {
        public const int Kick = 36;
        public const int Snare = 38;
        public const int SideStick = 37;
        public const int ClosedHiHat = 42;
        public const int OpenHiHat = 46;
        public const int PedalHiHat = 44;
        public const int Clap = 39;
        public const int LowTom = 41;
        public const int MidTom = 47;
        public const int HighTom = 50;
        public const int Crash = 49;
        public const int Ride = 51;
        public const int RideBell = 53;
        public const int Cowbell = 56;
        public const int Tambourine = 54;
        public const int Shaker = 70;
        public const int Clave = 75;
        public const int Conga = 63;
        public const int Bongo = 61;
    }

    /// <summary>
    /// Creates a basic Euclidean drum kit pattern with kick, snare, and hihat.
    /// </summary>
    /// <param name="synth">Drum synthesizer to use.</param>
    /// <param name="steps">Number of steps (default 16 for one bar of 16th notes).</param>
    /// <param name="kickPulses">Number of kick drum hits (default 4).</param>
    /// <param name="snarePulses">Number of snare hits (default 2).</param>
    /// <param name="hihatPulses">Number of hihat hits (default 8).</param>
    /// <param name="stepLength">Duration per step in beats (default 0.25 for 16th notes).</param>
    /// <returns>A combined drum pattern.</returns>
    public static Pattern CreateBasicDrumPattern(
        ISynth synth,
        int steps = 16,
        int kickPulses = 4,
        int snarePulses = 2,
        int hihatPulses = 8,
        double stepLength = 0.25)
    {
        return CombineLayers(synth,
            new EuclideanLayer(steps, kickPulses, DrumNotes.Kick, 110, 0, stepLength, "Kick"),
            new EuclideanLayer(steps, snarePulses, DrumNotes.Snare, 100, steps / 4, stepLength, "Snare"),
            new EuclideanLayer(steps, hihatPulses, DrumNotes.ClosedHiHat, 80, 0, stepLength, "HiHat")
        );
    }

    /// <summary>
    /// Creates a polyrhythmic pattern using different step counts for each layer.
    /// Creates interesting cross-rhythms through phase relationships.
    /// </summary>
    /// <param name="synth">Synthesizer to use.</param>
    /// <param name="baseSteps">Base number of steps for reference (determines loop length).</param>
    /// <param name="stepLength">Duration per step.</param>
    /// <returns>A polyrhythmic drum pattern.</returns>
    /// <example>
    /// <code>
    /// // Creates 3-against-4-against-5 polyrhythm
    /// var polyPattern = EuclideanRhythm.CreatePolyrhythmicPattern(drumSynth, 12);
    /// </code>
    /// </example>
    public static Pattern CreatePolyrhythmicPattern(ISynth synth, int baseSteps = 12, double stepLength = 0.25)
    {
        return CombineLayers(synth,
            new EuclideanLayer(baseSteps, 3, DrumNotes.Kick, 110, 0, stepLength, "3-Pulse"),
            new EuclideanLayer(baseSteps, 4, DrumNotes.Snare, 100, 0, stepLength, "4-Pulse"),
            new EuclideanLayer(baseSteps, 5, DrumNotes.ClosedHiHat, 80, 0, stepLength, "5-Pulse")
        );
    }

    #endregion
}
