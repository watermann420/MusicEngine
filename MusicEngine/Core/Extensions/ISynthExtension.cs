namespace MusicEngine.Core.Extensions;

/// <summary>
/// Interface for synth extensions with metadata.
/// </summary>
public interface ISynthExtension : ISynth
{
    /// <summary>
    /// Unique identifier for the extension.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Version of the extension.
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// Author/vendor of the extension.
    /// </summary>
    string Author { get; }

    /// <summary>
    /// Description of the extension.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the extension parameters.
    /// </summary>
    IReadOnlyDictionary<string, ExtensionParameter> Parameters { get; }
}

/// <summary>
/// Describes an extension parameter.
/// </summary>
public class ExtensionParameter
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public float DefaultValue { get; set; }
    public float MinValue { get; set; }
    public float MaxValue { get; set; } = 1.0f;
    public string? Unit { get; set; }
    public ParameterType Type { get; set; } = ParameterType.Float;
}

/// <summary>
/// Parameter types.
/// </summary>
public enum ParameterType
{
    Float,
    Int,
    Bool,
    Enum,
    String
}
