//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Audio recording functionality for capturing master output to WAV/MP3 files.


using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;


namespace MusicEngine.Core;


/// <summary>
/// Event arguments for recording events.
/// </summary>
public class RecordingEventArgs : EventArgs
{
    public string OutputPath { get; }
    public TimeSpan Duration { get; }

    public RecordingEventArgs(string outputPath, TimeSpan duration)
    {
        OutputPath = outputPath;
        Duration = duration;
    }
}

/// <summary>
/// A sample provider wrapper that captures audio while passing it through.
/// Thread-safe implementation for real-time audio capture.
/// </summary>
public class RecordingCaptureSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly object _bufferLock = new();
    private float[]? _captureBuffer;
    private int _captureWritePosition;
    private int _captureReadPosition;
    private bool _isCapturing;
    private long _totalSamplesCaptured;

    /// <summary>
    /// Gets the wave format of the source provider.
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets the total number of samples captured since recording started.
    /// </summary>
    public long TotalSamplesCaptured
    {
        get
        {
            lock (_bufferLock)
            {
                return _totalSamplesCaptured;
            }
        }
    }

    /// <summary>
    /// Gets the recording duration based on samples captured.
    /// </summary>
    public TimeSpan RecordingDuration
    {
        get
        {
            long samples = TotalSamplesCaptured;
            return TimeSpan.FromSeconds((double)samples / WaveFormat.SampleRate / WaveFormat.Channels);
        }
    }

    public RecordingCaptureSampleProvider(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    /// Starts capturing audio to the internal buffer.
    /// </summary>
    /// <param name="bufferSizeInSeconds">Size of the circular buffer in seconds.</param>
    public void StartCapture(int bufferSizeInSeconds = 60)
    {
        lock (_bufferLock)
        {
            int bufferSize = WaveFormat.SampleRate * WaveFormat.Channels * bufferSizeInSeconds;
            _captureBuffer = new float[bufferSize];
            _captureWritePosition = 0;
            _captureReadPosition = 0;
            _totalSamplesCaptured = 0;
            _isCapturing = true;
        }
    }

    /// <summary>
    /// Stops capturing audio.
    /// </summary>
    public void StopCapture()
    {
        lock (_bufferLock)
        {
            _isCapturing = false;
        }
    }

    /// <summary>
    /// Reads samples from the source and optionally captures them.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);

        if (samplesRead > 0)
        {
            lock (_bufferLock)
            {
                if (_isCapturing && _captureBuffer != null)
                {
                    for (int i = 0; i < samplesRead; i++)
                    {
                        _captureBuffer[_captureWritePosition] = buffer[offset + i];
                        _captureWritePosition = (_captureWritePosition + 1) % _captureBuffer.Length;
                    }
                    _totalSamplesCaptured += samplesRead;
                }
            }
        }

        return samplesRead;
    }

    /// <summary>
    /// Reads captured samples from the buffer.
    /// </summary>
    /// <param name="buffer">Destination buffer.</param>
    /// <param name="offset">Offset in the destination buffer.</param>
    /// <param name="count">Number of samples to read.</param>
    /// <returns>Number of samples actually read.</returns>
    public int ReadCapturedSamples(float[] buffer, int offset, int count)
    {
        lock (_bufferLock)
        {
            if (_captureBuffer == null) return 0;

            int available = GetAvailableSamples();
            int toRead = Math.Min(count, available);

            for (int i = 0; i < toRead; i++)
            {
                buffer[offset + i] = _captureBuffer[_captureReadPosition];
                _captureReadPosition = (_captureReadPosition + 1) % _captureBuffer.Length;
            }

            return toRead;
        }
    }

    /// <summary>
    /// Gets the number of samples available in the capture buffer.
    /// </summary>
    private int GetAvailableSamples()
    {
        if (_captureBuffer == null) return 0;

        int diff = _captureWritePosition - _captureReadPosition;
        if (diff < 0) diff += _captureBuffer.Length;
        return diff;
    }
}

/// <summary>
/// Audio recorder for capturing master output to WAV or MP3 files.
/// </summary>
public class AudioRecorder : IDisposable
{
    private readonly object _recordingLock = new();
    private WaveFileWriter? _waveWriter;
    private RecordingCaptureSampleProvider? _captureProvider;
    private Thread? _recordingThread;
    private volatile bool _isRecording;
    private volatile bool _stopRequested;
    private string _currentOutputPath = string.Empty;
    private DateTime _recordingStartTime;
    private int _sampleRate;
    private int _channels;
    private int _bitDepth;

