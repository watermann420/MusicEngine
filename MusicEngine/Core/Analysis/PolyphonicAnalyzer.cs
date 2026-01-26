// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio analysis component.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Result of polyphonic audio analysis containing extracted voices and notes.
/// </summary>
public class PolyphonicAnalysisResult
{
    /// <summary>
    /// List of detected voices/melodic strands, each containing a sequence of notes.
    /// </summary>
    public List<PolyphonicVoice> Voices { get; } = new();

    /// <summary>
    /// Total duration of the analyzed audio in seconds.
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// Sample rate of the analyzed audio.
    /// </summary>
    public int SampleRate { get; set; }

    /// <summary>
    /// Gets the total number of notes across all voices.
    /// </summary>
    public int TotalNoteCount => Voices.Sum(v => v.NoteCount);

    /// <summary>
    /// Gets all notes from all voices, ordered by start time.
    /// </summary>
    public IEnumerable<PolyphonicNote> AllNotes => Voices
        .SelectMany(v => v.Notes)
        .OrderBy(n => n.StartTime);

    /// <summary>
    /// Gets the note at a specific time across all voices.
    /// Returns multiple notes if they overlap.
    /// </summary>
    /// <param name="time">Time in seconds.</param>
    /// <returns>All notes active at the specified time.</returns>
    public IEnumerable<PolyphonicNote> GetNotesAt(double time)
    {
        return Voices
            .Select(v => v.GetNoteAt(time))
            .Where(n => n != null)!;
    }

    /// <summary>
    /// Gets notes within a time range across all voices.
    /// </summary>
    public IEnumerable<PolyphonicNote> GetNotesInRange(double startTime, double endTime)
    {
        return Voices
            .SelectMany(v => v.GetNotesInRange(startTime, endTime));
    }
}

/// <summary>
/// Internal class representing a pitch candidate during analysis.
/// </summary>
internal class PitchCandidate
{
    /// <summary>
    /// Detected fundamental frequency in Hz.
    /// </summary>
    public float Frequency { get; set; }

    /// <summary>
    /// Amplitude/magnitude of this pitch.
    /// </summary>
    public float Amplitude { get; set; }

    /// <summary>
    /// Salience/prominence of this pitch (how much it stands out).
    /// </summary>
    public float Salience { get; set; }

    /// <summary>
    /// Frame index where this pitch was detected.
    /// </summary>
    public int FrameIndex { get; set; }

    /// <summary>
    /// MIDI note number (can be fractional).
    /// </summary>
    public float MidiNote => FrequencyToMidiNote(Frequency);

    /// <summary>
    /// Converts frequency to MIDI note number.
    /// </summary>
    private static float FrequencyToMidiNote(float frequency)
    {
        if (frequency <= 0)
            return 0;
        return 69f + 12f * MathF.Log2(frequency / 440f);
    }
}

/// <summary>
/// Internal class for tracking pitches across frames to form notes.
/// </summary>
internal class PitchTrack
{
    public List<PitchCandidate> Candidates { get; } = new();
    public int StartFrame { get; set; }
    public int EndFrame { get; set; }
    public int VoiceIndex { get; set; }
    public bool IsActive { get; set; } = true;

    public float AverageFrequency => Candidates.Count > 0
        ? Candidates.Average(c => c.Frequency)
        : 0;

    public float AverageAmplitude => Candidates.Count > 0
        ? Candidates.Average(c => c.Amplitude)
        : 0;

    public float MaxAmplitude => Candidates.Count > 0
        ? Candidates.Max(c => c.Amplitude)
        : 0;
}

/// <summary>
/// Polyphonic audio analyzer that extracts individual notes and voices from mixed audio.
/// Uses Harmonic Product Spectrum (HPS) algorithm for robust multi-pitch detection.
/// Similar to Melodyne DNA technology for polyphonic pitch analysis.
/// </summary>
public class PolyphonicAnalyzer
{
    private readonly object _lock = new();

