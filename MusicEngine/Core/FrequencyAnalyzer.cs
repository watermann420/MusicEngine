// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Equalizer effect.

using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Dsp;


namespace MusicEngine.Core;


public class FrequencyAnalyzer
{
    private readonly int _fftLength; // Must be a power of two
    private readonly Complex[] _fftBuffer; // Buffer for FFT input
    private readonly float[] _prevSamples; // Buffer for previous samples
    private int _sampleCount; // Current number of samples in the buffer
    private readonly int _sampleRate; // Sample rate of the audio
    
    // Constructor
    public FrequencyAnalyzer(int? fftLength = null, int sampleRate = 44100)
    {
        int length = fftLength ?? Settings.FftSize; // Use provided or default FFT size
        if (!IsPowerOfTwo(length)) // Validate power of two
            throw new ArgumentException("FFT Length must be a power of two."); 

        _fftLength = length; // Set FFT length
        _sampleRate = sampleRate; // Set sample rate
        _fftBuffer = new Complex[length]; // Initialize FFT buffer
        _prevSamples = new float[length]; // Initialize previous samples buffer
    }
    
    // Check if a number is a power of two
    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;
    
    // Add samples to the analyzer
    public void AddSamples(float[] samples, int count)
    {
        for (int i = 0; i < count; i++)
        {
            _fftBuffer[_sampleCount].X = (float)(samples[i] * FastFourierTransform.HammingWindow(_sampleCount, _fftLength)); // Apply a Hamming window
            _fftBuffer[_sampleCount].Y = 0; // Imaginary part is zero
            _sampleCount++; // Increment sample count

            if (_sampleCount >= _fftLength)
            {
                ProcessFft(); // Process FFT when the buffer is full
                _sampleCount = 0; // Reset sample count
            }
        }
    }
    
    // Event triggered when FFT is calculated
    public event Action<float[]>? FftCalculated;
    
    // Process the FFT
    private void ProcessFft()
    {
        int m = (int)Math.Log(_fftLength, 2.0); // Calculate log2 of FFT length
        FastFourierTransform.FFT(true, m, _fftBuffer); // Perform FFT

        float[] magnitudes = new float[_fftLength / 2]; // Array for magnitudes
        for (int i = 0; i < _fftLength / 2; i++) 
        {
            magnitudes[i] = (float)Math.Sqrt(_fftBuffer[i].X * _fftBuffer[i].X + _fftBuffer[i].Y * _fftBuffer[i].Y); // Calculate magnitude
        }

        FftCalculated?.Invoke(magnitudes); // Trigger event with magnitudes
    }
    
    // Get average magnitude for a frequency range
    public float GetMagnitudeForRange(float[] magnitudes, float lowFreq, float highFreq)
    {
        int lowBin = (int)(lowFreq * _fftLength / _sampleRate); // Calculate low bin
        int highBin = (int)(highFreq * _fftLength / _sampleRate); // Calculate high bin

        lowBin = Math.Clamp(lowBin, 0, magnitudes.Length - 1); // Clamp to valid range
        highBin = Math.Clamp(highBin, 0, magnitudes.Length - 1); // Clamp to valid range

        float sum = 0; // Sum of magnitudes
        for (int i = lowBin; i <= highBin; i++) 
        {
            sum += magnitudes[i]; // Accumulate magnitudes
        }
        
        int count = highBin - lowBin + 1; // Number of bins
        return count > 0 ? sum / count : 0; // Return average magnitude
    }
}
