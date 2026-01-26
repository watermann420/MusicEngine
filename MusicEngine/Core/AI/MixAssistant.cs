//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: AI-based mixing assistant providing EQ, compression, and panning suggestions.

using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;

namespace MusicEngine.Core.AI;

/// <summary>
/// Genre presets for mixing guidance.
/// </summary>
public enum MixGenre
{
    /// <summary>Pop music mixing style.</summary>
    Pop,
    /// <summary>Rock music mixing style.</summary>
    Rock,
    /// <summary>Electronic/EDM mixing style.</summary>
    Electronic,
    /// <summary>Hip-hop/R&amp;B mixing style.</summary>
    HipHop,
    /// <summary>Jazz mixing style.</summary>
    Jazz,
    /// <summary>Classical/orchestral mixing style.</summary>
    Classical,
    /// <summary>Acoustic/folk mixing style.</summary>
    Acoustic,
    /// <summary>Metal/heavy rock mixing style.</summary>
    Metal
}

/// <summary>
/// Track type for context-aware mixing suggestions.
/// </summary>
public enum TrackType
{
    /// <summary>Lead vocals.</summary>
    LeadVocal,
    /// <summary>Background/harmony vocals.</summary>
    BackingVocal,
    /// <summary>Kick drum.</summary>
    Kick,
    /// <summary>Snare drum.</summary>
    Snare,
    /// <summary>Hi-hat and cymbals.</summary>
    HiHat,
    /// <summary>Full drum kit overhead/room.</summary>
    Drums,
    /// <summary>Bass guitar or synth bass.</summary>
    Bass,
    /// <summary>Electric guitar.</summary>
    ElectricGuitar,
    /// <summary>Acoustic guitar.</summary>
    AcousticGuitar,
    /// <summary>Piano/keys.</summary>
    Piano,
    /// <summary>Synthesizer.</summary>
    Synth,
    /// <summary>Strings/orchestral.</summary>
    Strings,
    /// <summary>Brass section.</summary>
    Brass,
    /// <summary>Pad/ambient sounds.</summary>
    Pad,
    /// <summary>Sound effects/FX.</summary>
    SoundFX,
    /// <summary>Generic/unknown track type.</summary>
    Generic
}

/// <summary>
/// EQ band suggestion with frequency, gain, and Q parameters.
/// </summary>
public class EqSuggestion
{
    /// <summary>Center frequency in Hz.</summary>
    public float Frequency { get; set; }

    /// <summary>Gain adjustment in dB (-12 to +12).</summary>
    public float GainDb { get; set; }

    /// <summary>Q factor (bandwidth).</summary>
    public float Q { get; set; } = 1.0f;

    /// <summary>Filter type (peak, lowshelf, highshelf, lowpass, highpass).</summary>
    public string FilterType { get; set; } = "peak";

    /// <summary>Reason for this suggestion.</summary>
    public string Reason { get; set; } = "";

    /// <summary>Priority/importance (0-1).</summary>
    public float Priority { get; set; } = 0.5f;
}

/// <summary>
/// Compression settings suggestion.
/// </summary>
public class CompressionSuggestion
{
    /// <summary>Threshold in dB.</summary>
    public float ThresholdDb { get; set; } = -20f;

    /// <summary>Compression ratio.</summary>
    public float Ratio { get; set; } = 4f;

    /// <summary>Attack time in milliseconds.</summary>
    public float AttackMs { get; set; } = 10f;

    /// <summary>Release time in milliseconds.</summary>
    public float ReleaseMs { get; set; } = 100f;

    /// <summary>Knee width in dB (0 = hard knee).</summary>
    public float KneeDb { get; set; } = 0f;

    /// <summary>Makeup gain in dB.</summary>
    public float MakeupGainDb { get; set; } = 0f;

    /// <summary>Reason for these settings.</summary>
    public string Reason { get; set; } = "";

    /// <summary>Compression style (punchy, transparent, glue, etc.).</summary>
    public string Style { get; set; } = "balanced";
}

/// <summary>
/// Panning position suggestion.
/// </summary>
public class PanningSuggestion
{
    /// <summary>Pan position (-1.0 = left, 0 = center, 1.0 = right).</summary>
    public float Position { get; set; }

    /// <summary>Suggested stereo width (0 = mono, 1 = full stereo).</summary>
    public float Width { get; set; } = 1.0f;

    /// <summary>Reason for this position.</summary>
    public string Reason { get; set; } = "";
}

/// <summary>
/// Level/volume suggestion.
/// </summary>
public class LevelSuggestion
{
    /// <summary>Suggested fader level in dB.</summary>
    public float LevelDb { get; set; }

    /// <summary>Reference level (what this is relative to).</summary>
    public string Reference { get; set; } = "mix";

    /// <summary>Reason for this level.</summary>
    public string Reason { get; set; } = "";
}

/// <summary>
/// Frequency conflict detected between tracks.
/// </summary>
public class FrequencyConflict
{
    /// <summary>First track name.</summary>
    public string Track1 { get; set; } = "";

    /// <summary>Second track name.</summary>
    public string Track2 { get; set; } = "";

    /// <summary>Conflicting frequency range (low, mid, high).</summary>
    public string FrequencyRange { get; set; } = "";

    /// <summary>Center frequency of conflict in Hz.</summary>
    public float CenterFrequency { get; set; }

    /// <summary>Severity (0-1).</summary>
    public float Severity { get; set; }

    /// <summary>Suggested resolution.</summary>
    public string Resolution { get; set; } = "";
}

/// <summary>
/// Complete mix suggestion for a single track.
/// </summary>
public class MixSuggestion
{
    /// <summary>Track name.</summary>
    public string TrackName { get; set; } = "";

    /// <summary>Detected or specified track type.</summary>
    public TrackType TrackType { get; set; }

