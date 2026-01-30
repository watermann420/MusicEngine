//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Algorithmic rhythm generator with Euclidean, random, and preset patterns.

namespace MusicEngine.Core.MIDI;

/// <summary>
/// Rhythm generation algorithm type.
/// </summary>
public enum RhythmAlgorithm
{
    /// <summary>Euclidean distribution using Bjorklund's algorithm.</summary>
    Euclidean,
    /// <summary>Random pattern with probability per step.</summary>
    Random,
    /// <summary>Preset pattern from built-in library.</summary>
    Preset,
    /// <summary>Probability-based with weighted step selection.</summary>
    Probability,
    /// <summary>Density-based distribution with clustering.</summary>
    Density
}

/// <summary>
/// Preset rhythm pattern types.
/// </summary>
public enum RhythmPresetType
{
    /// <summary>Four-on-the-floor kick pattern (house, techno).</summary>
    FourOnFloor,
    /// <summary>Classic breakbeat pattern (Amen, Think).</summary>
    Breakbeat,
    /// <summary>Disco/funk hi-hat pattern.</summary>
    DiscoHiHat,
    /// <summary>Basic rock beat.</summary>
    RockBeat,
    /// <summary>Reggae one-drop rhythm.</summary>
    OneDrop,
    /// <summary>Hip-hop boom-bap pattern.</summary>
    BoomBap,
    /// <summary>Drum and bass two-step pattern.</summary>
    DnBTwoStep,
    /// <summary>Shuffle/swing pattern.</summary>
    Shuffle,
    /// <summary>Latin clave 3-2.</summary>
    Clave32,
    /// <summary>Latin clave 2-3.</summary>
    Clave23,
    /// <summary>Soca/calypso pattern.</summary>
    Soca,
    /// <summary>Trap hi-hat pattern.</summary>
    TrapHiHat,
    /// <summary>UK garage 2-step.</summary>
    UKGarage,
    /// <summary>Afrobeat pattern.</summary>
    Afrobeat,
    /// <summary>Bossa nova kick pattern.</summary>
    BossaNova
}

/// <summary>
/// Configuration for rhythm generation.
/// </summary>
public class RhythmConfig
{
    /// <summary>Number of steps in the pattern.</summary>
    public int Steps { get; set; } = 16;

    /// <summary>Number of pulses (hits) for Euclidean algorithm.</summary>
    public int Pulses { get; set; } = 4;

    /// <summary>Rotation offset for the pattern.</summary>
    public int Rotation { get; set; }

    /// <summary>Swing amount (0.0 = straight, 1.0 = full swing).</summary>
    public double Swing { get; set; }

    /// <summary>Swing interval in steps (typically 2 for 16th note swing).</summary>
    public int SwingInterval { get; set; } = 2;

    /// <summary>Base velocity for notes (0-127).</summary>
    public int BaseVelocity { get; set; } = 100;

    /// <summary>Velocity variation amount (0-127).</summary>
    public int VelocityVariation { get; set; }

    /// <summary>Accent pattern as bitmask (bit 0 = step 0, etc.) or null for no accents.</summary>
    public int[]? AccentPattern { get; set; }

    /// <summary>Velocity for accented notes (0-127).</summary>
    public int AccentVelocity { get; set; } = 127;

    /// <summary>MIDI note number for the rhythm.</summary>
    public int Note { get; set; } = 36; // Kick drum

    /// <summary>Step length in beats (0.25 = 16th notes at 4/4).</summary>
    public double StepLength { get; set; } = 0.25;

    /// <summary>Probability (0.0-1.0) for random algorithm.</summary>
    public double Probability { get; set; } = 0.5;

    /// <summary>Algorithm to use for generation.</summary>
    public RhythmAlgorithm Algorithm { get; set; } = RhythmAlgorithm.Euclidean;

    /// <summary>Preset type when using Preset algorithm.</summary>
    public RhythmPresetType PresetType { get; set; } = RhythmPresetType.FourOnFloor;

    /// <summary>Random seed for reproducibility (null for non-deterministic).</summary>
    public int? Seed { get; set; }

    /// <summary>Note duration as fraction of step length (0.0-1.0).</summary>
    public double NoteDurationFraction { get; set; } = 0.9;

    /// <summary>Humanize timing amount in beats (0 = perfect timing).</summary>
    public double HumanizeTiming { get; set; }

