// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: AI mastering assistant.

using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;

namespace MusicEngine.Core.AI;

/// <summary>
/// Target platform presets for mastering.
/// </summary>
public enum MasteringTarget
{
    /// <summary>Streaming platforms (Spotify, Apple Music) - Target: -14 LUFS.</summary>
    Streaming,
    /// <summary>CD release - Target: -9 LUFS.</summary>
    CD,
    /// <summary>Broadcast (TV/Radio) - Target: -24 LUFS.</summary>
    Broadcast,
    /// <summary>Club/DJ use - Target: -6 LUFS, emphasis on low end.</summary>
    Club,
    /// <summary>YouTube/Video - Target: -14 LUFS.</summary>
    YouTube,
    /// <summary>Podcast/Spoken word - Target: -16 LUFS.</summary>
    Podcast,
    /// <summary>Vinyl preparation - Careful with sub-bass and stereo width.</summary>
    Vinyl,
    /// <summary>Custom settings.</summary>
    Custom
}

/// <summary>
/// Mastering chain effect configuration.
/// </summary>
public class MasteringEffectConfig
{
    /// <summary>Effect type name.</summary>
    public string EffectType { get; set; } = "";

    /// <summary>Whether this effect is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Effect parameters.</summary>
    public Dictionary<string, float> Parameters { get; set; } = new();

    /// <summary>Reason for including this effect.</summary>
    public string Reason { get; set; } = "";

    /// <summary>Processing order (lower = earlier in chain).</summary>
    public int Order { get; set; }
}

/// <summary>
/// EQ band configuration for mastering.
/// </summary>
public class MasteringEqBand
{
    /// <summary>Center frequency in Hz.</summary>
    public float Frequency { get; set; }

    /// <summary>Gain in dB.</summary>
    public float GainDb { get; set; }

    /// <summary>Q factor (bandwidth).</summary>
    public float Q { get; set; } = 1.0f;

    /// <summary>Filter type.</summary>
    public string FilterType { get; set; } = "peak";
}

/// <summary>
/// Analysis result for mastering.
/// </summary>
public class MasteringAnalysis
{
    /// <summary>Integrated loudness in LUFS.</summary>
    public float IntegratedLufs { get; set; }

    /// <summary>Short-term loudness in LUFS.</summary>
    public float ShortTermLufs { get; set; }

    /// <summary>Momentary loudness in LUFS.</summary>
    public float MomentaryLufs { get; set; }

    /// <summary>True peak level in dBTP.</summary>
    public float TruePeakDbtp { get; set; }

    /// <summary>Loudness range (LRA) in LU.</summary>
    public float LoudnessRange { get; set; }

    /// <summary>Dynamic range in dB.</summary>
    public float DynamicRangeDb { get; set; }

    /// <summary>Stereo correlation (-1 to 1).</summary>
    public float StereoCorrelation { get; set; }

    /// <summary>Spectral balance (normalized bands).</summary>
    public float[] SpectralBalance { get; set; } = new float[5];

    /// <summary>Spectral centroid in Hz.</summary>
    public float SpectralCentroid { get; set; }

    /// <summary>Crest factor (peak/RMS) in dB.</summary>
    public float CrestFactorDb { get; set; }

    /// <summary>Low frequency energy ratio (sub + bass).</summary>
    public float LowEndRatio { get; set; }

    /// <summary>High frequency energy ratio.</summary>
    public float HighEndRatio { get; set; }

    /// <summary>Detected issues and recommendations.</summary>
    public List<string> Issues { get; set; } = new();
}

/// <summary>
/// Complete mastering chain suggestion.
/// </summary>
public class MasteringChain
{
    /// <summary>Analysis of the input audio.</summary>
    public MasteringAnalysis InputAnalysis { get; set; } = new();

    /// <summary>Target platform.</summary>
    public MasteringTarget Target { get; set; }

    /// <summary>Target loudness in LUFS.</summary>
    public float TargetLufs { get; set; }

    /// <summary>EQ settings.</summary>
    public List<MasteringEqBand> EqBands { get; set; } = new();

    /// <summary>Multiband compressor settings.</summary>
    public MultibandCompressorConfig? MultibandCompressor { get; set; }

    /// <summary>Stereo processing settings.</summary>
    public StereoProcessingConfig? StereoProcessing { get; set; }

    /// <summary>Limiter settings.</summary>
    public LimiterConfig? Limiter { get; set; }

    /// <summary>All effects in chain order.</summary>
    public List<MasteringEffectConfig> EffectChain { get; set; } = new();

    /// <summary>Estimated output loudness in LUFS.</summary>
    public float EstimatedOutputLufs { get; set; }

    /// <summary>Overall confidence in the mastering chain (0-1).</summary>
    public float Confidence { get; set; }

    /// <summary>Notes and recommendations.</summary>
    public List<string> Notes { get; set; } = new();
}

