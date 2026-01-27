# Audio Streaming Protocol - Developer Guide

This guide explains how to use the P2P audio streaming system for real-time audio communication, similar to Discord but with raw audio samples for lower latency.

## Overview

The `AudioStreamingService` enables:

- **P2P Audio Calls**: Direct connection between two computers without a central server
- **Raw Audio Streaming**: Send uncompressed audio for lowest latency
- **Command Mode**: Send MusicEngine commands instead of audio (ultra-low bandwidth)
- **Text Messaging**: Send text messages over the same connection

## Architecture

```
┌─────────────────┐                              ┌─────────────────┐
│    Client A     │                              │    Client B     │
│                 │                              │                 │
│  ┌───────────┐  │    UDP (Audio/Commands)      │  ┌───────────┐  │
│  │ AudioSink │◄─┼──────────────────────────────┼─►│ AudioSink │  │
│  └───────────┘  │                              │  └───────────┘  │
│                 │                              │                 │
│  ┌───────────┐  │    TCP (Control/Text)        │  ┌───────────┐  │
│  │ TcpClient │◄─┼──────────────────────────────┼─►│ TcpClient │  │
│  └───────────┘  │                              │  └───────────┘  │
│                 │                              │                 │
│  MusicEngine    │                              │  MusicEngine    │
└─────────────────┘                              └─────────────────┘
```

## Quick Start

### 1. Get the Service Instance

```csharp
using MusicEngineEditor.Services;

var streaming = AudioStreamingService.Instance;
```

### 2. Start Listening (Receiver)

```csharp
// Configure settings
streaming.LocalPort = 9000;
streaming.SampleRate = 48000;
streaming.BufferSize = 256;  // Low latency
streaming.Mode = StreamingMode.RawPCM;

// Start listening for connections
await streaming.StartListeningAsync();

// Handle incoming calls
streaming.CallReceived += (sender, peer) =>
{
    Console.WriteLine($"Incoming call from {peer.Name} ({peer.Address})");

    // Accept or reject
    streaming.RespondToCallAsync(peer.PeerId, accept: true);
};
```

### 3. Connect to Peer (Caller)

```csharp
// Connect to remote peer
var peer = await streaming.ConnectAsync("192.168.1.100", 9000);

// Request a call
await streaming.RequestCallAsync(peer.PeerId);

// Wait for acceptance
streaming.CallAccepted += (sender, peerId) =>
{
    Console.WriteLine("Call accepted! Starting audio stream...");
};
```

### 4. Stream Audio

```csharp
// Audio is streamed automatically when in a call
// You can also manually send audio data:

float[] audioBuffer = new float[256];
// ... fill buffer with audio samples ...

await streaming.SendAudioAsync(audioBuffer);
```

## Streaming Modes

### RawPCM Mode (Lowest Latency)

Sends uncompressed 32-bit float audio samples.

```csharp
streaming.Mode = StreamingMode.RawPCM;

// Bandwidth: ~192 kbps per channel @ 48kHz
// Latency: ~5-10ms
```

### CommandsOnly Mode (Lowest Bandwidth)

Sends MusicEngine commands instead of audio. The receiver renders locally.

```csharp
streaming.Mode = StreamingMode.CommandsOnly;

// Bandwidth: ~1-5 kbps
// Latency: Depends on command complexity
```

### Hybrid Mode

Uses commands when possible, falls back to audio for complex sounds.

```csharp
streaming.Mode = StreamingMode.Hybrid;
```

## Packet Format

### Audio Packet (UDP)

```
┌──────────────────────────────────────────────────────────┐
│ Header (16 bytes)                                         │
├──────────────────────────────────────────────────────────┤
│ PacketType   │ Timestamp  │ Sequence │ Channels │ Format │
│ (1 byte)     │ (8 bytes)  │ (4 bytes)│ (1 byte) │(2 bytes)│
├──────────────────────────────────────────────────────────┤
│ Audio Data (variable length)                              │
│ float[] samples (32-bit per sample)                       │
└──────────────────────────────────────────────────────────┘
```

