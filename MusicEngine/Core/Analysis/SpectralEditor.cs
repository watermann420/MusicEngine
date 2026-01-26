//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Spectral editor for visualizing and editing audio in the frequency domain using STFT.

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Provides spectral editing capabilities using Short-Time Fourier Transform (STFT).
/// Enables visualization and manipulation of audio in the time-frequency domain,
/// similar to tools like iZotope RX or SpectraLayers.
/// </summary>
/// <remarks>
/// Features:
/// - STFT analysis with configurable FFT size and hop size
/// - Time-frequency selection and manipulation
/// - Operations: erase, amplify, frequency shift, harmonic enhance, noise reduce
/// - Undo/redo support for all operations
/// - Phase vocoder synthesis for artifact-free reconstruction
/// </remarks>
public class SpectralEditor : IDisposable
{
    private readonly List<SpectralFrame> _frames = new();
    private readonly Stack<SpectralOperation> _undoStack = new();
    private readonly Stack<SpectralOperation> _redoStack = new();
    private int _fftSize = 4096;
    private int _hopSize = 1024;
    private int _sampleRate;
    private bool _disposed;

    // Analysis window
    private float[] _analysisWindow = null!;
    private float[] _synthesisWindow = null!;

    // Original audio for comparison
    private float[]? _originalAudio;

    /// <summary>
    /// Gets or sets the FFT size for analysis (must be power of 2).
    /// Larger values provide better frequency resolution but worse time resolution.
    /// </summary>
    public int FftSize
    {
        get => _fftSize;
        set
        {
            if (!IsPowerOfTwo(value))
                throw new ArgumentException("FFT size must be a power of two.");
            if (value < 256 || value > 16384)
                throw new ArgumentOutOfRangeException(nameof(value), "FFT size must be between 256 and 16384.");
            _fftSize = value;
            InitializeWindows();
        }
    }

    /// <summary>
    /// Gets or sets the hop size (analysis stride) in samples.
    /// Smaller values provide better time resolution but more frames.
    /// </summary>
    public int HopSize
    {
        get => _hopSize;
        set
        {
            if (value < 1 || value > _fftSize)
                throw new ArgumentOutOfRangeException(nameof(value), "Hop size must be between 1 and FFT size.");
            _hopSize = value;
        }
    }

    /// <summary>
    /// Gets the number of frames in the current analysis.
    /// </summary>
    public int FrameCount => _frames.Count;

    /// <summary>
    /// Gets the sample rate of the analyzed audio.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the total duration of the analyzed audio in seconds.
    /// </summary>
    public double Duration => _frames.Count > 0 ? _frames[^1].TimePosition + (double)_hopSize / _sampleRate : 0;

    /// <summary>
    /// Gets whether undo is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Gets whether redo is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Gets the number of undoable operations.
    /// </summary>
    public int UndoCount => _undoStack.Count;

    /// <summary>
    /// Gets the number of redoable operations.
    /// </summary>
    public int RedoCount => _redoStack.Count;

    /// <summary>
    /// Event raised when analysis is complete.
    /// </summary>
    public event EventHandler? AnalysisComplete;

    /// <summary>
    /// Event raised when an operation is applied.
    /// </summary>
    public event EventHandler<SpectralOperation>? OperationApplied;

    /// <summary>
    /// Creates a new spectral editor with default settings.
    /// </summary>
    public SpectralEditor()
    {
        InitializeWindows();
    }

    /// <summary>
    /// Creates a new spectral editor with specified FFT settings.
    /// </summary>
    /// <param name="fftSize">FFT size (must be power of 2)</param>
    /// <param name="hopSize">Hop size in samples</param>
    public SpectralEditor(int fftSize, int hopSize)
    {
        _fftSize = fftSize;
        _hopSize = hopSize;
        InitializeWindows();
    }

    private void InitializeWindows()
    {
        // Hann window for analysis
        _analysisWindow = new float[_fftSize];
        for (int i = 0; i < _fftSize; i++)
        {
            _analysisWindow[i] = 0.5f * (1f - MathF.Cos(2f * MathF.PI * i / (_fftSize - 1)));
        }

        // Synthesis window (same as analysis for COLA property)
        _synthesisWindow = new float[_fftSize];
        Array.Copy(_analysisWindow, _synthesisWindow, _fftSize);
    }

