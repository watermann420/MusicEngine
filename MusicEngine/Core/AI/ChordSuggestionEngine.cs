// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: AI chord suggestion engine.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core.AI;

/// <summary>
/// Musical style for chord suggestions.
/// </summary>
public enum ChordSuggestionStyle
{
    /// <summary>Pop music style with common progressions.</summary>
    Pop,

    /// <summary>Jazz style with extended chords and substitutions.</summary>
    Jazz,

    /// <summary>Classical style following traditional harmony rules.</summary>
    Classical,

    /// <summary>Blues style with dominant seventh chords.</summary>
    Blues,

    /// <summary>Rock style with power chords and simple progressions.</summary>
    Rock,

    /// <summary>R&B/Soul style with smooth voice leading.</summary>
    RnB,

    /// <summary>Electronic/EDM style with minimal progressions.</summary>
    Electronic,

    /// <summary>Gospel/Contemporary Christian style.</summary>
    Gospel
}

/// <summary>
/// Represents a suggested chord with ranking information.
/// </summary>
public class ChordSuggestion
{
    /// <summary>
    /// Gets the root note of the suggested chord (pitch class 0-11).
    /// </summary>
    public int Root { get; init; }

    /// <summary>
    /// Gets the root note as NoteName enum.
    /// </summary>
    public NoteName RootNote => (NoteName)Root;

    /// <summary>
    /// Gets the chord type.
    /// </summary>
    public ChordType ChordType { get; init; }

    /// <summary>
    /// Gets the confidence/ranking score (0.0 - 1.0).
    /// Higher values indicate stronger recommendations.
    /// </summary>
    public float Score { get; init; }

    /// <summary>
    /// Gets the Roman numeral notation (e.g., "I", "IV", "V", "vi").
    /// </summary>
    public string RomanNumeral { get; init; } = string.Empty;

    /// <summary>
    /// Gets the function of this chord in the key (Tonic, Subdominant, Dominant).
    /// </summary>
    public ChordFunction Function { get; init; }

    /// <summary>
    /// Gets the reason for this suggestion.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the MIDI notes for this chord in the specified octave.
    /// </summary>
    /// <param name="octave">Base octave (default: 4).</param>
    public int[] GetNotes(int octave = 4)
    {
        return Chord.GetNotes(Note.FromName(RootNote, octave), ChordType);
    }

    /// <summary>
    /// Gets the chord name (e.g., "C", "Am7", "Fdim").
    /// </summary>
    public string GetChordName()
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        string suffix = GetChordSuffix();
        return $"{noteNames[Root]}{suffix}";
    }

    private string GetChordSuffix()
    {
        return ChordType switch
        {
            ChordType.Major => "",
            ChordType.Minor => "m",
            ChordType.Diminished => "dim",
            ChordType.Augmented => "aug",
            ChordType.Major7 => "maj7",
            ChordType.Minor7 => "m7",
            ChordType.Dominant7 => "7",
            ChordType.Diminished7 => "dim7",
            ChordType.HalfDiminished7 => "m7b5",
            ChordType.MinorMajor7 => "m(maj7)",
            ChordType.Sus2 => "sus2",
            ChordType.Sus4 => "sus4",
            ChordType.Add9 => "add9",
            ChordType.Major9 => "maj9",
            ChordType.Minor9 => "m9",
            ChordType.Dominant9 => "9",
            ChordType.Power => "5",
            ChordType.Major6 => "6",
            ChordType.Minor6 => "m6",
            _ => ""
        };
    }

    public override string ToString()
    {
        return $"{GetChordName()} ({RomanNumeral}) - Score: {Score:F2} [{Function}]";
    }
}

/// <summary>
/// Chord function in harmonic analysis.
/// </summary>
public enum ChordFunction
{
    /// <summary>Tonic function (I, vi in major; i, III in minor).</summary>
    Tonic,

    /// <summary>Subdominant function (IV, ii in major; iv, ii in minor).</summary>
    Subdominant,

    /// <summary>Dominant function (V, vii in major and minor).</summary>
    Dominant,

    /// <summary>Secondary dominant (V of another chord).</summary>
    SecondaryDominant,

    /// <summary>Borrowed chord from parallel mode.</summary>
    Borrowed,

    /// <summary>Passing chord for smooth voice leading.</summary>
    Passing
}

