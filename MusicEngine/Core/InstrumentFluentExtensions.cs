// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Fluent helpers for instrument setup.

using MusicEngine.Instruments;
using MusicEngine.Vst;

namespace MusicEngine.Core;

/// <summary>
/// Fluent helpers for instrument setup.
/// </summary>
public static class InstrumentFluentExtensions
{
    /// <summary>
    /// Set instrument volume (fluent).
    /// </summary>
    public static T Volume<T>(this T instrument, float value) where T : IInstrumentControls
    {
        instrument.Volume = value;
        return instrument;
    }

    /// <summary>
    /// Set instrument volume (lowercase alias).
    /// </summary>
    public static T volume<T>(this T instrument, float value) where T : IInstrumentControls
        => instrument.Volume(value);

    /// <summary>
    /// Set instrument pan (fluent).
    /// </summary>
    public static T Pan<T>(this T instrument, float value) where T : IInstrumentControls
    {
        instrument.Pan = value;
        return instrument;
    }

    /// <summary>
    /// Set instrument pan (lowercase alias).
    /// </summary>
    public static T pan<T>(this T instrument, float value) where T : IInstrumentControls
        => instrument.Pan(value);

    /// <summary>
    /// Set instrument MIDI channel (fluent).
    /// </summary>
    public static T Channel<T>(this T instrument, int value) where T : IInstrumentControls
    {
        instrument.Channel = value;
        return instrument;
    }

    /// <summary>
    /// Set instrument MIDI channel (lowercase alias).
    /// </summary>
    public static T channel<T>(this T instrument, int value) where T : IInstrumentControls
        => instrument.Channel(value);

    /// <summary>
    /// Set instrument reverb amount (fluent).
    /// </summary>
    public static T Reverb<T>(this T instrument, float value) where T : IInstrumentControls
    {
        instrument.Reverb = value;
        return instrument;
    }

    /// <summary>
    /// Set instrument reverb amount (lowercase alias).
    /// </summary>
    public static T reverb<T>(this T instrument, float value) where T : IInstrumentControls
        => instrument.Reverb(value);

    /// <summary>
    /// Set instrument chorus amount (fluent).
    /// </summary>
    public static T Chorus<T>(this T instrument, float value) where T : IInstrumentControls
    {
        instrument.Chorus = value;
        return instrument;
    }

    /// <summary>
    /// Set instrument chorus amount (lowercase alias).
    /// </summary>
    public static T chorus<T>(this T instrument, float value) where T : IInstrumentControls
        => instrument.Chorus(value);

    /// <summary>
    /// Set instrument mod wheel value (fluent).
    /// </summary>
    public static T ModWheel<T>(this T instrument, float value) where T : IInstrumentControls
    {
        instrument.ModWheel = value;
        return instrument;
    }

    /// <summary>
    /// Set instrument mod wheel value (lowercase alias).
    /// </summary>
    public static T modwheel<T>(this T instrument, float value) where T : IInstrumentControls
        => instrument.ModWheel(value);

    /// <summary>
    /// Set pitch bend for VSTs or mapped instruments (fluent).
    /// </summary>
    public static ISynth PitchBend(this ISynth instrument, float value)
    {
        if (instrument is IVstInstrument vst)
        {
            vst.PitchBend(value);
        }
        else
        {
            instrument.SetParameter("pitchbend", value);
        }

        return instrument;
    }

    /// <summary>
    /// Set pitch bend (alias).
    /// </summary>
    public static ISynth Pitchbend(this ISynth instrument, float value)
        => instrument.PitchBend(value);

    /// <summary>
    /// Set pitch bend (lowercase alias).
    /// </summary>
    public static ISynth pitchbend(this ISynth instrument, float value)
        => instrument.PitchBend(value);

    /// <summary>
    /// Set instrument name (fluent).
    /// </summary>
    public static T Name<T>(this T instrument, string value) where T : ISynth
    {
        instrument.Name = value;
        return instrument;
    }

    /// <summary>
    /// Set instrument name (lowercase alias).
    /// </summary>
    public static T name<T>(this T instrument, string value) where T : ISynth
        => instrument.Name(value);

    /// <summary>
    /// Set General MIDI program (fluent).
    /// </summary>
    public static GeneralMidiInstrument Program(this GeneralMidiInstrument instrument, GeneralMidiProgram program)
    {
        instrument.Program = program;
        return instrument;
    }

    /// <summary>
    /// Set General MIDI program (lowercase alias).
    /// </summary>
    public static GeneralMidiInstrument program(this GeneralMidiInstrument instrument, GeneralMidiProgram program)
        => instrument.Program(program);

    /// <summary>
    /// Set input gain (fluent).
    /// </summary>
    public static AudioInput Gain(this AudioInput input, float value)
    {
        input.Gain = value;
        return input;
    }

    /// <summary>
    /// Set input gain (lowercase alias).
    /// </summary>
    public static AudioInput gain(this AudioInput input, float value)
        => input.Gain(value);

    /// <summary>
    /// Set input mute (fluent).
    /// </summary>
    public static AudioInput Mute(this AudioInput input, bool value)
    {
        input.Mute = value;
        return input;
    }

    /// <summary>
    /// Set input mute (lowercase alias).
    /// </summary>
    public static AudioInput mute(this AudioInput input, bool value)
        => input.Mute(value);

    /// <summary>
    /// Restore VST state from base64 (fluent).
    /// </summary>
    public static IVstInstrument State(this IVstInstrument instrument, string base64)
    {
        instrument.State(base64);
        return instrument;
    }
}
