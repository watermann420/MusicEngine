//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Pitch synthesizer for resynthesizing modified polyphonic audio using phase vocoder with formant preservation.

using System;
using System.Linq;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Pitch synthesizer for resynthesizing audio from modified polyphonic notes.
/// Uses phase vocoder for pitch shifting with optional formant preservation.
/// </summary>
internal class PitchSynthesizer
{
    private const int DefaultFftSize = 4096;
    private const int DefaultHopSize = 256;
    private const int OverlapFactor = 4;

    /// <summary>
    /// Whether to preserve formants during pitch shifting.
    /// When true, maintains the spectral envelope (vocal character) independent of pitch.
    /// </summary>
    public bool PreserveFormants { get; set; } = true;

    /// <summary>
    /// FFT size for analysis/synthesis.
    /// </summary>
    public int FftSize { get; set; } = DefaultFftSize;

    /// <summary>
    /// Order of the spectral envelope estimation (affects formant preservation quality).
    /// </summary>
    public int EnvelopeOrder { get; set; } = 40;

    /// <summary>
    /// Quality factor for resynthesis (0.0 = fast, 1.0 = best quality).
    /// </summary>
    public float Quality { get; set; } = 0.8f;

    /// <summary>
    /// Resynthesizes audio from modified polyphonic analysis using phase vocoder.
    /// </summary>
    /// <param name="analysis">Analysis result with modified notes.</param>
    /// <param name="originalAudio">Original mono audio samples.</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    /// <returns>Resynthesized audio with pitch modifications applied.</returns>
    public float[] Synthesize(PolyphonicAnalysisResult analysis, float[] originalAudio, int sampleRate)
    {
        if (analysis == null || analysis.Voices.Count == 0)
            return (float[])originalAudio.Clone();

        // Check if any notes have been modified
        bool hasModifications = analysis.Voices.Any(v =>
            v.Notes.Any(n => n.IsModified));

        if (!hasModifications)
            return (float[])originalAudio.Clone();

        // Prepare output buffer
        float[] output = new float[originalAudio.Length];
        float[] residual = (float[])originalAudio.Clone();

        int hopSize = FftSize / OverlapFactor;

        // Process each voice
        foreach (var voice in analysis.Voices)
        {
            if (voice.IsMuted)
                continue;

            foreach (var note in voice.Notes)
            {
                if (!note.IsModified && Math.Abs(note.Formant) < 0.001f)
                {
                    // Unmodified note - copy original
                    continue;
                }

                // Extract note segment
                int startSample = (int)note.StartSample;
                int endSample = Math.Min((int)note.EndSample, originalAudio.Length);
                int noteLength = endSample - startSample;

                if (noteLength < FftSize)
                    continue;

                float[] noteSegment = new float[noteLength];
                Array.Copy(originalAudio, startSample, noteSegment, 0, noteLength);

                // Calculate pitch shift ratio
                float pitchRatio = MathF.Pow(2f, note.PitchDeviation / 12f);

                // Apply pitch shift with optional formant preservation
                float[] processedSegment;
                if (PreserveFormants && Math.Abs(note.Formant) > 0.001f)
                {
                    // Independent pitch and formant shifting
                    float formantRatio = MathF.Pow(2f, note.Formant / 12f);
                    processedSegment = ShiftPitchWithFormant(noteSegment, pitchRatio, formantRatio, sampleRate);
                }
                else if (PreserveFormants)
                {
                    // Pitch shift with formant preservation
                    processedSegment = ShiftPitchPreserveFormant(noteSegment, pitchRatio, sampleRate);
                }
                else
                {
                    // Simple pitch shift (formants shift with pitch)
                    processedSegment = ShiftPitch(noteSegment, pitchRatio, sampleRate);
                }

                // Apply volume adjustment
                float volume = voice.Volume;
                for (int i = 0; i < processedSegment.Length; i++)
                {
                    processedSegment[i] *= volume;
                }

                // Crossfade into output
                int fadeLength = Math.Min(hopSize, noteLength / 4);
                for (int i = 0; i < Math.Min(processedSegment.Length, noteLength); i++)
                {
                    int outIndex = startSample + i;
                    if (outIndex >= output.Length)
                        break;

                    float fade = 1f;
                    if (i < fadeLength)
                        fade = (float)i / fadeLength;
                    else if (i > noteLength - fadeLength)
                        fade = (float)(noteLength - i) / fadeLength;

                    output[outIndex] += processedSegment[i] * fade;
                    residual[outIndex] *= (1f - fade);
                }
            }
        }

        // Mix processed voices with residual
        for (int i = 0; i < output.Length; i++)
        {
            output[i] += residual[i];
        }

        // Normalize to prevent clipping
        NormalizeAudio(output);

        return output;
    }

