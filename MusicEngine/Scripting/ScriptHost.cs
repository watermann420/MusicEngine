//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: A class to host and execute C# scripts for controlling the MusicEngine audio engine and sequencer.


using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using MusicEngine.Core;


namespace MusicEngine.Scripting;


public class ScriptHost
{
    private readonly AudioEngine _engine; // The audio engine instance
    private readonly Sequencer _sequencer; // The sequencer instance
    
    // Constructor to initialize the script host with engine and sequencer
    public ScriptHost(AudioEngine engine, Sequencer sequencer)
    {
        _engine = engine; // Initialize the audio engine
        _sequencer = sequencer; // Initialize the sequencer
    }
    
    // Executes a C# script asynchronously
    public async Task ExecuteScriptAsync(string code)
    {
        var options = ScriptOptions.Default // Configure script options
            .WithReferences(typeof(AudioEngine).Assembly, typeof(NAudio.Wave.ISampleProvider).Assembly)  // Add necessary assembly references
            .WithImports("System", "MusicEngine.Core", "System.Collections.Generic"); // Add common namespaces

        var globals = new ScriptGlobals { Engine = _engine, Sequencer = _sequencer }; // Create globals for the script

        try
        {
            await CSharpScript.RunAsync(code, options, globals); // Execute the script
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Script Error: {ex.Message}"); // Log any script errors
        }
    }
    
    // Clears the current state of the engine and sequencer
    public void ClearState()
    {
        _sequencer.ClearPatterns(); // Stop patterns first so they call AllNotesOff if enabled
        _engine.ClearMappings(); // Clear MIDI and frequency mappings
        _engine.ClearMixer(); // Clear the audio mixer
    }
}
    
// Class to hold global objects and helper methods for scripts
public class ScriptGlobals
{
    public AudioEngine Engine { get; set; } = null!; // The audio engine instance
    public Sequencer Sequencer { get; set; } = null!; // The sequencer instance

    private SimpleSynth? _synth; // Default synth instance

    // Default synth - created on first access
    public SimpleSynth Synth => _synth ??= CreateSynth();

    // Lowercase aliases for convenience
    public AudioEngine engine => Engine;
    public Sequencer sequencer => Sequencer;

    // Creates and adds a SimpleSynth to the engine
    public SimpleSynth CreateSynth()
    {
        var synth = new SimpleSynth(); // Create a new SimpleSynth
        Engine.AddSampleProvider(synth); // Add it to the audio engine
        return synth; // Return the created synth
    }

    // Creates a Pattern with the default Synth
    public Pattern CreatePattern() => CreatePattern(Synth);

    // Creates a Pattern with reference to the sequencer
    // The pattern is NOT automatically added - call pattern.Play() to start it
    public Pattern CreatePattern(ISynth synth)
    {
        var pattern = new Pattern(synth); // Create a new Pattern with the given synth
        pattern.Sequencer = Sequencer; // Set sequencer reference for Play()/Stop()
        pattern.InstrumentName = synth is SimpleSynth ss ? (ss.Name ?? synth.GetType().Name) : synth.GetType().Name;
        return pattern; // Return the created pattern
    }
    
    // Routes MIDI input from a device to a synthesizer
    public void RouteMidi(int deviceIndex, ISynth synth)
    {
        Engine.RouteMidiInput(deviceIndex, synth); 
    }
    
    // Maps a MIDI control change to a synthesizer parameter
    public void MapControl(int deviceIndex, int cc, ISynth synth, string param)
    {
        Engine.MapMidiControl(deviceIndex, cc, synth, param); 
    }
    
    // Maps pitch bend to a synthesizer parameter
    public void MapPitchBend(int deviceIndex, ISynth synth, string param)
    {
        // We use -1 as an internal identifier for Pitch Bend
        Engine.MapMidiControl(deviceIndex, -1, synth, param); 
    }
    