/// <summary>
/// Multiband compressor configuration.
/// </summary>
public class MultibandCompressorConfig
{
    /// <summary>Low crossover frequency in Hz.</summary>
    public float CrossoverLow { get; set; } = 200f;

    /// <summary>Mid crossover frequency in Hz.</summary>
    public float CrossoverMid { get; set; } = 2000f;

    /// <summary>High crossover frequency in Hz.</summary>
    public float CrossoverHigh { get; set; } = 8000f;

    /// <summary>Band settings (threshold, ratio, attack, release, gain).</summary>
    public BandCompressorSettings[] Bands { get; set; } = new BandCompressorSettings[4];
}

/// <summary>
/// Single band compressor settings.
/// </summary>
public class BandCompressorSettings
{
    /// <summary>Band name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Threshold in dB.</summary>
    public float ThresholdDb { get; set; } = -20f;

    /// <summary>Compression ratio.</summary>
    public float Ratio { get; set; } = 2f;

    /// <summary>Attack time in ms.</summary>
    public float AttackMs { get; set; } = 10f;

    /// <summary>Release time in ms.</summary>
    public float ReleaseMs { get; set; } = 100f;

    /// <summary>Makeup gain in dB.</summary>
    public float GainDb { get; set; } = 0f;
}

/// <summary>
/// Stereo processing configuration.
/// </summary>
public class StereoProcessingConfig
{
    /// <summary>Stereo width (1.0 = normal, &gt;1 = wider, &lt;1 = narrower).</summary>
    public float Width { get; set; } = 1.0f;

    /// <summary>Low frequency mono width (0 = mono bass, 1 = full stereo).</summary>
    public float LowMonoWidth { get; set; } = 0.5f;

    /// <summary>Crossover frequency for mono bass.</summary>
    public float MonoCrossover { get; set; } = 120f;

    /// <summary>Mid/Side balance.</summary>
    public float MidSideBalance { get; set; } = 0f;
}

/// <summary>
/// Limiter configuration.
/// </summary>
public class LimiterConfig
{
    /// <summary>Ceiling level in dBTP.</summary>
    public float CeilingDbtp { get; set; } = -1.0f;

    /// <summary>Release time in ms.</summary>
    public float ReleaseMs { get; set; } = 100f;

    /// <summary>Lookahead time in ms.</summary>
    public float LookaheadMs { get; set; } = 5f;

    /// <summary>Target gain to reach loudness target.</summary>
    public float TargetGainDb { get; set; } = 0f;
}

/// <summary>
/// Reference track analysis for matching.
/// </summary>
public class ReferenceAnalysis
{
    /// <summary>Reference track name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Full analysis.</summary>
    public MasteringAnalysis Analysis { get; set; } = new();

    /// <summary>EQ curve to match (frequency, gain pairs).</summary>
    public (float frequency, float gainDb)[] MatchingCurve { get; set; } = Array.Empty<(float, float)>();
}

/// <summary>
/// AI-based mastering assistant providing one-click mastering chains and reference matching.
/// Uses heuristic analysis to suggest appropriate processing for different target platforms.
/// </summary>
public class MasteringAssistant
{
    private readonly Dictionary<MasteringTarget, TargetProfile> _targetProfiles;

    /// <summary>
    /// Creates a new mastering assistant.
    /// </summary>
    public MasteringAssistant()
    {
        _targetProfiles = InitializeTargetProfiles();
    }

    /// <summary>
    /// Analyzes audio and generates a complete mastering chain.
    /// </summary>
    /// <param name="samples">Audio samples (interleaved stereo).</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of channels.</param>
    /// <param name="target">Target platform.</param>
    /// <returns>Complete mastering chain with all settings.</returns>
    public MasteringChain CreateMasteringChain(
        float[] samples,
        int sampleRate,
        int channels,
        MasteringTarget target = MasteringTarget.Streaming)
    {
        // Analyze input
        var analysis = AnalyzeAudio(samples, sampleRate, channels);

        // Get target profile
        var profile = _targetProfiles[target];

        // Generate chain
        return GenerateMasteringChain(analysis, profile, target);
    }

    /// <summary>
    /// Analyzes audio for mastering decisions.
    /// </summary>
    public MasteringAnalysis AnalyzeAudio(float[] samples, int sampleRate, int channels)
    {
        var analysis = new MasteringAnalysis();

        if (samples.Length == 0)
        {
            analysis.Issues.Add("No audio data provided");
            return analysis;
        }

        // Loudness analysis
        CalculateLoudness(samples, sampleRate, channels, analysis);

        // Dynamic range analysis
        CalculateDynamics(samples, analysis);

        // Spectral analysis
        AnalyzeSpectrum(samples, sampleRate, channels, analysis);

        // Stereo analysis
        if (channels == 2)
        {
            AnalyzeStereo(samples, analysis);
        }

        // Detect issues
        DetectIssues(analysis);

        return analysis;
    }