    /// <summary>
    /// FFT size for spectral analysis. Larger = better frequency resolution, worse time resolution.
    /// Must be a power of 2.
    /// </summary>
    public int FftSize { get; set; } = 4096;

    /// <summary>
    /// Hop size between analysis frames in samples.
    /// Smaller = better time resolution, more computation.
    /// </summary>
    public int HopSize { get; set; } = 256;

    /// <summary>
    /// Minimum note length in seconds. Notes shorter than this are filtered out.
    /// </summary>
    public float MinNoteLength { get; set; } = 0.05f;

    /// <summary>
    /// Maximum number of simultaneous voices to detect.
    /// </summary>
    public int MaxVoices { get; set; } = 8;

    /// <summary>
    /// Minimum frequency to detect (in Hz).
    /// </summary>
    public float MinFrequency { get; set; } = 50f;

    /// <summary>
    /// Maximum frequency to detect (in Hz).
    /// </summary>
    public float MaxFrequency { get; set; } = 4000f;

    /// <summary>
    /// Amplitude threshold for pitch detection (0.0 to 1.0).
    /// </summary>
    public float AmplitudeThreshold { get; set; } = 0.01f;

    /// <summary>
    /// Number of harmonics to use in Harmonic Product Spectrum.
    /// More harmonics = more robust detection but slower.
    /// </summary>
    public int HarmonicCount { get; set; } = 5;

    /// <summary>
    /// Maximum pitch deviation (in semitones) for continuing a track.
    /// </summary>
    public float MaxPitchDeviation { get; set; } = 1.5f;

    /// <summary>
    /// Number of contour points per note for detailed pitch tracking.
    /// </summary>
    public int ContourResolution { get; set; } = 64;

    /// <summary>
    /// Event raised when analysis progress updates.
    /// </summary>
    public event EventHandler<float>? ProgressChanged;

    /// <summary>
    /// Analyzes polyphonic audio and extracts individual notes/voices.
    /// </summary>
    /// <param name="audioData">Mono audio samples.</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    /// <returns>Analysis result containing detected voices and notes.</returns>
    public PolyphonicAnalysisResult Analyze(float[] audioData, int sampleRate)
    {
        if (audioData == null || audioData.Length == 0)
            throw new ArgumentException("Audio data cannot be null or empty.", nameof(audioData));

        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");

        var result = new PolyphonicAnalysisResult
        {
            SampleRate = sampleRate,
            Duration = (double)audioData.Length / sampleRate
        };

        // Step 1: Compute spectrogram and detect pitches per frame
        var framePitches = new List<List<PitchCandidate>>();
        int totalFrames = (audioData.Length - FftSize) / HopSize + 1;

        for (int frame = 0; frame < totalFrames; frame++)
        {
            int offset = frame * HopSize;
            float[] frameData = new float[FftSize];
            int copyLength = Math.Min(FftSize, audioData.Length - offset);
            Array.Copy(audioData, offset, frameData, 0, copyLength);

            // Apply Hann window
            ApplyHannWindow(frameData);

            // Detect pitches in this frame using HPS
            var pitches = DetectPitches(frameData, sampleRate);
            foreach (var pitch in pitches)
            {
                pitch.FrameIndex = frame;
            }
            framePitches.Add(pitches);

            // Report progress
            if (frame % 100 == 0)
            {
                ProgressChanged?.Invoke(this, (float)frame / totalFrames * 0.5f);
            }
        }

        // Step 2: Track pitches across frames to form note tracks
        var tracks = TrackPitches(framePitches, sampleRate);
        ProgressChanged?.Invoke(this, 0.7f);

        // Step 3: Convert tracks to notes and group into voices
        SegmentIntoNotes(tracks, result, audioData, sampleRate, totalFrames);
        ProgressChanged?.Invoke(this, 0.9f);

        // Step 4: Extract pitch and amplitude contours for each note
        ExtractContours(result, audioData, sampleRate);
        ProgressChanged?.Invoke(this, 1.0f);

        return result;
    }