    /// <summary>EQ suggestions for this track.</summary>
    public List<EqSuggestion> EqSuggestions { get; set; } = new();

    /// <summary>Compression suggestion.</summary>
    public CompressionSuggestion? CompressionSuggestion { get; set; }

    /// <summary>Panning suggestion.</summary>
    public PanningSuggestion? PanningSuggestion { get; set; }

    /// <summary>Level suggestion.</summary>
    public LevelSuggestion? LevelSuggestion { get; set; }

    /// <summary>Additional processing suggestions.</summary>
    public List<string> AdditionalSuggestions { get; set; } = new();

    /// <summary>Overall confidence in suggestions (0-1).</summary>
    public float Confidence { get; set; } = 0.5f;
}

/// <summary>
/// Analysis result for a single track.
/// </summary>
public class TrackAnalysis
{
    /// <summary>Track name.</summary>
    public string TrackName { get; set; } = "";

    /// <summary>Peak level in dB.</summary>
    public float PeakDb { get; set; }

    /// <summary>RMS level in dB.</summary>
    public float RmsDb { get; set; }

    /// <summary>Crest factor (peak/RMS ratio in dB).</summary>
    public float CrestFactorDb { get; set; }

    /// <summary>Spectral centroid in Hz (brightness indicator).</summary>
    public float SpectralCentroid { get; set; }

    /// <summary>Low frequency energy (20-200 Hz).</summary>
    public float LowEnergy { get; set; }

    /// <summary>Low-mid frequency energy (200-500 Hz).</summary>
    public float LowMidEnergy { get; set; }

    /// <summary>Mid frequency energy (500-2000 Hz).</summary>
    public float MidEnergy { get; set; }

    /// <summary>High-mid frequency energy (2000-6000 Hz).</summary>
    public float HighMidEnergy { get; set; }

    /// <summary>High frequency energy (6000-20000 Hz).</summary>
    public float HighEnergy { get; set; }

    /// <summary>Detected track type based on spectral content.</summary>
    public TrackType DetectedType { get; set; } = TrackType.Generic;

    /// <summary>Stereo correlation (-1 to 1).</summary>
    public float StereoCorrelation { get; set; } = 1f;
}

/// <summary>
/// AI-based mixing assistant providing EQ, compression, panning, and level suggestions.
/// Uses heuristic analysis of audio content and genre-based presets.
/// </summary>
public class MixAssistant
{
    private readonly Dictionary<(TrackType, MixGenre), MixTemplate> _templates;
    private readonly Dictionary<TrackType, float[]> _idealSpectralProfiles;

    /// <summary>
    /// Creates a new mix assistant.
    /// </summary>
    public MixAssistant()
    {
        _templates = InitializeTemplates();
        _idealSpectralProfiles = InitializeSpectralProfiles();
    }

    /// <summary>
    /// Analyzes a track and returns mix suggestions.
    /// </summary>
    /// <param name="samples">Audio samples (interleaved stereo or mono).</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of audio channels.</param>
    /// <param name="trackName">Name of the track.</param>
    /// <param name="trackType">Type of track (or Generic for auto-detection).</param>
    /// <param name="genre">Target genre for mixing style.</param>
    /// <returns>Mix suggestions for the track.</returns>
    public MixSuggestion AnalyzeTrack(
        float[] samples,
        int sampleRate,
        int channels,
        string trackName,
        TrackType trackType = TrackType.Generic,
        MixGenre genre = MixGenre.Pop)
    {
        // Analyze the track
        var analysis = AnalyzeAudio(samples, sampleRate, channels, trackName);

        // Auto-detect track type if generic
        if (trackType == TrackType.Generic)
        {
            trackType = analysis.DetectedType;
        }

        // Generate suggestions based on analysis and templates
        return GenerateSuggestions(analysis, trackType, genre);
    }

    /// <summary>
    /// Analyzes multiple tracks and suggests overall mix balance.
    /// </summary>
    /// <param name="trackAnalyses">List of track analyses.</param>
    /// <param name="genre">Target genre.</param>
    /// <returns>List of suggestions for each track plus frequency conflicts.</returns>
    public (List<MixSuggestion> suggestions, List<FrequencyConflict> conflicts) AnalyzeMix(
        List<TrackAnalysis> trackAnalyses,
        MixGenre genre = MixGenre.Pop)
    {
        var suggestions = new List<MixSuggestion>();
        var conflicts = DetectFrequencyConflicts(trackAnalyses);

        // Generate suggestions for each track
        foreach (var analysis in trackAnalyses)
        {
            var suggestion = GenerateSuggestions(analysis, analysis.DetectedType, genre);

            // Adjust suggestions based on conflicts
            foreach (var conflict in conflicts.Where(c =>
                c.Track1 == analysis.TrackName || c.Track2 == analysis.TrackName))
            {
                AdjustForConflict(suggestion, conflict, analysis.TrackName);
            }

            suggestions.Add(suggestion);
        }

        // Balance levels across the mix
        BalanceLevels(suggestions, trackAnalyses, genre);

        return (suggestions, conflicts);
    }