    // Maps a MIDI control to BPM adjustment
    public void MapBpm(int deviceIndex, int cc)
    {
        Engine.MapTransportControl(deviceIndex, cc, val => {
            Sequencer.Bpm = 60 + (val * 140); // Map 0-1 to 60-200 BPM
        });
    }
    
    // Maps a MIDI note to start the sequencer
    public void MapStart(int deviceIndex, int note)
    {
        Engine.MapTransportNote(deviceIndex, note, val => {
            if (val > 0) Sequencer.Start();
        });
    }
    
    // Maps a MIDI note to stop the sequencer
    public void MapStop(int deviceIndex, int note)
    {
        Engine.MapTransportNote(deviceIndex, note, val => {
            if (val > 0) Sequencer.Stop();
        });
    }
    
    // Maps a MIDI control to skip beats in the sequencer
    public void MapSkip(int deviceIndex, int cc, double beats)
    {
        Engine.MapTransportControl(deviceIndex, cc, val => {
            if (val > 0.5f) Sequencer.Skip(beats);
        });
    }
    
    // Maps a MIDI control to scratching behavior
    public void MapScratch(int deviceIndex, int cc, double scale = 16.0)
    {
        Engine.MapTransportControl(deviceIndex, cc, val => {
            Sequencer.IsScratching = true;
            Sequencer.CurrentBeat = val * scale;
        });
        // We might want a way to release scratch mode
    }

    public void SetScratching(bool scratching) => Sequencer.IsScratching = scratching; // Enable or disable scratching mode

    public void Start() => Sequencer.Start(); // Start the sequencer
    public void Stop() => Sequencer.Stop(); // Stop the sequencer
    public void SetBpm(double bpm) => Sequencer.Bpm = bpm; // Set the BPM of the sequencer
    public void SetBPM(double bpm) => Sequencer.Bpm = bpm; // Alias for SetBpm
    public double BPM { get => Sequencer.Bpm; set => Sequencer.Bpm = value; } // BPM property
    public void Skip(double beats) => Sequencer.Skip(beats); // Skip a number of beats in the sequencer 

    public void StartPattern(Pattern p) => p.Enabled = true; // Start a pattern
    public void StopPattern(Pattern p) => p.Enabled = false; // Stop a pattern 

    public PatternControl patterns => new PatternControl(this); // Accessor for pattern controls

    public float Random(float min, float max) => (float)(new Random().NextDouble() * (max - min) + min); // Generate a random float
    public int RandomInt(int min, int max) => new Random().Next(min, max); // Generate a random integer
    
    // Adds a frequency trigger mapping
    public void AddFrequencyTrigger(int deviceIndex, float low, float high, float threshold, Action<float> action) 
    {
        Engine.AddFrequencyMapping(new FrequencyMidiMapping // Create and add a new frequency mapping
        {
            DeviceIndex = deviceIndex, // MIDI Device Index
            LowFreq = low, // Low frequency in Hz
            HighFreq = high, // High frequency in Hz
            Threshold = threshold, // Magnitude threshold for triggering
            OnTrigger = action // Action to invoke on trigger with magnitude
        });
    }
    
    // Prints a message to the console
    public void Print(string message) => Console.WriteLine(message);

    public AudioControl audio => new AudioControl(this);
    public MidiControl midi => new MidiControl(this);
    public VstControl vst => new VstControl(this);
    public SampleControl samples => new SampleControl(this);

    public VirtualChannelControl virtualChannels => new VirtualChannelControl(this);

    // === VST Plugin Methods ===

    // Load a VST plugin by name
    public VstPlugin? LoadVst(string nameOrPath)
    {
        return Engine.LoadVstPlugin(nameOrPath);
    }

    // Load a VST plugin by index
    public VstPlugin? LoadVstByIndex(int index)
    {
        return Engine.LoadVstPluginByIndex(index);
    }

