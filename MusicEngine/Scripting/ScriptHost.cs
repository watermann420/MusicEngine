// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

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

    /// <summary>
    /// Event fired when a refresh is requested (e.g., via MIDI binding).
    /// Subscribe to this to reload the script.
    /// </summary>
    public event Action? OnRefreshRequested;

    /// <summary>
    /// Event fired when a custom action is triggered via MIDI.
    /// The string parameter is the action name.
    /// </summary>
    public event Action<string>? OnActionTriggered;

    // Constructor to initialize the script host with engine and sequencer
    public ScriptHost(AudioEngine engine, Sequencer sequencer)
    {
        _engine = engine; // Initialize the audio engine
        _sequencer = sequencer; // Initialize the sequencer
    }

    /// <summary>
    /// Triggers the refresh event. Call this to request a script reload.
    /// </summary>
    public void TriggerRefresh() => OnRefreshRequested?.Invoke();

    /// <summary>
    /// Triggers a custom action event.
    /// </summary>
    public void TriggerAction(string actionName) => OnActionTriggered?.Invoke(actionName);

    // Executes a C# script asynchronously
    public async Task ExecuteScriptAsync(string code)
    {
        var options = ScriptOptions.Default // Configure script options
            .WithReferences(typeof(AudioEngine).Assembly, typeof(NAudio.Wave.ISampleProvider).Assembly)  // Add necessary assembly references
            .WithImports("System", "MusicEngine.Core", "System.Collections.Generic"); // Add common namespaces

        var globals = new ScriptGlobals { Engine = _engine, Sequencer = _sequencer, Host = this }; // Create globals for the script

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
    public ScriptHost? Host { get; set; } // Reference to the script host for events

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

    /// <summary>Alias for CreateSynth - Creates a synthesizer</summary>
    public SimpleSynth synth() => CreateSynth();
    /// <summary>Alias for CreateSynth - Creates a synthesizer (short form)</summary>
    public SimpleSynth s() => CreateSynth();
    /// <summary>Alias for CreateSynth - Creates a new synthesizer</summary>
    public SimpleSynth newSynth() => CreateSynth();

    // Creates and adds a GeneralMidiInstrument to the engine
    public GeneralMidiInstrument CreateGeneralMidiInstrument(GeneralMidiProgram program, int channel = 0)
    {
        var instrument = new GeneralMidiInstrument(program, channel);
        Engine.AddSampleProvider(instrument);
        return instrument;
    }

    /// <summary>Alias for CreateGeneralMidiInstrument - Creates a GM instrument (short form)</summary>
    public GeneralMidiInstrument gm(GeneralMidiProgram program, int channel = 0) => CreateGeneralMidiInstrument(program, channel);
    /// <summary>Alias for CreateGeneralMidiInstrument - Creates a new GM instrument</summary>
    public GeneralMidiInstrument newGm(GeneralMidiProgram program, int channel = 0) => CreateGeneralMidiInstrument(program, channel);

    // Creates a Pattern with the default Synth
    public Pattern CreatePattern() => CreatePattern(Synth);

    // Creates a Pattern with one or more synths
    // The pattern is NOT automatically added - call pattern.Play() to start it
    public Pattern CreatePattern(ISynth synth, params ISynth[] moreSynths)
    {
        var pattern = new Pattern(synth, moreSynths); // Create a new Pattern with the given synths
        pattern.Sequencer = Sequencer; // Set sequencer reference for Play()/Stop()
        pattern.InstrumentName = synth is SimpleSynth ss ? (ss.Name ?? synth.GetType().Name) : synth.GetType().Name;
        return pattern; // Return the created pattern
    }

    /// <summary>Alias for CreatePattern - Creates a pattern</summary>
    public Pattern pattern() => CreatePattern();
    /// <summary>Alias for CreatePattern - Creates a pattern</summary>
    public Pattern pattern(ISynth synth) => CreatePattern(synth);
    /// <summary>Alias for CreatePattern - Creates a pattern</summary>
    public Pattern pattern(ISynth synth, params ISynth[] more) => CreatePattern(synth, more);
    /// <summary>Alias for CreatePattern - Creates a pattern (short form)</summary>
    public Pattern p() => CreatePattern();
    /// <summary>Alias for CreatePattern - Creates a pattern (short form)</summary>
    public Pattern p(ISynth synth) => CreatePattern(synth);
    /// <summary>Alias for CreatePattern - Creates a pattern (short form)</summary>
    public Pattern p(ISynth synth, params ISynth[] more) => CreatePattern(synth, more);
    /// <summary>Alias for CreatePattern - Creates a new pattern</summary>
    public Pattern newPattern() => CreatePattern();
    /// <summary>Alias for CreatePattern - Creates a new pattern</summary>
    public Pattern newPattern(ISynth synth) => CreatePattern(synth);
    /// <summary>Alias for CreatePattern - Creates a new pattern</summary>
    public Pattern newPattern(ISynth synth, params ISynth[] more) => CreatePattern(synth, more);

    public RandomControl random => new RandomControl();

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

    // Maps a MIDI note to refresh/reload the script
    public void MapRefresh(int deviceIndex, int note)
    {
        Engine.MapTransportNote(deviceIndex, note, val => {
            if (val > 0) Host?.TriggerRefresh();
        });
    }

    /// <summary>Alias for MapRefresh - Binds a note to reload the script</summary>
    public void mapRefresh(int deviceIndex, int note) => MapRefresh(deviceIndex, note);
    /// <summary>Alias for MapRefresh - Binds a note to reload the script</summary>
    public void bindRefresh(int deviceIndex, int note) => MapRefresh(deviceIndex, note);

    // Maps a MIDI CC to refresh/reload the script (triggers when value > 64)
    public void MapRefreshCC(int deviceIndex, int cc)
    {
        Engine.MapTransportControl(deviceIndex, cc, val => {
            if (val > 0.5f) Host?.TriggerRefresh();
        });
    }

    // Maps a MIDI note to trigger a custom action by name
    public void MapAction(int deviceIndex, int note, string actionName)
    {
        Engine.MapTransportNote(deviceIndex, note, val => {
            if (val > 0) Host?.TriggerAction(actionName);
        });
    }

    /// <summary>Alias for MapAction - Binds a note to a custom action</summary>
    public void mapAction(int deviceIndex, int note, string actionName) => MapAction(deviceIndex, note, actionName);
    /// <summary>Alias for MapAction - Binds a note to a custom action</summary>
    public void bindAction(int deviceIndex, int note, string actionName) => MapAction(deviceIndex, note, actionName);

    // Maps a MIDI CC to trigger a custom action by name (triggers when value > 64)
    public void MapActionCC(int deviceIndex, int cc, string actionName)
    {
        Engine.MapTransportControl(deviceIndex, cc, val => {
            if (val > 0.5f) Host?.TriggerAction(actionName);
        });
    }

    // Maps a MIDI note to execute any Action callback
    public void MapNote(int deviceIndex, int note, Action action)
    {
        Engine.MapTransportNote(deviceIndex, note, val => {
            if (val > 0) action();
        });
    }

    /// <summary>Alias for MapNote - Binds a note to a callback</summary>
    public void mapNote(int deviceIndex, int note, Action action) => MapNote(deviceIndex, note, action);
    /// <summary>Alias for MapNote - Binds a note to a callback</summary>
    public void onNote(int deviceIndex, int note, Action action) => MapNote(deviceIndex, note, action);

    // Maps a MIDI note to execute any Action callback with velocity
    public void MapNoteWithVelocity(int deviceIndex, int note, Action<float> action)
    {
        Engine.MapTransportNote(deviceIndex, note, action);
    }

    // Maps a MIDI CC to execute any Action callback
    public void MapCC(int deviceIndex, int cc, Action<float> action)
    {
        Engine.MapTransportControl(deviceIndex, cc, action);
    }

    /// <summary>Alias for MapCC - Binds a CC to a callback</summary>
    public void mapCC(int deviceIndex, int cc, Action<float> action) => MapCC(deviceIndex, cc, action);
    /// <summary>Alias for MapCC - Binds a CC to a callback</summary>
    public void onCC(int deviceIndex, int cc, Action<float> action) => MapCC(deviceIndex, cc, action);

    // Maps a MIDI control to skip beats in the sequencer
    public void MapSkip(int deviceIndex, int cc, double beats)
    {
        Engine.MapTransportControl(deviceIndex, cc, val => {
            if (val > 0.5f) Sequencer.Skip(beats);
        });
    }

    // Map a MIDI control change to start playback
    public void MapStartCc(int deviceIndex, int cc)
    {
        Engine.MapTransportControl(deviceIndex, cc, val => { if (val > 0.5f) Sequencer.Start(); });
    }

    // Map a MIDI control change to stop playback
    public void MapStopCc(int deviceIndex, int cc)
    {
        Engine.MapTransportControl(deviceIndex, cc, val => { if (val > 0.5f) Sequencer.Stop(); });
    }

    // Map a MIDI control change to refresh/reload the script
    public void MapRefreshCc(int deviceIndex, int cc)
    {
        Engine.MapTransportControl(deviceIndex, cc, val => { if (val > 0.5f) Host?.TriggerRefresh(); });
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
    /// <summary>Alias for Start - Starts playback</summary>
    public void play() => Start();
    /// <summary>Alias for Start - Runs the sequencer</summary>
    public void run() => Start();
    /// <summary>Alias for Start - Starts the sequencer</summary>
    public void go() => Start();

    public void Stop() => Sequencer.Stop(); // Stop the sequencer
    /// <summary>Alias for Stop - Pauses playback</summary>
    public void pause() => Stop();
    /// <summary>Alias for Stop - Halts the sequencer</summary>
    public void halt() => Stop();

    public void SetBpm(double bpm) => Sequencer.Bpm = bpm; // Set the BPM of the sequencer
    /// <summary>Alias for SetBpm - Sets the tempo</summary>
    public void bpm(double bpm) => SetBpm(bpm);
    /// <summary>Alias for SetBpm - Sets the tempo</summary>
    public void tempo(double bpm) => SetBpm(bpm);

    public void SetBPM(double bpm) => Sequencer.Bpm = bpm; // Alias for SetBpm
    public double BPM { get => Sequencer.Bpm; set => Sequencer.Bpm = value; } // BPM property

    public void Skip(double beats) => Sequencer.Skip(beats); // Skip a number of beats in the sequencer
    /// <summary>Alias for Skip - Jumps forward by beats</summary>
    public void jump(double beats) => Skip(beats);
    /// <summary>Alias for Skip - Seeks to a position</summary>
    public void seek(double beats) => Skip(beats);

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
    /// <summary>Alias for Print - Logs a message to console</summary>
    public void log(string message) => Print(message);
    /// <summary>Alias for Print - Writes a message to console</summary>
    public void write(string message) => Print(message);

    public AudioControl audio => new AudioControl(this);
    public MidiControl midi => new MidiControl(this);
    public VstControl vst => new VstControl(this);
    public SampleControl samples => new SampleControl(this);

    public VirtualChannelControl virtualChannels => new VirtualChannelControl(this);

    // === VST Plugin Methods ===

    // Load a VST plugin by name (returns IVstPlugin to support both VST2 and VST3)
    public IVstPlugin? LoadVst(string nameOrPath)
    {
        return Engine.LoadVstPlugin(nameOrPath);
    }

    // Load a VST plugin by index (returns IVstPlugin to support both VST2 and VST3)
    public IVstPlugin? LoadVstByIndex(int index)
    {
        return Engine.LoadVstPluginByIndex(index);
    }

    // Get a loaded VST plugin (returns IVstPlugin to support both VST2 and VST3)
    public IVstPlugin? GetVst(string name)
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

    /// <summary>Alias for CreateSampler - Creates a sample instrument</summary>
    public SampleInstrument sampler(string? name = null) => CreateSampler(name);
    /// <summary>Alias for CreateSampler - Creates a sample instrument</summary>
    public SampleInstrument sample(string? name = null) => CreateSampler(name);

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

    /// <summary>Alias for CreateVirtualChannel - Creates a virtual audio channel (short form)</summary>
    public VirtualAudioChannel vchan(string name) => CreateVirtualChannel(name);
    /// <summary>Alias for CreateVirtualChannel - Creates a virtual audio channel</summary>
    public VirtualAudioChannel channel(string name) => CreateVirtualChannel(name);

    /// <summary>
    /// Lists all virtual audio channels.
    /// </summary>
    public void ListVirtualChannels()
    {
        Engine.ListVirtualChannels();
    }
}
