namespace MusicEngine.Core.Extensions;

/// <summary>
/// Interface for effect extensions with metadata.
/// </summary>
public interface IEffectExtension : IEffect
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
    /// Category of the effect (e.g., "Dynamics", "Modulation", "Reverb").
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Gets the extension parameters.
    /// </summary>
    IReadOnlyDictionary<string, ExtensionParameter> Parameters { get; }
}
