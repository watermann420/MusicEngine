//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Factory preset generator for MusicEngine synthesizers.

using MusicEngine.Core;

namespace MusicEngine.Core.Presets;

/// <summary>
/// Provides factory preset generation for MusicEngine synthesizers.
/// </summary>
public static class FactoryPresets
{
    /// <summary>
    /// Creates the factory preset bank containing all built-in presets.
    /// </summary>
    /// <returns>A preset bank with factory presets.</returns>
    public static SynthPresetBank CreateFactoryBank()
    {
        var bank = new SynthPresetBank
        {
            Name = "MusicEngine Factory",
            Author = "MusicEngine",
            Description = "Built-in factory presets for MusicEngine synthesizers",
            Version = "1.0.0",
            IsFactory = true,
            IsUser = false
        };

        // Add PolySynth presets
        foreach (var preset in CreatePolySynthPresets())
        {
            bank.AddPreset(preset);
        }

        // Add FMSynth presets
        foreach (var preset in CreateFMSynthPresets())
        {
            bank.AddPreset(preset);
        }

        // Add SimpleSynth presets
        foreach (var preset in CreateSimpleSynthPresets())
        {
            bank.AddPreset(preset);
        }

        return bank;
    }

    /// <summary>
    /// Creates factory presets for PolySynth.
    /// </summary>
    public static IEnumerable<SynthPreset> CreatePolySynthPresets()
    {
        // Warm Pad
        yield return new SynthPreset("Warm Pad", "PolySynth")
        {
            Category = SynthPresetCategory.Pad,
            Author = "MusicEngine",
            Description = "A warm, evolving pad sound perfect for ambient backgrounds",
            Tags = ["warm", "soft", "ambient", "atmospheric"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["waveform"] = (float)WaveType.Sawtooth,
                ["cutoff"] = 0.4f,
                ["resonance"] = 0.2f,
                ["volume"] = 0.6f,
                ["attack"] = 0.8f,
                ["decay"] = 0.5f,
                ["sustain"] = 0.7f,
                ["release"] = 1.2f,
                ["detune"] = 8f,
                ["vibrato"] = 0.1f
            }
        };

        // Classic Bass
        yield return new SynthPreset("Classic Bass", "PolySynth")
        {
            Category = SynthPresetCategory.Bass,
            Author = "MusicEngine",
            Description = "A punchy, classic synth bass sound",
            Tags = ["punchy", "classic", "fat"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["waveform"] = (float)WaveType.Square,
                ["cutoff"] = 0.3f,
                ["resonance"] = 0.4f,
                ["volume"] = 0.7f,
                ["attack"] = 0.01f,
                ["decay"] = 0.2f,
                ["sustain"] = 0.5f,
                ["release"] = 0.1f,
                ["detune"] = 0f,
                ["vibrato"] = 0f
            }
        };

        // Bright Lead
        yield return new SynthPreset("Bright Lead", "PolySynth")
        {
            Category = SynthPresetCategory.Lead,
            Author = "MusicEngine",
            Description = "A bright, cutting lead synth for melodies",
            Tags = ["bright", "cutting", "melody", "solo"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["waveform"] = (float)WaveType.Sawtooth,
                ["cutoff"] = 0.85f,
                ["resonance"] = 0.3f,
                ["volume"] = 0.5f,
                ["attack"] = 0.01f,
                ["decay"] = 0.15f,
                ["sustain"] = 0.8f,
                ["release"] = 0.2f,
                ["detune"] = 5f,
                ["vibrato"] = 0.15f
            }
        };

        // Soft Keys
        yield return new SynthPreset("Soft Keys", "PolySynth")
        {
            Category = SynthPresetCategory.Keys,
            Author = "MusicEngine",
            Description = "Soft, mellow keyboard sound for chords",
            Tags = ["soft", "mellow", "keys", "chords"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["waveform"] = (float)WaveType.Sine,
                ["cutoff"] = 0.6f,
                ["resonance"] = 0.1f,
                ["volume"] = 0.5f,
                ["attack"] = 0.02f,
                ["decay"] = 0.3f,
                ["sustain"] = 0.6f,
                ["release"] = 0.4f,
                ["detune"] = 0f,
                ["vibrato"] = 0f
            }
        };

        // Plucky Synth
        yield return new SynthPreset("Plucky Synth", "PolySynth")
        {
            Category = SynthPresetCategory.Pluck,
            Author = "MusicEngine",
            Description = "Short, plucky synth stab for rhythmic patterns",
            Tags = ["plucky", "short", "rhythmic", "stab"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["waveform"] = (float)WaveType.Triangle,
                ["cutoff"] = 0.7f,
                ["resonance"] = 0.5f,
                ["volume"] = 0.6f,
                ["attack"] = 0.001f,
                ["decay"] = 0.25f,
                ["sustain"] = 0.0f,
                ["release"] = 0.15f,
                ["detune"] = 3f,
                ["vibrato"] = 0f
            }
        };

        // String Ensemble
        yield return new SynthPreset("String Ensemble", "PolySynth")
        {
            Category = SynthPresetCategory.Strings,
            Author = "MusicEngine",
            Description = "Lush synthetic string ensemble",
            Tags = ["strings", "lush", "ensemble", "orchestral"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["waveform"] = (float)WaveType.Sawtooth,
                ["cutoff"] = 0.45f,
                ["resonance"] = 0.15f,
                ["volume"] = 0.55f,
                ["attack"] = 0.5f,
                ["decay"] = 0.3f,
                ["sustain"] = 0.8f,
                ["release"] = 0.6f,
                ["detune"] = 12f,
                ["vibrato"] = 0.2f
            }
        };

        // Noise Sweep
        yield return new SynthPreset("Noise Sweep", "PolySynth")
        {
            Category = SynthPresetCategory.FX,
            Author = "MusicEngine",
            Description = "Filtered noise for risers and sweeps",
            Tags = ["noise", "sweep", "riser", "fx"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["waveform"] = (float)WaveType.Noise,
                ["cutoff"] = 0.3f,
                ["resonance"] = 0.6f,
                ["volume"] = 0.4f,
                ["attack"] = 2.0f,
                ["decay"] = 0.5f,
                ["sustain"] = 0.5f,
                ["release"] = 1.0f,
                ["detune"] = 0f,
                ["vibrato"] = 0f
            }
        };

        // Ambient Texture
        yield return new SynthPreset("Ambient Texture", "PolySynth")
        {
            Category = SynthPresetCategory.Atmosphere,
            Author = "MusicEngine",
            Description = "Evolving ambient texture for soundscapes",
            Tags = ["ambient", "texture", "evolving", "soundscape"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["waveform"] = (float)WaveType.Sine,
                ["cutoff"] = 0.5f,
                ["resonance"] = 0.25f,
                ["volume"] = 0.45f,
                ["attack"] = 3.0f,
                ["decay"] = 2.0f,
                ["sustain"] = 0.6f,
                ["release"] = 4.0f,
                ["detune"] = 15f,
                ["vibrato"] = 0.3f
            }
        };
    }

