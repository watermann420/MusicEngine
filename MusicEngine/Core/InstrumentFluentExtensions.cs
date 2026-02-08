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
    public static T Volume<T>(this T instrument, float value) where T : IInstrumentControls
    {
        instrument.Volume = value;
        return instrument;
    }

    public static T volume<T>(this T instrument, float value) where T : IInstrumentControls
        => instrument.Volume(value);

    public static T Pan<T>(this T instrument, float value) where T : IInstrumentControls
    {
        instrument.Pan = value;
        return instrument;
    }

    public static T pan<T>(this T instrument, float value) where T : IInstrumentControls
        => instrument.Pan(value);

    public static T Channel<T>(this T instrument, int value) where T : IInstrumentControls
    {
        instrument.Channel = value;
        return instrument;
    }

    public static T channel<T>(this T instrument, int value) where T : IInstrumentControls
        => instrument.Channel(value);

    public static T Reverb<T>(this T instrument, float value) where T : IInstrumentControls
    {
        instrument.Reverb = value;
        return instrument;
    }

    public static T reverb<T>(this T instrument, float value) where T : IInstrumentControls
        => instrument.Reverb(value);

    public static T Chorus<T>(this T instrument, float value) where T : IInstrumentControls
    {
        instrument.Chorus = value;
        return instrument;
    }

    public static T chorus<T>(this T instrument, float value) where T : IInstrumentControls
        => instrument.Chorus(value);

    public static T ModWheel<T>(this T instrument, float value) where T : IInstrumentControls
    {
        instrument.ModWheel = value;
        return instrument;
    }

    public static T modwheel<T>(this T instrument, float value) where T : IInstrumentControls
        => instrument.ModWheel(value);

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

    public static ISynth Pitchbend(this ISynth instrument, float value)
        => instrument.PitchBend(value);

    public static ISynth pitchbend(this ISynth instrument, float value)
        => instrument.PitchBend(value);

    public static T Name<T>(this T instrument, string value) where T : ISynth
    {
        instrument.Name = value;
        return instrument;
    }

    public static T name<T>(this T instrument, string value) where T : ISynth
        => instrument.Name(value);

    public static GeneralMidiInstrument Program(this GeneralMidiInstrument instrument, GeneralMidiProgram program)
    {
        instrument.Program = program;
        return instrument;
    }

    public static GeneralMidiInstrument program(this GeneralMidiInstrument instrument, GeneralMidiProgram program)
        => instrument.Program(program);

    public static AudioInput Gain(this AudioInput input, float value)
    {
        input.Gain = value;
        return input;
    }

    public static AudioInput gain(this AudioInput input, float value)
        => input.Gain(value);

    public static AudioInput Mute(this AudioInput input, bool value)
    {
        input.Mute = value;
        return input;
    }

    public static AudioInput mute(this AudioInput input, bool value)
        => input.Mute(value);

    public static IVstInstrument State(this IVstInstrument instrument, string base64)
    {
        instrument.State(base64);
        return instrument;
    }
}
