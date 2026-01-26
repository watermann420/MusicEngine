// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MIDI handling component.

namespace MusicEngine.Core.Midi;

/// <summary>
/// Velocity curve types for zone processing.
/// </summary>
public enum VelocityCurve
{
    /// <summary>Linear (no change).</summary>
    Linear,
    /// <summary>Soft response (lower velocities boosted).</summary>
    Soft,
    /// <summary>Hard response (higher velocities emphasized).</summary>
    Hard,
    /// <summary>S-curve (soft at extremes, steep in middle).</summary>
    SCurve,
    /// <summary>Exponential (quiet notes quieter).</summary>
    Exponential,
    /// <summary>Logarithmic (quiet notes louder).</summary>
    Logarithmic,
    /// <summary>Fixed velocity (ignores input velocity).</summary>
    Fixed
}

/// <summary>
/// Represents a keyboard split zone.
/// </summary>
public class SplitZone
{
    /// <summary>Unique identifier for this zone.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Display name for this zone.</summary>
    public string Name { get; set; } = "";

    /// <summary>Whether this zone is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Lowest note in the zone (inclusive).</summary>
    public int LowNote { get; set; }

    /// <summary>Highest note in the zone (inclusive).</summary>
    public int HighNote { get; set; } = 127;

    /// <summary>Lowest velocity to trigger this zone (inclusive).</summary>
    public int LowVelocity { get; set; } = 1;

    /// <summary>Highest velocity to trigger this zone (inclusive).</summary>
    public int HighVelocity { get; set; } = 127;

    /// <summary>Transpose amount in semitones.</summary>
    public int Transpose { get; set; }

    /// <summary>Fine tune in cents (-100 to +100).</summary>
    public int FineTune { get; set; }

    /// <summary>Output MIDI channel (0-15, -1 = pass through).</summary>
    public int OutputChannel { get; set; } = -1;

    /// <summary>Velocity curve type.</summary>
    public VelocityCurve VelocityCurve { get; set; } = VelocityCurve.Linear;

    /// <summary>Fixed velocity value (used when VelocityCurve is Fixed).</summary>
    public int FixedVelocity { get; set; } = 100;

    /// <summary>Velocity scale factor (0.0 to 2.0).</summary>
    public float VelocityScale { get; set; } = 1.0f;

    /// <summary>Velocity offset (-127 to +127).</summary>
    public int VelocityOffset { get; set; }

    /// <summary>Priority for overlapping zones (higher = processed first).</summary>
    public int Priority { get; set; }

    /// <summary>Whether to pass the note to subsequent zones.</summary>
    public bool PassThrough { get; set; } = true;

    /// <summary>Optional user data for the zone.</summary>
    public object? UserData { get; set; }

    /// <summary>
    /// Creates a new split zone covering the full keyboard.
    /// </summary>
    public SplitZone()
    {
    }

    /// <summary>
    /// Creates a new split zone with specified range.
    /// </summary>
    /// <param name="lowNote">Lowest note (inclusive).</param>
    /// <param name="highNote">Highest note (inclusive).</param>
    /// <param name="name">Optional zone name.</param>
    public SplitZone(int lowNote, int highNote, string? name = null)
    {
        LowNote = Math.Clamp(lowNote, 0, 127);
        HighNote = Math.Clamp(highNote, 0, 127);
        Name = name ?? $"Zone {LowNote}-{HighNote}";
    }

    /// <summary>
    /// Checks if a note falls within this zone.
    /// </summary>
    /// <param name="note">MIDI note number.</param>
    /// <returns>True if the note is within the zone range.</returns>
    public bool ContainsNote(int note)
    {
        return note >= LowNote && note <= HighNote;
    }

    /// <summary>
    /// Checks if a velocity falls within this zone.
    /// </summary>
    /// <param name="velocity">MIDI velocity.</param>
    /// <returns>True if the velocity is within the zone range.</returns>
    public bool ContainsVelocity(int velocity)
    {
        return velocity >= LowVelocity && velocity <= HighVelocity;
    }

    /// <summary>
    /// Checks if a note and velocity fall within this zone.
    /// </summary>
    /// <param name="note">MIDI note number.</param>
    /// <param name="velocity">MIDI velocity.</param>
    /// <returns>True if both are within range.</returns>
    public bool Contains(int note, int velocity)
    {
        return Enabled && ContainsNote(note) && ContainsVelocity(velocity);
    }

    /// <summary>
    /// Processes a note through this zone.
    /// </summary>
    /// <param name="note">Input note.</param>
    /// <param name="velocity">Input velocity.</param>
    /// <returns>Processed note and velocity, or null if outside zone.</returns>
    public (int Note, int Velocity)? Process(int note, int velocity)
    {
        if (!Contains(note, velocity)) return null;

        int processedNote = Math.Clamp(note + Transpose, 0, 127);
        int processedVelocity = ProcessVelocity(velocity);

        return (processedNote, processedVelocity);
    }