    /// <summary>
    /// Detects multiple pitches in a single frame using Harmonic Product Spectrum.
    /// </summary>
    private List<PitchCandidate> DetectPitches(float[] frame, int sampleRate)
    {
        var candidates = new List<PitchCandidate>();

        // Compute FFT magnitude spectrum
        int fftLength = frame.Length;
        float[] magnitude = ComputeMagnitudeSpectrum(frame);

        // Compute Harmonic Product Spectrum
        float[] hps = ComputeHPS(magnitude, sampleRate);

        // Find peaks in HPS that correspond to fundamental frequencies
        float binResolution = (float)sampleRate / fftLength;
        int minBin = Math.Max(1, (int)(MinFrequency / binResolution));
        int maxBin = Math.Min(hps.Length - 1, (int)(MaxFrequency / binResolution));

        // Find local maxima
        var peaks = new List<(int bin, float value)>();
        for (int bin = minBin + 1; bin < maxBin - 1; bin++)
        {
            if (hps[bin] > hps[bin - 1] && hps[bin] > hps[bin + 1] && hps[bin] > AmplitudeThreshold)
            {
                peaks.Add((bin, hps[bin]));
            }
        }

        // Sort by magnitude and take top candidates
        var topPeaks = peaks
            .OrderByDescending(p => p.value)
            .Take(MaxVoices)
            .ToList();

        foreach (var (bin, value) in topPeaks)
        {
            // Parabolic interpolation for sub-bin precision
            float refinedBin = ParabolicInterpolation(hps, bin);
            float frequency = refinedBin * binResolution;

            // Calculate salience based on harmonic strength
            float salience = CalculateSalience(magnitude, bin, sampleRate);

            // Get amplitude from original magnitude spectrum
            float amplitude = magnitude[bin];

            candidates.Add(new PitchCandidate
            {
                Frequency = frequency,
                Amplitude = amplitude,
                Salience = salience
            });
        }

        return candidates;
    }

    /// <summary>
    /// Computes the magnitude spectrum using FFT.
    /// </summary>
    private float[] ComputeMagnitudeSpectrum(float[] frame)
    {
        int n = frame.Length;
        Complex[] fftData = new Complex[n];

        for (int i = 0; i < n; i++)
        {
            fftData[i] = new Complex(frame[i], 0);
        }

        FFT(fftData, false);

        float[] magnitude = new float[n / 2 + 1];
        for (int i = 0; i <= n / 2; i++)
        {
            magnitude[i] = MathF.Sqrt(fftData[i].Real * fftData[i].Real + fftData[i].Imag * fftData[i].Imag);
        }

        return magnitude;
    }

    /// <summary>
    /// Computes the Harmonic Product Spectrum for multi-pitch detection.
    /// </summary>
    private float[] ComputeHPS(float[] magnitude, int sampleRate)
    {
        int length = magnitude.Length / HarmonicCount;
        float[] hps = new float[length];

        // Initialize with original spectrum
        Array.Copy(magnitude, hps, length);

        // Multiply by downsampled versions for each harmonic
        for (int h = 2; h <= HarmonicCount; h++)
        {
            for (int i = 0; i < length; i++)
            {
                int harmonicBin = i * h;
                if (harmonicBin < magnitude.Length)
                {
                    hps[i] *= magnitude[harmonicBin];
                }
            }
        }

        // Normalize
        float maxVal = hps.Max();
        if (maxVal > 0)
        {
            for (int i = 0; i < length; i++)
            {
                hps[i] /= maxVal;
            }
        }

        return hps;
    }

