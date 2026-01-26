//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Automated mixing assistant providing gain staging, panning, EQ suggestions, compression recommendations, and mix analysis.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MusicEngine.Core.Utilities;

/// <summary>
/// Genre presets for mixdown styling.
/// </summary>
public enum MixdownGenre
{
    /// <summary>Pop music style with clear vocals and polished sound.</summary>
    Pop,
    /// <summary>Rock music style with punchy drums and guitars.</summary>
    Rock,
    /// <summary>EDM style with heavy bass and compressed dynamics.</summary>
    EDM,
    /// <summary>Hip-Hop style with prominent bass and vocals.</summary>
    HipHop,
    /// <summary>Jazz style with natural dynamics and wide stereo.</summary>
    Jazz,
    /// <summary>Classical style with maximum dynamic range.</summary>
    Classical
}

/// <summary>
/// Track category for grouping suggestions.
/// </summary>
public enum MixdownTrackCategory
{
    /// <summary>Drum tracks (kick, snare, hats, etc.).</summary>
    Drums,
    /// <summary>Bass instruments.</summary>
    Bass,
    /// <summary>Lead vocals.</summary>
    Vocals,
    /// <summary>Background/harmony vocals.</summary>
    BackingVocals,
    /// <summary>Guitar tracks.</summary>
    Guitars,
    /// <summary>Keyboard/piano tracks.</summary>
    Keys,
    /// <summary>Synthesizer tracks.</summary>
    Synths,
    /// <summary>Strings/orchestral tracks.</summary>
    Strings,
    /// <summary>Brass/wind instruments.</summary>
    Brass,
    /// <summary>Ambient/pad sounds.</summary>
    Pads,
    /// <summary>Sound effects and samples.</summary>
    SFX,
    /// <summary>Unknown/other tracks.</summary>
    Other
}

/// <summary>
/// State snapshot for a single track used for undo functionality.
/// </summary>
public class TrackMixState
{
    /// <summary>Gets or sets the track identifier.</summary>
    public string TrackId { get; set; } = string.Empty;

    /// <summary>Gets or sets the track name.</summary>
    public string TrackName { get; set; } = string.Empty;

    /// <summary>Gets or sets the volume/gain in dB.</summary>
    public float GainDb { get; set; }

    /// <summary>Gets or sets the pan position (-1 to 1).</summary>
    public float Pan { get; set; }

    /// <summary>Gets or sets the mute state.</summary>
    public bool Muted { get; set; }

    /// <summary>Gets or sets the solo state.</summary>
    public bool Soloed { get; set; }

    /// <summary>Gets or sets the stereo width (0 to 1).</summary>
    public float StereoWidth { get; set; } = 1.0f;

    /// <summary>Gets or sets the low-cut frequency in Hz (0 = disabled).</summary>
    public float LowCutHz { get; set; }

    /// <summary>Gets or sets the high-cut frequency in Hz (0 = disabled).</summary>
    public float HighCutHz { get; set; }

    /// <summary>Gets or sets the assigned category.</summary>
    public MixdownTrackCategory Category { get; set; } = MixdownTrackCategory.Other;

    /// <summary>Creates a deep copy of this state.</summary>
    public TrackMixState Clone()
    {
        return new TrackMixState
        {
            TrackId = TrackId,
            TrackName = TrackName,
            GainDb = GainDb,
            Pan = Pan,
            Muted = Muted,
            Soloed = Soloed,
            StereoWidth = StereoWidth,
            LowCutHz = LowCutHz,
            HighCutHz = HighCutHz,
            Category = Category
        };
    }
}

/// <summary>
/// Complete mix state snapshot for undo functionality.
/// </summary>
public class MixStateSnapshot
{
    /// <summary>Gets or sets the snapshot timestamp.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the snapshot description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the track states.</summary>
    public List<TrackMixState> TrackStates { get; set; } = new();

    /// <summary>Gets or sets the master gain in dB.</summary>
    public float MasterGainDb { get; set; }

    /// <summary>Gets or sets the target LUFS.</summary>
    public float TargetLufs { get; set; } = -14f;
}

/// <summary>
/// Suggestion for EQ frequency slot allocation.
/// </summary>
public class FrequencySlotSuggestion
{
    /// <summary>Gets or sets the track name.</summary>
    public string TrackName { get; set; } = string.Empty;

    /// <summary>Gets or sets the suggested cut frequency in Hz.</summary>
    public float CutFrequencyHz { get; set; }

    /// <summary>Gets or sets the suggested cut amount in dB (negative).</summary>
    public float CutAmountDb { get; set; }

    /// <summary>Gets or sets the Q factor for the cut.</summary>
    public float Q { get; set; } = 1.0f;

    /// <summary>Gets or sets the reason for this suggestion.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets the conflicting track name (if masking detected).</summary>
    public string? ConflictingTrack { get; set; }

    /// <summary>Gets or sets the severity (0-1).</summary>
    public float Severity { get; set; }
}

/// <summary>
/// Compression recommendation for a track.
/// </summary>
public class CompressionRecommendation
{
    /// <summary>Gets or sets the track name.</summary>
    public string TrackName { get; set; } = string.Empty;

    /// <summary>Gets or sets the track category.</summary>
    public MixdownTrackCategory Category { get; set; }

    /// <summary>Gets or sets the suggested threshold in dB.</summary>
    public float ThresholdDb { get; set; }

    /// <summary>Gets or sets the suggested ratio.</summary>
    public float Ratio { get; set; }

    /// <summary>Gets or sets the suggested attack time in ms.</summary>
    public float AttackMs { get; set; }

    /// <summary>Gets or sets the suggested release time in ms.</summary>
    public float ReleaseMs { get; set; }

    /// <summary>Gets or sets the suggested knee width in dB.</summary>
    public float KneeDb { get; set; }

    /// <summary>Gets or sets the suggested makeup gain in dB.</summary>
    public float MakeupGainDb { get; set; }

    /// <summary>Gets or sets whether parallel compression is recommended.</summary>
    public bool ParallelRecommended { get; set; }

    /// <summary>Gets or sets the parallel mix amount (0-1).</summary>
    public float ParallelMix { get; set; }

    /// <summary>Gets or sets additional notes.</summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Reverb/delay send level suggestion.
/// </summary>
public class SendLevelSuggestion
{
    /// <summary>Gets or sets the track name.</summary>
    public string TrackName { get; set; } = string.Empty;

    /// <summary>Gets or sets the bus name (e.g., "Reverb", "Delay").</summary>
    public string BusName { get; set; } = string.Empty;

    /// <summary>Gets or sets the suggested send level (0-1).</summary>
    public float SendLevel { get; set; }

    /// <summary>Gets or sets whether pre-fader send is recommended.</summary>
    public bool PreFader { get; set; }

    /// <summary>Gets or sets the reason for this suggestion.</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Bus routing suggestion.
/// </summary>
public class BusRoutingSuggestion
{
    /// <summary>Gets or sets the suggested bus name.</summary>
    public string BusName { get; set; } = string.Empty;

    /// <summary>Gets or sets the bus type (Group, Aux).</summary>
    public string BusType { get; set; } = "Group";

    /// <summary>Gets or sets the tracks that should route to this bus.</summary>
    public List<string> TrackNames { get; set; } = new();

    /// <summary>Gets or sets the suggested bus color (hex).</summary>
    public string Color { get; set; } = "#4A9EFF";

    /// <summary>Gets or sets suggested processing for this bus.</summary>
    public string SuggestedProcessing { get; set; } = string.Empty;
}

/// <summary>
/// Track grouping suggestion for organization.
/// </summary>
public class TrackGroupingSuggestion
{
    /// <summary>Gets or sets the group name.</summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>Gets or sets the category.</summary>
    public MixdownTrackCategory Category { get; set; }

    /// <summary>Gets or sets the tracks in this group.</summary>
    public List<string> TrackNames { get; set; } = new();

    /// <summary>Gets or sets the suggested group color.</summary>
    public string Color { get; set; } = "#4A9EFF";
}

/// <summary>
/// Low-end management recommendation.
/// </summary>
public class LowEndManagementSuggestion
{
    /// <summary>Gets or sets the track name.</summary>
    public string TrackName { get; set; } = string.Empty;

    /// <summary>Gets or sets whether mono below threshold is recommended.</summary>
    public bool MonoBelowFrequency { get; set; }

    /// <summary>Gets or sets the frequency threshold for mono summing (Hz).</summary>
    public float MonoFrequencyHz { get; set; } = 120f;

    /// <summary>Gets or sets the suggested high-pass frequency (Hz).</summary>
    public float HighPassHz { get; set; }

    /// <summary>Gets or sets the reason for this suggestion.</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Problem frequency identified in the mix.
/// </summary>
public class ProblemFrequency
{
    /// <summary>Gets or sets the center frequency in Hz.</summary>
    public float FrequencyHz { get; set; }

    /// <summary>Gets or sets the bandwidth in Hz.</summary>
    public float BandwidthHz { get; set; }

    /// <summary>Gets or sets the severity (0-1).</summary>
    public float Severity { get; set; }

    /// <summary>Gets or sets the problem type (resonance, buildup, masking, etc.).</summary>
    public string ProblemType { get; set; } = string.Empty;

    /// <summary>Gets or sets the tracks contributing to this problem.</summary>
    public List<string> ContributingTracks { get; set; } = new();

