// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;

namespace MusicEngine.Core.UndoRedo;

/// <summary>
/// Manages the undo/redo stack for command history.
/// Thread-safe implementation with configurable history limit.
/// </summary>
public class UndoManager
{
    private readonly Stack<IUndoableCommand> _undoStack = new();
    private readonly Stack<IUndoableCommand> _redoStack = new();
    private readonly object _lock = new();
    private readonly int _maxHistorySize;
    private bool _isExecuting;

    /// <summary>
    /// Gets the maximum number of commands to keep in history.
    /// </summary>
    public int MaxHistorySize => _maxHistorySize;

    /// <summary>
    /// Gets whether there are commands that can be undone.
    /// </summary>
    public bool CanUndo
    {
        get
        {
            lock (_lock)
            {
                return _undoStack.Count > 0;
            }
        }
    }

    /// <summary>
    /// Gets whether there are commands that can be redone.
    /// </summary>
    public bool CanRedo
    {
        get
        {
            lock (_lock)
            {
                return _redoStack.Count > 0;
            }
        }
    }

    /// <summary>
    /// Gets the number of commands in the undo stack.
    /// </summary>
    public int UndoCount
    {
        get
        {
            lock (_lock)
            {
                return _undoStack.Count;
            }
        }
    }

    /// <summary>
    /// Gets the number of commands in the redo stack.
    /// </summary>
    public int RedoCount
    {
        get
        {
            lock (_lock)
            {
                return _redoStack.Count;
            }
        }
    }

    /// <summary>
    /// Gets the description of the next command to undo, or null if none.
    /// </summary>
    public string? NextUndoDescription
    {
        get
        {
            lock (_lock)
            {
                return _undoStack.Count > 0 ? _undoStack.Peek().Description : null;
            }
        }
    }

    /// <summary>
    /// Gets the description of the next command to redo, or null if none.
    /// </summary>
    public string? NextRedoDescription
    {
        get
        {
            lock (_lock)
            {
                return _redoStack.Count > 0 ? _redoStack.Peek().Description : null;
            }
        }
    }

    /// <summary>
    /// Event raised when the undo/redo state changes.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Event raised before a command is executed.
    /// </summary>
    public event EventHandler<CommandEventArgs>? CommandExecuting;

    /// <summary>
    /// Event raised after a command is executed.
    /// </summary>
    public event EventHandler<CommandEventArgs>? CommandExecuted;

    /// <summary>
    /// Creates a new UndoManager with the specified history limit.
    /// </summary>
    /// <param name="maxHistorySize">Maximum number of commands to keep. Default is 100.</param>
    public UndoManager(int maxHistorySize = 100)
    {
        if (maxHistorySize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHistorySize), "History size must be positive.");

