//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Fluent API for MIDI control mappings and operations.


using MusicEngine.Core;


namespace MusicEngine.Scripting.FluentApi;


// Fluent API for MIDI control mappings
public class MidiControl
{
    private readonly ScriptGlobals _globals; // Reference to script globals
    public MidiControl(ScriptGlobals globals) => _globals = globals; // Constructor

    public DeviceControl device(int index) => new DeviceControl(_globals, index); // Access device by index

    // Access device by name (MIDI input)
    public DeviceControl device(string name)
    {
        int index = _globals.Engine.GetMidiDeviceIndex(name); // Get device index by name
        return new DeviceControl(_globals, index); // Return device control
    }

    // Access MIDI input by index
    public DeviceControl input(int index) => device(index);

    // Access MIDI input by name
    public DeviceControl input(string name) => device(name);

    // Access MIDI output by index
    public MidiOutputControl output(int index) => new MidiOutputControl(_globals, index);

    // Access MIDI output by name
    public MidiOutputControl output(string name)
    {
        int index = _globals.Engine.GetMidiOutputDeviceIndex(name);
        return new MidiOutputControl(_globals, index);
    }

    // Access playable keys mapping
    public PlayableKeys playablekeys => new PlayableKeys(_globals);
}

// Control for a specific MIDI device
public class DeviceControl
{
    private readonly ScriptGlobals _globals; // Reference to script globals
    private readonly int _deviceIndex; // MIDI device index

    public DeviceControl(ScriptGlobals globals, int deviceIndex) // Constructor
    {
        _globals = globals; // Initialize globals
        _deviceIndex = deviceIndex; // Initialize device index
    }

    public void route(ISynth synth) => _globals.RouteMidi(_deviceIndex, synth); // Route MIDI to synth

    public ControlMapping cc(int ccNumber) => new ControlMapping(_globals, _deviceIndex, ccNumber); // Control change mapping
    public ControlMapping pitchbend() => new ControlMapping(_globals, _deviceIndex, -1); // Pitch bend mapping
}

// Mapping for a specific MIDI control
public class ControlMapping
{
    private readonly ScriptGlobals _globals; // Reference to script globals
    private readonly int _deviceIndex; // MIDI device index
    private readonly int _controlId; // Control identifier (CC number or -1 for pitch bend)

    // Constructor
    public ControlMapping(ScriptGlobals globals, int deviceIndex, int controlId)
    {
        _globals = globals;
        _deviceIndex = deviceIndex;
        _controlId = controlId;
    }

    // Maps the control to a synthesizer parameter
    public void to(ISynth synth, string parameter)
    {
        _globals.Engine.MapMidiControl(_deviceIndex, _controlId, synth, parameter);
    }
}


// Mapping for playable keys range
public class PlayableKeys
{
    private readonly ScriptGlobals _globals; // Reference to script globals
    public PlayableKeys(ScriptGlobals globals) => _globals = globals; // Constructor

    public KeyRange range(int start, int end) => new KeyRange(_globals, start, end); // Create a key range mapping
}


// Represents a range of MIDI keys for mapping
public class KeyRange
{
    private readonly ScriptGlobals _globals; // Reference to script globals
    private readonly int _start; // Start of the key range
    private readonly int _end; // Key range boundaries
    private int _deviceIndex = 0; // Default to the first device

    private bool _reversed = false; // Direction of mapping (true = high.to.low)
    private bool? _startIsHigh = null; // null = not set, true = started with high, false = started with low

    // Constructor to initialize the key range
    public KeyRange(ScriptGlobals globals, int start, int end)
    {
        _globals = globals;
        _start = start;
        _end = end;
    }

    // Specify the MIDI device index
    public KeyRange from(int deviceIndex)
    {
        _deviceIndex = deviceIndex;
        return this;
    }

    // Specify the MIDI device by name
    public KeyRange from(string deviceName)
    {
        _deviceIndex = _globals.Engine.GetMidiDeviceIndex(deviceName);
        return this;
    }

    // Fluent properties to set mapping direction
    // Usage: .low.to.high (normal) or .high.to.low (reversed)
    public KeyRange low
    {
        get
        {
            if (_startIsHigh == null)
            {
                // First call: low.to.*
                _startIsHigh = false;
            }
            else if (_startIsHigh == true)
            {
                // Second call after high: high.to.low = reversed
                _reversed = true;
            }
            // low.to.low would also be _reversed = false (no change needed)
            return this;
        }
    }

    // Fluent properties to set mapping direction
    public KeyRange high
    {
        get
        {
            if (_startIsHigh == null)
            {
                // First call: high.to.*
                _startIsHigh = true;
            }
            else if (_startIsHigh == false)
            {
                // Second call after low: low.to.high = normal (not reversed)
                _reversed = false;
            }
            // high.to.high would also be _reversed = false (no change needed)
            return this;
        }
    }

    // Marks the 'to' part of the mapping (chainable connector)
    public KeyRange to => this;

    // Sets a mapping direction from high to low
    public KeyRange high_to_low()
    {
        _reversed = true;
        return this;
    }

    // Sets a mapping direction from low to high
    public KeyRange low_to_high()
    {
        _reversed = false;
        return this;
    }

    // Maps the key range to a synthesizer
    public void map(ISynth synth)
    {
        _globals.Engine.MapRange(_deviceIndex, _start, _end, synth, _reversed);
    }

    // Allow syntax like range(21, 108)(synth)
    public void Invoke(ISynth synth) => map(synth);
}


// MIDI Output control for sending MIDI to external devices
public class MidiOutputControl
{
    private readonly ScriptGlobals _globals;
    private readonly int _outputIndex;

    public MidiOutputControl(ScriptGlobals globals, int outputIndex)
    {
        _globals = globals;
        _outputIndex = outputIndex;
    }

    // Send a note on
    public MidiOutputControl noteOn(int note, int velocity = 100, int channel = 0)
    {
        _globals.Engine.SendNoteOn(_outputIndex, channel, note, velocity);
        return this;
    }

    // Send a note off
    public MidiOutputControl noteOff(int note, int channel = 0)
    {
        _globals.Engine.SendNoteOff(_outputIndex, channel, note);
        return this;
    }

    // Send control change
    public MidiOutputControl cc(int controller, int value, int channel = 0)
    {
        _globals.Engine.SendControlChange(_outputIndex, channel, controller, value);
        return this;
    }
}
