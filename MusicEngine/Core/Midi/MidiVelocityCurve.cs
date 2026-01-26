//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: MIDI velocity curve processor for mapping and transforming velocity values.

namespace MusicEngine.Core.Midi;

/// <summary>
/// Type of velocity curve transformation.
/// </summary>
public enum VelocityCurveType
{
    /// <summary>Linear mapping (no curve transformation).</summary>
    Linear,
    /// <summary>Exponential curve (quiet notes become quieter).</summary>
    Exponential,
    /// <summary>Logarithmic curve (quiet notes become louder).</summary>
    Logarithmic,
    /// <summary>S-Curve (smooth transition with soft extremes).</summary>
    SCurve,
    /// <summary>Fixed velocity (all notes same velocity).</summary>
    Fixed,
    /// <summary>Custom curve using user-defined points.</summary>
    Custom
}

/// <summary>
/// Preset velocity curves for different playing styles.
/// </summary>
public enum VelocityCurvePreset
{
    /// <summary>Default linear response.</summary>
    Default,
    /// <summary>Soft touch for gentle playing.</summary>
    SoftTouch,
    /// <summary>Hard touch for aggressive playing.</summary>
    HardTouch,
    /// <summary>Keyboard player style (natural piano response).</summary>
    KeyboardPlayer,
    /// <summary>Drum machine style (more consistent velocities).</summary>
    DrumMachine,
    /// <summary>Organ style (compressed dynamics).</summary>
    Organ,
    /// <summary>Synth lead style (punchy response).</summary>
    SynthLead,
    /// <summary>Pad style (smooth, even dynamics).</summary>
    Pad,
    /// <summary>Bass style (consistent low end).</summary>
    Bass,
    /// <summary>Expressive style (wide dynamic range).</summary>
    Expressive,
    /// <summary>Compressed style (reduced dynamic range).</summary>
    Compressed,
    /// <summary>Beginner friendly (easier to control).</summary>
    BeginnerFriendly,
    /// <summary>Concert pianist (wide dynamic control).</summary>
    ConcertPianist,
    /// <summary>Electronic music (punchy, consistent).</summary>
    Electronic,
    /// <summary>Jazz style (nuanced dynamics).</summary>
    Jazz
}

/// <summary>
/// A point on a custom velocity curve.
/// </summary>
public class VelocityCurvePoint
{
    /// <summary>Input velocity value (0-127).</summary>
    public int InputValue { get; set; }

    /// <summary>Output velocity value (0-127).</summary>
    public int OutputValue { get; set; }

    /// <summary>
    /// Creates a new curve point.
    /// </summary>
    public VelocityCurvePoint()
    {
    }

    /// <summary>
    /// Creates a new curve point with specified values.
    /// </summary>
    /// <param name="input">Input velocity (0-127).</param>
    /// <param name="output">Output velocity (0-127).</param>
    public VelocityCurvePoint(int input, int output)
    {
        InputValue = Math.Clamp(input, 0, 127);
        OutputValue = Math.Clamp(output, 0, 127);
    }

    /// <summary>
    /// Creates a copy of this curve point.
    /// </summary>
    public VelocityCurvePoint Clone()
    {
        return new VelocityCurvePoint(InputValue, OutputValue);
    }
}

/// <summary>
/// Configuration for a velocity curve.
/// </summary>
public class VelocityCurveConfig
{
    /// <summary>Type of curve transformation.</summary>
    public VelocityCurveType CurveType { get; set; } = VelocityCurveType.Linear;

    /// <summary>Minimum input velocity to process (0-127).</summary>
    public int InputMin { get; set; } = 0;

    /// <summary>Maximum input velocity to process (0-127).</summary>
    public int InputMax { get; set; } = 127;

    /// <summary>Minimum output velocity (0-127).</summary>
    public int OutputMin { get; set; } = 0;

    /// <summary>Maximum output velocity (0-127).</summary>
    public int OutputMax { get; set; } = 127;

    /// <summary>Fixed velocity value (used when CurveType is Fixed).</summary>
    public int FixedVelocity { get; set; } = 100;