    /// <summary>
    /// Shifts pitch using phase vocoder.
    /// </summary>
    public float[] ShiftPitch(float[] segment, float semitones, int sampleRate)
    {
        float ratio = MathF.Pow(2f, semitones / 12f);
        return ShiftPitchByRatio(segment, ratio, sampleRate);
    }

    /// <summary>
    /// Shifts pitch by ratio using phase vocoder.
    /// </summary>
    private float[] ShiftPitchByRatio(float[] segment, float pitchRatio, int sampleRate)
    {
        if (Math.Abs(pitchRatio - 1f) < 0.001f)
            return (float[])segment.Clone();

        int hopSize = FftSize / OverlapFactor;
        int numFrames = (segment.Length - FftSize) / hopSize + 1;

        if (numFrames < 2)
            return (float[])segment.Clone();

        // Analysis buffers
        Complex[] fftBuffer = new Complex[FftSize];
        float[] lastPhase = new float[FftSize / 2 + 1];
        float[] accumPhase = new float[FftSize / 2 + 1];
        float[] window = CreateHannWindow(FftSize);

        // Synthesis buffer (may be different length due to pitch shift)
        int outputLength = (int)(segment.Length / pitchRatio);
        float[] output = new float[outputLength + FftSize];
        int outputHop = (int)(hopSize / pitchRatio);

        float freqPerBin = (float)sampleRate / FftSize;
        float expectedPhaseDiff = 2f * MathF.PI * hopSize / FftSize;

        for (int frame = 0; frame < numFrames; frame++)
        {
            int inputOffset = frame * hopSize;
            int outputOffset = frame * outputHop;

            // Copy windowed input to FFT buffer
            for (int i = 0; i < FftSize; i++)
            {
                int idx = inputOffset + i;
                float sample = idx < segment.Length ? segment[idx] : 0;
                fftBuffer[i] = new Complex(sample * window[i], 0);
            }

            // Forward FFT
            FFT(fftBuffer, false);

            // Phase vocoder processing
            for (int k = 0; k <= FftSize / 2; k++)
            {
                float magnitude = MathF.Sqrt(fftBuffer[k].Real * fftBuffer[k].Real +
                                             fftBuffer[k].Imag * fftBuffer[k].Imag);
                float phase = MathF.Atan2(fftBuffer[k].Imag, fftBuffer[k].Real);

                // Calculate phase difference
                float phaseDiff = phase - lastPhase[k];
                lastPhase[k] = phase;

                // Remove expected phase increment
                phaseDiff -= k * expectedPhaseDiff;
                phaseDiff = WrapPhase(phaseDiff);

                // Calculate true frequency
                float deviation = phaseDiff * OverlapFactor / (2f * MathF.PI);
                float trueFreq = k + deviation;

                // Scale frequency by pitch ratio
                float newFreq = trueFreq * pitchRatio;

                // Accumulate phase for synthesis
                accumPhase[k] += newFreq * expectedPhaseDiff / pitchRatio;
                accumPhase[k] = WrapPhase(accumPhase[k]);

                // Set new magnitude and phase
                fftBuffer[k] = new Complex(
                    magnitude * MathF.Cos(accumPhase[k]),
                    magnitude * MathF.Sin(accumPhase[k]));

                // Mirror for negative frequencies
                if (k > 0 && k < FftSize / 2)
                {
                    fftBuffer[FftSize - k] = new Complex(
                        magnitude * MathF.Cos(accumPhase[k]),
                        -magnitude * MathF.Sin(accumPhase[k]));
                }
            }

            // Inverse FFT
            FFT(fftBuffer, true);

            // Overlap-add to output
            float normFactor = 1f / (OverlapFactor * 0.5f);
            for (int i = 0; i < FftSize; i++)
            {
                int outIdx = outputOffset + i;
                if (outIdx < output.Length)
                {
                    output[outIdx] += fftBuffer[i].Real * window[i] * normFactor;
                }
            }
        }

        // Trim output to expected length
        float[] result = new float[Math.Min(outputLength, output.Length)];
        Array.Copy(output, result, result.Length);

        return result;
    }

