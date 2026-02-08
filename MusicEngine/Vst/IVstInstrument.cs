// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Common VST instrument surface for scripting.

using System;
using MusicEngine.Core;

namespace MusicEngine.Vst;

/// <summary>
/// Common VST instrument surface for scripting.
/// </summary>
public interface IVstInstrument : ISynth
{
    /// <summary>
    /// Open the VST editor window.
    /// </summary>
    void OpenEditor();

    /// <summary>
    /// Send pitch bend in normalized range [-1, 1].
    /// </summary>
    void PitchBend(float normalized);

    /// <summary>
    /// Reset common performance state such as notes and pitch bend.
    /// </summary>
    void ResetState();

    /// <summary>
    /// Create a setter for automation.
    /// </summary>
    Action<float> Param(string name, float min = 0f, float max = 1f);

    /// <summary>
    /// Set a named parameter with normalized value in [0, 1].
    /// </summary>
    void SetParameterNormalized(string name, float value);

    /// <summary>
    /// Get the plugin state as a binary blob.
    /// </summary>
    byte[] GetState();

    /// <summary>
    /// Load the plugin state from a binary blob.
    /// </summary>
    void SetState(byte[] data);

    /// <summary>
    /// Save the plugin state to a file.
    /// </summary>
    void SaveState(string path);

    /// <summary>
    /// Load the plugin state from a file.
    /// </summary>
    void LoadState(string path);

    /// <summary>
    /// Save the current state using the active auto-save path.
    /// </summary>
    void SaveStateNow();

    /// <summary>
    /// Disable automatic state save/load.
    /// </summary>
    void NoSave();

    /// <summary>
    /// Get or set the state as base64.
    /// </summary>
    string State(string? base64 = null);
}
