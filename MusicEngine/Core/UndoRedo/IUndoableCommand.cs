// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

namespace MusicEngine.Core.UndoRedo;

/// <summary>
/// Interface for commands that can be executed, undone, and redone.
/// Implements the Command Pattern for undo/redo functionality.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>
    /// Gets a human-readable description of what this command does.
    /// Used for display in undo/redo history.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes the command.
    /// </summary>
    void Execute();

    /// <summary>
    /// Undoes the command, reverting to the state before Execute was called.
    /// </summary>
    void Undo();

    /// <summary>
    /// Redoes the command after it has been undone.
    /// Default implementation calls Execute().
    /// </summary>
    void Redo() => Execute();

    /// <summary>
    /// Gets whether this command can be merged with another command of the same type.
    /// Useful for combining multiple small changes (e.g., typing characters).
    /// </summary>
    bool CanMergeWith(IUndoableCommand other) => false;

    /// <summary>
    /// Merges this command with another command.
    /// Only called if CanMergeWith returns true.
    /// </summary>
    /// <param name="other">The command to merge with.</param>
    /// <returns>A new merged command, or this command if merge is in-place.</returns>
    IUndoableCommand MergeWith(IUndoableCommand other) => this;
}