    /// <summary>
    /// Analyzes audio data and generates spectral frames.
    /// </summary>
    /// <param name="audioData">Mono audio samples</param>
    /// <param name="sampleRate">Sample rate in Hz</param>
    public void Analyze(float[] audioData, int sampleRate)
    {
        if (audioData == null || audioData.Length == 0)
            throw new ArgumentException("Audio data cannot be null or empty.", nameof(audioData));
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");

        _sampleRate = sampleRate;
        _frames.Clear();
        _undoStack.Clear();
        _redoStack.Clear();

        // Store original audio for comparison
        _originalAudio = new float[audioData.Length];
        Array.Copy(audioData, _originalAudio, audioData.Length);

        int halfSize = _fftSize / 2;
        Complex[] fftBuffer = new Complex[_fftSize];

        // Process each frame
        int frameIndex = 0;
        for (int position = 0; position + _fftSize <= audioData.Length; position += _hopSize)
        {
            double time = (double)position / sampleRate;

            // Apply window and prepare FFT input
            for (int i = 0; i < _fftSize; i++)
            {
                fftBuffer[i] = new Complex(audioData[position + i] * _analysisWindow[i], 0f);
            }

            // Forward FFT
            FFT(fftBuffer, false);

            // Create spectral frame
            var frame = new SpectralFrame(time, _fftSize, sampleRate);

            // Extract magnitude and phase for positive frequencies
            for (int k = 0; k <= halfSize; k++)
            {
                float real = fftBuffer[k].Real;
                float imag = fftBuffer[k].Imag;
                frame.Magnitudes[k] = MathF.Sqrt(real * real + imag * imag);
                frame.Phases[k] = MathF.Atan2(imag, real);
            }

            _frames.Add(frame);
            frameIndex++;
        }

        AnalysisComplete?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Synthesizes audio from the spectral frames using inverse STFT with overlap-add.
    /// </summary>
    /// <returns>Reconstructed audio samples</returns>
    public float[] Synthesize()
    {
        if (_frames.Count == 0)
            throw new InvalidOperationException("No spectral data to synthesize. Call Analyze first.");

        int halfSize = _fftSize / 2;
        int outputLength = (int)((_frames.Count - 1) * _hopSize + _fftSize);
        float[] output = new float[outputLength];
        float[] windowSum = new float[outputLength];
        Complex[] fftBuffer = new Complex[_fftSize];

        // Process each frame
        for (int frameIndex = 0; frameIndex < _frames.Count; frameIndex++)
        {
            var frame = _frames[frameIndex];
            int position = frameIndex * _hopSize;

            // Build complex spectrum from magnitude and phase
            for (int k = 0; k <= halfSize; k++)
            {
                float mag = frame.Magnitudes[k];
                float phase = frame.Phases[k];
                fftBuffer[k] = new Complex(mag * MathF.Cos(phase), mag * MathF.Sin(phase));

                // Mirror for negative frequencies (conjugate symmetric)
                if (k > 0 && k < halfSize)
                {
                    fftBuffer[_fftSize - k] = new Complex(mag * MathF.Cos(phase), -mag * MathF.Sin(phase));
                }
            }

            // Inverse FFT
            FFT(fftBuffer, true);

            // Overlap-add with synthesis window
            for (int i = 0; i < _fftSize && position + i < outputLength; i++)
            {
                float windowedSample = fftBuffer[i].Real * _synthesisWindow[i];
                output[position + i] += windowedSample;
                windowSum[position + i] += _synthesisWindow[i] * _synthesisWindow[i];
            }
        }

        // Normalize by window sum (COLA normalization)
        for (int i = 0; i < outputLength; i++)
        {
            if (windowSum[i] > 1e-6f)
            {
                output[i] /= windowSum[i];
            }
        }

        return output;
    }

    /// <summary>
    /// Gets a spectral frame by index.
    /// </summary>
    /// <param name="index">Frame index</param>
    /// <returns>The spectral frame</returns>
    public SpectralFrame GetFrame(int index)
    {
        if (index < 0 || index >= _frames.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _frames[index];
    }

    /// <summary>
    /// Gets the spectral frame closest to a given time.
    /// </summary>
    /// <param name="time">Time in seconds</param>
    /// <returns>The nearest spectral frame</returns>
    public SpectralFrame GetFrameAtTime(double time)
    {
        if (_frames.Count == 0)
            throw new InvalidOperationException("No frames available.");

        int index = (int)(time * _sampleRate / _hopSize);
        index = Math.Clamp(index, 0, _frames.Count - 1);
        return _frames[index];
    }

    /// <summary>
    /// Gets the frame index for a given time.
    /// </summary>
    /// <param name="time">Time in seconds</param>
    /// <returns>Frame index</returns>
    public int GetFrameIndexAtTime(double time)
    {
        int index = (int)(time * _sampleRate / _hopSize);
        return Math.Clamp(index, 0, _frames.Count - 1);
    }

    /// <summary>
    /// Applies a spectral operation to the frames.
    /// </summary>
    /// <param name="operation">The operation to apply</param>
    public void ApplyOperation(SpectralOperation operation)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        if (!operation.Selection.IsValid())
            throw new ArgumentException("Operation has invalid selection.", nameof(operation));

        // Store undo data
        operation.UndoData = StoreUndoData(operation.Selection);

        // Apply the operation
        switch (operation.Type)
        {
            case SpectralOperationType.Erase:
                ApplyErase(operation.Selection);
                break;
            case SpectralOperationType.Amplify:
                ApplyAmplify(operation.Selection, operation.Amount);
                break;
            case SpectralOperationType.FrequencyShift:
                ApplyFrequencyShift(operation.Selection, operation.Amount);
                break;
            case SpectralOperationType.HarmonicEnhance:
                ApplyHarmonicEnhance(operation.Selection, operation.Amount);
                break;
            case SpectralOperationType.NoiseReduce:
                ApplyNoiseReduce(operation.Selection, operation.Amount);
                break;
            case SpectralOperationType.Fade:
                bool fadeIn = operation.Parameters.GetValueOrDefault("FadeType", 0f) < 0.5f;
                ApplyFade(operation.Selection, fadeIn);
                break;
            case SpectralOperationType.PhaseInvert:
                ApplyPhaseInvert(operation.Selection);
                break;
            case SpectralOperationType.TimeBlur:
                ApplyTimeBlur(operation.Selection, operation.Amount);
                break;
            case SpectralOperationType.Smooth:
                ApplySmooth(operation.Selection, operation.Amount);
                break;
            case SpectralOperationType.Clone:
                if (operation.Parameters.TryGetValue("SourceStartTime", out float srcStart) &&
                    operation.Parameters.TryGetValue("SourceEndTime", out float srcEnd))
                {
                    ApplyClone(operation.Selection, srcStart, srcEnd);
                }
                break;
        }

        _undoStack.Push(operation);
        _redoStack.Clear();

        OperationApplied?.Invoke(this, operation);
    }

    /// <summary>
    /// Erases (zeroes) magnitudes in the selected region.
    /// </summary>
    public void EraseSelection(SpectralSelection selection)
    {
        ApplyOperation(SpectralOperation.CreateErase(selection));
    }

    /// <summary>
    /// Amplifies magnitudes in the selected region.
    /// </summary>
    /// <param name="selection">Selection region</param>
    /// <param name="gainDb">Gain in decibels</param>
    public void AmplifySelection(SpectralSelection selection, float gainDb)
    {
        ApplyOperation(SpectralOperation.CreateAmplify(selection, gainDb));
    }

    /// <summary>
    /// Shifts frequencies in the selected region.
    /// </summary>
    /// <param name="selection">Selection region</param>
    /// <param name="semitones">Semitones to shift</param>
    public void ShiftFrequency(SpectralSelection selection, float semitones)
    {
        ApplyOperation(SpectralOperation.CreateFrequencyShift(selection, semitones));
    }

    /// <summary>
    /// Undoes the last operation.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo)
            return;

        var operation = _undoStack.Pop();
        if (operation.UndoData != null)
        {
            RestoreUndoData(operation.Selection, operation.UndoData);
        }
        _redoStack.Push(operation);
    }

