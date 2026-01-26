//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Real-time loop recording and playback for live performance with tempo synchronization.

using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MusicEngine.Core.Performance;

#region Enumerations

/// <summary>
/// Loop layer state
/// </summary>
public enum LoopLayerState
{
    /// <summary>Layer is empty, no audio recorded</summary>
    Empty,
    /// <summary>Layer is currently recording</summary>
    Recording,
    /// <summary>Layer is playing back</summary>
    Playing,
    /// <summary>Layer is stopped</summary>
    Stopped,
    /// <summary>Layer is overdubbing (recording while playing)</summary>
    Overdubbing
}

/// <summary>
/// Looper transport state
/// </summary>
public enum LooperState
{
    /// <summary>Looper is stopped with no content</summary>
    Empty,
    /// <summary>Looper is recording the first loop</summary>
    Recording,
    /// <summary>Looper is playing back</summary>
    Playing,
    /// <summary>Looper is stopped with content</summary>
    Stopped,
    /// <summary>Looper is overdubbing</summary>
    Overdubbing,
    /// <summary>Looper is waiting for tempo sync to start</summary>
    WaitingForSync
}

/// <summary>
/// Quantize mode for loop boundaries
/// </summary>
public enum LoopQuantizeMode
{
    /// <summary>No quantization - freeform recording</summary>
    Off,
    /// <summary>Quantize to beat boundaries</summary>
    Beat,
    /// <summary>Quantize to bar boundaries</summary>
    Bar,
    /// <summary>Quantize to 2-bar boundaries</summary>
    TwoBars,
    /// <summary>Quantize to 4-bar boundaries</summary>
    FourBars
}

#endregion

#region Data Classes

/// <summary>
/// Represents a single loop layer with its own audio buffer
/// </summary>
public class LoopLayer
{
    private readonly object _lock = new();
    private float[] _buffer;
    private int _writePosition;
    private int _recordedLength;

    /// <summary>Layer index (0-based)</summary>
    public int Index { get; }

    /// <summary>Layer name for display</summary>
    public string Name { get; set; }

    /// <summary>Current state of this layer</summary>
    public LoopLayerState State { get; private set; } = LoopLayerState.Empty;

    /// <summary>Volume level (0-1)</summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>Pan position (-1 = left, 0 = center, 1 = right)</summary>
    public float Pan { get; set; } = 0.0f;

    /// <summary>Mute state</summary>
    public bool IsMuted { get; set; }

    /// <summary>Solo state</summary>
    public bool IsSolo { get; set; }

    /// <summary>Feedback amount for overdub (0-1)</summary>
    public float Feedback { get; set; } = 1.0f;

    /// <summary>Number of samples recorded in this layer</summary>
    public int RecordedLength
    {
        get { lock (_lock) return _recordedLength; }
    }

    /// <summary>Whether this layer has content</summary>
    public bool HasContent => RecordedLength > 0;

    /// <summary>Number of channels (typically 2 for stereo)</summary>
    public int Channels { get; }

    /// <summary>
    /// Creates a new loop layer
    /// </summary>
    /// <param name="index">Layer index</param>
    /// <param name="maxLengthSamples">Maximum loop length in samples (including all channels)</param>
    /// <param name="channels">Number of audio channels</param>
    public LoopLayer(int index, int maxLengthSamples, int channels = 2)
    {
        Index = index;
        Name = $"Layer {index + 1}";
        Channels = channels;
        _buffer = new float[maxLengthSamples];
        _writePosition = 0;
        _recordedLength = 0;
    }

    /// <summary>
    /// Starts recording on this layer
    /// </summary>
    public void StartRecording()
    {
        lock (_lock)
        {
            State = LoopLayerState.Recording;
            _writePosition = 0;
            _recordedLength = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }
    }

    /// <summary>
    /// Starts overdubbing on this layer (record while playing)
    /// </summary>
    public void StartOverdub()
    {
        lock (_lock)
        {
            if (State == LoopLayerState.Playing || HasContent)
            {
                State = LoopLayerState.Overdubbing;
            }
        }
    }

    /// <summary>
    /// Stops recording and transitions to playing
    /// </summary>
    public void StopRecording()
    {
        lock (_lock)
        {
            if (State == LoopLayerState.Recording)
            {
                _recordedLength = _writePosition;
                State = _recordedLength > 0 ? LoopLayerState.Playing : LoopLayerState.Empty;
            }
            else if (State == LoopLayerState.Overdubbing)
            {
                State = LoopLayerState.Playing;
            }
        }
    }

    /// <summary>
    /// Starts playback
    /// </summary>
    public void Play()
    {
        lock (_lock)
        {
            if (HasContent)
            {
                State = LoopLayerState.Playing;
            }
        }
    }