    /// <summary>
    /// Gets whether recording is currently in progress.
    /// </summary>
    public bool IsRecording
    {
        get
        {
            lock (_recordingLock)
            {
                return _isRecording;
            }
        }
    }

    /// <summary>
    /// Gets the current recording duration.
    /// </summary>
    public TimeSpan RecordingDuration
    {
        get
        {
            lock (_recordingLock)
            {
                if (!_isRecording) return TimeSpan.Zero;
                return _captureProvider?.RecordingDuration ?? TimeSpan.Zero;
            }
        }
    }

    /// <summary>
    /// Gets the current output file path.
    /// </summary>
    public string CurrentOutputPath
    {
        get
        {
            lock (_recordingLock)
            {
                return _currentOutputPath;
            }
        }
    }

    /// <summary>
    /// Gets or sets the sample rate for recording. Default uses Settings.SampleRate.
    /// </summary>
    public int SampleRate
    {
        get => _sampleRate;
        set
        {
            if (_isRecording)
                throw new InvalidOperationException("Cannot change sample rate while recording.");
            _sampleRate = value;
        }
    }

    /// <summary>
    /// Gets or sets the number of channels for recording. Default uses Settings.Channels.
    /// </summary>
    public int Channels
    {
        get => _channels;
        set
        {
            if (_isRecording)
                throw new InvalidOperationException("Cannot change channels while recording.");
            _channels = value;
        }
    }

    /// <summary>
    /// Gets or sets the bit depth for WAV export (16 or 24). Default is 16.
    /// </summary>
    public int BitDepth
    {
        get => _bitDepth;
        set
        {
            if (value != 16 && value != 24 && value != 32)
                throw new ArgumentException("BitDepth must be 16, 24, or 32.");
            if (_isRecording)
                throw new InvalidOperationException("Cannot change bit depth while recording.");
            _bitDepth = value;
        }
    }

    /// <summary>
    /// Event raised when recording starts.
    /// </summary>
    public event EventHandler<RecordingEventArgs>? RecordingStarted;

    /// <summary>
    /// Event raised when recording stops.
    /// </summary>
    public event EventHandler<RecordingEventArgs>? RecordingStopped;

    public AudioRecorder()
    {
        _sampleRate = Settings.SampleRate;
        _channels = Settings.Channels;
        _bitDepth = 16;
    }

    /// <summary>
    /// Creates a capture wrapper around the given sample provider.
    /// Use the returned provider in place of the original to enable recording.
    /// </summary>
    /// <param name="source">The source sample provider (e.g., master volume output).</param>
    /// <returns>A sample provider that can be used for recording.</returns>
    public RecordingCaptureSampleProvider CreateCaptureProvider(ISampleProvider source)
    {
        lock (_recordingLock)
        {
            _captureProvider = new RecordingCaptureSampleProvider(source);
            return _captureProvider;
        }
    }

    /// <summary>
    /// Gets the current capture provider, or null if not created.
    /// </summary>
    public RecordingCaptureSampleProvider? CaptureProvider
    {
        get
        {
            lock (_recordingLock)
            {
                return _captureProvider;
            }
        }
    }