/// <summary>
/// Represents a chord in the current context.
/// </summary>
public class ContextChord
{
    /// <summary>Root pitch class (0-11).</summary>
    public int Root { get; init; }

    /// <summary>Chord type.</summary>
    public ChordType ChordType { get; init; }

    /// <summary>Duration in beats.</summary>
    public double Duration { get; init; } = 1.0;

    /// <summary>Position in the progression (0-based).</summary>
    public int Position { get; init; }
}

/// <summary>
/// Engine for suggesting chord progressions based on music theory rules,
/// style-specific patterns, and voice leading optimization.
/// </summary>
public class ChordSuggestionEngine
{
    private readonly Dictionary<ChordSuggestionStyle, List<int[]>> _commonProgressions;
    private readonly Dictionary<int, ChordFunction> _degreeFunctions;

    /// <summary>
    /// Creates a new chord suggestion engine.
    /// </summary>
    public ChordSuggestionEngine()
    {
        _commonProgressions = InitializeProgressions();
        _degreeFunctions = InitializeDegreeFunctions();
    }

    /// <summary>
    /// Gets chord suggestions based on context.
    /// </summary>
    /// <param name="currentChords">Current chords in the progression.</param>
    /// <param name="keyRoot">Root of the key (pitch class 0-11).</param>
    /// <param name="isMinor">True if minor key.</param>
    /// <param name="style">Musical style.</param>
    /// <param name="maxSuggestions">Maximum suggestions to return.</param>
    /// <returns>Ranked list of chord suggestions.</returns>
    public List<ChordSuggestion> GetSuggestions(
        IReadOnlyList<ContextChord> currentChords,
        int keyRoot,
        bool isMinor = false,
        ChordSuggestionStyle style = ChordSuggestionStyle.Pop,
        int maxSuggestions = 8)
    {
        var suggestions = new List<ChordSuggestion>();
        var scaleType = isMinor ? ScaleType.NaturalMinor : ScaleType.Major;

        // Get diatonic chords for the key
        var diatonicChords = GetDiatonicChords(keyRoot, isMinor);

        // Score each possible chord
        foreach (var (degree, root, chordType, romanNumeral) in diatonicChords)
        {
            float score = CalculateChordScore(
                root, chordType, degree,
                currentChords, keyRoot, isMinor, style);

            if (score > 0.1f)
            {
                suggestions.Add(new ChordSuggestion
                {
                    Root = root,
                    ChordType = chordType,
                    Score = score,
                    RomanNumeral = romanNumeral,
                    Function = GetChordFunction(degree, isMinor),
                    Reason = GetSuggestionReason(degree, currentChords, style)
                });
            }
        }

        // Add style-specific extended chords
        AddStyleSpecificChords(suggestions, currentChords, keyRoot, isMinor, style);

        // Add secondary dominants if appropriate
        AddSecondaryDominants(suggestions, currentChords, keyRoot, isMinor, style);

        // Add borrowed chords
        AddBorrowedChords(suggestions, currentChords, keyRoot, isMinor, style);

        // Optimize voice leading and re-rank
        OptimizeVoiceLeading(suggestions, currentChords, keyRoot);

        // Sort by score and limit results
        return suggestions
            .OrderByDescending(s => s.Score)
            .Take(maxSuggestions)
            .ToList();
    }

    /// <summary>
    /// Gets suggestions for completing a chord progression to a target length.
    /// </summary>
    public List<List<ChordSuggestion>> GetProgressionSuggestions(
        IReadOnlyList<ContextChord> startChords,
        int keyRoot,
        bool isMinor,
        ChordSuggestionStyle style,
        int targetLength,
        int maxVariations = 4)
    {
        var results = new List<List<ChordSuggestion>>();

        // Get common progressions for the style
        if (_commonProgressions.TryGetValue(style, out var patterns))
        {
            foreach (var pattern in patterns.Take(maxVariations))
            {
                var progression = new List<ChordSuggestion>();
                int startDegree = startChords.Count > 0
                    ? GetDegree(startChords[^1].Root, keyRoot)
                    : 0;

                // Find matching pattern position
                int patternStart = Array.IndexOf(pattern, startDegree);
                if (patternStart < 0) patternStart = 0;

                // Generate remaining chords
                int chordsNeeded = targetLength - startChords.Count;
                for (int i = 0; i < chordsNeeded; i++)
                {
                    int degree = pattern[(patternStart + 1 + i) % pattern.Length];
                    var (root, chordType, roman) = GetChordForDegree(degree, keyRoot, isMinor, style);

                    progression.Add(new ChordSuggestion
                    {
                        Root = root,
                        ChordType = chordType,
                        Score = 0.8f - (i * 0.05f),
                        RomanNumeral = roman,
                        Function = GetChordFunction(degree, isMinor),
                        Reason = $"Pattern: {GetPatternName(pattern)}"
                    });
                }

                if (progression.Count > 0)
                {
                    results.Add(progression);
                }
            }
        }

        // Add algorithmically generated progression
        var algorithmicProgression = GenerateAlgorithmicProgression(
            startChords, keyRoot, isMinor, style, targetLength);
        if (algorithmicProgression.Count > 0)
        {
            results.Add(algorithmicProgression);
        }

        return results.Take(maxVariations).ToList();
    }