    /// <summary>
    /// Creates factory presets for FMSynth.
    /// </summary>
    public static IEnumerable<SynthPreset> CreateFMSynthPresets()
    {
        // Electric Piano
        yield return new SynthPreset("Electric Piano", "FMSynth")
        {
            Category = SynthPresetCategory.Keys,
            Author = "MusicEngine",
            Description = "Classic DX7-style electric piano with bell-like tones",
            Tags = ["piano", "electric", "dx7", "bell", "classic"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["algorithm"] = (float)FMAlgorithm.EPiano,
                ["volume"] = 0.6f,
                ["feedback"] = 0.3f,
                // Operator 1 - Carrier (fundamental)
                ["op1_ratio"] = 1.0f,
                ["op1_level"] = 0.8f,
                ["op1_attack"] = 0.001f,
                ["op1_decay"] = 1.5f,
                ["op1_sustain"] = 0.0f,
                ["op1_release"] = 0.5f,
                // Operator 2 - Carrier (octave)
                ["op2_ratio"] = 2.0f,
                ["op2_level"] = 0.3f,
                ["op2_attack"] = 0.001f,
                ["op2_decay"] = 1.0f,
                ["op2_sustain"] = 0.0f,
                ["op2_release"] = 0.3f,
                // Operator 3 - Modulator
                ["op3_ratio"] = 1.0f,
                ["op3_level"] = 0.5f,
                ["op3_attack"] = 0.001f,
                ["op3_decay"] = 0.5f,
                ["op3_sustain"] = 0.0f,
                ["op3_release"] = 0.2f,
                // Operator 4 - Bell modulator
                ["op4_ratio"] = 14.0f,
                ["op4_level"] = 0.3f,
                ["op4_attack"] = 0.001f,
                ["op4_decay"] = 0.3f,
                ["op4_sustain"] = 0.0f,
                ["op4_release"] = 0.1f
            }
        };

        // FM Bass
        yield return new SynthPreset("FM Bass", "FMSynth")
        {
            Category = SynthPresetCategory.Bass,
            Author = "MusicEngine",
            Description = "Punchy FM bass with quick attack",
            Tags = ["bass", "fm", "punchy", "synth"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["algorithm"] = (float)FMAlgorithm.Bass,
                ["volume"] = 0.7f,
                ["feedback"] = 0.2f,
                ["op1_ratio"] = 1.0f,
                ["op1_level"] = 0.9f,
                ["op1_attack"] = 0.001f,
                ["op1_decay"] = 0.2f,
                ["op1_sustain"] = 0.4f,
                ["op1_release"] = 0.1f,
                ["op2_ratio"] = 0.5f,
                ["op2_level"] = 0.5f,
                ["op2_attack"] = 0.001f,
                ["op2_decay"] = 0.15f,
                ["op2_sustain"] = 0.3f,
                ["op2_release"] = 0.1f,
                ["op3_ratio"] = 1.0f,
                ["op3_level"] = 0.8f,
                ["op3_attack"] = 0.001f,
                ["op3_decay"] = 0.1f,
                ["op3_sustain"] = 0.0f,
                ["op3_release"] = 0.05f,
                ["op4_ratio"] = 2.0f,
                ["op4_level"] = 0.6f,
                ["op4_attack"] = 0.001f,
                ["op4_decay"] = 0.08f,
                ["op4_sustain"] = 0.0f,
                ["op4_release"] = 0.05f
            }
        };

        // Bell
        yield return new SynthPreset("Crystal Bell", "FMSynth")
        {
            Category = SynthPresetCategory.Bell,
            Author = "MusicEngine",
            Description = "Shimmering metallic bell with long decay",
            Tags = ["bell", "metallic", "shimmer", "crystal"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["algorithm"] = (float)FMAlgorithm.Bells,
                ["volume"] = 0.5f,
                ["feedback"] = 0.1f,
                ["op1_ratio"] = 1.0f,
                ["op1_level"] = 0.8f,
                ["op1_attack"] = 0.001f,
                ["op1_decay"] = 3.0f,
                ["op1_sustain"] = 0.0f,
                ["op1_release"] = 2.0f,
                ["op2_ratio"] = 3.5f,
                ["op2_level"] = 0.6f,
                ["op2_attack"] = 0.001f,
                ["op2_decay"] = 2.0f,
                ["op2_sustain"] = 0.0f,
                ["op2_release"] = 1.5f,
                ["op3_ratio"] = 1.0f,
                ["op3_level"] = 0.7f,
                ["op3_attack"] = 0.001f,
                ["op3_decay"] = 1.0f,
                ["op3_sustain"] = 0.0f,
                ["op3_release"] = 0.5f,
                ["op4_ratio"] = 7.0f,
                ["op4_level"] = 0.5f,
                ["op4_attack"] = 0.001f,
                ["op4_decay"] = 0.5f,
                ["op4_sustain"] = 0.0f,
                ["op4_release"] = 0.3f,
                ["op5_ratio"] = 11.0f,
                ["op5_level"] = 0.3f,
                ["op5_attack"] = 0.001f,
                ["op5_decay"] = 0.3f,
                ["op5_sustain"] = 0.0f,
                ["op5_release"] = 0.2f
            }
        };

        // FM Brass
        yield return new SynthPreset("FM Brass", "FMSynth")
        {
            Category = SynthPresetCategory.Brass,
            Author = "MusicEngine",
            Description = "Bold FM brass section sound",
            Tags = ["brass", "fm", "bold", "section"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["algorithm"] = (float)FMAlgorithm.Brass,
                ["volume"] = 0.65f,
                ["feedback"] = 0.4f,
                ["op1_ratio"] = 1.0f,
                ["op1_level"] = 0.9f,
                ["op1_attack"] = 0.1f,
                ["op1_decay"] = 0.2f,
                ["op1_sustain"] = 0.8f,
                ["op1_release"] = 0.2f,
                ["op2_ratio"] = 1.0f,
                ["op2_level"] = 0.7f,
                ["op2_attack"] = 0.1f,
                ["op2_decay"] = 0.2f,
                ["op2_sustain"] = 0.8f,
                ["op2_release"] = 0.2f,
                ["op3_ratio"] = 1.0f,
                ["op3_level"] = 0.6f,
                ["op3_attack"] = 0.05f,
                ["op3_decay"] = 0.3f,
                ["op3_sustain"] = 0.5f,
                ["op3_release"] = 0.1f,
                ["op4_ratio"] = 2.0f,
                ["op4_level"] = 0.5f,
                ["op4_attack"] = 0.05f,
                ["op4_decay"] = 0.2f,
                ["op4_sustain"] = 0.3f,
                ["op4_release"] = 0.1f
            }
        };

        // Organ
        yield return new SynthPreset("FM Organ", "FMSynth")
        {
            Category = SynthPresetCategory.Keys,
            Author = "MusicEngine",
            Description = "Classic organ sound with drawbar-like harmonics",
            Tags = ["organ", "keys", "classic", "harmonics"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["algorithm"] = (float)FMAlgorithm.Organ,
                ["volume"] = 0.6f,
                ["feedback"] = 0.1f,
                ["op1_ratio"] = 0.5f,
                ["op1_level"] = 0.7f,
                ["op1_attack"] = 0.01f,
                ["op1_decay"] = 0.01f,
                ["op1_sustain"] = 1.0f,
                ["op1_release"] = 0.05f,
                ["op2_ratio"] = 1.0f,
                ["op2_level"] = 0.9f,
                ["op2_attack"] = 0.01f,
                ["op2_decay"] = 0.01f,
                ["op2_sustain"] = 1.0f,
                ["op2_release"] = 0.05f,
                ["op3_ratio"] = 2.0f,
                ["op3_level"] = 0.5f,
                ["op3_attack"] = 0.01f,
                ["op3_decay"] = 0.01f,
                ["op3_sustain"] = 1.0f,
                ["op3_release"] = 0.05f,
                ["op4_ratio"] = 1.5f,
                ["op4_level"] = 0.3f,
                ["op4_attack"] = 0.01f,
                ["op4_decay"] = 0.01f,
                ["op4_sustain"] = 1.0f,
                ["op4_release"] = 0.05f,
                ["op5_ratio"] = 3.0f,
                ["op5_level"] = 0.4f,
                ["op5_attack"] = 0.01f,
                ["op5_decay"] = 0.01f,
                ["op5_sustain"] = 1.0f,
                ["op5_release"] = 0.05f,
                ["op6_ratio"] = 4.0f,
                ["op6_level"] = 0.3f,
                ["op6_attack"] = 0.01f,
                ["op6_decay"] = 0.01f,
                ["op6_sustain"] = 1.0f,
                ["op6_release"] = 0.05f
            }
        };

        // Metallic Lead
        yield return new SynthPreset("Metallic Lead", "FMSynth")
        {
            Category = SynthPresetCategory.Lead,
            Author = "MusicEngine",
            Description = "Sharp, metallic lead synth for cutting melodies",
            Tags = ["lead", "metallic", "sharp", "cutting"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["algorithm"] = (float)FMAlgorithm.Stack6,
                ["volume"] = 0.55f,
                ["feedback"] = 0.6f,
                ["vibratodepth"] = 0.15f,
                ["op1_ratio"] = 1.0f,
                ["op1_level"] = 0.85f,
                ["op1_attack"] = 0.01f,
                ["op1_decay"] = 0.2f,
                ["op1_sustain"] = 0.7f,
                ["op1_release"] = 0.2f,
                ["op2_ratio"] = 2.0f,
                ["op2_level"] = 0.7f,
                ["op2_attack"] = 0.01f,
                ["op2_decay"] = 0.15f,
                ["op2_sustain"] = 0.5f,
                ["op2_release"] = 0.15f,
                ["op3_ratio"] = 3.0f,
                ["op3_level"] = 0.5f,
                ["op3_attack"] = 0.01f,
                ["op3_decay"] = 0.1f,
                ["op3_sustain"] = 0.3f,
                ["op3_release"] = 0.1f
            }
        };
    }

