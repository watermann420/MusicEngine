// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Shared modulation engine for scripted parameter control.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using ThreadingTimer = System.Threading.Timer;

namespace MusicEngine.Core.Modulation;

/// <summary>
/// Shared modulation engine that updates registered mod nodes.
/// </summary>
internal sealed class ModEngine : IDisposable
{
    private readonly List<IModNode> _nodes = new();
    private readonly object _lock = new();
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly ThreadingTimer _timer;

    /// <summary>
    /// Shared global modulation engine.
    /// </summary>
    public static ModEngine Shared { get; } = new ModEngine();

    private ModEngine()
    {
        _timer = new ThreadingTimer(_ => Tick(), null, 30, 30);
    }

    /// <summary>
    /// Register a mod node for updates.
    /// </summary>
    public void Register(IModNode node)
    {
        if (node == null) return;
        lock (_lock)
        {
            if (_nodes.Contains(node)) return;
            _nodes.Add(node);
        }
    }

    /// <summary>
    /// Unregister a mod node.
    /// </summary>
    public void Unregister(IModNode node)
    {
        if (node == null) return;
        lock (_lock)
        {
            _nodes.Remove(node);
        }
    }

    private void Tick()
    {
        var elapsed = _watch.Elapsed;
        _watch.Restart();
        var deltaSeconds = Math.Max(0.0, elapsed.TotalSeconds);

        IModNode[] snapshot;
        lock (_lock)
        {
            if (_nodes.Count == 0) return;
            snapshot = _nodes.ToArray();
        }

        foreach (var node in snapshot)
        {
            node.Update(deltaSeconds);
        }
    }

    /// <summary>
    /// Stop the update timer and clear nodes.
    /// </summary>
    public void Dispose()
    {
        _timer.Dispose();
        lock (_lock)
        {
            _nodes.Clear();
        }
    }
}