    /// <summary>
    /// Analyzes a reference track and creates matching suggestions.
    /// </summary>
    public ReferenceAnalysis AnalyzeReference(float[] samples, int sampleRate, int channels, string name)
    {
        var analysis = AnalyzeAudio(samples, sampleRate, channels);

        return new ReferenceAnalysis
        {
            Name = name,
            Analysis = analysis,
            MatchingCurve = GenerateMatchingCurve(analysis)
        };
    }

    /// <summary>
    /// Generates EQ curve to match a reference track.
    /// </summary>
    public List<MasteringEqBand> MatchReference(MasteringAnalysis source, ReferenceAnalysis reference)
    {
        var bands = new List<MasteringEqBand>();

        float[] sourceBalance = source.SpectralBalance;
        float[] refBalance = reference.Analysis.SpectralBalance;

        float[] centerFreqs = { 60f, 250f, 1000f, 4000f, 12000f };
        string[] filterTypes = { "lowshelf", "peak", "peak", "peak", "highshelf" };
        float[] defaultQ = { 0.7f, 1.0f, 1.0f, 1.0f, 0.7f };

        for (int i = 0; i < 5; i++)
        {
            float diff = refBalance[i] - sourceBalance[i];

            // Only suggest if difference is significant
            if (Math.Abs(diff) > 0.05f)
            {
                // Convert normalized difference to dB (rough approximation)
                float gainDb = diff * 6f;
                gainDb = Math.Clamp(gainDb, -4f, 4f);

                bands.Add(new MasteringEqBand
                {
                    Frequency = centerFreqs[i],
                    GainDb = gainDb,
                    Q = defaultQ[i],
                    FilterType = filterTypes[i]
                });
            }
        }

        // Brightness matching
        float centroidDiff = reference.Analysis.SpectralCentroid - source.SpectralCentroid;
        if (Math.Abs(centroidDiff) > 500f)
        {
            float highShelfGain = centroidDiff > 0 ? 1.5f : -1.5f;
            var existingHighShelf = bands.FirstOrDefault(b => b.Frequency > 8000f);
            if (existingHighShelf != null)
            {
                existingHighShelf.GainDb += highShelfGain;
            }
            else
            {
                bands.Add(new MasteringEqBand
                {
                    Frequency = 10000f,
                    GainDb = highShelfGain,
                    Q = 0.7f,
                    FilterType = "highshelf"
                });
            }
        }

        return bands;
    }

    /// <summary>
    /// Calculates the gain needed to reach target loudness.
    /// </summary>
    public float CalculateGainForTarget(MasteringAnalysis analysis, float targetLufs)
    {
        float currentLufs = analysis.IntegratedLufs;
        float requiredGain = targetLufs - currentLufs;

        // Limit to reasonable range
        return Math.Clamp(requiredGain, -12f, 24f);
    }

    /// <summary>
    /// Gets preset mastering settings for a specific genre.
    /// </summary>
    public MasteringChain GetGenrePreset(string genre, MasteringTarget target)
    {
        var profile = _targetProfiles[target];
        var analysis = new MasteringAnalysis
        {
            // Assume neutral analysis for preset
            IntegratedLufs = -18f,
            SpectralBalance = new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f },
            DynamicRangeDb = 12f,
            StereoCorrelation = 0.8f
        };

        var chain = GenerateMasteringChain(analysis, profile, target);

        // Apply genre-specific adjustments
        ApplyGenreAdjustments(chain, genre.ToLowerInvariant());