    /// <summary>Curve intensity/steepness factor (0.1 to 10.0).</summary>
    public float CurveAmount { get; set; } = 1.0f;

    /// <summary>Custom curve points (used when CurveType is Custom).</summary>
    public List<VelocityCurvePoint> CustomCurvePoints { get; set; } = new();

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    public VelocityCurveConfig Clone()
    {
        return new VelocityCurveConfig
        {
            CurveType = CurveType,
            InputMin = InputMin,
            InputMax = InputMax,
            OutputMin = OutputMin,
            OutputMax = OutputMax,
            FixedVelocity = FixedVelocity,
            CurveAmount = CurveAmount,
            CustomCurvePoints = CustomCurvePoints.Select(p => p.Clone()).ToList()
        };
    }
}

/// <summary>
/// MIDI velocity curve processor for mapping and transforming velocity values.
/// Supports multiple curve types including linear, exponential, logarithmic, S-curve, fixed, and custom.
/// </summary>
public class MidiVelocityCurve
{
    private VelocityCurveConfig _config;
    private readonly object _lock = new();

    // Pre-computed lookup table for performance
    private int[] _lookupTable;
    private bool _lookupTableValid;

    /// <summary>
    /// Gets or sets whether the velocity curve is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the curve type.
    /// </summary>
    public VelocityCurveType CurveType
    {
        get => _config.CurveType;
        set
        {
            lock (_lock)
            {
                _config.CurveType = value;
                InvalidateLookupTable();
            }
        }
    }

    /// <summary>
    /// Gets or sets the minimum input velocity.
    /// </summary>
    public int InputMin
    {
        get => _config.InputMin;
        set
        {
            lock (_lock)
            {
                _config.InputMin = Math.Clamp(value, 0, 127);
                InvalidateLookupTable();
            }
        }
    }

    /// <summary>
    /// Gets or sets the maximum input velocity.
    /// </summary>
    public int InputMax
    {
        get => _config.InputMax;
        set
        {
            lock (_lock)
            {
                _config.InputMax = Math.Clamp(value, 0, 127);
                InvalidateLookupTable();
            }
        }
    }

    /// <summary>
    /// Gets or sets the minimum output velocity.
    /// </summary>
    public int OutputMin
    {
        get => _config.OutputMin;
        set
        {
            lock (_lock)
            {
                _config.OutputMin = Math.Clamp(value, 0, 127);
                InvalidateLookupTable();
            }
        }
    }

    /// <summary>
    /// Gets or sets the maximum output velocity.
    /// </summary>
    public int OutputMax
    {
        get => _config.OutputMax;
        set
        {
            lock (_lock)
            {
                _config.OutputMax = Math.Clamp(value, 0, 127);
                InvalidateLookupTable();
            }
        }
    }

    /// <summary>
    /// Gets or sets the fixed velocity value (used when CurveType is Fixed).
    /// </summary>
    public int FixedVelocity
    {
        get => _config.FixedVelocity;
        set
        {
            lock (_lock)
            {
                _config.FixedVelocity = Math.Clamp(value, 1, 127);
                InvalidateLookupTable();
            }
        }
    }

    /// <summary>
    /// Gets or sets the curve amount/intensity (0.1 to 10.0).
    /// </summary>
    public float CurveAmount
    {
        get => _config.CurveAmount;
        set
        {
            lock (_lock)
            {
                _config.CurveAmount = Math.Clamp(value, 0.1f, 10.0f);
                InvalidateLookupTable();
            }
        }
    }