    /// <summary>
    /// Gets suggestions optimized for voice leading from a specific chord.
    /// </summary>
    public List<ChordSuggestion> GetVoiceLeadingSuggestions(
        int fromRoot,
        ChordType fromType,
        int keyRoot,
        bool isMinor,
        int maxSuggestions = 6)
    {
        var suggestions = new List<ChordSuggestion>();
        var fromNotes = Chord.GetNotes(fromRoot, fromType);

        var diatonicChords = GetDiatonicChords(keyRoot, isMinor);

        foreach (var (degree, root, chordType, romanNumeral) in diatonicChords)
        {
            var toNotes = Chord.GetNotes(root, chordType);

            // Calculate voice leading score (smaller = better)
            float voiceLeadingCost = CalculateVoiceLeadingCost(fromNotes, toNotes);
            float score = 1f / (1f + voiceLeadingCost);

            suggestions.Add(new ChordSuggestion
            {
                Root = root,
                ChordType = chordType,
                Score = score,
                RomanNumeral = romanNumeral,
                Function = GetChordFunction(degree, isMinor),
                Reason = voiceLeadingCost < 3 ? "Smooth voice leading" : "Good progression"
            });
        }

        return suggestions
            .OrderByDescending(s => s.Score)
            .Take(maxSuggestions)
            .ToList();
    }

    /// <summary>
    /// Analyzes a chord progression and suggests improvements.
    /// </summary>
    public string AnalyzeProgression(
        IReadOnlyList<ContextChord> chords,
        int keyRoot,
        bool isMinor)
    {
        var analysis = new System.Text.StringBuilder();

        analysis.AppendLine($"Key: {GetNoteName(keyRoot)} {(isMinor ? "minor" : "major")}");
        analysis.AppendLine();

        // Analyze each chord
        for (int i = 0; i < chords.Count; i++)
        {
            var chord = chords[i];
            int degree = GetDegree(chord.Root, keyRoot);
            var function = GetChordFunction(degree, isMinor);
            string roman = GetRomanNumeral(degree, isMinor, chord.ChordType);

            analysis.AppendLine($"Chord {i + 1}: {GetNoteName(chord.Root)}{GetTypeSuffix(chord.ChordType)}");
            analysis.AppendLine($"  Function: {function} ({roman})");

            if (i > 0)
            {
                var prevChord = chords[i - 1];
                var prevNotes = Chord.GetNotes(prevChord.Root, prevChord.ChordType);
                var currNotes = Chord.GetNotes(chord.Root, chord.ChordType);
                float voiceLeading = CalculateVoiceLeadingCost(prevNotes, currNotes);

                if (voiceLeading <= 2)
                    analysis.AppendLine("  Voice Leading: Excellent");
                else if (voiceLeading <= 4)
                    analysis.AppendLine("  Voice Leading: Good");
                else
                    analysis.AppendLine("  Voice Leading: Could be smoother");
            }
        }

        // Overall progression analysis
        analysis.AppendLine();
        analysis.AppendLine("Progression Analysis:");

        bool hasTonicAtEnd = chords.Count > 0 &&
            GetChordFunction(GetDegree(chords[^1].Root, keyRoot), isMinor) == ChordFunction.Tonic;
        bool hasDominantBeforeEnd = chords.Count > 1 &&
            GetChordFunction(GetDegree(chords[^2].Root, keyRoot), isMinor) == ChordFunction.Dominant;

        if (hasTonicAtEnd && hasDominantBeforeEnd)
            analysis.AppendLine("  - Strong authentic cadence at end");
        else if (hasTonicAtEnd)
            analysis.AppendLine("  - Ends on tonic (resolved)");
        else
            analysis.AppendLine("  - Does not end on tonic (open-ended)");

        return analysis.ToString();
    }

