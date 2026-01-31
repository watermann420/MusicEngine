// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Midi;
using NAudio.Wave.SampleProviders;
using Microsoft.Extensions.Logging;
using MusicEngine.Core.Events;
using MusicEngine.Core.PDC;
using MusicEngine.Core.Progress;
using MusicEngine.Core.Routing;
using MusicEngine.Infrastructure.Logging;
using MusicEngine.Infrastructure.Memory;


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
    private readonly Dictionary<int, bool> _midiLogEnabled = new(); // General MIDI logging flag per device (notes/cc/pitch, no clock)
    private readonly Dictionary<int, bool> _midiLogClockEnabled = new(); // TimingClock logging per device
    private readonly Dictionary<int, bool> _midiLogCcEnabled = new(); // CC-only toggle (in addition to general log)
    private readonly Dictionary<int, IWaveIn> _inputDevices = new(); // Input devices for handler cleanup
    private readonly MixingSampleProvider _mixer; // Main Mixer
    private readonly VolumeSampleProvider _masterVolume; // Master Volume Control
    private readonly WaveFormat _waveFormat; // Audio Format
    private readonly List<VolumeSampleProvider> _channels = new(); // Individual Channel Volume Controls
    private volatile float _masterGain = 1.0f; // Master Gain

    // VST Host
    private readonly VstHost _vstHost = new(); // VST Plugin Host
    private readonly Dictionary<string, VstPlugin> _vstRouting = new(); // VST Plugin Routing

    // Virtual Audio Channels
    private readonly VirtualChannelManager _virtualChannels = new();

    // Plugin Delay Compensation (PDC)
    private readonly PdcManager _pdcManager;

    // Routing Matrix and Sidechain Management
    private readonly RoutingMatrix _routingMatrix;
    private readonly SidechainBusManager _sidechainBusManager;
    private readonly TrackGroupManager _trackGroupManager;
    private readonly Dictionary<string, ISampleProvider> _registeredSources = new();

    // Logging
    private readonly ILogger? _logger;

    // Events for external subscribers
    public event EventHandler<ChannelEventArgs>? ChannelAdded;
    public event EventHandler<PluginEventArgs>? PluginLoaded;
    public event EventHandler<PluginEventArgs>? PluginUnloaded;
    public event EventHandler<MidiRoutingEventArgs>? MidiRoutingChanged;

    // Constructor
    public AudioEngine(int? sampleRate = null) : this(sampleRate, null)
    {
    }

    // Constructor with logging support
    public AudioEngine(int? sampleRate = null, ILogger? logger = null)
    {
        _logger = logger;
        int rate = sampleRate ?? Settings.SampleRate; // Use provided or default sample rate
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels); // Create a wave format
        _mixer = new MixingSampleProvider(_waveFormat); // Initialize mixer
        _mixer.ReadFully = true; // Ensure continuous output
        _masterVolume = new VolumeSampleProvider(_mixer); // Master volume control

        // Initialize PDC Manager
        _pdcManager = new PdcManager(rate, Settings.Channels, logger);

        // Initialize Routing Matrix and Managers
        _routingMatrix = new RoutingMatrix(rate, Settings.Channels);
        _sidechainBusManager = new SidechainBusManager(_waveFormat);
        _trackGroupManager = new TrackGroupManager();

        _logger?.LogInformation("AudioEngine initialized with sample rate {SampleRate}Hz", rate);
    }

    // MIDI Routing and Mapping Methods
    public void RouteMidiInput(int deviceIndex, ISynth synth)
    {
        lock (_midiInputRouting)
        {
            _midiInputRouting[deviceIndex] = synth; // Route MIDI input to synth
        }

        string? deviceName;
        lock (_midiInputNames)
        {
            deviceName = _midiInputNames.TryGetValue(deviceIndex, out var name) ? name : null;
        }
        var handler = MidiRoutingChanged;
        handler?.Invoke(this, new MidiRoutingEventArgs(deviceIndex, deviceName, synth.Name));
        _logger?.LogDebug("MIDI input {DeviceIndex} routed to {SynthName}", deviceIndex, synth.Name);
    }

    /// <summary>
    /// Enable/disable verbose MIDI logging for a given input device.
    /// </summary>
    public void SetMidiLogging(int deviceIndex, bool enabled)
    {
        lock (_midiLogEnabled)
        {
            _midiLogEnabled[deviceIndex] = enabled;
        }
        _logger?.LogInformation("MIDI logging {State} for device {DeviceIndex}", enabled ? "enabled" : "disabled", deviceIndex);
    }

    /// <summary>
    /// Convenience for script bindings: log.info(true/false) or log.stop().
    /// </summary>
    public void SetMidiLogInfo(int deviceIndex, bool enabled = true) => SetMidiLogging(deviceIndex, enabled);

    /// <summary>Enable/disable logging of MIDI TimingClock for a device.</summary>
    public void SetMidiLogClock(int deviceIndex, bool enabled = true)
    {
        lock (_midiLogClockEnabled)
        {
            _midiLogClockEnabled[deviceIndex] = enabled;
        }
        _logger?.LogInformation("MIDI clock logging {State} for device {DeviceIndex}", enabled ? "enabled" : "disabled", deviceIndex);
    }

    /// <summary>Enable/disable logging of MIDI CC messages for a device (in addition to info logging).</summary>
    public void SetMidiLogCc(int deviceIndex, bool enabled = true)
    {
        lock (_midiLogCcEnabled)
        {
            _midiLogCcEnabled[deviceIndex] = enabled;
        }
        _logger?.LogInformation("MIDI CC logging {State} for device {DeviceIndex}", enabled ? "enabled" : "disabled", deviceIndex);
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

    /// <summary>Log basic screen capability info for a MIDI device (best effort).</summary>
    public void LogMidiScreen(int deviceIndex)
    {
        string name = GetMidiInputNameSafe(deviceIndex);
        var lower = name.ToLowerInvariant();
        string capability =
            lower.Contains("push") || lower.Contains("maschine")
                ? "screen detected (assumed Push/Maschine class)"
                : lower.Contains("launchpad")
                    ? "no screen (grid controller)"
                    : "no known screen support";
        var msg = $"[MIDI {deviceIndex}] {name}: {capability}";
        if (_logger != null) _logger.LogInformation(msg); else Console.WriteLine(msg);
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
        lock (_midiLogEnabled)
        {
            _midiLogEnabled.Clear(); // Clear MIDI logging flags
        }
        lock (_midiLogClockEnabled)
        {
            _midiLogClockEnabled.Clear(); // Clear clock logging flags
        }
        lock (_midiLogCcEnabled)
        {
            _midiLogCcEnabled.Clear(); // Clear CC logging flags
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

            var analyzer = new FrequencyAnalyzer(Settings.FftSize, _waveFormat.SampleRate);
            WaveInEvent? waveIn = null;

            // Store the FFT handler for later cleanup
            Action<float[]> fftHandler = magnitudes => {
                lock (_frequencyMappings)
                {
                    foreach (var mapping in _frequencyMappings)
                    {
                        if (mapping.DeviceIndex == deviceIndex)
                        {
                            float magnitude = analyzer.GetMagnitudeForRange(magnitudes, mapping.LowFreq, mapping.HighFreq);
                            mapping.ProcessMagnitude(magnitude);
                        }
                    }
                }
            };

            try
            {
                waveIn = new WaveInEvent
                {
                    DeviceNumber = deviceIndex,
                    WaveFormat = new WaveFormat(_waveFormat.SampleRate, 16, 1)
                };

                // Store the DataAvailable handler for later cleanup
                EventHandler<WaveInEventArgs> dataHandler = (s, e) => {
                    int sampleCount = e.BytesRecorded / 2;
                    using var pooledBuffer = AudioBufferPool.Instance.RentScoped(sampleCount);
                    var samples = pooledBuffer.Data;
                    for (int i = 0; i < sampleCount; i++)
                    {
                        samples[i] = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;
                    }
                    analyzer.AddSamples(pooledBuffer.Array, sampleCount);
                };

                // Register handlers only after successful waveIn creation
                analyzer.FftCalculated += fftHandler;
                waveIn.DataAvailable += dataHandler;

                // Store references for cleanup
                _fftHandlers[deviceIndex] = fftHandler;
                _dataHandlers[deviceIndex] = dataHandler;
                _inputDevices[deviceIndex] = waveIn;

                waveIn.StartRecording();
                _inputs.Add(waveIn);
                _inputAnalyzers[deviceIndex] = analyzer;
                _logger?.LogDebug("Started capturing from Input Device [{Index}]", deviceIndex);
            }
            catch (Exception ex)
            {
                // Cleanup on failure to prevent memory leaks
                waveIn?.Dispose();
                _fftHandlers.Remove(deviceIndex);
                _dataHandlers.Remove(deviceIndex);
                _inputDevices.Remove(deviceIndex);
                _logger?.LogWarning(ex, "Failed to start input capture for device {Index}", deviceIndex);
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
        Console.WriteLine("[AudioEngine] Step 1: Creating WaveOutEvent...");

        // Setup default output
        var output = new WaveOutEvent(); // Create output device

        Console.WriteLine("[AudioEngine] Step 2: Initializing output device...");
        output.Init(_masterVolume); // Initialize with the master volume

        Console.WriteLine("[AudioEngine] Step 3: Starting playback...");
        output.Play(); // Start playback
        _outputs.Add(output); // Store output

        Console.WriteLine("[AudioEngine] Step 4: Enumerating audio outputs...");
        // Enumerate Audio Outputs
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var capabilities = WaveOut.GetCapabilities(i); // Get device capabilities
            Console.WriteLine($"[AudioEngine]   Output {i}: {capabilities.ProductName}");
            _logger?.LogDebug("Found Output Device [{Index}]: {Name}", i, capabilities.ProductName); // Log found device
        }

        Console.WriteLine("[AudioEngine] Step 5: Enumerating audio inputs...");
        // Enumerate Audio Inputs
        for (int i = 0; i < WaveIn.DeviceCount; i++)
        {
            var capabilities = WaveIn.GetCapabilities(i); // Get device capabilities
            Console.WriteLine($"[AudioEngine]   Input {i}: {capabilities.ProductName}");
            _logger?.LogDebug("Found Input Device [{Index}]: {Name}", i, capabilities.ProductName); // Log found device
        }

        Console.WriteLine("[AudioEngine] Step 6: Enumerating MIDI inputs...");
        // Enumerate MIDI Inputs
        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            var name = MidiIn.DeviceInfo(i).ProductName; // Get device name
            Console.WriteLine($"[AudioEngine]   MIDI Input {i}: {name}");
            _logger?.LogDebug("Found MIDI Input [{Index}]: {Name}", i, name); // Log found device
            _midiInputNames[i] = name; // Store device name
            try
            {
                var midiIn = new MidiIn(i); // Create MIDI input
                int deviceIndex = i; // Capture index for closure

                // Store the MIDI MessageReceived handler for later cleanup
                EventHandler<MidiInMessageEventArgs> midiHandler = (s, e) => HandleMidiMessage(deviceIndex, e);
                midiIn.MessageReceived += midiHandler;
                _midiHandlers[deviceIndex] = midiHandler;

                midiIn.Start(); // Start MIDI input
                _midiInputs.Add(midiIn); // Store MIDI input
            }
            catch (Exception ex)  // Handle exceptions
            {
                _logger?.LogWarning(ex, "Failed to open MIDI Input {Index}", i);
            }
        }

        Console.WriteLine("[AudioEngine] Step 7: Enumerating MIDI outputs...");
        // Enumerate MIDI Outputs
        for (int i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            var name = MidiOut.DeviceInfo(i).ProductName; // Get device name
            Console.WriteLine($"[AudioEngine]   MIDI Output {i}: {name}");
            _logger?.LogDebug("Found MIDI Output [{Index}]: {Name}", i, name); // Log found device
            _midiOutputNames[i] = name; // Store device name
            try
            {
                var midiOut = new MidiOut(i); // Create MIDI output
                _midiOutputs.Add(midiOut); // Store MIDI output
            }
            catch (Exception ex) // Handle exceptions
            {
                Console.WriteLine($"[AudioEngine]   WARNING: Failed to open MIDI Output {i}: {ex.Message}");
                _logger?.LogWarning(ex, "Failed to open MIDI Output {Index}", i);
            }
        }

        Console.WriteLine("[AudioEngine] Step 8: Scanning for VST plugins...");
        // Scan for VST Plugins
        _logger?.LogDebug("Scanning for VST Plugins...");
        var vstPlugins = ScanVstPlugins();
        if (vstPlugins.Count > 0)
        {
            Console.WriteLine($"[AudioEngine] Found {vstPlugins.Count} VST plugin(s)");
            PrintVstPlugins();
        }
        else
        {
            Console.WriteLine("[AudioEngine] No VST plugins found");
            _logger?.LogDebug("No VST plugins found in configured paths.");
        }

        Console.WriteLine("[AudioEngine] Initialization complete!");
    }

    /// <summary>
    /// Asynchronously initializes the audio engine with progress reporting.
    /// </summary>
    /// <param name="progress">Optional progress reporter for initialization status.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the initialization.</param>
    /// <returns>A task that completes when initialization is finished.</returns>
    /// <remarks>
    /// This method performs the following initialization steps:
    /// <list type="number">
    /// <item>Sets up the default audio output device</item>
    /// <item>Enumerates available audio input and output devices</item>
    /// <item>Enumerates MIDI input and output devices</item>
    /// <item>Scans for VST plugins</item>
    /// </list>
    /// Uses <see cref="Progress.InitializationProgress"/> record for structured progress reporting.
    /// </remarks>
    /// <example>
    /// <code>
    /// var engine = new AudioEngine();
    /// var progress = new Progress&lt;InitializationProgress&gt;(p =>
    ///     Console.WriteLine($"{p.Stage}: {p.PercentComplete:F1}%"));
    ///
    /// await engine.InitializeAsync(progress, cancellationToken);
    /// </code>
    /// </example>
    public async Task InitializeAsync(
        IProgress<Progress.InitializationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        const int totalSteps = 5;

        await Task.Run(() =>
        {
            progress?.Report(new Progress.InitializationProgress(
                "Audio Output", 1, totalSteps, "Setting up default audio output device"));

            // Setup default output
            var output = new WaveOutEvent();
            output.Init(_masterVolume);
            output.Play();
            _outputs.Add(output);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new Progress.InitializationProgress(
                "Audio Devices", 2, totalSteps,
                $"Found {WaveOut.DeviceCount} output, {WaveIn.DeviceCount} input devices"));

            // Enumerate Audio Outputs
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var capabilities = WaveOut.GetCapabilities(i);
                _logger?.LogDebug("Found Output Device [{Index}]: {Name}", i, capabilities.ProductName);
            }

            // Enumerate Audio Inputs
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var capabilities = WaveIn.GetCapabilities(i);
                _logger?.LogDebug("Found Input Device [{Index}]: {Name}", i, capabilities.ProductName);
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new Progress.InitializationProgress(
                "MIDI Devices", 3, totalSteps,
                $"Found {MidiIn.NumberOfDevices} MIDI inputs, {MidiOut.NumberOfDevices} MIDI outputs"));

            // Enumerate MIDI Inputs (simplified - the full MIDI setup remains in sync Initialize)
            for (int i = 0; i < MidiIn.NumberOfDevices; i++)
            {
                var name = MidiIn.DeviceInfo(i).ProductName;
                _logger?.LogDebug("Found MIDI Input [{Index}]: {Name}", i, name);
                _midiInputNames[i] = name;
            }

            // Enumerate MIDI Outputs
            for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            {
                var name = MidiOut.DeviceInfo(i).ProductName;
                _logger?.LogDebug("Found MIDI Output [{Index}]: {Name}", i, name);
                _midiOutputNames[i] = name;
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new Progress.InitializationProgress(
                "VST Plugins", 4, totalSteps, "Scanning for VST plugins..."));

            // Scan for VST Plugins
            _logger?.LogInformation("Scanning for VST Plugins...");
            var vstPlugins = ScanVstPlugins();
            if (vstPlugins.Count > 0)
            {
                _logger?.LogInformation("Found {Count} VST plugins", vstPlugins.Count);
            }

            progress?.Report(Progress.InitializationProgress.Complete(totalSteps, "AudioEngine initialization complete"));
            _logger?.LogInformation("AudioEngine initialization complete");

        }, cancellationToken).ConfigureAwait(false);
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

    #region MIDI Logging

    private void LogMidiEvent(int deviceIndex, MidiInMessageEventArgs e)
    {
        var infoEnabled = _midiLogEnabled.TryGetValue(deviceIndex, out var info) && info;
        var clockEnabled = _midiLogClockEnabled.TryGetValue(deviceIndex, out var clk) && clk;
        var ccEnabled = _midiLogCcEnabled.TryGetValue(deviceIndex, out var ccFlag) && ccFlag;

        string msg;
        switch (e.MidiEvent)
        {
            case NAudio.Midi.NoteOnEvent on when on.Velocity > 0:
                if (!infoEnabled) return;
                msg = $"[MIDI {deviceIndex}] NoteOn ch {on.Channel + 1} note {on.NoteNumber} vel {on.Velocity}";
                break;
            case NAudio.Midi.NoteOnEvent onOff when onOff.Velocity == 0:
                if (!infoEnabled) return;
                msg = $"[MIDI {deviceIndex}] NoteOff ch {onOff.Channel + 1} note {onOff.NoteNumber} (NoteOn vel0)";
                break;
            case NAudio.Midi.NoteEvent off when off.CommandCode == MidiCommandCode.NoteOff:
                if (!infoEnabled) return;
                msg = $"[MIDI {deviceIndex}] NoteOff ch {off.Channel + 1} note {off.NoteNumber}";
                break;
            case NAudio.Midi.ControlChangeEvent cc:
                if (!(infoEnabled || ccEnabled)) return;
                msg = $"[MIDI {deviceIndex}] CC ch {cc.Channel + 1} #{(int)cc.Controller} val {cc.ControllerValue}";
                break;
            case NAudio.Midi.PitchWheelChangeEvent pb:
                if (!infoEnabled) return;
                msg = $"[MIDI {deviceIndex}] PitchBend ch {pb.Channel + 1} value {pb.Pitch}";
                break;
            case MidiEvent clock when clock.CommandCode == MidiCommandCode.TimingClock:
                if (!clockEnabled) return;
                msg = $"[MIDI {deviceIndex}] TimingClock";
                break;
            default:
                if (!infoEnabled) return;
                msg = $"[MIDI {deviceIndex}] {e.MidiEvent}";
                break;
        }
        if (_logger != null)
        {
            _logger.LogInformation(msg);
        }
        else
        {
            Console.WriteLine(msg);
        }
    }

    private void HandleMidiMessage(int deviceIndex, MidiInMessageEventArgs e)
    {
        TryLogMidiEvent(deviceIndex, e);

        switch (e.MidiEvent)
        {
            case ControlChangeEvent ccEvent:
                HandleControlChange(deviceIndex, ccEvent);
                return;
            case PitchWheelChangeEvent pitchEvent:
                HandlePitchBend(deviceIndex, pitchEvent);
                return;
            case NAudio.Midi.NoteEvent noteEvent:
                if (HandleNoteWithRanges(deviceIndex, noteEvent)) return;
                var routedSynth = GetRoutedSynth(deviceIndex);
                if (routedSynth != null)
                {
                    ProcessNoteEvent(noteEvent, routedSynth);
                    return;
                }
                HandleTransportNote(deviceIndex, noteEvent);
                return;
            default:
                return;
        }
    }

    private void TryLogMidiEvent(int deviceIndex, MidiInMessageEventArgs e)
    {
        var infoEnabled = _midiLogEnabled.TryGetValue(deviceIndex, out var info) && info;
        var clockEnabled = _midiLogClockEnabled.TryGetValue(deviceIndex, out var clk) && clk;
        var ccEnabled = _midiLogCcEnabled.TryGetValue(deviceIndex, out var ccFlag) && ccFlag;

        if (!infoEnabled && !clockEnabled && !ccEnabled) return;
        try { LogMidiEvent(deviceIndex, e); } catch { /* ignore logging errors */ }
    }

    private void HandleControlChange(int deviceIndex, ControlChangeEvent ccEvent)
    {
        float normalizedValue = ccEvent.ControllerValue / 127f;

        var ccRoutedSynth = GetRoutedSynth(deviceIndex);
        if (ccRoutedSynth != null)
        {
            ApplyCommonCcToSynth(ccEvent, normalizedValue, ccRoutedSynth);
        }

        ApplyMappedCc(deviceIndex, ccEvent, normalizedValue);
        ApplyTransportCc(deviceIndex, ccEvent, normalizedValue);
    }

    private void ApplyCommonCcToSynth(ControlChangeEvent ccEvent, float normalizedValue, ISynth synth)
    {
        switch ((int)ccEvent.Controller)
        {
            case 1: synth.SetParameter("modwheel", normalizedValue); break;            // Mod Wheel
            case 7: synth.SetParameter("volume", normalizedValue); break;              // Volume
            case 10: synth.SetParameter("pan", normalizedValue * 2f - 1f); break;      // Pan -1..+1
            case 74: synth.SetParameter("cutoff", normalizedValue); break;             // Filter cutoff
            case 71: synth.SetParameter("resonance", normalizedValue); break;          // Resonance
            case 73: synth.SetParameter("attack", normalizedValue * 2f); break;        // Attack 0-2s
            case 72: synth.SetParameter("release", normalizedValue * 2f); break;       // Release 0-2s
        }
    }

    private void ApplyMappedCc(int deviceIndex, ControlChangeEvent ccEvent, float normalizedValue)
    {
        lock (_midiMappings)
        {
            foreach (var mapping in _midiMappings)
            {
                if (mapping.deviceIndex == deviceIndex && mapping.control == (int)ccEvent.Controller)
                {
                    mapping.synth.SetParameter(mapping.parameter, normalizedValue);
                }
            }
        }
    }

    private void ApplyTransportCc(int deviceIndex, ControlChangeEvent ccEvent, float normalizedValue)
    {
        lock (_transportMappings)
        {
            foreach (var mapping in _transportMappings)
            {
                if (mapping.deviceIndex == deviceIndex && mapping.command == ((int)ccEvent.Controller).ToString())
                {
                    mapping.action(normalizedValue);
                }
            }
        }
    }

    private void HandlePitchBend(int deviceIndex, PitchWheelChangeEvent pitchEvent)
    {
        float normalizedBipolar = (pitchEvent.Pitch - 8192) / 8192f;  // -1..+1
        float normalizedUnipolar = pitchEvent.Pitch / 16383f;         // 0..1

        var pitchRoutedSynth = GetRoutedSynth(deviceIndex);
        pitchRoutedSynth?.SetParameter("pitchbend", normalizedBipolar);

        lock (_midiMappings)
        {
            foreach (var mapping in _midiMappings)
            {
                if (mapping.deviceIndex == deviceIndex && mapping.control == -1) // -1 denotes pitch bend
                {
                    mapping.synth.SetParameter(mapping.parameter, normalizedUnipolar);
                }
            }
        }

        lock (_transportMappings)
        {
            foreach (var mapping in _transportMappings)
            {
                if (mapping.deviceIndex == deviceIndex && mapping.command == "pitch")
                {
                    mapping.action(normalizedUnipolar);
                }
            }
        }
    }

    private bool HandleNoteWithRanges(int deviceIndex, NAudio.Midi.NoteEvent noteEvent)
    {
        lock (_rangeMappings)
        {
            foreach (var mapping in _rangeMappings)
            {
                if (mapping.deviceIndex != deviceIndex) continue;
                if (noteEvent.NoteNumber < Math.Min(mapping.startNote, mapping.endNote)) continue;
                if (noteEvent.NoteNumber > Math.Max(mapping.startNote, mapping.endNote)) continue;

                int effectiveNote = mapping.reversed
                    ? mapping.startNote + mapping.endNote - noteEvent.NoteNumber
                    : noteEvent.NoteNumber;

                ProcessEffectiveNoteEvent(noteEvent, mapping.synth, effectiveNote);
                return true;
            }
        }
        return false;
    }

    private ISynth? GetRoutedSynth(int deviceIndex)
    {
        lock (_midiInputRouting)
        {
            _midiInputRouting.TryGetValue(deviceIndex, out var routed);
            return routed;
        }
    }

    private void HandleTransportNote(int deviceIndex, NAudio.Midi.NoteEvent note)
    {
        List<(int deviceIndex, string command, Action<float> action)> mappingsCopy;
        lock (_transportMappings)
        {
            mappingsCopy = _transportMappings.ToList();
        }

        foreach (var mapping in mappingsCopy)
        {
            if (mapping.deviceIndex != deviceIndex) continue;
            if (mapping.command != "note_" + note.NoteNumber) continue;

            var value = note.CommandCode == MidiCommandCode.NoteOn ? 1.0f : 0.0f;
            mapping.action(value);
        }
    }

    #endregion

    // Get MIDI Device Index by Name
    public int GetMidiDeviceIndex(string name)
    {
        lock (_midiInputNames)
        {
            foreach (var kvp in _midiInputNames)
            {
                if (kvp.Value.Contains(name, StringComparison.OrdinalIgnoreCase)) // Case-insensitive match
                    return kvp.Key;
            }
        }
        return -1;
    }

    private string GetMidiInputNameSafe(int deviceIndex)
    {
        lock (_midiInputNames)
        {
            if (_midiInputNames.TryGetValue(deviceIndex, out var name))
                return name;
        }
        return $"Device {deviceIndex}";
    }

    // Get MIDI Output Device Index by Name
    public int GetMidiOutputDeviceIndex(string name)
    {
        lock (_midiOutputNames)
        {
            foreach (var kvp in _midiOutputNames)
            {
                if (kvp.Value.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return kvp.Key;
            }
        }
        return -1;
    }

    // Get MIDI Output by Index
    public MidiOut? GetMidiOutput(int index)
    {
        lock (_midiOutputs)
        {
            if (index >= 0 && index < _midiOutputs.Count)
            {
                return _midiOutputs[index];
            }
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

    // Load a VST plugin by name (returns IVstPlugin to support both VST2 and VST3)
    public IVstPlugin? LoadVstPlugin(string nameOrPath)
    {
        var plugin = _vstHost.LoadPlugin(nameOrPath);
        if (plugin != null)
        {
            AddSampleProvider(plugin);
            var handler = PluginLoaded;
            handler?.Invoke(this, new PluginEventArgs(plugin));
        }
        return plugin;
    }

    // Load a VST plugin by index (returns IVstPlugin to support both VST2 and VST3)
    public IVstPlugin? LoadVstPluginByIndex(int index)
    {
        var plugin = _vstHost.LoadPluginByIndex(index);
        if (plugin != null)
        {
            AddSampleProvider(plugin);
            var handler = PluginLoaded;
            handler?.Invoke(this, new PluginEventArgs(plugin));
        }
        return plugin;
    }

    // Get a loaded VST plugin (returns IVstPlugin to support both VST2 and VST3)
    public IVstPlugin? GetVstPlugin(string name)
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
        var plugin = _vstHost.GetPlugin(name);
        _vstHost.UnloadPlugin(name);
        if (plugin != null)
        {
            var handler = PluginUnloaded;
            handler?.Invoke(this, new PluginEventArgs(plugin));
        }
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

        int channelIndex;
        lock (_channels) // Lock for thread safety
        {
            volumeProvider.Volume = 1.0f;  // Default volume
            _channels.Add(volumeProvider); // Add to the channel list
            channelIndex = _channels.Count - 1; // Capture index under lock
        }

        _mixer.AddMixerInput(volumeProvider); // Add to mixer
        var handler = ChannelAdded;
        handler?.Invoke(this, new ChannelEventArgs(channelIndex));
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

    // === Audio Recording Methods ===

    /// <summary>
    /// Creates an AudioRecorder for recording the master output.
    /// </summary>
    /// <returns>A new AudioRecorder configured with the master output.</returns>
    /// <example>
    /// using var recorder = engine.CreateRecorder();
    /// recorder.StartRecording("output.wav");
    /// // ... play audio ...
    /// recorder.StopRecording();
    /// </example>
    public AudioRecorder CreateRecorder()
    {
        return new AudioRecorder(_masterVolume, _waveFormat.SampleRate, _waveFormat.Channels);
    }

    // === Plugin Delay Compensation (PDC) Methods ===

    /// <summary>
    /// Gets the PDC Manager for managing plugin delay compensation across tracks.
    /// </summary>
    public PdcManager PdcManager => _pdcManager;

    /// <summary>
    /// Gets or sets whether Plugin Delay Compensation is enabled.
    /// </summary>
    public bool PdcEnabled
    {
        get => _pdcManager.Enabled;
        set => _pdcManager.Enabled = value;
    }

    /// <summary>
    /// Gets the maximum latency across all registered PDC tracks in samples.
    /// </summary>
    public int PdcMaxLatencySamples => _pdcManager.MaxLatencySamples;

    /// <summary>
    /// Gets the maximum latency across all registered PDC tracks in milliseconds.
    /// </summary>
    public double PdcMaxLatencyMs => _pdcManager.MaxLatencyMs;

    /// <summary>
    /// Registers a track with its effect chain for PDC management.
    /// </summary>
    /// <param name="trackId">Unique identifier for the track.</param>
    /// <param name="effectChain">The effect chain for this track.</param>
    /// <param name="maxCompensationSamples">Maximum compensation delay capacity (default: 1 second).</param>
    public void RegisterTrackForPdc(string trackId, EffectChain effectChain, int maxCompensationSamples = 0)
    {
        var reporters = effectChain.GetLatencyReporters();
        _pdcManager.RegisterTrack(trackId, reporters, maxCompensationSamples);
        _logger?.LogDebug("Registered track '{TrackId}' for PDC with {Count} latency reporters", trackId, reporters.Count);
    }

    /// <summary>
    /// Registers a track with a VST plugin for PDC management.
    /// </summary>
    /// <param name="trackId">Unique identifier for the track.</param>
    /// <param name="vstEffectAdapter">The VST effect adapter for this track.</param>
    /// <param name="maxCompensationSamples">Maximum compensation delay capacity (default: 1 second).</param>
    public void RegisterTrackForPdc(string trackId, VstEffectAdapter vstEffectAdapter, int maxCompensationSamples = 0)
    {
        _pdcManager.RegisterTrack(trackId, vstEffectAdapter, maxCompensationSamples);
        _logger?.LogDebug("Registered track '{TrackId}' for PDC with VST effect '{EffectName}'", trackId, vstEffectAdapter.Name);
    }

    /// <summary>
    /// Unregisters a track from PDC management.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    public void UnregisterTrackFromPdc(string trackId)
    {
        _pdcManager.UnregisterTrack(trackId);
        _logger?.LogDebug("Unregistered track '{TrackId}' from PDC", trackId);
    }

    /// <summary>
    /// Gets the compensation delay buffer for a track to apply PDC during processing.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <returns>The delay compensation buffer, or null if track not found.</returns>
    public DelayCompensationBuffer? GetPdcBuffer(string trackId)
    {
        return _pdcManager.GetCompensationBuffer(trackId);
    }

    /// <summary>
    /// Processes audio through PDC compensation for a specific track.
    /// </summary>
    /// <param name="trackId">The track identifier.</param>
    /// <param name="input">Input audio samples.</param>
    /// <param name="output">Output buffer for compensated audio.</param>
    /// <param name="offset">Offset into the output buffer.</param>
    /// <param name="count">Number of samples to process.</param>
    /// <returns>Number of samples processed.</returns>
    public int ApplyPdcCompensation(string trackId, float[] input, float[] output, int offset, int count)
    {
        return _pdcManager.ProcessCompensation(trackId, input, output, offset, count);
    }

    /// <summary>
    /// Manually triggers recalculation of PDC compensation values.
    /// This is automatically called when track latencies change.
    /// </summary>
    public void RecalculatePdc()
    {
        _pdcManager.RecalculateCompensation();
    }

    /// <summary>
    /// Gets a summary of PDC latencies and compensations for all tracks.
    /// </summary>
    /// <returns>Dictionary mapping track IDs to (Latency, Compensation) tuples.</returns>
    public Dictionary<string, (int Latency, int Compensation)> GetPdcSummary()
    {
        return _pdcManager.GetLatencySummary();
    }

    // === Routing Matrix Methods ===

    /// <summary>
    /// Gets the routing matrix for managing audio signal routing.
    /// </summary>
    public RoutingMatrix RoutingMatrix => _routingMatrix;

    /// <summary>
    /// Gets the sidechain bus manager for managing sidechain signal paths.
    /// </summary>
    public SidechainBusManager SidechainBusManager => _sidechainBusManager;

    /// <summary>
    /// Gets the track group manager for managing track groups.
    /// </summary>
    public TrackGroupManager TrackGroupManager => _trackGroupManager;

    /// <summary>
    /// Registers a track source for routing.
    /// </summary>
    /// <param name="trackId">Unique identifier for the track.</param>
    /// <param name="trackName">Display name for the track.</param>
    /// <param name="source">The audio source sample provider.</param>
    /// <returns>The created routing point for the track output.</returns>
    public RoutingPoint RegisterTrackSource(string trackId, string trackName, ISampleProvider source)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            throw new ArgumentNullException(nameof(trackId));
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        lock (_registeredSources)
        {
            _registeredSources[trackId] = source;
        }

        // Create and register routing point
        var point = RoutingPointFactory.CreateTrackOutput(trackId, trackName, source, source.WaveFormat.Channels);
        _routingMatrix.RegisterPoint(point);

        _logger?.LogDebug("Registered track source '{TrackId}' ({TrackName}) for routing", trackId, trackName);
        return point;
    }

    /// <summary>
    /// Unregisters a track source from routing.
    /// </summary>
    /// <param name="trackId">The track identifier to unregister.</param>
    /// <returns>True if unregistered successfully.</returns>
    public bool UnregisterTrackSource(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            return false;

        lock (_registeredSources)
        {
            _registeredSources.Remove(trackId);
        }

        // Find and unregister the routing point
        var point = _routingMatrix.GetAllPoints()
            .Find(p => p.AssociatedTrackId == trackId);

        if (point != null)
        {
            _routingMatrix.UnregisterPoint(point.PointId);
            _logger?.LogDebug("Unregistered track source '{TrackId}' from routing", trackId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the sidechain signal from a registered source.
    /// </summary>
    /// <param name="sourceId">The source track ID or routing point name.</param>
    /// <returns>The sample provider for the sidechain signal, or null if not found.</returns>
    /// <remarks>
    /// This method retrieves the audio signal from a registered track source
    /// that can be used as a sidechain input for dynamics processors like compressors.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get the kick drum track as a sidechain source
    /// var kickSidechain = engine.GetSidechainSignal("kick_track");
    /// if (kickSidechain != null)
    /// {
    ///     compressor.ConnectSidechain(kickSidechain);
    /// }
    /// </code>
    /// </example>
    public ISampleProvider? GetSidechainSignal(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            return null;

        // First check registered sources
        lock (_registeredSources)
        {
            if (_registeredSources.TryGetValue(sourceId, out var source))
            {
                return source;
            }
        }

        // Check routing points
        var point = _routingMatrix.GetPointByName(sourceId);
        if (point?.SampleProvider != null)
        {
            return point.SampleProvider;
        }

        // Check by track ID in routing points
        point = _routingMatrix.GetAllPoints()
            .Find(p => p.AssociatedTrackId?.Equals(sourceId, StringComparison.OrdinalIgnoreCase) == true);

        return point?.SampleProvider;
    }

    /// <summary>
    /// Creates a sidechain bus that routes audio from a source track to a target effect.
    /// </summary>
    /// <param name="sourceTrackId">The source track ID.</param>
    /// <param name="targetEffectName">The target effect name.</param>
    /// <param name="tapPoint">Where to tap the signal (default: PostFader).</param>
    /// <returns>The created sidechain bus.</returns>
    public SidechainBus CreateSidechainRoute(string sourceTrackId, string targetEffectName,
        SidechainTapPoint tapPoint = SidechainTapPoint.PostFader)
    {
        var source = GetSidechainSignal(sourceTrackId);
        var bus = _sidechainBusManager.CreateBusForEffect(targetEffectName, source, sourceTrackId);
        bus.TapPoint = tapPoint;

        // Create routing points and route
        var sourcePoint = _routingMatrix.GetAllPoints()
            .Find(p => p.AssociatedTrackId == sourceTrackId);

        if (sourcePoint == null)
        {
            // Create a send point if source point doesn't exist
            sourcePoint = RoutingPointFactory.CreateSend($"{sourceTrackId} SC Send", sourceTrackId);
            sourcePoint.SampleProvider = source;
            _routingMatrix.RegisterPoint(sourcePoint);
        }

        var destPoint = RoutingPointFactory.CreateSidechainInput(targetEffectName);
        _routingMatrix.RegisterPoint(destPoint);

        // Create the route if both points exist
        if (sourcePoint.CanBeSource)
        {
            try
            {
                _routingMatrix.CreateRoute(sourcePoint, destPoint, 0f);
                _logger?.LogDebug("Created sidechain route from '{Source}' to '{Target}'", sourceTrackId, targetEffectName);
            }
            catch (InvalidOperationException ex)
            {
                _logger?.LogWarning(ex, "Could not create sidechain route: {Message}", ex.Message);
            }
        }

        return bus;
    }

    /// <summary>
    /// Creates a route between two tracks or routing points.
    /// </summary>
    /// <param name="sourceId">The source track ID or routing point name.</param>
    /// <param name="destinationId">The destination track ID or routing point name.</param>
    /// <param name="gainDb">Route gain in decibels (default: 0 dB).</param>
    /// <returns>The created audio route, or null if routing is not possible.</returns>
    public AudioRoute? CreateRoute(string sourceId, string destinationId, float gainDb = 0f)
    {
        var sourcePoint = _routingMatrix.GetPointByName(sourceId) ??
            _routingMatrix.GetAllPoints().Find(p => p.AssociatedTrackId == sourceId);

        var destPoint = _routingMatrix.GetPointByName(destinationId) ??
            _routingMatrix.GetAllPoints().Find(p => p.AssociatedTrackId == destinationId);

        if (sourcePoint == null || destPoint == null)
        {
            _logger?.LogWarning("Cannot create route: source or destination point not found");
            return null;
        }

        try
        {
            return _routingMatrix.CreateRoute(sourcePoint, destPoint, gainDb);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to create route from '{Source}' to '{Destination}'", sourceId, destinationId);
            return null;
        }
    }

    /// <summary>
    /// Gets the routing matrix visualization data for UI display.
    /// </summary>
    /// <returns>List of matrix cells representing the routing state.</returns>
    public List<MatrixCell> GetRoutingMatrixVisualization()
    {
        return _routingMatrix.GetMatrixCells();
    }

    /// <summary>
    /// Gets a summary of the current routing configuration.
    /// </summary>
    public string GetRoutingSummary()
    {
        return _routingMatrix.GetRoutingSummary();
    }

    // === Track Group Methods ===

    /// <summary>
    /// Creates a new track group.
    /// </summary>
    /// <param name="name">The name of the group.</param>
    /// <param name="color">Optional display color (as ARGB int).</param>
    /// <returns>The created track group.</returns>
    public TrackGroup CreateTrackGroup(string name, int? color = null)
    {
        var group = _trackGroupManager.CreateGroup(name, color);

        // Create a routing point for the group
        var groupPoint = RoutingPointFactory.CreateGroup(name);
        _routingMatrix.RegisterPoint(groupPoint);

        _logger?.LogDebug("Created track group '{GroupName}'", name);
        return group;
    }

    /// <summary>
    /// Adds a track to a group.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <param name="trackId">The track ID to add.</param>
    /// <param name="channel">Optional audio channel reference.</param>
    /// <returns>True if successful.</returns>
    public bool AddTrackToGroup(Guid groupId, string trackId, AudioChannel? channel = null)
    {
        return _trackGroupManager.AddTrackToGroup(groupId, trackId, channel);
    }

    /// <summary>
    /// Removes a track from its group.
    /// </summary>
    /// <param name="trackId">The track ID to remove.</param>
    /// <returns>True if successful.</returns>
    public bool RemoveTrackFromGroup(string trackId)
    {
        return _trackGroupManager.RemoveTrackFromGroup(trackId);
    }

    /// <summary>
    /// Gets the group that a track belongs to.
    /// </summary>
    /// <param name="trackId">The track ID.</param>
    /// <returns>The track group, or null if not in any group.</returns>
    public TrackGroup? GetGroupForTrack(string trackId)
    {
        return _trackGroupManager.GetGroupForTrack(trackId);
    }

    /// <summary>
    /// Gets all track groups.
    /// </summary>
    public IReadOnlyList<TrackGroup> GetAllTrackGroups()
    {
        return _trackGroupManager.Groups;
    }

    private int _disposed; // 0 = not disposed, 1 = disposed

    // Dispose resources
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        // Stop all outputs and inputs safely
        foreach (var output in _outputs)
        {
            try { output.Stop(); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Error stopping audio output"); }
        }
        foreach (var input in _inputs)
        {
            try { input.StopRecording(); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Error stopping audio input"); }
        }

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

        // Dispose input analyzers
        foreach (var kvp in _inputAnalyzers)
        {
            try { (kvp.Value as IDisposable)?.Dispose(); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Error disposing input analyzer for device {Index}", kvp.Key); }
        }
        _inputAnalyzers.Clear();

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

        // Dispose all resources with exception handling
        foreach (var input in _inputs)
        {
            try { input.Dispose(); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Error disposing audio input"); }
        }
        foreach (var output in _outputs)
        {
            try { output.Dispose(); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Error disposing audio output"); }
        }
        foreach (var midiIn in _midiInputs)
        {
            try { midiIn.Dispose(); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Error disposing MIDI input"); }
        }
        foreach (var midiOut in _midiOutputs)
        {
            try { midiOut.Dispose(); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Error disposing MIDI output"); }
        }

        try { _vstHost.Dispose(); }
        catch (Exception ex) { _logger?.LogWarning(ex, "Error disposing VST host"); }

        try { _virtualChannels.Dispose(); }
        catch (Exception ex) { _logger?.LogWarning(ex, "Error disposing virtual channels"); }

        try { _pdcManager.Dispose(); }
        catch (Exception ex) { _logger?.LogWarning(ex, "Error disposing PDC manager"); }

        try { _routingMatrix.Dispose(); }
        catch (Exception ex) { _logger?.LogWarning(ex, "Error disposing routing matrix"); }

        _trackGroupManager.Clear();
        _sidechainBusManager.Clear();

        _logger?.LogInformation("AudioEngine disposed");

        GC.SuppressFinalize(this);
    }
}