    /// <summary>
    /// Starts recording to the specified output file.
    /// </summary>
    /// <param name="outputPath">Path for the output WAV file.</param>
    /// <param name="captureProvider">Optional capture provider. If null, uses the previously created one.</param>
    public void StartRecording(string outputPath, RecordingCaptureSampleProvider? captureProvider = null)
    {
        lock (_recordingLock)
        {
            if (_isRecording)
                throw new InvalidOperationException("Recording is already in progress.");

            if (captureProvider != null)
            {
                _captureProvider = captureProvider;
            }

            if (_captureProvider == null)
                throw new InvalidOperationException("No capture provider available. Call CreateCaptureProvider first.");

            // Ensure directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create wave format for output file
            var waveFormat = new WaveFormat(_sampleRate, _bitDepth, _channels);
            _waveWriter = new WaveFileWriter(outputPath, waveFormat);
            _currentOutputPath = outputPath;
            _recordingStartTime = DateTime.Now;
            _stopRequested = false;
            _isRecording = true;

            // Start capture
            _captureProvider.StartCapture();

            // Start recording thread
            _recordingThread = new Thread(RecordingLoop)
            {
                Name = "AudioRecorderThread",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _recordingThread.Start();

            Console.WriteLine($"Recording started: {outputPath}");
            RecordingStarted?.Invoke(this, new RecordingEventArgs(outputPath, TimeSpan.Zero));
        }
    }

    /// <summary>
    /// Stops the current recording.
    /// </summary>
    /// <returns>The path of the recorded file, or null if not recording.</returns>
    public string? StopRecording()
    {
        string? outputPath;

        lock (_recordingLock)
        {
            if (!_isRecording) return null;

            _stopRequested = true;
            outputPath = _currentOutputPath;
        }

        // Wait for recording thread to finish
        _recordingThread?.Join(TimeSpan.FromSeconds(5));

        lock (_recordingLock)
        {
            var duration = RecordingDuration;

            _captureProvider?.StopCapture();

            try
            {
                _waveWriter?.Flush();
                _waveWriter?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finalizing recording: {ex.Message}");
            }

            _waveWriter = null;
            _isRecording = false;
            _recordingThread = null;

            Console.WriteLine($"Recording stopped: {outputPath} (Duration: {duration:hh\\:mm\\:ss\\.fff})");
            RecordingStopped?.Invoke(this, new RecordingEventArgs(outputPath, duration));

            return outputPath;
        }
    }

    /// <summary>
    /// Recording loop that writes captured samples to the WAV file.
    /// </summary>
    private void RecordingLoop()
    {
        float[] buffer = new float[4096];
        byte[] byteBuffer = new byte[buffer.Length * (_bitDepth / 8)];

        while (!_stopRequested)
        {
            int samplesRead;
            lock (_recordingLock)
            {
                if (_captureProvider == null || _waveWriter == null) break;
                samplesRead = _captureProvider.ReadCapturedSamples(buffer, 0, buffer.Length);
            }

            if (samplesRead > 0)
            {
                lock (_recordingLock)
                {
                    if (_waveWriter == null) break;

                    // Convert float samples to bytes based on bit depth
                    int bytesWritten = ConvertFloatToBytes(buffer, 0, samplesRead, byteBuffer, _bitDepth);
                    _waveWriter.Write(byteBuffer, 0, bytesWritten);
                }
            }
            else
            {
                // No samples available, sleep briefly
                Thread.Sleep(1);
            }
        }
    }

    /// <summary>
    /// Converts float samples to byte array based on bit depth.
    /// </summary>
    private static int ConvertFloatToBytes(float[] source, int sourceOffset, int sampleCount, byte[] dest, int bitDepth)
    {
        int bytesPerSample = bitDepth / 8;
        int destOffset = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            float sample = source[sourceOffset + i];

            // Clamp sample to [-1, 1]
            sample = Math.Max(-1.0f, Math.Min(1.0f, sample));

            switch (bitDepth)
            {
                case 16:
                    short sample16 = (short)(sample * 32767);
                    dest[destOffset++] = (byte)(sample16 & 0xFF);
                    dest[destOffset++] = (byte)((sample16 >> 8) & 0xFF);
                    break;

                case 24:
                    int sample24 = (int)(sample * 8388607);
                    dest[destOffset++] = (byte)(sample24 & 0xFF);
                    dest[destOffset++] = (byte)((sample24 >> 8) & 0xFF);
                    dest[destOffset++] = (byte)((sample24 >> 16) & 0xFF);
                    break;

                case 32:
                    // 32-bit float - write directly
                    byte[] floatBytes = BitConverter.GetBytes(sample);
                    Array.Copy(floatBytes, 0, dest, destOffset, 4);
                    destOffset += 4;
                    break;
            }
        }

        return destOffset;
    }

