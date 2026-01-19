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
using MusicEngine.Scripting.FluentApi;


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
