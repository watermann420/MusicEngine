// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;


namespace MusicEngine.Core.Freeze;


/// <summary>
/// Renders a track offline (faster than realtime) to an audio buffer or file.
/// Used for freeze/bounce operations.
/// </summary>
public class TrackRenderer
{
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly int _bufferSize;

    /// <summary>
    /// Gets or sets whether to include effects in the render.
    /// </summary>
    public bool IncludeEffects { get; set; } = true;

    /// <summary>
    /// Gets or sets the tail length in seconds to render after all notes end.
    /// Useful for capturing reverb/delay tails.
    /// </summary>
    public double TailLengthSeconds { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets whether to normalize the output after rendering.
    /// </summary>
    public bool NormalizeOutput { get; set; } = false;

    /// <summary>
    /// Gets or sets the target peak level for normalization (0.0 to 1.0).
    /// </summary>
    public float NormalizationTargetLevel { get; set; } = 0.95f;

    /// <summary>
    /// Creates a new TrackRenderer instance.
    /// </summary>
    /// <param name="sampleRate">The sample rate to render at (defaults to engine sample rate).</param>
    /// <param name="channels">The number of channels (defaults to 2 for stereo).</param>
    /// <param name="bufferSize">The buffer size for rendering chunks (defaults to 4096).</param>
    public TrackRenderer(int? sampleRate = null, int? channels = null, int bufferSize = 4096)
    {
        _sampleRate = sampleRate ?? Settings.SampleRate;
        _channels = channels ?? Settings.Channels;
        _bufferSize = bufferSize;
    }

    /// <summary>
    /// Renders a pattern with its synth to an audio buffer.
    /// </summary>
    /// <param name="pattern">The pattern to render.</param>
    /// <param name="bpm">The tempo in BPM.</param>
    /// <param name="startBeat">The start position in beats.</param>
    /// <param name="endBeat">The end position in beats.</param>
    /// <param name="effectChain">Optional effect chain to apply.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered audio buffer.</returns>
    public async Task<float[]> RenderPatternAsync(
        Pattern pattern,
        double bpm,
        double startBeat,
        double endBeat,
        EffectChain? effectChain = null,
        IProgress<RenderProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));

        if (endBeat <= startBeat)
            throw new ArgumentException("End beat must be greater than start beat.", nameof(endBeat));

        var synth = pattern.Synth ?? throw new InvalidOperationException("Pattern has no synth assigned.");

