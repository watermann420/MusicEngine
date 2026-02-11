// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Global activity controller for performance tuning.

using System;
using MusicEngine.Core;

namespace MusicEngine.Scripting;

/// <summary>
/// Global activity controller for performance tuning.
/// </summary>
public sealed class ActivityController
{
    private readonly ScriptGlobals _globals;
    private readonly AudioEngine _engine;
    private readonly Sequencer _sequencer;

    /// <summary>
    /// Create an activity controller for the current script context.
    /// </summary>
    public ActivityController(ScriptGlobals globals)
    {
        _globals = globals ?? throw new ArgumentNullException(nameof(globals));
        _engine = globals.Engine;
        _sequencer = globals.Sequencer;
    }

    /// <summary>
    /// Enable or disable audio output.
    /// </summary>
    public bool AudioEnabled
    {
        get => _engine.OutputRunning;
        set
        {
            if (value)
            {
                _engine.ResumeOutput();
            }
            else
            {
                _engine.SuspendOutput();
            }
        }
    }

    /// <summary>
    /// Enable or disable MIDI input processing.
    /// </summary>
    public bool MidiEnabled
    {
        get => _engine.MidiEnabled;
        set => _engine.SetMidiEnabled(value, sendAllNotesOff: true);
    }

    /// <summary>
    /// Enable or disable sequencer processing.
    /// </summary>
    public bool SequencerEnabled
    {
        get => Settings.SequencerEnabled;
        set
        {
            Settings.SequencerEnabled = value;
            if (value)
            {
                if (!_sequencer.IsRunning)
                {
                    _sequencer.Start();
                }
            }
            else
            {
                _sequencer.Stop();
            }
        }
    }

    /// <summary>
    /// Enable or disable non-VST audio effects.
    /// </summary>
    public bool AudioEffectsEnabled
    {
        get => Settings.AudioEffectsEnabled;
        set => Settings.AudioEffectsEnabled = value;
    }

    /// <summary>
    /// Enable or disable VST instrument processing.
    /// </summary>
    public bool VstInstrumentsEnabled
    {
        get => Settings.VstInstrumentsEnabled;
        set => Settings.VstInstrumentsEnabled = value;
    }

    /// <summary>
    /// Enable or disable VST effect processing.
    /// </summary>
    public bool VstEffectsEnabled
    {
        get => Settings.VstEffectsEnabled;
        set => Settings.VstEffectsEnabled = value;
    }

    /// <summary>
    /// Default idle sleep for VST instruments.
    /// </summary>
    public bool VstInstrumentSleepWhenIdle
    {
        get => Settings.VstInstrumentSleepWhenIdle;
        set
        {
            Settings.VstInstrumentSleepWhenIdle = value;
            ApplyVstSleepSettings();
        }
    }

    /// <summary>
    /// Default idle sleep for VST effects.
    /// </summary>
    public bool VstEffectSleepWhenIdle
    {
        get => Settings.VstEffectSleepWhenIdle;
        set
        {
            Settings.VstEffectSleepWhenIdle = value;
            ApplyVstSleepSettings();
        }
    }

    /// <summary>
    /// Default idle threshold for VST sleep detection.
    /// </summary>
    public float VstIdleThreshold
    {
        get => Settings.VstIdleThreshold;
        set
        {
            Settings.VstIdleThreshold = value;
            ApplyVstSleepSettings();
        }
    }

    /// <summary>
    /// Default idle timeout in seconds for VST sleep detection.
    /// </summary>
    public double VstIdleTimeoutSeconds
    {
        get => Settings.VstIdleTimeoutSeconds;
        set
        {
            Settings.VstIdleTimeoutSeconds = value;
            ApplyVstSleepSettings();
        }
    }

    /// <summary>
    /// Apply current VST sleep settings to loaded instances.
    /// </summary>
    public void ApplyVstSleepSettings()
    {
        _globals.VstAccessInstance?.ApplySleepSettings();
    }

    /// <summary>
    /// Set a more aggressive VST sleep preset.
    /// </summary>
    public void AggressiveVstSleep()
    {
        Settings.VstIdleThreshold = 5e-4f;
        Settings.VstIdleTimeoutSeconds = 0.08;
        ApplyVstSleepSettings();
    }
}