### Command Packet (UDP)

```
┌──────────────────────────────────────────────────────────┐
│ Header (16 bytes)                                         │
├──────────────────────────────────────────────────────────┤
│ PacketType   │ Timestamp  │ Sequence │ CmdType │ Length  │
│ (1 byte)     │ (8 bytes)  │ (4 bytes)│ (1 byte)│(2 bytes)│
├──────────────────────────────────────────────────────────┤
│ Command Data (JSON)                                       │
│ { "type": "PlayNote", "params": { ... } }                │
└──────────────────────────────────────────────────────────┘
```

### Control Packet (TCP)

Used for call setup, text messages, and reliable commands.

```
┌──────────────────────────────────────────────────────────┐
│ Length (4 bytes) │ Type (1 byte) │ Payload (variable)    │
└──────────────────────────────────────────────────────────┘

Types:
  0x01 = CallRequest
  0x02 = CallResponse
  0x03 = CallEnd
  0x04 = TextMessage
  0x05 = Ping
  0x06 = Pong
```

## API Reference

### Events

```csharp
// Connection events
event EventHandler<PeerInfo> PeerConnected;
event EventHandler<string> PeerDisconnected;

// Call events
event EventHandler<PeerInfo> CallReceived;
event EventHandler<string> CallAccepted;
event EventHandler<string> CallRejected;
event EventHandler<string> CallEnded;

// Data events
event EventHandler<AudioDataEventArgs> AudioReceived;
event EventHandler<CommandEventArgs> CommandReceived;
event EventHandler<TextMessageEventArgs> TextMessageReceived;
```

### Methods

```csharp
// Lifecycle
Task StartListeningAsync();
Task StopAsync();

// Connections
Task<PeerInfo> ConnectAsync(string address, int port);
Task DisconnectAsync(string peerId);

// Calls
Task RequestCallAsync(string peerId);
Task RespondToCallAsync(string peerId, bool accept);
Task EndCallAsync(string peerId);

// Data
Task SendAudioAsync(float[] samples);
Task SendCommandAsync(string command, object parameters);
Task SendTextMessageAsync(string peerId, string message);
```

### Properties

```csharp
int LocalPort { get; set; }              // Default: 9000
int SampleRate { get; set; }             // Default: 48000
int BufferSize { get; set; }             // Default: 256
int Channels { get; set; }               // Default: 1 (mono)
StreamingMode Mode { get; set; }         // Default: RawPCM
bool IsListening { get; }
IReadOnlyList<PeerInfo> ConnectedPeers { get; }
```

## Complete Example: Voice Chat App

