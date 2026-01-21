//Engine License (MEL) – Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Arpeggiator for creating automatic note patterns from held notes.


using System;
using System.Collections.Generic;
using System.Linq;


namespace MusicEngine.Core;


/// <summary>
/// Arpeggiator pattern types
/// </summary>
public enum ArpPattern
{
    /// <summary>Play notes in ascending order</summary>
    Up,
    /// <summary>Play notes in descending order</summary>
    Down,
    /// <summary>Play notes up then down</summary>
    UpDown,
    /// <summary>Play notes down then up</summary>
    DownUp,
    /// <summary>Play notes in random order</summary>
    Random,
    /// <summary>Play notes in the order they were pressed</summary>
    Order,
    /// <summary>Play notes in reverse order of pressing</summary>
    OrderReverse,
    /// <summary>Play root note, then alternating high/low</summary>
    Converge,
    /// <summary>Play from outside notes inward</summary>
    Diverge,
    /// <summary>Play two notes at once, moving up</summary>
    ThumbUp,
    /// <summary>Play two notes at once, moving down</summary>
    ThumbDown,
    /// <summary>Pinky pattern - alternating from top</summary>
    PinkyUp,
    /// <summary>Pinky pattern - alternating from bottom</summary>
    PinkyDown
}


/// <summary>
/// Note duration options for the arpeggiator
/// </summary>
public enum ArpNoteDuration
{
    /// <summary>Whole note (4 beats)</summary>
    Whole = 4,
    /// <summary>Half note (2 beats)</summary>
    Half = 2,
    /// <summary>Quarter note (1 beat)</summary>
    Quarter = 1,
    /// <summary>Eighth note (0.5 beats)</summary>
    Eighth = 8,
    /// <summary>Sixteenth note (0.25 beats)</summary>
    Sixteenth = 16,
    /// <summary>Thirty-second note (0.125 beats)</summary>
    ThirtySecond = 32,
    /// <summary>Triplet eighth (1/3 beat)</summary>
    TripletEighth = 12,
    /// <summary>Triplet sixteenth (1/6 beat)</summary>
    TripletSixteenth = 24,
    /// <summary>Dotted eighth (0.75 beats)</summary>
    DottedEighth = 6,
    /// <summary>Dotted sixteenth (0.375 beats)</summary>
    DottedSixteenth = 48
}


/// <summary>
/// Arpeggiator that creates automatic note patterns from held notes.
/// Can be connected to a synth and sequencer for real-time arpeggiation.
/// </summary>
public class Arpeggiator : IDisposable
{
    private readonly ISynth _synth;
    private readonly List<int> _heldNotes = new();
    private readonly List<int> _orderNotes = new(); // Notes in order of pressing
    private readonly List<int> _currentPattern = new();
    private readonly Random _random = new();
    private readonly object _lock = new();

    private int _patternIndex;
    private bool _ascending = true; // For UpDown/DownUp patterns
    private int _lastPlayedNote = -1;
    private double _lastTriggerBeat;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the arpeggiator pattern
    /// </summary>
    public ArpPattern Pattern { get; set; } = ArpPattern.Up;

    /// <summary>
    /// Gets or sets the note rate (how often notes trigger)
    /// </summary>
    public ArpNoteDuration Rate { get; set; } = ArpNoteDuration.Sixteenth;

    /// <summary>
    /// Gets or sets the octave range (0 = same octave, 1 = +1 octave, etc.)
    /// </summary>
    public int OctaveRange { get; set; } = 1;

    /// <summary>
    /// Gets or sets the gate percentage (0-1, how long each note plays relative to rate)
    /// </summary>
    public float Gate { get; set; } = 0.8f;

    /// <summary>
    /// Gets or sets the velocity (0-127, or -1 to use original velocity)
    /// </summary>
    public int Velocity { get; set; } = -1;

    /// <summary>
    /// Gets or sets the swing amount (0-1, 0 = no swing)
    /// </summary>
    public float Swing { get; set; } = 0f;

    /// <summary>
    /// Gets or sets whether the arpeggiator is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to latch notes (keep playing after release)
    /// </summary>
    public bool Latch { get; set; } = false;

