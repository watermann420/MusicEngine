// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Modulation helpers for binding variables.

using System;
using System.Reflection;
using MusicEngine.Effects.Audio;
using MusicEngine.Instruments;
using MusicEngine.Vst;

namespace MusicEngine.Core.Modulation;

/// <summary>
/// Modulation helpers for binding variables.
/// </summary>
public static class Mod
{
    /// <summary>
    /// Bind a getter/setter pair to a modulated variable.
    /// </summary>
    public static ModVar Bind(Func<float> get, Action<float> set, float? initial = null)
        => new ModVar(get, set, initial);

    /// <summary>
    /// Bind a getter/setter pair to a modulated variable.
    /// </summary>
    public static ModVar Var(Func<float> get, Action<float> set, float? initial = null)
        => Bind(get, set, initial);

    /// <summary>
    /// Bind a writable property/field by name to a modulated variable.
    /// </summary>
    public static ModVar Var(object target, string member, float? initial = null)
        => CreateVar(target, member, initial);

    /// <summary>
    /// Alias for Var (property/field binding).
    /// </summary>
    public static ModVar Param(object target, string member, float? initial = null)
        => Var(target, member, initial);

    /// <summary>
    /// Group multiple mod variables for combined enable/disable.
    /// </summary>
    public static ModGroup Group(params ModVar[] vars)
        => new ModGroup().Add(vars);

    /// <summary>
    /// Bind an instrument volume to a modulated variable.
    /// </summary>
    public static ModVar Volume(IInstrumentControls instrument, float? initial = null)
        => Bind(() => instrument.Volume, v => instrument.Volume = v, initial);

    /// <summary>
    /// Bind an instrument pan to a modulated variable.
    /// </summary>
    public static ModVar Pan(IInstrumentControls instrument, float? initial = null)
        => Bind(() => instrument.Pan, v => instrument.Pan = v, initial);

    /// <summary>
    /// Bind an instrument reverb to a modulated variable.
    /// </summary>
    public static ModVar Reverb(IInstrumentControls instrument, float? initial = null)
        => Bind(() => instrument.Reverb, v => instrument.Reverb = v, initial);

    /// <summary>
    /// Bind an instrument chorus to a modulated variable.
    /// </summary>
    public static ModVar Chorus(IInstrumentControls instrument, float? initial = null)
        => Bind(() => instrument.Chorus, v => instrument.Chorus = v, initial);

    /// <summary>
    /// Bind an instrument mod wheel to a modulated variable.
    /// </summary>
    public static ModVar ModWheel(IInstrumentControls instrument, float? initial = null)
        => Bind(() => instrument.ModWheel, v => instrument.ModWheel = v, initial);

    /// <summary>
    /// Bind an input gain to a modulated variable.
    /// </summary>
    public static ModVar Gain(AudioInput input, float? initial = null)
        => Bind(() => input.Gain, v => input.Gain = v, initial);

    /// <summary>
    /// Bind a delay mix to a modulated variable.
    /// </summary>
    public static ModVar Mix(SimpleDelayEffect effect, float? initial = null)
        => Bind(() => effect.Mix, v => effect.Mix = v, initial);

    /// <summary>
    /// Bind a delay time (ms) to a modulated variable.
    /// </summary>
    public static ModVar TimeMs(SimpleDelayEffect effect, float? initial = null)
        => Bind(() => effect.TimeMs, v => effect.TimeMs = v, initial);

    /// <summary>
    /// Bind a delay feedback to a modulated variable.
    /// </summary>
    public static ModVar Feedback(SimpleDelayEffect effect, float? initial = null)
        => Bind(() => effect.Feedback, v => effect.Feedback = v, initial);

    /// <summary>
    /// Bind a reverb mix to a modulated variable.
    /// </summary>
    public static ModVar Mix(SimpleReverbEffect effect, float? initial = null)
        => Bind(() => effect.Mix, v => effect.Mix = v, initial);

    /// <summary>
    /// Bind a reverb size to a modulated variable.
    /// </summary>
    public static ModVar Size(SimpleReverbEffect effect, float? initial = null)
        => Bind(() => effect.Size, v => effect.Size = v, initial);

    /// <summary>
    /// Bind a reverb damping to a modulated variable.
    /// </summary>
    public static ModVar Damping(SimpleReverbEffect effect, float? initial = null)
        => Bind(() => effect.Damping, v => effect.Damping = v, initial);

    /// <summary>
    /// Bind a tremolo depth to a modulated variable.
    /// </summary>
    public static ModVar Depth(TremoloEffect effect, float? initial = null)
        => Bind(() => effect.Depth, v => effect.Depth = v, initial);

    /// <summary>
    /// Bind a tremolo rate to a modulated variable.
    /// </summary>
    public static ModVar Rate(TremoloEffect effect, float? initial = null)
        => Bind(() => effect.Rate, v => effect.Rate = v, initial);

    private static ModVar CreateVar(object target, string member, float? initial)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (string.IsNullOrWhiteSpace(member)) throw new ArgumentException("Member name is required.", nameof(member));

        var (getter, setter, memberType) = ResolveAccessor(target.GetType(), member);
        return new ModVar(
            get: () => ConvertFromMember(getter(target), memberType),
            set: value => setter(target, ConvertValue(value, memberType, 0f, 1f)),
            initial: initial
        );
    }

    private static (Func<object, object?> getter, Action<object, object?> setter, Type memberType) ResolveAccessor(
        Type type, string member)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;
        var prop = type.GetProperty(member, flags);
        if (prop != null && prop.CanRead && prop.CanWrite)
        {
            return (obj => prop.GetValue(obj), (obj, value) => prop.SetValue(obj, value), prop.PropertyType);
        }

        var field = type.GetField(member, flags);
        if (field != null && !field.IsInitOnly)
        {
            return (obj => field.GetValue(obj), (obj, value) => field.SetValue(obj, value), field.FieldType);
        }

        throw new InvalidOperationException($"Member '{member}' not found or not writable on {type.Name}.");
    }

    private static object? ConvertValue(float value, Type targetType, float min, float max)
    {
        if (targetType == typeof(float)) return value;
        if (targetType == typeof(double)) return (double)value;
        if (targetType == typeof(int)) return (int)Math.Round(value);
        if (targetType == typeof(short)) return (short)Math.Round(value);
        if (targetType == typeof(long)) return (long)Math.Round(value);
        if (targetType == typeof(byte)) return (byte)Math.Max(byte.MinValue, Math.Min(byte.MaxValue, Math.Round(value)));
        if (targetType == typeof(bool)) return value > 0.5f;

        if (targetType.IsEnum)
        {
            var values = Enum.GetValues(targetType);
            int maxIndex = Math.Max(0, values.Length - 1);
            int index = (int)Math.Round(Math.Clamp(value, min, max));
            index = Math.Clamp(index, 0, maxIndex);
            return values.GetValue(index);
        }

        return Convert.ChangeType(value, targetType);
    }

    private static float ConvertFromMember(object? value, Type targetType)
    {
        if (value == null) return 0f;
        if (targetType == typeof(float)) return (float)value;
        if (targetType == typeof(double)) return (float)(double)value;
        if (targetType == typeof(int)) return (int)value;
        if (targetType == typeof(short)) return (short)value;
        if (targetType == typeof(long)) return (long)value;
        if (targetType == typeof(byte)) return (byte)value;
        if (targetType == typeof(bool)) return (bool)value ? 1f : 0f;

        if (targetType.IsEnum)
        {
            return Convert.ToSingle(value);
        }

        return Convert.ToSingle(value);
    }
}