    /// <summary>
    /// Analyzes audio samples and returns spectral and dynamic information.
    /// </summary>
    public TrackAnalysis AnalyzeAudio(float[] samples, int sampleRate, int channels, string trackName)
    {
        var analysis = new TrackAnalysis { TrackName = trackName };

        if (samples.Length == 0)
        {
            return analysis;
        }

        // Calculate peak and RMS
        float peak = 0;
        float sumSquared = 0;
        int sampleCount = samples.Length;

        for (int i = 0; i < sampleCount; i++)
        {
            float abs = Math.Abs(samples[i]);
            if (abs > peak) peak = abs;
            sumSquared += samples[i] * samples[i];
        }

        float rms = MathF.Sqrt(sumSquared / sampleCount);
        analysis.PeakDb = 20f * MathF.Log10(Math.Max(peak, 1e-10f));
        analysis.RmsDb = 20f * MathF.Log10(Math.Max(rms, 1e-10f));
        analysis.CrestFactorDb = analysis.PeakDb - analysis.RmsDb;

        // Calculate stereo correlation
        if (channels == 2)
        {
            analysis.StereoCorrelation = CalculateStereoCorrelation(samples);
        }

        // Spectral analysis (simplified FFT-based)
        var spectralBands = AnalyzeSpectrum(samples, sampleRate, channels);
        analysis.LowEnergy = spectralBands[0];
        analysis.LowMidEnergy = spectralBands[1];
        analysis.MidEnergy = spectralBands[2];
        analysis.HighMidEnergy = spectralBands[3];
        analysis.HighEnergy = spectralBands[4];

        // Calculate spectral centroid
        analysis.SpectralCentroid = CalculateSpectralCentroid(spectralBands);

        // Detect track type
        analysis.DetectedType = DetectTrackType(analysis);

        return analysis;
    }

    /// <summary>
    /// Gets template-based suggestions for a track type and genre.
    /// </summary>
    public MixSuggestion GetTemplateSuggestions(TrackType trackType, MixGenre genre)
    {
        var suggestion = new MixSuggestion
        {
            TrackType = trackType,
            Confidence = 0.7f
        };

        if (_templates.TryGetValue((trackType, genre), out var template))
        {
            ApplyTemplate(suggestion, template);
        }
        else if (_templates.TryGetValue((trackType, MixGenre.Pop), out var fallback))
        {
            // Fall back to pop template
            ApplyTemplate(suggestion, fallback);
            suggestion.Confidence = 0.5f;
        }

        return suggestion;
    }

    /// <summary>
    /// Compares before/after analysis to evaluate mix improvements.
    /// </summary>
    public MixComparisonResult CompareMixes(TrackAnalysis before, TrackAnalysis after)
    {
        return new MixComparisonResult
        {
            DynamicRangeChange = after.CrestFactorDb - before.CrestFactorDb,
            LevelChange = after.RmsDb - before.RmsDb,
            BrightnessChange = after.SpectralCentroid - before.SpectralCentroid,
            LowEndChange = after.LowEnergy - before.LowEnergy,
            ClarityImproved = after.MidEnergy > before.MidEnergy && after.LowMidEnergy < before.LowMidEnergy,
            StereoWidthChange = after.StereoCorrelation - before.StereoCorrelation
        };
    }

    private MixSuggestion GenerateSuggestions(TrackAnalysis analysis, TrackType trackType, MixGenre genre)
    {
        var suggestion = GetTemplateSuggestions(trackType, genre);
        suggestion.TrackName = analysis.TrackName;
        suggestion.TrackType = trackType;

        // Adjust EQ based on actual spectral content
        AdjustEqForContent(suggestion, analysis, trackType);

        // Adjust compression based on dynamics
        AdjustCompressionForDynamics(suggestion, analysis, trackType);

        // Calculate confidence based on how well the analysis matches expected profile
        suggestion.Confidence = CalculateConfidence(analysis, trackType);

        return suggestion;
    }

    private void AdjustEqForContent(MixSuggestion suggestion, TrackAnalysis analysis, TrackType trackType)
    {
        if (!_idealSpectralProfiles.TryGetValue(trackType, out var idealProfile))
        {
            return;
        }

        float[] actualProfile = { analysis.LowEnergy, analysis.LowMidEnergy, analysis.MidEnergy, analysis.HighMidEnergy, analysis.HighEnergy };
        float[] centerFreqs = { 80f, 350f, 1000f, 3500f, 10000f };
        string[] bandNames = { "Low", "Low-Mid", "Mid", "High-Mid", "High" };

        for (int i = 0; i < 5; i++)
        {
            float difference = idealProfile[i] - actualProfile[i];

            // Only suggest changes if difference is significant
            if (Math.Abs(difference) > 0.1f)
            {
                // Limit gain adjustments
                float suggestedGain = Math.Clamp(difference * 6f, -6f, 6f);

                // Check if there's already a suggestion for this frequency range
                var existingSuggestion = suggestion.EqSuggestions.FirstOrDefault(
                    eq => Math.Abs(eq.Frequency - centerFreqs[i]) < centerFreqs[i] * 0.3f);

                if (existingSuggestion != null)
                {
                    // Blend with existing suggestion
                    existingSuggestion.GainDb = (existingSuggestion.GainDb + suggestedGain) / 2f;
                    existingSuggestion.Reason += $" | Adjusted for {bandNames[i]} content";
                }
                else
                {
                    suggestion.EqSuggestions.Add(new EqSuggestion
                    {
                        Frequency = centerFreqs[i],
                        GainDb = suggestedGain,
                        Q = i == 1 ? 0.7f : 1.0f, // Wider Q for low-mid to avoid muddiness
                        FilterType = "peak",
                        Reason = $"Adjust {bandNames[i]} frequencies based on content analysis",
                        Priority = Math.Abs(difference)
                    });
                }
            }
        }

        // Sort EQ suggestions by priority
        suggestion.EqSuggestions = suggestion.EqSuggestions
            .OrderByDescending(eq => eq.Priority)
            .Take(6) // Limit to 6 bands
            .ToList();
    }