    /// <summary>
    /// Shifts pitch while preserving formants using spectral envelope.
    /// </summary>
    private float[] ShiftPitchPreserveFormant(float[] segment, float pitchRatio, int sampleRate)
    {
        if (Math.Abs(pitchRatio - 1f) < 0.001f)
            return (float[])segment.Clone();

        int hopSize = FftSize / OverlapFactor;
        int numFrames = (segment.Length - FftSize) / hopSize + 1;

        if (numFrames < 2)
            return (float[])segment.Clone();

        Complex[] fftBuffer = new Complex[FftSize];
        float[] lastPhase = new float[FftSize / 2 + 1];
        float[] accumPhase = new float[FftSize / 2 + 1];
        float[] window = CreateHannWindow(FftSize);
        float[] envelope = new float[FftSize / 2 + 1];

        int outputLength = (int)(segment.Length / pitchRatio);
        float[] output = new float[outputLength + FftSize];
        int outputHop = (int)(hopSize / pitchRatio);

        float expectedPhaseDiff = 2f * MathF.PI * hopSize / FftSize;

        for (int frame = 0; frame < numFrames; frame++)
        {
            int inputOffset = frame * hopSize;
            int outputOffset = frame * outputHop;

            // Copy windowed input
            for (int i = 0; i < FftSize; i++)
            {
                int idx = inputOffset + i;
                float sample = idx < segment.Length ? segment[idx] : 0;
                fftBuffer[i] = new Complex(sample * window[i], 0);
            }

            FFT(fftBuffer, false);

            // Extract spectral envelope using cepstral smoothing
            ExtractEnvelope(fftBuffer, envelope);

            // Process with formant preservation
            float[] magnitude = new float[FftSize / 2 + 1];
            float[] phase = new float[FftSize / 2 + 1];
            float[] newMagnitude = new float[FftSize / 2 + 1];

            for (int k = 0; k <= FftSize / 2; k++)
            {
                magnitude[k] = MathF.Sqrt(fftBuffer[k].Real * fftBuffer[k].Real +
                                          fftBuffer[k].Imag * fftBuffer[k].Imag);
                phase[k] = MathF.Atan2(fftBuffer[k].Imag, fftBuffer[k].Real);

                // Phase processing
                float phaseDiff = phase[k] - lastPhase[k];
                lastPhase[k] = phase[k];
                phaseDiff -= k * expectedPhaseDiff;
                phaseDiff = WrapPhase(phaseDiff);

                float deviation = phaseDiff * OverlapFactor / (2f * MathF.PI);
                float trueFreq = k + deviation;
                float newFreq = trueFreq * pitchRatio;

                accumPhase[k] += newFreq * expectedPhaseDiff / pitchRatio;
                accumPhase[k] = WrapPhase(accumPhase[k]);
            }

            // Shift spectrum with envelope correction
            for (int k = 0; k <= FftSize / 2; k++)
            {
                int sourceBin = (int)(k / pitchRatio);
                if (sourceBin <= FftSize / 2 && sourceBin >= 0)
                {
                    // Get original magnitude (without envelope)
                    float sourceEnv = envelope[sourceBin];
                    float flatMag = sourceEnv > 1e-10f ? magnitude[sourceBin] / sourceEnv : 0;

                    // Apply target envelope
                    newMagnitude[k] = flatMag * envelope[k];
                }
            }

            // Reconstruct FFT buffer
            for (int k = 0; k <= FftSize / 2; k++)
            {
                fftBuffer[k] = new Complex(
                    newMagnitude[k] * MathF.Cos(accumPhase[k]),
                    newMagnitude[k] * MathF.Sin(accumPhase[k]));

                if (k > 0 && k < FftSize / 2)
                {
                    fftBuffer[FftSize - k] = new Complex(
                        newMagnitude[k] * MathF.Cos(accumPhase[k]),
                        -newMagnitude[k] * MathF.Sin(accumPhase[k]));
                }
            }

            FFT(fftBuffer, true);

            float normFactor = 1f / (OverlapFactor * 0.5f);
            for (int i = 0; i < FftSize; i++)
            {
                int outIdx = outputOffset + i;
                if (outIdx < output.Length)
                {
                    output[outIdx] += fftBuffer[i].Real * window[i] * normFactor;
                }
            }
        }

        float[] result = new float[Math.Min(outputLength, output.Length)];
        Array.Copy(output, result, result.Length);
        return result;
    }

