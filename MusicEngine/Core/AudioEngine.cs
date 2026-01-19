//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Core Audio Engine for handling audio and MIDI routing, mixing, and processing.


using System;
using System.Collections.Generic;
using NAudio.Wave;
using NAudio.Midi;
using NAudio.Wave.SampleProviders;


namespace MusicEngine.Core;


public class AudioEngine : IDisposable
{
    private readonly List<IWaveIn> _inputs = new(); // Audio Inputs
    private readonly List<IWavePlayer> _outputs = new(); // Audio Outputs
    private readonly List<MidiIn> _midiInputs = new(); // MIDI Inputs
    private readonly Dictionary<int, string> _midiInputNames = new(); // MIDI Input Names
    private readonly List<MidiOut> _midiOutputs = new();   // MIDI Outputs
    private readonly Dictionary<int, string> _midiOutputNames = new(); // MIDI Output Names
    private readonly Dictionary<int, ISynth> _midiInputRouting = new(); // MIDI Input to Synth Routing
    private readonly List<(int deviceIndex, int control, ISynth synth, string parameter)> _midiMappings = new(); // MIDI Control Mappings
    private readonly List<(int deviceIndex, string command, Action<float> action)> _transportMappings = new(); // Transport Control Mappings
    private readonly List<(int deviceIndex, int startNote, int endNote, ISynth synth, bool reversed)> _rangeMappings = new(); // Note Range Mappings
    private readonly List<FrequencyMidiMapping> _frequencyMappings = new(); // Frequency Analysis Mappings
    private readonly Dictionary<int, FrequencyAnalyzer> _inputAnalyzers = new(); // Input Analyzers
    private readonly Dictionary<int, Action<float[]>> _fftHandlers = new(); // FFT event handlers for cleanup
    private readonly Dictionary<int, EventHandler<WaveInEventArgs>> _dataHandlers = new(); // DataAvailable event handlers for cleanup
    private readonly Dictionary<int, EventHandler<MidiInMessageEventArgs>> _midiHandlers = new(); // MIDI MessageReceived event handlers for cleanup
    private readonly Dictionary<int, IWaveIn> _inputDevices = new(); // Input devices for handler cleanup
    private readonly MixingSampleProvider _mixer; // Main Mixer
    private readonly VolumeSampleProvider _masterVolume; // Master Volume Control
    private readonly WaveFormat _waveFormat; // Audio Format
    private readonly List<VolumeSampleProvider> _channels = new(); // Individual Channel Volume Controls
    private float _masterGain = 1.0f; // Master Gain

    // VST Host
    private readonly VstHost _vstHost = new(); // VST Plugin Host
    private readonly Dictionary<string, VstPlugin> _vstRouting = new(); // VST Plugin Routing

    // Virtual Audio Channels
    private readonly VirtualChannelManager _virtualChannels = new();