```csharp
using MusicEngineEditor.Services;
using NAudio.Wave;

public class VoiceChatApp
{
    private readonly AudioStreamingService _streaming;
    private WaveInEvent _waveIn;
    private WaveOutEvent _waveOut;
    private BufferedWaveProvider _playbackBuffer;

    public VoiceChatApp()
    {
        _streaming = AudioStreamingService.Instance;
        SetupAudioDevices();
        SetupEventHandlers();
    }

    private void SetupAudioDevices()
    {
        // Microphone input
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(48000, 16, 1),
            BufferMilliseconds = 10
        };
        _waveIn.DataAvailable += OnMicrophoneData;

        // Speaker output
        _playbackBuffer = new BufferedWaveProvider(new WaveFormat(48000, 16, 1))
        {
            BufferDuration = TimeSpan.FromMilliseconds(100),
            DiscardOnBufferOverflow = true
        };

        _waveOut = new WaveOutEvent();
        _waveOut.Init(_playbackBuffer);
    }

    private void SetupEventHandlers()
    {
        // Handle incoming calls
        _streaming.CallReceived += async (s, peer) =>
        {
            Console.WriteLine($"Incoming call from {peer.Name}");
            Console.Write("Accept? (y/n): ");

            if (Console.ReadKey().Key == ConsoleKey.Y)
            {
                await _streaming.RespondToCallAsync(peer.PeerId, true);
                StartAudio();
            }
            else
            {
                await _streaming.RespondToCallAsync(peer.PeerId, false);
            }
        };

        // Handle call accepted
        _streaming.CallAccepted += (s, peerId) =>
        {
            Console.WriteLine("Call connected!");
            StartAudio();
        };

        // Handle incoming audio
        _streaming.AudioReceived += (s, e) =>
        {
            // Convert float samples to bytes and play
            byte[] bytes = ConvertToBytes(e.Samples);
            _playbackBuffer.AddSamples(bytes, 0, bytes.Length);
        };

        // Handle text messages
        _streaming.TextMessageReceived += (s, e) =>
        {
            Console.WriteLine($"[{e.PeerId}]: {e.Message}");
        };

        // Handle disconnection
        _streaming.CallEnded += (s, peerId) =>
        {
            Console.WriteLine("Call ended");
            StopAudio();
        };
    }

    public async Task StartAsync()
    {
        // Configure streaming
        _streaming.LocalPort = 9000;
        _streaming.SampleRate = 48000;
        _streaming.BufferSize = 256;
        _streaming.Mode = StreamingMode.RawPCM;

        // Start listening
        await _streaming.StartListeningAsync();

        Console.WriteLine("Voice chat started. Listening on port 9000");
        Console.WriteLine("Commands: /call <ip>, /msg <text>, /end, /quit");

        await RunCommandLoop();
    }

    private async Task RunCommandLoop()
    {
        while (true)
        {
            Console.Write("> ");
            string input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.StartsWith("/call "))
            {
                string ip = input.Substring(6).Trim();
                await CallPeer(ip);
            }
            else if (input.StartsWith("/msg "))
            {
                string msg = input.Substring(5);
                await SendMessage(msg);
            }
            else if (input == "/end")
            {
                await EndCall();
            }
            else if (input == "/quit")
            {
                await _streaming.StopAsync();
                break;
            }
        }
    }

    private async Task CallPeer(string ip)
    {
        try
        {
            Console.WriteLine($"Connecting to {ip}...");
            var peer = await _streaming.ConnectAsync(ip, 9000);

            Console.WriteLine($"Connected. Requesting call...");
            await _streaming.RequestCallAsync(peer.PeerId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to call: {ex.Message}");
        }
    }

    private async Task SendMessage(string message)
    {
        var peers = _streaming.ConnectedPeers;
        foreach (var peer in peers)
        {
            await _streaming.SendTextMessageAsync(peer.PeerId, message);
        }
    }

    private async Task EndCall()
    {
        var peers = _streaming.ConnectedPeers;
        foreach (var peer in peers)
        {
            await _streaming.EndCallAsync(peer.PeerId);
        }
        StopAudio();
    }

    private void StartAudio()
    {
        _waveIn.StartRecording();
        _waveOut.Play();
    }

    private void StopAudio()
    {
        _waveIn.StopRecording();
        _waveOut.Stop();
    }

    private async void OnMicrophoneData(object sender, WaveInEventArgs e)
    {
        // Convert bytes to float samples
        float[] samples = ConvertToFloats(e.Buffer, e.BytesRecorded);

        // Send to remote peer
        await _streaming.SendAudioAsync(samples);
    }

    private float[] ConvertToFloats(byte[] bytes, int length)
    {
        int sampleCount = length / 2;  // 16-bit samples
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(bytes, i * 2);
            samples[i] = sample / 32768f;
        }

        return samples;
    }

    private byte[] ConvertToBytes(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];

        for (int i = 0; i < samples.Length; i++)
        {
            short sample = (short)(samples[i] * 32767f);
            byte[] sampleBytes = BitConverter.GetBytes(sample);
            bytes[i * 2] = sampleBytes[0];
            bytes[i * 2 + 1] = sampleBytes[1];
        }

        return bytes;
    }
}
```