    /// <summary>Gets or sets the suggested action.</summary>
    public string SuggestedAction { get; set; } = string.Empty;
}

/// <summary>
/// Dynamic range analysis result.
/// </summary>
public class DynamicRangeAnalysis
{
    /// <summary>Gets or sets the track name (or "Master" for overall).</summary>
    public string TrackName { get; set; } = string.Empty;

    /// <summary>Gets or sets the peak level in dB.</summary>
    public float PeakDb { get; set; }

    /// <summary>Gets or sets the RMS level in dB.</summary>
    public float RmsDb { get; set; }

    /// <summary>Gets or sets the LUFS integrated loudness.</summary>
    public float LufsIntegrated { get; set; }

    /// <summary>Gets or sets the crest factor in dB.</summary>
    public float CrestFactorDb { get; set; }

    /// <summary>Gets or sets the dynamic range in dB.</summary>
    public float DynamicRangeDb { get; set; }

    /// <summary>Gets or sets whether the track is over-compressed.</summary>
    public bool IsOverCompressed { get; set; }

    /// <summary>Gets or sets whether the track has too much dynamic range.</summary>
    public bool NeedsMoreCompression { get; set; }

    /// <summary>Gets or sets analysis notes.</summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Complete mix analysis report.
/// </summary>
public class MixAnalysisReport
{
    /// <summary>Gets or sets the report generation timestamp.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the genre preset used.</summary>
    public MixdownGenre Genre { get; set; }

    /// <summary>Gets or sets the overall mix score (0-100).</summary>
    public float OverallScore { get; set; }

    /// <summary>Gets or sets the headroom in dB (peak below 0).</summary>
    public float HeadroomDb { get; set; }

    /// <summary>Gets or sets the integrated loudness in LUFS.</summary>
    public float LufsIntegrated { get; set; }

    /// <summary>Gets or sets the target LUFS.</summary>
    public float TargetLufs { get; set; }

    /// <summary>Gets or sets the loudness deviation from target in LU.</summary>
    public float LoudnessDeviationLu { get; set; }

    /// <summary>Gets or sets the stereo correlation (-1 to 1).</summary>
    public float StereoCorrelation { get; set; }

    /// <summary>Gets or sets the spectral balance score (0-100).</summary>
    public float SpectralBalanceScore { get; set; }

    /// <summary>Gets or sets the dynamic range score (0-100).</summary>
    public float DynamicRangeScore { get; set; }

    /// <summary>Gets or sets the frequency collision score (0-100, higher is better).</summary>
    public float FrequencyCollisionScore { get; set; }

    /// <summary>Gets or sets the track analyses.</summary>
    public List<DynamicRangeAnalysis> TrackAnalyses { get; set; } = new();

    /// <summary>Gets or sets the problem frequencies.</summary>
    public List<ProblemFrequency> ProblemFrequencies { get; set; } = new();

    /// <summary>Gets or sets the summary text.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets detailed recommendations.</summary>
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Result of reference track comparison.
/// </summary>
public class ReferenceComparisonResult
{
    /// <summary>Gets or sets the reference track name/path.</summary>
    public string ReferenceName { get; set; } = string.Empty;

    /// <summary>Gets or sets the loudness difference in LU.</summary>
    public float LoudnessDifferenceLu { get; set; }

    /// <summary>Gets or sets band-by-band comparison (8 bands).</summary>
    public float[] BandDifferencesDb { get; set; } = new float[8];

    /// <summary>Gets or sets the stereo width difference.</summary>
    public float StereoWidthDifference { get; set; }

    /// <summary>Gets or sets the dynamic range difference in dB.</summary>
    public float DynamicRangeDifferenceDb { get; set; }

    /// <summary>Gets or sets the overall similarity score (0-100).</summary>
    public float SimilarityScore { get; set; }

    /// <summary>Gets or sets specific recommendations to match reference.</summary>
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Automated mixing assistant providing comprehensive mix analysis and suggestions.
/// Supports auto-gain staging, panning, EQ suggestions, compression recommendations,
/// headroom management, and genre-based presets.
/// </summary>
public class MixdownAssistant
{
    private readonly List<MixStateSnapshot> _undoStack = new();
    private readonly List<MixStateSnapshot> _redoStack = new();
    private readonly Dictionary<MixdownGenre, GenrePreset> _genrePresets;
    private readonly Dictionary<MixdownTrackCategory, float[]> _categorySpectralProfiles;
    private readonly Dictionary<MixdownTrackCategory, CompressionRecommendation> _categoryCompressionDefaults;

    private int _sampleRate;
    private int _channels;
    private MixdownGenre _currentGenre = MixdownGenre.Pop;

    /// <summary>
    /// Gets or sets the target LUFS for loudness targeting.
    /// </summary>
    public float TargetLufs { get; set; } = -14f;

    /// <summary>
    /// Gets or sets the minimum headroom in dB below 0 dBFS.
    /// </summary>
    public float MinHeadroomDb { get; set; } = 1f;

    /// <summary>
    /// Gets or sets the mono bass frequency threshold in Hz.
    /// </summary>
    public float MonoBassFrequencyHz { get; set; } = 120f;

    /// <summary>
    /// Gets or sets the current genre preset.
    /// </summary>
    public MixdownGenre CurrentGenre
    {
        get => _currentGenre;
        set => _currentGenre = value;
    }

    /// <summary>
    /// Gets whether undo is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Gets whether redo is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Event raised when mix analysis is updated.
    /// </summary>
    public event EventHandler<MixAnalysisReport>? AnalysisUpdated;

    /// <summary>
    /// Creates a new mixdown assistant.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate (default: 44100).</param>
    /// <param name="channels">Number of audio channels (default: 2).</param>
    public MixdownAssistant(int sampleRate = 44100, int channels = 2)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _genrePresets = InitializeGenrePresets();
        _categorySpectralProfiles = InitializeCategoryProfiles();
        _categoryCompressionDefaults = InitializeCompressionDefaults();
    }

    /// <summary>
    /// Captures the current mix state for undo functionality.
    /// </summary>
    /// <param name="trackStates">Current track states.</param>
    /// <param name="masterGainDb">Current master gain.</param>
    /// <param name="description">Description of the state.</param>
    public void CaptureState(List<TrackMixState> trackStates, float masterGainDb, string description)
    {
        var snapshot = new MixStateSnapshot
        {
            Description = description,
            MasterGainDb = masterGainDb,
            TargetLufs = TargetLufs,
            TrackStates = trackStates.Select(t => t.Clone()).ToList()
        };

        _undoStack.Add(snapshot);
        _redoStack.Clear();

        // Limit undo stack size
        while (_undoStack.Count > 50)
        {
            _undoStack.RemoveAt(0);
        }
    }

    /// <summary>
    /// Reverts to the previous mix state.
    /// </summary>
    /// <returns>The previous state, or null if undo stack is empty.</returns>
    public MixStateSnapshot? Undo()
    {
        if (_undoStack.Count == 0)
            return null;

        var state = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        _redoStack.Add(state);

        return _undoStack.Count > 0 ? _undoStack[^1] : null;
    }

    /// <summary>
    /// Reapplies a previously undone state.
    /// </summary>
    /// <returns>The reapplied state, or null if redo stack is empty.</returns>
    public MixStateSnapshot? Redo()
    {
        if (_redoStack.Count == 0)
            return null;

        var state = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        _undoStack.Add(state);

        return state;
    }

    /// <summary>
    /// Undoes all changes and returns to the initial state.
    /// </summary>
    /// <returns>The initial state, or null if no history exists.</returns>
    public MixStateSnapshot? UndoAll()
    {
        if (_undoStack.Count == 0)
            return null;

        // Move all states to redo stack
        while (_undoStack.Count > 1)
        {
            var state = _undoStack[^1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            _redoStack.Add(state);
        }

        return _undoStack.Count > 0 ? _undoStack[0] : null;
    }

    /// <summary>
    /// Performs auto-gain staging on all tracks to achieve optimal levels.
    /// </summary>
    /// <param name="trackAnalyses">Analysis data for each track.</param>
    /// <returns>Dictionary of track names to suggested gain adjustments in dB.</returns>
    public Dictionary<string, float> AutoGainStaging(List<DynamicRangeAnalysis> trackAnalyses)
    {
        var adjustments = new Dictionary<string, float>();
        var preset = _genrePresets[_currentGenre];

        foreach (var analysis in trackAnalyses)
        {
            // Target -18 dBFS RMS for gain staging (K-20 style)
            float targetRms = preset.TargetRmsDb;
            float currentRms = analysis.RmsDb;

            float adjustment = targetRms - currentRms;

            // Clamp adjustment to reasonable range
            adjustment = Math.Clamp(adjustment, -24f, 24f);

            adjustments[analysis.TrackName] = adjustment;
        }

        return adjustments;
    }

    /// <summary>
    /// Calculates automatic pan positions to spread tracks across the stereo field.
    /// </summary>
    /// <param name="trackStates">Current track states with categories.</param>
    /// <returns>Dictionary of track names to suggested pan positions (-1 to 1).</returns>
    public Dictionary<string, float> AutoPan(List<TrackMixState> trackStates)
    {
        var panPositions = new Dictionary<string, float>();
        var preset = _genrePresets[_currentGenre];

        // Group tracks by category
        var groupedTracks = trackStates.GroupBy(t => t.Category).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var kvp in groupedTracks)
        {
            var category = kvp.Key;
            var tracks = kvp.Value;

            // Get category pan rules
            var panRule = GetCategoryPanRule(category, preset);

            if (tracks.Count == 1)
            {
                panPositions[tracks[0].TrackName] = panRule.DefaultPan;
            }
            else
            {
                // Spread multiple tracks of same category
                for (int i = 0; i < tracks.Count; i++)
                {
                    float spreadPosition;
                    if (panRule.KeepCentered)
                    {
                        spreadPosition = 0f;
                    }
                    else
                    {
                        // Alternate left/right with spread
                        float basePosition = panRule.DefaultPan;
                        float spread = panRule.SpreadWidth;
                        float offset = (i - (tracks.Count - 1) / 2f) * (spread / Math.Max(1, tracks.Count - 1));
                        spreadPosition = Math.Clamp(basePosition + offset, -1f, 1f);
                    }

                    panPositions[tracks[i].TrackName] = spreadPosition;
                }
            }
        }

        return panPositions;
    }