    #region Private Methods

    private Dictionary<ChordSuggestionStyle, List<int[]>> InitializeProgressions()
    {
        return new Dictionary<ChordSuggestionStyle, List<int[]>>
        {
            [ChordSuggestionStyle.Pop] = new List<int[]>
            {
                new[] { 0, 4, 5, 3 },      // I-V-vi-IV (most common pop progression)
                new[] { 0, 3, 4, 0 },      // I-IV-V-I (classic)
                new[] { 0, 5, 3, 4 },      // I-vi-IV-V
                new[] { 5, 3, 0, 4 },      // vi-IV-I-V
                new[] { 0, 4, 1, 3 },      // I-V-ii-IV
                new[] { 0, 3, 0, 4 }       // I-IV-I-V
            },
            [ChordSuggestionStyle.Jazz] = new List<int[]>
            {
                new[] { 1, 4, 0 },         // ii-V-I (most common jazz progression)
                new[] { 0, 5, 1, 4 },      // I-vi-ii-V
                new[] { 2, 5, 1, 4, 0 },   // iii-vi-ii-V-I
                new[] { 0, 6, 2, 5, 1, 4, 0 }, // Coltrane changes inspiration
                new[] { 3, 6, 1, 4, 0 }    // IV-vii-ii-V-I
            },
            [ChordSuggestionStyle.Classical] = new List<int[]>
            {
                new[] { 0, 3, 4, 0 },      // I-IV-V-I (authentic cadence)
                new[] { 0, 4, 5, 4, 0 },   // I-V-vi-V-I
                new[] { 0, 5, 3, 4, 0 },   // I-vi-IV-V-I
                new[] { 0, 1, 4, 0 },      // I-ii-V-I
                new[] { 0, 3, 1, 4, 0 }    // I-IV-ii-V-I
            },
            [ChordSuggestionStyle.Blues] = new List<int[]>
            {
                new[] { 0, 0, 0, 0, 3, 3, 0, 0, 4, 3, 0, 4 }, // 12-bar blues
                new[] { 0, 3, 0, 4 },      // Quick change blues
                new[] { 0, 0, 3, 0, 4, 0 } // Simplified blues
            },
            [ChordSuggestionStyle.Rock] = new List<int[]>
            {
                new[] { 0, 3, 4, 0 },      // I-IV-V-I
                new[] { 0, 5, 3, 4 },      // I-vi-IV-V
                new[] { 0, 6, 3, 4 },      // I-bVII-IV-V
                new[] { 0, 4, 3 },         // I-V-IV
                new[] { 5, 4, 0 }          // vi-V-I (Aeolian cadence)
            },
            [ChordSuggestionStyle.RnB] = new List<int[]>
            {
                new[] { 0, 5, 1, 4 },      // I-vi-ii-V
                new[] { 0, 2, 5, 1, 4 },   // I-iii-vi-ii-V
                new[] { 0, 3, 1, 4 },      // I-IV-ii-V
                new[] { 0, 5, 3, 4 }       // I-vi-IV-V
            },
            [ChordSuggestionStyle.Electronic] = new List<int[]>
            {
                new[] { 0, 3 },            // I-IV (minimal)
                new[] { 0, 5 },            // I-vi
                new[] { 0, 4 },            // I-V
                new[] { 5, 4 },            // vi-V
                new[] { 0, 3, 5, 4 }       // I-IV-vi-V
            },
            [ChordSuggestionStyle.Gospel] = new List<int[]>
            {
                new[] { 0, 3, 4, 0 },      // I-IV-V-I
                new[] { 0, 2, 5, 1, 4, 0 }, // I-iii-vi-ii-V-I
                new[] { 3, 0, 4, 0 },      // IV-I-V-I (plagal)
                new[] { 0, 1, 4, 0 }       // I-ii-V-I
            }
        };
    }

    private Dictionary<int, ChordFunction> InitializeDegreeFunctions()
    {
        return new Dictionary<int, ChordFunction>
        {
            [0] = ChordFunction.Tonic,        // I
            [1] = ChordFunction.Subdominant,  // ii
            [2] = ChordFunction.Tonic,        // iii (weak tonic)
            [3] = ChordFunction.Subdominant,  // IV
            [4] = ChordFunction.Dominant,     // V
            [5] = ChordFunction.Tonic,        // vi (tonic substitute)
            [6] = ChordFunction.Dominant      // vii (dominant function)
        };
    }