    /// <summary>
    /// Redoes the last undone operation.
    /// </summary>
    public void Redo()
    {
        if (!CanRedo)
            return;

        var operation = _redoStack.Pop();

        // Re-store undo data and reapply
        operation.UndoData = StoreUndoData(operation.Selection);

        switch (operation.Type)
        {
            case SpectralOperationType.Erase:
                ApplyErase(operation.Selection);
                break;
            case SpectralOperationType.Amplify:
                ApplyAmplify(operation.Selection, operation.Amount);
                break;
            case SpectralOperationType.FrequencyShift:
                ApplyFrequencyShift(operation.Selection, operation.Amount);
                break;
            case SpectralOperationType.HarmonicEnhance:
                ApplyHarmonicEnhance(operation.Selection, operation.Amount);
                break;
            case SpectralOperationType.NoiseReduce:
                ApplyNoiseReduce(operation.Selection, operation.Amount);
                break;
            case SpectralOperationType.Fade:
                bool fadeIn = operation.Parameters.GetValueOrDefault("FadeType", 0f) < 0.5f;
                ApplyFade(operation.Selection, fadeIn);
                break;
            case SpectralOperationType.PhaseInvert:
                ApplyPhaseInvert(operation.Selection);
                break;
            case SpectralOperationType.TimeBlur:
                ApplyTimeBlur(operation.Selection, operation.Amount);
                break;
            case SpectralOperationType.Smooth:
                ApplySmooth(operation.Selection, operation.Amount);
                break;
            case SpectralOperationType.Clone:
                if (operation.Parameters.TryGetValue("SourceStartTime", out float srcStart) &&
                    operation.Parameters.TryGetValue("SourceEndTime", out float srcEnd))
                {
                    ApplyClone(operation.Selection, srcStart, srcEnd);
                }
                break;
        }

        _undoStack.Push(operation);
    }

