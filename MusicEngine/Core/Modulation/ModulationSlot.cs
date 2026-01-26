namespace MusicEngine.Core.Modulation;

/// <summary>
/// Represents a connection between a modulation source and a target parameter.
/// </summary>
public class ModulationSlot
{
    /// <summary>
    /// Unique identifier for this modulation slot.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// The modulation source providing the modulation value.
    /// </summary>
    public ModulationSource Source { get; set; } = null!;

    /// <summary>
    /// The name/path of the target parameter (e.g., "Filter.Cutoff", "Oscillator1.Pitch").
    /// </summary>
    public string TargetParameter { get; set; } = string.Empty;

    /// <summary>
    /// Modulation amount/depth from -1 to 1.
    /// Positive values increase the parameter, negative values decrease it.
    /// </summary>
    public float Amount { get; set; }

    /// <summary>
    /// Whether the modulation is bipolar (centered around base value) or unipolar.
    /// Bipolar: base + (source * amount), where source is -1 to 1
    /// Unipolar: base + (source * amount), where source is 0 to 1
    /// </summary>
    public bool Bipolar { get; set; }

    /// <summary>
    /// Minimum value for the modulated output.
    /// </summary>
    public float MinValue { get; set; } = 0f;

    /// <summary>
    /// Maximum value for the modulated output.
    /// </summary>
    public float MaxValue { get; set; } = 1f;

    /// <summary>
    /// Optional curve shape for the modulation (0 = linear, negative = log, positive = exp).
    /// </summary>
    public float Curve { get; set; }

    /// <summary>
    /// Whether this slot is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Calculates the modulated value for a given base value.
    /// </summary>
    /// <param name="baseValue">The parameter's base value (before modulation)</param>
    /// <returns>The modulated value</returns>
    public float GetModulatedValue(float baseValue)
    {
        if (!IsEnabled || Source == null)
        {
            return baseValue;
        }

        float sourceValue = Source.Value;

        // Apply curve shaping
        if (Math.Abs(Curve) > 0.001f)
        {
            sourceValue = ApplyCurve(sourceValue, Curve);
        }

        // Calculate modulation offset
        float modulation;
        if (Bipolar)
        {
            // Bipolar: source value is -1 to 1, modulation is symmetric around base
            modulation = sourceValue * Amount;
        }
        else
        {
            // Unipolar: source value is 0 to 1, modulation is additive
            modulation = sourceValue * Amount;
        }

        // Apply modulation to base value
        float result = baseValue + modulation;

        // Clamp to valid range
        return Math.Clamp(result, MinValue, MaxValue);
    }

    /// <summary>
    /// Calculates the scaled modulation offset (without base value).
    /// Useful for combining multiple modulation sources.
    /// </summary>
    /// <returns>The modulation offset to add to the base value</returns>
    public float GetModulationOffset()
    {
        if (!IsEnabled || Source == null)
        {
            return 0f;
        }

        float sourceValue = Source.Value;

        if (Math.Abs(Curve) > 0.001f)
        {
            sourceValue = ApplyCurve(sourceValue, Curve);
        }

        return sourceValue * Amount;
    }

    private static float ApplyCurve(float value, float curve)
    {
        // Normalize to 0-1 range if bipolar
        float normalized = (value + 1f) * 0.5f;

        // Apply exponential curve
        if (curve > 0)
        {
            normalized = MathF.Pow(normalized, 1f + curve);
        }
        else
        {
            normalized = 1f - MathF.Pow(1f - normalized, 1f - curve);
        }

        // Convert back to original range
        return normalized * 2f - 1f;
    }

    /// <summary>
    /// Creates a display string for this modulation slot.
    /// </summary>
    /// <returns>A human-readable description of this modulation routing</returns>
    public override string ToString()
    {
        string direction = Amount >= 0 ? "+" : "";
        return $"{Source?.Name ?? "None"} -> {TargetParameter} ({direction}{Amount:P0})";
    }
}