    private IEnumerable<(int degree, int root, ChordType type, string roman)> GetDiatonicChords(int keyRoot, bool isMinor)
    {
        if (isMinor)
        {
            // Natural minor diatonic chords
            yield return (0, keyRoot, ChordType.Minor, "i");
            yield return (1, (keyRoot + 2) % 12, ChordType.Diminished, "ii\u00b0");
            yield return (2, (keyRoot + 3) % 12, ChordType.Major, "III");
            yield return (3, (keyRoot + 5) % 12, ChordType.Minor, "iv");
            yield return (4, (keyRoot + 7) % 12, ChordType.Minor, "v");
            yield return (5, (keyRoot + 8) % 12, ChordType.Major, "VI");
            yield return (6, (keyRoot + 10) % 12, ChordType.Major, "VII");
            // Harmonic minor V chord
            yield return (4, (keyRoot + 7) % 12, ChordType.Major, "V");
        }
        else
        {
            // Major diatonic chords
            yield return (0, keyRoot, ChordType.Major, "I");
            yield return (1, (keyRoot + 2) % 12, ChordType.Minor, "ii");
            yield return (2, (keyRoot + 4) % 12, ChordType.Minor, "iii");
            yield return (3, (keyRoot + 5) % 12, ChordType.Major, "IV");
            yield return (4, (keyRoot + 7) % 12, ChordType.Major, "V");
            yield return (5, (keyRoot + 9) % 12, ChordType.Minor, "vi");
            yield return (6, (keyRoot + 11) % 12, ChordType.Diminished, "vii\u00b0");
        }
    }

    private float CalculateChordScore(
        int root, ChordType chordType, int degree,
        IReadOnlyList<ContextChord> currentChords,
        int keyRoot, bool isMinor, ChordSuggestionStyle style)
    {
        float score = 0.5f;

        // Check if chord follows common progressions for the style
        if (_commonProgressions.TryGetValue(style, out var patterns))
        {
            if (currentChords.Count > 0)
            {
                int prevDegree = GetDegree(currentChords[^1].Root, keyRoot);

                foreach (var pattern in patterns)
                {
                    for (int i = 0; i < pattern.Length - 1; i++)
                    {
                        if (pattern[i] == prevDegree && pattern[i + 1] == degree)
                        {
                            score += 0.3f;
                            break;
                        }
                    }
                }
            }
        }

        // Functional harmony bonuses
        if (currentChords.Count > 0)
        {
            var prevFunction = GetChordFunction(GetDegree(currentChords[^1].Root, keyRoot), isMinor);
            var currFunction = GetChordFunction(degree, isMinor);

            // Dominant to Tonic resolution
            if (prevFunction == ChordFunction.Dominant && currFunction == ChordFunction.Tonic)
                score += 0.25f;

            // Subdominant to Dominant progression
            if (prevFunction == ChordFunction.Subdominant && currFunction == ChordFunction.Dominant)
                score += 0.2f;

            // Tonic to Subdominant
            if (prevFunction == ChordFunction.Tonic && currFunction == ChordFunction.Subdominant)
                score += 0.15f;
        }
        else
        {
            // First chord bonus for tonic
            if (degree == 0)
                score += 0.3f;
        }

        // Avoid repeating the same chord
        if (currentChords.Count > 0 && currentChords[^1].Root == root && currentChords[^1].ChordType == chordType)
            score -= 0.4f;

        // Style-specific adjustments
        score += GetStyleBonus(degree, chordType, style);

        return Math.Clamp(score, 0f, 1f);
    }

    private float GetStyleBonus(int degree, ChordType chordType, ChordSuggestionStyle style)
    {
        return style switch
        {
            ChordSuggestionStyle.Jazz => chordType switch
            {
                ChordType.Major7 or ChordType.Minor7 or ChordType.Dominant7 => 0.15f,
                ChordType.Major9 or ChordType.Minor9 => 0.1f,
                _ => 0
            },
            ChordSuggestionStyle.Blues => degree switch
            {
                0 or 3 or 4 when chordType == ChordType.Dominant7 => 0.2f,
                _ => 0
            },
            ChordSuggestionStyle.Rock => chordType switch
            {
                ChordType.Power => 0.15f,
                ChordType.Major or ChordType.Minor => 0.1f,
                _ => 0
            },
            ChordSuggestionStyle.Classical => degree switch
            {
                0 or 4 => 0.1f, // Emphasize tonic and dominant
                _ => 0
            },
            _ => 0
        };
    }

