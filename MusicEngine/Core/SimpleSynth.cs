// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Basic monophonic/polyphonic synthesizer.

using System;
using System.Collections.Generic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;


namespace MusicEngine.Core;


// Enum for different waveform types
public enum WaveType
{
    Sine, // Default waveform 
    Square, // 50% duty cycle
    Sawtooth, // Ramp up
    Triangle, // Linear up and down
    Noise // White noise
}

/// A simple synthesizer implementation
public class SimpleSynth : ISynth
{
    private readonly WaveFormat _waveFormat; // Audio format
    private readonly List<Oscillator> _activeOscillators = new(); // Currently active oscillators
    private readonly object _lock = new(); // Thread safety lock

    public WaveType Waveform { get; set; } = WaveType.Sine; // Default waveform
    public float Cutoff { get; set; } = 1.0f; // 0.0 to 1.0 // Lowpass filter cutoff
    public float Resonance { get; set; } = 0.0f; // 0.0 to 1.0 // Lowpass filter resonance
    public string Name { get; set; } = "SimpleSynth"; // Synth name for identification
    
    // ISampleProvider implementation
    public WaveFormat WaveFormat => _waveFormat;
    
    // Constructor with an optional sample rate
    public SimpleSynth(int? sampleRate = null)
    {
        int rate = sampleRate ?? Settings.SampleRate; // Use provided or default sample rate
        _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Settings.Channels); // Create the wave format
    }
    
    // Note on event to start a note
    public void NoteOn(int note, int velocity)
    {
        MidiValidation.ValidateNote(note);
        MidiValidation.ValidateVelocity(velocity);

        lock (_lock)
        {
            var frequency = (float)(440.0 * Math.Pow(2.0, (note - 69.0) / 12.0)); // Convert MIDI note to frequency
            var osc = new Oscillator(frequency, (float)velocity / 127f, _waveFormat.SampleRate, Waveform); // Create oscillator
            _activeOscillators.Add(osc); // Add to active oscillators
        }
    }

    // Note off event to stop a note
    public void NoteOff(int note)
    {
        MidiValidation.ValidateNote(note);

        lock (_lock)
        {
            var frequency = (float)(440.0 * Math.Pow(2.0, (note - 69.0) / 12.0)); // Convert MIDI note to frequency
            foreach (var osc in _activeOscillators) // Find matching oscillators
            {
                if (Math.Abs(osc.Frequency - frequency) < 0.1f) // Frequency match tolerance
                {
                    osc.Stop(); // Stop the oscillator
                }
            }
        }
    }
    
    // Stop all currently playing notes
    public void AllNotesOff()
    {
        lock (_lock)
        {
            foreach (var osc in _activeOscillators) // Stop all oscillators
            {
                osc.Stop(); // Stop the oscillator
            }
        }
    }
    
    // Set synthesizer parameters by name
    public void SetParameter(string name, float value)
    {
        switch (name.ToLower()) // Case-insensitive parameter names
        {
            case "waveform":
                Waveform = (WaveType)(int)value; // Set waveform type
                break;
            case "cutoff":
                Cutoff = Math.Clamp(value, 0f, 1f); // Set cutoff with clamping
                break;
            case "resonance":
                Resonance = Math.Clamp(value, 0f, 1f); // Set resonance with clamping
                break;
        }
    }
    
    // Read audio samples into the buffer
    public int Read(float[] buffer, int offset, int count)
    {
        for (int n = 0; n < count; n++) buffer[offset + n] = 0; // Clear buffer

        int channels = _waveFormat.Channels; // Number of audio channels
        lock (_lock) // Ensure thread safety
        {
            for (int i = _activeOscillators.Count - 1; i >= 0; i--)  // Iterate backwards to allow removal
            {
                var osc = _activeOscillators[i]; // Get the oscillator
                for (int n = 0; n < count; n += channels) // For each sample frame
                {
                    float sample = osc.NextSample(); // Get the next sample
                    
                    // Simple lowpass filter implementation
                    float alpha = Cutoff * Cutoff * 0.5f;  // Calculate filter coefficient
                    osc.LastSample = osc.LastSample + alpha * (sample - osc.LastSample); // Apply filter
                    sample = osc.LastSample; // Use filtered sample

                    for (int c = 0; c < channels; c++) // For each channel
                    {
                        if (offset + n + c < buffer.Length)
                        {
                            buffer[offset + n + c] += sample;
                        }
                    }
                }
                if (osc.IsFinished) _activeOscillators.RemoveAt(i); // Remove finished oscillators
            }
        }
        return count;
    }
    
    // Internal oscillator class
    private class Oscillator
    {
        private float _phase; // Current phase of the oscillator
        private readonly float _phaseIncrement; // Phase increment per sample
        private float _amplitude; // Amplitude of the oscillator
        private float _currentGain; // Current gain for fade in/out
        private bool _stopping; // Is the oscillator stopping?
        private readonly int _sampleRate; // Sample rate
        private readonly WaveType _waveType; // Waveform type
        private readonly Random _random = new(); // Random generator for noise
        
        public float Frequency { get; } // Frequency in Hz
        public bool IsFinished { get; private set; } // Is the oscillator finished?
        public float LastSample { get; set; } // For filter state
    
        // Constructor 
        public Oscillator(float frequency, float amplitude, int sampleRate, WaveType waveType)
        {
            Frequency = frequency; // Set frequency
            _sampleRate = sampleRate; // Set sample rate
            _phaseIncrement = (float)(2.0 * Math.PI * frequency / _sampleRate); // Calculate phase increment
            _amplitude = amplitude; // Set amplitude
            _currentGain = 0f; // Start with zero gain for fade in
            _waveType = waveType; // Set waveform type
        }
        
        // Generate the next audio sample
        public float NextSample()
        {
            if (IsFinished) return 0; // Return silence if finished
            
            // Fade in
            if (!_stopping && _currentGain < 1.0f)
            {
                _currentGain += 0.01f; // Linear fade in
                if (_currentGain > 1.0f) _currentGain = 1.0f; // Clamp to max gain
            }
            
            // Generate a waveform sample based on type
            float rawSample = _waveType switch
            {
                WaveType.Sine => (float)Math.Sin(_phase), // Sine wave
                WaveType.Square => _phase < Math.PI ? 1.0f : -1.0f, // Square wave
                WaveType.Sawtooth => (float)(2.0 * (_phase / (2.0 * Math.PI)) - 1.0), // Sawtooth wave
                WaveType.Triangle => (float)(_phase < Math.PI ? (2.0 * (_phase / Math.PI) - 1.0) : (3.0 - 2.0 * (_phase / Math.PI))), // Triangle wave
                WaveType.Noise => (float)(_random.NextDouble() * 2.0 - 1.0), // White noise
                _ => 0
            };
            
            // Apply amplitude and current gain
            float sample = rawSample * _amplitude * _currentGain; // Final sample
            _phase += _phaseIncrement; // Increment phase
            if (_phase > 2.0 * Math.PI) _phase -= (float)(2.0 * Math.PI); // Wrap phase
            
            // Handle fade out
            if (_stopping)
            {
                _currentGain *= 0.995f; // Exponential fade out
                if (_currentGain < 0.001f) IsFinished = true; // Mark as finished
            }
            return sample;
        }
        
        // Stop the oscillator with fade out
        public void Stop()
        {
            _stopping = true;
        }
    }
}
