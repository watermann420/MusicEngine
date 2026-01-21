//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Composite command that groups multiple commands into one.

using System;
using System.Collections.Generic;

namespace MusicEngine.Core.UndoRedo;

/// <summary>
/// A command that groups multiple commands into a single undoable unit.
/// </summary>
public class CompositeCommand : IUndoableCommand
{
    private readonly List<IUndoableCommand> _commands = new();
    private readonly string _description;

    /// <summary>
    /// Gets the list of commands in this composite.
    /// </summary>
    public IReadOnlyList<IUndoableCommand> Commands => _commands;

    /// <inheritdoc/>
    public string Description => _description;

    /// <summary>
    /// Creates a new composite command.
    /// </summary>
    /// <param name="description">Description for the composite command.</param>
    public CompositeCommand(string description)
    {
        _description = description ?? throw new ArgumentNullException(nameof(description));
    }

    /// <summary>
    /// Adds a command to this composite.
    /// </summary>
    /// <param name="command">The command to add.</param>
    public void Add(IUndoableCommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        _commands.Add(command);
    }

    /// <inheritdoc/>
    public void Execute()
    {
        foreach (var command in _commands)
        {
            command.Execute();
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        // Undo in reverse order
        for (int i = _commands.Count - 1; i >= 0; i--)
        {
            _commands[i].Undo();
        }
    }

    /// <inheritdoc/>
    public void Redo()
    {
        foreach (var command in _commands)
        {
            command.Redo();
        }
    }
}

/// <summary>
/// Represents a batch operation that groups commands.
/// Dispose to commit the batch.
/// </summary>
public class UndoBatch : IDisposable
{
    private readonly UndoManager _manager;
    private readonly CompositeCommand _composite;
    private bool _disposed;
    private bool _committed;

    internal UndoBatch(UndoManager manager, string description)
    {
        _manager = manager;
        _composite = new CompositeCommand(description);
    }

    /// <summary>
    /// Adds and executes a command within this batch.
    /// </summary>
    /// <param name="command">The command to add and execute.</param>
    public void Execute(IUndoableCommand command)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UndoBatch));
        if (_committed)
            throw new InvalidOperationException("Batch has already been committed.");

        command.Execute();
        _composite.Add(command);
    }

    /// <summary>
    /// Commits the batch to the undo manager.
    /// Called automatically on dispose.
    /// </summary>
    public void Commit()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UndoBatch));
        if (_committed)
            return;

        _committed = true;
        _manager.ExecuteBatch(_composite);
    }

    /// <summary>
    /// Cancels the batch and undoes all commands.
    /// </summary>
    public void Cancel()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UndoBatch));
        if (_committed)
            throw new InvalidOperationException("Cannot cancel a committed batch.");

        // Undo all commands in reverse order
        _composite.Undo();
        _committed = true; // Mark as handled
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        if (!_committed)
        {
            Commit();
        }

        _disposed = true;
    }
}