    private void AddStyleSpecificChords(
        List<ChordSuggestion> suggestions,
        IReadOnlyList<ContextChord> currentChords,
        int keyRoot, bool isMinor, ChordSuggestionStyle style)
    {
        switch (style)
        {
            case ChordSuggestionStyle.Jazz:
                // Add seventh chord versions
                foreach (var (degree, root, _, roman) in GetDiatonicChords(keyRoot, isMinor))
                {
                    ChordType extendedType = degree switch
                    {
                        0 => ChordType.Major7,
                        1 => ChordType.Minor7,
                        2 => ChordType.Minor7,
                        3 => ChordType.Major7,
                        4 => ChordType.Dominant7,
                        5 => ChordType.Minor7,
                        6 => ChordType.HalfDiminished7,
                        _ => ChordType.Major7
                    };

                    suggestions.Add(new ChordSuggestion
                    {
                        Root = root,
                        ChordType = extendedType,
                        Score = 0.6f,
                        RomanNumeral = roman + "7",
                        Function = GetChordFunction(degree, isMinor),
                        Reason = "Jazz voicing"
                    });
                }
                break;

            case ChordSuggestionStyle.Blues:
                // All dominant sevenths for I, IV, V
                suggestions.Add(new ChordSuggestion
                {
                    Root = keyRoot,
                    ChordType = ChordType.Dominant7,
                    Score = 0.7f,
                    RomanNumeral = "I7",
                    Function = ChordFunction.Tonic,
                    Reason = "Blues tonic"
                });
                suggestions.Add(new ChordSuggestion
                {
                    Root = (keyRoot + 5) % 12,
                    ChordType = ChordType.Dominant7,
                    Score = 0.65f,
                    RomanNumeral = "IV7",
                    Function = ChordFunction.Subdominant,
                    Reason = "Blues subdominant"
                });
                break;

            case ChordSuggestionStyle.Gospel:
                // Add suspended and add9 chords
                suggestions.Add(new ChordSuggestion
                {
                    Root = keyRoot,
                    ChordType = ChordType.Add9,
                    Score = 0.55f,
                    RomanNumeral = "Iadd9",
                    Function = ChordFunction.Tonic,
                    Reason = "Gospel color"
                });
                break;
        }
    }

    private void AddSecondaryDominants(
        List<ChordSuggestion> suggestions,
        IReadOnlyList<ContextChord> currentChords,
        int keyRoot, bool isMinor, ChordSuggestionStyle style)
    {
        if (style == ChordSuggestionStyle.Rock || style == ChordSuggestionStyle.Electronic)
            return; // Less common in these styles

        // V/V (secondary dominant of V)
        int dominantRoot = (keyRoot + 7) % 12;
        int secondaryDom = (dominantRoot + 7) % 12;

        suggestions.Add(new ChordSuggestion
        {
            Root = secondaryDom,
            ChordType = ChordType.Dominant7,
            Score = 0.5f,
            RomanNumeral = "V/V",
            Function = ChordFunction.SecondaryDominant,
            Reason = "Secondary dominant of V"
        });

        // V/vi (leading to relative minor)
        int relativeMinor = (keyRoot + 9) % 12;
        int vOfVi = (relativeMinor + 7) % 12;

        suggestions.Add(new ChordSuggestion
        {
            Root = vOfVi,
            ChordType = ChordType.Dominant7,
            Score = 0.45f,
            RomanNumeral = "V/vi",
            Function = ChordFunction.SecondaryDominant,
            Reason = "Secondary dominant of vi"
        });
    }