        _maxHistorySize = maxHistorySize;
    }

    /// <summary>
    /// Executes a command and adds it to the undo stack.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    public void Execute(IUndoableCommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        lock (_lock)
        {
            if (_isExecuting)
                throw new InvalidOperationException("Cannot execute command while another command is executing.");

            _isExecuting = true;
            try
            {
                CommandExecuting?.Invoke(this, new CommandEventArgs(command, CommandAction.Execute));

                command.Execute();

                // Try to merge with previous command
                if (_undoStack.Count > 0 && _undoStack.Peek().CanMergeWith(command))
                {
                    var previous = _undoStack.Pop();
                    var merged = previous.MergeWith(command);
                    _undoStack.Push(merged);
                }
                else
                {
                    _undoStack.Push(command);

                    // Trim history if needed
                    TrimHistory();
                }

                // Clear redo stack when new command is executed
                _redoStack.Clear();

                CommandExecuted?.Invoke(this, new CommandEventArgs(command, CommandAction.Execute));
            }
            finally
            {
                _isExecuting = false;
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Undoes the most recent command.
    /// </summary>
    /// <returns>True if a command was undone, false if undo stack was empty.</returns>
    public bool Undo()
    {
        IUndoableCommand? command = null;

        lock (_lock)
        {
            if (_undoStack.Count == 0)
                return false;

            if (_isExecuting)
                throw new InvalidOperationException("Cannot undo while a command is executing.");

            _isExecuting = true;
            try
            {
                command = _undoStack.Pop();
                CommandExecuting?.Invoke(this, new CommandEventArgs(command, CommandAction.Undo));

                command.Undo();

                _redoStack.Push(command);

                CommandExecuted?.Invoke(this, new CommandEventArgs(command, CommandAction.Undo));
            }
            finally
            {
                _isExecuting = false;
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Redoes the most recently undone command.
    /// </summary>
    /// <returns>True if a command was redone, false if redo stack was empty.</returns>
    public bool Redo()
    {
        IUndoableCommand? command = null;

        lock (_lock)
        {
            if (_redoStack.Count == 0)
                return false;

            if (_isExecuting)
                throw new InvalidOperationException("Cannot redo while a command is executing.");

            _isExecuting = true;
            try
            {
                command = _redoStack.Pop();
                CommandExecuting?.Invoke(this, new CommandEventArgs(command, CommandAction.Redo));

                command.Redo();

                _undoStack.Push(command);

                CommandExecuted?.Invoke(this, new CommandEventArgs(command, CommandAction.Redo));
            }
            finally
            {
                _isExecuting = false;
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Clears all undo and redo history.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets a list of undo command descriptions (most recent first).
    /// </summary>
    public IReadOnlyList<string> GetUndoHistory()
    {
        lock (_lock)
        {
            var history = new List<string>();
            foreach (var command in _undoStack)
            {
                history.Add(command.Description);
            }
            return history;
        }
    }

    /// <summary>
    /// Gets a list of redo command descriptions (most recent first).
    /// </summary>
    public IReadOnlyList<string> GetRedoHistory()
    {
        lock (_lock)
        {
            var history = new List<string>();
            foreach (var command in _redoStack)
            {
                history.Add(command.Description);
            }
            return history;
        }
    }

    /// <summary>
    /// Begins a batch operation that groups multiple commands into one undo step.
    /// </summary>
    /// <param name="description">Description for the batch.</param>
    /// <returns>A disposable batch that commits on dispose.</returns>
    public UndoBatch BeginBatch(string description)
    {
        return new UndoBatch(this, description);
    }

    /// <summary>
    /// Executes a batch command (used internally by UndoBatch).
    /// </summary>
    internal void ExecuteBatch(CompositeCommand batch)
    {
        if (batch.Commands.Count == 0)
            return;

        lock (_lock)
        {
            _undoStack.Push(batch);
            _redoStack.Clear();
            TrimHistory();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TrimHistory()
    {
        // Convert to array, trim, and rebuild stack
        if (_undoStack.Count > _maxHistorySize)
        {
            var commands = _undoStack.ToArray();
            _undoStack.Clear();

            // Push back only the most recent commands (array is in reverse order)
            for (int i = _maxHistorySize - 1; i >= 0; i--)
            {
                _undoStack.Push(commands[i]);
            }
        }
    }
}

/// <summary>
/// Event arguments for command events.
/// </summary>
public class CommandEventArgs : EventArgs
{
    /// <summary>
    /// Gets the command being executed.
    /// </summary>
    public IUndoableCommand Command { get; }

    /// <summary>
    /// Gets the action being performed.
    /// </summary>
    public CommandAction Action { get; }

    public CommandEventArgs(IUndoableCommand command, CommandAction action)
    {
        Command = command;
        Action = action;
    }
}

/// <summary>
/// The type of command action.
/// </summary>
public enum CommandAction
{
    /// <summary>Initial execution</summary>
    Execute,
    /// <summary>Undoing the command</summary>
    Undo,
    /// <summary>Redoing the command</summary>
    Redo
}