    private void AdjustCompressionForDynamics(MixSuggestion suggestion, TrackAnalysis analysis, TrackType trackType)
    {
        if (suggestion.CompressionSuggestion == null)
        {
            suggestion.CompressionSuggestion = new CompressionSuggestion();
        }

        var comp = suggestion.CompressionSuggestion;

        // Adjust based on crest factor
        if (analysis.CrestFactorDb > 20f)
        {
            // Very dynamic - might need more compression
            comp.ThresholdDb = Math.Min(comp.ThresholdDb, analysis.RmsDb + 6f);
            comp.Ratio = Math.Max(comp.Ratio, 4f);
            comp.Reason += " | High dynamic range detected";
        }
        else if (analysis.CrestFactorDb < 8f)
        {
            // Already compressed - use lighter settings
            comp.Ratio = Math.Min(comp.Ratio, 2f);
            comp.ThresholdDb = analysis.RmsDb + 10f;
            comp.Reason += " | Already compressed, using lighter settings";
        }

        // Adjust attack based on transient content
        if (trackType == TrackType.Kick || trackType == TrackType.Snare)
        {
            // Drums need faster attack to catch transients, or slower to let them through
            if (analysis.CrestFactorDb > 15f)
            {
                comp.AttackMs = Math.Max(comp.AttackMs, 30f); // Let transients through
                comp.Reason += " | Slower attack to preserve transients";
            }
        }

        // Calculate makeup gain
        float expectedReduction = (comp.ThresholdDb - analysis.RmsDb) * (1f - 1f / comp.Ratio);
        comp.MakeupGainDb = Math.Max(0, -expectedReduction * 0.5f);
    }

    private List<FrequencyConflict> DetectFrequencyConflicts(List<TrackAnalysis> analyses)
    {
        var conflicts = new List<FrequencyConflict>();

        for (int i = 0; i < analyses.Count; i++)
        {
            for (int j = i + 1; j < analyses.Count; j++)
            {
                var track1 = analyses[i];
                var track2 = analyses[j];

                // Check each frequency band for conflicts
                CheckBandConflict(conflicts, track1, track2, "Low", track1.LowEnergy, track2.LowEnergy, 80f);
                CheckBandConflict(conflicts, track1, track2, "Low-Mid", track1.LowMidEnergy, track2.LowMidEnergy, 350f);
                CheckBandConflict(conflicts, track1, track2, "Mid", track1.MidEnergy, track2.MidEnergy, 1000f);
                CheckBandConflict(conflicts, track1, track2, "High-Mid", track1.HighMidEnergy, track2.HighMidEnergy, 3500f);
            }
        }

        return conflicts.OrderByDescending(c => c.Severity).ToList();
    }

    private void CheckBandConflict(
        List<FrequencyConflict> conflicts,
        TrackAnalysis track1,
        TrackAnalysis track2,
        string bandName,
        float energy1,
        float energy2,
        float centerFreq)
    {
        // Conflict detected when both tracks have significant energy in the same band
        const float threshold = 0.5f;
        if (energy1 > threshold && energy2 > threshold)
        {
            float severity = (energy1 + energy2) / 2f;

            // Some conflicts are expected (bass and kick both have low end)
            if (bandName == "Low" &&
                (track1.DetectedType == TrackType.Bass || track1.DetectedType == TrackType.Kick) &&
                (track2.DetectedType == TrackType.Bass || track2.DetectedType == TrackType.Kick))
            {
                severity *= 0.8f; // Reduce severity for expected conflicts
            }

            if (severity > 0.4f)
            {
                string resolution = GenerateConflictResolution(track1, track2, bandName, centerFreq);

                conflicts.Add(new FrequencyConflict
                {
                    Track1 = track1.TrackName,
                    Track2 = track2.TrackName,
                    FrequencyRange = bandName,
                    CenterFrequency = centerFreq,
                    Severity = severity,
                    Resolution = resolution
                });
            }
        }
    }

    private string GenerateConflictResolution(TrackAnalysis track1, TrackAnalysis track2, string band, float centerFreq)
    {
        // Determine which track should "own" this frequency range
        var priorityTrack = DeterminePriorityTrack(track1.DetectedType, track2.DetectedType, band);

        if (priorityTrack == track1.TrackName)
        {
            return $"Cut {centerFreq:F0}Hz on '{track2.TrackName}' by 2-4dB to make room for '{track1.TrackName}'";
        }
        else if (priorityTrack == track2.TrackName)
        {
            return $"Cut {centerFreq:F0}Hz on '{track1.TrackName}' by 2-4dB to make room for '{track2.TrackName}'";
        }
        else
        {
            return $"Consider sidechaining or dynamic EQ between '{track1.TrackName}' and '{track2.TrackName}' around {centerFreq:F0}Hz";
        }
    }

    private string DeterminePriorityTrack(TrackType type1, TrackType type2, string band)
    {
        // Priority rules based on band and track type
        var priorities = band switch
        {
            "Low" => new[] { TrackType.Kick, TrackType.Bass },
            "Low-Mid" => new[] { TrackType.Bass, TrackType.ElectricGuitar, TrackType.Piano },
            "Mid" => new[] { TrackType.LeadVocal, TrackType.ElectricGuitar, TrackType.Piano },
            "High-Mid" => new[] { TrackType.LeadVocal, TrackType.Snare, TrackType.AcousticGuitar },
            _ => Array.Empty<TrackType>()
        };

        for (int i = 0; i < priorities.Length; i++)
        {
            if (type1 == priorities[i]) return "track1";
            if (type2 == priorities[i]) return "track2";
        }

        return "";
    }

    private void AdjustForConflict(MixSuggestion suggestion, FrequencyConflict conflict, string trackName)
    {
        // If this track should yield, add a cut
        if (!conflict.Resolution.Contains($"'{trackName}'") ||
            !conflict.Resolution.Contains("make room for"))
        {
            return;
        }

        // Check if we should cut
        if (conflict.Resolution.Contains($"Cut") && conflict.Resolution.Contains($"'{trackName}'"))
        {
            suggestion.EqSuggestions.Add(new EqSuggestion
            {
                Frequency = conflict.CenterFrequency,
                GainDb = -3f * conflict.Severity,
                Q = 1.5f,
                FilterType = "peak",
                Reason = $"Reduce conflict with another track in {conflict.FrequencyRange} range",
                Priority = conflict.Severity
            });
        }
    }