## Command Mode Example

Send MusicEngine commands instead of audio for minimal bandwidth:

```csharp
using MusicEngineEditor.Services;

public class CommandStreamingExample
{
    private readonly AudioStreamingService _streaming;

    public CommandStreamingExample()
    {
        _streaming = AudioStreamingService.Instance;
        _streaming.Mode = StreamingMode.CommandsOnly;

        // Handle incoming commands
        _streaming.CommandReceived += OnCommandReceived;
    }

    public async Task SendNoteOn(int midiNote, float velocity)
    {
        await _streaming.SendCommandAsync("NoteOn", new
        {
            Note = midiNote,
            Velocity = velocity,
            Channel = 0
        });
    }

    public async Task SendNoteOff(int midiNote)
    {
        await _streaming.SendCommandAsync("NoteOff", new
        {
            Note = midiNote,
            Channel = 0
        });
    }

    public async Task SendParameterChange(string parameter, float value)
    {
        await _streaming.SendCommandAsync("SetParameter", new
        {
            Name = parameter,
            Value = value
        });
    }

    public async Task SendBPMChange(float bpm)
    {
        await _streaming.SendCommandAsync("SetBPM", new { BPM = bpm });
    }

    private void OnCommandReceived(object sender, CommandEventArgs e)
    {
        // Route command to MusicEngine
        switch (e.Command)
        {
            case "NoteOn":
                int note = (int)e.Parameters["Note"];
                float velocity = (float)e.Parameters["Velocity"];
                MusicEngine.PlayNote(note, velocity);
                break;

            case "NoteOff":
                MusicEngine.StopNote((int)e.Parameters["Note"]);
                break;

            case "SetParameter":
                ExternalControlService.Instance.SetVariable(
                    (string)e.Parameters["Name"],
                    e.Parameters["Value"]
                );
                break;

            case "SetBPM":
                ExternalControlService.Instance.SetVariable("BPM", e.Parameters["BPM"]);
                break;
        }
    }
}
```

## Bandwidth Comparison

| Mode | Bandwidth (Mono) | Bandwidth (Stereo) | Latency |
|------|------------------|--------------------| --------|
| RawPCM @ 48kHz | ~384 kbps | ~768 kbps | 5-10ms |
| RawPCM @ 44.1kHz | ~353 kbps | ~706 kbps | 5-10ms |
| CommandsOnly | ~1-5 kbps | ~1-5 kbps | Varies |
| Hybrid | ~10-100 kbps | ~20-200 kbps | 5-20ms |

## Latency Optimization

### For Lowest Latency

```csharp
streaming.BufferSize = 128;      // Smaller buffer
streaming.SampleRate = 48000;    // Higher sample rate
streaming.Mode = StreamingMode.RawPCM;

// Network settings
streaming.UdpSendBufferSize = 65536;
streaming.UdpReceiveBufferSize = 65536;
```

### For Stability Over WiFi

```csharp
streaming.BufferSize = 512;      // Larger buffer for jitter
streaming.JitterBufferMs = 50;   // Add jitter buffer
streaming.PacketLossRecovery = true;  // Enable FEC
```

## NAT Traversal

For connections behind NAT, you may need:

1. **Port Forwarding**: Forward UDP/TCP port 9000 on router
2. **STUN Server**: Discover public IP/port
3. **TURN Server**: Relay traffic if direct connection fails

```csharp
// Configure STUN
streaming.StunServer = "stun.l.google.com:19302";

// Get public endpoint
var publicEndpoint = await streaming.GetPublicEndpointAsync();
Console.WriteLine($"My public address: {publicEndpoint}");
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| No audio received | Check firewall allows UDP port |
| High latency | Reduce buffer size |
| Audio crackling | Increase buffer size |
| Connection timeout | Check both peers can reach each other |
| One-way audio | Check NAT settings on both sides |