    /// <summary>
    /// Calculates the salience (prominence) of a pitch based on its harmonics.
    /// </summary>
    private float CalculateSalience(float[] magnitude, int fundamentalBin, int sampleRate)
    {
        float salience = 0;
        float weightSum = 0;

        for (int h = 1; h <= HarmonicCount; h++)
        {
            int harmonicBin = fundamentalBin * h;
            if (harmonicBin < magnitude.Length)
            {
                float weight = 1f / h; // Higher harmonics have less weight
                salience += magnitude[harmonicBin] * weight;
                weightSum += weight;
            }
        }

        return weightSum > 0 ? salience / weightSum : 0;
    }

    /// <summary>
    /// Parabolic interpolation for sub-bin frequency precision.
    /// </summary>
    private static float ParabolicInterpolation(float[] data, int peakIndex)
    {
        if (peakIndex <= 0 || peakIndex >= data.Length - 1)
            return peakIndex;

        float alpha = data[peakIndex - 1];
        float beta = data[peakIndex];
        float gamma = data[peakIndex + 1];

        float p = 0.5f * (alpha - gamma) / (alpha - 2 * beta + gamma);
        return peakIndex + p;
    }

    /// <summary>
    /// Tracks pitches across frames to form continuous pitch tracks.
    /// </summary>
    private List<PitchTrack> TrackPitches(List<List<PitchCandidate>> framePitches, int sampleRate)
    {
        var activeTracks = new List<PitchTrack>();
        var completedTracks = new List<PitchTrack>();

        for (int frameIndex = 0; frameIndex < framePitches.Count; frameIndex++)
        {
            var candidates = framePitches[frameIndex];
            var usedCandidates = new HashSet<int>();
            var matchedTracks = new HashSet<int>();

            // Match candidates to existing tracks
            foreach (var candidate in candidates.OrderByDescending(c => c.Amplitude))
            {
                PitchTrack? bestTrack = null;
                float bestDistance = float.MaxValue;
                int bestTrackIndex = -1;

                for (int t = 0; t < activeTracks.Count; t++)
                {
                    if (matchedTracks.Contains(t))
                        continue;

                    var track = activeTracks[t];
                    if (!track.IsActive)
                        continue;

                    // Calculate pitch distance in semitones
                    float lastFreq = track.Candidates[^1].Frequency;
                    float distance = Math.Abs(12f * MathF.Log2(candidate.Frequency / lastFreq));

                    if (distance < MaxPitchDeviation && distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestTrack = track;
                        bestTrackIndex = t;
                    }
                }

                if (bestTrack != null && bestTrackIndex >= 0)
                {
                    // Extend existing track
                    bestTrack.Candidates.Add(candidate);
                    bestTrack.EndFrame = frameIndex;
                    matchedTracks.Add(bestTrackIndex);
                    usedCandidates.Add(candidates.IndexOf(candidate));
                }
            }

            // Start new tracks for unmatched candidates
            foreach (var candidate in candidates)
            {
                if (!usedCandidates.Contains(candidates.IndexOf(candidate)))
                {
                    var newTrack = new PitchTrack
                    {
                        StartFrame = frameIndex,
                        EndFrame = frameIndex
                    };
                    newTrack.Candidates.Add(candidate);
                    activeTracks.Add(newTrack);
                }
            }

            // Close tracks that weren't matched
            for (int t = activeTracks.Count - 1; t >= 0; t--)
            {
                var track = activeTracks[t];
                if (!matchedTracks.Contains(t) && track.IsActive)
                {
                    // Allow a small gap before closing
                    if (frameIndex - track.EndFrame > 3)
                    {
                        track.IsActive = false;
                        completedTracks.Add(track);
                        activeTracks.RemoveAt(t);
                    }
                }
            }
        }

        // Add remaining active tracks
        completedTracks.AddRange(activeTracks);

        return completedTracks;
    }

