// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: IPC server for C++ editors to poll state and receive note events.

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MusicEngine.Core;

namespace MusicEngine.Scripting;

/// <summary>
/// Named pipe IPC server for polling state and receiving editor events.
/// </summary>
public sealed class EngineIpcServer : IDisposable
{
    /// <summary>
    /// Pipe name for state requests.
    /// </summary>
    public const string StatePipeName = "MusicEngine.State";
    /// <summary>
    /// Pipe name for event stream.
    /// </summary>
    public const string EventsPipeName = "MusicEngine.Events";

    private readonly IEngineScriptInterface _engine;
    private readonly object _eventWriteLock = new();
    private readonly JsonSerializerOptions _jsonOptions = new();
    private CancellationTokenSource? _cts;
    private Task? _stateTask;
    private Task? _eventsTask;

    /// <summary>
    /// Create an IPC server for the script engine.
    /// </summary>
    public EngineIpcServer(IEngineScriptInterface engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>
    /// Start the IPC server loops.
    /// </summary>
    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        _stateTask = Task.Run(() => RunStateLoopAsync(_cts.Token));
        _eventsTask = Task.Run(() => RunEventsLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Stop the IPC server loops.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts == null) return;
        _cts.Cancel();
        try
        {
            if (_stateTask != null)
            {
                await _stateTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        try
        {
            if (_eventsTask != null)
            {
                await _eventsTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        _cts.Dispose();
        _cts = null;
    }

    /// <summary>
    /// Stop the server and release resources.
    /// </summary>
    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    private async Task RunStateLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(StatePipeName, PipeDirection.InOut, 1,
#if WINDOWS
                PipeTransmissionMode.Message,
#else
                PipeTransmissionMode.Byte,
#endif
                PipeOptions.Asynchronous);
            await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);

            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true)
            {
                AutoFlush = true
            };

            while (!token.IsCancellationRequested && pipe.IsConnected)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;
                await HandleStateCommandAsync(line, writer).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleStateCommandAsync(string line, StreamWriter writer)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            if (!doc.RootElement.TryGetProperty("cmd", out var cmdProp))
            {
                await WriteErrorAsync(writer, "Missing cmd.").ConfigureAwait(false);
                return;
            }

            var cmd = cmdProp.GetString() ?? string.Empty;
            switch (cmd)
            {
                case "get_state":
                {
                    var state = _engine.GetStateSnapshot();
                    var payload = new { ok = true, state };
                    await WriteJsonAsync(writer, payload).ConfigureAwait(false);
                    break;
                }
                case "set_editor_mode":
                {
                    if (!doc.RootElement.TryGetProperty("enabled", out var enabledProp))
                    {
                        await WriteErrorAsync(writer, "Missing enabled.").ConfigureAwait(false);
                        return;
                    }
                    var enabled = enabledProp.GetBoolean();
                    _engine.SetEditorMode(enabled);
                    await WriteJsonAsync(writer, new { ok = true }).ConfigureAwait(false);
                    break;
                }
                case "refresh_script":
                {
                    var executed = await _engine.Host.RefreshMainScriptsAsync().ConfigureAwait(false);
                    await WriteJsonAsync(writer, new { ok = true, executed }).ConfigureAwait(false);
                    break;
                }
                case "sleep":
                {
                    _engine.Sleep();
                    await WriteJsonAsync(writer, new { ok = true }).ConfigureAwait(false);
                    break;
                }
                case "wake":
                {
                    _engine.Wake();
                    await WriteJsonAsync(writer, new { ok = true }).ConfigureAwait(false);
                    break;
                }
                default:
                    await WriteErrorAsync(writer, $"Unknown cmd: {cmd}").ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(writer, $"Bad request: {ex.Message}").ConfigureAwait(false);
        }
    }

    private async Task RunEventsLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(EventsPipeName, PipeDirection.Out, 1,
#if WINDOWS
                PipeTransmissionMode.Message,
#else
                PipeTransmissionMode.Byte,
#endif
                PipeOptions.Asynchronous);
            await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);

            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true)
            {
                AutoFlush = true
            };

            void OnPatternNote(PatternNoteEventInfo info)
            {
                WriteEvent(writer, new
                {
                    type = "pattern_note",
                    patternId = info.PatternId,
                    note = info.Note,
                    velocity = info.Velocity,
                    isOn = info.IsOn,
                    timestampUtc = info.TimestampUtc
                });
            }

            void OnMidiNote(MidiNoteEventInfo info)
            {
                WriteEvent(writer, new
                {
                    type = "midi_note",
                    deviceIndex = info.DeviceIndex,
                    note = info.Note,
                    velocity = info.Velocity,
                    isOn = info.IsOn,
                    timestampUtc = info.TimestampUtc
                });
            }

            void OnMidiDeviceActive(int deviceIndex)
            {
                WriteEvent(writer, new
                {
                    type = "midi_device_active",
                    deviceIndex,
                    timestampUtc = DateTime.UtcNow
                });
            }

            _engine.EditorPatternNote += OnPatternNote;
            _engine.EditorMidiNote += OnMidiNote;
            _engine.EditorMidiDeviceActive += OnMidiDeviceActive;

            try
            {
                while (!token.IsCancellationRequested && pipe.IsConnected)
                {
                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _engine.EditorPatternNote -= OnPatternNote;
                _engine.EditorMidiNote -= OnMidiNote;
                _engine.EditorMidiDeviceActive -= OnMidiDeviceActive;
            }
        }
    }

    private void WriteEvent(StreamWriter writer, object payload)
    {
        lock (_eventWriteLock)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload, _jsonOptions);
                writer.WriteLine(json);
                writer.Flush();
            }
            catch (IOException)
            {
            }
        }
    }

    private async Task WriteJsonAsync(StreamWriter writer, object payload)
    {
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        await writer.WriteLineAsync(json).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    private Task WriteErrorAsync(StreamWriter writer, string message)
    {
        return WriteJsonAsync(writer, new { ok = false, error = message });
    }
}