    /// <summary>
    /// Clears all undo/redo history.
    /// </summary>
    public void ClearHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    /// <summary>
    /// Gets the undo history descriptions.
    /// </summary>
    /// <returns>List of operation descriptions</returns>
    public IReadOnlyList<string> GetUndoHistory()
    {
        return _undoStack.Select(op => op.Description).Reverse().ToList();
    }

    #region Private Operation Implementations

    private void ApplyErase(SpectralSelection selection)
    {
        ForEachFrameInSelection(selection, (frame, minBin, maxBin) =>
        {
            for (int bin = minBin; bin <= maxBin; bin++)
            {
                frame.Magnitudes[bin] = 0f;
            }
        });
    }

    private void ApplyAmplify(SpectralSelection selection, float gain)
    {
        ForEachFrameInSelection(selection, (frame, minBin, maxBin) =>
        {
            for (int bin = minBin; bin <= maxBin; bin++)
            {
                frame.Magnitudes[bin] *= gain;
            }
        });
    }

    private void ApplyFrequencyShift(SpectralSelection selection, float semitones)
    {
        float ratio = MathF.Pow(2f, semitones / 12f);

        ForEachFrameInSelection(selection, (frame, minBin, maxBin) =>
        {
            int binCount = frame.BinCount;
            float[] newMags = new float[binCount];
            float[] newPhases = new float[binCount];
            Array.Copy(frame.Magnitudes, newMags, binCount);
            Array.Copy(frame.Phases, newPhases, binCount);

            // Clear the target region
            for (int bin = minBin; bin <= maxBin; bin++)
            {
                newMags[bin] = 0f;
                newPhases[bin] = 0f;
            }

            // Shift frequencies
            for (int sourceBin = minBin; sourceBin <= maxBin; sourceBin++)
            {
                int targetBin = (int)(sourceBin * ratio);
                if (targetBin >= 0 && targetBin < binCount)
                {
                    newMags[targetBin] += frame.Magnitudes[sourceBin];
                    newPhases[targetBin] = frame.Phases[sourceBin];
                }
            }

            // Copy back
            for (int bin = minBin; bin <= maxBin; bin++)
            {
                frame.Magnitudes[bin] = newMags[bin];
                frame.Phases[bin] = newPhases[bin];
            }
        });
    }

    private void ApplyHarmonicEnhance(SpectralSelection selection, float strength)
    {
        ForEachFrameInSelection(selection, (frame, minBin, maxBin) =>
        {
            // Find dominant frequency in selection
            float peakMag = 0f;
            int peakBin = minBin;
            for (int bin = minBin; bin <= maxBin; bin++)
            {
                if (frame.Magnitudes[bin] > peakMag)
                {
                    peakMag = frame.Magnitudes[bin];
                    peakBin = bin;
                }
            }

            if (peakMag < 1e-6f) return;

            // Enhance harmonics
            for (int harmonic = 2; harmonic <= 8; harmonic++)
            {
                int harmonicBin = peakBin * harmonic;
                if (harmonicBin < frame.BinCount && harmonicBin >= minBin && harmonicBin <= maxBin)
                {
                    float enhancement = peakMag * strength / harmonic;
                    frame.Magnitudes[harmonicBin] += enhancement;
                }
            }
        });
    }