    private void BalanceLevels(List<MixSuggestion> suggestions, List<TrackAnalysis> analyses, MixGenre genre)
    {
        // Genre-specific level targets (relative to mix)
        var levelTargets = GetLevelTargets(genre);

        foreach (var suggestion in suggestions)
        {
            var analysis = analyses.FirstOrDefault(a => a.TrackName == suggestion.TrackName);
            if (analysis == null) continue;

            if (levelTargets.TryGetValue(suggestion.TrackType, out var targetDb))
            {
                float currentRms = analysis.RmsDb;
                float adjustment = targetDb - currentRms;

                // Limit adjustment range
                adjustment = Math.Clamp(adjustment, -12f, 12f);

                suggestion.LevelSuggestion = new LevelSuggestion
                {
                    LevelDb = adjustment,
                    Reference = "mix average",
                    Reason = $"Balance {suggestion.TrackType} relative to mix"
                };
            }
        }
    }

    private Dictionary<TrackType, float> GetLevelTargets(MixGenre genre)
    {
        return genre switch
        {
            MixGenre.Pop => new Dictionary<TrackType, float>
            {
                { TrackType.LeadVocal, -10f },
                { TrackType.Kick, -14f },
                { TrackType.Snare, -14f },
                { TrackType.Bass, -14f },
                { TrackType.ElectricGuitar, -18f },
                { TrackType.Piano, -18f },
                { TrackType.Synth, -20f },
                { TrackType.BackingVocal, -20f },
                { TrackType.Pad, -24f }
            },
            MixGenre.Rock => new Dictionary<TrackType, float>
            {
                { TrackType.LeadVocal, -12f },
                { TrackType.Kick, -12f },
                { TrackType.Snare, -12f },
                { TrackType.Bass, -14f },
                { TrackType.ElectricGuitar, -14f },
                { TrackType.Drums, -14f },
                { TrackType.BackingVocal, -20f }
            },
            MixGenre.Electronic => new Dictionary<TrackType, float>
            {
                { TrackType.Kick, -10f },
                { TrackType.Bass, -12f },
                { TrackType.Synth, -14f },
                { TrackType.LeadVocal, -14f },
                { TrackType.Pad, -20f },
                { TrackType.HiHat, -20f }
            },
            MixGenre.HipHop => new Dictionary<TrackType, float>
            {
                { TrackType.LeadVocal, -10f },
                { TrackType.Kick, -12f },
                { TrackType.Bass, -12f },
                { TrackType.Snare, -14f },
                { TrackType.HiHat, -18f },
                { TrackType.Synth, -18f }
            },
            _ => new Dictionary<TrackType, float>
            {
                { TrackType.LeadVocal, -12f },
                { TrackType.Kick, -14f },
                { TrackType.Bass, -14f },
                { TrackType.Snare, -14f }
            }
        };
    }

    private float[] AnalyzeSpectrum(float[] samples, int sampleRate, int channels)
    {
        // Simplified spectral analysis using band-pass energy estimation
        float[] bandEnergies = new float[5];

        if (samples.Length < 1024) return bandEnergies;

        // Convert to mono if stereo
        float[] mono = ConvertToMono(samples, channels);

        // Simple energy calculation per band using first-order filters
        // This is a simplified approach - a full FFT would be more accurate

        // Band boundaries: 20-200, 200-500, 500-2000, 2000-6000, 6000-20000 Hz
        float[] lowFreqs = { 20f, 200f, 500f, 2000f, 6000f };
        float[] highFreqs = { 200f, 500f, 2000f, 6000f, 20000f };

        for (int band = 0; band < 5; band++)
        {
            // Simple bandpass using state variable filter approximation
            float energy = EstimateBandEnergy(mono, sampleRate, lowFreqs[band], highFreqs[band]);
            bandEnergies[band] = energy;
        }

        // Normalize
        float maxEnergy = bandEnergies.Max();
        if (maxEnergy > 0)
        {
            for (int i = 0; i < 5; i++)
            {
                bandEnergies[i] /= maxEnergy;
            }
        }

        return bandEnergies;
    }

    private float EstimateBandEnergy(float[] samples, int sampleRate, float lowFreq, float highFreq)
    {
        // Simple energy estimation using running filter
        float centerFreq = (lowFreq + highFreq) / 2f;
        float bandwidth = highFreq - lowFreq;

        // State variable filter coefficients (simplified)
        float f = 2f * MathF.Sin(MathF.PI * centerFreq / sampleRate);
        float q = centerFreq / bandwidth;

        float low = 0, band = 0;
        float energy = 0;

        int step = Math.Max(1, samples.Length / 4096); // Sample subset for performance

        for (int i = 0; i < samples.Length; i += step)
        {
            float input = samples[i];

            // State variable filter
            low += f * band;
            float high = input - low - (1f / q) * band;
            band += f * high;

            energy += band * band;
        }

        return MathF.Sqrt(energy / (samples.Length / step));
    }

    private float[] ConvertToMono(float[] samples, int channels)
    {
        if (channels == 1) return samples;

        var mono = new float[samples.Length / channels];
        for (int i = 0; i < mono.Length; i++)
        {
            float sum = 0;
            for (int ch = 0; ch < channels; ch++)
            {
                sum += samples[i * channels + ch];
            }
            mono[i] = sum / channels;
        }

        return mono;
    }

    private float CalculateStereoCorrelation(float[] samples)
    {
        if (samples.Length < 4) return 1f;

        float sumLR = 0, sumL2 = 0, sumR2 = 0;
        int frameCount = samples.Length / 2;

        for (int i = 0; i < frameCount; i++)
        {
            float left = samples[i * 2];
            float right = samples[i * 2 + 1];

            sumLR += left * right;
            sumL2 += left * left;
            sumR2 += right * right;
        }

        float denominator = MathF.Sqrt(sumL2 * sumR2);
        if (denominator < 1e-10f) return 1f;

        return sumLR / denominator;
    }