    /// <summary>
    /// Shifts pitch and formant independently.
    /// </summary>
    private float[] ShiftPitchWithFormant(float[] segment, float pitchRatio, float formantRatio, int sampleRate)
    {
        // First shift pitch
        float[] pitchShifted = ShiftPitchPreserveFormant(segment, pitchRatio, sampleRate);

        // Then shift formant (envelope only, not pitch)
        return ShiftFormant(pitchShifted, formantRatio, sampleRate);
    }

    /// <summary>
    /// Shifts formants without changing pitch.
    /// </summary>
    private float[] ShiftFormant(float[] segment, float formantRatio, int sampleRate)
    {
        if (Math.Abs(formantRatio - 1f) < 0.001f)
            return (float[])segment.Clone();

        int hopSize = FftSize / OverlapFactor;
        int numFrames = (segment.Length - FftSize) / hopSize + 1;

        if (numFrames < 2)
            return (float[])segment.Clone();

        Complex[] fftBuffer = new Complex[FftSize];
        float[] window = CreateHannWindow(FftSize);
        float[] envelope = new float[FftSize / 2 + 1];
        float[] output = new float[segment.Length];

        for (int frame = 0; frame < numFrames; frame++)
        {
            int offset = frame * hopSize;

            for (int i = 0; i < FftSize; i++)
            {
                int idx = offset + i;
                float sample = idx < segment.Length ? segment[idx] : 0;
                fftBuffer[i] = new Complex(sample * window[i], 0);
            }

            FFT(fftBuffer, false);

            // Extract envelope
            ExtractEnvelope(fftBuffer, envelope);

            // Shift envelope
            float[] newEnvelope = new float[FftSize / 2 + 1];
            for (int k = 0; k <= FftSize / 2; k++)
            {
                int sourceBin = (int)(k / formantRatio);
                if (sourceBin >= 0 && sourceBin <= FftSize / 2)
                {
                    newEnvelope[k] = envelope[sourceBin];
                }
            }

            // Apply new envelope
            for (int k = 0; k <= FftSize / 2; k++)
            {
                float magnitude = MathF.Sqrt(fftBuffer[k].Real * fftBuffer[k].Real +
                                             fftBuffer[k].Imag * fftBuffer[k].Imag);
                float phase = MathF.Atan2(fftBuffer[k].Imag, fftBuffer[k].Real);

                // Remove old envelope, apply new
                float flatMag = envelope[k] > 1e-10f ? magnitude / envelope[k] : 0;
                float newMag = flatMag * newEnvelope[k];

                fftBuffer[k] = new Complex(newMag * MathF.Cos(phase), newMag * MathF.Sin(phase));

                if (k > 0 && k < FftSize / 2)
                {
                    fftBuffer[FftSize - k] = new Complex(newMag * MathF.Cos(phase), -newMag * MathF.Sin(phase));
                }
            }

            FFT(fftBuffer, true);

            float normFactor = 1f / (OverlapFactor * 0.5f);
            for (int i = 0; i < FftSize; i++)
            {
                int outIdx = offset + i;
                if (outIdx < output.Length)
                {
                    output[outIdx] += fftBuffer[i].Real * window[i] * normFactor;
                }
            }
        }

        return output;
    }