    /// <summary>
    /// Gets whether there are notes to arpeggiate
    /// </summary>
    public bool HasNotes
    {
        get
        {
            lock (_lock)
            {
                return _heldNotes.Count > 0;
            }
        }
    }

    /// <summary>
    /// Gets the current note count
    /// </summary>
    public int NoteCount
    {
        get
        {
            lock (_lock)
            {
                return _heldNotes.Count;
            }
        }
    }

    /// <summary>
    /// Fired when a note is triggered by the arpeggiator
    /// </summary>
    public event EventHandler<ArpNoteEventArgs>? NotePlayed;

    /// <summary>
    /// Creates an arpeggiator connected to a synth
    /// </summary>
    public Arpeggiator(ISynth synth)
    {
        _synth = synth ?? throw new ArgumentNullException(nameof(synth));
    }

    /// <summary>
    /// Add a note to the arpeggiator (called on NoteOn)
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        lock (_lock)
        {
            if (!_heldNotes.Contains(note))
            {
                _heldNotes.Add(note);
                _orderNotes.Add(note);
                RebuildPattern();
            }
        }
    }

    /// <summary>
    /// Remove a note from the arpeggiator (called on NoteOff)
    /// </summary>
    public void NoteOff(int note)
    {
        if (Latch) return; // Don't remove notes in latch mode

        lock (_lock)
        {
            _heldNotes.Remove(note);
            _orderNotes.Remove(note);

            if (_heldNotes.Count == 0)
            {
                _patternIndex = 0;
                _ascending = true;
                if (_lastPlayedNote >= 0)
                {
                    _synth.NoteOff(_lastPlayedNote);
                    _lastPlayedNote = -1;
                }
            }
            else
            {
                RebuildPattern();
            }
        }
    }

    /// <summary>
    /// Clear all held notes
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            if (_lastPlayedNote >= 0)
            {
                _synth.NoteOff(_lastPlayedNote);
                _lastPlayedNote = -1;
            }
            _heldNotes.Clear();
            _orderNotes.Clear();
            _currentPattern.Clear();
            _patternIndex = 0;
            _ascending = true;
        }
    }

    /// <summary>
    /// Process the arpeggiator at the given beat position
    /// </summary>
    public void Process(double currentBeat, double bpm)
    {
        if (!Enabled || _disposed) return;

        lock (_lock)
        {
            if (_currentPattern.Count == 0) return;

            // Calculate beats per note based on rate
            double beatsPerNote = GetBeatsPerNote();

            // Apply swing to even beats
            double swingOffset = 0;
            int beatIndex = (int)(currentBeat / beatsPerNote);
            if (beatIndex % 2 == 1 && Swing > 0)
            {
                swingOffset = beatsPerNote * Swing * 0.5;
            }

            double adjustedBeat = currentBeat - swingOffset;
            double triggerBeat = Math.Floor(adjustedBeat / beatsPerNote) * beatsPerNote + swingOffset;

            // Check if we should trigger a new note
            if (triggerBeat > _lastTriggerBeat || _lastTriggerBeat == 0)
            {
                _lastTriggerBeat = triggerBeat;
                TriggerNextNote(bpm);
            }
        }
    }

    /// <summary>
    /// Get the beats per note based on current rate
    /// </summary>
    private double GetBeatsPerNote()
    {
        return Rate switch
        {
            ArpNoteDuration.Whole => 4.0,
            ArpNoteDuration.Half => 2.0,
            ArpNoteDuration.Quarter => 1.0,
            ArpNoteDuration.Eighth => 0.5,
            ArpNoteDuration.Sixteenth => 0.25,
            ArpNoteDuration.ThirtySecond => 0.125,
            ArpNoteDuration.TripletEighth => 1.0 / 3.0,
            ArpNoteDuration.TripletSixteenth => 1.0 / 6.0,
            ArpNoteDuration.DottedEighth => 0.75,
            ArpNoteDuration.DottedSixteenth => 0.375,
            _ => 0.25
        };
    }

    /// <summary>
    /// Trigger the next note in the pattern
    /// </summary>
    private void TriggerNextNote(double bpm)
    {
        if (_currentPattern.Count == 0) return;

        // Stop previous note
        if (_lastPlayedNote >= 0)
        {
            _synth.NoteOff(_lastPlayedNote);
        }

        // Get next note based on pattern
        int note = GetNextNote();
        int velocity = Velocity >= 0 ? Velocity : 100;

        // Play the note
        _synth.NoteOn(note, velocity);
        _lastPlayedNote = note;

        // Emit event
        NotePlayed?.Invoke(this, new ArpNoteEventArgs(note, velocity));

        // Schedule note off based on gate
        double beatsPerNote = GetBeatsPerNote();
        double noteDurationMs = (beatsPerNote * Gate * 60000.0) / bpm;

        int noteToOff = note;
        _ = Task.Run(async () =>
        {
            await Task.Delay((int)noteDurationMs);
            lock (_lock)
            {
                if (_lastPlayedNote == noteToOff)
                {
                    _synth.NoteOff(noteToOff);
                    _lastPlayedNote = -1;
                }
            }
        });
    }

    /// <summary>
    /// Get the next note based on current pattern
    /// </summary>
    private int GetNextNote()
    {
        int note;

        switch (Pattern)
        {
            case ArpPattern.Up:
            case ArpPattern.Down:
            case ArpPattern.Order:
            case ArpPattern.OrderReverse:
                note = _currentPattern[_patternIndex];
                _patternIndex = (_patternIndex + 1) % _currentPattern.Count;
                break;

            case ArpPattern.UpDown:
                note = _currentPattern[_patternIndex];
                if (_ascending)
                {
                    _patternIndex++;
                    if (_patternIndex >= _currentPattern.Count)
                    {
                        _patternIndex = _currentPattern.Count - 2;
                        _ascending = false;
                        if (_patternIndex < 0) _patternIndex = 0;
                    }
                }
                else
                {
                    _patternIndex--;
                    if (_patternIndex < 0)
                    {
                        _patternIndex = 1;
                        _ascending = true;
                        if (_patternIndex >= _currentPattern.Count) _patternIndex = 0;
                    }
                }
                break;

            case ArpPattern.DownUp:
                note = _currentPattern[_patternIndex];
                if (!_ascending)
                {
                    _patternIndex++;
                    if (_patternIndex >= _currentPattern.Count)
                    {
                        _patternIndex = _currentPattern.Count - 2;
                        _ascending = true;
                        if (_patternIndex < 0) _patternIndex = 0;
                    }
                }
                else
                {
                    _patternIndex--;
                    if (_patternIndex < 0)
                    {
                        _patternIndex = 1;
                        _ascending = false;
                        if (_patternIndex >= _currentPattern.Count) _patternIndex = 0;
                    }
                }
                break;

            case ArpPattern.Random:
                note = _currentPattern[_random.Next(_currentPattern.Count)];
                break;

            case ArpPattern.Converge:
            case ArpPattern.Diverge:
            case ArpPattern.ThumbUp:
            case ArpPattern.ThumbDown:
            case ArpPattern.PinkyUp:
            case ArpPattern.PinkyDown:
                note = _currentPattern[_patternIndex];
                _patternIndex = (_patternIndex + 1) % _currentPattern.Count;
                break;

            default:
                note = _currentPattern[0];
                break;
        }

        return note;
    }

    /// <summary>
    /// Rebuild the pattern based on held notes and settings
    /// </summary>
    private void RebuildPattern()
    {
        _currentPattern.Clear();

        if (_heldNotes.Count == 0) return;

        // Build base note list with octave expansion
        var notes = new List<int>();
        var sortedNotes = _heldNotes.OrderBy(n => n).ToList();

        for (int octave = 0; octave <= OctaveRange; octave++)
        {
            foreach (var note in sortedNotes)
            {
                int transposedNote = note + (octave * 12);
                if (transposedNote <= 127)
                {
                    notes.Add(transposedNote);
                }
            }
        }

        // Apply pattern ordering
        switch (Pattern)
        {
            case ArpPattern.Up:
                _currentPattern.AddRange(notes);
                break;

            case ArpPattern.Down:
                notes.Reverse();
                _currentPattern.AddRange(notes);
                break;

            case ArpPattern.UpDown:
                _currentPattern.AddRange(notes);
                _ascending = true;
                break;

            case ArpPattern.DownUp:
                notes.Reverse();
                _currentPattern.AddRange(notes);
                _ascending = false;
                break;

            case ArpPattern.Random:
                _currentPattern.AddRange(notes);
                break;

            case ArpPattern.Order:
                // Use order of pressing with octave expansion
                for (int octave = 0; octave <= OctaveRange; octave++)
                {
                    foreach (var note in _orderNotes)
                    {
                        int transposedNote = note + (octave * 12);
                        if (transposedNote <= 127)
                        {
                            _currentPattern.Add(transposedNote);
                        }
                    }
                }
                break;

            case ArpPattern.OrderReverse:
                for (int octave = OctaveRange; octave >= 0; octave--)
                {
                    for (int i = _orderNotes.Count - 1; i >= 0; i--)
                    {
                        int transposedNote = _orderNotes[i] + (octave * 12);
                        if (transposedNote <= 127)
                        {
                            _currentPattern.Add(transposedNote);
                        }
                    }
                }
                break;

            case ArpPattern.Converge:
                BuildConvergePattern(notes);
                break;

            case ArpPattern.Diverge:
                BuildDivergePattern(notes);
                break;

            case ArpPattern.ThumbUp:
                BuildThumbPattern(notes, true);
                break;

            case ArpPattern.ThumbDown:
                BuildThumbPattern(notes, false);
                break;

            case ArpPattern.PinkyUp:
                BuildPinkyPattern(notes, true);
                break;

            case ArpPattern.PinkyDown:
                BuildPinkyPattern(notes, false);
                break;
        }

        // Reset pattern index if out of bounds
        if (_patternIndex >= _currentPattern.Count)
        {
            _patternIndex = 0;
        }
    }

    private void BuildConvergePattern(List<int> notes)
    {
        int low = 0;
        int high = notes.Count - 1;
        bool fromLow = true;

        while (low <= high)
        {
            if (fromLow)
            {
                _currentPattern.Add(notes[low]);
                low++;
            }
            else
            {
                _currentPattern.Add(notes[high]);
                high--;
            }
            fromLow = !fromLow;
        }
    }

    private void BuildDivergePattern(List<int> notes)
    {
        int mid = notes.Count / 2;
        int low = mid;
        int high = mid + 1;

        while (low >= 0 || high < notes.Count)
        {
            if (low >= 0)
            {
                _currentPattern.Add(notes[low]);
                low--;
            }
            if (high < notes.Count)
            {
                _currentPattern.Add(notes[high]);
                high++;
            }
        }
    }

    private void BuildThumbPattern(List<int> notes, bool ascending)
    {
        if (notes.Count < 2)
        {
            _currentPattern.AddRange(notes);
            return;
        }

        int thumbNote = ascending ? notes[0] : notes[^1];
        var otherNotes = ascending ? notes.Skip(1).ToList() : notes.Take(notes.Count - 1).Reverse().ToList();

        foreach (var note in otherNotes)
        {
            _currentPattern.Add(thumbNote);
            _currentPattern.Add(note);
        }
    }

    private void BuildPinkyPattern(List<int> notes, bool ascending)
    {
        if (notes.Count < 2)
        {
            _currentPattern.AddRange(notes);
            return;
        }

        int pinkyNote = ascending ? notes[^1] : notes[0];
        var otherNotes = ascending ? notes.Take(notes.Count - 1).ToList() : notes.Skip(1).Reverse().ToList();

        foreach (var note in otherNotes)
        {
            _currentPattern.Add(note);
            _currentPattern.Add(pinkyNote);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
        GC.SuppressFinalize(this);
    }

    ~Arpeggiator()
    {
        Dispose();
    }
}


/// <summary>
/// Event args for arpeggiator note events
/// </summary>
public class ArpNoteEventArgs : EventArgs
{
    public int Note { get; }
    public int Velocity { get; }

    public ArpNoteEventArgs(int note, int velocity)
    {
        Note = note;
        Velocity = velocity;
    }
}