    private float CalculateSpectralCentroid(float[] bandEnergies)
    {
        float[] centerFreqs = { 100f, 350f, 1000f, 4000f, 13000f };
        float weightedSum = 0;
        float energySum = 0;

        for (int i = 0; i < 5; i++)
        {
            weightedSum += centerFreqs[i] * bandEnergies[i];
            energySum += bandEnergies[i];
        }

        return energySum > 0 ? weightedSum / energySum : 1000f;
    }

    private TrackType DetectTrackType(TrackAnalysis analysis)
    {
        // Heuristic-based track type detection
        float lowRatio = analysis.LowEnergy / (analysis.MidEnergy + 0.01f);
        float highRatio = analysis.HighEnergy / (analysis.MidEnergy + 0.01f);

        // Kick: very strong low end, short transients
        if (analysis.LowEnergy > 0.8f && analysis.MidEnergy < 0.3f && analysis.CrestFactorDb > 12f)
        {
            return TrackType.Kick;
        }

        // Bass: strong low and low-mid, weak highs
        if (analysis.LowEnergy > 0.6f && analysis.LowMidEnergy > 0.4f && analysis.HighEnergy < 0.2f)
        {
            return TrackType.Bass;
        }

        // Vocals: strong mid and high-mid, moderate dynamics
        if (analysis.MidEnergy > 0.5f && analysis.HighMidEnergy > 0.4f &&
            analysis.CrestFactorDb > 6f && analysis.CrestFactorDb < 18f)
        {
            return TrackType.LeadVocal;
        }

        // Hi-hat: very strong highs, weak lows
        if (analysis.HighEnergy > 0.7f && analysis.LowEnergy < 0.1f)
        {
            return TrackType.HiHat;
        }

        // Snare: mid-focused with transients
        if (analysis.MidEnergy > 0.4f && analysis.HighMidEnergy > 0.4f && analysis.CrestFactorDb > 10f)
        {
            return TrackType.Snare;
        }

        // Pad: smooth dynamics, full spectrum
        if (analysis.CrestFactorDb < 8f && analysis.StereoCorrelation < 0.9f)
        {
            return TrackType.Pad;
        }

        // Guitar: mid-heavy
        if (analysis.MidEnergy > 0.5f && analysis.HighMidEnergy > 0.3f)
        {
            return TrackType.ElectricGuitar;
        }

        // Synth: variable but often bright
        if (analysis.SpectralCentroid > 2000f)
        {
            return TrackType.Synth;
        }

        return TrackType.Generic;
    }

    private float CalculateConfidence(TrackAnalysis analysis, TrackType trackType)
    {
        if (!_idealSpectralProfiles.TryGetValue(trackType, out var idealProfile))
        {
            return 0.3f;
        }

        float[] actualProfile = { analysis.LowEnergy, analysis.LowMidEnergy, analysis.MidEnergy, analysis.HighMidEnergy, analysis.HighEnergy };

        float sumError = 0;
        for (int i = 0; i < 5; i++)
        {
            sumError += Math.Abs(idealProfile[i] - actualProfile[i]);
        }

        // Convert error to confidence (0-1)
        float confidence = 1f - (sumError / 5f);
        return Math.Clamp(confidence, 0.2f, 0.95f);
    }

    private void ApplyTemplate(MixSuggestion suggestion, MixTemplate template)
    {
        suggestion.EqSuggestions = template.EqBands.Select(b => new EqSuggestion
        {
            Frequency = b.frequency,
            GainDb = b.gain,
            Q = b.q,
            FilterType = b.filterType,
            Reason = b.reason
        }).ToList();

        suggestion.CompressionSuggestion = new CompressionSuggestion
        {
            ThresholdDb = template.CompThreshold,
            Ratio = template.CompRatio,
            AttackMs = template.CompAttack,
            ReleaseMs = template.CompRelease,
            KneeDb = template.CompKnee,
            Style = template.CompStyle,
            Reason = template.CompReason
        };

        suggestion.PanningSuggestion = new PanningSuggestion
        {
            Position = template.PanPosition,
            Width = template.StereoWidth,
            Reason = template.PanReason
        };

        suggestion.AdditionalSuggestions = template.AdditionalTips;
    }