    /// <summary>
    /// Time stretches audio without changing pitch.
    /// </summary>
    public float[] TimeStretch(float[] segment, double ratio, int sampleRate)
    {
        if (Math.Abs(ratio - 1.0) < 0.001)
            return (float[])segment.Clone();

        int hopSize = FftSize / OverlapFactor;
        int numFrames = (segment.Length - FftSize) / hopSize + 1;

        if (numFrames < 2)
            return (float[])segment.Clone();

        int outputHop = (int)(hopSize * ratio);
        int outputLength = (int)(segment.Length * ratio);
        float[] output = new float[outputLength + FftSize];

        Complex[] fftBuffer = new Complex[FftSize];
        float[] lastPhase = new float[FftSize / 2 + 1];
        float[] accumPhase = new float[FftSize / 2 + 1];
        float[] window = CreateHannWindow(FftSize);

        float expectedPhaseDiff = 2f * MathF.PI * hopSize / FftSize;

        for (int frame = 0; frame < numFrames; frame++)
        {
            int inputOffset = frame * hopSize;
            int outputOffset = (int)(frame * outputHop);

            for (int i = 0; i < FftSize; i++)
            {
                int idx = inputOffset + i;
                float sample = idx < segment.Length ? segment[idx] : 0;
                fftBuffer[i] = new Complex(sample * window[i], 0);
            }

            FFT(fftBuffer, false);

            // Phase vocoder for time stretching
            for (int k = 0; k <= FftSize / 2; k++)
            {
                float magnitude = MathF.Sqrt(fftBuffer[k].Real * fftBuffer[k].Real +
                                             fftBuffer[k].Imag * fftBuffer[k].Imag);
                float phase = MathF.Atan2(fftBuffer[k].Imag, fftBuffer[k].Real);

                float phaseDiff = phase - lastPhase[k];
                lastPhase[k] = phase;
                phaseDiff -= k * expectedPhaseDiff;
                phaseDiff = WrapPhase(phaseDiff);

                float deviation = phaseDiff * OverlapFactor / (2f * MathF.PI);
                float trueFreq = k + deviation;

                // Accumulate phase at output rate
                accumPhase[k] += trueFreq * 2f * MathF.PI * outputHop / FftSize;
                accumPhase[k] = WrapPhase(accumPhase[k]);

                fftBuffer[k] = new Complex(
                    magnitude * MathF.Cos(accumPhase[k]),
                    magnitude * MathF.Sin(accumPhase[k]));

                if (k > 0 && k < FftSize / 2)
                {
                    fftBuffer[FftSize - k] = new Complex(
                        magnitude * MathF.Cos(accumPhase[k]),
                        -magnitude * MathF.Sin(accumPhase[k]));
                }
            }

            FFT(fftBuffer, true);

            float normFactor = 1f / (OverlapFactor * 0.5f);
            for (int i = 0; i < FftSize; i++)
            {
                int outIdx = outputOffset + i;
                if (outIdx < output.Length)
                {
                    output[outIdx] += fftBuffer[i].Real * window[i] * normFactor;
                }
            }
        }

        float[] result = new float[outputLength];
        Array.Copy(output, result, Math.Min(outputLength, output.Length));
        return result;
    }