    // Get a loaded VST plugin
    public VstPlugin? GetVst(string name)
    {
        return Engine.GetVstPlugin(name);
    }

    // Route MIDI to a VST plugin
    public void RouteToVst(int deviceIndex, VstPlugin plugin)
    {
        Engine.RouteMidiToVst(deviceIndex, plugin);
    }

    // Print all discovered VST plugins
    public void ListVstPlugins()
    {
        Engine.PrintVstPlugins();
    }

    // Print loaded VST plugins
    public void ListLoadedVstPlugins()
    {
        Engine.PrintLoadedVstPlugins();
    }

    // === Sample Instrument Methods ===

    /// <summary>
    /// Creates a new sample instrument and adds it to the audio engine.
    /// </summary>
    public SampleInstrument CreateSampler(string? name = null)
    {
        var sampler = new SampleInstrument();
        if (name != null) sampler.Name = name;
        Engine.AddSampleProvider(sampler);
        return sampler;
    }

    /// <summary>
    /// Creates a sample instrument and loads a single sample.
    /// The sample is mapped to all notes with pitch shifting from the root note.
    /// </summary>
    public SampleInstrument CreateSamplerFromFile(string filePath, int rootNote = 60)
    {
        var sampler = CreateSampler();
        var sample = sampler.LoadSample(filePath, rootNote);
        return sampler;
    }

    /// <summary>
    /// Creates a sample instrument from a directory of samples.
    /// Each sample is mapped to a note based on filename (e.g., "kick.wav" -> use LoadAndMap).
    /// </summary>
    public SampleInstrument CreateSamplerFromDirectory(string directoryPath)
    {
        var sampler = CreateSampler();
        sampler.SetSampleDirectory(directoryPath);
        return sampler;
    }

    /// <summary>
    /// Loads a sample into an existing sampler and maps it to a specific note.
    /// Great for drum pads.
    /// </summary>
    public Sample? LoadSampleToNote(SampleInstrument sampler, string filePath, int note)
    {
        var sample = sampler.LoadSample(filePath, note);
        if (sample != null)
        {
            sampler.MapSampleToNote(sample, note);
        }
        return sample;
    }

    // === Virtual Audio Channel Methods ===

    /// <summary>
    /// Creates a virtual audio channel for routing audio to other applications.
    /// Other apps can connect via the named pipe to receive audio.
    /// </summary>
    public VirtualAudioChannel CreateVirtualChannel(string name)
    {
        return Engine.CreateVirtualChannel(name);
    }

    /// <summary>
    /// Lists all virtual audio channels.
    /// </summary>
    public void ListVirtualChannels()
    {
        Engine.ListVirtualChannels();
    }
}

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

// Control for pattern operations
public class PatternControl
{ 
    private readonly ScriptGlobals _globals; // Reference to script globals
    public PatternControl(ScriptGlobals globals) => _globals = globals; // Constructor

    public void start(Pattern p) => p.Enabled = true; // Start a pattern
    public void stop(Pattern p) => p.Enabled = false; // Stop a pattern
    public void toggle(Pattern p) => p.Enabled = !p.Enabled; // Toggle a pattern's enabled state
}

// Control for audio operations
public class AudioControl
{
    private readonly ScriptGlobals _globals; // Reference to script globals
    public AudioControl(ScriptGlobals globals) => _globals = globals; // Constructor

    public ChannelControl channel(int index) => new ChannelControl(_globals.Engine, index); // Access specific channel control
    public AllChannelsControl all => new AllChannelsControl(_globals.Engine); // Access all channels control

    public InputControl input(int index) => new InputControl(_globals, index); // Access audio input control
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


// === VST Fluent API ===

// Main VST control access
public class VstControl
{
    private readonly ScriptGlobals _globals;
    public VstControl(ScriptGlobals globals) => _globals = globals;

    // Load a VST plugin by name or path
    public VstPluginControl? load(string nameOrPath)
    {
        var plugin = _globals.LoadVst(nameOrPath);
        return plugin != null ? new VstPluginControl(_globals, plugin) : null;
    }