    private Dictionary<(TrackType, MixGenre), MixTemplate> InitializeTemplates()
    {
        var templates = new Dictionary<(TrackType, MixGenre), MixTemplate>();

        // Lead Vocal - Pop
        templates[(TrackType.LeadVocal, MixGenre.Pop)] = new MixTemplate
        {
            EqBands = new[]
            {
                (80f, -6f, 1.5f, "highpass", "Remove rumble and plosives"),
                (200f, -2f, 1f, "peak", "Reduce muddiness"),
                (3000f, 2f, 1.2f, "peak", "Add presence"),
                (10000f, 1.5f, 0.7f, "highshelf", "Add air")
            },
            CompThreshold = -18f, CompRatio = 4f, CompAttack = 10f, CompRelease = 100f, CompKnee = 3f,
            CompStyle = "transparent", CompReason = "Control dynamics while maintaining natural sound",
            PanPosition = 0f, StereoWidth = 0.3f, PanReason = "Keep lead vocal centered",
            AdditionalTips = new List<string> { "Consider de-esser around 5-8kHz", "Light reverb for depth" }
        };

        // Kick - Pop/Electronic
        templates[(TrackType.Kick, MixGenre.Pop)] = new MixTemplate
        {
            EqBands = new[]
            {
                (30f, -12f, 1f, "highpass", "Remove sub-rumble"),
                (60f, 3f, 1.2f, "peak", "Add sub weight"),
                (400f, -4f, 1.5f, "peak", "Remove boxiness"),
                (3500f, 2f, 2f, "peak", "Add click/attack")
            },
            CompThreshold = -12f, CompRatio = 4f, CompAttack = 20f, CompRelease = 50f, CompKnee = 0f,
            CompStyle = "punchy", CompReason = "Consistent punch with transient preservation",
            PanPosition = 0f, StereoWidth = 0f, PanReason = "Keep kick centered for club systems",
            AdditionalTips = new List<string> { "Consider parallel compression for weight", "Sidechain bass to kick" }
        };

        templates[(TrackType.Kick, MixGenre.Electronic)] = new MixTemplate
        {
            EqBands = new[]
            {
                (25f, -12f, 1f, "highpass", "Remove sub-rumble"),
                (50f, 4f, 1f, "peak", "Strong sub bass"),
                (100f, -2f, 1.5f, "peak", "Control low-mid"),
                (5000f, 3f, 2f, "peak", "Add click")
            },
            CompThreshold = -10f, CompRatio = 6f, CompAttack = 5f, CompRelease = 40f, CompKnee = 0f,
            CompStyle = "aggressive", CompReason = "Tight, punchy electronic kick",
            PanPosition = 0f, StereoWidth = 0f, PanReason = "Mono for club compatibility",
            AdditionalTips = new List<string> { "Layer with sub sine for low end", "Careful with phase between layers" }
        };

        // Bass
        templates[(TrackType.Bass, MixGenre.Pop)] = new MixTemplate
        {
            EqBands = new[]
            {
                (40f, -6f, 1f, "highpass", "Remove sub-rumble"),
                (80f, 2f, 1.2f, "peak", "Add fundamental"),
                (250f, -2f, 1f, "peak", "Reduce mud"),
                (800f, 1f, 2f, "peak", "Add definition")
            },
            CompThreshold = -16f, CompRatio = 4f, CompAttack = 15f, CompRelease = 80f, CompKnee = 3f,
            CompStyle = "smooth", CompReason = "Even bass level throughout",
            PanPosition = 0f, StereoWidth = 0f, PanReason = "Keep bass centered",
            AdditionalTips = new List<string> { "Consider multiband compression for tighter low end" }
        };

        // Snare
        templates[(TrackType.Snare, MixGenre.Pop)] = new MixTemplate
        {
            EqBands = new[]
            {
                (80f, -6f, 1f, "highpass", "Remove bleed"),
                (200f, 2f, 1.5f, "peak", "Add body"),
                (900f, -2f, 2f, "peak", "Reduce boxiness"),
                (5000f, 3f, 1.5f, "peak", "Add crack")
            },
            CompThreshold = -14f, CompRatio = 4f, CompAttack = 5f, CompRelease = 80f, CompKnee = 0f,
            CompStyle = "punchy", CompReason = "Consistent snare hits",
            PanPosition = 0f, StereoWidth = 0.2f, PanReason = "Keep snare near center",
            AdditionalTips = new List<string> { "Parallel compression for fatness", "Gate to reduce bleed" }
        };

        // Electric Guitar
        templates[(TrackType.ElectricGuitar, MixGenre.Rock)] = new MixTemplate
        {
            EqBands = new[]
            {
                (100f, -6f, 1f, "highpass", "Remove rumble"),
                (400f, -2f, 1.5f, "peak", "Reduce mud"),
                (2500f, 2f, 1.2f, "peak", "Add bite"),
                (8000f, -2f, 1f, "peak", "Reduce harshness")
            },
            CompThreshold = -16f, CompRatio = 3f, CompAttack = 20f, CompRelease = 150f, CompKnee = 6f,
            CompStyle = "gentle", CompReason = "Gentle compression, amp already compressed",
            PanPosition = 0.6f, StereoWidth = 0.8f, PanReason = "Pan guitars for width",
            AdditionalTips = new List<string> { "Double-track and pan L/R", "Consider high-shelf boost for presence" }
        };

        // Piano
        templates[(TrackType.Piano, MixGenre.Pop)] = new MixTemplate
        {
            EqBands = new[]
            {
                (80f, -6f, 1f, "highpass", "Remove rumble"),
                (300f, -2f, 1f, "peak", "Reduce muddiness"),
                (3000f, 1.5f, 1.2f, "peak", "Add clarity"),
                (10000f, 1f, 0.7f, "highshelf", "Add sparkle")
            },
            CompThreshold = -18f, CompRatio = 2.5f, CompAttack = 15f, CompRelease = 120f, CompKnee = 6f,
            CompStyle = "transparent", CompReason = "Maintain dynamics while evening out levels",
            PanPosition = 0f, StereoWidth = 1f, PanReason = "Wide stereo piano",
            AdditionalTips = new List<string> { "Use stereo recording if possible", "Room reverb for natural space" }
        };

        // Synth
        templates[(TrackType.Synth, MixGenre.Electronic)] = new MixTemplate
        {
            EqBands = new[]
            {
                (150f, -6f, 1f, "highpass", "Leave room for bass"),
                (500f, -2f, 1f, "peak", "Reduce mud"),
                (5000f, 2f, 1.2f, "peak", "Add brightness"),
                (12000f, 1f, 0.7f, "highshelf", "Add air")
            },
            CompThreshold = -14f, CompRatio = 3f, CompAttack = 10f, CompRelease = 80f, CompKnee = 3f,
            CompStyle = "balanced", CompReason = "Control peaks while maintaining movement",
            PanPosition = 0.3f, StereoWidth = 0.9f, PanReason = "Wide synth pad",
            AdditionalTips = new List<string> { "Sidechain to kick for pumping effect", "Automate filter for movement" }
        };

        // Pad
        templates[(TrackType.Pad, MixGenre.Pop)] = new MixTemplate
        {
            EqBands = new[]
            {
                (200f, -6f, 0.7f, "highpass", "Leave room for other elements"),
                (400f, -3f, 1f, "peak", "Reduce mud"),
                (8000f, 1f, 0.7f, "highshelf", "Gentle brightness")
            },
            CompThreshold = -20f, CompRatio = 2f, CompAttack = 30f, CompRelease = 200f, CompKnee = 6f,
            CompStyle = "gentle", CompReason = "Minimal compression for ambient elements",
            PanPosition = 0f, StereoWidth = 1f, PanReason = "Wide stereo for ambience",
            AdditionalTips = new List<string> { "Keep level low to sit in background", "Long reverb for depth" }
        };

        // Hi-hat
        templates[(TrackType.HiHat, MixGenre.Pop)] = new MixTemplate
        {
            EqBands = new[]
            {
                (400f, -12f, 0.7f, "highpass", "Remove low content"),
                (6000f, 2f, 1.5f, "peak", "Add sizzle"),
                (12000f, 1f, 0.7f, "highshelf", "Add air")
            },
            CompThreshold = -16f, CompRatio = 2f, CompAttack = 1f, CompRelease = 40f, CompKnee = 0f,
            CompStyle = "fast", CompReason = "Tame peaks on hats",
            PanPosition = 0.5f, StereoWidth = 0.5f, PanReason = "Pan slightly for realism",
            AdditionalTips = new List<string> { "Gate to tighten", "Consider transient shaping" }
        };

        return templates;
    }

