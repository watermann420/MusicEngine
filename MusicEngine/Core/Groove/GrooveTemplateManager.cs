// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace MusicEngine.Core.Groove;


/// <summary>
/// Manages groove templates including saving, loading, and providing built-in presets.
/// Templates are stored as JSON files.
/// </summary>
public class GrooveTemplateManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Default directory for user groove templates.
    /// </summary>
    public string TemplateDirectory { get; set; }

    /// <summary>
    /// Creates a new GrooveTemplateManager with the specified template directory.
    /// </summary>
    /// <param name="templateDirectory">Directory for storing user templates. If null, uses AppData.</param>
    public GrooveTemplateManager(string? templateDirectory = null)
    {
        TemplateDirectory = templateDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                           "MusicEngine", "GrooveTemplates");
    }

    /// <summary>
    /// Saves an ExtractedGroove to a JSON file.
    /// </summary>
    /// <param name="groove">The groove to save.</param>
    /// <param name="filePath">Full path to the file. If null, saves to TemplateDirectory with groove name.</param>
    public void SaveTemplate(ExtractedGroove groove, string? filePath = null)
    {
        if (filePath == null)
        {
            EnsureDirectoryExists(TemplateDirectory);
            string safeName = GetSafeFileName(groove.Name);
            filePath = Path.Combine(TemplateDirectory, $"{safeName}.groove.json");
        }
        else
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                EnsureDirectoryExists(directory);
        }

        string json = JsonSerializer.Serialize(groove, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Loads an ExtractedGroove from a JSON file.
    /// </summary>
    /// <param name="filePath">Path to the groove template file.</param>
    /// <returns>The loaded groove, or null if loading fails.</returns>
    public ExtractedGroove? LoadTemplate(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ExtractedGroove>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Lists all user groove templates in the template directory.
    /// </summary>
    /// <returns>List of file paths to groove templates.</returns>
    public List<string> ListUserTemplates()
    {
        if (!Directory.Exists(TemplateDirectory))
            return [];

        return [.. Directory.GetFiles(TemplateDirectory, "*.groove.json")];
    }

    /// <summary>
    /// Loads all user templates from the template directory.
    /// </summary>
    /// <returns>List of loaded groove templates.</returns>
    public List<ExtractedGroove> LoadAllUserTemplates()
    {
        var templates = new List<ExtractedGroove>();

        foreach (var file in ListUserTemplates())
        {
            var template = LoadTemplate(file);
            if (template != null)
                templates.Add(template);
        }

        return templates;
    }

    /// <summary>
    /// Deletes a user template file.
    /// </summary>
    /// <param name="filePath">Path to the template to delete.</param>
    /// <returns>True if deleted successfully.</returns>
    public bool DeleteTemplate(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        try
        {
            File.Delete(filePath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns a collection of built-in groove templates.
    /// These are classic grooves inspired by hardware drum machines and genres.
    /// </summary>
    /// <returns>Dictionary of groove name to ExtractedGroove.</returns>
    public static Dictionary<string, ExtractedGroove> GetBuiltInTemplates()
    {
        return new Dictionary<string, ExtractedGroove>
        {
            ["MPC Swing 50%"] = CreateMpcSwing(50),
            ["MPC Swing 54%"] = CreateMpcSwing(54),
            ["MPC Swing 58%"] = CreateMpcSwing(58),
            ["MPC Swing 62%"] = CreateMpcSwing(62),
            ["MPC Swing 66%"] = CreateMpcSwing(66),
            ["MPC Swing 70%"] = CreateMpcSwing(70),
            ["MPC Swing 75%"] = CreateMpcSwing(75),
            ["Shuffle Light"] = CreateShuffle(54),
            ["Shuffle Medium"] = CreateShuffle(60),
            ["Shuffle Heavy"] = CreateShuffle(67),
            ["Hip-Hop Lazy"] = CreateHipHopGroove(),
            ["Funk Tight"] = CreateFunkGroove(),
            ["Jazz Swing"] = CreateJazzSwing(),
            ["Reggae One Drop"] = CreateReggaeGroove(),
            ["House Push"] = CreateHouseGroove(),
            ["Drum & Bass Rush"] = CreateDnBGroove()
        };
    }

    /// <summary>
    /// Gets a specific built-in template by name.
    /// </summary>
    /// <param name="name">Name of the built-in template.</param>
    /// <returns>The groove template or null if not found.</returns>
    public static ExtractedGroove? GetBuiltInTemplate(string name)
    {
        var templates = GetBuiltInTemplates();
        return templates.TryGetValue(name, out var groove) ? groove : null;
    }

    /// <summary>
    /// Gets the names of all built-in templates.
    /// </summary>
    public static IEnumerable<string> GetBuiltInTemplateNames()
    {
        return GetBuiltInTemplates().Keys;
    }

    #region Built-in Groove Creation Methods

    /// <summary>
    /// Creates an MPC-style swing groove.
    /// </summary>
    /// <param name="swingPercent">Swing percentage (50 = straight, 67 = triplet).</param>
    private static ExtractedGroove CreateMpcSwing(int swingPercent)
    {
        // MPC swing affects 16th note upbeats
        // Swing percentage indicates how far into the beat the upbeat is placed
        // 50% = straight (0.5), 67% = triplet feel (0.67)
        double upbeatPosition = swingPercent / 100.0;
        double deviationBeats = upbeatPosition - 0.5;
        double deviationTicks = deviationBeats * 480; // Assuming 480 PPQN

        return new ExtractedGroove
        {
            Name = $"MPC Swing {swingPercent}%",
            Description = $"Classic MPC-style swing at {swingPercent}%",
            Resolution = 480,
            CycleLengthBeats = 1.0,
            SwingAmount = swingPercent,
            Tags = ["swing", "mpc", "hip-hop"],
            TimingDeviations =
            [
                new() { BeatPosition = 0.00, DeviationInTicks = 0 },
                new() { BeatPosition = 0.25, DeviationInTicks = deviationTicks * 0.5 }, // Light swing on e
                new() { BeatPosition = 0.50, DeviationInTicks = deviationTicks },        // Full swing on and
                new() { BeatPosition = 0.75, DeviationInTicks = deviationTicks * 0.5 }  // Light swing on a
            ],
            VelocityPattern =
            [
                new() { BeatPosition = 0.00, VelocityMultiplier = 1.00 },
                new() { BeatPosition = 0.25, VelocityMultiplier = 0.85 },
                new() { BeatPosition = 0.50, VelocityMultiplier = 0.92 },
                new() { BeatPosition = 0.75, VelocityMultiplier = 0.80 }
            ]
        };
    }

    /// <summary>
    /// Creates a standard shuffle groove.
    /// </summary>
    private static ExtractedGroove CreateShuffle(int swingPercent)
    {
        double upbeatPosition = swingPercent / 100.0;
        double deviationBeats = upbeatPosition - 0.5;
        double deviationTicks = deviationBeats * 480;

        return new ExtractedGroove
        {
            Name = $"Shuffle {swingPercent}%",
            Description = $"Standard shuffle feel at {swingPercent}%",
            Resolution = 480,
            CycleLengthBeats = 1.0,
            SwingAmount = swingPercent,
            Tags = ["shuffle", "swing", "blues"],
            TimingDeviations =
            [
                new() { BeatPosition = 0.00, DeviationInTicks = 0 },
                new() { BeatPosition = 0.25, DeviationInTicks = 0 },                    // 16ths stay straight
                new() { BeatPosition = 0.50, DeviationInTicks = deviationTicks },        // 8th note shuffle
                new() { BeatPosition = 0.75, DeviationInTicks = 0 }
            ],
            VelocityPattern =
            [
                new() { BeatPosition = 0.00, VelocityMultiplier = 1.00 },
                new() { BeatPosition = 0.25, VelocityMultiplier = 0.75 },
                new() { BeatPosition = 0.50, VelocityMultiplier = 0.88 },
                new() { BeatPosition = 0.75, VelocityMultiplier = 0.75 }
            ]
        };
    }

    /// <summary>
    /// Creates a hip-hop lazy groove with a laid-back feel.
    /// </summary>
    private static ExtractedGroove CreateHipHopGroove()
    {
        return new ExtractedGroove
        {
            Name = "Hip-Hop Lazy",
            Description = "Laid-back hip-hop groove with slightly late timing",
            Resolution = 480,
            CycleLengthBeats = 1.0,
            SwingAmount = 58,
            Tags = ["hip-hop", "lazy", "laid-back"],
            TimingDeviations =
            [
                new() { BeatPosition = 0.00, DeviationInTicks = 0 },
                new() { BeatPosition = 0.25, DeviationInTicks = 15 },   // Slightly late
                new() { BeatPosition = 0.50, DeviationInTicks = 40 },   // Noticeably late
                new() { BeatPosition = 0.75, DeviationInTicks = 20 }    // Slightly late
            ],
            VelocityPattern =
            [
                new() { BeatPosition = 0.00, VelocityMultiplier = 1.00 },
                new() { BeatPosition = 0.25, VelocityMultiplier = 0.70 },
                new() { BeatPosition = 0.50, VelocityMultiplier = 0.85 },
                new() { BeatPosition = 0.75, VelocityMultiplier = 0.65 }
            ]
        };
    }

    /// <summary>
    /// Creates a tight funk groove.
    /// </summary>
    private static ExtractedGroove CreateFunkGroove()
    {
        return new ExtractedGroove
        {
            Name = "Funk Tight",
            Description = "Tight funk groove with slight anticipation on upbeats",
            Resolution = 480,
            CycleLengthBeats = 1.0,
            SwingAmount = 52,
            Tags = ["funk", "tight", "groove"],
            TimingDeviations =
            [
                new() { BeatPosition = 0.00, DeviationInTicks = 0 },
                new() { BeatPosition = 0.25, DeviationInTicks = -8 },   // Slight push
                new() { BeatPosition = 0.50, DeviationInTicks = 12 },   // Slight lay back
                new() { BeatPosition = 0.75, DeviationInTicks = -5 }    // Tiny push
            ],
            VelocityPattern =
            [
                new() { BeatPosition = 0.00, VelocityMultiplier = 1.00 },
                new() { BeatPosition = 0.25, VelocityMultiplier = 0.90 },
                new() { BeatPosition = 0.50, VelocityMultiplier = 0.95 },
                new() { BeatPosition = 0.75, VelocityMultiplier = 0.88 }
            ]
        };
    }

    /// <summary>
    /// Creates a jazz swing groove.
    /// </summary>
    private static ExtractedGroove CreateJazzSwing()
    {
        return new ExtractedGroove
        {
            Name = "Jazz Swing",
            Description = "Classic jazz swing feel with triplet timing",
            Resolution = 480,
            CycleLengthBeats = 1.0,
            SwingAmount = 67, // Triplet feel
            Tags = ["jazz", "swing", "triplet"],
            TimingDeviations =
            [
                new() { BeatPosition = 0.00, DeviationInTicks = 0 },
                new() { BeatPosition = 0.25, DeviationInTicks = 25 },
                new() { BeatPosition = 0.50, DeviationInTicks = 80 },   // Strong triplet swing
                new() { BeatPosition = 0.75, DeviationInTicks = 30 }
            ],
            VelocityPattern =
            [
                new() { BeatPosition = 0.00, VelocityMultiplier = 1.00 },
                new() { BeatPosition = 0.25, VelocityMultiplier = 0.72 },
                new() { BeatPosition = 0.50, VelocityMultiplier = 0.82 },
                new() { BeatPosition = 0.75, VelocityMultiplier = 0.70 }
            ]
        };
    }

    /// <summary>
    /// Creates a reggae one-drop groove.
    /// </summary>
    private static ExtractedGroove CreateReggaeGroove()
    {
        return new ExtractedGroove
        {
            Name = "Reggae One Drop",
            Description = "Reggae one-drop feel with emphasis on beat 3",
            Resolution = 480,
            CycleLengthBeats = 4.0, // Full bar groove
            SwingAmount = 50,
            Tags = ["reggae", "one-drop", "dub"],
            TimingDeviations =
            [
                new() { BeatPosition = 0.0, DeviationInTicks = 0 },
                new() { BeatPosition = 1.0, DeviationInTicks = 5 },
                new() { BeatPosition = 2.0, DeviationInTicks = 0 },    // Beat 3 on time
                new() { BeatPosition = 2.5, DeviationInTicks = 8 },    // Offbeat slightly late
                new() { BeatPosition = 3.0, DeviationInTicks = 5 }
            ],
            VelocityPattern =
            [
                new() { BeatPosition = 0.0, VelocityMultiplier = 0.85 },
                new() { BeatPosition = 1.0, VelocityMultiplier = 0.70 },
                new() { BeatPosition = 2.0, VelocityMultiplier = 1.00 }, // Accent on 3
                new() { BeatPosition = 2.5, VelocityMultiplier = 0.75 },
                new() { BeatPosition = 3.0, VelocityMultiplier = 0.72 }
            ]
        };
    }

    /// <summary>
    /// Creates a house push groove.
    /// </summary>
    private static ExtractedGroove CreateHouseGroove()
    {
        return new ExtractedGroove
        {
            Name = "House Push",
            Description = "House groove with forward momentum",
            Resolution = 480,
            CycleLengthBeats = 1.0,
            SwingAmount = 50,
            Tags = ["house", "electronic", "dance"],
            TimingDeviations =
            [
                new() { BeatPosition = 0.00, DeviationInTicks = 0 },
                new() { BeatPosition = 0.25, DeviationInTicks = -10 },  // Push
                new() { BeatPosition = 0.50, DeviationInTicks = -5 },   // Slight push
                new() { BeatPosition = 0.75, DeviationInTicks = -8 }    // Push
            ],
            VelocityPattern =
            [
                new() { BeatPosition = 0.00, VelocityMultiplier = 1.00 },
                new() { BeatPosition = 0.25, VelocityMultiplier = 0.92 },
                new() { BeatPosition = 0.50, VelocityMultiplier = 0.95 },
                new() { BeatPosition = 0.75, VelocityMultiplier = 0.90 }
            ]
        };
    }

    /// <summary>
    /// Creates a drum and bass rush groove.
    /// </summary>
    private static ExtractedGroove CreateDnBGroove()
    {
        return new ExtractedGroove
        {
            Name = "Drum & Bass Rush",
            Description = "DnB groove with rushing feel",
            Resolution = 480,
            CycleLengthBeats = 1.0,
            SwingAmount = 48, // Slightly ahead
            Tags = ["dnb", "drum-and-bass", "jungle"],
            TimingDeviations =
            [
                new() { BeatPosition = 0.00, DeviationInTicks = 0 },
                new() { BeatPosition = 0.125, DeviationInTicks = -12 }, // 32nd note rush
                new() { BeatPosition = 0.25, DeviationInTicks = -8 },
                new() { BeatPosition = 0.375, DeviationInTicks = -10 },
                new() { BeatPosition = 0.50, DeviationInTicks = -5 },
                new() { BeatPosition = 0.625, DeviationInTicks = -12 },
                new() { BeatPosition = 0.75, DeviationInTicks = -6 },
                new() { BeatPosition = 0.875, DeviationInTicks = -10 }
            ],
            VelocityPattern =
            [
                new() { BeatPosition = 0.00, VelocityMultiplier = 1.00 },
                new() { BeatPosition = 0.25, VelocityMultiplier = 0.88 },
                new() { BeatPosition = 0.50, VelocityMultiplier = 0.95 },
                new() { BeatPosition = 0.75, VelocityMultiplier = 0.85 }
            ]
        };
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Ensures a directory exists, creating it if necessary.
    /// </summary>
    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    /// <summary>
    /// Creates a safe file name from a string.
    /// </summary>
    private static string GetSafeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "unnamed" : safe;
    }

    #endregion
}
