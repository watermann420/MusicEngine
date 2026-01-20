// ============================================================================
// 09 - VIRTUAL AUDIO CHANNELS
// ============================================================================
// This script demonstrates virtual audio channel creation and routing
// SYNTAX TO REVIEW: Virtual channel functions, named pipe creation
// ============================================================================

Print("=== VIRTUAL AUDIO CHANNELS TEST ===");
Print("");

// ============================================================================
// 1. CREATE VIRTUAL CHANNEL (Simple)
// ============================================================================
Print("1. Create simple virtual channel:");

var channel1 = VirtualChannel.Create("stream");
Print("   VirtualChannel.Create(\"stream\")");
Print($"   → Created channel: {channel1.Name}");
Print($"   → Pipe name: {channel1.PipeName}");
Print("");

// ============================================================================
// 2. CREATE VIRTUAL CHANNEL (With Fluent API)
// ============================================================================
Print("2. Create channel with fluent API:");

var channel2 = VirtualChannel.Create("recording")
    .Volume(0.8)
    .Start();

Print("   VirtualChannel.Create(\"recording\")");
Print("       .Volume(0.8)");
Print("       .Start()");
Print($"   → Channel created and started");
Print("");

// ============================================================================
// 3. CREATE MULTIPLE CHANNELS
// ============================================================================
Print("3. Create multiple channels:");

var drums = VirtualChannel.Create("drums").Volume(0.9).Start();
var bass = VirtualChannel.Create("bass").Volume(0.7).Start();
var synth = VirtualChannel.Create("synth").Volume(0.6).Start();
var vocals = VirtualChannel.Create("vocals").Volume(0.8).Start();

Print("   Created 4 channels: drums, bass, synth, vocals");
Print("");

// ============================================================================
// 4. LIST ALL VIRTUAL CHANNELS
// ============================================================================
Print("4. List all virtual channels:");

VirtualChannel.List();
Print("   VirtualChannel.List()");
Print("   → Shows all active channels");
Print("");

// ============================================================================
// 5. CHANNEL CONTROL METHODS
// ============================================================================
Print("5. Channel control methods:");

// Start channel
channel1.Start();
Print("   channel1.Start()");
Print("   → Channel started (pipe server active)");

// Stop channel
channel1.Stop();
Print("   channel1.Stop()");
Print("   → Channel stopped (pipe server closed)");

// Set volume
channel1.SetVolume(0.75);
Print("   channel1.SetVolume(0.75)");
Print("   → Volume set to 75%");
Print("");

// ============================================================================
// 6. GET PIPE NAME
// ============================================================================
Print("6. Get pipe name for external programs:");

var pipeName = channel1.GetPipeName();
Print($"   channel1.GetPipeName() → \"{pipeName}\"");
Print("   → Use this name to connect external programs");
Print("");

// ============================================================================
// 7. GET CHANNEL OBJECT (For Direct Control)
// ============================================================================
Print("7. Get channel object:");

var channelObj = channel1.GetChannel();
Print("   channel1.GetChannel()");
Print("   → Returns VirtualAudioChannel object for direct access");
Print("");

// ============================================================================
// 8. ROUTE SYNTH TO VIRTUAL CHANNEL
// ============================================================================
Print("8. Route synth to virtual channel:");

var testSynth = CreateSynth("test-synth");

// Route synth output to virtual channel
// (Note: This requires integration with Engine's audio routing)
Print("   // Synth audio routing to virtual channels");
Print("   // is handled through Engine.AddVirtualChannel()");
Print("");

// ============================================================================
// 9. PRACTICAL EXAMPLE - STREAMING SETUP
// ============================================================================
Print("9. Practical example - Streaming setup:");

// Create channel for streaming
var streamChannel = VirtualChannel.Create("obs-stream")
    .Volume(0.85)
    .Start();

Print("   Stream channel created:");
Print($"   - Name: {streamChannel.Name}");
Print($"   - Pipe: {streamChannel.GetPipeName()}");
Print("   - Connect OBS/Discord to this pipe");
Print("");

// ============================================================================
// 10. PRACTICAL EXAMPLE - MULTITRACK RECORDING
// ============================================================================
Print("10. Practical example - Multitrack recording:");

var track1 = VirtualChannel.Create("track-drums").Volume(1.0).Start();
var track2 = VirtualChannel.Create("track-bass").Volume(1.0).Start();
var track3 = VirtualChannel.Create("track-melody").Volume(1.0).Start();
var track4 = VirtualChannel.Create("track-fx").Volume(1.0).Start();

Print("   Multitrack setup created:");
Print("   - track-drums");
Print("   - track-bass");
Print("   - track-melody");
Print("   - track-fx");
Print("   Each track can be recorded separately");
Print("");

// ============================================================================
// 11. EXTERNAL PROGRAM CONNECTION EXAMPLE
// ============================================================================
Print("11. How external programs connect:");

Print("   PYTHON EXAMPLE:");
Print("   ```python");
Print("   import sounddevice as sd");
Print($"   pipe_name = r'\\\\.\\pipe\\{streamChannel.GetPipeName()}'");
Print("   # Connect to pipe and read audio data");
Print("   ```");
Print("");

Print("   C# EXAMPLE:");
Print("   ```csharp");
Print("   using System.IO.Pipes;");
Print($"   var pipe = new NamedPipeClientStream(\".\", \"{streamChannel.GetPipeName()}\");");
Print("   pipe.Connect();");
Print("   // Read audio data from pipe");
Print("   ```");
Print("");

// ============================================================================
// 12. CLEANUP - STOP ALL CHANNELS
// ============================================================================
Print("12. Cleanup - stop all channels:");

channel1.Stop();
channel2.Stop();
drums.Stop();
bass.Stop();
synth.Stop();
vocals.Stop();
streamChannel.Stop();
track1.Stop();
track2.Stop();
track3.Stop();
track4.Stop();

Print("   All channels stopped");
Print("");

Print("=== VIRTUAL AUDIO CHANNELS TEST COMPLETED ===");

// ============================================================================
// SYNTAX ELEMENTS TO CUSTOMIZE:
// ============================================================================
// VIRTUAL CHANNEL FUNCTIONS:
// - VirtualChannel.Create (could be: create, new, make, channel, pipe)
// - VirtualChannel.List (could be: list, show, all, channels, enumerate)
//
// CHANNEL BUILDER METHODS:
// - Volume (could be: volume, vol, gain, level)
// - Start (could be: start, begin, open, activate, run)
//
// CHANNEL CONTROL METHODS:
// - Start (could be: start, begin, open, activate)
// - Stop (could be: stop, close, end, deactivate)
// - SetVolume (could be: volume, vol, setVolume, setLevel, gain)
// - GetPipeName (could be: pipeName, name, pipe, getPipe)
// - GetChannel (could be: channel, object, instance, get)
//
// PROPERTY NAMES:
// - Name (could be: name, id, label, channelName)
// - PipeName (could be: pipeName, pipe, path, pipeId)
//
// ALTERNATIVE NAMING IDEAS:
// - VirtualChannel → VChan, Channel, Pipe, AudioPipe, VirtualOutput
// - Create → new, make, add, open
// - Volume → vol, gain, level, amp
// - Start/Stop → open/close, begin/end, activate/deactivate
// ============================================================================