    private Dictionary<TrackType, float[]> InitializeSpectralProfiles()
    {
        // Ideal spectral profiles for each track type [low, low-mid, mid, high-mid, high]
        return new Dictionary<TrackType, float[]>
        {
            { TrackType.LeadVocal, new[] { 0.1f, 0.3f, 0.8f, 0.9f, 0.5f } },
            { TrackType.BackingVocal, new[] { 0.1f, 0.2f, 0.7f, 0.8f, 0.4f } },
            { TrackType.Kick, new[] { 0.9f, 0.4f, 0.2f, 0.3f, 0.1f } },
            { TrackType.Snare, new[] { 0.2f, 0.5f, 0.6f, 0.8f, 0.5f } },
            { TrackType.HiHat, new[] { 0f, 0.1f, 0.2f, 0.6f, 0.9f } },
            { TrackType.Drums, new[] { 0.5f, 0.4f, 0.5f, 0.6f, 0.6f } },
            { TrackType.Bass, new[] { 0.8f, 0.6f, 0.3f, 0.1f, 0.05f } },
            { TrackType.ElectricGuitar, new[] { 0.2f, 0.5f, 0.8f, 0.7f, 0.4f } },
            { TrackType.AcousticGuitar, new[] { 0.3f, 0.4f, 0.7f, 0.8f, 0.5f } },
            { TrackType.Piano, new[] { 0.4f, 0.5f, 0.6f, 0.7f, 0.6f } },
            { TrackType.Synth, new[] { 0.3f, 0.4f, 0.6f, 0.7f, 0.7f } },
            { TrackType.Strings, new[] { 0.3f, 0.5f, 0.7f, 0.7f, 0.5f } },
            { TrackType.Brass, new[] { 0.2f, 0.4f, 0.7f, 0.8f, 0.4f } },
            { TrackType.Pad, new[] { 0.3f, 0.4f, 0.5f, 0.5f, 0.5f } },
            { TrackType.Generic, new[] { 0.4f, 0.4f, 0.5f, 0.5f, 0.4f } }
        };
    }

    /// <summary>
    /// Template for mix settings.
    /// </summary>
    private class MixTemplate
    {
        public (float frequency, float gain, float q, string filterType, string reason)[] EqBands { get; set; } = Array.Empty<(float, float, float, string, string)>();
        public float CompThreshold { get; set; }
        public float CompRatio { get; set; }
        public float CompAttack { get; set; }
        public float CompRelease { get; set; }
        public float CompKnee { get; set; }
        public string CompStyle { get; set; } = "";
        public string CompReason { get; set; } = "";
        public float PanPosition { get; set; }
        public float StereoWidth { get; set; }
        public string PanReason { get; set; } = "";
        public List<string> AdditionalTips { get; set; } = new();
    }
}

/// <summary>
/// Result of comparing before/after mix.
/// </summary>
public class MixComparisonResult
{
    /// <summary>Change in dynamic range (crest factor) in dB.</summary>
    public float DynamicRangeChange { get; set; }

    /// <summary>Change in overall level in dB.</summary>
    public float LevelChange { get; set; }

    /// <summary>Change in spectral centroid (brightness) in Hz.</summary>
    public float BrightnessChange { get; set; }

    /// <summary>Change in low-end energy.</summary>
    public float LowEndChange { get; set; }

    /// <summary>Whether clarity appears improved (more mid, less low-mid).</summary>
    public bool ClarityImproved { get; set; }

    /// <summary>Change in stereo correlation.</summary>
    public float StereoWidthChange { get; set; }

    /// <summary>
    /// Gets a summary of the comparison.
    /// </summary>
    public string GetSummary()
    {
        var parts = new List<string>();

        if (Math.Abs(DynamicRangeChange) > 1f)
        {
            parts.Add(DynamicRangeChange > 0 ? "More dynamic" : "More compressed");
        }

        if (Math.Abs(BrightnessChange) > 500f)
        {
            parts.Add(BrightnessChange > 0 ? "Brighter" : "Warmer");
        }

        if (ClarityImproved)
        {
            parts.Add("Improved clarity");
        }

        if (Math.Abs(StereoWidthChange) > 0.1f)
        {
            parts.Add(StereoWidthChange > 0 ? "Wider stereo" : "Narrower stereo");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "Minimal change";
    }
}