    private int ProcessVelocity(int velocity)
    {
        float v = velocity / 127f;
        float processed;

        switch (VelocityCurve)
        {
            case VelocityCurve.Linear:
                processed = v;
                break;

            case VelocityCurve.Soft:
                processed = (float)Math.Sqrt(v);
                break;

            case VelocityCurve.Hard:
                processed = v * v;
                break;

            case VelocityCurve.SCurve:
                processed = v < 0.5f
                    ? 2 * v * v
                    : 1 - (float)Math.Pow(-2 * v + 2, 2) / 2;
                break;

            case VelocityCurve.Exponential:
                processed = (float)(Math.Exp(v) - 1) / (float)(Math.E - 1);
                break;

            case VelocityCurve.Logarithmic:
                processed = (float)Math.Log(1 + v * (Math.E - 1));
                break;

            case VelocityCurve.Fixed:
                return FixedVelocity;

            default:
                processed = v;
                break;
        }

        int result = (int)(processed * 127 * VelocityScale) + VelocityOffset;
        return Math.Clamp(result, 1, 127);
    }

    /// <summary>
    /// Creates a copy of this zone.
    /// </summary>
    public SplitZone Clone()
    {
        return new SplitZone
        {
            Name = Name,
            Enabled = Enabled,
            LowNote = LowNote,
            HighNote = HighNote,
            LowVelocity = LowVelocity,
            HighVelocity = HighVelocity,
            Transpose = Transpose,
            FineTune = FineTune,
            OutputChannel = OutputChannel,
            VelocityCurve = VelocityCurve,
            FixedVelocity = FixedVelocity,
            VelocityScale = VelocityScale,
            VelocityOffset = VelocityOffset,
            Priority = Priority,
            PassThrough = PassThrough
        };
    }
}

/// <summary>
/// Result of processing a note through MIDI split zones.
/// </summary>
public class SplitResult
{
    /// <summary>Original input note.</summary>
    public int OriginalNote { get; init; }

    /// <summary>Original input velocity.</summary>
    public int OriginalVelocity { get; init; }

    /// <summary>Original input channel.</summary>
    public int OriginalChannel { get; init; }

    /// <summary>List of outputs from matching zones.</summary>
    public List<SplitOutput> Outputs { get; } = new();

    /// <summary>Whether any zone matched.</summary>
    public bool HasOutputs => Outputs.Count > 0;
}

/// <summary>
/// Single output from a split zone.
/// </summary>
public class SplitOutput
{
    /// <summary>The zone that produced this output.</summary>
    public required SplitZone Zone { get; init; }

    /// <summary>Processed note number.</summary>
    public int Note { get; init; }

    /// <summary>Processed velocity.</summary>
    public int Velocity { get; init; }

    /// <summary>Output channel (or original if -1).</summary>
    public int Channel { get; init; }
}

/// <summary>
/// Event arguments for zone match events.
/// </summary>
public class ZoneMatchEventArgs : EventArgs
{
    /// <summary>The zone that matched.</summary>
    public required SplitZone Zone { get; init; }

    /// <summary>Original note.</summary>
    public int OriginalNote { get; init; }

    /// <summary>Processed note.</summary>
    public int ProcessedNote { get; init; }

    /// <summary>Original velocity.</summary>
    public int OriginalVelocity { get; init; }

    /// <summary>Processed velocity.</summary>
    public int ProcessedVelocity { get; init; }
}

/// <summary>
/// MIDI keyboard split processor for routing notes to different zones.
/// Supports multiple split points, per-zone transpose, velocity curves, and channel routing.
/// </summary>
public class MidiSplit : IDisposable
{
    private readonly List<SplitZone> _zones = new();
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>Whether MIDI split is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Number of configured zones.</summary>
    public int ZoneCount
    {
        get
        {
            lock (_lock)
            {
                return _zones.Count;
            }
        }
    }

    /// <summary>Fired when a zone matches a note.</summary>
    public event EventHandler<ZoneMatchEventArgs>? ZoneMatched;

    /// <summary>
    /// Creates a new MIDI split processor.
    /// </summary>
    public MidiSplit()
    {
    }