    /// <summary>
    /// Exports a WAV file to MP3 format using NAudio.Lame if available.
    /// </summary>
    /// <param name="wavPath">Path to the source WAV file.</param>
    /// <param name="mp3Path">Path for the output MP3 file. If null, uses the same name with .mp3 extension.</param>
    /// <param name="bitRate">MP3 bit rate in kbps (default 320).</param>
    /// <returns>True if export succeeded, false otherwise.</returns>
    public bool ExportToMp3(string wavPath, string? mp3Path = null, int bitRate = 320)
    {
        if (!File.Exists(wavPath))
        {
            Console.WriteLine($"WAV file not found: {wavPath}");
            return false;
        }

        mp3Path ??= Path.ChangeExtension(wavPath, ".mp3");

        try
        {
            // Try to use NAudio.Lame for MP3 encoding
            // This requires the NAudio.Lame NuGet package to be installed
            return TryExportMp3WithLame(wavPath, mp3Path, bitRate);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MP3 export failed: {ex.Message}");
            Console.WriteLine("Make sure NAudio.Lame is installed: dotnet add package NAudio.Lame");
            return false;
        }
    }

    /// <summary>
    /// Attempts to export to MP3 using NAudio.Lame via reflection.
    /// This allows the feature to work if NAudio.Lame is installed, without requiring a compile-time dependency.
    /// </summary>
    private bool TryExportMp3WithLame(string wavPath, string mp3Path, int bitRate)
    {
        try
        {
            // Try to load NAudio.Lame assembly
            var lameType = Type.GetType("NAudio.Lame.LameMP3FileWriter, NAudio.Lame");

            if (lameType == null)
            {
                // Try loading the assembly explicitly
                var assembly = System.Reflection.Assembly.Load("NAudio.Lame");
                lameType = assembly.GetType("NAudio.Lame.LameMP3FileWriter");
            }

            if (lameType == null)
            {
                Console.WriteLine("NAudio.Lame not found. Install with: dotnet add package NAudio.Lame");
                return false;
            }

            using var reader = new WaveFileReader(wavPath);

            // Create LameMP3FileWriter instance
            // Constructor: LameMP3FileWriter(string path, WaveFormat format, int quality)
            var constructor = lameType.GetConstructor(new[] { typeof(string), typeof(WaveFormat), typeof(int) });

            if (constructor == null)
            {
                // Try alternative constructor
                constructor = lameType.GetConstructor(new[] { typeof(Stream), typeof(WaveFormat), typeof(int) });
                if (constructor == null)
                {
                    Console.WriteLine("Could not find suitable LameMP3FileWriter constructor.");
                    return false;
                }
            }

            using var mp3Stream = File.Create(mp3Path);
            using var writer = (IDisposable)constructor.Invoke(new object[] { mp3Stream, reader.WaveFormat, bitRate });

            // Get the Write method
            var writeMethod = lameType.GetMethod("Write", new[] { typeof(byte[]), typeof(int), typeof(int) });
            if (writeMethod == null)
            {
                Console.WriteLine("Could not find Write method on LameMP3FileWriter.");
                return false;
            }

            byte[] buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                writeMethod.Invoke(writer, new object[] { buffer, 0, bytesRead });
            }

            Console.WriteLine($"MP3 exported successfully: {mp3Path}");
            return true;
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine("NAudio.Lame assembly not found. Install with: dotnet add package NAudio.Lame");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MP3 export error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Exports a WAV file with different sample rate and/or bit depth.
    /// </summary>
    /// <param name="inputPath">Path to the source WAV file.</param>
    /// <param name="outputPath">Path for the output WAV file.</param>
    /// <param name="sampleRate">Target sample rate (null to keep original).</param>
    /// <param name="bitDepth">Target bit depth (null to keep original).</param>
    /// <returns>True if export succeeded, false otherwise.</returns>
    public bool ExportWav(string inputPath, string outputPath, int? sampleRate = null, int? bitDepth = null)
    {
        if (!File.Exists(inputPath))
        {
            Console.WriteLine($"Input file not found: {inputPath}");
            return false;
        }

        try
        {
            using var reader = new WaveFileReader(inputPath);

            int targetSampleRate = sampleRate ?? reader.WaveFormat.SampleRate;
            int targetBitDepth = bitDepth ?? reader.WaveFormat.BitsPerSample;
            int channels = reader.WaveFormat.Channels;

            // Ensure directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Check if resampling is needed
            if (targetSampleRate != reader.WaveFormat.SampleRate)
            {
                // Use resampling
                var resampler = new MediaFoundationResampler(reader,
                    new WaveFormat(targetSampleRate, targetBitDepth, channels));

                using var writer = new WaveFileWriter(outputPath, resampler.WaveFormat);
                byte[] buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
                {
                    writer.Write(buffer, 0, bytesRead);
                }
                resampler.Dispose();
            }
            else if (targetBitDepth != reader.WaveFormat.BitsPerSample)
            {
                // Convert bit depth without resampling
                var targetFormat = new WaveFormat(targetSampleRate, targetBitDepth, channels);
                using var writer = new WaveFileWriter(outputPath, targetFormat);

                // Convert via float samples
                var sampleReader = reader.ToSampleProvider();
                float[] sampleBuffer = new float[4096];
                byte[] byteBuffer = new byte[sampleBuffer.Length * (targetBitDepth / 8)];

                int samplesRead;
                while ((samplesRead = sampleReader.Read(sampleBuffer, 0, sampleBuffer.Length)) > 0)
                {
                    int bytesWritten = ConvertFloatToBytes(sampleBuffer, 0, samplesRead, byteBuffer, targetBitDepth);
                    writer.Write(byteBuffer, 0, bytesWritten);
                }
            }
            else
            {
                // Simple copy if no conversion needed
                File.Copy(inputPath, outputPath, overwrite: true);
            }

            Console.WriteLine($"WAV exported successfully: {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WAV export error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Disposes resources used by the recorder.
    /// </summary>
    public void Dispose()
    {
        if (_isRecording)
        {
            StopRecording();
        }

        lock (_recordingLock)
        {
            _waveWriter?.Dispose();
            _waveWriter = null;
        }
    }

    /// <summary>
    /// Exports an audio file using the specified preset with loudness normalization.
    /// This is a two-pass process: first measures loudness, then exports with normalization.
    /// </summary>
    /// <param name="inputPath">Path to the source audio file.</param>
    /// <param name="outputPath">Path for the output file.</param>
    /// <param name="preset">Export preset defining format and loudness settings.</param>
    /// <param name="progress">Optional progress callback (0.0 to 1.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Export result with loudness measurements.</returns>
    public async Task<ExportResult> ExportWithPresetAsync(
        string inputPath,
        string outputPath,
        ExportPreset preset,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
        {
            return new ExportResult(false, "Input file not found", null, null);
        }

        return await Task.Run(() =>
        {
            try
            {
                LoudnessMeasurement? sourceMeasurement = null;
                LoudnessMeasurement? outputMeasurement = null;

                // Phase 1: Measure source loudness if normalization is enabled
                if (preset.NormalizeLoudness && preset.TargetLufs.HasValue)
                {
                    progress?.Report(new ExportProgress(ExportPhase.Analyzing, 0, "Analyzing source loudness..."));

                    using var analyzeReader = new AudioFileReader(inputPath);
                    sourceMeasurement = LoudnessAnalyzer.Measure(analyzeReader, p =>
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        progress?.Report(new ExportProgress(ExportPhase.Analyzing, p * 0.3, $"Analyzing: {p * 100:F0}%"));
                    });

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return new ExportResult(false, "Export cancelled", sourceMeasurement, null);
                    }

                    progress?.Report(new ExportProgress(ExportPhase.Analyzing, 0.3,
                        $"Source: {sourceMeasurement.IntegratedLoudness:F1} LUFS, {sourceMeasurement.TruePeak:F1} dBTP"));
                }

                // Phase 2: Export with normalization
                progress?.Report(new ExportProgress(ExportPhase.Exporting, 0.3, "Exporting..."));

                using var reader = new AudioFileReader(inputPath);
                ISampleProvider exportSource = reader;

                // Apply loudness normalization if enabled
                if (preset.NormalizeLoudness && preset.TargetLufs.HasValue && sourceMeasurement != null)
                {
                    var normalizer = LoudnessNormalizer.CreateWithMeasuredLoudness(
                        reader,
                        sourceMeasurement.IntegratedLoudness,
                        sourceMeasurement.TruePeak,
                        preset.TargetLufs.Value,
                        preset.MaxTruePeak ?? -1f);
                    exportSource = normalizer;
                }

                // Resample if needed
                if (preset.SampleRate != reader.WaveFormat.SampleRate)
                {
                    exportSource = new WdlResamplingSampleProvider(exportSource, preset.SampleRate);
                }

                // Create temp WAV file for intermediate processing
                string tempPath = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid()}.wav");

                try
                {
                    // Write to temp WAV
                    var outputFormat = new WaveFormat(preset.SampleRate, preset.BitDepth, reader.WaveFormat.Channels);
                    using (var writer = new WaveFileWriter(tempPath, outputFormat))
                    {
                        float[] buffer = new float[4096];
                        int samplesRead;
                        long totalSamples = 0;
                        long estimatedSamples = (long)(reader.TotalTime.TotalSeconds * preset.SampleRate * reader.WaveFormat.Channels);

                        while ((samplesRead = exportSource.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                return new ExportResult(false, "Export cancelled", sourceMeasurement, null);
                            }

                            // Convert float samples to bytes based on bit depth
                            byte[] byteBuffer = new byte[samplesRead * (preset.BitDepth / 8)];
                            int bytesWritten = ConvertFloatToBytes(buffer, 0, samplesRead, byteBuffer, preset.BitDepth);
                            writer.Write(byteBuffer, 0, bytesWritten);

                            totalSamples += samplesRead;
                            double exportProgress = 0.3 + (0.5 * Math.Min(1.0, (double)totalSamples / estimatedSamples));
                            progress?.Report(new ExportProgress(ExportPhase.Exporting, exportProgress,
                                $"Exporting: {(exportProgress - 0.3) / 0.5 * 100:F0}%"));
                        }
                    }

                    // Convert to final format if not WAV
                    bool formatSuccess = true;
                    switch (preset.Format)
                    {
                        case AudioFormat.Mp3:
                            progress?.Report(new ExportProgress(ExportPhase.Converting, 0.8, "Converting to MP3..."));
                            formatSuccess = TryExportMp3WithLame(tempPath, outputPath, preset.BitRate ?? 320);
                            break;

                        case AudioFormat.Wav:
                            File.Copy(tempPath, outputPath, overwrite: true);
                            break;

                        case AudioFormat.Flac:
                            progress?.Report(new ExportProgress(ExportPhase.Converting, 0.8, "Converting to FLAC..."));
                            // FLAC encoding would require additional library
                            // For now, export as WAV
                            File.Copy(tempPath, Path.ChangeExtension(outputPath, ".wav"), overwrite: true);
                            Console.WriteLine("FLAC encoding not available, exported as WAV.");
                            break;

                        case AudioFormat.Ogg:
                            progress?.Report(new ExportProgress(ExportPhase.Converting, 0.8, "Converting to OGG..."));
                            // OGG encoding would require additional library
                            // For now, export as WAV
                            File.Copy(tempPath, Path.ChangeExtension(outputPath, ".wav"), overwrite: true);
                            Console.WriteLine("OGG encoding not available, exported as WAV.");
                            break;
                    }

                    if (!formatSuccess)
                    {
                        // Fallback to WAV if format conversion fails
                        string wavOutput = Path.ChangeExtension(outputPath, ".wav");
                        File.Copy(tempPath, wavOutput, overwrite: true);
                        Console.WriteLine($"Format conversion failed, exported as WAV: {wavOutput}");
                    }

                    // Phase 3: Verify output loudness
                    progress?.Report(new ExportProgress(ExportPhase.Verifying, 0.9, "Verifying output..."));

                    string verifyPath = File.Exists(outputPath) ? outputPath : Path.ChangeExtension(outputPath, ".wav");
                    if (File.Exists(verifyPath))
                    {
                        using var verifyReader = new AudioFileReader(verifyPath);
                        outputMeasurement = LoudnessAnalyzer.Measure(verifyReader);
                    }

                    progress?.Report(new ExportProgress(ExportPhase.Complete, 1.0,
                        outputMeasurement != null
                            ? $"Complete: {outputMeasurement.IntegratedLoudness:F1} LUFS"
                            : "Complete"));

                    return new ExportResult(true, null, sourceMeasurement, outputMeasurement);
                }
                finally
                {
                    // Clean up temp file
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                return new ExportResult(false, ex.Message, null, null);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Exports an audio source using the specified preset.
    /// </summary>
    /// <param name="source">The audio source to export.</param>
    /// <param name="outputPath">Path for the output file.</param>
    /// <param name="preset">Export preset defining format and loudness settings.</param>
    /// <param name="duration">Duration to export.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Export result.</returns>
    public async Task<ExportResult> ExportSourceWithPresetAsync(
        ISampleProvider source,
        string outputPath,
        ExportPreset preset,
        TimeSpan duration,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                // First, render to a temp file to measure loudness
                string tempPath = Path.Combine(Path.GetTempPath(), $"render_{Guid.NewGuid()}.wav");

                progress?.Report(new ExportProgress(ExportPhase.Rendering, 0, "Rendering audio..."));

                var format = new WaveFormat(source.WaveFormat.SampleRate, 32, source.WaveFormat.Channels);
                using (var writer = new WaveFileWriter(tempPath, format))
                {
                    float[] buffer = new float[4096];
                    int samplesRead;
                    long totalSamples = 0;
                    long targetSamples = (long)(duration.TotalSeconds * source.WaveFormat.SampleRate * source.WaveFormat.Channels);

                    while (totalSamples < targetSamples && (samplesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return new ExportResult(false, "Export cancelled", null, null);
                        }

                        // Limit to remaining samples if needed
                        int samplesToWrite = (int)Math.Min(samplesRead, targetSamples - totalSamples);
                        writer.WriteSamples(buffer, 0, samplesToWrite);

                        totalSamples += samplesToWrite;
                        progress?.Report(new ExportProgress(ExportPhase.Rendering,
                            0.3 * totalSamples / targetSamples,
                            $"Rendering: {100.0 * totalSamples / targetSamples:F0}%"));
                    }
                }

                // Now export with preset
                var result = ExportWithPresetAsync(tempPath, outputPath, preset,
                    new Progress<ExportProgress>(p =>
                    {
                        // Adjust progress to account for rendering phase
                        double adjustedProgress = 0.3 + p.Progress * 0.7;
                        progress?.Report(new ExportProgress(p.Phase, adjustedProgress, p.Message));
                    }), cancellationToken).GetAwaiter().GetResult();

                // Clean up temp file
                try { File.Delete(tempPath); } catch { }

                return result;
            }
            catch (Exception ex)
            {
                return new ExportResult(false, ex.Message, null, null);
            }
        }, cancellationToken);
    }
}

/// <summary>
/// Export progress phase.
/// </summary>
public enum ExportPhase
{
    /// <summary>Analyzing source loudness</summary>
    Analyzing,
    /// <summary>Rendering audio source</summary>
    Rendering,
    /// <summary>Exporting with normalization</summary>
    Exporting,
    /// <summary>Converting to target format</summary>
    Converting,
    /// <summary>Verifying output loudness</summary>
    Verifying,
    /// <summary>Export complete</summary>
    Complete
}

/// <summary>
/// Export progress information.
/// </summary>
/// <param name="Phase">Current export phase.</param>
/// <param name="Progress">Progress value (0.0 to 1.0).</param>
/// <param name="Message">Status message.</param>
public record ExportProgress(ExportPhase Phase, double Progress, string Message);

/// <summary>
/// Result of an export operation.
/// </summary>
/// <param name="Success">Whether the export succeeded.</param>
/// <param name="ErrorMessage">Error message if failed.</param>
/// <param name="SourceMeasurement">Loudness measurement of the source.</param>
/// <param name="OutputMeasurement">Loudness measurement of the output.</param>
public record ExportResult(
    bool Success,
    string? ErrorMessage,
    LoudnessMeasurement? SourceMeasurement,
    LoudnessMeasurement? OutputMeasurement)
{
    /// <summary>
    /// Gets a summary of the export result.
    /// </summary>
    public string Summary
    {
        get
        {
            if (!Success)
            {
                return $"Export failed: {ErrorMessage}";
            }

            var sb = new System.Text.StringBuilder("Export successful");

            if (SourceMeasurement != null)
            {
                sb.Append($"\nSource: {SourceMeasurement.IntegratedLoudness:F1} LUFS, {SourceMeasurement.TruePeak:F1} dBTP");
            }

            if (OutputMeasurement != null)
            {
                sb.Append($"\nOutput: {OutputMeasurement.IntegratedLoudness:F1} LUFS, {OutputMeasurement.TruePeak:F1} dBTP");
            }

            return sb.ToString();
        }
    }
}