    /// <summary>
    /// Extracts spectral envelope using cepstral smoothing.
    /// </summary>
    private void ExtractEnvelope(Complex[] spectrum, float[] envelope)
    {
        int halfSize = FftSize / 2;

        // Calculate log magnitude
        float[] logMag = new float[FftSize];
        for (int i = 0; i < FftSize; i++)
        {
            float mag = MathF.Sqrt(spectrum[i].Real * spectrum[i].Real +
                                   spectrum[i].Imag * spectrum[i].Imag);
            logMag[i] = MathF.Log(mag + 1e-10f);
        }

        // Transform to cepstrum
        Complex[] cepstrum = new Complex[FftSize];
        for (int i = 0; i < FftSize; i++)
        {
            cepstrum[i] = new Complex(logMag[i], 0);
        }
        FFT(cepstrum, false);

        // Lifter: keep only low-order cepstral coefficients
        for (int i = EnvelopeOrder; i < FftSize - EnvelopeOrder; i++)
        {
            cepstrum[i] = new Complex(0, 0);
        }

        // Transform back
        FFT(cepstrum, true);

        // Convert to linear envelope
        for (int k = 0; k <= halfSize; k++)
        {
            envelope[k] = MathF.Exp(cepstrum[k].Real / FftSize);
        }
    }

    /// <summary>
    /// Creates a Hann window.
    /// </summary>
    private static float[] CreateHannWindow(int size)
    {
        float[] window = new float[size];
        for (int i = 0; i < size; i++)
        {
            window[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (size - 1)));
        }
        return window;
    }

    /// <summary>
    /// Wraps phase to [-PI, PI].
    /// </summary>
    private static float WrapPhase(float phase)
    {
        while (phase > MathF.PI) phase -= 2f * MathF.PI;
        while (phase < -MathF.PI) phase += 2f * MathF.PI;
        return phase;
    }

    /// <summary>
    /// Normalizes audio to prevent clipping.
    /// </summary>
    private static void NormalizeAudio(float[] audio)
    {
        float maxAbs = 0;
        for (int i = 0; i < audio.Length; i++)
        {
            float abs = MathF.Abs(audio[i]);
            if (abs > maxAbs) maxAbs = abs;
        }

        if (maxAbs > 0.99f)
        {
            float scale = 0.99f / maxAbs;
            for (int i = 0; i < audio.Length; i++)
            {
                audio[i] *= scale;
            }
        }
    }

    /// <summary>
    /// In-place Cooley-Tukey FFT.
    /// </summary>
    private static void FFT(Complex[] data, bool inverse)
    {
        int n = data.Length;
        if (n <= 1) return;

        // Bit-reversal permutation
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (data[i], data[j]) = (data[j], data[i]);
            }
            int m = n >> 1;
            while (j >= m && m >= 1)
            {
                j -= m;
                m >>= 1;
            }
            j += m;
        }

        // Cooley-Tukey iterative FFT
        float direction = inverse ? 1f : -1f;
        for (int len = 2; len <= n; len <<= 1)
        {
            float theta = direction * 2f * MathF.PI / len;
            Complex wn = new Complex(MathF.Cos(theta), MathF.Sin(theta));

            for (int i = 0; i < n; i += len)
            {
                Complex w = new Complex(1f, 0f);
                int halfLen = len / 2;
                for (int k = 0; k < halfLen; k++)
                {
                    Complex t = w * data[i + k + halfLen];
                    Complex u = data[i + k];
                    data[i + k] = u + t;
                    data[i + k + halfLen] = u - t;
                    w = w * wn;
                }
            }
        }

        if (inverse)
        {
            for (int i = 0; i < n; i++)
            {
                data[i] = new Complex(data[i].Real / n, data[i].Imag / n);
            }
        }
    }

    /// <summary>
    /// Simple complex number struct.
    /// </summary>
    private readonly struct Complex
    {
        public readonly float Real;
        public readonly float Imag;

        public Complex(float real, float imag)
        {
            Real = real;
            Imag = imag;
        }

        public static Complex operator +(Complex a, Complex b)
            => new(a.Real + b.Real, a.Imag + b.Imag);

        public static Complex operator -(Complex a, Complex b)
            => new(a.Real - b.Real, a.Imag - b.Imag);

        public static Complex operator *(Complex a, Complex b)
            => new(a.Real * b.Real - a.Imag * b.Imag,
                   a.Real * b.Imag + a.Imag * b.Real);
    }
}