    // Load a VST plugin by index
    public VstPluginControl? load(int index)
    {
        var plugin = _globals.LoadVstByIndex(index);
        return plugin != null ? new VstPluginControl(_globals, plugin) : null;
    }

    // Get a loaded VST plugin by name
    public VstPluginControl? get(string name)
    {
        var plugin = _globals.GetVst(name);
        return plugin != null ? new VstPluginControl(_globals, plugin) : null;
    }

    // List all discovered VST plugins
    public void list() => _globals.ListVstPlugins();

    // List all loaded VST plugins
    public void loaded() => _globals.ListLoadedVstPlugins();

    // Access VST plugin by name for fluent chaining
    public VstPluginControl? plugin(string name) => get(name);
}


// Control for a specific VST plugin
public class VstPluginControl
{
    private readonly ScriptGlobals _globals;
    private readonly VstPlugin _plugin;

    public VstPluginControl(ScriptGlobals globals, VstPlugin plugin)
    {
        _globals = globals;
        _plugin = plugin;
    }

    // Get the underlying plugin
    public VstPlugin Plugin => _plugin;

    // Route MIDI input to this plugin
    public VstPluginControl from(int deviceIndex)
    {
        _globals.RouteToVst(deviceIndex, _plugin);
        return this;
    }

    // Route MIDI input by device name
    public VstPluginControl from(string deviceName)
    {
        int index = _globals.Engine.GetMidiDeviceIndex(deviceName);
        if (index >= 0) _globals.RouteToVst(index, _plugin);
        return this;
    }

    // Set a parameter by name
    public VstPluginControl param(string name, float value)
    {
        _plugin.SetParameter(name, value);
        return this;
    }

    // Set a parameter by index
    public VstPluginControl param(int index, float value)
    {
        _plugin.SetParameterByIndex(index, value);
        return this;
    }

    // Set volume/gain
    public VstPluginControl volume(float value)
    {
        _plugin.SetParameter("volume", value);
        return this;
    }

    // Send a note on
    public VstPluginControl noteOn(int note, int velocity = 100)
    {
        _plugin.NoteOn(note, velocity);
        return this;
    }

    // Send a note off
    public VstPluginControl noteOff(int note)
    {
        _plugin.NoteOff(note);
        return this;
    }

    // Send all notes off
    public VstPluginControl allNotesOff()
    {
        _plugin.AllNotesOff();
        return this;
    }

    // Send control change
    public VstPluginControl cc(int controller, int value, int channel = 0)
    {
        _plugin.SendControlChange(channel, controller, value);
        return this;
    }

    // Send program change
    public VstPluginControl program(int programNumber, int channel = 0)
    {
        _plugin.SendProgramChange(channel, programNumber);
        return this;
    }

    // Send pitch bend
    public VstPluginControl pitchBend(int value, int channel = 0)
    {
        _plugin.SendPitchBend(channel, value);
        return this;
    }

    // Implicit conversion to VstPlugin for direct use
    public static implicit operator VstPlugin(VstPluginControl control) => control._plugin;
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

// === Sample Fluent API ===

/// <summary>
/// Fluent API for creating and configuring sample instruments.
/// </summary>
public class SampleControl
{
    private readonly ScriptGlobals _globals;
    public SampleControl(ScriptGlobals globals) => _globals = globals;

    /// <summary>
    /// Creates a new sampler.
    /// </summary>
    public SamplerBuilder create(string? name = null)
    {
        var sampler = _globals.CreateSampler(name);
        return new SamplerBuilder(_globals, sampler);
    }

    /// <summary>
    /// Loads a single sample as an instrument.
    /// </summary>
    public SamplerBuilder load(string filePath, int rootNote = 60)
    {
        var sampler = _globals.CreateSamplerFromFile(filePath, rootNote);
        return new SamplerBuilder(_globals, sampler);
    }

