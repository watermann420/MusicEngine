// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Equalizer effect.

using System;


namespace MusicEngine.Core;


public class FrequencyMidiMapping
{
    public int DeviceIndex { get; set; } // MIDI Device Index
    public float LowFreq { get; set; } // in Hz // Lower bound of frequency range
    public float HighFreq { get; set; } // in Hz // Upper bound of frequency range
    public float Threshold { get; set; } = 0.1f; // Magnitude threshold for triggering
    public Action<float>? OnTrigger { get; set; } // Action to invoke on trigger with magnitude
    
    private bool _isAboveThreshold; // To prevent retriggering
    
    // Processes the magnitude for the frequency range and triggers if above the threshold
    public void ProcessMagnitude(float magnitude)
    {
        if (magnitude > Threshold)
        {
            if (!_isAboveThreshold)
            {
                _isAboveThreshold = true; // Set a flag to indicate we are above the threshold
                OnTrigger?.Invoke(magnitude); // Invoke the trigger action
            }
        }
        else
        {
            _isAboveThreshold = false; // Reset the flag when below a threshold
        }
    }
}
