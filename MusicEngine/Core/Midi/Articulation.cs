// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MIDI handling component.

using System;
using System.Drawing;


namespace MusicEngine.Core.Midi;


/// <summary>
/// Types of musical articulations commonly used in orchestral and instrument libraries.
/// </summary>
public enum ArticulationType
{
    /// <summary>
    /// Sustained notes with full duration.
    /// </summary>
    Sustain,

    /// <summary>
    /// Short, detached notes.
    /// </summary>
    Staccato,

    /// <summary>
    /// Smooth, connected notes.
    /// </summary>
    Legato,

    /// <summary>
    /// Plucked string technique.
    /// </summary>
    Pizzicato,

    /// <summary>
    /// Rapid repetition of a note.
    /// </summary>
    Tremolo,

    /// <summary>
    /// Rapid alternation between two notes.
    /// </summary>
    Trill,

    /// <summary>
    /// Strong, accented attack.
    /// </summary>
    Marcato,

    /// <summary>
    /// Held for full value with slight emphasis.
    /// </summary>
    Tenuto,

    /// <summary>
    /// Light, bouncing bow technique.
    /// </summary>
    Spiccato,

    /// <summary>
    /// Striking strings with the wood of the bow.
    /// </summary>
    Col_Legno
}


/// <summary>
/// Represents a single articulation in an expression map.
/// An articulation defines how a note should be played and what MIDI messages
/// are needed to trigger that playing technique in a sample library or synthesizer.
/// </summary>
public class Articulation
{
    /// <summary>
    /// Gets the unique identifier for this articulation.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets or sets the display name of this articulation.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of articulation.
    /// </summary>
    public ArticulationType Type { get; set; } = ArticulationType.Sustain;

    /// <summary>
    /// Gets or sets the keyswitch note that triggers this articulation.
    /// -1 indicates no keyswitch is used.
    /// </summary>
    public int KeyswitchNote { get; set; } = -1;

    /// <summary>
    /// Gets or sets the program change number that triggers this articulation.
    /// -1 indicates no program change is used.
    /// </summary>
    public int ProgramChange { get; set; } = -1;

    /// <summary>
    /// Gets or sets the control change number used to trigger this articulation.
    /// -1 indicates no control change is used.
    /// </summary>
    public int ControlChange { get; set; } = -1;

    /// <summary>
    /// Gets or sets the value to send with the control change message.
    /// Only used when ControlChange is not -1.
    /// </summary>
    public int ControlValue { get; set; }

    /// <summary>
    /// Gets or sets the display color for this articulation in the UI.
    /// </summary>
    public Color DisplayColor { get; set; } = Color.Gray;


    /// <summary>
    /// Creates a new articulation with a unique ID.
    /// </summary>
    public Articulation()
    {
        Id = Guid.NewGuid();
    }


    /// <summary>
    /// Creates a new articulation with a specific ID.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    public Articulation(Guid id)
    {
        Id = id;
    }


    /// <summary>
    /// Creates a new articulation with the specified properties.
    /// </summary>
    /// <param name="name">The display name.</param>
    /// <param name="type">The articulation type.</param>
    /// <param name="keyswitchNote">The keyswitch note (-1 for none).</param>
    public Articulation(string name, ArticulationType type, int keyswitchNote = -1)
    {
        Id = Guid.NewGuid();
        Name = name;
        Type = type;
        KeyswitchNote = keyswitchNote;
    }


    /// <summary>
    /// Determines whether this articulation uses a keyswitch.
    /// </summary>
    public bool UsesKeyswitch => KeyswitchNote >= 0 && KeyswitchNote <= 127;


    /// <summary>
    /// Determines whether this articulation uses a program change.
    /// </summary>
    public bool UsesProgramChange => ProgramChange >= 0 && ProgramChange <= 127;


    /// <summary>
    /// Determines whether this articulation uses a control change.
    /// </summary>
    public bool UsesControlChange => ControlChange >= 0 && ControlChange <= 127;


    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{Name} ({Type})";
    }


    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is Articulation other && Id == other.Id;
    }


    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