    /// <summary>
    /// Gets the custom curve points (read-only).
    /// </summary>
    public IReadOnlyList<VelocityCurvePoint> CustomCurvePoints
    {
        get
        {
            lock (_lock)
            {
                return _config.CustomCurvePoints.AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Creates a new MIDI velocity curve processor with default settings.
    /// </summary>
    public MidiVelocityCurve()
    {
        _config = new VelocityCurveConfig();
        _lookupTable = new int[128];
        _lookupTableValid = false;
    }

    /// <summary>
    /// Creates a new MIDI velocity curve processor with specified curve type.
    /// </summary>
    /// <param name="curveType">The curve type to use.</param>
    public MidiVelocityCurve(VelocityCurveType curveType) : this()
    {
        _config.CurveType = curveType;
    }

    /// <summary>
    /// Creates a new MIDI velocity curve processor with specified configuration.
    /// </summary>
    /// <param name="config">The configuration to use.</param>
    public MidiVelocityCurve(VelocityCurveConfig config) : this()
    {
        _config = config?.Clone() ?? new VelocityCurveConfig();
    }

    /// <summary>
    /// Processes a velocity value through the curve.
    /// </summary>
    /// <param name="velocity">Input velocity (0-127).</param>
    /// <returns>Processed velocity (0-127).</returns>
    public int ProcessVelocity(int velocity)
    {
        if (!Enabled)
            return velocity;

        velocity = Math.Clamp(velocity, 0, 127);

        lock (_lock)
        {
            EnsureLookupTable();
            return _lookupTable[velocity];
        }
    }

    /// <summary>
    /// Processes multiple velocity values through the curve.
    /// </summary>
    /// <param name="velocities">Input velocities.</param>
    /// <returns>Processed velocities.</returns>
    public int[] ProcessVelocities(int[] velocities)
    {
        if (velocities == null)
            throw new ArgumentNullException(nameof(velocities));

        var result = new int[velocities.Length];

        if (!Enabled)
        {
            Array.Copy(velocities, result, velocities.Length);
            return result;
        }

        lock (_lock)
        {
            EnsureLookupTable();
            for (int i = 0; i < velocities.Length; i++)
            {
                int v = Math.Clamp(velocities[i], 0, 127);
                result[i] = _lookupTable[v];
            }
        }

        return result;
    }

    /// <summary>
    /// Sets the curve type and optional parameters.
    /// </summary>
    /// <param name="curveType">The curve type.</param>
    /// <param name="curveAmount">Optional curve intensity (0.1 to 10.0).</param>
    public void SetCurve(VelocityCurveType curveType, float? curveAmount = null)
    {
        lock (_lock)
        {
            _config.CurveType = curveType;
            if (curveAmount.HasValue)
            {
                _config.CurveAmount = Math.Clamp(curveAmount.Value, 0.1f, 10.0f);
            }
            InvalidateLookupTable();
        }
    }

    /// <summary>
    /// Sets the input and output range.
    /// </summary>
    /// <param name="inputMin">Minimum input velocity.</param>
    /// <param name="inputMax">Maximum input velocity.</param>
    /// <param name="outputMin">Minimum output velocity.</param>
    /// <param name="outputMax">Maximum output velocity.</param>
    public void SetRange(int inputMin, int inputMax, int outputMin, int outputMax)
    {
        lock (_lock)
        {
            _config.InputMin = Math.Clamp(inputMin, 0, 127);
            _config.InputMax = Math.Clamp(inputMax, 0, 127);
            _config.OutputMin = Math.Clamp(outputMin, 0, 127);
            _config.OutputMax = Math.Clamp(outputMax, 0, 127);
            InvalidateLookupTable();
        }
    }

    /// <summary>
    /// Sets custom curve points for custom curve type.
    /// </summary>
    /// <param name="points">List of curve points.</param>
    public void SetCustomCurvePoints(IEnumerable<VelocityCurvePoint> points)
    {
        if (points == null)
            throw new ArgumentNullException(nameof(points));

        lock (_lock)
        {
            _config.CustomCurvePoints = points.Select(p => p.Clone()).ToList();
            // Sort by input value for proper interpolation
            _config.CustomCurvePoints.Sort((a, b) => a.InputValue.CompareTo(b.InputValue));
            InvalidateLookupTable();
        }
    }

    /// <summary>
    /// Adds a custom curve point.
    /// </summary>
    /// <param name="inputValue">Input velocity value.</param>
    /// <param name="outputValue">Output velocity value.</param>
    public void AddCustomCurvePoint(int inputValue, int outputValue)
    {
        lock (_lock)
        {
            // Remove existing point at same input value
            _config.CustomCurvePoints.RemoveAll(p => p.InputValue == inputValue);
            _config.CustomCurvePoints.Add(new VelocityCurvePoint(inputValue, outputValue));
            _config.CustomCurvePoints.Sort((a, b) => a.InputValue.CompareTo(b.InputValue));
            InvalidateLookupTable();
        }
    }

    /// <summary>
    /// Clears all custom curve points.
    /// </summary>
    public void ClearCustomCurvePoints()
    {
        lock (_lock)
        {
            _config.CustomCurvePoints.Clear();
            InvalidateLookupTable();
        }
    }

    /// <summary>
    /// Loads a preset velocity curve.
    /// </summary>
    /// <param name="preset">The preset to load.</param>
    public void LoadPreset(VelocityCurvePreset preset)
    {
        lock (_lock)
        {
            _config.CustomCurvePoints.Clear();

            switch (preset)
            {
                case VelocityCurvePreset.Default:
                    _config.CurveType = VelocityCurveType.Linear;
                    _config.InputMin = 0;
                    _config.InputMax = 127;
                    _config.OutputMin = 0;
                    _config.OutputMax = 127;
                    _config.CurveAmount = 1.0f;
                    break;

                case VelocityCurvePreset.SoftTouch:
                    _config.CurveType = VelocityCurveType.Logarithmic;
                    _config.InputMin = 0;
                    _config.InputMax = 127;
                    _config.OutputMin = 20;
                    _config.OutputMax = 127;
                    _config.CurveAmount = 1.5f;
                    break;

                case VelocityCurvePreset.HardTouch:
                    _config.CurveType = VelocityCurveType.Exponential;
                    _config.InputMin = 0;
                    _config.InputMax = 127;
                    _config.OutputMin = 0;
                    _config.OutputMax = 127;
                    _config.CurveAmount = 2.0f;
                    break;

                case VelocityCurvePreset.KeyboardPlayer:
                    _config.CurveType = VelocityCurveType.SCurve;
                    _config.InputMin = 0;
                    _config.InputMax = 127;
                    _config.OutputMin = 10;
                    _config.OutputMax = 120;
                    _config.CurveAmount = 1.2f;
                    break;

                case VelocityCurvePreset.DrumMachine:
                    _config.CurveType = VelocityCurveType.Exponential;
                    _config.InputMin = 0;
                    _config.InputMax = 127;
                    _config.OutputMin = 40;
                    _config.OutputMax = 127;
                    _config.CurveAmount = 0.7f;
                    break;

                case VelocityCurvePreset.Organ:
                    _config.CurveType = VelocityCurveType.Logarithmic;
                    _config.InputMin = 0;
                    _config.InputMax = 127;
                    _config.OutputMin = 80;
                    _config.OutputMax = 120;
                    _config.CurveAmount = 0.5f;
                    break;

                case VelocityCurvePreset.SynthLead:
                    _config.CurveType = VelocityCurveType.Exponential;
                    _config.InputMin = 0;
                    _config.InputMax = 127;
                    _config.OutputMin = 30;
                    _config.OutputMax = 127;
                    _config.CurveAmount = 1.8f;
                    break;

                case VelocityCurvePreset.Pad:
                    _config.CurveType = VelocityCurveType.Logarithmic;
                    _config.InputMin = 0;
                    _config.InputMax = 127;
                    _config.OutputMin = 50;
                    _config.OutputMax = 100;
                    _config.CurveAmount = 0.8f;
                    break;

                case VelocityCurvePreset.Bass:
                    _config.CurveType = VelocityCurveType.SCurve;
                    _config.InputMin = 0;
                    _config.InputMax = 127;
                    _config.OutputMin = 60;
                    _config.OutputMax = 110;
                    _config.CurveAmount = 1.0f;
                    break;

                case VelocityCurvePreset.Expressive:
                    _config.CurveType = VelocityCurveType.Linear;
                    _config.InputMin = 0;
                    _config.InputMax = 127;
                    _config.OutputMin = 1;
                    _config.OutputMax = 127;
                    _config.CurveAmount = 1.0f;
                    break;

                case VelocityCurvePreset.Compressed:
                    _config.CurveType = VelocityCurveType.Logarithmic;
                    _config.InputMin = 0;
                    _config.InputMax = 127;
                    _config.OutputMin = 60;
                    _config.OutputMax = 110;
                    _config.CurveAmount = 2.0f;
                    break;

                case VelocityCurvePreset.BeginnerFriendly:
                    _config.CurveType = VelocityCurveType.Logarithmic;
                    _config.InputMin = 0;
                    _config.InputMax = 127;
                    _config.OutputMin = 50;
                    _config.OutputMax = 100;
                    _config.CurveAmount = 1.5f;
                    break;

                case VelocityCurvePreset.ConcertPianist:
                    _config.CurveType = VelocityCurveType.SCurve;
                    _config.InputMin = 0;
                    _config.InputMax = 127;
                    _config.OutputMin = 5;
                    _config.OutputMax = 127;
                    _config.CurveAmount = 1.3f;
                    break;

                case VelocityCurvePreset.Electronic:
                    _config.CurveType = VelocityCurveType.Exponential;
                    _config.InputMin = 0;
                    _config.InputMax = 127;
                    _config.OutputMin = 50;
                    _config.OutputMax = 127;
                    _config.CurveAmount = 1.5f;
                    break;

                case VelocityCurvePreset.Jazz:
                    _config.CurveType = VelocityCurveType.Custom;
                    _config.InputMin = 0;
                    _config.InputMax = 127;
                    _config.OutputMin = 0;
                    _config.OutputMax = 127;
                    _config.CurveAmount = 1.0f;
                    // Jazz curve: nuanced low velocities, compressed highs
                    _config.CustomCurvePoints.Add(new VelocityCurvePoint(0, 0));
                    _config.CustomCurvePoints.Add(new VelocityCurvePoint(20, 30));
                    _config.CustomCurvePoints.Add(new VelocityCurvePoint(40, 55));
                    _config.CustomCurvePoints.Add(new VelocityCurvePoint(60, 75));
                    _config.CustomCurvePoints.Add(new VelocityCurvePoint(80, 95));
                    _config.CustomCurvePoints.Add(new VelocityCurvePoint(100, 110));
                    _config.CustomCurvePoints.Add(new VelocityCurvePoint(127, 120));
                    break;
            }

            InvalidateLookupTable();
        }
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    /// <returns>A copy of the current configuration.</returns>
    public VelocityCurveConfig GetConfiguration()
    {
        lock (_lock)
        {
            return _config.Clone();
        }
    }

    /// <summary>
    /// Sets the configuration.
    /// </summary>
    /// <param name="config">The configuration to use.</param>
    public void SetConfiguration(VelocityCurveConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        lock (_lock)
        {
            _config = config.Clone();
            InvalidateLookupTable();
        }
    }

    /// <summary>
    /// Gets the full lookup table for visualization or debugging.
    /// </summary>
    /// <returns>Array of 128 output values indexed by input velocity.</returns>
    public int[] GetLookupTable()
    {
        lock (_lock)
        {
            EnsureLookupTable();
            var copy = new int[128];
            Array.Copy(_lookupTable, copy, 128);
            return copy;
        }
    }

    /// <summary>
    /// Resets to default linear curve.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _config = new VelocityCurveConfig();
            InvalidateLookupTable();
        }
    }

    private void InvalidateLookupTable()
    {
        _lookupTableValid = false;
    }

    private void EnsureLookupTable()
    {
        if (_lookupTableValid)
            return;

        BuildLookupTable();
        _lookupTableValid = true;
    }

    private void BuildLookupTable()
    {
        for (int i = 0; i < 128; i++)
        {
            _lookupTable[i] = CalculateOutputVelocity(i);
        }
    }

    private int CalculateOutputVelocity(int input)
    {
        // Handle fixed velocity
        if (_config.CurveType == VelocityCurveType.Fixed)
        {
            return input == 0 ? 0 : _config.FixedVelocity;
        }

        // Clamp input to range
        int clampedInput = Math.Clamp(input, _config.InputMin, _config.InputMax);

        // Calculate normalized position (0.0 to 1.0)
        float inputRange = _config.InputMax - _config.InputMin;
        float normalized = inputRange > 0 ? (clampedInput - _config.InputMin) / inputRange : 0f;

        // Apply curve transformation
        float curved = ApplyCurve(normalized, _config.CurveType, _config.CurveAmount);

        // Map to output range
        float outputRange = _config.OutputMax - _config.OutputMin;
        float output = _config.OutputMin + (curved * outputRange);

        // Preserve velocity 0 as 0 (note off)
        if (input == 0)
            return 0;

        // Ensure minimum velocity of 1 for non-zero input
        return Math.Max(1, Math.Clamp((int)Math.Round(output), 0, 127));
    }

    private float ApplyCurve(float normalized, VelocityCurveType curveType, float amount)
    {
        switch (curveType)
        {
            case VelocityCurveType.Linear:
                return normalized;

            case VelocityCurveType.Exponential:
                // Exponential curve: quiet notes become quieter
                // f(x) = x^amount
                return (float)Math.Pow(normalized, amount);

            case VelocityCurveType.Logarithmic:
                // Logarithmic curve: quiet notes become louder
                // f(x) = log(1 + x*(e^amount-1)) / amount
                if (amount <= 0) return normalized;
                double expAmount = Math.Exp(amount) - 1;
                return (float)(Math.Log(1 + normalized * expAmount) / amount);

            case VelocityCurveType.SCurve:
                // S-Curve: smooth transition with soft extremes
                // Uses a smoothstep-like function with adjustable steepness
                float t = normalized;
                float steepness = amount;

                // Apply smoothstep with variable steepness
                if (t <= 0) return 0;
                if (t >= 1) return 1;

                // Modified sigmoid for S-curve
                double centered = (t - 0.5) * 2; // -1 to 1
                double sigmoid = 1.0 / (1.0 + Math.Exp(-centered * steepness * 4));
                return (float)sigmoid;

            case VelocityCurveType.Custom:
                return InterpolateCustomCurve(normalized);

            default:
                return normalized;
        }
    }

    private float InterpolateCustomCurve(float normalized)
    {
        var points = _config.CustomCurvePoints;

        if (points.Count == 0)
            return normalized;

        // Convert normalized (0-1) to velocity (0-127)
        float inputVelocity = normalized * 127f;

        // Find surrounding points
        VelocityCurvePoint? lower = null;
        VelocityCurvePoint? upper = null;

        foreach (var point in points)
        {
            if (point.InputValue <= inputVelocity)
            {
                lower = point;
            }
            else if (upper == null)
            {
                upper = point;
                break;
            }
        }

        // Handle edge cases
        if (lower == null && upper == null)
            return normalized;

        if (lower == null)
            return upper!.OutputValue / 127f;

        if (upper == null)
            return lower.OutputValue / 127f;

        // Linear interpolation between points
        float range = upper.InputValue - lower.InputValue;
        if (range <= 0)
            return lower.OutputValue / 127f;

        float t = (inputVelocity - lower.InputValue) / range;
        float outputVelocity = lower.OutputValue + t * (upper.OutputValue - lower.OutputValue);

        return outputVelocity / 127f;
    }

    #region Static Factory Methods

    /// <summary>
    /// Creates a linear velocity curve.
    /// </summary>
    /// <returns>A new linear velocity curve.</returns>
    public static MidiVelocityCurve CreateLinear()
    {
        var curve = new MidiVelocityCurve();
        curve.LoadPreset(VelocityCurvePreset.Default);
        return curve;
    }

    /// <summary>
    /// Creates an exponential velocity curve.
    /// </summary>
    /// <param name="amount">Curve amount (0.1 to 10.0).</param>
    /// <returns>A new exponential velocity curve.</returns>
    public static MidiVelocityCurve CreateExponential(float amount = 2.0f)
    {
        var curve = new MidiVelocityCurve(VelocityCurveType.Exponential);
        curve.CurveAmount = amount;
        return curve;
    }

    /// <summary>
    /// Creates a logarithmic velocity curve.
    /// </summary>
    /// <param name="amount">Curve amount (0.1 to 10.0).</param>
    /// <returns>A new logarithmic velocity curve.</returns>
    public static MidiVelocityCurve CreateLogarithmic(float amount = 2.0f)
    {
        var curve = new MidiVelocityCurve(VelocityCurveType.Logarithmic);
        curve.CurveAmount = amount;
        return curve;
    }

    /// <summary>
    /// Creates an S-curve velocity curve.
    /// </summary>
    /// <param name="amount">Curve steepness (0.1 to 10.0).</param>
    /// <returns>A new S-curve velocity curve.</returns>
    public static MidiVelocityCurve CreateSCurve(float amount = 1.5f)
    {
        var curve = new MidiVelocityCurve(VelocityCurveType.SCurve);
        curve.CurveAmount = amount;
        return curve;
    }

    /// <summary>
    /// Creates a fixed velocity curve.
    /// </summary>
    /// <param name="velocity">Fixed velocity value (1-127).</param>
    /// <returns>A new fixed velocity curve.</returns>
    public static MidiVelocityCurve CreateFixed(int velocity)
    {
        var curve = new MidiVelocityCurve(VelocityCurveType.Fixed);
        curve.FixedVelocity = Math.Clamp(velocity, 1, 127);
        return curve;
    }

    /// <summary>
    /// Creates a velocity curve from a preset.
    /// </summary>
    /// <param name="preset">The preset to use.</param>
    /// <returns>A new velocity curve with the preset applied.</returns>
    public static MidiVelocityCurve CreateFromPreset(VelocityCurvePreset preset)
    {
        var curve = new MidiVelocityCurve();
        curve.LoadPreset(preset);
        return curve;
    }

    /// <summary>
    /// Creates a custom velocity curve from points.
    /// </summary>
    /// <param name="points">The curve points.</param>
    /// <returns>A new custom velocity curve.</returns>
    public static MidiVelocityCurve CreateCustom(IEnumerable<VelocityCurvePoint> points)
    {
        var curve = new MidiVelocityCurve(VelocityCurveType.Custom);
        curve.SetCustomCurvePoints(points);
        return curve;
    }

    /// <summary>
    /// Creates a custom velocity curve from input/output pairs.
    /// </summary>
    /// <param name="pairs">Array of (input, output) tuples.</param>
    /// <returns>A new custom velocity curve.</returns>
    public static MidiVelocityCurve CreateCustom(params (int input, int output)[] pairs)
    {
        var points = pairs.Select(p => new VelocityCurvePoint(p.input, p.output));
        return CreateCustom(points);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Gets all available preset names.
    /// </summary>
    /// <returns>Array of preset names.</returns>
    public static string[] GetPresetNames()
    {
        return Enum.GetNames(typeof(VelocityCurvePreset));
    }

    /// <summary>
    /// Gets all available curve type names.
    /// </summary>
    /// <returns>Array of curve type names.</returns>
    public static string[] GetCurveTypeNames()
    {
        return Enum.GetNames(typeof(VelocityCurveType));
    }

    /// <summary>
    /// Parses a preset name to enum value.
    /// </summary>
    /// <param name="name">The preset name.</param>
    /// <param name="preset">The resulting preset.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool TryParsePreset(string name, out VelocityCurvePreset preset)
    {
        return Enum.TryParse(name, true, out preset);
    }

    /// <summary>
    /// Parses a curve type name to enum value.
    /// </summary>
    /// <param name="name">The curve type name.</param>
    /// <param name="curveType">The resulting curve type.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool TryParseCurveType(string name, out VelocityCurveType curveType)
    {
        return Enum.TryParse(name, true, out curveType);
    }

    #endregion
}