    /// <summary>Humanize velocity amount (0-127).</summary>
    public int HumanizeVelocity { get; set; }

    /// <summary>
    /// Creates a default rhythm configuration.
    /// </summary>
    public RhythmConfig() { }

    /// <summary>
    /// Creates a rhythm configuration with basic parameters.
    /// </summary>
    public RhythmConfig(int steps, int pulses, int note = 36, int velocity = 100)
    {
        Steps = steps;
        Pulses = pulses;
        Note = note;
        BaseVelocity = velocity;
    }

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    public RhythmConfig Clone()
    {
        return new RhythmConfig
        {
            Steps = Steps,
            Pulses = Pulses,
            Rotation = Rotation,
            Swing = Swing,
            SwingInterval = SwingInterval,
            BaseVelocity = BaseVelocity,
            VelocityVariation = VelocityVariation,
            AccentPattern = AccentPattern != null ? (int[])AccentPattern.Clone() : null,
            AccentVelocity = AccentVelocity,
            Note = Note,
            StepLength = StepLength,
            Probability = Probability,
            Algorithm = Algorithm,
            PresetType = PresetType,
            Seed = Seed,
            NoteDurationFraction = NoteDurationFraction,
            HumanizeTiming = HumanizeTiming,
            HumanizeVelocity = HumanizeVelocity
        };
    }
}

/// <summary>
/// Represents a generated rhythm step.
/// </summary>
public readonly struct RhythmStep
{
    /// <summary>Step index (0-based).</summary>
    public int Index { get; init; }

    /// <summary>Whether this step has a hit.</summary>
    public bool IsHit { get; init; }

    /// <summary>Beat position (with swing applied).</summary>
    public double Beat { get; init; }

    /// <summary>Velocity for this step.</summary>
    public int Velocity { get; init; }

    /// <summary>Whether this step is accented.</summary>
    public bool IsAccent { get; init; }

    /// <summary>Duration of the note in beats.</summary>
    public double Duration { get; init; }
}

/// <summary>
/// Result of rhythm generation containing pattern data and metadata.
/// </summary>
public class RhythmResult
{
    /// <summary>The raw boolean pattern.</summary>
    public bool[] Pattern { get; init; } = Array.Empty<bool>();

    /// <summary>Detailed step information.</summary>
    public RhythmStep[] Steps { get; init; } = Array.Empty<RhythmStep>();

    /// <summary>Configuration used for generation.</summary>
    public RhythmConfig Config { get; init; } = new();

    /// <summary>Total pattern length in beats.</summary>
    public double LengthInBeats => Config.Steps * Config.StepLength;

    /// <summary>Number of hits in the pattern.</summary>
    public int HitCount => Pattern.Count(p => p);

    /// <summary>Pattern density (0.0-1.0).</summary>
    public double Density => Pattern.Length > 0 ? (double)HitCount / Pattern.Length : 0;
}

/// <summary>
/// Algorithmic rhythm generator with support for Euclidean, random, and preset patterns.
/// Provides features like swing, velocity variation, accents, and pattern mutation.
/// </summary>
/// <example>
/// <code>
/// // Generate a Euclidean kick pattern
/// var generator = new RhythmGenerator();
/// var kickPattern = generator.Generate(new RhythmConfig
/// {
///     Steps = 16,
///     Pulses = 4,
///     Note = 36,
///     Algorithm = RhythmAlgorithm.Euclidean
/// });
///
/// // Generate a preset breakbeat
/// var breakbeat = generator.GeneratePreset(RhythmPresetType.Breakbeat, 38); // Snare
///
/// // Combine patterns into a drum kit
/// var drums = RhythmGenerator.CombinePatterns(synth,
///     generator.Generate(new RhythmConfig { Steps = 16, Pulses = 4, Note = 36 }),
///     generator.Generate(new RhythmConfig { Steps = 16, Pulses = 2, Note = 38, Rotation = 4 }),
///     generator.Generate(new RhythmConfig { Steps = 16, Pulses = 8, Note = 42 })
/// );
/// </code>
/// </example>
public class RhythmGenerator
{
    private Random _random;

    /// <summary>
    /// Creates a new rhythm generator with non-deterministic randomness.
    /// </summary>
    public RhythmGenerator()
    {
        _random = new Random();
    }

