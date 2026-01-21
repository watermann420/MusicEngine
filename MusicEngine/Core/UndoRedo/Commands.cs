//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Common undoable command implementations.

using System;
using System.Collections.Generic;

namespace MusicEngine.Core.UndoRedo;

/// <summary>
/// A generic command that uses delegates for execute and undo.
/// </summary>
public class DelegateCommand : IUndoableCommand
{
    private readonly Action _execute;
    private readonly Action _undo;

    /// <inheritdoc/>
    public string Description { get; }

    /// <summary>
    /// Creates a new delegate command.
    /// </summary>
    /// <param name="description">Description of the command.</param>
    /// <param name="execute">Action to execute.</param>
    /// <param name="undo">Action to undo.</param>
    public DelegateCommand(string description, Action execute, Action undo)
    {
        Description = description ?? throw new ArgumentNullException(nameof(description));
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _undo = undo ?? throw new ArgumentNullException(nameof(undo));
    }

    /// <inheritdoc/>
    public void Execute() => _execute();

    /// <inheritdoc/>
    public void Undo() => _undo();
}

/// <summary>
/// A command that changes a property value.
/// </summary>
/// <typeparam name="T">The type of the property value.</typeparam>
public class PropertyChangeCommand<T> : IUndoableCommand
{
    private readonly Action<T> _setter;
    private readonly T _oldValue;
    private readonly T _newValue;
    private readonly string _propertyName;

    /// <inheritdoc/>
    public string Description => $"Change {_propertyName}";

    /// <summary>
    /// Gets the old value.
    /// </summary>
    public T OldValue => _oldValue;

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public T NewValue => _newValue;

    /// <summary>
    /// Creates a new property change command.
    /// </summary>
    /// <param name="propertyName">Name of the property being changed.</param>
    /// <param name="setter">Action to set the property value.</param>
    /// <param name="oldValue">The value before the change.</param>
    /// <param name="newValue">The value after the change.</param>
    public PropertyChangeCommand(string propertyName, Action<T> setter, T oldValue, T newValue)
    {
        _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        _setter = setter ?? throw new ArgumentNullException(nameof(setter));
        _oldValue = oldValue;
        _newValue = newValue;
    }

    /// <inheritdoc/>
    public void Execute() => _setter(_newValue);

    /// <inheritdoc/>
    public void Undo() => _setter(_oldValue);

    /// <inheritdoc/>
    public bool CanMergeWith(IUndoableCommand other)
    {
        // Can merge with another property change on the same property
        return other is PropertyChangeCommand<T> otherCmd &&
               otherCmd._propertyName == _propertyName &&
               EqualityComparer<T>.Default.Equals(otherCmd._oldValue, _newValue);
    }

    /// <inheritdoc/>
    public IUndoableCommand MergeWith(IUndoableCommand other)
    {
        if (other is PropertyChangeCommand<T> otherCmd)
        {
            // Create a new command that goes from our old value to their new value
            return new PropertyChangeCommand<T>(_propertyName, _setter, _oldValue, otherCmd._newValue);
        }
        return this;
    }
}

/// <summary>
/// A command that adds an item to a collection.
/// </summary>
/// <typeparam name="T">The type of item.</typeparam>
public class AddItemCommand<T> : IUndoableCommand
{
    private readonly IList<T> _collection;
    private readonly T _item;
    private readonly int _index;

    /// <inheritdoc/>
    public string Description { get; }

    /// <summary>
    /// Creates a new add item command.
    /// </summary>
    /// <param name="description">Description of the command.</param>
    /// <param name="collection">The collection to add to.</param>
    /// <param name="item">The item to add.</param>
    /// <param name="index">The index to insert at, or -1 to append.</param>
    public AddItemCommand(string description, IList<T> collection, T item, int index = -1)
    {
        Description = description ?? throw new ArgumentNullException(nameof(description));
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        _item = item;
        _index = index;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        if (_index >= 0 && _index < _collection.Count)
        {
            _collection.Insert(_index, _item);
        }
        else
        {
            _collection.Add(_item);
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        _collection.Remove(_item);
    }
}

/// <summary>
/// A command that removes an item from a collection.
/// </summary>
/// <typeparam name="T">The type of item.</typeparam>
public class RemoveItemCommand<T> : IUndoableCommand
{
    private readonly IList<T> _collection;
    private readonly T _item;
    private int _originalIndex;

    /// <inheritdoc/>
    public string Description { get; }

    /// <summary>
    /// Creates a new remove item command.
    /// </summary>
    /// <param name="description">Description of the command.</param>
    /// <param name="collection">The collection to remove from.</param>
    /// <param name="item">The item to remove.</param>
    public RemoveItemCommand(string description, IList<T> collection, T item)
    {
        Description = description ?? throw new ArgumentNullException(nameof(description));
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        _item = item;
        _originalIndex = -1;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        _originalIndex = _collection.IndexOf(_item);
        if (_originalIndex >= 0)
        {
            _collection.RemoveAt(_originalIndex);
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        if (_originalIndex >= 0)
        {
            if (_originalIndex <= _collection.Count)
            {
                _collection.Insert(_originalIndex, _item);
            }
            else
            {
                _collection.Add(_item);
            }
        }
    }
}

/// <summary>
/// A command that moves an item within a collection.
/// </summary>
/// <typeparam name="T">The type of item.</typeparam>
public class MoveItemCommand<T> : IUndoableCommand
{
    private readonly IList<T> _collection;
    private readonly int _fromIndex;
    private readonly int _toIndex;

    /// <inheritdoc/>
    public string Description { get; }

    /// <summary>
    /// Creates a new move item command.
    /// </summary>
    /// <param name="description">Description of the command.</param>
    /// <param name="collection">The collection containing the item.</param>
    /// <param name="fromIndex">The original index.</param>
    /// <param name="toIndex">The target index.</param>
    public MoveItemCommand(string description, IList<T> collection, int fromIndex, int toIndex)
    {
        Description = description ?? throw new ArgumentNullException(nameof(description));
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        _fromIndex = fromIndex;
        _toIndex = toIndex;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        var item = _collection[_fromIndex];
        _collection.RemoveAt(_fromIndex);
        _collection.Insert(_toIndex, item);
    }

    /// <inheritdoc/>
    public void Undo()
    {
        var item = _collection[_toIndex];
        _collection.RemoveAt(_toIndex);
        _collection.Insert(_fromIndex, item);
    }
}
