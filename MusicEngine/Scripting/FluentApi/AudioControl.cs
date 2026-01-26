// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Threading.Tasks;
using MusicEngine.Core;


namespace MusicEngine.Scripting.FluentApi;


// Control for audio operations
public class AudioControl
{
    private readonly ScriptGlobals _globals; // Reference to script globals
    public AudioControl(ScriptGlobals globals) => _globals = globals; // Constructor

    public ChannelControl channel(int index) => new ChannelControl(_globals.Engine, index); // Access specific channel control

    /// <summary>Alias for channel - PascalCase version</summary>
    public ChannelControl Channel(int index) => channel(index);
    /// <summary>Alias for channel - Short form</summary>
    public ChannelControl ch(int index) => channel(index);
    /// <summary>Alias for channel - Audio track control</summary>
    public ChannelControl track(int index) => channel(index);

    public AllChannelsControl all => new AllChannelsControl(_globals.Engine); // Access all channels control

    /// <summary>Alias for all - PascalCase version</summary>
    public AllChannelsControl All => all;
    /// <summary>Alias for all - Access all audio channels</summary>
    public AllChannelsControl allChannels => all;

    public InputControl input(int index) => new InputControl(_globals, index); // Access audio input control

    /// <summary>Alias for input - PascalCase version</summary>
    public InputControl Input(int index) => input(index);
    /// <summary>Alias for input - Short form</summary>
    public InputControl @in(int index) => input(index);
    /// <summary>Alias for input - Audio capture control</summary>
    public InputControl capture(int index) => input(index);
}

// Control for audio input frequency triggers
public class InputControl
{
    private readonly ScriptGlobals _globals; // Reference to script globals
    private readonly int _deviceIndex; // Audio device index

    // Constructor to initialize input control
    public InputControl(ScriptGlobals globals, int deviceIndex)
    {
        _globals = globals;
        _deviceIndex = deviceIndex;
    }

    // Creates a frequency trigger configuration for the specified frequency range
    public FrequencyTriggerConfig onFrequency(float low, float high)
    {
        return new FrequencyTriggerConfig(_globals, _deviceIndex, low, high);
    }
}

// Configuration for frequency-based triggers
public class FrequencyTriggerConfig
{
    private readonly ScriptGlobals _globals; // Reference to script globals
    private readonly int _deviceIndex; // Audio device index
    private readonly float _low; // Low frequency in Hz
    private readonly float _high; // High frequency in Hz
    private float _threshold = 0.1f; // Default magnitude threshold

    // Constructor to initialize frequency trigger configuration
    public FrequencyTriggerConfig(ScriptGlobals globals, int deviceIndex, float low, float high)
    {
        _globals = globals; // Initialize globals
        _deviceIndex = deviceIndex; // Initialize device index
        _low = low; // Initialize low frequency
        _high = high; // Initialize high frequency
    }

    // Sets the magnitude threshold for triggering
    public FrequencyTriggerConfig threshold(float value)
    {
        _threshold = value;
        return this;
    }

    // Triggers a MIDI note on a synthesizer when the frequency condition is met
    public void trigger(ISynth synth, int note, int velocity = 100)
    {
        _globals.AddFrequencyTrigger(_deviceIndex, _low, _high, _threshold, mag => {
            synth.NoteOn(note, (int)(velocity * Math.Clamp(mag * 2, 0, 1)));
            // We might need a way to turn it off, but usually for drum triggers it's a short hit.
            // For now, let's just trigger NoteOn.
            // Better: trigger and then off after a short delay or if it's a "gate"
            Task.Delay(100).ContinueWith(_ => synth.NoteOff(note));
        });
    }

    // Triggers a custom action when the frequency condition is met
    public void trigger(Action<float> action)
    {
        _globals.AddFrequencyTrigger(_deviceIndex, _low, _high, _threshold, action);
    }
}

// Control for a specific audio channel
public class ChannelControl
{
    private readonly AudioEngine _engine; // Reference to the audio engine
    private readonly int _index; // Channel index
    public ChannelControl(AudioEngine engine, int index) // Constructor
    {
        _engine = engine; // Initialize engine
        _index = index; // Initialize channel index
    }

    public void gain(float value) => _engine.SetChannelGain(_index, value); // Set gain for the specific channel
    public void gain(double value) => gain((float)value); // Overload for double
}

// Control for all audio channels
public class AllChannelsControl
{
    private readonly AudioEngine _engine; // Reference to the audio engine
    public AllChannelsControl(AudioEngine engine) => _engine = engine; // Constructor

    public AllChannelsControl channel => this; // Allows audio.all.channel.gain

    public void gain(float value) => _engine.SetAllChannelsGain(value); // Set gain for all channels
    public void gain(double value) => gain((float)value); // Overload for double
}