    /// <summary>
    /// Creates a new rhythm generator with a specific seed for reproducibility.
    /// </summary>
    /// <param name="seed">Random seed.</param>
    public RhythmGenerator(int seed)
    {
        _random = new Random(seed);
    }

    #region Core Generation

    /// <summary>
    /// Generates a rhythm pattern based on the provided configuration.
    /// </summary>
    /// <param name="config">Rhythm configuration.</param>
    /// <returns>Rhythm result with pattern and step data.</returns>
    public RhythmResult Generate(RhythmConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.Steps <= 0)
            throw new ArgumentOutOfRangeException(nameof(config), "Steps must be greater than 0.");

        // Use seed if provided
        if (config.Seed.HasValue)
        {
            _random = new Random(config.Seed.Value);
        }

        bool[] pattern = config.Algorithm switch
        {
            RhythmAlgorithm.Euclidean => GenerateEuclidean(config.Steps, config.Pulses, config.Rotation),
            RhythmAlgorithm.Random => GenerateRandom(config.Steps, config.Probability),
            RhythmAlgorithm.Preset => GenerateFromPreset(config.PresetType, config.Steps),
            RhythmAlgorithm.Probability => GenerateProbability(config.Steps, config.Pulses),
            RhythmAlgorithm.Density => GenerateDensity(config.Steps, config.Pulses),
            _ => GenerateEuclidean(config.Steps, config.Pulses, config.Rotation)
        };

        // Apply rotation if not already applied by algorithm
        if (config.Algorithm != RhythmAlgorithm.Euclidean && config.Rotation != 0)
        {
            pattern = RotatePattern(pattern, config.Rotation);
        }

        // Generate detailed step information
        var steps = new RhythmStep[config.Steps];
        for (int i = 0; i < config.Steps; i++)
        {
            double beat = CalculateBeatPosition(i, config);
            int velocity = CalculateVelocity(i, pattern[i], config);
            bool isAccent = IsAccentedStep(i, config);

            // Apply humanization
            if (config.HumanizeTiming > 0)
            {
                beat += (_random.NextDouble() - 0.5) * config.HumanizeTiming * 2;
            }
            if (config.HumanizeVelocity > 0 && pattern[i])
            {
                velocity = Math.Clamp(
                    velocity + (int)((_random.NextDouble() - 0.5) * config.HumanizeVelocity * 2),
                    1, 127);
            }

            steps[i] = new RhythmStep
            {
                Index = i,
                IsHit = pattern[i],
                Beat = beat,
                Velocity = velocity,
                IsAccent = isAccent,
                Duration = config.StepLength * config.NoteDurationFraction
            };
        }