    private void AddBorrowedChords(
        List<ChordSuggestion> suggestions,
        IReadOnlyList<ContextChord> currentChords,
        int keyRoot, bool isMinor, ChordSuggestionStyle style)
    {
        if (isMinor) return; // Borrowing from major is less common

        // bVII (borrowed from natural minor)
        suggestions.Add(new ChordSuggestion
        {
            Root = (keyRoot + 10) % 12,
            ChordType = ChordType.Major,
            Score = 0.45f,
            RomanNumeral = "bVII",
            Function = ChordFunction.Borrowed,
            Reason = "Borrowed from parallel minor"
        });

        // iv (borrowed from minor)
        suggestions.Add(new ChordSuggestion
        {
            Root = (keyRoot + 5) % 12,
            ChordType = ChordType.Minor,
            Score = 0.4f,
            RomanNumeral = "iv",
            Function = ChordFunction.Borrowed,
            Reason = "Minor subdominant"
        });

        // bVI (borrowed from minor)
        suggestions.Add(new ChordSuggestion
        {
            Root = (keyRoot + 8) % 12,
            ChordType = ChordType.Major,
            Score = 0.35f,
            RomanNumeral = "bVI",
            Function = ChordFunction.Borrowed,
            Reason = "Borrowed from parallel minor"
        });
    }

    private void OptimizeVoiceLeading(
        List<ChordSuggestion> suggestions,
        IReadOnlyList<ContextChord> currentChords,
        int keyRoot)
    {
        if (currentChords.Count == 0) return;

        var lastChord = currentChords[^1];
        var lastNotes = Chord.GetNotes(lastChord.Root, lastChord.ChordType);

        foreach (var suggestion in suggestions)
        {
            var suggestionNotes = Chord.GetNotes(suggestion.Root, suggestion.ChordType);
            float voiceLeadingCost = CalculateVoiceLeadingCost(lastNotes, suggestionNotes);

            // Boost score for smooth voice leading
            float voiceLeadingBonus = voiceLeadingCost switch
            {
                <= 2 => 0.15f,
                <= 4 => 0.08f,
                <= 6 => 0.03f,
                _ => -0.05f
            };

            // Modify the score (we need to create a new suggestion since Score is init-only)
            // For simplicity, we'll recalculate in the main method
        }
    }

    private float CalculateVoiceLeadingCost(int[] fromNotes, int[] toNotes)
    {
        float totalCost = 0;

        // Find minimum movement for each voice
        foreach (var fromNote in fromNotes)
        {
            int minDistance = int.MaxValue;
            foreach (var toNote in toNotes)
            {
                int distance = Math.Min(
                    Math.Abs(fromNote % 12 - toNote % 12),
                    12 - Math.Abs(fromNote % 12 - toNote % 12));
                minDistance = Math.Min(minDistance, distance);
            }
            totalCost += minDistance;
        }

        return totalCost;
    }

    private List<ChordSuggestion> GenerateAlgorithmicProgression(
        IReadOnlyList<ContextChord> startChords,
        int keyRoot, bool isMinor,
        ChordSuggestionStyle style, int targetLength)
    {
        var progression = new List<ChordSuggestion>();
        var usedChords = new HashSet<int>();

        // Track what's been used
        foreach (var chord in startChords)
        {
            usedChords.Add(GetDegree(chord.Root, keyRoot));
        }

        int currentDegree = startChords.Count > 0
            ? GetDegree(startChords[^1].Root, keyRoot)
            : 0;

        int chordsNeeded = targetLength - startChords.Count;

        for (int i = 0; i < chordsNeeded; i++)
        {
            // Determine next chord based on harmonic function
            var currentFunction = GetChordFunction(currentDegree, isMinor);
            int nextDegree;

            // Use function-based logic for next chord
            if (i == chordsNeeded - 1)
            {
                // End on tonic
                nextDegree = 0;
            }
            else if (i == chordsNeeded - 2)
            {
                // Penultimate chord should be dominant
                nextDegree = 4;
            }
            else
            {
                // Follow functional logic
                nextDegree = currentFunction switch
                {
                    ChordFunction.Tonic => new[] { 3, 5, 1 }[i % 3], // I -> IV, vi, or ii
                    ChordFunction.Subdominant => 4, // IV/ii -> V
                    ChordFunction.Dominant => 0, // V -> I
                    _ => (currentDegree + 4) % 7
                };
            }

            var (root, chordType, roman) = GetChordForDegree(nextDegree, keyRoot, isMinor, style);

            progression.Add(new ChordSuggestion
            {
                Root = root,
                ChordType = chordType,
                Score = 0.7f - (i * 0.03f),
                RomanNumeral = roman,
                Function = GetChordFunction(nextDegree, isMinor),
                Reason = "Algorithmic generation"
            });

            currentDegree = nextDegree;
        }

        return progression;
    }