    /// <summary>
    /// Adds a zone to the split configuration.
    /// </summary>
    /// <param name="zone">The zone to add.</param>
    public void AddZone(SplitZone zone)
    {
        if (zone == null)
            throw new ArgumentNullException(nameof(zone));

        lock (_lock)
        {
            _zones.Add(zone);
            _zones.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    /// <summary>
    /// Removes a zone from the split configuration.
    /// </summary>
    /// <param name="zone">The zone to remove.</param>
    /// <returns>True if the zone was removed.</returns>
    public bool RemoveZone(SplitZone zone)
    {
        lock (_lock)
        {
            return _zones.Remove(zone);
        }
    }

    /// <summary>
    /// Removes a zone by ID.
    /// </summary>
    /// <param name="zoneId">The zone ID to remove.</param>
    /// <returns>True if the zone was removed.</returns>
    public bool RemoveZone(Guid zoneId)
    {
        lock (_lock)
        {
            var zone = _zones.FirstOrDefault(z => z.Id == zoneId);
            if (zone != null)
            {
                return _zones.Remove(zone);
            }
            return false;
        }
    }

    /// <summary>
    /// Gets all configured zones.
    /// </summary>
    public IReadOnlyList<SplitZone> GetZones()
    {
        lock (_lock)
        {
            return _zones.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Clears all zones.
    /// </summary>
    public void ClearZones()
    {
        lock (_lock)
        {
            _zones.Clear();
        }
    }

    /// <summary>
    /// Processes a note through all matching zones.
    /// </summary>
    /// <param name="note">Input MIDI note.</param>
    /// <param name="velocity">Input velocity.</param>
    /// <param name="channel">Input channel (0-15).</param>
    /// <returns>Split result with all matching zone outputs.</returns>
    public SplitResult Process(int note, int velocity, int channel = 0)
    {
        var result = new SplitResult
        {
            OriginalNote = note,
            OriginalVelocity = velocity,
            OriginalChannel = channel
        };

        if (!Enabled)
        {
            // Pass through unchanged
            result.Outputs.Add(new SplitOutput
            {
                Zone = new SplitZone { Name = "Passthrough" },
                Note = note,
                Velocity = velocity,
                Channel = channel
            });
            return result;
        }

        lock (_lock)
        {
            foreach (var zone in _zones)
            {
                var processed = zone.Process(note, velocity);
                if (processed.HasValue)
                {
                    int outputChannel = zone.OutputChannel >= 0 ? zone.OutputChannel : channel;

                    result.Outputs.Add(new SplitOutput
                    {
                        Zone = zone,
                        Note = processed.Value.Note,
                        Velocity = processed.Value.Velocity,
                        Channel = outputChannel
                    });

                    ZoneMatched?.Invoke(this, new ZoneMatchEventArgs
                    {
                        Zone = zone,
                        OriginalNote = note,
                        ProcessedNote = processed.Value.Note,
                        OriginalVelocity = velocity,
                        ProcessedVelocity = processed.Value.Velocity
                    });

                    if (!zone.PassThrough)
                    {
                        break; // Stop processing if zone doesn't pass through
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Creates a simple two-zone split at a given note.
    /// </summary>
    /// <param name="splitPoint">The split point (this note goes to upper zone).</param>
    /// <param name="lowerTranspose">Transpose for lower zone.</param>
    /// <param name="upperTranspose">Transpose for upper zone.</param>
    public void CreateSimpleSplit(int splitPoint, int lowerTranspose = 0, int upperTranspose = 0)
    {
        lock (_lock)
        {
            _zones.Clear();

            _zones.Add(new SplitZone(0, splitPoint - 1, "Lower")
            {
                Transpose = lowerTranspose,
                OutputChannel = 0
            });

            _zones.Add(new SplitZone(splitPoint, 127, "Upper")
            {
                Transpose = upperTranspose,
                OutputChannel = 1
            });
        }
    }

    /// <summary>
    /// Creates a three-zone split (bass, mid, treble).
    /// </summary>
    /// <param name="bassHigh">Highest note for bass zone.</param>
    /// <param name="midHigh">Highest note for mid zone.</param>
    public void CreateThreeZoneSplit(int bassHigh = 47, int midHigh = 71)
    {
        lock (_lock)
        {
            _zones.Clear();

            _zones.Add(new SplitZone(0, bassHigh, "Bass")
            {
                OutputChannel = 0
            });

            _zones.Add(new SplitZone(bassHigh + 1, midHigh, "Mid")
            {
                OutputChannel = 1
            });

            _zones.Add(new SplitZone(midHigh + 1, 127, "Treble")
            {
                OutputChannel = 2
            });
        }
    }

    /// <summary>
    /// Creates velocity-split layers (soft/hard).
    /// </summary>
    /// <param name="velocitySplit">Velocity split point.</param>
    public void CreateVelocityLayers(int velocitySplit = 64)
    {
        lock (_lock)
        {
            _zones.Clear();

            _zones.Add(new SplitZone
            {
                Name = "Soft Layer",
                LowNote = 0,
                HighNote = 127,
                LowVelocity = 1,
                HighVelocity = velocitySplit - 1,
                OutputChannel = 0,
                PassThrough = false
            });

            _zones.Add(new SplitZone
            {
                Name = "Hard Layer",
                LowNote = 0,
                HighNote = 127,
                LowVelocity = velocitySplit,
                HighVelocity = 127,
                OutputChannel = 1,
                PassThrough = false
            });
        }
    }

    /// <summary>
    /// Creates an octave layer (same zone doubled at octave).
    /// </summary>
    /// <param name="octaveOffset">Octave offset for the layer (+1 or -1).</param>
    public void CreateOctaveLayer(int octaveOffset = 1)
    {
        lock (_lock)
        {
            _zones.Clear();

            _zones.Add(new SplitZone
            {
                Name = "Original",
                LowNote = 0,
                HighNote = 127,
                OutputChannel = 0,
                PassThrough = true
            });

            _zones.Add(new SplitZone
            {
                Name = $"Octave {(octaveOffset > 0 ? "+" : "")}{octaveOffset}",
                LowNote = 0,
                HighNote = 127,
                Transpose = octaveOffset * 12,
                OutputChannel = 1,
                PassThrough = true
            });
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            _zones.Clear();
        }

        GC.SuppressFinalize(this);
    }
}