        return new RhythmResult
        {
            Pattern = pattern,
            Steps = steps,
            Config = config.Clone()
        };
    }

    /// <summary>
    /// Generates a pattern using the Euclidean algorithm.
    /// </summary>
    private bool[] GenerateEuclidean(int steps, int pulses, int rotation)
    {
        pulses = Math.Clamp(pulses, 0, steps);

        if (pulses == 0)
            return new bool[steps];
        if (pulses == steps)
            return Enumerable.Repeat(true, steps).ToArray();

        var pattern = BjorklundAlgorithm(steps, pulses);

        if (rotation != 0)
        {
            pattern = RotatePattern(pattern, rotation);
        }

        return pattern;
    }

    /// <summary>
    /// Generates a random pattern based on probability.
    /// </summary>
    private bool[] GenerateRandom(int steps, double probability)
    {
        probability = Math.Clamp(probability, 0.0, 1.0);
        var pattern = new bool[steps];

        for (int i = 0; i < steps; i++)
        {
            pattern[i] = _random.NextDouble() < probability;
        }

        return pattern;
    }

    /// <summary>
    /// Generates a probability-weighted pattern targeting a specific pulse count.
    /// </summary>
    private bool[] GenerateProbability(int steps, int targetPulses)
    {
        targetPulses = Math.Clamp(targetPulses, 0, steps);
        var pattern = new bool[steps];
        var indices = Enumerable.Range(0, steps).OrderBy(_ => _random.Next()).Take(targetPulses).ToHashSet();

        foreach (int i in indices)
        {
            pattern[i] = true;
        }

        return pattern;
    }

    /// <summary>
    /// Generates a density-based pattern with natural clustering.
    /// </summary>
    private bool[] GenerateDensity(int steps, int pulses)
    {
        pulses = Math.Clamp(pulses, 0, steps);
        var pattern = new bool[steps];

        if (pulses == 0) return pattern;
        if (pulses >= steps) return Enumerable.Repeat(true, steps).ToArray();

        // Start with a random position
        int currentPos = _random.Next(steps);
        pattern[currentPos] = true;
        int placed = 1;

        // Place remaining pulses with preference for nearby positions
        while (placed < pulses)
        {
            // Choose a step with probability weighted by distance from existing hits
            var weights = new double[steps];
            for (int i = 0; i < steps; i++)
            {
                if (pattern[i]) continue;

                // Find distance to nearest hit
                int minDist = steps;
                for (int j = 0; j < steps; j++)
                {
                    if (pattern[j])
                    {
                        int dist = Math.Min(Math.Abs(i - j), steps - Math.Abs(i - j));
                        minDist = Math.Min(minDist, dist);
                    }
                }

                // Weight inversely proportional to distance (prefer clustering)
                weights[i] = 1.0 / (minDist + 1);
            }

            // Normalize and select
            double total = weights.Sum();
            if (total == 0) break;

            double target = _random.NextDouble() * total;
            double cumulative = 0;
            for (int i = 0; i < steps; i++)
            {
                cumulative += weights[i];
                if (cumulative >= target && !pattern[i])
                {
                    pattern[i] = true;
                    placed++;
                    break;
                }
            }
        }

        return pattern;
    }

    /// <summary>
    /// Generates a pattern from a built-in preset.
    /// </summary>
    private bool[] GenerateFromPreset(RhythmPresetType preset, int steps)
    {
        // Get the preset pattern
        bool[] presetPattern = GetPresetPattern(preset);

        // Scale pattern to requested step count if different
        if (presetPattern.Length != steps)
        {
            return ScalePattern(presetPattern, steps);
        }

        return presetPattern;
    }

    /// <summary>
    /// Gets the raw pattern for a preset type.
    /// </summary>
    private static bool[] GetPresetPattern(RhythmPresetType preset)
    {
        return preset switch
        {
            // Four-on-the-floor: x . . . x . . . x . . . x . . . (kick on 1, 2, 3, 4)
            RhythmPresetType.FourOnFloor => new bool[]
            {
                true, false, false, false, true, false, false, false,
                true, false, false, false, true, false, false, false
            },

            // Breakbeat (Amen-style): x . . . . . x . . . x . . . . .
            RhythmPresetType.Breakbeat => new bool[]
            {
                true, false, false, false, false, false, true, false,
                false, false, true, false, false, false, false, false
            },

            // Disco hi-hat: x . x . x . x . x . x . x . x .
            RhythmPresetType.DiscoHiHat => new bool[]
            {
                true, false, true, false, true, false, true, false,
                true, false, true, false, true, false, true, false
            },

            // Rock beat snare: . . . . x . . . . . . . x . . .
            RhythmPresetType.RockBeat => new bool[]
            {
                false, false, false, false, true, false, false, false,
                false, false, false, false, true, false, false, false
            },

            // One-drop (reggae): . . . . . . . . . . . . x . . .
            RhythmPresetType.OneDrop => new bool[]
            {
                false, false, false, false, false, false, false, false,
                false, false, false, false, true, false, false, false
            },

            // Boom-bap: x . . . . . . . x . x . . . . .
            RhythmPresetType.BoomBap => new bool[]
            {
                true, false, false, false, false, false, false, false,
                true, false, true, false, false, false, false, false
            },

            // DnB two-step: x . . . . . . . . . x . . . . .
            RhythmPresetType.DnBTwoStep => new bool[]
            {
                true, false, false, false, false, false, false, false,
                false, false, true, false, false, false, false, false
            },

            // Shuffle (swung feel represented): x . . x . . x . . x . .
            RhythmPresetType.Shuffle => new bool[]
            {
                true, false, false, true, false, false, true, false,
                false, true, false, false
            },

            // 3-2 Clave: x . . x . . x . . . x . x . . .
            RhythmPresetType.Clave32 => new bool[]
            {
                true, false, false, true, false, false, true, false,
                false, false, true, false, true, false, false, false
            },

            // 2-3 Clave: . . x . x . . . x . . x . . x .
            RhythmPresetType.Clave23 => new bool[]
            {
                false, false, true, false, true, false, false, false,
                true, false, false, true, false, false, true, false
            },

            // Soca: x . x . x . . . x . x . x . . .
            RhythmPresetType.Soca => new bool[]
            {
                true, false, true, false, true, false, false, false,
                true, false, true, false, true, false, false, false
            },

            // Trap hi-hat (fast): x x x x x . x x x x x . x x x .
            RhythmPresetType.TrapHiHat => new bool[]
            {
                true, true, true, true, true, false, true, true,
                true, true, true, false, true, true, true, false
            },

            // UK Garage 2-step: x . . . x . . . . . x . x . . .
            RhythmPresetType.UKGarage => new bool[]
            {
                true, false, false, false, true, false, false, false,
                false, false, true, false, true, false, false, false
            },

            // Afrobeat: x . x . x . . . x . . . x . x .
            RhythmPresetType.Afrobeat => new bool[]
            {
                true, false, true, false, true, false, false, false,
                true, false, false, false, true, false, true, false
            },

            // Bossa nova: x . . x . . x . . . x . . x . .
            RhythmPresetType.BossaNova => new bool[]
            {
                true, false, false, true, false, false, true, false,
                false, false, true, false, false, true, false, false
            },

            _ => new bool[]
            {
                true, false, false, false, true, false, false, false,
                true, false, false, false, true, false, false, false
            }
        };
    }

    #endregion

    #region Pattern Operations

    /// <summary>
    /// Mutates a pattern by randomly flipping some steps.
    /// </summary>
    /// <param name="result">Original rhythm result.</param>
    /// <param name="mutationRate">Probability of each step being mutated (0.0-1.0).</param>
    /// <returns>New mutated rhythm result.</returns>
    public RhythmResult Mutate(RhythmResult result, double mutationRate = 0.1)
    {
        ArgumentNullException.ThrowIfNull(result);
        mutationRate = Math.Clamp(mutationRate, 0.0, 1.0);

        var newPattern = (bool[])result.Pattern.Clone();

        for (int i = 0; i < newPattern.Length; i++)
        {
            if (_random.NextDouble() < mutationRate)
            {
                newPattern[i] = !newPattern[i];
            }
        }

        // Regenerate with the new pattern
        var config = result.Config.Clone();
        config.Algorithm = RhythmAlgorithm.Random; // Bypass normal generation

        // Create new steps based on mutated pattern
        var steps = new RhythmStep[config.Steps];
        for (int i = 0; i < config.Steps; i++)
        {
            double beat = CalculateBeatPosition(i, config);
            int velocity = CalculateVelocity(i, newPattern[i], config);
            bool isAccent = IsAccentedStep(i, config);

            steps[i] = new RhythmStep
            {
                Index = i,
                IsHit = newPattern[i],
                Beat = beat,
                Velocity = velocity,
                IsAccent = isAccent,
                Duration = config.StepLength * config.NoteDurationFraction
            };
        }

        return new RhythmResult
        {
            Pattern = newPattern,
            Steps = steps,
            Config = config
        };
    }

    /// <summary>
    /// Combines two patterns using a logical operation.
    /// </summary>
    /// <param name="a">First pattern.</param>
    /// <param name="b">Second pattern.</param>
    /// <param name="operation">Combine operation (AND, OR, XOR).</param>
    /// <returns>Combined pattern (uses configuration from pattern A).</returns>
    public RhythmResult Combine(RhythmResult a, RhythmResult b, CombineOperation operation = CombineOperation.Or)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        int length = Math.Max(a.Pattern.Length, b.Pattern.Length);
        var newPattern = new bool[length];

        for (int i = 0; i < length; i++)
        {
            bool aVal = i < a.Pattern.Length && a.Pattern[i];
            bool bVal = i < b.Pattern.Length && b.Pattern[i];

            newPattern[i] = operation switch
            {
                CombineOperation.And => aVal && bVal,
                CombineOperation.Or => aVal || bVal,
                CombineOperation.Xor => aVal ^ bVal,
                _ => aVal || bVal
            };
        }

        var config = a.Config.Clone();
        config.Steps = length;

        var steps = new RhythmStep[length];
        for (int i = 0; i < length; i++)
        {
            double beat = CalculateBeatPosition(i, config);
            int velocity = CalculateVelocity(i, newPattern[i], config);
            bool isAccent = IsAccentedStep(i, config);

            steps[i] = new RhythmStep
            {
                Index = i,
                IsHit = newPattern[i],
                Beat = beat,
                Velocity = velocity,
                IsAccent = isAccent,
                Duration = config.StepLength * config.NoteDurationFraction
            };
        }

        return new RhythmResult
        {
            Pattern = newPattern,
            Steps = steps,
            Config = config
        };
    }

    /// <summary>
    /// Rotates a pattern by the specified number of steps.
    /// </summary>
    /// <param name="result">Original rhythm result.</param>
    /// <param name="amount">Steps to rotate (positive = left, negative = right).</param>
    /// <returns>Rotated rhythm result.</returns>
    public RhythmResult Rotate(RhythmResult result, int amount)
    {
        ArgumentNullException.ThrowIfNull(result);

        var newPattern = RotatePattern(result.Pattern, amount);
        var config = result.Config.Clone();
        config.Rotation = (config.Rotation + amount) % config.Steps;

        var steps = new RhythmStep[config.Steps];
        for (int i = 0; i < config.Steps; i++)
        {
            double beat = CalculateBeatPosition(i, config);
            int velocity = CalculateVelocity(i, newPattern[i], config);
            bool isAccent = IsAccentedStep(i, config);

            steps[i] = new RhythmStep
            {
                Index = i,
                IsHit = newPattern[i],
                Beat = beat,
                Velocity = velocity,
                IsAccent = isAccent,
                Duration = config.StepLength * config.NoteDurationFraction
            };
        }

        return new RhythmResult
        {
            Pattern = newPattern,
            Steps = steps,
            Config = config
        };
    }

    /// <summary>
    /// Inverts a pattern (hits become rests and vice versa).
    /// </summary>
    /// <param name="result">Original rhythm result.</param>
    /// <returns>Inverted rhythm result.</returns>
    public RhythmResult Invert(RhythmResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var newPattern = result.Pattern.Select(p => !p).ToArray();
        var config = result.Config.Clone();

        var steps = new RhythmStep[config.Steps];
        for (int i = 0; i < config.Steps; i++)
        {
            double beat = CalculateBeatPosition(i, config);
            int velocity = CalculateVelocity(i, newPattern[i], config);
            bool isAccent = IsAccentedStep(i, config);

            steps[i] = new RhythmStep
            {
                Index = i,
                IsHit = newPattern[i],
                Beat = beat,
                Velocity = velocity,
                IsAccent = isAccent,
                Duration = config.StepLength * config.NoteDurationFraction
            };
        }

        return new RhythmResult
        {
            Pattern = newPattern,
            Steps = steps,
            Config = config
        };
    }

    /// <summary>
    /// Reverses a pattern.
    /// </summary>
    /// <param name="result">Original rhythm result.</param>
    /// <returns>Reversed rhythm result.</returns>
    public RhythmResult Reverse(RhythmResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var newPattern = result.Pattern.Reverse().ToArray();
        var config = result.Config.Clone();

        var steps = new RhythmStep[config.Steps];
        for (int i = 0; i < config.Steps; i++)
        {
            double beat = CalculateBeatPosition(i, config);
            int velocity = CalculateVelocity(i, newPattern[i], config);
            bool isAccent = IsAccentedStep(i, config);

            steps[i] = new RhythmStep
            {
                Index = i,
                IsHit = newPattern[i],
                Beat = beat,
                Velocity = velocity,
                IsAccent = isAccent,
                Duration = config.StepLength * config.NoteDurationFraction
            };
        }

        return new RhythmResult
        {
            Pattern = newPattern,
            Steps = steps,
            Config = config
        };
    }

    #endregion

    #region Conversion

    /// <summary>
    /// Converts a rhythm result to a list of NoteEvents.
    /// </summary>
    /// <param name="result">Rhythm result to convert.</param>
    /// <returns>List of NoteEvents for hits in the pattern.</returns>
    public List<NoteEvent> ToNoteEvents(RhythmResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var events = new List<NoteEvent>();

        foreach (var step in result.Steps)
        {
            if (step.IsHit)
            {
                events.Add(new NoteEvent
                {
                    Beat = step.Beat,
                    Note = result.Config.Note,
                    Velocity = step.Velocity,
                    Duration = step.Duration
                });
            }
        }

        return events;
    }

    /// <summary>
    /// Converts a rhythm result to a Pattern.
    /// </summary>
    /// <param name="result">Rhythm result to convert.</param>
    /// <param name="synth">Synthesizer for the pattern.</param>
    /// <returns>Pattern containing the rhythm.</returns>
    public Pattern ToPattern(RhythmResult result, ISynth synth)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(synth);

        var pattern = new Pattern(synth)
        {
            LoopLength = result.LengthInBeats,
            IsLooping = true,
            Name = $"Rhythm ({result.Config.Algorithm})"
        };

        pattern.Events.AddRange(ToNoteEvents(result));

        return pattern;
    }

    /// <summary>
    /// Generates a pattern directly using a preset type.
    /// </summary>
    /// <param name="preset">Preset type to use.</param>
    /// <param name="note">MIDI note number.</param>
    /// <param name="velocity">Base velocity.</param>
    /// <param name="steps">Number of steps (uses preset default if 0).</param>
    /// <returns>Rhythm result with the preset pattern.</returns>
    public RhythmResult GeneratePreset(RhythmPresetType preset, int note = 36, int velocity = 100, int steps = 0)
    {
        var presetPattern = GetPresetPattern(preset);
        int actualSteps = steps > 0 ? steps : presetPattern.Length;

        return Generate(new RhythmConfig
        {
            Steps = actualSteps,
            Note = note,
            BaseVelocity = velocity,
            Algorithm = RhythmAlgorithm.Preset,
            PresetType = preset
        });
    }

    #endregion

    #region Static Helpers

    /// <summary>
    /// Combines multiple rhythm results into a single Pattern.
    /// </summary>
    /// <param name="synth">Synthesizer for the combined pattern.</param>
    /// <param name="results">Rhythm results to combine.</param>
    /// <returns>Combined Pattern with all note events.</returns>
    public static Pattern CombinePatterns(ISynth synth, params RhythmResult[] results)
    {
        ArgumentNullException.ThrowIfNull(synth);
        if (results.Length == 0)
            throw new ArgumentException("At least one rhythm result is required.", nameof(results));

        double maxLength = results.Max(r => r.LengthInBeats);

        var pattern = new Pattern(synth)
        {
            LoopLength = maxLength,
            IsLooping = true,
            Name = "Combined Rhythms"
        };

        var generator = new RhythmGenerator();
        foreach (var result in results)
        {
            pattern.Events.AddRange(generator.ToNoteEvents(result));
        }

        // Sort by beat position
        pattern.Events = pattern.Events.OrderBy(e => e.Beat).ThenBy(e => e.Note).ToList();

        return pattern;
    }

    /// <summary>
    /// Creates a quick Euclidean rhythm pattern.
    /// </summary>
    /// <param name="steps">Number of steps.</param>
    /// <param name="pulses">Number of pulses.</param>
    /// <param name="note">MIDI note number.</param>
    /// <param name="synth">Synthesizer for the pattern.</param>
    /// <returns>Pattern with Euclidean rhythm.</returns>
    public static Pattern CreateEuclidean(int steps, int pulses, int note, ISynth synth)
    {
        var generator = new RhythmGenerator();
        var result = generator.Generate(new RhythmConfig
        {
            Steps = steps,
            Pulses = pulses,
            Note = note,
            Algorithm = RhythmAlgorithm.Euclidean
        });
        return generator.ToPattern(result, synth);
    }

    /// <summary>
    /// Creates a quick preset rhythm pattern.
    /// </summary>
    /// <param name="preset">Preset type.</param>
    /// <param name="note">MIDI note number.</param>
    /// <param name="synth">Synthesizer for the pattern.</param>
    /// <returns>Pattern with preset rhythm.</returns>
    public static Pattern CreatePreset(RhythmPresetType preset, int note, ISynth synth)
    {
        var generator = new RhythmGenerator();
        var result = generator.GeneratePreset(preset, note);
        return generator.ToPattern(result, synth);
    }

    /// <summary>
    /// Converts a rhythm pattern to a string representation.
    /// </summary>
    /// <param name="pattern">Boolean pattern array.</param>
    /// <param name="hitChar">Character for hits.</param>
    /// <param name="restChar">Character for rests.</param>
    /// <returns>String representation.</returns>
    public static string PatternToString(bool[] pattern, char hitChar = 'x', char restChar = '.')
    {
        return string.Join(" ", pattern.Select(p => p ? hitChar : restChar));
    }

    /// <summary>
    /// Parses a string representation into a boolean pattern.
    /// </summary>
    /// <param name="str">String pattern (e.g., "x . . x . . x .").</param>
    /// <param name="hitChar">Character representing hits.</param>
    /// <returns>Boolean pattern array.</returns>
    public static bool[] ParsePattern(string str, char hitChar = 'x')
    {
        return str.Where(c => !char.IsWhiteSpace(c))
                  .Select(c => c == hitChar)
                  .ToArray();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Bjorklund's algorithm for Euclidean rhythm generation.
    /// </summary>
    private static bool[] BjorklundAlgorithm(int steps, int pulses)
    {
        var groups = new List<List<bool>>();

        for (int i = 0; i < pulses; i++)
            groups.Add(new List<bool> { true });

        int remainder = steps - pulses;
        for (int i = 0; i < remainder; i++)
            groups.Add(new List<bool> { false });

        while (remainder > 1)
        {
            int numToDistribute = Math.Min(pulses, remainder);

            for (int i = 0; i < numToDistribute; i++)
            {
                var last = groups[groups.Count - 1];
                groups.RemoveAt(groups.Count - 1);
                groups[i].AddRange(last);
            }

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

        var result = new List<bool>();
        foreach (var group in groups)
        {
            result.AddRange(group);
        }

        return result.ToArray();
    }

    /// <summary>
    /// Rotates a pattern by the specified amount.
    /// </summary>
    private static bool[] RotatePattern(bool[] pattern, int rotation)
    {
        if (pattern.Length == 0) return pattern;

        rotation = ((rotation % pattern.Length) + pattern.Length) % pattern.Length;
        if (rotation == 0) return (bool[])pattern.Clone();

        var result = new bool[pattern.Length];
        for (int i = 0; i < pattern.Length; i++)
        {
            result[i] = pattern[(i + rotation) % pattern.Length];
        }

        return result;
    }

    /// <summary>
    /// Scales a pattern to a different step count.
    /// </summary>
    private static bool[] ScalePattern(bool[] pattern, int newSteps)
    {
        var result = new bool[newSteps];
        double ratio = (double)pattern.Length / newSteps;

        for (int i = 0; i < newSteps; i++)
        {
            int sourceIndex = (int)(i * ratio);
            result[i] = pattern[sourceIndex % pattern.Length];
        }

        return result;
    }

    /// <summary>
    /// Calculates the beat position for a step with swing applied.
    /// </summary>
    private double CalculateBeatPosition(int stepIndex, RhythmConfig config)
    {
        double baseBeat = stepIndex * config.StepLength;

        // Apply swing to off-beat steps
        if (config.Swing > 0 && config.SwingInterval > 0)
        {
            int swingPosition = stepIndex % config.SwingInterval;
            if (swingPosition > 0)
            {
                // Delay odd steps based on swing amount
                double swingDelay = config.StepLength * config.Swing * 0.5;
                baseBeat += swingDelay * swingPosition;
            }
        }

        return baseBeat;
    }

    /// <summary>
    /// Calculates velocity for a step.
    /// </summary>
    private int CalculateVelocity(int stepIndex, bool isHit, RhythmConfig config)
    {
        if (!isHit) return 0;

        int velocity = config.BaseVelocity;

        // Apply accent
        if (IsAccentedStep(stepIndex, config))
        {
            velocity = config.AccentVelocity;
        }

        // Apply variation
        if (config.VelocityVariation > 0)
        {
            int variation = _random.Next(-config.VelocityVariation, config.VelocityVariation + 1);
            velocity += variation;
        }

        return Math.Clamp(velocity, 1, 127);
    }

    /// <summary>
    /// Checks if a step should be accented.
    /// </summary>
    private bool IsAccentedStep(int stepIndex, RhythmConfig config)
    {
        if (config.AccentPattern == null || config.AccentPattern.Length == 0)
            return false;

        int patternIndex = stepIndex % config.AccentPattern.Length;
        return config.AccentPattern[patternIndex] != 0;
    }

    #endregion
}

/// <summary>
/// Logical operations for combining patterns.
/// </summary>
public enum CombineOperation
{
    /// <summary>Both patterns must have a hit.</summary>
    And,
    /// <summary>Either pattern has a hit.</summary>
    Or,
    /// <summary>Exactly one pattern has a hit.</summary>
    Xor
}