    /// <summary>
    /// Creates factory presets for SimpleSynth.
    /// </summary>
    public static IEnumerable<SynthPreset> CreateSimpleSynthPresets()
    {
        // Pure Sine
        yield return new SynthPreset("Pure Sine", "SimpleSynth")
        {
            Category = SynthPresetCategory.Synth,
            Author = "MusicEngine",
            Description = "Clean sine wave for sub bass and pure tones",
            Tags = ["sine", "pure", "clean", "sub"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["waveform"] = (float)WaveType.Sine,
                ["cutoff"] = 1.0f,
                ["resonance"] = 0.0f
            }
        };

        // Square Lead
        yield return new SynthPreset("Square Lead", "SimpleSynth")
        {
            Category = SynthPresetCategory.Lead,
            Author = "MusicEngine",
            Description = "Classic square wave lead",
            Tags = ["square", "lead", "retro", "8bit"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["waveform"] = (float)WaveType.Square,
                ["cutoff"] = 0.7f,
                ["resonance"] = 0.2f
            }
        };

        // Sawtooth Buzz
        yield return new SynthPreset("Sawtooth Buzz", "SimpleSynth")
        {
            Category = SynthPresetCategory.Synth,
            Author = "MusicEngine",
            Description = "Raw sawtooth oscillator",
            Tags = ["saw", "sawtooth", "raw", "buzzy"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["waveform"] = (float)WaveType.Sawtooth,
                ["cutoff"] = 0.8f,
                ["resonance"] = 0.3f
            }
        };

        // Mellow Triangle
        yield return new SynthPreset("Mellow Triangle", "SimpleSynth")
        {
            Category = SynthPresetCategory.Synth,
            Author = "MusicEngine",
            Description = "Soft triangle wave",
            Tags = ["triangle", "mellow", "soft", "flute-like"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["waveform"] = (float)WaveType.Triangle,
                ["cutoff"] = 0.9f,
                ["resonance"] = 0.1f
            }
        };

        // White Noise
        yield return new SynthPreset("White Noise", "SimpleSynth")
        {
            Category = SynthPresetCategory.FX,
            Author = "MusicEngine",
            Description = "Filtered white noise for percussion and FX",
            Tags = ["noise", "white", "fx", "percussion"],
            IsFactory = true,
            ParameterData = new Dictionary<string, object>
            {
                ["waveform"] = (float)WaveType.Noise,
                ["cutoff"] = 0.5f,
                ["resonance"] = 0.4f
            }
        };
    }

    /// <summary>
    /// Saves the factory bank to a file.
    /// </summary>
    /// <param name="directoryPath">The directory to save to.</param>
    public static void SaveFactoryBank(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);
        var bank = CreateFactoryBank();
        var filePath = Path.Combine(directoryPath, "MusicEngine Factory" + SynthPresetBank.FileExtension);
        bank.Save(filePath);
    }

    /// <summary>
    /// Gets factory presets for a specific synth type.
    /// </summary>
    /// <param name="synthTypeName">The synth type name.</param>
    /// <returns>Factory presets for the synth.</returns>
    public static IEnumerable<SynthPreset> GetPresetsForSynth(string synthTypeName)
    {
        return synthTypeName.ToLowerInvariant() switch
        {
            "polysynth" => CreatePolySynthPresets(),
            "fmsynth" => CreateFMSynthPresets(),
            "simplesynth" => CreateSimpleSynthPresets(),
            _ => Enumerable.Empty<SynthPreset>()
        };
    }
}