    private void ApplyNoiseReduce(SpectralSelection selection, float reduction)
    {
        // Simple spectral subtraction - estimate noise floor and subtract
        float noiseFloor = EstimateNoiseFloor(selection) * (1f + reduction);

        ForEachFrameInSelection(selection, (frame, minBin, maxBin) =>
        {
            for (int bin = minBin; bin <= maxBin; bin++)
            {
                float magnitude = frame.Magnitudes[bin];
                if (magnitude < noiseFloor)
                {
                    // Soft knee attenuation
                    float attenuation = MathF.Pow(magnitude / noiseFloor, 2f) * (1f - reduction);
                    frame.Magnitudes[bin] = magnitude * attenuation;
                }
            }
        });
    }

    private void ApplyFade(SpectralSelection selection, bool fadeIn)
    {
        int startFrame = GetFrameIndexAtTime(selection.StartTime);
        int endFrame = GetFrameIndexAtTime(selection.EndTime);
        int frameRange = endFrame - startFrame;
        if (frameRange <= 0) return;

        for (int i = startFrame; i <= endFrame && i < _frames.Count; i++)
        {
            var frame = _frames[i];
            float progress = (float)(i - startFrame) / frameRange;
            float gain = fadeIn ? progress : (1f - progress);

            int minBin = frame.GetBinForFrequency(selection.MinFrequency);
            int maxBin = frame.GetBinForFrequency(selection.MaxFrequency);

            for (int bin = minBin; bin <= maxBin; bin++)
            {
                frame.Magnitudes[bin] *= gain;
            }
        }
    }

    private void ApplyPhaseInvert(SpectralSelection selection)
    {
        ForEachFrameInSelection(selection, (frame, minBin, maxBin) =>
        {
            for (int bin = minBin; bin <= maxBin; bin++)
            {
                frame.Phases[bin] = WrapPhase(frame.Phases[bin] + MathF.PI);
            }
        });
    }

    private void ApplyTimeBlur(SpectralSelection selection, float amount)
    {
        int startFrame = GetFrameIndexAtTime(selection.StartTime);
        int endFrame = GetFrameIndexAtTime(selection.EndTime);
        int blurRadius = Math.Max(1, (int)(amount * 10));

        // Create temporary storage
        var blurredMags = new List<float[]>();
        for (int i = startFrame; i <= endFrame && i < _frames.Count; i++)
        {
            blurredMags.Add(new float[_frames[i].BinCount]);
        }

        // Apply blur
        for (int i = startFrame; i <= endFrame && i < _frames.Count; i++)
        {
            var frame = _frames[i];
            int minBin = frame.GetBinForFrequency(selection.MinFrequency);
            int maxBin = frame.GetBinForFrequency(selection.MaxFrequency);
            int localIndex = i - startFrame;

            for (int bin = minBin; bin <= maxBin; bin++)
            {
                float sum = 0f;
                int count = 0;

                for (int j = -blurRadius; j <= blurRadius; j++)
                {
                    int sourceFrame = i + j;
                    if (sourceFrame >= startFrame && sourceFrame <= endFrame && sourceFrame < _frames.Count)
                    {
                        sum += _frames[sourceFrame].Magnitudes[bin];
                        count++;
                    }
                }

                blurredMags[localIndex][bin] = count > 0 ? sum / count : frame.Magnitudes[bin];
            }
        }

        // Apply blurred magnitudes
        for (int i = startFrame; i <= endFrame && i < _frames.Count; i++)
        {
            var frame = _frames[i];
            int minBin = frame.GetBinForFrequency(selection.MinFrequency);
            int maxBin = frame.GetBinForFrequency(selection.MaxFrequency);
            int localIndex = i - startFrame;

            for (int bin = minBin; bin <= maxBin; bin++)
            {
                frame.Magnitudes[bin] = blurredMags[localIndex][bin];
            }
        }
    }

    private void ApplySmooth(SpectralSelection selection, float amount)
    {
        int smoothRadius = Math.Max(1, (int)(amount * 20));

        ForEachFrameInSelection(selection, (frame, minBin, maxBin) =>
        {
            float[] smoothed = new float[frame.BinCount];
            Array.Copy(frame.Magnitudes, smoothed, frame.BinCount);

            for (int bin = minBin; bin <= maxBin; bin++)
            {
                float sum = 0f;
                int count = 0;

                for (int j = -smoothRadius; j <= smoothRadius; j++)
                {
                    int sourceBin = bin + j;
                    if (sourceBin >= 0 && sourceBin < frame.BinCount)
                    {
                        sum += frame.Magnitudes[sourceBin];
                        count++;
                    }
                }

                smoothed[bin] = count > 0 ? sum / count : frame.Magnitudes[bin];
            }

            for (int bin = minBin; bin <= maxBin; bin++)
            {
                frame.Magnitudes[bin] = smoothed[bin];
            }
        });
    }

