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
    public static ModVar Bind(Func<float> get, Action<float> set, float? initial = null)
        => new ModVar(get, set, initial);

    public static ModVar Var(Func<float> get, Action<float> set, float? initial = null)
        => Bind(get, set, initial);

    public static ModVar Var(object target, string member, float? initial = null)
        => CreateVar(target, member, initial);

    public static ModVar Param(object target, string member, float? initial = null)
        => Var(target, member, initial);

    public static ModGroup Group(params ModVar[] vars)
        => new ModGroup().Add(vars);

    public static ModVar Volume(IInstrumentControls instrument, float? initial = null)
        => Bind(() => instrument.Volume, v => instrument.Volume = v, initial);

    public static ModVar Pan(IInstrumentControls instrument, float? initial = null)
        => Bind(() => instrument.Pan, v => instrument.Pan = v, initial);

    public static ModVar Reverb(IInstrumentControls instrument, float? initial = null)
        => Bind(() => instrument.Reverb, v => instrument.Reverb = v, initial);

    public static ModVar Chorus(IInstrumentControls instrument, float? initial = null)
        => Bind(() => instrument.Chorus, v => instrument.Chorus = v, initial);

    public static ModVar ModWheel(IInstrumentControls instrument, float? initial = null)
        => Bind(() => instrument.ModWheel, v => instrument.ModWheel = v, initial);

    public static ModVar Gain(AudioInput input, float? initial = null)
        => Bind(() => input.Gain, v => input.Gain = v, initial);

    public static ModVar Mix(SimpleDelayEffect effect, float? initial = null)
        => Bind(() => effect.Mix, v => effect.Mix = v, initial);

    public static ModVar TimeMs(SimpleDelayEffect effect, float? initial = null)
        => Bind(() => effect.TimeMs, v => effect.TimeMs = v, initial);

    public static ModVar Feedback(SimpleDelayEffect effect, float? initial = null)
        => Bind(() => effect.Feedback, v => effect.Feedback = v, initial);

    public static ModVar Mix(SimpleReverbEffect effect, float? initial = null)
        => Bind(() => effect.Mix, v => effect.Mix = v, initial);

    public static ModVar Size(SimpleReverbEffect effect, float? initial = null)
        => Bind(() => effect.Size, v => effect.Size = v, initial);

    public static ModVar Damping(SimpleReverbEffect effect, float? initial = null)
        => Bind(() => effect.Damping, v => effect.Damping = v, initial);

    public static ModVar Depth(TremoloEffect effect, float? initial = null)
        => Bind(() => effect.Depth, v => effect.Depth = v, initial);

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