    // Constructor
    public AudioEngine(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate; // Use provided or default sample rate
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels); // Create a wave format
        _mixer = new MixingSampleProvider(_waveFormat); // Initialize mixer
        _mixer.ReadFully = true; // Ensure continuous output
        _masterVolume = new VolumeSampleProvider(_mixer); // Master volume control
    }
    
    // MIDI Routing and Mapping Methods
    public void RouteMidiInput(int deviceIndex, ISynth synth)
    {
        lock (_midiInputRouting)
        {
            _midiInputRouting[deviceIndex] = synth; // Route MIDI input to synth
        }
    }
    
    // Map a MIDI control change to a synth parameter
    public void MapMidiControl(int deviceIndex, int controlNumber, ISynth synth, string parameter) // Map MIDI control
    {
        lock (_midiMappings)
        {
            _midiMappings.Add((deviceIndex, controlNumber, synth, parameter)); // Add control mapping
        }
    }
    
    // Map a transport control (like play, stop) to an action
    public void MapTransportControl(int deviceIndex, int controlNumber, Action<float> action) // Map transport control
    {
        lock (_transportMappings)
        {
            _transportMappings.Add((deviceIndex, controlNumber.ToString(), action)); // Add control mapping
        }
    }
    
    // Map a transport note (like start/stop) to an action
    public void MapTransportNote(int deviceIndex, int noteNumber, Action<float> action) // Map transport note
    {
        lock (_transportMappings)
        {
            _transportMappings.Add((deviceIndex, "note_" + noteNumber, action)); // Add note mapping
        }
    }
    
    // Map a range of MIDI notes to a synth
    public void MapRange(int deviceIndex, int startNote, int endNote, ISynth synth, bool reversed = false) // Map range of notes
    {
        lock (_rangeMappings)
        {
            _rangeMappings.Add((deviceIndex, startNote, endNote, synth, reversed)); // Add range mapping
        }
    }
    
    // Clear all MIDI mappings
    public void ClearMappings()
    {
        lock (_midiInputRouting) 
        {
            _midiInputRouting.Clear(); // Clear routing
        }
        lock (_midiMappings)
        {
            _midiMappings.Clear(); // Clear control mappings
        }
        lock (_transportMappings)
        {
            _transportMappings.Clear(); // Clear transport mappings
        }
        lock (_rangeMappings)
        {
            _rangeMappings.Clear(); // Clear range mappings
        }
        lock (_frequencyMappings)
        {
            _frequencyMappings.Clear(); // Clear frequency mappings
        }
    }
    
    // Add a frequency to MIDI mapping
    public void AddFrequencyMapping(FrequencyMidiMapping mapping)
    {
        lock (_frequencyMappings)
        {
            _frequencyMappings.Add(mapping);
        }
        StartInputCapture(mapping.DeviceIndex);
    }
    
    // Start capturing audio input for frequency analysis
    private void StartInputCapture(int deviceIndex) 
    {
        lock (_inputAnalyzers)
        {
            if (_inputAnalyzers.ContainsKey(deviceIndex)) return; // Already capturing

            var analyzer = new FrequencyAnalyzer(Settings.FftSize, _waveFormat.SampleRate); // Create off a frequency analyzer

            // Store the FFT handler for later cleanup
            Action<float[]> fftHandler = magnitudes => { // On FFT calculated
                lock (_frequencyMappings)
                {
                    foreach (var mapping in _frequencyMappings) // Iterate frequency mappings
                    {
                        if (mapping.DeviceIndex == deviceIndex) // Match device index
                        {
                            float magnitude = analyzer.GetMagnitudeForRange(magnitudes, mapping.LowFreq, mapping.HighFreq); // Get magnitude for the specified frequency range
                            mapping.ProcessMagnitude(magnitude); // Process magnitude for the mapping
                        }
                    }
                }
            };
            analyzer.FftCalculated += fftHandler;
            _fftHandlers[deviceIndex] = fftHandler;

            try
            {
                var waveIn = new WaveInEvent // Create audio input
                {
                    DeviceNumber = deviceIndex, // Set device index
                    WaveFormat = new WaveFormat(_waveFormat.SampleRate, 16, 1) // Mono for analysis
                };

                // Store the DataAvailable handler for later cleanup
                EventHandler<WaveInEventArgs> dataHandler = (s, e) => { // On audio data available
                    float[] samples = new float[e.BytesRecorded / 2]; // 16-bit audio
                    for (int i = 0; i < samples.Length; i++) // Convert byte data to float samples
                    {
                        samples[i] = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f; // Convert to float
                    }
                    analyzer.AddSamples(samples, samples.Length); // Feed samples to analyzer
                };
                waveIn.DataAvailable += dataHandler;
                _dataHandlers[deviceIndex] = dataHandler;
                _inputDevices[deviceIndex] = waveIn;

                waveIn.StartRecording(); // Start recording
                _inputs.Add(waveIn); // Store input
                _inputAnalyzers[deviceIndex] = analyzer; // Store analyzer
                Console.WriteLine($"Started capturing from Input Device [{deviceIndex}]"); // Log capture start
            }
            catch (Exception ex) // Handle exceptions
            {
                Console.WriteLine($"Failed to start input capture for device {deviceIndex}: {ex.Message}");
            }
        }
    }
    
    // Clear all channels from the mixer
    public void ClearMixer()
    {
        lock (_channels)
        {
            _mixer.RemoveAllMixerInputs(); // Clear mixer inputs
            _channels.Clear(); // Clear channel list
        }
    }
    
    // Initialize Audio Engine
    public void Initialize()
    {
        // Setup default output
        var output = new WaveOutEvent(); // Create output device
        output.Init(_masterVolume); // Initialize with the master volume
        output.Play(); // Start playback
        _outputs.Add(output); // Store output

        // Enumerate Audio Outputs
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var capabilities = WaveOut.GetCapabilities(i); // Get device capabilities
            Console.WriteLine($"Found Output Device [{i}]: {capabilities.ProductName}"); // Log found device
        }

        // Enumerate Audio Inputs
        for (int i = 0; i < WaveIn.DeviceCount; i++)
        {
            var capabilities = WaveIn.GetCapabilities(i); // Get device capabilities
            Console.WriteLine($"Found Input Device [{i}]: {capabilities.ProductName}"); // Log found device
        }
        
        // Enumerate MIDI Inputs
        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            var name = MidiIn.DeviceInfo(i).ProductName; // Get device name
            Console.WriteLine($"Found MIDI Input [{i}]: {name}"); // Log found device
            _midiInputNames[i] = name; // Store device name
            try
            {
                var midiIn = new MidiIn(i); // Create MIDI input
                int deviceIndex = i; // Capture index for closure

                // Store the MIDI MessageReceived handler for later cleanup
                EventHandler<MidiInMessageEventArgs> midiHandler = (s, e) => {
                    if (e.MidiEvent is ControlChangeEvent ccEvent) // Handle Control Change events
                    {
                        lock (_midiMappings) // Lock for thread safety
                        {
                            foreach (var mapping in _midiMappings) // Iterate mappings
                            {
                                if (mapping.deviceIndex == deviceIndex && mapping.control == (int)ccEvent.Controller) // Match device and control
                                {
                                    mapping.synth.SetParameter(mapping.parameter, ccEvent.ControllerValue / 127f); // Normalize and set parameter
                                }
                            }
                        }

                        lock (_transportMappings) // Lock for thread safety
                        {
                            foreach (var mapping in _transportMappings) // Iterate transport mappings
                            {
                                if (mapping.deviceIndex == deviceIndex && mapping.command == ((int)ccEvent.Controller).ToString()) // Match device and command
                                {
                                    mapping.action(ccEvent.ControllerValue / 127f); // Normalize and invoke action
                                }
                            }
                        }
                    }

                    if (e.MidiEvent is PitchWheelChangeEvent pitchEvent) // Handle Pitch Bend events
                    {
                        lock (_midiMappings) // Lock for thread safety
                        {
                            foreach (var mapping in _midiMappings) // Iterate mappings
                            {
                                if (mapping.deviceIndex == deviceIndex && mapping.control == -1) // Use -1 to denote pitch bend
                                {
                                    float normalizedValue = pitchEvent.Pitch / 16383f; // Normalize pitch bend value
                                    mapping.synth.SetParameter(mapping.parameter, normalizedValue); // Set parameter
                                }
                            }
                        }

                        lock (_transportMappings) // Lock for thread safety
                        {
                            foreach (var mapping in _transportMappings) // Iterate transport mappings
                            {
                                if (mapping.deviceIndex == deviceIndex && mapping.command == "pitch") // Match pitch command
                                {
                                    mapping.action(pitchEvent.Pitch / 16383f); // Normalize and invoke action
                                }
                            }
                        }
                    }

                    bool noteHandledByRange = false; // Flag to check if a note was handled by range mapping
                    if (e.MidiEvent is NAudio.Midi.NoteEvent noteEvent) // Handle Note events
                    {
                        lock (_rangeMappings) // Lock for thread safety
                        {
                            foreach (var mapping in _rangeMappings) // Iterate range mappings
                            {
                                if (mapping.deviceIndex == deviceIndex && noteEvent.NoteNumber >= Math.Min(mapping.startNote, mapping.endNote) && noteEvent.NoteNumber <= Math.Max(mapping.startNote, mapping.endNote)) // Check if the note is within range
                                {
                                    int effectiveNote = noteEvent.NoteNumber; // Calculate effective note
                                    if (mapping.reversed) // If reversed mapping
                                    {
                                        effectiveNote = mapping.startNote + mapping.endNote - noteEvent.NoteNumber; // Reverse the note
                                    }

                                    ProcessEffectiveNoteEvent(noteEvent, mapping.synth, effectiveNote); // Process the note event
                                    noteHandledByRange = true; // Mark as handled
                                }
                            }
                        }
                    }

                    if (noteHandledByRange) return; // If the note was handled by range mapping, skip further processing

                    lock (_midiInputRouting) // Lock for thread safety
                    {
                        if (!_midiInputRouting.TryGetValue(deviceIndex, out var synth)) // Get routed synth
                        {
                            // If no synth routed, check for transport note mappings
                            if (e.MidiEvent is NAudio.Midi.NoteEvent note)
                            {
                                lock (_transportMappings) // Lock for thread safety
                                {
                                    foreach (var mapping in _transportMappings) // Iterate transport mappings
                                    {
                                        if (mapping.deviceIndex == deviceIndex && mapping.command == "note_" + note.NoteNumber) // Match device and note command
                                        {
                                            mapping.action(note.CommandCode == MidiCommandCode.NoteOn ? 1.0f : 0.0f); // Invoke action based on note on/off
                                        }
                                    }
                                }
                            }
                            return;
                        }

                        if (e.MidiEvent is NAudio.Midi.NoteEvent ne) // Handle Note events
                        {
                            ProcessNoteEvent(ne, synth); // Process the note event
                        }
                    }
                };
                midiIn.MessageReceived += midiHandler;
                _midiHandlers[deviceIndex] = midiHandler;

                midiIn.Start(); // Start MIDI input
                _midiInputs.Add(midiIn); // Store MIDI input
            }
            catch (Exception ex)  // Handle exceptions
            { 
                Console.WriteLine($"Failed to open MIDI Input {i}: {ex.Message}");
            }
        }

        // Enumerate MIDI Outputs
        for (int i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            var name = MidiOut.DeviceInfo(i).ProductName; // Get device name
            Console.WriteLine($"Found MIDI Output [{i}]: {name}"); // Log found device
            _midiOutputNames[i] = name; // Store device name
            try
            {
                var midiOut = new MidiOut(i); // Create MIDI output
                _midiOutputs.Add(midiOut); // Store MIDI output
            }
            catch (Exception ex) // Handle exceptions
            {
                Console.WriteLine($"Failed to open MIDI Output {i}: {ex.Message}");
            }
        }

        // Scan for VST Plugins
        Console.WriteLine("\nScanning for VST Plugins...");
        var vstPlugins = ScanVstPlugins();
        if (vstPlugins.Count > 0)
        {
            PrintVstPlugins();
        }
        else
        {
            Console.WriteLine("No VST plugins found in configured paths.");
        }
    }
    
    // Process Note Events
    private void ProcessNoteEvent(NAudio.Midi.NoteEvent noteEvent, ISynth synth)
    {
        ProcessEffectiveNoteEvent(noteEvent, synth, noteEvent.NoteNumber); // Process with the original note number
    }
    
    // Process Note Events with effective note number
    private void ProcessEffectiveNoteEvent(NAudio.Midi.NoteEvent noteEvent, ISynth synth, int effectiveNote)
    {
        // In NAudio, NoteOff is often represented as NoteOn with Velocity 0
        // or as a distinct NoteOff event.
        bool isNoteOn = noteEvent.CommandCode == MidiCommandCode.NoteOn; // Note On event
        bool isNoteOff = noteEvent.CommandCode == MidiCommandCode.NoteOff; // Note Off event
        int velocity = 0; // Default velocity
        if (noteEvent is NoteOnEvent on) velocity = on.Velocity; // Get velocity for Note On

        if (isNoteOn && velocity > 0) // Note On with non-zero velocity
        {
            synth.NoteOn(effectiveNote, velocity);
        }
        else if (isNoteOff || (isNoteOn && velocity == 0)) // Note Off or Note On with zero velocity
        {
            synth.NoteOff(effectiveNote);
        }
    }
    
    // Get MIDI Device Index by Name
    public int GetMidiDeviceIndex(string name)
    {
        foreach (var kvp in _midiInputNames)
        {
            if (kvp.Value.Contains(name, StringComparison.OrdinalIgnoreCase)) // Case-insensitive match
                return kvp.Key;
        }
        return -1;
    }

    // Get MIDI Output Device Index by Name
    public int GetMidiOutputDeviceIndex(string name)
    {
        foreach (var kvp in _midiOutputNames)
        {
            if (kvp.Value.Contains(name, StringComparison.OrdinalIgnoreCase))
                return kvp.Key;
        }
        return -1;
    }

    // Get MIDI Output by Index
    public MidiOut? GetMidiOutput(int index)
    {
        if (index >= 0 && index < _midiOutputs.Count)
        {
            return _midiOutputs[index];
        }
        return null;
    }

    // Send MIDI message to output
    public void SendMidiMessage(int outputIndex, int status, int data1, int data2)
    {
        var midiOut = GetMidiOutput(outputIndex);
        if (midiOut != null)
        {
            int message = status | (data1 << 8) | (data2 << 16);
            midiOut.Send(message);
        }
    }

    // Send Note On to MIDI output
    public void SendNoteOn(int outputIndex, int channel, int note, int velocity)
    {
        int status = 0x90 | (channel & 0x0F);
        SendMidiMessage(outputIndex, status, note, velocity);
    }

    // Send Note Off to MIDI output
    public void SendNoteOff(int outputIndex, int channel, int note)
    {
        int status = 0x80 | (channel & 0x0F);
        SendMidiMessage(outputIndex, status, note, 0);
    }

    // Send Control Change to MIDI output
    public void SendControlChange(int outputIndex, int channel, int controller, int value)
    {
        int status = 0xB0 | (channel & 0x0F);
        SendMidiMessage(outputIndex, status, controller, value);
    }

    // === VST Plugin Methods ===

    // Get VST Host instance
    public VstHost VstHost => _vstHost;

    // Scan for VST plugins
    public List<VstPluginInfo> ScanVstPlugins()
    {
        return _vstHost.ScanForPlugins();
    }

    // Load a VST plugin by name
    public VstPlugin? LoadVstPlugin(string nameOrPath)
    {
        var plugin = _vstHost.LoadPlugin(nameOrPath);
        if (plugin != null)
        {
            AddSampleProvider(plugin);
        }
        return plugin;
    }

    // Load a VST plugin by index
    public VstPlugin? LoadVstPluginByIndex(int index)
    {
        var plugin = _vstHost.LoadPluginByIndex(index);
        if (plugin != null)
        {
            AddSampleProvider(plugin);
        }
        return plugin;
    }

    // Get a loaded VST plugin
    public VstPlugin? GetVstPlugin(string name)
    {
        return _vstHost.GetPlugin(name);
    }

    // Route MIDI input to a VST plugin
    public void RouteMidiToVst(int deviceIndex, VstPlugin plugin)
    {
        lock (_midiInputRouting)
        {
            _midiInputRouting[deviceIndex] = plugin;
        }
    }

    // Unload a VST plugin
    public void UnloadVstPlugin(string name)
    {
        _vstHost.UnloadPlugin(name);
    }

    // Print discovered VST plugins
    public void PrintVstPlugins()
    {
        _vstHost.PrintDiscoveredPlugins();
    }

    // Print loaded VST plugins
    public void PrintLoadedVstPlugins()
    {
        _vstHost.PrintLoadedPlugins();
    }
    
    // Mixer and Channel Management
    public void AddSampleProvider(ISampleProvider provider)
    {
        var resampled = provider.WaveFormat.SampleRate == _waveFormat.SampleRate // No resampling needed
            ? provider // Use as is
            : new WdlResamplingSampleProvider(provider, _waveFormat.SampleRate); // Resample to match engine sample rate
        
        var volumeProvider = new VolumeSampleProvider(resampled); // Create volume control for the channel
        
        lock (_channels) // Lock for thread safety
        {
            volumeProvider.Volume = 1.0f;  // Default volume
            _channels.Add(volumeProvider); // Add to the channel list
        }
        
        _mixer.AddMixerInput(volumeProvider); // Add to mixer
    }
    
    // Set gain for a specific channel
    public void SetChannelGain(int index, float gain)
    {
        lock (_channels)
        {
            if (index >= 0 && index < _channels.Count)
            {
                _channels[index].Volume = gain;
            }
        }
    }
    
    // Set master gain for all channels
    public void SetAllChannelsGain(float gain)
    {
        _masterVolume.Volume = gain; // Set master volume
        _masterGain = gain; // Store master gain
    }

    // === Virtual Audio Channel Methods ===

    /// <summary>
    /// Gets the virtual channel manager.
    /// </summary>
    public VirtualChannelManager VirtualChannels => _virtualChannels;

    /// <summary>
    /// Creates a virtual audio channel that other applications can read from.
    /// </summary>
    public VirtualAudioChannel CreateVirtualChannel(string name)
    {
        var channel = _virtualChannels.CreateChannel(name, _waveFormat.SampleRate, _waveFormat.Channels);
        channel.Start();
        return channel;
    }

    /// <summary>
    /// Sends audio to a virtual channel (call from a sample provider).
    /// </summary>
    public void SendToVirtualChannel(string channelName, float[] samples)
    {
        var channel = _virtualChannels.GetChannel(channelName);
        channel?.Write(samples);
    }

    /// <summary>
    /// Lists all virtual channels.
    /// </summary>
    public void ListVirtualChannels()
    {
        _virtualChannels.ListChannels();
    }

    // Dispose resources
    public void Dispose()
    {
        foreach (var output in _outputs) output.Stop(); // Stop all outputs
        foreach (var input in _inputs) input.StopRecording(); // Stop all inputs

        System.Threading.Thread.Sleep(100); // Give it a moment to stop

        // Unsubscribe FFT event handlers to prevent memory leaks
        foreach (var kvp in _fftHandlers)
        {
            if (_inputAnalyzers.TryGetValue(kvp.Key, out var analyzer))
            {
                analyzer.FftCalculated -= kvp.Value;
            }
        }
        _fftHandlers.Clear();

        // Unsubscribe DataAvailable event handlers to prevent memory leaks
        foreach (var kvp in _dataHandlers)
        {
            if (_inputDevices.TryGetValue(kvp.Key, out var waveIn))
            {
                waveIn.DataAvailable -= kvp.Value;
            }
        }
        _dataHandlers.Clear();
        _inputDevices.Clear();

        // Unsubscribe MIDI MessageReceived event handlers to prevent memory leaks
        for (int i = 0; i < _midiInputs.Count; i++)
        {
            if (_midiHandlers.TryGetValue(i, out var handler))
            {
                _midiInputs[i].MessageReceived -= handler;
            }
        }
        _midiHandlers.Clear();

        foreach (var input in _inputs) input.Dispose(); // Dispose inputs
        foreach (var output in _outputs) output.Dispose(); // Dispose outputs
        foreach (var midiIn in _midiInputs) midiIn.Dispose(); // Dispose MIDI inputs
        foreach (var midiOut in _midiOutputs) midiOut.Dispose(); // Dispose MIDI outputs

        _vstHost.Dispose(); // Dispose VST host and all loaded plugins
        _virtualChannels.Dispose(); // Dispose virtual channels
    }
}