    private void ApplyClone(SpectralSelection targetSelection, double sourceStartTime, double sourceEndTime)
    {
        int sourceStartFrame = GetFrameIndexAtTime(sourceStartTime);
        int sourceEndFrame = GetFrameIndexAtTime(sourceEndTime);
        int targetStartFrame = GetFrameIndexAtTime(targetSelection.StartTime);
        int targetEndFrame = GetFrameIndexAtTime(targetSelection.EndTime);

        int sourceFrameCount = sourceEndFrame - sourceStartFrame + 1;
        int targetFrameCount = targetEndFrame - targetStartFrame + 1;

        for (int i = 0; i < targetFrameCount && i < _frames.Count - targetStartFrame; i++)
        {
            int sourceIndex = sourceStartFrame + (i % sourceFrameCount);
            int targetIndex = targetStartFrame + i;

            if (sourceIndex >= _frames.Count || targetIndex >= _frames.Count)
                continue;

            var sourceFrame = _frames[sourceIndex];
            var targetFrame = _frames[targetIndex];

            int minBin = targetFrame.GetBinForFrequency(targetSelection.MinFrequency);
            int maxBin = targetFrame.GetBinForFrequency(targetSelection.MaxFrequency);

            for (int bin = minBin; bin <= maxBin; bin++)
            {
                targetFrame.Magnitudes[bin] = sourceFrame.Magnitudes[bin];
                targetFrame.Phases[bin] = sourceFrame.Phases[bin];
            }
        }
    }

    private void ForEachFrameInSelection(SpectralSelection selection, Action<SpectralFrame, int, int> action)
    {
        int startFrame = GetFrameIndexAtTime(selection.StartTime);
        int endFrame = GetFrameIndexAtTime(selection.EndTime);

        for (int i = startFrame; i <= endFrame && i < _frames.Count; i++)
        {
            var frame = _frames[i];
            int minBin = frame.GetBinForFrequency(selection.MinFrequency);
            int maxBin = frame.GetBinForFrequency(selection.MaxFrequency);
            action(frame, minBin, maxBin);
        }
    }

    private float EstimateNoiseFloor(SpectralSelection selection)
    {
        float sum = 0f;
        int count = 0;

        ForEachFrameInSelection(selection, (frame, minBin, maxBin) =>
        {
            // Use lower percentile as noise estimate
            var magnitudes = new List<float>();
            for (int bin = minBin; bin <= maxBin; bin++)
            {
                magnitudes.Add(frame.Magnitudes[bin]);
            }

            magnitudes.Sort();
            int percentileIndex = magnitudes.Count / 10; // 10th percentile
            if (percentileIndex < magnitudes.Count)
            {
                sum += magnitudes[percentileIndex];
                count++;
            }
        });

        return count > 0 ? sum / count : 0f;
    }

    private List<SpectralFrame> StoreUndoData(SpectralSelection selection)
    {
        var undoData = new List<SpectralFrame>();
        int startFrame = GetFrameIndexAtTime(selection.StartTime);
        int endFrame = GetFrameIndexAtTime(selection.EndTime);

        for (int i = startFrame; i <= endFrame && i < _frames.Count; i++)
        {
            undoData.Add(_frames[i].Clone());
        }

        return undoData;
    }

    private void RestoreUndoData(SpectralSelection selection, List<SpectralFrame> undoData)
    {
        int startFrame = GetFrameIndexAtTime(selection.StartTime);

        for (int i = 0; i < undoData.Count && startFrame + i < _frames.Count; i++)
        {
            _frames[startFrame + i].CopyFrom(undoData[i]);
        }
    }

    #endregion

    #region FFT Implementation

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

    private static float WrapPhase(float phase)
    {
        while (phase > MathF.PI) phase -= 2f * MathF.PI;
        while (phase < -MathF.PI) phase += 2f * MathF.PI;
        return phase;
    }

    private static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

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

    #endregion

    /// <summary>
    /// Disposes of the spectral editor resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _frames.Clear();
            _undoStack.Clear();
            _redoStack.Clear();
            _originalAudio = null;
            _disposed = true;
        }
    }
}