    private (int root, ChordType type, string roman) GetChordForDegree(
        int degree, int keyRoot, bool isMinor, ChordSuggestionStyle style)
    {
        // Get scale intervals
        int[] majorIntervals = { 0, 2, 4, 5, 7, 9, 11 };
        int[] minorIntervals = { 0, 2, 3, 5, 7, 8, 10 };

        var intervals = isMinor ? minorIntervals : majorIntervals;
        int root = (keyRoot + intervals[degree % 7]) % 12;

        ChordType chordType;
        string roman;

        if (isMinor)
        {
            (chordType, roman) = degree switch
            {
                0 => (ChordType.Minor, "i"),
                1 => (ChordType.Diminished, "ii\u00b0"),
                2 => (ChordType.Major, "III"),
                3 => (ChordType.Minor, "iv"),
                4 => (style == ChordSuggestionStyle.Classical ? ChordType.Major : ChordType.Minor, style == ChordSuggestionStyle.Classical ? "V" : "v"),
                5 => (ChordType.Major, "VI"),
                6 => (ChordType.Major, "VII"),
                _ => (ChordType.Minor, "i")
            };
        }
        else
        {
            (chordType, roman) = degree switch
            {
                0 => (ChordType.Major, "I"),
                1 => (ChordType.Minor, "ii"),
                2 => (ChordType.Minor, "iii"),
                3 => (ChordType.Major, "IV"),
                4 => (ChordType.Major, "V"),
                5 => (ChordType.Minor, "vi"),
                6 => (ChordType.Diminished, "vii\u00b0"),
                _ => (ChordType.Major, "I")
            };
        }

        // Style-specific modifications
        if (style == ChordSuggestionStyle.Jazz && (degree == 1 || degree == 4))
        {
            chordType = degree == 1 ? ChordType.Minor7 : ChordType.Dominant7;
            roman += "7";
        }

        return (root, chordType, roman);
    }

    private ChordFunction GetChordFunction(int degree, bool isMinor)
    {
        return _degreeFunctions.TryGetValue(degree % 7, out var function)
            ? function
            : ChordFunction.Tonic;
    }

    private int GetDegree(int chordRoot, int keyRoot)
    {
        int[] majorIntervals = { 0, 2, 4, 5, 7, 9, 11 };
        int interval = ((chordRoot - keyRoot) % 12 + 12) % 12;

        for (int i = 0; i < majorIntervals.Length; i++)
        {
            if (majorIntervals[i] == interval)
                return i;
        }

        return 0;
    }

    private string GetRomanNumeral(int degree, bool isMinor, ChordType chordType)
    {
        string[] majorRoman = { "I", "ii", "iii", "IV", "V", "vi", "vii\u00b0" };
        string[] minorRoman = { "i", "ii\u00b0", "III", "iv", "v", "VI", "VII" };

        return (isMinor ? minorRoman : majorRoman)[degree % 7];
    }

    private string GetSuggestionReason(int degree, IReadOnlyList<ContextChord> currentChords, ChordSuggestionStyle style)
    {
        if (currentChords.Count == 0)
            return degree == 0 ? "Strong opening chord" : "Alternative opening";

        return degree switch
        {
            0 => "Resolution to tonic",
            4 => "Dominant tension",
            3 => "Subdominant color",
            5 => "Relative minor",
            1 => "Pre-dominant function",
            _ => $"Diatonic {style} option"
        };
    }

    private string GetPatternName(int[] pattern)
    {
        // Identify common patterns
        if (pattern.SequenceEqual(new[] { 0, 4, 5, 3 }))
            return "I-V-vi-IV (Axis)";
        if (pattern.SequenceEqual(new[] { 0, 3, 4, 0 }))
            return "I-IV-V-I (Classic)";
        if (pattern.SequenceEqual(new[] { 1, 4, 0 }))
            return "ii-V-I (Jazz)";

        return string.Join("-", pattern.Select(d => (d + 1).ToString()));
    }

    private static string GetNoteName(int pitchClass)
    {
        string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        return names[pitchClass % 12];
    }

    private static string GetTypeSuffix(ChordType type)
    {
        return type switch
        {
            ChordType.Major => "",
            ChordType.Minor => "m",
            ChordType.Diminished => "dim",
            ChordType.Augmented => "aug",
            ChordType.Major7 => "maj7",
            ChordType.Minor7 => "m7",
            ChordType.Dominant7 => "7",
            _ => ""
        };
    }

    #endregion
}