    /// <summary>
    /// Segments pitch tracks into notes and groups them into voices.
    /// </summary>
    private void SegmentIntoNotes(
        List<PitchTrack> tracks,
        PolyphonicAnalysisResult result,
        float[] audioData,
        int sampleRate,
        int totalFrames)
    {
        float frameToSeconds = (float)HopSize / sampleRate;
        float minFrames = MinNoteLength / frameToSeconds;

        // Filter tracks by minimum length
        var validTracks = tracks
            .Where(t => t.Candidates.Count >= minFrames)
            .OrderBy(t => t.StartFrame)
            .ThenByDescending(t => t.MaxAmplitude)
            .ToList();

        // Assign voices based on pitch and timing
        var voiceAssignments = AssignVoices(validTracks);

        // Create voice objects
        for (int v = 0; v < MaxVoices; v++)
        {
            result.Voices.Add(new PolyphonicVoice(v));
        }

        // Convert tracks to notes
        foreach (var track in validTracks)
        {
            int voiceIndex = voiceAssignments[track];
            if (voiceIndex >= result.Voices.Count)
                continue;

            var voice = result.Voices[voiceIndex];
            float avgFreq = track.AverageFrequency;
            float avgAmp = track.AverageAmplitude;

            var note = new PolyphonicNote(PolyphonicNote.FrequencyToMidiNote(avgFreq))
            {
                StartTime = track.StartFrame * frameToSeconds,
                EndTime = (track.EndFrame + 1) * frameToSeconds,
                Amplitude = avgAmp,
                VoiceIndex = voiceIndex,
                StartSample = track.StartFrame * HopSize,
                EndSample = (track.EndFrame + 1) * HopSize
            };

            voice.AddNote(note);
        }

        // Remove empty voices
        result.Voices.RemoveAll(v => v.Notes.Count == 0);

        // Re-index voices
        for (int i = 0; i < result.Voices.Count; i++)
        {
            var voice = result.Voices[i];
            foreach (var note in voice.Notes)
            {
                note.VoiceIndex = i;
            }
        }
    }

    /// <summary>
    /// Assigns voice indices to tracks based on pitch proximity and timing.
    /// </summary>
    private Dictionary<PitchTrack, int> AssignVoices(List<PitchTrack> tracks)
    {
        var assignments = new Dictionary<PitchTrack, int>();
        var voiceLastPitch = new float[MaxVoices];
        var voiceLastEnd = new int[MaxVoices];

        for (int i = 0; i < MaxVoices; i++)
        {
            voiceLastPitch[i] = float.NaN;
            voiceLastEnd[i] = -1000;
        }

        foreach (var track in tracks.OrderBy(t => t.StartFrame))
        {
            float trackPitch = track.Candidates[0].MidiNote;
            int bestVoice = 0;
            float bestScore = float.MaxValue;

            for (int v = 0; v < MaxVoices; v++)
            {
                float score;
                if (float.IsNaN(voiceLastPitch[v]))
                {
                    // Empty voice - prefer based on voice index (lower = higher pitch typically)
                    score = v * 0.1f;
                }
                else
                {
                    // Score based on pitch similarity and time gap
                    float pitchDiff = Math.Abs(trackPitch - voiceLastPitch[v]);
                    float timeDiff = Math.Max(0, track.StartFrame - voiceLastEnd[v]);
                    score = pitchDiff + timeDiff * 0.01f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestVoice = v;
                }
            }

            assignments[track] = bestVoice;
            voiceLastPitch[bestVoice] = track.Candidates[^1].MidiNote;
            voiceLastEnd[bestVoice] = track.EndFrame;
        }

        return assignments;
    }

    /// <summary>
    /// Extracts detailed pitch and amplitude contours for each note.
    /// </summary>
    private void ExtractContours(PolyphonicAnalysisResult result, float[] audioData, int sampleRate)
    {
        foreach (var voice in result.Voices)
        {
            foreach (var note in voice.Notes)
            {
                ExtractNoteContour(note, audioData, sampleRate);
            }
        }
    }