        return chain;
    }

    private MasteringChain GenerateMasteringChain(MasteringAnalysis analysis, TargetProfile profile, MasteringTarget target)
    {
        var chain = new MasteringChain
        {
            InputAnalysis = analysis,
            Target = target,
            TargetLufs = profile.TargetLufs,
            Confidence = CalculateConfidence(analysis)
        };

        int order = 0;

        // 1. EQ (tonal balance correction)
        GenerateEqSettings(chain, analysis, profile, order++);

        // 2. Multiband compression (dynamics control per band)
        GenerateMultibandSettings(chain, analysis, profile, order++);

        // 3. Stereo processing
        GenerateStereoSettings(chain, analysis, profile, order++);

        // 4. Single-band glue compression (optional)
        GenerateGlueCompression(chain, analysis, profile, order++);

        // 5. Final EQ/tilt (optional)
        GenerateFinalEq(chain, analysis, profile, order++);

        // 6. Limiting
        GenerateLimiterSettings(chain, analysis, profile, order++);

        // Estimate output loudness
        chain.EstimatedOutputLufs = EstimateOutputLoudness(analysis, chain);

        // Add notes
        GenerateNotes(chain, analysis, profile);

        return chain;
    }

    private void GenerateEqSettings(MasteringChain chain, MasteringAnalysis analysis, TargetProfile profile, int order)
    {
        var bands = new List<MasteringEqBand>();

        // Reference spectral balance
        float[] idealBalance = profile.IdealSpectralBalance;
        float[] centerFreqs = { 60f, 250f, 1000f, 4000f, 12000f };

        for (int i = 0; i < 5; i++)
        {
            float diff = idealBalance[i] - analysis.SpectralBalance[i];

            if (Math.Abs(diff) > 0.1f)
            {
                float gain = diff * 4f; // Subtle correction
                gain = Math.Clamp(gain, -3f, 3f);

                bands.Add(new MasteringEqBand
                {
                    Frequency = centerFreqs[i],
                    GainDb = gain,
                    Q = i == 0 || i == 4 ? 0.7f : 1.0f,
                    FilterType = i == 0 ? "lowshelf" : (i == 4 ? "highshelf" : "peak")
                });
            }
        }

        // High-pass filter for sub-rumble
        if (analysis.LowEndRatio > 0.4f || profile.Target == MasteringTarget.Vinyl)
        {
            bands.Insert(0, new MasteringEqBand
            {
                Frequency = 30f,
                GainDb = 0f,
                Q = 0.7f,
                FilterType = "highpass"
            });
        }

        chain.EqBands = bands;

        if (bands.Count > 0)
        {
            chain.EffectChain.Add(new MasteringEffectConfig
            {
                EffectType = "ParametricEQ",
                Enabled = true,
                Order = order,
                Reason = "Tonal balance correction",
                Parameters = bands.SelectMany((b, idx) => new Dictionary<string, float>
                {
                    { $"Band{idx}_Frequency", b.Frequency },
                    { $"Band{idx}_Gain", b.GainDb },
                    { $"Band{idx}_Q", b.Q }
                }).ToDictionary(kv => kv.Key, kv => kv.Value)
            });
        }
    }

    private void GenerateMultibandSettings(MasteringChain chain, MasteringAnalysis analysis, TargetProfile profile, int order)
    {
        var mb = new MultibandCompressorConfig
        {
            CrossoverLow = profile.CrossoverLow,
            CrossoverMid = profile.CrossoverMid,
            CrossoverHigh = profile.CrossoverHigh,
            Bands = new BandCompressorSettings[4]
        };

        // Low band - tighter control
        mb.Bands[0] = new BandCompressorSettings
        {
            Name = "Low",
            ThresholdDb = -20f + (analysis.LowEndRatio > 0.3f ? 2f : 0f),
            Ratio = profile.Target == MasteringTarget.Club ? 3f : 2f,
            AttackMs = 30f,
            ReleaseMs = 200f,
            GainDb = analysis.LowEndRatio < 0.2f ? 1f : 0f
        };

        // Low-mid band
        mb.Bands[1] = new BandCompressorSettings
        {
            Name = "Low-Mid",
            ThresholdDb = -18f,
            Ratio = 2f,
            AttackMs = 20f,
            ReleaseMs = 150f,
            GainDb = 0f
        };

        // High-mid band
        mb.Bands[2] = new BandCompressorSettings
        {
            Name = "High-Mid",
            ThresholdDb = -16f,
            Ratio = 2f,
            AttackMs = 10f,
            ReleaseMs = 100f,
            GainDb = analysis.SpectralBalance[3] < 0.4f ? 1f : 0f
        };

        // High band
        mb.Bands[3] = new BandCompressorSettings
        {
            Name = "High",
            ThresholdDb = -14f,
            Ratio = 1.5f,
            AttackMs = 5f,
            ReleaseMs = 80f,
            GainDb = analysis.HighEndRatio < 0.2f ? 1.5f : 0f
        };

        chain.MultibandCompressor = mb;

        var mbParams = new Dictionary<string, float>
        {
            { "CrossoverLow", mb.CrossoverLow },
            { "CrossoverMid", mb.CrossoverMid },
            { "CrossoverHigh", mb.CrossoverHigh }
        };

        for (int i = 0; i < 4; i++)
        {
            mbParams[$"Band{i}_Threshold"] = mb.Bands[i].ThresholdDb;
            mbParams[$"Band{i}_Ratio"] = mb.Bands[i].Ratio;
            mbParams[$"Band{i}_Attack"] = mb.Bands[i].AttackMs;
            mbParams[$"Band{i}_Release"] = mb.Bands[i].ReleaseMs;
            mbParams[$"Band{i}_Gain"] = mb.Bands[i].GainDb;
        }

        chain.EffectChain.Add(new MasteringEffectConfig
        {
            EffectType = "MultibandCompressor",
            Enabled = true,
            Order = order,
            Reason = "Per-band dynamics control",
            Parameters = mbParams
        });
    }

    private void GenerateStereoSettings(MasteringChain chain, MasteringAnalysis analysis, TargetProfile profile, int order)
    {
        var stereo = new StereoProcessingConfig
        {
            Width = 1.0f,
            LowMonoWidth = 0.5f,
            MonoCrossover = profile.MonoBassFrequency
        };

        // Adjust width based on correlation
        if (analysis.StereoCorrelation < 0.5f)
        {
            // Already wide or out of phase - narrow slightly
            stereo.Width = 0.9f;
            chain.Notes.Add("Narrowed stereo width due to low correlation");
        }
        else if (analysis.StereoCorrelation > 0.95f)
        {
            // Very narrow/mono - widen
            stereo.Width = profile.Target == MasteringTarget.Club ? 1.0f : 1.1f;
        }

        // Vinyl needs careful stereo handling
        if (profile.Target == MasteringTarget.Vinyl)
        {
            stereo.Width = Math.Min(stereo.Width, 1.0f);
            stereo.LowMonoWidth = 0f; // Full mono bass for vinyl
            stereo.MonoCrossover = 200f;
        }

        chain.StereoProcessing = stereo;

        chain.EffectChain.Add(new MasteringEffectConfig
        {
            EffectType = "StereoWidener",
            Enabled = stereo.Width != 1.0f || stereo.LowMonoWidth < 1.0f,
            Order = order,
            Reason = "Stereo image optimization",
            Parameters = new Dictionary<string, float>
            {
                { "Width", stereo.Width },
                { "LowWidth", stereo.LowMonoWidth },
                { "CrossoverFreq", stereo.MonoCrossover }
            }
        });
    }

    private void GenerateGlueCompression(MasteringChain chain, MasteringAnalysis analysis, TargetProfile profile, int order)
    {
        // Add light glue compression for cohesion
        if (analysis.DynamicRangeDb > 15f)
        {
            chain.EffectChain.Add(new MasteringEffectConfig
            {
                EffectType = "Compressor",
                Enabled = true,
                Order = order,
                Reason = "Glue compression for cohesion",
                Parameters = new Dictionary<string, float>
                {
                    { "Threshold", -16f },
                    { "Ratio", 2f },
                    { "Attack", 30f },
                    { "Release", 200f },
                    { "Knee", 6f },
                    { "MakeupGain", 1f }
                }
            });
        }
    }

    private void GenerateFinalEq(MasteringChain chain, MasteringAnalysis analysis, TargetProfile profile, int order)
    {
        // Optional tilt EQ for final character
        if (profile.TiltAmount != 0f)
        {
            chain.EffectChain.Add(new MasteringEffectConfig
            {
                EffectType = "TiltEQ",
                Enabled = true,
                Order = order,
                Reason = "Final tonal character",
                Parameters = new Dictionary<string, float>
                {
                    { "Tilt", profile.TiltAmount },
                    { "CenterFrequency", 800f }
                }
            });
        }
    }

    private void GenerateLimiterSettings(MasteringChain chain, MasteringAnalysis analysis, TargetProfile profile, int order)
    {
        // Calculate gain needed to reach target
        float gainNeeded = CalculateGainForTarget(analysis, profile.TargetLufs);

        // Account for processing in chain (rough estimate: -2dB from compression)
        gainNeeded += 2f;

        var limiter = new LimiterConfig
        {
            CeilingDbtp = profile.TruePeakCeiling,
            ReleaseMs = profile.LimiterRelease,
            LookaheadMs = 5f,
            TargetGainDb = Math.Max(0, gainNeeded)
        };

        chain.Limiter = limiter;

        chain.EffectChain.Add(new MasteringEffectConfig
        {
            EffectType = "Limiter",
            Enabled = true,
            Order = order,
            Reason = $"Final limiting to {profile.TruePeakCeiling:F1} dBTP",
            Parameters = new Dictionary<string, float>
            {
                { "Ceiling", limiter.CeilingDbtp },
                { "Release", limiter.ReleaseMs },
                { "Lookahead", limiter.LookaheadMs },
                { "InputGain", limiter.TargetGainDb }
            }
        });
    }

    private void CalculateLoudness(float[] samples, int sampleRate, int channels, MasteringAnalysis analysis)
    {
        // Simplified loudness calculation (K-weighted approximation)
        float sumSquared = 0;
        float peak = 0;
        int frameCount = samples.Length / channels;

        // Apply simplified K-weighting (high-shelf boost, high-pass)
        for (int i = 0; i < frameCount; i++)
        {
            float mono = 0;
            for (int ch = 0; ch < channels; ch++)
            {
                float sample = samples[i * channels + ch];
                mono += sample;
                float abs = Math.Abs(sample);
                if (abs > peak) peak = abs;
            }
            mono /= channels;
            sumSquared += mono * mono;
        }

        float rms = MathF.Sqrt(sumSquared / frameCount);

        // Convert to LUFS (approximate)
        analysis.IntegratedLufs = -0.691f + 20f * MathF.Log10(Math.Max(rms, 1e-10f));
        analysis.ShortTermLufs = analysis.IntegratedLufs; // Simplified
        analysis.MomentaryLufs = analysis.IntegratedLufs; // Simplified
        analysis.TruePeakDbtp = 20f * MathF.Log10(Math.Max(peak * 1.1f, 1e-10f)); // Approximate true peak

        // Estimate loudness range
        analysis.LoudnessRange = Math.Min(analysis.DynamicRangeDb, 20f);
    }

    private void CalculateDynamics(float[] samples, MasteringAnalysis analysis)
    {
        if (samples.Length == 0) return;

        float peak = 0;
        float sumSquared = 0;

        // Sample a subset for performance
        int step = Math.Max(1, samples.Length / 10000);
        int count = 0;

        for (int i = 0; i < samples.Length; i += step)
        {
            float abs = Math.Abs(samples[i]);
            if (abs > peak) peak = abs;
            sumSquared += samples[i] * samples[i];
            count++;
        }

        float rms = MathF.Sqrt(sumSquared / count);
        float peakDb = 20f * MathF.Log10(Math.Max(peak, 1e-10f));
        float rmsDb = 20f * MathF.Log10(Math.Max(rms, 1e-10f));

        analysis.CrestFactorDb = peakDb - rmsDb;
        analysis.DynamicRangeDb = analysis.CrestFactorDb; // Simplified
    }

    private void AnalyzeSpectrum(float[] samples, int sampleRate, int channels, MasteringAnalysis analysis)
    {
        // Convert to mono
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

        // Analyze energy in bands
        float[] bandEnergies = new float[5];
        float[] lowFreqs = { 20f, 200f, 500f, 2000f, 6000f };
        float[] highFreqs = { 200f, 500f, 2000f, 6000f, 20000f };

        for (int band = 0; band < 5; band++)
        {
            bandEnergies[band] = EstimateBandEnergy(mono, sampleRate, lowFreqs[band], highFreqs[band]);
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

        analysis.SpectralBalance = bandEnergies;
        analysis.LowEndRatio = (bandEnergies[0] + bandEnergies[1] * 0.5f) / 1.5f;
        analysis.HighEndRatio = bandEnergies[4];

        // Spectral centroid
        float[] centerFreqs = { 100f, 350f, 1000f, 4000f, 13000f };
        float weightedSum = 0, energySum = 0;
        for (int i = 0; i < 5; i++)
        {
            weightedSum += centerFreqs[i] * bandEnergies[i];
            energySum += bandEnergies[i];
        }
        analysis.SpectralCentroid = energySum > 0 ? weightedSum / energySum : 1000f;
    }

    private float EstimateBandEnergy(float[] samples, int sampleRate, float lowFreq, float highFreq)
    {
        float centerFreq = (lowFreq + highFreq) / 2f;
        float bandwidth = highFreq - lowFreq;

        float f = 2f * MathF.Sin(MathF.PI * centerFreq / sampleRate);
        float q = centerFreq / bandwidth;

        float low = 0, band = 0;
        float energy = 0;

        int step = Math.Max(1, samples.Length / 4096);

        for (int i = 0; i < samples.Length; i += step)
        {
            float input = samples[i];
            low += f * band;
            float high = input - low - (1f / q) * band;
            band += f * high;
            energy += band * band;
        }

        return MathF.Sqrt(energy / (samples.Length / step));
    }

    private void AnalyzeStereo(float[] samples, MasteringAnalysis analysis)
    {
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

        float denom = MathF.Sqrt(sumL2 * sumR2);
        analysis.StereoCorrelation = denom > 1e-10f ? sumLR / denom : 1f;
    }

    private void DetectIssues(MasteringAnalysis analysis)
    {
        // Loudness issues
        if (analysis.IntegratedLufs < -24f)
        {
            analysis.Issues.Add("Very quiet source - significant gain needed");
        }
        else if (analysis.IntegratedLufs > -8f)
        {
            analysis.Issues.Add("Source is already very loud - limited headroom");
        }

        // Dynamic range issues
        if (analysis.CrestFactorDb < 6f)
        {
            analysis.Issues.Add("Low dynamic range - source may be over-compressed");
        }
        else if (analysis.CrestFactorDb > 20f)
        {
            analysis.Issues.Add("High dynamic range - may need significant compression");
        }

        // True peak issues
        if (analysis.TruePeakDbtp > -0.5f)
        {
            analysis.Issues.Add("True peaks are high - risk of clipping after codec conversion");
        }

        // Stereo issues
        if (analysis.StereoCorrelation < 0.3f)
        {
            analysis.Issues.Add("Low stereo correlation - potential phase issues");
        }
        else if (analysis.StereoCorrelation > 0.98f)
        {
            analysis.Issues.Add("Very narrow stereo image - essentially mono");
        }

        // Spectral issues
        if (analysis.LowEndRatio > 0.5f)
        {
            analysis.Issues.Add("Excessive low-end energy - may need high-pass filter");
        }
        if (analysis.HighEndRatio < 0.1f)
        {
            analysis.Issues.Add("Lacking high frequencies - may sound dull");
        }
        if (analysis.SpectralCentroid < 800f)
        {
            analysis.Issues.Add("Dark/muddy spectral balance");
        }
        else if (analysis.SpectralCentroid > 4000f)
        {
            analysis.Issues.Add("Harsh/bright spectral balance");
        }
    }

    private (float frequency, float gainDb)[] GenerateMatchingCurve(MasteringAnalysis analysis)
    {
        float[] centerFreqs = { 60f, 250f, 1000f, 4000f, 12000f };
        var curve = new (float, float)[5];

        for (int i = 0; i < 5; i++)
        {
            // Store as relative gain from neutral (0.5)
            float relativeGain = (analysis.SpectralBalance[i] - 0.5f) * 6f;
            curve[i] = (centerFreqs[i], relativeGain);
        }

        return curve;
    }

    private float EstimateOutputLoudness(MasteringAnalysis input, MasteringChain chain)
    {
        float output = input.IntegratedLufs;

        // Add gain from limiter
        if (chain.Limiter != null)
        {
            output += chain.Limiter.TargetGainDb;
        }

        // Account for compression (reduces peaks, increases RMS)
        if (chain.MultibandCompressor != null)
        {
            output += 2f; // Rough estimate
        }

        return Math.Min(output, chain.Limiter?.CeilingDbtp ?? -1f);
    }

    private float CalculateConfidence(MasteringAnalysis analysis)
    {
        float confidence = 0.7f;

        // Reduce confidence for problematic sources
        if (analysis.Issues.Count > 0)
        {
            confidence -= analysis.Issues.Count * 0.05f;
        }

        // Reduce for extreme values
        if (analysis.IntegratedLufs < -30f || analysis.IntegratedLufs > -6f)
        {
            confidence -= 0.1f;
        }

        if (analysis.StereoCorrelation < 0.2f)
        {
            confidence -= 0.1f;
        }

        return Math.Clamp(confidence, 0.3f, 0.9f);
    }

    private void GenerateNotes(MasteringChain chain, MasteringAnalysis analysis, TargetProfile profile)
    {
        chain.Notes.AddRange(analysis.Issues);

        chain.Notes.Add($"Target: {profile.TargetLufs:F1} LUFS for {chain.Target}");
        chain.Notes.Add($"True peak ceiling: {profile.TruePeakCeiling:F1} dBTP");

        if (chain.Limiter?.TargetGainDb > 10f)
        {
            chain.Notes.Add("Warning: High gain needed - check for artifacts");
        }

        if (analysis.DynamicRangeDb < 8f && profile.Target == MasteringTarget.Streaming)
        {
            chain.Notes.Add("Consider keeping some dynamic range for streaming normalization");
        }
    }

    private void ApplyGenreAdjustments(MasteringChain chain, string genre)
    {
        switch (genre)
        {
            case "edm":
            case "electronic":
            case "dance":
                // More bass, more compression
                if (chain.MultibandCompressor != null)
                {
                    chain.MultibandCompressor.Bands[0].Ratio = 3f;
                    chain.MultibandCompressor.Bands[0].GainDb = 2f;
                }
                chain.EqBands.Add(new MasteringEqBand { Frequency = 50f, GainDb = 2f, Q = 1f, FilterType = "peak" });
                break;

            case "rock":
            case "metal":
                // More mids, punch
                if (chain.MultibandCompressor != null)
                {
                    chain.MultibandCompressor.Bands[1].Ratio = 3f;
                    chain.MultibandCompressor.Bands[2].GainDb = 1f;
                }
                break;

            case "jazz":
            case "classical":
                // More dynamic range, less compression
                if (chain.MultibandCompressor != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        chain.MultibandCompressor.Bands[i].Ratio = Math.Max(1.5f, chain.MultibandCompressor.Bands[i].Ratio - 0.5f);
                        chain.MultibandCompressor.Bands[i].ThresholdDb -= 4f;
                    }
                }
                break;

            case "hiphop":
            case "rap":
                // Strong low end, clear vocals
                if (chain.MultibandCompressor != null)
                {
                    chain.MultibandCompressor.Bands[0].GainDb = 2f;
                    chain.MultibandCompressor.Bands[2].GainDb = 1f; // Vocal presence
                }
                break;

            case "acoustic":
            case "folk":
                // Natural, open sound
                if (chain.StereoProcessing != null)
                {
                    chain.StereoProcessing.Width = 1.05f;
                }
                break;
        }
    }

    private Dictionary<MasteringTarget, TargetProfile> InitializeTargetProfiles()
    {
        return new Dictionary<MasteringTarget, TargetProfile>
        {
            {
                MasteringTarget.Streaming, new TargetProfile
                {
                    Target = MasteringTarget.Streaming,
                    TargetLufs = -14f,
                    TruePeakCeiling = -1f,
                    LimiterRelease = 100f,
                    MonoBassFrequency = 80f,
                    CrossoverLow = 200f,
                    CrossoverMid = 2000f,
                    CrossoverHigh = 8000f,
                    IdealSpectralBalance = new[] { 0.5f, 0.45f, 0.5f, 0.55f, 0.5f },
                    TiltAmount = 0f
                }
            },
            {
                MasteringTarget.CD, new TargetProfile
                {
                    Target = MasteringTarget.CD,
                    TargetLufs = -9f,
                    TruePeakCeiling = -0.3f,
                    LimiterRelease = 50f,
                    MonoBassFrequency = 60f,
                    CrossoverLow = 150f,
                    CrossoverMid = 1500f,
                    CrossoverHigh = 7000f,
                    IdealSpectralBalance = new[] { 0.55f, 0.5f, 0.5f, 0.5f, 0.45f },
                    TiltAmount = 0f
                }
            },
            {
                MasteringTarget.Broadcast, new TargetProfile
                {
                    Target = MasteringTarget.Broadcast,
                    TargetLufs = -24f,
                    TruePeakCeiling = -2f,
                    LimiterRelease = 150f,
                    MonoBassFrequency = 100f,
                    CrossoverLow = 200f,
                    CrossoverMid = 2000f,
                    CrossoverHigh = 8000f,
                    IdealSpectralBalance = new[] { 0.4f, 0.45f, 0.55f, 0.55f, 0.45f },
                    TiltAmount = 0f
                }
            },
            {
                MasteringTarget.Club, new TargetProfile
                {
                    Target = MasteringTarget.Club,
                    TargetLufs = -6f,
                    TruePeakCeiling = -0.1f,
                    LimiterRelease = 30f,
                    MonoBassFrequency = 100f,
                    CrossoverLow = 150f,
                    CrossoverMid = 1500f,
                    CrossoverHigh = 6000f,
                    IdealSpectralBalance = new[] { 0.7f, 0.55f, 0.5f, 0.45f, 0.4f },
                    TiltAmount = -0.5f
                }
            },
            {
                MasteringTarget.YouTube, new TargetProfile
                {
                    Target = MasteringTarget.YouTube,
                    TargetLufs = -14f,
                    TruePeakCeiling = -1f,
                    LimiterRelease = 100f,
                    MonoBassFrequency = 80f,
                    CrossoverLow = 200f,
                    CrossoverMid = 2000f,
                    CrossoverHigh = 8000f,
                    IdealSpectralBalance = new[] { 0.45f, 0.45f, 0.55f, 0.55f, 0.5f },
                    TiltAmount = 0.3f
                }
            },
            {
                MasteringTarget.Podcast, new TargetProfile
                {
                    Target = MasteringTarget.Podcast,
                    TargetLufs = -16f,
                    TruePeakCeiling = -1f,
                    LimiterRelease = 200f,
                    MonoBassFrequency = 150f,
                    CrossoverLow = 300f,
                    CrossoverMid = 2500f,
                    CrossoverHigh = 8000f,
                    IdealSpectralBalance = new[] { 0.3f, 0.4f, 0.7f, 0.6f, 0.4f },
                    TiltAmount = 0.5f
                }
            },
            {
                MasteringTarget.Vinyl, new TargetProfile
                {
                    Target = MasteringTarget.Vinyl,
                    TargetLufs = -12f,
                    TruePeakCeiling = -1f,
                    LimiterRelease = 150f,
                    MonoBassFrequency = 200f,
                    CrossoverLow = 250f,
                    CrossoverMid = 2000f,
                    CrossoverHigh = 8000f,
                    IdealSpectralBalance = new[] { 0.4f, 0.5f, 0.55f, 0.5f, 0.4f },
                    TiltAmount = 0f
                }
            },
            {
                MasteringTarget.Custom, new TargetProfile
                {
                    Target = MasteringTarget.Custom,
                    TargetLufs = -14f,
                    TruePeakCeiling = -1f,
                    LimiterRelease = 100f,
                    MonoBassFrequency = 80f,
                    CrossoverLow = 200f,
                    CrossoverMid = 2000f,
                    CrossoverHigh = 8000f,
                    IdealSpectralBalance = new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f },
                    TiltAmount = 0f
                }
            }
        };
    }

    /// <summary>
    /// Target profile for mastering.
    /// </summary>
    private class TargetProfile
    {
        public MasteringTarget Target { get; set; }
        public float TargetLufs { get; set; }
        public float TruePeakCeiling { get; set; }
        public float LimiterRelease { get; set; }
        public float MonoBassFrequency { get; set; }
        public float CrossoverLow { get; set; }
        public float CrossoverMid { get; set; }
        public float CrossoverHigh { get; set; }
        public float[] IdealSpectralBalance { get; set; } = new float[5];
        public float TiltAmount { get; set; }
    }
}