        return await RenderSynthWithPatternsAsync(
            synth,
            new[] { pattern },
            bpm,
            startBeat,
            endBeat,
            effectChain,
            progress,
            cancellationToken);
    }

    /// <summary>
    /// Renders a synth with multiple patterns to an audio buffer.
    /// </summary>
    /// <param name="synth">The synth to render.</param>
    /// <param name="patterns">The patterns containing note events.</param>
    /// <param name="bpm">The tempo in BPM.</param>
    /// <param name="startBeat">The start position in beats.</param>
    /// <param name="endBeat">The end position in beats.</param>
    /// <param name="effectChain">Optional effect chain to apply.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered audio buffer.</returns>
    public async Task<float[]> RenderSynthWithPatternsAsync(
        ISynth synth,
        IEnumerable<Pattern> patterns,
        double bpm,
        double startBeat,
        double endBeat,
        EffectChain? effectChain = null,
        IProgress<RenderProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => RenderSynthWithPatterns(
            synth, patterns, bpm, startBeat, endBeat, effectChain, progress, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Renders a synth with patterns synchronously.
    /// </summary>
    private float[] RenderSynthWithPatterns(
        ISynth synth,
        IEnumerable<Pattern> patterns,
        double bpm,
        double startBeat,
        double endBeat,
        EffectChain? effectChain,
        IProgress<RenderProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        // Calculate total length including tail
        double totalBeats = endBeat - startBeat;
        double tailBeats = TailLengthSeconds * bpm / 60.0;
        double totalBeatsWithTail = totalBeats + tailBeats;

        double secondsPerBeat = 60.0 / bpm;
        double totalSeconds = totalBeatsWithTail * secondsPerBeat;
        long totalSamples = (long)(totalSeconds * _sampleRate * _channels);

        // Allocate output buffer
        var outputBuffer = new float[totalSamples];
        var renderBuffer = new float[_bufferSize];

        // Build note schedule
        var noteSchedule = BuildNoteSchedule(patterns, startBeat, endBeat, bpm);

        // Determine audio source
        ISampleProvider audioSource = IncludeEffects && effectChain != null
            ? effectChain
            : synth;

        double currentBeat = startBeat;
        long currentSample = 0;
        int chunkCount = 0;
        int totalChunks = (int)Math.Ceiling((double)totalSamples / _bufferSize);

        // Report initial progress
        progress?.Report(RenderProgress.Preparing(0, totalBeats));

        while (currentSample < totalSamples)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int samplesToRender = (int)Math.Min(_bufferSize, totalSamples - currentSample);

            // Process note events for this chunk
            double chunkStartBeat = currentBeat;
            double chunkEndBeat = currentBeat + (samplesToRender / (double)_channels) / _sampleRate * bpm / 60.0;

            ProcessNoteEvents(synth, noteSchedule, chunkStartBeat, chunkEndBeat);

            // Read audio from source
            Array.Clear(renderBuffer, 0, renderBuffer.Length);
            int samplesRead = audioSource.Read(renderBuffer, 0, samplesToRender);

            // Copy to output buffer
            Array.Copy(renderBuffer, 0, outputBuffer, currentSample, samplesRead);

            currentSample += samplesRead;
            currentBeat = chunkEndBeat;
            chunkCount++;

            // Report progress
            if (chunkCount % 10 == 0 || currentSample >= totalSamples)
            {
                double renderedBeats = Math.Min(currentBeat - startBeat, totalBeats);
                var elapsed = stopwatch.Elapsed;
                double renderSpeed = elapsed.TotalSeconds > 0
                    ? (renderedBeats * secondsPerBeat) / elapsed.TotalSeconds
                    : 1.0;

                TimeSpan? eta = null;
                if (renderSpeed > 0 && renderedBeats < totalBeatsWithTail)
                {
                    double remainingSeconds = (totalBeatsWithTail - renderedBeats) * secondsPerBeat / renderSpeed;
                    eta = TimeSpan.FromSeconds(remainingSeconds);
                }

                progress?.Report(new RenderProgress(
                    "Rendering",
                    renderedBeats,
                    totalBeats,
                    0,
                    null)
                {
                    CurrentPositionSamples = currentSample,
                    TotalLengthSamples = totalSamples,
                    ElapsedTime = elapsed,
                    EstimatedTimeRemaining = eta,
                    RenderSpeedMultiplier = renderSpeed
                });
            }
        }

        // Ensure all notes are off
        synth.AllNotesOff();

        // Normalize if requested
        if (NormalizeOutput)
        {
            NormalizeBuffer(outputBuffer);
        }

        progress?.Report(RenderProgress.Complete(0, totalBeats));

        return outputBuffer;
    }

    /// <summary>
    /// Renders directly from an ISampleProvider to a buffer.
    /// </summary>
    /// <param name="source">The audio source to render.</param>
    /// <param name="durationSeconds">The duration to render in seconds.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered audio buffer.</returns>
    public async Task<float[]> RenderSampleProviderAsync(
        ISampleProvider source,
        double durationSeconds,
        IProgress<RenderProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => RenderSampleProvider(source, durationSeconds, progress, cancellationToken),
            cancellationToken);
    }

    private float[] RenderSampleProvider(
        ISampleProvider source,
        double durationSeconds,
        IProgress<RenderProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        long totalSamples = (long)(durationSeconds * _sampleRate * _channels);
        var outputBuffer = new float[totalSamples];
        var renderBuffer = new float[_bufferSize];

        long currentSample = 0;
        int chunkCount = 0;

        while (currentSample < totalSamples)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int samplesToRender = (int)Math.Min(_bufferSize, totalSamples - currentSample);
            int samplesRead = source.Read(renderBuffer, 0, samplesToRender);

            if (samplesRead == 0)
                break;

            Array.Copy(renderBuffer, 0, outputBuffer, currentSample, samplesRead);
            currentSample += samplesRead;
            chunkCount++;

            if (chunkCount % 10 == 0)
            {
                double currentSeconds = currentSample / (double)(_sampleRate * _channels);
                progress?.Report(new RenderProgress(
                    "Rendering",
                    currentSeconds,
                    durationSeconds,
                    0)
                {
                    CurrentPositionSamples = currentSample,
                    TotalLengthSamples = totalSamples,
                    ElapsedTime = stopwatch.Elapsed
                });
            }
        }

        if (NormalizeOutput)
        {
            NormalizeBuffer(outputBuffer);
        }

        return outputBuffer;
    }

    /// <summary>
    /// Saves a rendered buffer to a WAV file.
    /// </summary>
    /// <param name="buffer">The audio buffer to save.</param>
    /// <param name="filePath">The output file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveToFileAsync(
        float[] buffer,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, _channels);

            using var writer = new WaveFileWriter(filePath, waveFormat);

            int chunkSize = 8192;
            for (int i = 0; i < buffer.Length; i += chunkSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int count = Math.Min(chunkSize, buffer.Length - i);
                writer.WriteSamples(buffer, i, count);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Loads audio from a WAV file to a buffer.
    /// </summary>
    /// <param name="filePath">The file path to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The audio buffer.</returns>
    public async Task<float[]> LoadFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Audio file not found.", filePath);

            using var reader = new AudioFileReader(filePath);

            var buffer = new float[(int)(reader.Length / sizeof(float))];
            int samplesRead = reader.Read(buffer, 0, buffer.Length);

            if (samplesRead < buffer.Length)
            {
                Array.Resize(ref buffer, samplesRead);
            }

            return buffer;
        }, cancellationToken);
    }

    private List<ScheduledNote> BuildNoteSchedule(
        IEnumerable<Pattern> patterns,
        double startBeat,
        double endBeat,
        double bpm)
    {
        var schedule = new List<ScheduledNote>();
        double secondsPerBeat = 60.0 / bpm;

        foreach (var pattern in patterns)
        {
            if (!pattern.Enabled)
                continue;

            double patternLength = pattern.LoopLength;
            double patternStart = pattern.StartBeat ?? startBeat;

            foreach (var noteEvent in pattern.Events)
            {
                if (pattern.IsLooping)
                {
                    // Calculate all loop iterations that fall within the range
                    double firstOccurrence = patternStart + noteEvent.Beat;
                    while (firstOccurrence < startBeat)
                        firstOccurrence += patternLength;

                    double currentOccurrence = firstOccurrence;
                    while (currentOccurrence < endBeat)
                    {
                        double noteStartSeconds = (currentOccurrence - startBeat) * secondsPerBeat;
                        double noteDurationSeconds = noteEvent.Duration * secondsPerBeat;

                        schedule.Add(new ScheduledNote
                        {
                            StartBeat = currentOccurrence,
                            EndBeat = currentOccurrence + noteEvent.Duration,
                            StartSeconds = noteStartSeconds,
                            EndSeconds = noteStartSeconds + noteDurationSeconds,
                            Note = noteEvent.Note,
                            Velocity = noteEvent.Velocity,
                            IsTriggered = false
                        });

                        currentOccurrence += patternLength;
                    }
                }
                else
                {
                    double noteStartBeat = patternStart + noteEvent.Beat;
                    if (noteStartBeat >= startBeat && noteStartBeat < endBeat)
                    {
                        double noteStartSeconds = (noteStartBeat - startBeat) * secondsPerBeat;
                        double noteDurationSeconds = noteEvent.Duration * secondsPerBeat;

                        schedule.Add(new ScheduledNote
                        {
                            StartBeat = noteStartBeat,
                            EndBeat = noteStartBeat + noteEvent.Duration,
                            StartSeconds = noteStartSeconds,
                            EndSeconds = noteStartSeconds + noteDurationSeconds,
                            Note = noteEvent.Note,
                            Velocity = noteEvent.Velocity,
                            IsTriggered = false
                        });
                    }
                }
            }
        }

        schedule.Sort((a, b) => a.StartBeat.CompareTo(b.StartBeat));
        return schedule;
    }

    private void ProcessNoteEvents(
        ISynth synth,
        List<ScheduledNote> schedule,
        double startBeat,
        double endBeat)
    {
        foreach (var note in schedule)
        {
            // Trigger note on
            if (!note.IsTriggered && note.StartBeat >= startBeat && note.StartBeat < endBeat)
            {
                synth.NoteOn(note.Note, note.Velocity);
                note.IsTriggered = true;
            }

            // Trigger note off
            if (note.IsTriggered && !note.IsReleased && note.EndBeat >= startBeat && note.EndBeat < endBeat)
            {
                synth.NoteOff(note.Note);
                note.IsReleased = true;
            }
        }
    }

    private void NormalizeBuffer(float[] buffer)
    {
        float maxSample = 0f;

        // Find peak
        for (int i = 0; i < buffer.Length; i++)
        {
            float abs = Math.Abs(buffer[i]);
            if (abs > maxSample)
                maxSample = abs;
        }

        // Apply normalization
        if (maxSample > 0.0001f)
        {
            float scale = NormalizationTargetLevel / maxSample;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] *= scale;
            }
        }
    }

    private class ScheduledNote
    {
        public double StartBeat { get; set; }
        public double EndBeat { get; set; }
        public double StartSeconds { get; set; }
        public double EndSeconds { get; set; }
        public int Note { get; set; }
        public int Velocity { get; set; }
        public bool IsTriggered { get; set; }
        public bool IsReleased { get; set; }
    }
}