    /// <summary>
    /// Stops playback
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (State == LoopLayerState.Playing || State == LoopLayerState.Overdubbing)
            {
                State = LoopLayerState.Stopped;
            }
        }
    }

    /// <summary>
    /// Clears all recorded content
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            State = LoopLayerState.Empty;
            _writePosition = 0;
            _recordedLength = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }
    }

    /// <summary>
    /// Writes audio samples to the layer buffer
    /// </summary>
    /// <param name="samples">Audio samples to write</param>
    /// <param name="offset">Offset in samples array</param>
    /// <param name="count">Number of samples to write</param>
    /// <param name="loopLength">Current loop length for overdub wrapping</param>
    public void WriteSamples(float[] samples, int offset, int count, int loopLength)
    {
        lock (_lock)
        {
            if (State != LoopLayerState.Recording && State != LoopLayerState.Overdubbing)
                return;

            for (int i = 0; i < count; i++)
            {
                int targetPos = _writePosition + i;

                // For overdubbing, wrap around to loop length
                if (State == LoopLayerState.Overdubbing && loopLength > 0)
                {
                    targetPos = targetPos % loopLength;
                    // Apply feedback to existing content and add new
                    _buffer[targetPos] = (_buffer[targetPos] * Feedback) + samples[offset + i];
                }
                else if (targetPos < _buffer.Length)
                {
                    _buffer[targetPos] = samples[offset + i];
                }
            }

            if (State == LoopLayerState.Recording)
            {
                _writePosition += count;
                if (_writePosition > _buffer.Length)
                    _writePosition = _buffer.Length;
            }
            else if (State == LoopLayerState.Overdubbing && loopLength > 0)
            {
                _writePosition = (_writePosition + count) % loopLength;
            }
        }
    }

    /// <summary>
    /// Reads audio samples from the layer buffer
    /// </summary>
    /// <param name="buffer">Destination buffer</param>
    /// <param name="offset">Offset in destination buffer</param>
    /// <param name="count">Number of samples to read</param>
    /// <param name="readPosition">Current read position in the loop</param>
    /// <param name="loopLength">Total loop length</param>
    /// <returns>Actual samples read</returns>
    public int ReadSamples(float[] buffer, int offset, int count, int readPosition, int loopLength)
    {
        lock (_lock)
        {
            if (State != LoopLayerState.Playing && State != LoopLayerState.Overdubbing)
                return 0;

            if (_recordedLength == 0 || IsMuted)
                return 0;

            int effectiveLength = Math.Min(_recordedLength, loopLength > 0 ? loopLength : _recordedLength);

            // Calculate pan gains
            float leftGain = Volume * Math.Max(0, 1 - Pan);
            float rightGain = Volume * Math.Max(0, 1 + Pan);

            for (int i = 0; i < count; i += Channels)
            {
                int srcPos = (readPosition + i) % effectiveLength;

                // Ensure we read complete frames
                srcPos = (srcPos / Channels) * Channels;

                if (Channels == 2)
                {
                    if (srcPos + 1 < effectiveLength)
                    {
                        buffer[offset + i] += _buffer[srcPos] * leftGain;
                        buffer[offset + i + 1] += _buffer[srcPos + 1] * rightGain;
                    }
                }
                else
                {
                    // Mono to stereo
                    float sample = _buffer[srcPos] * Volume;
                    buffer[offset + i] += sample * leftGain;
                    if (i + 1 < count)
                        buffer[offset + i + 1] += sample * rightGain;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Sets the loop length (truncates or extends as needed)
    /// </summary>
    /// <param name="length">New length in samples</param>
    public void SetLoopLength(int length)
    {
        lock (_lock)
        {
            if (length > 0 && length < _recordedLength)
            {
                _recordedLength = length;
            }
        }
    }

    /// <summary>
    /// Creates a copy of this layer
    /// </summary>
    public LoopLayer Clone()
    {
        lock (_lock)
        {
            var clone = new LoopLayer(Index, _buffer.Length, Channels)
            {
                Name = Name,
                Volume = Volume,
                Pan = Pan,
                IsMuted = IsMuted,
                IsSolo = IsSolo,
                Feedback = Feedback,
                State = State
            };
            Array.Copy(_buffer, clone._buffer, _buffer.Length);
            clone._writePosition = _writePosition;
            clone._recordedLength = _recordedLength;
            return clone;
        }
    }
}

/// <summary>
/// Undo state snapshot
/// </summary>
internal class LooperUndoState
{
    public List<LoopLayer> Layers { get; }
    public int LoopLength { get; }
    public double Timestamp { get; }

    public LooperUndoState(List<LoopLayer> layers, int loopLength)
    {
        Layers = layers.ConvertAll(l => l.Clone());
        LoopLength = loopLength;
        Timestamp = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
    }
}

#endregion

#region Events

/// <summary>
/// Event arguments for looper state changes
/// </summary>
public class LooperStateChangedEventArgs : EventArgs
{
    /// <summary>Previous state</summary>
    public LooperState OldState { get; }

    /// <summary>New state</summary>
    public LooperState NewState { get; }

    /// <summary>Current loop position in beats</summary>
    public double CurrentBeat { get; }

    public LooperStateChangedEventArgs(LooperState oldState, LooperState newState, double currentBeat)
    {
        OldState = oldState;
        NewState = newState;
        CurrentBeat = currentBeat;
    }
}

/// <summary>
/// Event arguments for layer state changes
/// </summary>
public class LayerStateChangedEventArgs : EventArgs
{
    /// <summary>Layer index</summary>
    public int LayerIndex { get; }

    /// <summary>Previous state</summary>
    public LoopLayerState OldState { get; }

    /// <summary>New state</summary>
    public LoopLayerState NewState { get; }

    public LayerStateChangedEventArgs(int layerIndex, LoopLayerState oldState, LoopLayerState newState)
    {
        LayerIndex = layerIndex;
        OldState = oldState;
        NewState = newState;
    }
}

/// <summary>
/// Event arguments for loop cycle completion
/// </summary>
public class LoopCycleEventArgs : EventArgs
{
    /// <summary>Number of completed cycles</summary>
    public int CycleCount { get; }

    /// <summary>Loop length in samples</summary>
    public int LoopLengthSamples { get; }

    /// <summary>Loop length in beats</summary>
    public double LoopLengthBeats { get; }

    public LoopCycleEventArgs(int cycleCount, int loopLengthSamples, double loopLengthBeats)
    {
        CycleCount = cycleCount;
        LoopLengthSamples = loopLengthSamples;
        LoopLengthBeats = loopLengthBeats;
    }
}

#endregion

/// <summary>
/// Real-time loop recording and playback for live performance.
/// Supports multiple layers, tempo synchronization, overdubbing, and undo.
/// </summary>
public class LiveLooper : ISampleProvider, IDisposable
{
    private readonly WaveFormat _waveFormat;
    private readonly List<LoopLayer> _layers;
    private readonly Stack<LooperUndoState> _undoStack;
    private readonly object _lock = new();

    // Transport state
    private LooperState _state = LooperState.Empty;
    private int _playPosition;
    private int _loopLength;
    private int _cycleCount;

    // Input handling
    private ISampleProvider? _inputSource;
    private float[] _inputBuffer = Array.Empty<float>();
    private bool _inputMonitorEnabled = true;

    // Tempo sync
    private double _bpm = 120.0;
    private int _beatsPerBar = 4;
    private bool _syncToTempo = true;
    private LoopQuantizeMode _quantizeMode = LoopQuantizeMode.Bar;
    private double _pendingQuantizePosition;
    private Action? _pendingAction;

    // Fade handling
    private float _fadeTimeSamples;

    // Maximum configuration
    private readonly int _maxLoopLengthSamples;
    private readonly int _maxLayers;
    private readonly int _maxUndoSteps;

    // Active layer for recording
    private int _activeLayerIndex;

    // Disposed flag
    private bool _disposed;

    #region Properties

    /// <summary>Audio wave format</summary>
    public WaveFormat WaveFormat => _waveFormat;

    /// <summary>Current looper state</summary>
    public LooperState State
    {
        get { lock (_lock) return _state; }
    }

    /// <summary>Current playback position in samples</summary>
    public int PlayPosition
    {
        get { lock (_lock) return _playPosition; }
    }

    /// <summary>Current loop length in samples</summary>
    public int LoopLength
    {
        get { lock (_lock) return _loopLength; }
    }

    /// <summary>Current loop length in seconds</summary>
    public double LoopLengthSeconds => LoopLength / (double)_waveFormat.SampleRate / _waveFormat.Channels;

    /// <summary>Current loop length in beats</summary>
    public double LoopLengthBeats => LoopLengthSeconds * _bpm / 60.0;

    /// <summary>Number of completed loop cycles</summary>
    public int CycleCount
    {
        get { lock (_lock) return _cycleCount; }
    }

    /// <summary>Playback position as 0-1 fraction</summary>
    public double PlaybackFraction
    {
        get
        {
            lock (_lock)
            {
                return _loopLength > 0 ? (double)_playPosition / _loopLength : 0;
            }
        }
    }

    /// <summary>Tempo in BPM for sync calculations</summary>
    public double Bpm
    {
        get => _bpm;
        set => _bpm = Math.Max(20, Math.Min(300, value));
    }

    /// <summary>Beats per bar for tempo sync</summary>
    public int BeatsPerBar
    {
        get => _beatsPerBar;
        set => _beatsPerBar = Math.Max(1, Math.Min(16, value));
    }

    /// <summary>Whether to sync loop boundaries to tempo</summary>
    public bool SyncToTempo
    {
        get => _syncToTempo;
        set => _syncToTempo = value;
    }

    /// <summary>Quantize mode for loop boundaries</summary>
    public LoopQuantizeMode QuantizeMode
    {
        get => _quantizeMode;
        set => _quantizeMode = value;
    }

    /// <summary>Fade time in milliseconds for loop start/end</summary>
    public float FadeTimeMs
    {
        get => _fadeTimeSamples / _waveFormat.SampleRate * 1000f;
        set => _fadeTimeSamples = value * _waveFormat.SampleRate / 1000f;
    }

    /// <summary>Whether to pass input through to output (monitoring)</summary>
    public bool InputMonitorEnabled
    {
        get => _inputMonitorEnabled;
        set => _inputMonitorEnabled = value;
    }

    /// <summary>Master volume for all layers (0-1)</summary>
    public float MasterVolume { get; set; } = 1.0f;

    /// <summary>Number of layers</summary>
    public int LayerCount => _layers.Count;

    /// <summary>Currently active layer for recording</summary>
    public int ActiveLayerIndex
    {
        get => _activeLayerIndex;
        set => _activeLayerIndex = Math.Max(0, Math.Min(_layers.Count - 1, value));
    }

    /// <summary>Number of available undo steps</summary>
    public int UndoCount => _undoStack.Count;

    /// <summary>Whether half-speed playback is enabled</summary>
    public bool HalfSpeed { get; set; }

    /// <summary>Whether reverse playback is enabled</summary>
    public bool Reverse { get; set; }

    #endregion

    #region Events

    /// <summary>Fired when looper state changes</summary>
    public event EventHandler<LooperStateChangedEventArgs>? StateChanged;

    /// <summary>Fired when a layer state changes</summary>
    public event EventHandler<LayerStateChangedEventArgs>? LayerStateChanged;

    /// <summary>Fired when a loop cycle completes</summary>
    public event EventHandler<LoopCycleEventArgs>? LoopCycleCompleted;

    /// <summary>Fired when undo is performed</summary>
    public event EventHandler? UndoPerformed;

    /// <summary>Fired when looper is cleared</summary>
    public event EventHandler? Cleared;

    #endregion

    /// <summary>
    /// Creates a new LiveLooper instance
    /// </summary>
    /// <param name="sampleRate">Audio sample rate (default: from Settings)</param>
    /// <param name="channels">Number of audio channels (default: 2 for stereo)</param>
    /// <param name="maxLoopLengthSeconds">Maximum loop length in seconds (default: 300)</param>
    /// <param name="maxLayers">Maximum number of layers (default: 8)</param>
    /// <param name="maxUndoSteps">Maximum undo history size (default: 10)</param>
    public LiveLooper(
        int? sampleRate = null,
        int? channels = null,
        float maxLoopLengthSeconds = 300f,
        int maxLayers = 8,
        int maxUndoSteps = 10)
    {
        int rate = sampleRate ?? Settings.SampleRate;
        int ch = channels ?? Settings.Channels;
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, ch);

        _maxLoopLengthSamples = (int)(maxLoopLengthSeconds * rate * ch);
        _maxLayers = maxLayers;
        _maxUndoSteps = maxUndoSteps;

        _layers = new List<LoopLayer>(maxLayers);
        for (int i = 0; i < maxLayers; i++)
        {
            _layers.Add(new LoopLayer(i, _maxLoopLengthSamples, ch));
        }

        _undoStack = new Stack<LooperUndoState>(maxUndoSteps);
        _fadeTimeSamples = 10f * rate / 1000f; // Default 10ms fade
    }

    #region Input Source

    /// <summary>
    /// Sets the input source for recording
    /// </summary>
    /// <param name="source">Audio input source</param>
    public void SetInputSource(ISampleProvider? source)
    {
        lock (_lock)
        {
            _inputSource = source;
            if (source != null && _inputBuffer.Length < 4096)
            {
                _inputBuffer = new float[4096];
            }
        }
    }

    #endregion

    #region Transport Controls

    /// <summary>
    /// Starts recording on the active layer.
    /// If this is the first recording, it defines the loop length.
    /// </summary>
    public void Record()
    {
        lock (_lock)
        {
            if (_disposed) return;

            SaveUndoState();

            var oldState = _state;
            var layer = _layers[_activeLayerIndex];
            var oldLayerState = layer.State;

            if (_state == LooperState.Empty)
            {
                // First recording - will define loop length
                _state = LooperState.Recording;
                _playPosition = 0;
                _loopLength = 0;
                layer.StartRecording();
            }
            else if (_state == LooperState.Playing || _state == LooperState.Stopped)
            {
                // Overdub on active layer
                if (_syncToTempo)
                {
                    // Queue overdub to start at next quantize boundary
                    QueueAction(() =>
                    {
                        var l = _layers[_activeLayerIndex];
                        l.StartOverdub();
                        SetState(LooperState.Overdubbing);
                    });
                    _state = LooperState.WaitingForSync;
                }
                else
                {
                    layer.StartOverdub();
                    _state = LooperState.Overdubbing;
                }
            }

            if (layer.State != oldLayerState)
            {
                OnLayerStateChanged(_activeLayerIndex, oldLayerState, layer.State);
            }

            if (_state != oldState)
            {
                OnStateChanged(oldState, _state);
            }
        }
    }

    /// <summary>
    /// Starts or resumes playback
    /// </summary>
    public void Play()
    {
        lock (_lock)
        {
            if (_disposed) return;

            var oldState = _state;

            if (_state == LooperState.Recording)
            {
                // Stop recording and start playback
                var layer = _layers[_activeLayerIndex];
                var oldLayerState = layer.State;
                layer.StopRecording();

                if (layer.RecordedLength > 0)
                {
                    _loopLength = layer.RecordedLength;

                    // Quantize loop length to tempo if enabled
                    if (_syncToTempo)
                    {
                        _loopLength = QuantizeLoopLength(_loopLength);
                    }

                    // Ensure all layers use the same length
                    foreach (var l in _layers)
                    {
                        l.SetLoopLength(_loopLength);
                    }

                    _state = LooperState.Playing;
                }
                else
                {
                    _state = LooperState.Empty;
                }

                OnLayerStateChanged(_activeLayerIndex, oldLayerState, layer.State);
            }
            else if (_state == LooperState.Stopped)
            {
                // Resume playback
                _state = LooperState.Playing;
                foreach (var layer in _layers)
                {
                    if (layer.HasContent)
                    {
                        layer.Play();
                    }
                }
            }
            else if (_state == LooperState.Overdubbing)
            {
                // Stop overdubbing, continue playing
                var layer = _layers[_activeLayerIndex];
                var oldLayerState = layer.State;
                layer.StopRecording();
                _state = LooperState.Playing;
                OnLayerStateChanged(_activeLayerIndex, oldLayerState, layer.State);
            }

            if (_state != oldState)
            {
                OnStateChanged(oldState, _state);
            }
        }
    }

    /// <summary>
    /// Stops playback (pauses at current position)
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_disposed) return;

            var oldState = _state;

            if (_state == LooperState.Recording)
            {
                // Abort recording
                var layer = _layers[_activeLayerIndex];
                var oldLayerState = layer.State;
                layer.Clear();
                _state = LooperState.Empty;
                _loopLength = 0;
                OnLayerStateChanged(_activeLayerIndex, oldLayerState, layer.State);
            }
            else if (_state == LooperState.Playing || _state == LooperState.Overdubbing)
            {
                foreach (var layer in _layers)
                {
                    var oldLayerState = layer.State;
                    layer.Stop();
                    if (layer.State != oldLayerState)
                    {
                        OnLayerStateChanged(layer.Index, oldLayerState, layer.State);
                    }
                }
                _state = LooperState.Stopped;
            }
            else if (_state == LooperState.WaitingForSync)
            {
                _pendingAction = null;
                _state = _loopLength > 0 ? LooperState.Stopped : LooperState.Empty;
            }

            if (_state != oldState)
            {
                OnStateChanged(oldState, _state);
            }
        }
    }

    /// <summary>
    /// Toggles between record/play/overdub based on current state
    /// </summary>
    public void Toggle()
    {
        lock (_lock)
        {
            switch (_state)
            {
                case LooperState.Empty:
                    Record();
                    break;
                case LooperState.Recording:
                    Play();
                    break;
                case LooperState.Playing:
                    Record(); // Start overdub
                    break;
                case LooperState.Overdubbing:
                    Play(); // Stop overdub
                    break;
                case LooperState.Stopped:
                    Play();
                    break;
                case LooperState.WaitingForSync:
                    Stop();
                    break;
            }
        }
    }

    /// <summary>
    /// Clears all layers and resets the looper
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            if (_disposed) return;

            SaveUndoState();

            var oldState = _state;

            foreach (var layer in _layers)
            {
                var oldLayerState = layer.State;
                layer.Clear();
                if (layer.State != oldLayerState)
                {
                    OnLayerStateChanged(layer.Index, oldLayerState, layer.State);
                }
            }

            _state = LooperState.Empty;
            _playPosition = 0;
            _loopLength = 0;
            _cycleCount = 0;
            _pendingAction = null;

            if (_state != oldState)
            {
                OnStateChanged(oldState, _state);
            }

            Cleared?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Clears a specific layer
    /// </summary>
    /// <param name="layerIndex">Layer index to clear</param>
    public void ClearLayer(int layerIndex)
    {
        lock (_lock)
        {
            if (layerIndex < 0 || layerIndex >= _layers.Count) return;

            SaveUndoState();

            var layer = _layers[layerIndex];
            var oldState = layer.State;
            layer.Clear();

            if (layer.State != oldState)
            {
                OnLayerStateChanged(layerIndex, oldState, layer.State);
            }

            // Check if all layers are empty
            bool allEmpty = true;
            foreach (var l in _layers)
            {
                if (l.HasContent)
                {
                    allEmpty = false;
                    break;
                }
            }

            if (allEmpty)
            {
                var oldLooperState = _state;
                _state = LooperState.Empty;
                _loopLength = 0;
                if (_state != oldLooperState)
                {
                    OnStateChanged(oldLooperState, _state);
                }
            }
        }
    }

    /// <summary>
    /// Undoes the last recording/clear operation
    /// </summary>
    public void Undo()
    {
        lock (_lock)
        {
            if (_undoStack.Count == 0 || _disposed) return;

            var undoState = _undoStack.Pop();

            // Restore layer states
            for (int i = 0; i < _layers.Count && i < undoState.Layers.Count; i++)
            {
                var oldState = _layers[i].State;
                _layers[i] = undoState.Layers[i].Clone();
                if (_layers[i].State != oldState)
                {
                    OnLayerStateChanged(i, oldState, _layers[i].State);
                }
            }

            var oldLooperState = _state;
            _loopLength = undoState.LoopLength;

            // Determine new state based on restored content
            bool hasContent = false;
            foreach (var layer in _layers)
            {
                if (layer.HasContent)
                {
                    hasContent = true;
                    break;
                }
            }

            _state = hasContent ? LooperState.Stopped : LooperState.Empty;

            if (_state != oldLooperState)
            {
                OnStateChanged(oldLooperState, _state);
            }

            UndoPerformed?.Invoke(this, EventArgs.Empty);
        }
    }

    #endregion

    #region Layer Control

    /// <summary>
    /// Gets a layer by index
    /// </summary>
    /// <param name="index">Layer index</param>
    /// <returns>The layer, or null if index is invalid</returns>
    public LoopLayer? GetLayer(int index)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _layers.Count) return null;
            return _layers[index];
        }
    }

    /// <summary>
    /// Sets volume for a layer
    /// </summary>
    /// <param name="layerIndex">Layer index</param>
    /// <param name="volume">Volume (0-1)</param>
    public void SetLayerVolume(int layerIndex, float volume)
    {
        lock (_lock)
        {
            if (layerIndex >= 0 && layerIndex < _layers.Count)
            {
                _layers[layerIndex].Volume = Math.Clamp(volume, 0f, 1f);
            }
        }
    }

    /// <summary>
    /// Sets pan for a layer
    /// </summary>
    /// <param name="layerIndex">Layer index</param>
    /// <param name="pan">Pan position (-1 to 1)</param>
    public void SetLayerPan(int layerIndex, float pan)
    {
        lock (_lock)
        {
            if (layerIndex >= 0 && layerIndex < _layers.Count)
            {
                _layers[layerIndex].Pan = Math.Clamp(pan, -1f, 1f);
            }
        }
    }

    /// <summary>
    /// Toggles mute for a layer
    /// </summary>
    /// <param name="layerIndex">Layer index</param>
    public void ToggleLayerMute(int layerIndex)
    {
        lock (_lock)
        {
            if (layerIndex >= 0 && layerIndex < _layers.Count)
            {
                _layers[layerIndex].IsMuted = !_layers[layerIndex].IsMuted;
            }
        }
    }

    /// <summary>
    /// Toggles solo for a layer
    /// </summary>
    /// <param name="layerIndex">Layer index</param>
    public void ToggleLayerSolo(int layerIndex)
    {
        lock (_lock)
        {
            if (layerIndex >= 0 && layerIndex < _layers.Count)
            {
                _layers[layerIndex].IsSolo = !_layers[layerIndex].IsSolo;
            }
        }
    }

    /// <summary>
    /// Sets the overdub feedback amount for a layer
    /// </summary>
    /// <param name="layerIndex">Layer index</param>
    /// <param name="feedback">Feedback amount (0-1)</param>
    public void SetLayerFeedback(int layerIndex, float feedback)
    {
        lock (_lock)
        {
            if (layerIndex >= 0 && layerIndex < _layers.Count)
            {
                _layers[layerIndex].Feedback = Math.Clamp(feedback, 0f, 1f);
            }
        }
    }

    #endregion

    #region Tempo Sync

    /// <summary>
    /// Calculates samples per beat at current tempo
    /// </summary>
    private int SamplesPerBeat => (int)(_waveFormat.SampleRate * 60.0 / _bpm * _waveFormat.Channels);

    /// <summary>
    /// Calculates samples per bar at current tempo
    /// </summary>
    private int SamplesPerBar => SamplesPerBeat * _beatsPerBar;

    /// <summary>
    /// Quantizes loop length to nearest tempo boundary
    /// </summary>
    private int QuantizeLoopLength(int lengthSamples)
    {
        int quantizeUnit = _quantizeMode switch
        {
            LoopQuantizeMode.Beat => SamplesPerBeat,
            LoopQuantizeMode.Bar => SamplesPerBar,
            LoopQuantizeMode.TwoBars => SamplesPerBar * 2,
            LoopQuantizeMode.FourBars => SamplesPerBar * 4,
            _ => 1
        };

        if (quantizeUnit <= 1) return lengthSamples;

        int bars = (lengthSamples + quantizeUnit / 2) / quantizeUnit;
        return Math.Max(quantizeUnit, bars * quantizeUnit);
    }

    /// <summary>
    /// Queues an action to execute at the next quantize boundary
    /// </summary>
    private void QueueAction(Action action)
    {
        _pendingAction = action;

        int quantizeUnit = _quantizeMode switch
        {
            LoopQuantizeMode.Beat => SamplesPerBeat,
            LoopQuantizeMode.Bar => SamplesPerBar,
            LoopQuantizeMode.TwoBars => SamplesPerBar * 2,
            LoopQuantizeMode.FourBars => SamplesPerBar * 4,
            _ => 1
        };

        if (quantizeUnit > 1 && _loopLength > 0)
        {
            int currentPos = _playPosition % _loopLength;
            int nextBoundary = ((currentPos / quantizeUnit) + 1) * quantizeUnit;
            _pendingQuantizePosition = nextBoundary;
        }
        else
        {
            _pendingQuantizePosition = 0;
        }
    }

    /// <summary>
    /// Checks and executes pending quantized actions
    /// </summary>
    private void CheckPendingAction(int position)
    {
        if (_pendingAction != null && _loopLength > 0)
        {
            int wrappedPos = position % _loopLength;
            if (wrappedPos >= _pendingQuantizePosition || wrappedPos < _playPosition % _loopLength)
            {
                var action = _pendingAction;
                _pendingAction = null;
                action();
            }
        }
    }

    /// <summary>
    /// Syncs the looper to an external tempo source
    /// </summary>
    /// <param name="bpm">Current tempo</param>
    /// <param name="beat">Current beat position</param>
    public void SyncToExternalTempo(double bpm, double beat)
    {
        lock (_lock)
        {
            _bpm = bpm;
            // Optionally sync play position to beat
            if (_syncToTempo && _loopLength > 0)
            {
                int samplesPerBeat = SamplesPerBeat;
                int targetPosition = (int)(beat * samplesPerBeat) % _loopLength;
                _playPosition = targetPosition;
            }
        }
    }

    #endregion

    #region ISampleProvider Implementation

    /// <summary>
    /// Reads audio samples from the looper
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

            // Clear output buffer
            Array.Clear(buffer, offset, count);

            // Read from input source if available
            float[] inputSamples = Array.Empty<float>();
            int inputRead = 0;

            if (_inputSource != null)
            {
                if (_inputBuffer.Length < count)
                {
                    _inputBuffer = new float[count];
                }

                inputRead = _inputSource.Read(_inputBuffer, 0, count);
                inputSamples = _inputBuffer;

                // Pass through input if monitoring enabled
                if (_inputMonitorEnabled && inputRead > 0)
                {
                    for (int i = 0; i < inputRead; i++)
                    {
                        buffer[offset + i] += inputSamples[i];
                    }
                }
            }

            // Handle recording
            if ((_state == LooperState.Recording || _state == LooperState.Overdubbing) && inputRead > 0)
            {
                var activeLayer = _layers[_activeLayerIndex];
                activeLayer.WriteSamples(inputSamples, 0, inputRead, _loopLength);
            }

            // Handle playback
            if (_state == LooperState.Playing || _state == LooperState.Overdubbing)
            {
                if (_loopLength > 0)
                {
                    // Check for any solo layers
                    bool hasSolo = false;
                    foreach (var layer in _layers)
                    {
                        if (layer.IsSolo && layer.HasContent)
                        {
                            hasSolo = true;
                            break;
                        }
                    }

                    // Read from all active layers
                    foreach (var layer in _layers)
                    {
                        // Skip if soloed and this layer is not solo
                        if (hasSolo && !layer.IsSolo) continue;

                        layer.ReadSamples(buffer, offset, count, _playPosition, _loopLength);
                    }

                    // Apply master volume and fades
                    ApplyMasterVolumeAndFades(buffer, offset, count);

                    // Update play position
                    int oldPosition = _playPosition;

                    if (HalfSpeed)
                    {
                        _playPosition += count / 2;
                    }
                    else if (Reverse)
                    {
                        _playPosition -= count;
                        if (_playPosition < 0)
                        {
                            _playPosition += _loopLength;
                            _cycleCount++;
                            OnLoopCycleCompleted();
                        }
                    }
                    else
                    {
                        _playPosition += count;
                    }

                    // Wrap at loop boundary
                    if (_playPosition >= _loopLength)
                    {
                        _playPosition = _playPosition % _loopLength;
                        _cycleCount++;
                        OnLoopCycleCompleted();
                    }

                }
            }

            // Check for pending quantized actions (must be outside Playing/Overdubbing block for WaitingForSync state)
            if (_state == LooperState.WaitingForSync)
            {
                CheckPendingAction(_playPosition);
            }

            return count;
        }
    }

    /// <summary>
    /// Applies master volume and fade in/out at loop boundaries
    /// </summary>
    private void ApplyMasterVolumeAndFades(float[] buffer, int offset, int count)
    {
        if (_loopLength <= 0) return;

        int fadeSamples = (int)_fadeTimeSamples;
        int channels = _waveFormat.Channels;

        for (int i = 0; i < count; i++)
        {
            int samplePos = (_playPosition + i) % _loopLength;
            float fadeGain = 1.0f;

            // Fade in at loop start
            if (samplePos < fadeSamples)
            {
                fadeGain = (float)samplePos / fadeSamples;
            }
            // Fade out at loop end
            else if (samplePos > _loopLength - fadeSamples)
            {
                fadeGain = (float)(_loopLength - samplePos) / fadeSamples;
            }

            buffer[offset + i] *= MasterVolume * fadeGain;
        }
    }

    #endregion

    #region State Management

    private void SaveUndoState()
    {
        if (_undoStack.Count >= _maxUndoSteps)
        {
            // Remove oldest state (convert to list, remove first, recreate stack)
            var list = new List<LooperUndoState>(_undoStack);
            list.RemoveAt(list.Count - 1);
            _undoStack.Clear();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                _undoStack.Push(list[i]);
            }
        }

        _undoStack.Push(new LooperUndoState(_layers, _loopLength));
    }

    private void SetState(LooperState newState)
    {
        var oldState = _state;
        _state = newState;
        if (oldState != newState)
        {
            OnStateChanged(oldState, newState);
        }
    }

    private void OnStateChanged(LooperState oldState, LooperState newState)
    {
        StateChanged?.Invoke(this, new LooperStateChangedEventArgs(
            oldState, newState, LoopLengthBeats * PlaybackFraction));
    }

    private void OnLayerStateChanged(int layerIndex, LoopLayerState oldState, LoopLayerState newState)
    {
        LayerStateChanged?.Invoke(this, new LayerStateChangedEventArgs(layerIndex, oldState, newState));
    }

    private void OnLoopCycleCompleted()
    {
        LoopCycleCompleted?.Invoke(this, new LoopCycleEventArgs(_cycleCount, _loopLength, LoopLengthBeats));
    }

    #endregion

    #region Parameter Control

    /// <summary>
    /// Sets a looper parameter by name
    /// </summary>
    /// <param name="name">Parameter name</param>
    /// <param name="value">Parameter value</param>
    public void SetParameter(string name, float value)
    {
        switch (name.ToLowerInvariant())
        {
            case "mastervolume":
            case "volume":
                MasterVolume = Math.Clamp(value, 0f, 1f);
                break;
            case "bpm":
            case "tempo":
                Bpm = value;
                break;
            case "fadetime":
            case "fadetimems":
                FadeTimeMs = Math.Max(0, value);
                break;
            case "inputmonitor":
                InputMonitorEnabled = value > 0.5f;
                break;
            case "synctotempo":
                SyncToTempo = value > 0.5f;
                break;
            case "halfspeed":
                HalfSpeed = value > 0.5f;
                break;
            case "reverse":
                Reverse = value > 0.5f;
                break;
            case "activelayer":
                ActiveLayerIndex = (int)value;
                break;
            case "quantizemode":
                QuantizeMode = (LoopQuantizeMode)(int)Math.Clamp(value, 0, 4);
                break;
        }
    }

    /// <summary>
    /// Gets a looper parameter by name
    /// </summary>
    /// <param name="name">Parameter name</param>
    /// <returns>Parameter value</returns>
    public float GetParameter(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "mastervolume" or "volume" => MasterVolume,
            "bpm" or "tempo" => (float)Bpm,
            "fadetime" or "fadetimems" => FadeTimeMs,
            "inputmonitor" => InputMonitorEnabled ? 1f : 0f,
            "synctotempo" => SyncToTempo ? 1f : 0f,
            "halfspeed" => HalfSpeed ? 1f : 0f,
            "reverse" => Reverse ? 1f : 0f,
            "activelayer" => ActiveLayerIndex,
            "quantizemode" => (float)QuantizeMode,
            "looplength" => LoopLength,
            "looplengthbeats" => (float)LoopLengthBeats,
            "playposition" => PlayPosition,
            "cyclecount" => CycleCount,
            _ => 0f
        };
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the looper and releases resources
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            Stop();
            foreach (var layer in _layers)
            {
                layer.Clear();
            }
            _layers.Clear();
            _undoStack.Clear();
            _inputSource = null;
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}