    /// <summary>
    /// Suggests EQ cuts to reduce frequency masking between tracks.
    /// </summary>
    /// <param name="trackAnalyses">Per-track spectral analyses.</param>
    /// <param name="trackCategories">Category assignments for each track.</param>
    /// <returns>List of frequency slot suggestions.</returns>
    public List<FrequencySlotSuggestion> SuggestFrequencySlots(
        Dictionary<string, float[]> trackAnalyses,
        Dictionary<string, MixdownTrackCategory> trackCategories)
    {
        var suggestions = new List<FrequencySlotSuggestion>();

        // Define frequency bands
        float[] bandCenters = { 60f, 150f, 350f, 800f, 2000f, 4000f, 8000f, 14000f };
        string[] bandNames = { "Sub", "Bass", "Low-Mid", "Mid", "High-Mid", "Presence", "Brilliance", "Air" };

        var trackNames = trackAnalyses.Keys.ToList();

        // Find masking issues
        for (int i = 0; i < trackNames.Count; i++)
        {
            var track1 = trackNames[i];
            var spectrum1 = trackAnalyses[track1];
            var category1 = trackCategories.GetValueOrDefault(track1, MixdownTrackCategory.Other);

            for (int j = i + 1; j < trackNames.Count; j++)
            {
                var track2 = trackNames[j];
                var spectrum2 = trackAnalyses[track2];
                var category2 = trackCategories.GetValueOrDefault(track2, MixdownTrackCategory.Other);

                // Check each band for collision
                for (int band = 0; band < Math.Min(spectrum1.Length, spectrum2.Length); band++)
                {
                    float energy1 = spectrum1[band];
                    float energy2 = spectrum2[band];

                    // Both tracks have significant energy in this band
                    if (energy1 > 0.4f && energy2 > 0.4f)
                    {
                        float collisionSeverity = (energy1 + energy2) / 2f;

                        // Determine which track should be cut
                        var (trackToCut, otherTrack) = DetermineTrackToCut(
                            track1, category1, energy1,
                            track2, category2, energy2,
                            band);

                        // Calculate cut amount based on severity
                        float cutAmount = -Math.Min(collisionSeverity * 4f, 6f);

                        suggestions.Add(new FrequencySlotSuggestion
                        {
                            TrackName = trackToCut,
                            CutFrequencyHz = bandCenters[Math.Min(band, bandCenters.Length - 1)],
                            CutAmountDb = cutAmount,
                            Q = band < 2 ? 0.7f : 1.5f, // Wider Q for low frequencies
                            Reason = $"Reduce masking with {otherTrack} in {bandNames[Math.Min(band, bandNames.Length - 1)]} range",
                            ConflictingTrack = otherTrack,
                            Severity = collisionSeverity
                        });
                    }
                }
            }
        }

        // Sort by severity and remove duplicates
        return suggestions
            .OrderByDescending(s => s.Severity)
            .GroupBy(s => new { s.TrackName, Band = (int)(s.CutFrequencyHz / 100) })
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// Ensures the master output maintains adequate headroom.
    /// </summary>
    /// <param name="currentPeakDb">Current peak level in dB.</param>
    /// <param name="currentLufs">Current integrated LUFS.</param>
    /// <returns>Suggested master gain adjustment in dB.</returns>
    public float ManageHeadroom(float currentPeakDb, float currentLufs)
    {
        float targetPeak = -MinHeadroomDb;
        float headroomAdjustment = 0f;

        // If we're clipping or too close to 0 dBFS
        if (currentPeakDb > targetPeak)
        {
            headroomAdjustment = targetPeak - currentPeakDb;
        }

        // Also consider loudness targeting
        if (Math.Abs(currentLufs - TargetLufs) > 1f)
        {
            float loudnessAdjustment = TargetLufs - currentLufs;

            // Use the more conservative adjustment
            if (currentPeakDb + loudnessAdjustment > targetPeak)
            {
                headroomAdjustment = Math.Min(headroomAdjustment, targetPeak - currentPeakDb);
            }
            else
            {
                headroomAdjustment = loudnessAdjustment;
            }
        }

        return Math.Clamp(headroomAdjustment, -12f, 12f);
    }

    /// <summary>
    /// Suggests track groupings based on names and audio characteristics.
    /// </summary>
    /// <param name="trackNames">List of track names.</param>
    /// <param name="trackAnalyses">Optional spectral analysis data.</param>
    /// <returns>List of suggested track groupings.</returns>
    public List<TrackGroupingSuggestion> SuggestTrackGroupings(
        List<string> trackNames,
        Dictionary<string, float[]>? trackAnalyses = null)
    {
        var suggestions = new List<TrackGroupingSuggestion>();
        var categorizedTracks = new Dictionary<MixdownTrackCategory, List<string>>();

        foreach (var trackName in trackNames)
        {
            var category = DetectTrackCategory(trackName, trackAnalyses?.GetValueOrDefault(trackName));

            if (!categorizedTracks.ContainsKey(category))
            {
                categorizedTracks[category] = new List<string>();
            }
            categorizedTracks[category].Add(trackName);
        }

        // Create grouping suggestions for categories with multiple tracks
        foreach (var kvp in categorizedTracks)
        {
            if (kvp.Value.Count >= 2)
            {
                suggestions.Add(new TrackGroupingSuggestion
                {
                    GroupName = GetGroupNameForCategory(kvp.Key),
                    Category = kvp.Key,
                    TrackNames = kvp.Value,
                    Color = GetColorForCategory(kvp.Key)
                });
            }
        }

        return suggestions;
    }

    /// <summary>
    /// Suggests bus routing configuration.
    /// </summary>
    /// <param name="trackCategories">Track category assignments.</param>
    /// <returns>List of bus routing suggestions.</returns>
    public List<BusRoutingSuggestion> SuggestBusRouting(Dictionary<string, MixdownTrackCategory> trackCategories)
    {
        var suggestions = new List<BusRoutingSuggestion>();
        var preset = _genrePresets[_currentGenre];

        // Group buses
        var drumTracks = trackCategories.Where(t => t.Value == MixdownTrackCategory.Drums).Select(t => t.Key).ToList();
        if (drumTracks.Count > 0)
        {
            suggestions.Add(new BusRoutingSuggestion
            {
                BusName = "Drum Bus",
                BusType = "Group",
                TrackNames = drumTracks,
                Color = "#FF5722",
                SuggestedProcessing = "Bus compression (glue), EQ for punch, optional parallel compression"
            });
        }

        var bassTracks = trackCategories.Where(t => t.Value == MixdownTrackCategory.Bass).Select(t => t.Key).ToList();
        if (bassTracks.Count > 0)
        {
            suggestions.Add(new BusRoutingSuggestion
            {
                BusName = "Bass Bus",
                BusType = "Group",
                TrackNames = bassTracks,
                Color = "#9C27B0",
                SuggestedProcessing = "Gentle compression, low-end EQ, mono below 120Hz"
            });
        }

        var vocalTracks = trackCategories.Where(t =>
            t.Value == MixdownTrackCategory.Vocals ||
            t.Value == MixdownTrackCategory.BackingVocals).Select(t => t.Key).ToList();
        if (vocalTracks.Count > 0)
        {
            suggestions.Add(new BusRoutingSuggestion
            {
                BusName = "Vocal Bus",
                BusType = "Group",
                TrackNames = vocalTracks,
                Color = "#2196F3",
                SuggestedProcessing = "Bus EQ for cohesion, gentle compression, de-essing"
            });
        }

        // Effect sends
        suggestions.Add(new BusRoutingSuggestion
        {
            BusName = "Reverb",
            BusType = "Aux",
            TrackNames = new List<string>(),
            Color = "#4CAF50",
            SuggestedProcessing = $"Plate or hall reverb, {preset.ReverbDecay}s decay, pre-delay {preset.ReverbPreDelayMs}ms"
        });

        suggestions.Add(new BusRoutingSuggestion
        {
            BusName = "Delay",
            BusType = "Aux",
            TrackNames = new List<string>(),
            Color = "#FF9800",
            SuggestedProcessing = $"Stereo delay, tempo-synced, {preset.DelayFeedback * 100:F0}% feedback"
        });

        return suggestions;
    }

    /// <summary>
    /// Gets compression recommendations per track type.
    /// </summary>
    /// <param name="trackCategories">Track category assignments.</param>
    /// <param name="trackAnalyses">Dynamic range analyses.</param>
    /// <returns>List of compression recommendations.</returns>
    public List<CompressionRecommendation> GetCompressionRecommendations(
        Dictionary<string, MixdownTrackCategory> trackCategories,
        Dictionary<string, DynamicRangeAnalysis>? trackAnalyses = null)
    {
        var recommendations = new List<CompressionRecommendation>();

        foreach (var kvp in trackCategories)
        {
            var trackName = kvp.Key;
            var category = kvp.Value;

            var defaultComp = _categoryCompressionDefaults.GetValueOrDefault(category);
            if (defaultComp == null)
            {
                defaultComp = _categoryCompressionDefaults[MixdownTrackCategory.Other];
            }

            var recommendation = new CompressionRecommendation
            {
                TrackName = trackName,
                Category = category,
                ThresholdDb = defaultComp.ThresholdDb,
                Ratio = defaultComp.Ratio,
                AttackMs = defaultComp.AttackMs,
                ReleaseMs = defaultComp.ReleaseMs,
                KneeDb = defaultComp.KneeDb,
                MakeupGainDb = defaultComp.MakeupGainDb,
                ParallelRecommended = defaultComp.ParallelRecommended,
                ParallelMix = defaultComp.ParallelMix,
                Notes = defaultComp.Notes
            };

            // Adjust based on actual analysis if available
            if (trackAnalyses != null && trackAnalyses.TryGetValue(trackName, out var analysis))
            {
                if (analysis.CrestFactorDb < 8f)
                {
                    // Already compressed - use lighter settings
                    recommendation.Ratio = Math.Max(2f, recommendation.Ratio * 0.5f);
                    recommendation.ThresholdDb = Math.Min(-10f, recommendation.ThresholdDb + 6f);
                    recommendation.Notes += " (adjusted for pre-compressed source)";
                }
                else if (analysis.CrestFactorDb > 20f)
                {
                    // Very dynamic - may need more compression
                    recommendation.Ratio = Math.Min(8f, recommendation.Ratio * 1.5f);
                    recommendation.ThresholdDb = analysis.RmsDb + 6f;
                    recommendation.Notes += " (adjusted for high dynamic range)";
                }
            }

            recommendations.Add(recommendation);
        }

        return recommendations;
    }

    /// <summary>
    /// Suggests reverb and delay send levels.
    /// </summary>
    /// <param name="trackCategories">Track category assignments.</param>
    /// <returns>List of send level suggestions.</returns>
    public List<SendLevelSuggestion> SuggestSendLevels(Dictionary<string, MixdownTrackCategory> trackCategories)
    {
        var suggestions = new List<SendLevelSuggestion>();
        var preset = _genrePresets[_currentGenre];

        foreach (var kvp in trackCategories)
        {
            var trackName = kvp.Key;
            var category = kvp.Value;

            // Reverb send
            float reverbLevel = GetCategoryReverbLevel(category, preset);
            if (reverbLevel > 0)
            {
                suggestions.Add(new SendLevelSuggestion
                {
                    TrackName = trackName,
                    BusName = "Reverb",
                    SendLevel = reverbLevel,
                    PreFader = false,
                    Reason = GetReverbReason(category)
                });
            }

            // Delay send
            float delayLevel = GetCategoryDelayLevel(category, preset);
            if (delayLevel > 0)
            {
                suggestions.Add(new SendLevelSuggestion
                {
                    TrackName = trackName,
                    BusName = "Delay",
                    SendLevel = delayLevel,
                    PreFader = false,
                    Reason = GetDelayReason(category)
                });
            }
        }

        return suggestions;
    }

    /// <summary>
    /// Suggests stereo width settings per track.
    /// </summary>
    /// <param name="trackCategories">Track category assignments.</param>
    /// <returns>Dictionary of track names to suggested stereo width (0-1).</returns>
    public Dictionary<string, float> SuggestStereoWidth(Dictionary<string, MixdownTrackCategory> trackCategories)
    {
        var suggestions = new Dictionary<string, float>();
        var preset = _genrePresets[_currentGenre];

        foreach (var kvp in trackCategories)
        {
            float width = kvp.Value switch
            {
                MixdownTrackCategory.Drums => 0.8f,
                MixdownTrackCategory.Bass => 0f, // Mono bass
                MixdownTrackCategory.Vocals => preset.VocalStereoWidth,
                MixdownTrackCategory.BackingVocals => 0.9f, // Wide backing vocals
                MixdownTrackCategory.Guitars => 0.85f,
                MixdownTrackCategory.Keys => 0.7f,
                MixdownTrackCategory.Synths => 0.9f,
                MixdownTrackCategory.Strings => 1f,
                MixdownTrackCategory.Pads => 1f,
                _ => 0.7f
            };

            suggestions[kvp.Key] = width;
        }

        return suggestions;
    }

    /// <summary>
    /// Suggests low-end management settings.
    /// </summary>
    /// <param name="trackCategories">Track category assignments.</param>
    /// <returns>List of low-end management suggestions.</returns>
    public List<LowEndManagementSuggestion> SuggestLowEndManagement(Dictionary<string, MixdownTrackCategory> trackCategories)
    {
        var suggestions = new List<LowEndManagementSuggestion>();

        foreach (var kvp in trackCategories)
        {
            var trackName = kvp.Key;
            var category = kvp.Value;

            var suggestion = new LowEndManagementSuggestion
            {
                TrackName = trackName
            };

            switch (category)
            {
                case MixdownTrackCategory.Drums:
                    suggestion.MonoBelowFrequency = true;
                    suggestion.MonoFrequencyHz = 80f;
                    suggestion.HighPassHz = 30f;
                    suggestion.Reason = "Keep sub frequencies mono for punch, remove rumble below 30Hz";
                    break;

                case MixdownTrackCategory.Bass:
                    suggestion.MonoBelowFrequency = true;
                    suggestion.MonoFrequencyHz = 120f;
                    suggestion.HighPassHz = 25f;
                    suggestion.Reason = "Keep bass mono for tight low end, remove sub-rumble";
                    break;

                case MixdownTrackCategory.Vocals:
                    suggestion.MonoBelowFrequency = false;
                    suggestion.HighPassHz = 80f;
                    suggestion.Reason = "Remove rumble and plosives";
                    break;

                case MixdownTrackCategory.Guitars:
                    suggestion.MonoBelowFrequency = false;
                    suggestion.HighPassHz = 100f;
                    suggestion.Reason = "Clear space for bass and kick";
                    break;

                case MixdownTrackCategory.Keys:
                    suggestion.MonoBelowFrequency = false;
                    suggestion.HighPassHz = 60f;
                    suggestion.Reason = "Keep fundamental but remove rumble";
                    break;

                case MixdownTrackCategory.Synths:
                    suggestion.MonoBelowFrequency = false;
                    suggestion.HighPassHz = 80f;
                    suggestion.Reason = "Clear space for bass instruments";
                    break;

                case MixdownTrackCategory.Pads:
                    suggestion.MonoBelowFrequency = false;
                    suggestion.HighPassHz = 150f;
                    suggestion.Reason = "Keep pads out of low-end territory";
                    break;

                default:
                    suggestion.MonoBelowFrequency = false;
                    suggestion.HighPassHz = 80f;
                    suggestion.Reason = "General cleanup of sub frequencies";
                    break;
            }

            suggestions.Add(suggestion);
        }

        return suggestions;
    }

    /// <summary>
    /// Analyzes the mix and identifies problem frequencies.
    /// </summary>
    /// <param name="masterSpectrum">Master channel spectrum data (8 bands).</param>
    /// <param name="trackSpectrums">Per-track spectrum data.</param>
    /// <returns>List of identified problem frequencies.</returns>
    public List<ProblemFrequency> AnalyzeProblemFrequencies(
        float[] masterSpectrum,
        Dictionary<string, float[]> trackSpectrums)
    {
        var problems = new List<ProblemFrequency>();
        float[] bandCenters = { 60f, 150f, 350f, 800f, 2000f, 4000f, 8000f, 14000f };
        string[] bandNames = { "Sub", "Bass", "Low-Mid", "Mid", "High-Mid", "Presence", "Brilliance", "Air" };

        // Check for buildup in each band
        for (int band = 0; band < Math.Min(masterSpectrum.Length, 8); band++)
        {
            // Find which tracks contribute most to this band
            var contributors = trackSpectrums
                .Where(t => t.Value.Length > band && t.Value[band] > 0.3f)
                .OrderByDescending(t => t.Value[band])
                .Select(t => t.Key)
                .ToList();

            // Check for resonance/buildup
            if (masterSpectrum[band] > 0.85f)
            {
                problems.Add(new ProblemFrequency
                {
                    FrequencyHz = bandCenters[band],
                    BandwidthHz = bandCenters[band] * 0.5f,
                    Severity = (masterSpectrum[band] - 0.7f) / 0.3f,
                    ProblemType = "Buildup",
                    ContributingTracks = contributors.Take(3).ToList(),
                    SuggestedAction = $"Cut {bandNames[band]} range ({bandCenters[band]:F0} Hz) on contributing tracks by 2-4 dB"
                });
            }

            // Check for masking (multiple tracks fighting)
            if (contributors.Count >= 3)
            {
                float avgEnergy = trackSpectrums
                    .Where(t => t.Value.Length > band)
                    .Average(t => t.Value[band]);

                if (avgEnergy > 0.5f)
                {
                    problems.Add(new ProblemFrequency
                    {
                        FrequencyHz = bandCenters[band],
                        BandwidthHz = bandCenters[band] * 0.4f,
                        Severity = avgEnergy,
                        ProblemType = "Masking",
                        ContributingTracks = contributors,
                        SuggestedAction = $"Create frequency separation in {bandNames[band]} range - assign different slots to each track"
                    });
                }
            }
        }

        // Check for low-mid mud (250-500 Hz)
        if (masterSpectrum.Length > 2 && masterSpectrum[2] > 0.75f)
        {
            problems.Add(new ProblemFrequency
            {
                FrequencyHz = 350f,
                BandwidthHz = 200f,
                Severity = masterSpectrum[2],
                ProblemType = "Mud",
                ContributingTracks = trackSpectrums
                    .Where(t => t.Value.Length > 2 && t.Value[2] > 0.4f)
                    .Select(t => t.Key)
                    .ToList(),
                SuggestedAction = "Apply broad cut around 250-400 Hz on non-bass tracks to improve clarity"
            });
        }

        return problems.OrderByDescending(p => p.Severity).ToList();
    }

    /// <summary>
    /// Compares the current mix to a reference track.
    /// </summary>
    /// <param name="mixSpectrum">Current mix spectrum (8 bands).</param>
    /// <param name="mixLufs">Current mix LUFS.</param>
    /// <param name="mixDynamicRange">Current mix dynamic range in dB.</param>
    /// <param name="mixStereoCorrelation">Current mix stereo correlation.</param>
    /// <param name="referenceSpectrum">Reference track spectrum (8 bands).</param>
    /// <param name="referenceLufs">Reference track LUFS.</param>
    /// <param name="referenceDynamicRange">Reference track dynamic range in dB.</param>
    /// <param name="referenceStereoCorrelation">Reference track stereo correlation.</param>
    /// <param name="referenceName">Name of the reference track.</param>
    /// <returns>Comparison result with recommendations.</returns>
    public ReferenceComparisonResult CompareToReference(
        float[] mixSpectrum,
        float mixLufs,
        float mixDynamicRange,
        float mixStereoCorrelation,
        float[] referenceSpectrum,
        float referenceLufs,
        float referenceDynamicRange,
        float referenceStereoCorrelation,
        string referenceName)
    {
        var result = new ReferenceComparisonResult
        {
            ReferenceName = referenceName,
            LoudnessDifferenceLu = mixLufs - referenceLufs,
            DynamicRangeDifferenceDb = mixDynamicRange - referenceDynamicRange,
            StereoWidthDifference = referenceStereoCorrelation - mixStereoCorrelation
        };

        // Band-by-band comparison
        float totalDifference = 0f;
        string[] bandNames = { "Sub", "Bass", "Low-Mid", "Mid", "High-Mid", "Presence", "Brilliance", "Air" };

        for (int i = 0; i < Math.Min(8, Math.Min(mixSpectrum.Length, referenceSpectrum.Length)); i++)
        {
            float mixDb = 20f * MathF.Log10(Math.Max(mixSpectrum[i], 0.001f));
            float refDb = 20f * MathF.Log10(Math.Max(referenceSpectrum[i], 0.001f));
            result.BandDifferencesDb[i] = mixDb - refDb;
            totalDifference += Math.Abs(result.BandDifferencesDb[i]);

            // Add specific recommendations for significant differences
            if (Math.Abs(result.BandDifferencesDb[i]) > 3f)
            {
                string action = result.BandDifferencesDb[i] > 0 ? "reduce" : "boost";
                result.Recommendations.Add(
                    $"{bandNames[i]}: {action} by {Math.Abs(result.BandDifferencesDb[i]):F1} dB to match reference");
            }
        }

        // Calculate similarity score
        float spectralSimilarity = Math.Max(0, 100f - totalDifference * 2f);
        float loudnessSimilarity = Math.Max(0, 100f - Math.Abs(result.LoudnessDifferenceLu) * 10f);
        float dynamicsSimilarity = Math.Max(0, 100f - Math.Abs(result.DynamicRangeDifferenceDb) * 5f);

        result.SimilarityScore = (spectralSimilarity + loudnessSimilarity + dynamicsSimilarity) / 3f;

        // Loudness recommendation
        if (Math.Abs(result.LoudnessDifferenceLu) > 1f)
        {
            string action = result.LoudnessDifferenceLu > 0 ? "reduce" : "increase";
            result.Recommendations.Add(
                $"Overall loudness: {action} by {Math.Abs(result.LoudnessDifferenceLu):F1} LU to match reference");
        }

        // Dynamic range recommendation
        if (Math.Abs(result.DynamicRangeDifferenceDb) > 2f)
        {
            string action = result.DynamicRangeDifferenceDb > 0 ? "apply more" : "reduce";
            result.Recommendations.Add(
                $"Dynamic range: {action} compression to match reference character");
        }

        // Stereo width recommendation
        if (Math.Abs(result.StereoWidthDifference) > 0.1f)
        {
            string action = result.StereoWidthDifference > 0 ? "wider" : "narrower";
            result.Recommendations.Add($"Stereo image: mix is {action} than reference");
        }

        return result;
    }

    /// <summary>
    /// Sets the mix to a target LUFS level.
    /// </summary>
    /// <param name="currentLufs">Current integrated LUFS.</param>
    /// <param name="targetLufs">Target LUFS level.</param>
    /// <returns>Required gain adjustment in dB.</returns>
    public float CalculateLoudnessAdjustment(float currentLufs, float targetLufs)
    {
        return targetLufs - currentLufs;
    }

    /// <summary>
    /// Analyzes the dynamic range of the mix.
    /// </summary>
    /// <param name="trackAnalyses">Per-track dynamic range analyses.</param>
    /// <param name="masterAnalysis">Master channel analysis.</param>
    /// <returns>Complete dynamic range analysis.</returns>
    public DynamicRangeAnalysis AnalyzeDynamicRange(
        List<DynamicRangeAnalysis> trackAnalyses,
        DynamicRangeAnalysis masterAnalysis)
    {
        var preset = _genrePresets[_currentGenre];

        var result = new DynamicRangeAnalysis
        {
            TrackName = "Overall Analysis",
            PeakDb = masterAnalysis.PeakDb,
            RmsDb = masterAnalysis.RmsDb,
            LufsIntegrated = masterAnalysis.LufsIntegrated,
            CrestFactorDb = masterAnalysis.CrestFactorDb,
            DynamicRangeDb = masterAnalysis.DynamicRangeDb
        };

        // Check against genre expectations
        if (masterAnalysis.CrestFactorDb < preset.MinCrestFactor)
        {
            result.IsOverCompressed = true;
            result.Notes = $"Mix appears over-compressed. Crest factor ({masterAnalysis.CrestFactorDb:F1} dB) " +
                          $"is below recommended minimum ({preset.MinCrestFactor:F1} dB) for {_currentGenre}.";
        }
        else if (masterAnalysis.CrestFactorDb > preset.MaxCrestFactor)
        {
            result.NeedsMoreCompression = true;
            result.Notes = $"Mix has high dynamic range. Crest factor ({masterAnalysis.CrestFactorDb:F1} dB) " +
                          $"exceeds typical range ({preset.MaxCrestFactor:F1} dB) for {_currentGenre}. " +
                          "Consider bus compression or limiting for more consistency.";
        }
        else
        {
            result.Notes = $"Dynamic range is appropriate for {_currentGenre} " +
                          $"(crest factor: {masterAnalysis.CrestFactorDb:F1} dB).";
        }

        return result;
    }

    /// <summary>
    /// Generates a complete text report of the mix state.
    /// </summary>
    /// <param name="analysisReport">Mix analysis report.</param>
    /// <param name="trackStates">Current track states.</param>
    /// <returns>Formatted text report.</returns>
    public string GenerateMixReport(MixAnalysisReport analysisReport, List<TrackMixState> trackStates)
    {
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("                    MIX ANALYSIS REPORT");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine($"Generated: {analysisReport.Timestamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Genre Preset: {analysisReport.Genre}");
        sb.AppendLine();

        sb.AppendLine("───────────────────────────────────────────────────────────");
        sb.AppendLine("OVERALL SCORES");
        sb.AppendLine("───────────────────────────────────────────────────────────");
        sb.AppendLine($"  Overall Mix Score:        {analysisReport.OverallScore:F0}/100");
        sb.AppendLine($"  Spectral Balance:         {analysisReport.SpectralBalanceScore:F0}/100");
        sb.AppendLine($"  Dynamic Range:            {analysisReport.DynamicRangeScore:F0}/100");
        sb.AppendLine($"  Frequency Collision:      {analysisReport.FrequencyCollisionScore:F0}/100");
        sb.AppendLine();

        sb.AppendLine("───────────────────────────────────────────────────────────");
        sb.AppendLine("LOUDNESS & HEADROOM");
        sb.AppendLine("───────────────────────────────────────────────────────────");
        sb.AppendLine($"  Integrated Loudness:      {analysisReport.LufsIntegrated:F1} LUFS");
        sb.AppendLine($"  Target Loudness:          {analysisReport.TargetLufs:F1} LUFS");
        sb.AppendLine($"  Deviation:                {analysisReport.LoudnessDeviationLu:+0.0;-0.0;0.0} LU");
        sb.AppendLine($"  Headroom:                 {analysisReport.HeadroomDb:F1} dB");
        sb.AppendLine($"  Stereo Correlation:       {analysisReport.StereoCorrelation:F2}");
        sb.AppendLine();

        if (trackStates.Count > 0)
        {
            sb.AppendLine("───────────────────────────────────────────────────────────");
            sb.AppendLine("TRACK SUMMARY");
            sb.AppendLine("───────────────────────────────────────────────────────────");
            sb.AppendLine($"{"Track",-25} {"Category",-15} {"Gain",-8} {"Pan",-8}");
            sb.AppendLine(new string('-', 60));

            foreach (var track in trackStates.OrderBy(t => t.Category))
            {
                string panStr = track.Pan == 0 ? "C" :
                               track.Pan < 0 ? $"L{Math.Abs(track.Pan * 100):F0}" :
                               $"R{track.Pan * 100:F0}";

                sb.AppendLine($"{Truncate(track.TrackName, 25),-25} {track.Category,-15} {track.GainDb:+0.0;-0.0;0.0} dB   {panStr,-8}");
            }
            sb.AppendLine();
        }

        if (analysisReport.ProblemFrequencies.Count > 0)
        {
            sb.AppendLine("───────────────────────────────────────────────────────────");
            sb.AppendLine("PROBLEM FREQUENCIES");
            sb.AppendLine("───────────────────────────────────────────────────────────");

            foreach (var problem in analysisReport.ProblemFrequencies.Take(5))
            {
                sb.AppendLine($"  [{problem.ProblemType}] {problem.FrequencyHz:F0} Hz (Severity: {problem.Severity:P0})");
                sb.AppendLine($"    Tracks: {string.Join(", ", problem.ContributingTracks.Take(3))}");
                sb.AppendLine($"    Action: {problem.SuggestedAction}");
                sb.AppendLine();
            }
        }

        if (analysisReport.Recommendations.Count > 0)
        {
            sb.AppendLine("───────────────────────────────────────────────────────────");
            sb.AppendLine("RECOMMENDATIONS");
            sb.AppendLine("───────────────────────────────────────────────────────────");

            for (int i = 0; i < analysisReport.Recommendations.Count; i++)
            {
                sb.AppendLine($"  {i + 1}. {analysisReport.Recommendations[i]}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("───────────────────────────────────────────────────────────");
        sb.AppendLine("SUMMARY");
        sb.AppendLine("───────────────────────────────────────────────────────────");
        sb.AppendLine(analysisReport.Summary);
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════");

        return sb.ToString();
    }

    /// <summary>
    /// Applies a one-click starting point for a new mix.
    /// </summary>
    /// <param name="trackNames">Track names.</param>
    /// <param name="trackCategories">Optional category assignments (will auto-detect if null).</param>
    /// <returns>Complete initial mix state.</returns>
    public MixStateSnapshot CreateOneClickStartingPoint(
        List<string> trackNames,
        Dictionary<string, MixdownTrackCategory>? trackCategories = null)
    {
        var snapshot = new MixStateSnapshot
        {
            Description = $"One-click starting point ({_currentGenre})",
            TargetLufs = TargetLufs
        };

        // Auto-detect categories if not provided
        trackCategories ??= trackNames.ToDictionary(
            name => name,
            name => DetectTrackCategory(name, null));

        // Get auto-pan positions
        var panPositions = AutoPan(trackNames.Select(n => new TrackMixState
        {
            TrackName = n,
            Category = trackCategories.GetValueOrDefault(n, MixdownTrackCategory.Other)
        }).ToList());

        // Get low-end management
        var lowEndSuggestions = SuggestLowEndManagement(trackCategories);

        // Get stereo width suggestions
        var stereoWidths = SuggestStereoWidth(trackCategories);

        // Create track states
        foreach (var trackName in trackNames)
        {
            var category = trackCategories.GetValueOrDefault(trackName, MixdownTrackCategory.Other);
            var lowEnd = lowEndSuggestions.FirstOrDefault(s => s.TrackName == trackName);

            var state = new TrackMixState
            {
                TrackId = trackName,
                TrackName = trackName,
                GainDb = 0f, // Neutral starting point
                Pan = panPositions.GetValueOrDefault(trackName, 0f),
                Muted = false,
                Soloed = false,
                StereoWidth = stereoWidths.GetValueOrDefault(trackName, 1f),
                LowCutHz = lowEnd?.HighPassHz ?? 0f,
                Category = category
            };

            snapshot.TrackStates.Add(state);
        }

        return snapshot;
    }

    /// <summary>
    /// Gets the available genre presets.
    /// </summary>
    /// <returns>List of available genres.</returns>
    public IReadOnlyList<MixdownGenre> GetAvailableGenres()
    {
        return Enum.GetValues<MixdownGenre>();
    }

    /// <summary>
    /// Gets the settings for a specific genre preset.
    /// </summary>
    /// <param name="genre">The genre to get settings for.</param>
    /// <returns>Genre preset description.</returns>
    public string GetGenrePresetDescription(MixdownGenre genre)
    {
        var preset = _genrePresets[genre];
        return $"{genre}: Target {preset.TargetLufs:F0} LUFS, " +
               $"Dynamic range {preset.MinCrestFactor:F0}-{preset.MaxCrestFactor:F0} dB, " +
               $"Reverb decay {preset.ReverbDecay:F1}s";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helper methods
    // ─────────────────────────────────────────────────────────────────────────

    private MixdownTrackCategory DetectTrackCategory(string trackName, float[]? spectrum)
    {
        var nameLower = trackName.ToLowerInvariant();

        // Name-based detection
        if (ContainsAny(nameLower, "kick", "bd ", "bass drum"))
            return MixdownTrackCategory.Drums;
        if (ContainsAny(nameLower, "snare", "sn ", "sd "))
            return MixdownTrackCategory.Drums;
        if (ContainsAny(nameLower, "hat", "hh ", "hihat", "cymbal", "crash", "ride"))
            return MixdownTrackCategory.Drums;
        if (ContainsAny(nameLower, "drum", "kit", "perc", "tom"))
            return MixdownTrackCategory.Drums;
        if (ContainsAny(nameLower, "bass", "sub", "808"))
            return MixdownTrackCategory.Bass;
        if (ContainsAny(nameLower, "vocal", "vox", "voice", "lead voc", "main voc"))
            return MixdownTrackCategory.Vocals;
        if (ContainsAny(nameLower, "bv ", "bgv", "backing", "harmony", "choir", "backup"))
            return MixdownTrackCategory.BackingVocals;
        if (ContainsAny(nameLower, "guitar", "gtr", "git", "acoustic g", "electric g"))
            return MixdownTrackCategory.Guitars;
        if (ContainsAny(nameLower, "piano", "keys", "keyboard", "organ", "rhodes", "wurli"))
            return MixdownTrackCategory.Keys;
        if (ContainsAny(nameLower, "synth", "pad", "lead synth", "arp"))
            return MixdownTrackCategory.Synths;
        if (ContainsAny(nameLower, "string", "violin", "cello", "viola", "orchestra"))
            return MixdownTrackCategory.Strings;
        if (ContainsAny(nameLower, "brass", "horn", "trumpet", "trombone", "sax"))
            return MixdownTrackCategory.Brass;
        if (ContainsAny(nameLower, "pad", "ambient", "atmo", "texture"))
            return MixdownTrackCategory.Pads;
        if (ContainsAny(nameLower, "fx", "sfx", "effect", "noise", "riser", "impact"))
            return MixdownTrackCategory.SFX;

        // Spectral-based detection as fallback
        if (spectrum != null && spectrum.Length >= 5)
        {
            float lowEnergy = spectrum[0] + spectrum[1];
            float midEnergy = spectrum[2] + spectrum[3];
            float highEnergy = spectrum.Length > 4 ? spectrum[4] : 0;

            if (lowEnergy > midEnergy && lowEnergy > highEnergy)
                return MixdownTrackCategory.Bass;
            if (highEnergy > lowEnergy && highEnergy > midEnergy)
                return MixdownTrackCategory.Drums; // Likely cymbals/hats
            if (midEnergy > lowEnergy * 2)
                return MixdownTrackCategory.Vocals;
        }

        return MixdownTrackCategory.Other;
    }

    private static bool ContainsAny(string text, params string[] terms)
    {
        return terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;
        return text[..(maxLength - 3)] + "...";
    }

    private string GetGroupNameForCategory(MixdownTrackCategory category)
    {
        return category switch
        {
            MixdownTrackCategory.Drums => "Drums",
            MixdownTrackCategory.Bass => "Bass",
            MixdownTrackCategory.Vocals => "Vocals",
            MixdownTrackCategory.BackingVocals => "Backing Vocals",
            MixdownTrackCategory.Guitars => "Guitars",
            MixdownTrackCategory.Keys => "Keys",
            MixdownTrackCategory.Synths => "Synths",
            MixdownTrackCategory.Strings => "Strings",
            MixdownTrackCategory.Brass => "Brass",
            MixdownTrackCategory.Pads => "Pads",
            MixdownTrackCategory.SFX => "FX",
            _ => "Other"
        };
    }

    private string GetColorForCategory(MixdownTrackCategory category)
    {
        return category switch
        {
            MixdownTrackCategory.Drums => "#FF5722",
            MixdownTrackCategory.Bass => "#9C27B0",
            MixdownTrackCategory.Vocals => "#2196F3",
            MixdownTrackCategory.BackingVocals => "#03A9F4",
            MixdownTrackCategory.Guitars => "#4CAF50",
            MixdownTrackCategory.Keys => "#FFEB3B",
            MixdownTrackCategory.Synths => "#E91E63",
            MixdownTrackCategory.Strings => "#795548",
            MixdownTrackCategory.Brass => "#FF9800",
            MixdownTrackCategory.Pads => "#00BCD4",
            MixdownTrackCategory.SFX => "#607D8B",
            _ => "#9E9E9E"
        };
    }

    private (string trackToCut, string otherTrack) DetermineTrackToCut(
        string track1, MixdownTrackCategory cat1, float energy1,
        string track2, MixdownTrackCategory cat2, float energy2,
        int band)
    {
        // Priority order for frequency ownership (higher priority keeps its frequency slot)
        int priority1 = GetCategoryFrequencyPriority(cat1, band);
        int priority2 = GetCategoryFrequencyPriority(cat2, band);

        if (priority1 > priority2)
            return (track2, track1);
        if (priority2 > priority1)
            return (track1, track2);

        // Equal priority - cut the quieter one less, louder one more
        return energy1 > energy2 ? (track1, track2) : (track2, track1);
    }

    private int GetCategoryFrequencyPriority(MixdownTrackCategory category, int band)
    {
        // Band indexes: 0=Sub, 1=Bass, 2=LowMid, 3=Mid, 4=HighMid, 5=Presence, 6=Brilliance, 7=Air
        return (category, band) switch
        {
            (MixdownTrackCategory.Drums, 0) => 10, // Kick owns sub
            (MixdownTrackCategory.Bass, 0) => 8,
            (MixdownTrackCategory.Bass, 1) => 10, // Bass owns bass range
            (MixdownTrackCategory.Drums, 1) => 7,
            (MixdownTrackCategory.Vocals, 3) => 10, // Vocals own mids
            (MixdownTrackCategory.Vocals, 4) => 10, // Vocals own high-mids
            (MixdownTrackCategory.Drums, 4) => 8, // Snare high-mids
            (MixdownTrackCategory.Guitars, 3) => 7,
            (MixdownTrackCategory.Keys, 3) => 6,
            (MixdownTrackCategory.Drums, 6) => 8, // Cymbals own brilliance
            (MixdownTrackCategory.Drums, 7) => 8, // Cymbals own air
            _ => 5
        };
    }

    private (float DefaultPan, float SpreadWidth, bool KeepCentered) GetCategoryPanRule(
        MixdownTrackCategory category, GenrePreset preset)
    {
        return category switch
        {
            MixdownTrackCategory.Drums => (0f, preset.DrumPanSpread, false),
            MixdownTrackCategory.Bass => (0f, 0f, true),
            MixdownTrackCategory.Vocals => (0f, 0.2f, true),
            MixdownTrackCategory.BackingVocals => (0f, preset.BackingVocalSpread, false),
            MixdownTrackCategory.Guitars => (0.5f, preset.GuitarPanSpread, false),
            MixdownTrackCategory.Keys => (0f, 0.6f, false),
            MixdownTrackCategory.Synths => (0f, 0.8f, false),
            MixdownTrackCategory.Strings => (0f, 1f, false),
            MixdownTrackCategory.Brass => (0f, 0.7f, false),
            MixdownTrackCategory.Pads => (0f, 1f, false),
            MixdownTrackCategory.SFX => (0f, 1f, false),
            _ => (0f, 0.5f, false)
        };
    }

    private float GetCategoryReverbLevel(MixdownTrackCategory category, GenrePreset preset)
    {
        return category switch
        {
            MixdownTrackCategory.Drums => preset.DrumReverbSend,
            MixdownTrackCategory.Bass => 0f, // No reverb on bass
            MixdownTrackCategory.Vocals => preset.VocalReverbSend,
            MixdownTrackCategory.BackingVocals => preset.VocalReverbSend * 1.2f,
            MixdownTrackCategory.Guitars => 0.2f,
            MixdownTrackCategory.Keys => 0.25f,
            MixdownTrackCategory.Synths => 0.15f,
            MixdownTrackCategory.Strings => 0.3f,
            MixdownTrackCategory.Brass => 0.2f,
            MixdownTrackCategory.Pads => 0.35f,
            MixdownTrackCategory.SFX => 0.2f,
            _ => 0.15f
        };
    }

    private float GetCategoryDelayLevel(MixdownTrackCategory category, GenrePreset preset)
    {
        return category switch
        {
            MixdownTrackCategory.Drums => 0f,
            MixdownTrackCategory.Bass => 0f,
            MixdownTrackCategory.Vocals => preset.VocalDelaySend,
            MixdownTrackCategory.BackingVocals => preset.VocalDelaySend * 0.5f,
            MixdownTrackCategory.Guitars => 0.15f,
            MixdownTrackCategory.Keys => 0.1f,
            MixdownTrackCategory.Synths => 0.2f,
            MixdownTrackCategory.Strings => 0f,
            MixdownTrackCategory.Brass => 0f,
            MixdownTrackCategory.Pads => 0.1f,
            MixdownTrackCategory.SFX => 0.25f,
            _ => 0.1f
        };
    }

    private string GetReverbReason(MixdownTrackCategory category)
    {
        return category switch
        {
            MixdownTrackCategory.Drums => "Add space to drums without washing out transients",
            MixdownTrackCategory.Vocals => "Create depth and polish for vocals",
            MixdownTrackCategory.BackingVocals => "Push backing vocals back in the mix",
            MixdownTrackCategory.Guitars => "Add dimension to guitars",
            MixdownTrackCategory.Keys => "Create natural piano/keys space",
            MixdownTrackCategory.Synths => "Add depth to synths",
            MixdownTrackCategory.Strings => "Natural string hall ambience",
            MixdownTrackCategory.Brass => "Add room ambience to brass",
            MixdownTrackCategory.Pads => "Enhance pad texture",
            _ => "Add depth and space"
        };
    }

    private string GetDelayReason(MixdownTrackCategory category)
    {
        return category switch
        {
            MixdownTrackCategory.Vocals => "Add rhythmic interest and width to vocals",
            MixdownTrackCategory.Guitars => "Create stereo spread with slap delay",
            MixdownTrackCategory.Keys => "Add subtle movement",
            MixdownTrackCategory.Synths => "Rhythmic delay for movement",
            MixdownTrackCategory.SFX => "Enhance effect impact",
            _ => "Add rhythmic depth"
        };
    }

    private Dictionary<MixdownGenre, GenrePreset> InitializeGenrePresets()
    {
        return new Dictionary<MixdownGenre, GenrePreset>
        {
            [MixdownGenre.Pop] = new GenrePreset
            {
                TargetLufs = -14f,
                TargetRmsDb = -18f,
                MinCrestFactor = 6f,
                MaxCrestFactor = 12f,
                ReverbDecay = 1.5f,
                ReverbPreDelayMs = 30f,
                DelayFeedback = 0.3f,
                DrumPanSpread = 0.6f,
                GuitarPanSpread = 0.8f,
                BackingVocalSpread = 0.7f,
                VocalStereoWidth = 0.3f,
                DrumReverbSend = 0.15f,
                VocalReverbSend = 0.25f,
                VocalDelaySend = 0.15f
            },
            [MixdownGenre.Rock] = new GenrePreset
            {
                TargetLufs = -12f,
                TargetRmsDb = -16f,
                MinCrestFactor = 8f,
                MaxCrestFactor = 14f,
                ReverbDecay = 1.2f,
                ReverbPreDelayMs = 20f,
                DelayFeedback = 0.25f,
                DrumPanSpread = 0.5f,
                GuitarPanSpread = 1f,
                BackingVocalSpread = 0.6f,
                VocalStereoWidth = 0.2f,
                DrumReverbSend = 0.2f,
                VocalReverbSend = 0.2f,
                VocalDelaySend = 0.1f
            },
            [MixdownGenre.EDM] = new GenrePreset
            {
                TargetLufs = -10f,
                TargetRmsDb = -14f,
                MinCrestFactor = 4f,
                MaxCrestFactor = 8f,
                ReverbDecay = 2f,
                ReverbPreDelayMs = 40f,
                DelayFeedback = 0.4f,
                DrumPanSpread = 0.4f,
                GuitarPanSpread = 0.6f,
                BackingVocalSpread = 0.8f,
                VocalStereoWidth = 0.5f,
                DrumReverbSend = 0.1f,
                VocalReverbSend = 0.3f,
                VocalDelaySend = 0.2f
            },
            [MixdownGenre.HipHop] = new GenrePreset
            {
                TargetLufs = -12f,
                TargetRmsDb = -15f,
                MinCrestFactor = 5f,
                MaxCrestFactor = 10f,
                ReverbDecay = 1f,
                ReverbPreDelayMs = 25f,
                DelayFeedback = 0.35f,
                DrumPanSpread = 0.3f,
                GuitarPanSpread = 0.5f,
                BackingVocalSpread = 0.7f,
                VocalStereoWidth = 0.3f,
                DrumReverbSend = 0.1f,
                VocalReverbSend = 0.15f,
                VocalDelaySend = 0.2f
            },
            [MixdownGenre.Jazz] = new GenrePreset
            {
                TargetLufs = -16f,
                TargetRmsDb = -20f,
                MinCrestFactor = 12f,
                MaxCrestFactor = 20f,
                ReverbDecay = 2.5f,
                ReverbPreDelayMs = 50f,
                DelayFeedback = 0.2f,
                DrumPanSpread = 0.8f,
                GuitarPanSpread = 0.5f,
                BackingVocalSpread = 0.6f,
                VocalStereoWidth = 0.4f,
                DrumReverbSend = 0.25f,
                VocalReverbSend = 0.2f,
                VocalDelaySend = 0.05f
            },
            [MixdownGenre.Classical] = new GenrePreset
            {
                TargetLufs = -18f,
                TargetRmsDb = -22f,
                MinCrestFactor = 15f,
                MaxCrestFactor = 25f,
                ReverbDecay = 3f,
                ReverbPreDelayMs = 60f,
                DelayFeedback = 0.1f,
                DrumPanSpread = 1f,
                GuitarPanSpread = 0.8f,
                BackingVocalSpread = 1f,
                VocalStereoWidth = 0.6f,
                DrumReverbSend = 0.3f,
                VocalReverbSend = 0.25f,
                VocalDelaySend = 0f
            }
        };
    }

    private Dictionary<MixdownTrackCategory, float[]> InitializeCategoryProfiles()
    {
        // Spectral profiles: [Sub, Bass, LowMid, Mid, HighMid, Presence, Brilliance, Air]
        return new Dictionary<MixdownTrackCategory, float[]>
        {
            [MixdownTrackCategory.Drums] = new[] { 0.6f, 0.5f, 0.4f, 0.5f, 0.7f, 0.6f, 0.7f, 0.5f },
            [MixdownTrackCategory.Bass] = new[] { 0.8f, 0.9f, 0.6f, 0.3f, 0.2f, 0.1f, 0.05f, 0.02f },
            [MixdownTrackCategory.Vocals] = new[] { 0.1f, 0.2f, 0.4f, 0.8f, 0.9f, 0.7f, 0.5f, 0.3f },
            [MixdownTrackCategory.BackingVocals] = new[] { 0.1f, 0.2f, 0.3f, 0.7f, 0.8f, 0.6f, 0.4f, 0.3f },
            [MixdownTrackCategory.Guitars] = new[] { 0.2f, 0.4f, 0.6f, 0.8f, 0.7f, 0.5f, 0.4f, 0.3f },
            [MixdownTrackCategory.Keys] = new[] { 0.3f, 0.4f, 0.5f, 0.7f, 0.7f, 0.6f, 0.5f, 0.4f },
            [MixdownTrackCategory.Synths] = new[] { 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.7f, 0.6f, 0.5f },
            [MixdownTrackCategory.Strings] = new[] { 0.2f, 0.4f, 0.6f, 0.7f, 0.6f, 0.5f, 0.5f, 0.4f },
            [MixdownTrackCategory.Brass] = new[] { 0.2f, 0.3f, 0.5f, 0.7f, 0.8f, 0.6f, 0.4f, 0.3f },
            [MixdownTrackCategory.Pads] = new[] { 0.3f, 0.4f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f },
            [MixdownTrackCategory.SFX] = new[] { 0.3f, 0.4f, 0.5f, 0.5f, 0.6f, 0.6f, 0.6f, 0.5f },
            [MixdownTrackCategory.Other] = new[] { 0.3f, 0.4f, 0.5f, 0.5f, 0.5f, 0.5f, 0.4f, 0.3f }
        };
    }

    private Dictionary<MixdownTrackCategory, CompressionRecommendation> InitializeCompressionDefaults()
    {
        return new Dictionary<MixdownTrackCategory, CompressionRecommendation>
        {
            [MixdownTrackCategory.Drums] = new CompressionRecommendation
            {
                ThresholdDb = -12f,
                Ratio = 4f,
                AttackMs = 10f,
                ReleaseMs = 100f,
                KneeDb = 0f,
                MakeupGainDb = 2f,
                ParallelRecommended = true,
                ParallelMix = 0.4f,
                Notes = "Fast attack for punch control, parallel for weight"
            },
            [MixdownTrackCategory.Bass] = new CompressionRecommendation
            {
                ThresholdDb = -16f,
                Ratio = 4f,
                AttackMs = 20f,
                ReleaseMs = 150f,
                KneeDb = 3f,
                MakeupGainDb = 2f,
                ParallelRecommended = false,
                Notes = "Consistent low end, moderate attack to preserve note definition"
            },
            [MixdownTrackCategory.Vocals] = new CompressionRecommendation
            {
                ThresholdDb = -18f,
                Ratio = 3f,
                AttackMs = 10f,
                ReleaseMs = 100f,
                KneeDb = 6f,
                MakeupGainDb = 3f,
                ParallelRecommended = false,
                Notes = "Transparent compression, soft knee for natural sound"
            },
            [MixdownTrackCategory.BackingVocals] = new CompressionRecommendation
            {
                ThresholdDb = -20f,
                Ratio = 4f,
                AttackMs = 5f,
                ReleaseMs = 80f,
                KneeDb = 3f,
                MakeupGainDb = 4f,
                ParallelRecommended = false,
                Notes = "More aggressive to keep backgrounds consistent"
            },
            [MixdownTrackCategory.Guitars] = new CompressionRecommendation
            {
                ThresholdDb = -16f,
                Ratio = 3f,
                AttackMs = 15f,
                ReleaseMs = 120f,
                KneeDb = 6f,
                MakeupGainDb = 2f,
                ParallelRecommended = false,
                Notes = "Gentle compression, amp already provides some"
            },
            [MixdownTrackCategory.Keys] = new CompressionRecommendation
            {
                ThresholdDb = -18f,
                Ratio = 2.5f,
                AttackMs = 15f,
                ReleaseMs = 150f,
                KneeDb = 6f,
                MakeupGainDb = 2f,
                ParallelRecommended = false,
                Notes = "Transparent, maintain dynamics"
            },
            [MixdownTrackCategory.Synths] = new CompressionRecommendation
            {
                ThresholdDb = -14f,
                Ratio = 3f,
                AttackMs = 10f,
                ReleaseMs = 100f,
                KneeDb = 3f,
                MakeupGainDb = 2f,
                ParallelRecommended = false,
                Notes = "Control peaks while maintaining character"
            },
            [MixdownTrackCategory.Strings] = new CompressionRecommendation
            {
                ThresholdDb = -20f,
                Ratio = 2f,
                AttackMs = 30f,
                ReleaseMs = 200f,
                KneeDb = 6f,
                MakeupGainDb = 1f,
                ParallelRecommended = false,
                Notes = "Minimal compression, preserve natural dynamics"
            },
            [MixdownTrackCategory.Brass] = new CompressionRecommendation
            {
                ThresholdDb = -14f,
                Ratio = 3f,
                AttackMs = 5f,
                ReleaseMs = 80f,
                KneeDb = 3f,
                MakeupGainDb = 2f,
                ParallelRecommended = false,
                Notes = "Control peaks on loud passages"
            },
            [MixdownTrackCategory.Pads] = new CompressionRecommendation
            {
                ThresholdDb = -20f,
                Ratio = 2f,
                AttackMs = 30f,
                ReleaseMs = 250f,
                KneeDb = 6f,
                MakeupGainDb = 1f,
                ParallelRecommended = false,
                Notes = "Minimal compression for ambient elements"
            },
            [MixdownTrackCategory.SFX] = new CompressionRecommendation
            {
                ThresholdDb = -12f,
                Ratio = 4f,
                AttackMs = 1f,
                ReleaseMs = 50f,
                KneeDb = 0f,
                MakeupGainDb = 3f,
                ParallelRecommended = false,
                Notes = "Control transients while maintaining impact"
            },
            [MixdownTrackCategory.Other] = new CompressionRecommendation
            {
                ThresholdDb = -18f,
                Ratio = 3f,
                AttackMs = 10f,
                ReleaseMs = 100f,
                KneeDb = 3f,
                MakeupGainDb = 2f,
                ParallelRecommended = false,
                Notes = "General purpose settings"
            }
        };
    }

    /// <summary>
    /// Internal genre preset configuration.
    /// </summary>
    private class GenrePreset
    {
        public float TargetLufs { get; set; }
        public float TargetRmsDb { get; set; }
        public float MinCrestFactor { get; set; }
        public float MaxCrestFactor { get; set; }
        public float ReverbDecay { get; set; }
        public float ReverbPreDelayMs { get; set; }
        public float DelayFeedback { get; set; }
        public float DrumPanSpread { get; set; }
        public float GuitarPanSpread { get; set; }
        public float BackingVocalSpread { get; set; }
        public float VocalStereoWidth { get; set; }
        public float DrumReverbSend { get; set; }
        public float VocalReverbSend { get; set; }
        public float VocalDelaySend { get; set; }
    }
}