    /// <summary>
    /// Creates a sampler from a directory of samples.
    /// </summary>
    public SamplerBuilder fromDirectory(string path)
    {
        var sampler = _globals.CreateSamplerFromDirectory(path);
        return new SamplerBuilder(_globals, sampler);
    }
}

/// <summary>
/// Builder for configuring a sample instrument.
/// </summary>
public class SamplerBuilder
{
    private readonly ScriptGlobals _globals;
    private readonly SampleInstrument _sampler;

    public SamplerBuilder(ScriptGlobals globals, SampleInstrument sampler)
    {
        _globals = globals;
        _sampler = sampler;
    }

    /// <summary>
    /// Gets the underlying sampler.
    /// </summary>
    public SampleInstrument Sampler => _sampler;

    /// <summary>
    /// Loads a sample and maps it to a specific note.
    /// </summary>
    public SamplerBuilder map(string filePath, int note)
    {
        _globals.LoadSampleToNote(_sampler, filePath, note);
        return this;
    }

    /// <summary>
    /// Sets the sample directory for relative paths.
    /// </summary>
    public SamplerBuilder directory(string path)
    {
        _sampler.SetSampleDirectory(path);
        return this;
    }

    /// <summary>
    /// Sets the master volume.
    /// </summary>
    public SamplerBuilder volume(float vol)
    {
        _sampler.Volume = vol;
        return this;
    }

    /// <summary>
    /// Sets the name.
    /// </summary>
    public SamplerBuilder name(string n)
    {
        _sampler.Name = n;
        return this;
    }

    /// <summary>
    /// Creates a pattern with this sampler.
    /// </summary>
    public Pattern pattern()
    {
        return _globals.CreatePattern(_sampler);
    }

    /// <summary>
    /// Implicit conversion to SampleInstrument.
    /// </summary>
    public static implicit operator SampleInstrument(SamplerBuilder builder) => builder._sampler;
}

// === Virtual Channel Fluent API ===

/// <summary>
/// Fluent API for virtual audio channels.
/// </summary>
public class VirtualChannelControl
{
    private readonly ScriptGlobals _globals;
    public VirtualChannelControl(ScriptGlobals globals) => _globals = globals;

    /// <summary>
    /// Creates a new virtual channel.
    /// </summary>
    public VirtualChannelBuilder create(string name)
    {
        var channel = _globals.CreateVirtualChannel(name);
        return new VirtualChannelBuilder(channel);
    }

    /// <summary>
    /// Lists all virtual channels.
    /// </summary>
    public void list() => _globals.ListVirtualChannels();
}

/// <summary>
/// Builder for configuring virtual channels.
/// </summary>
public class VirtualChannelBuilder
{
    private readonly VirtualAudioChannel _channel;

    public VirtualChannelBuilder(VirtualAudioChannel channel)
    {
        _channel = channel;
    }

    /// <summary>
    /// Gets the underlying channel.
    /// </summary>
    public VirtualAudioChannel Channel => _channel;

    /// <summary>
    /// Sets the volume.
    /// </summary>
    public VirtualChannelBuilder volume(float vol)
    {
        _channel.Volume = vol;
        return this;
    }

    /// <summary>
    /// Starts the channel.
    /// </summary>
    public VirtualChannelBuilder start()
    {
        _channel.Start();
        return this;
    }

    /// <summary>
    /// Stops the channel.
    /// </summary>
    public VirtualChannelBuilder stop()
    {
        _channel.Stop();
        return this;
    }

    /// <summary>
    /// Gets the pipe name for connecting from other applications.
    /// </summary>
    public string pipeName => _channel.PipeName;

    /// <summary>
    /// Implicit conversion to VirtualAudioChannel.
    /// </summary>
    public static implicit operator VirtualAudioChannel(VirtualChannelBuilder builder) => builder._channel;
}