    /// <summary>
    /// Extracts pitch and amplitude contour for a single note.
    /// </summary>
    private void ExtractNoteContour(PolyphonicNote note, float[] audioData, int sampleRate)
    {
        int startSample = (int)note.StartSample;
        int endSample = (int)Math.Min(note.EndSample, audioData.Length);
        int noteSamples = endSample - startSample;

        if (noteSamples < FftSize)
        {
            note.PitchContour = new float[] { note.Pitch };
            note.AmplitudeContour = new float[] { note.Amplitude };
            return;
        }

        // Calculate contour points
        int points = Math.Min(ContourResolution, noteSamples / HopSize);
        points = Math.Max(2, points);

        note.PitchContour = new float[points];
        note.AmplitudeContour = new float[points];

        float[] frameData = new float[FftSize];
        float targetFreq = 440f * MathF.Pow(2f, (note.OriginalPitch - 69f) / 12f);

        for (int i = 0; i < points; i++)
        {
            float t = (float)i / (points - 1);
            int sampleOffset = startSample + (int)(t * (noteSamples - FftSize));

            // Extract frame
            int copyLength = Math.Min(FftSize, audioData.Length - sampleOffset);
            if (copyLength > 0)
            {
                Array.Copy(audioData, sampleOffset, frameData, 0, copyLength);
                if (copyLength < FftSize)
                    Array.Clear(frameData, copyLength, FftSize - copyLength);

                ApplyHannWindow(frameData);

                // Detect pitch at this point
                var candidates = DetectPitches(frameData, sampleRate);
                var bestCandidate = candidates
                    .OrderBy(c => Math.Abs(c.MidiNote - note.OriginalPitch))
                    .FirstOrDefault();

                if (bestCandidate != null && Math.Abs(bestCandidate.MidiNote - note.OriginalPitch) < 3f)
                {
                    note.PitchContour[i] = bestCandidate.MidiNote;
                    note.AmplitudeContour[i] = bestCandidate.Amplitude;
                }
                else
                {
                    // Use interpolation from neighbors
                    note.PitchContour[i] = note.OriginalPitch;
                    note.AmplitudeContour[i] = note.Amplitude;
                }
            }
        }

        // Detect vibrato from pitch contour
        note.Vibrato = CalculateVibrato(note.PitchContour);
    }

    /// <summary>
    /// Calculates vibrato amount from pitch contour.
    /// </summary>
    private static float CalculateVibrato(float[] contour)
    {
        if (contour.Length < 4)
            return 0;

        // Calculate variance from median
        float median = contour.OrderBy(x => x).ElementAt(contour.Length / 2);
        float variance = contour.Average(x => (x - median) * (x - median));

        // Map variance to vibrato amount (0-1)
        // Typical vibrato is about +/- 0.5 semitones
        return Math.Clamp(MathF.Sqrt(variance) / 0.5f, 0f, 1f);
    }

    /// <summary>
    /// Applies a Hann window to the frame.
    /// </summary>
    private static void ApplyHannWindow(float[] frame)
    {
        int n = frame.Length;
        for (int i = 0; i < n; i++)
        {
            float window = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (n - 1)));
            frame[i] *= window;
        }
    }

    /// <summary>
    /// In-place Cooley-Tukey FFT implementation.
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

        // Scale for inverse FFT
        if (inverse)
        {
            for (int i = 0; i < n; i++)
            {
                data[i] = new Complex(data[i].Real / n, data[i].Imag / n);
            }
        }
    }

    /// <summary>
    /// Simple complex number struct for FFT operations.
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
        {
            return new Complex(a.Real + b.Real, a.Imag + b.Imag);
        }

        public static Complex operator -(Complex a, Complex b)
        {
            return new Complex(a.Real - b.Real, a.Imag - b.Imag);
        }

        public static Complex operator *(Complex a, Complex b)
        {
            return new Complex(
                a.Real * b.Real - a.Imag * b.Imag,
                a.Real * b.Imag + a.Imag * b.Real
            );
        }
    }
}
