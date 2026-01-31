// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MusicEngine.Core.AudioEncoding;
using NAudio.Wave;

namespace MusicEngine.Core;

/// <summary>
/// Audio recorder for capturing audio from any ISampleProvider to WAV or MP3 files.
/// Supports pause/resume, progress reporting, and multiple output formats.
/// Thread-safe implementation with proper resource cleanup.
/// </summary>
public class AudioRecorder : IDisposable
{
    private readonly ISampleProvider _source;
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly object _recordingLock = new();

    private WaveFileRecorder? _waveRecorder;
    private Thread? _recordingThread;
    private volatile bool _isRecording;
    private volatile bool _isPaused;
    private volatile bool _stopRequested;
    private string? _outputPath;
    private DateTime _recordingStartTime;
    private TimeSpan _pausedDuration;
    private DateTime _pauseStartTime;
    private long _totalSamplesRecorded;
    private float _currentPeakLevel;
    private RecordingFormat _format = RecordingFormat.Wav16Bit;
    private bool _disposed;

    // Progress reporting
    private readonly Timer? _progressTimer;
    private const int ProgressIntervalMs = 100;

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
    /// Gets whether recording is currently paused.
    /// </summary>
    public bool IsPaused
    {
        get
        {
            lock (_recordingLock)
            {
                return _isPaused;
            }
        }
    }

    /// <summary>
    /// Gets the duration of audio recorded so far.
    /// </summary>
    public TimeSpan RecordedDuration
    {
        get
        {
            lock (_recordingLock)
            {
                if (_totalSamplesRecorded == 0) return TimeSpan.Zero;
                return TimeSpan.FromSeconds((double)_totalSamplesRecorded / _sampleRate / _channels);
            }
        }
    }

    /// <summary>
    /// Gets the current output file path, or null if not recording.
    /// </summary>
    public string? OutputPath
    {
        get
        {
            lock (_recordingLock)
            {
                return _outputPath;
            }
        }
    }

    /// <summary>
    /// Gets or sets the recording format.
    /// Cannot be changed while recording is in progress.
    /// </summary>
    public RecordingFormat Format
    {
        get => _format;
        set
        {
            lock (_recordingLock)
            {
                if (_isRecording)
                    throw new InvalidOperationException("Cannot change format while recording is in progress.");
                _format = value;
            }
        }
    }

    /// <summary>
    /// Gets the sample rate for recording.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the number of channels for recording.
    /// </summary>
    public int Channels => _channels;

    /// <summary>
    /// Event raised to report recording progress.
    /// </summary>
    public event EventHandler<RecordingProgressEventArgs>? Progress;

    /// <summary>
    /// Event raised when recording starts.
    /// </summary>
    public event EventHandler? RecordingStarted;

    /// <summary>
    /// Event raised when recording completes (successfully or with error).
    /// </summary>
    public event EventHandler<RecordingCompletedEventArgs>? RecordingCompleted;

    /// <summary>
    /// Creates a new AudioRecorder.
    /// </summary>
    /// <param name="source">The audio source to record from.</param>
    /// <param name="sampleRate">Sample rate in Hz. Default is 44100.</param>
    /// <param name="channels">Number of channels. Default is 2 (stereo).</param>
    public AudioRecorder(ISampleProvider source, int sampleRate = 44100, int channels = 2)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));

        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
        if (channels <= 0)
            throw new ArgumentOutOfRangeException(nameof(channels), "Channels must be positive.");

        _sampleRate = sampleRate;
        _channels = channels;

        // Initialize progress timer (not started until recording begins)
        _progressTimer = new Timer(OnProgressTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>Sets the desired recording format for the next StartRecording call.</summary>
    public void SetFormat(RecordingFormat format)
    {
        _format = format;
    }

    /// <summary>
    /// Starts recording to the specified output file.
    /// </summary>
    /// <param name="outputPath">Path for the output file.</param>
    /// <exception cref="InvalidOperationException">Thrown if recording is already in progress.</exception>
    /// <exception cref="ArgumentException">Thrown if outputPath is null or empty.</exception>
    public void StartRecording(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be null or empty.", nameof(outputPath));

        lock (_recordingLock)
        {
            if (_isRecording)
                throw new InvalidOperationException("Recording is already in progress.");
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioRecorder));

            _outputPath = outputPath;
            _stopRequested = false;
            _isPaused = false;
            _totalSamplesRecorded = 0;
            _currentPeakLevel = 0;
            _pausedDuration = TimeSpan.Zero;

            // Ensure correct file extension
            string actualPath = EnsureCorrectExtension(outputPath);
            _outputPath = actualPath;

            // Create recorder based on format
            if (_format.IsWavFormat())
            {
                _waveRecorder = new WaveFileRecorder(actualPath, _sampleRate, _channels, _format);
            }
            else
            {
                // For MP3, we'll record to a temp WAV first, then convert
                string tempWavPath = Path.Combine(Path.GetTempPath(), $"recording_{Guid.NewGuid()}.wav");
                _waveRecorder = new WaveFileRecorder(tempWavPath, _sampleRate, _channels, RecordingFormat.Wav16Bit);
            }

            _recordingStartTime = DateTime.Now;
            _isRecording = true;

            // Start recording thread
            _recordingThread = new Thread(RecordingLoop)
            {
                Name = "AudioRecorderThread",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _recordingThread.Start();

            // Start progress reporting
            _progressTimer?.Change(ProgressIntervalMs, ProgressIntervalMs);
        }

        // Raise event outside lock to prevent deadlocks
        RecordingStarted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Starts recording to a provided stream (WAV only). Honors the format set via SetFormat.
    /// </summary>
    public void StartRecording(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        if (!stream.CanWrite) throw new ArgumentException("Stream must be writable.", nameof(stream));

        lock (_recordingLock)
        {
            if (_isRecording)
                throw new InvalidOperationException("Recording is already in progress.");
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioRecorder));

            _outputPath = null;
            _stopRequested = false;
            _isPaused = false;
            _totalSamplesRecorded = 0;
            _currentPeakLevel = 0;
            _pausedDuration = TimeSpan.Zero;

            if (!_format.IsWavFormat())
            {
                throw new NotSupportedException("In-memory recording currently supports WAV formats only.");
            }

            _waveRecorder = new WaveFileRecorder(stream, _sampleRate, _channels, _format);

            _recordingStartTime = DateTime.Now;
            _isRecording = true;

            _recordingThread = new Thread(RecordingLoop)
            {
                Name = "AudioRecorderThread",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _recordingThread.Start();

            _progressTimer?.Change(ProgressIntervalMs, ProgressIntervalMs);
        }

        RecordingStarted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Stops the current recording.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if not currently recording.</exception>
    public void StopRecording()
    {
        RecordingCompletedEventArgs? completedArgs = null;

        lock (_recordingLock)
        {
            if (!_isRecording)
                throw new InvalidOperationException("Not currently recording.");

            _stopRequested = true;
            _isPaused = false;
        }

        // Wait for recording thread to finish
        _recordingThread?.Join(TimeSpan.FromSeconds(5));

        // Stop progress timer
        _progressTimer?.Change(Timeout.Infinite, Timeout.Infinite);

        lock (_recordingLock)
        {
            try
            {
                string finalPath = _outputPath ?? string.Empty;
                long fileSize = 0;

                // Finalize the WAV file
                if (_waveRecorder != null)
                {
                    _waveRecorder.FinalizeFile();
                    string wavPath = _waveRecorder.FilePath;
                    _waveRecorder.Dispose();
                    _waveRecorder = null;

                    // Convert to MP3 if needed
                    if (_format.IsMp3Format())
                    {
                        bool converted = ConvertToMp3(wavPath, finalPath, _format.GetMp3Bitrate());
                        if (converted)
                        {
                            // Delete temp WAV file
                            try { File.Delete(wavPath); }
                            catch (IOException) { /* Ignore cleanup errors */ }
                        }
                        else
                        {
                            // Conversion failed, keep WAV and update path
                            finalPath = Path.ChangeExtension(finalPath, ".wav");
                            if (wavPath != finalPath)
                            {
                                File.Move(wavPath, finalPath, true);
                            }
                        }
                    }

                    if (File.Exists(finalPath))
                    {
                        fileSize = new FileInfo(finalPath).Length;
                    }

                    completedArgs = new RecordingCompletedEventArgs(
                        finalPath,
                        RecordedDuration,
                        fileSize,
                        _format,
                        _sampleRate,
                        _channels,
                        _totalSamplesRecorded);
                }

                _isRecording = false;
                _recordingThread = null;
                _outputPath = null;
            }
            catch (Exception ex)
            {
                _isRecording = false;
                _recordingThread = null;
                completedArgs = new RecordingCompletedEventArgs(_outputPath ?? string.Empty, RecordedDuration, ex);
            }
        }

        // Raise event outside lock
        if (completedArgs != null)
        {
            RecordingCompleted?.Invoke(this, completedArgs);
        }
    }

    /// <summary>
    /// Pauses the current recording.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if not recording or already paused.</exception>
    public void PauseRecording()
    {
        lock (_recordingLock)
        {
            if (!_isRecording)
                throw new InvalidOperationException("Not currently recording.");
            if (_isPaused)
                throw new InvalidOperationException("Recording is already paused.");

            _isPaused = true;
            _pauseStartTime = DateTime.Now;
        }
    }

    /// <summary>
    /// Resumes a paused recording.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if not recording or not paused.</exception>
    public void ResumeRecording()
    {
        lock (_recordingLock)
        {
            if (!_isRecording)
                throw new InvalidOperationException("Not currently recording.");
            if (!_isPaused)
                throw new InvalidOperationException("Recording is not paused.");

            _pausedDuration += DateTime.Now - _pauseStartTime;
            _isPaused = false;
        }
    }

    /// <summary>
    /// Records audio for a specified duration asynchronously.
    /// </summary>
    /// <param name="outputPath">Path for the output file.</param>
    /// <param name="duration">Duration to record.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Path to the recorded file.</returns>
    public async Task<string> RecordAsync(string outputPath, TimeSpan duration, CancellationToken ct = default)
    {
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be positive.");

        StartRecording(outputPath);

        try
        {
            var startTime = DateTime.Now;
            while (RecordedDuration < duration && !ct.IsCancellationRequested)
            {
                await Task.Delay(100, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            if (IsRecording)
            {
                StopRecording();
            }
        }

        return _outputPath ?? outputPath;
    }

    /// <summary>
    /// Exports an audio file with the specified preset settings.
    /// Supports WAV, MP3, FLAC, OGG Vorbis, and AIFF formats.
    /// Loudness normalization is not yet implemented.
    /// </summary>
    /// <param name="inputPath">Path to the input audio file.</param>
    /// <param name="outputPath">Path for the output file.</param>
    /// <param name="preset">Export preset with format and loudness settings.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Export result with success status and measurements.</returns>
    public static async Task<ExportResult> ExportWithPresetAsync(
        string inputPath,
        string outputPath,
        ExportPreset preset,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
            throw new ArgumentException("Input path cannot be null or empty.", nameof(inputPath));
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be null or empty.", nameof(outputPath));
        if (!System.IO.File.Exists(inputPath))
            return ExportResult.Failed($"Input file not found: {inputPath}");

        var startTime = DateTime.Now;

        try
        {
            progress?.Report(new ExportProgress(ExportPhase.Starting, 0, "Starting export..."));

            bool exportSuccess = await Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new ExportProgress(ExportPhase.Analyzing, 0.1, "Analyzing source..."));

                using var reader = new NAudio.Wave.AudioFileReader(inputPath);

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new ExportProgress(ExportPhase.Writing, 0.3, "Writing output..."));

                // Create output based on format
                switch (preset.Format)
                {
                    case AudioFormat.Wav:
                        return ExportToWav(reader, outputPath, preset, progress, cancellationToken);

                    case AudioFormat.Mp3:
                        return ExportToMp3(reader, outputPath, preset, progress, cancellationToken);

                    case AudioFormat.Flac:
                        return await ExportToFlacAsync(inputPath, outputPath, preset, progress, cancellationToken);

                    case AudioFormat.Ogg:
                        return await ExportToOggAsync(inputPath, outputPath, preset, progress, cancellationToken);

                    case AudioFormat.Aiff:
                        return await ExportToAiffAsync(inputPath, outputPath, preset, progress, cancellationToken);

                    default:
                        // Fallback: copy file
                        System.IO.File.Copy(inputPath, outputPath, true);
                        return true;
                }
            }, cancellationToken);

            if (!exportSuccess)
            {
                return ExportResult.Failed($"Export to {preset.Format} format failed. Check if required encoder packages are installed.");
            }

            progress?.Report(new ExportProgress(ExportPhase.Complete, 1.0, "Export complete!"));

            var exportDuration = DateTime.Now - startTime;
            var result = ExportResult.Succeeded(outputPath);
            result.ExportDuration = exportDuration;

            if (System.IO.File.Exists(outputPath))
            {
                result.OutputFileSize = new System.IO.FileInfo(outputPath).Length;
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return ExportResult.Failed("Export cancelled by user");
        }
        catch (Exception ex)
        {
            return ExportResult.Failed(ex.Message, ex);
        }
    }

    /// <summary>
    /// Exports audio to WAV format.
    /// </summary>
    private static bool ExportToWav(
        NAudio.Wave.AudioFileReader reader,
        string outputPath,
        ExportPreset preset,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        var format = new NAudio.Wave.WaveFormat(preset.SampleRate, preset.BitDepth, reader.WaveFormat.Channels);
        using var writer = new NAudio.Wave.WaveFileWriter(outputPath, format);

        float[] buffer = new float[4096];
        int samplesRead;
        long totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);
        long processedSamples = 0;

        while ((samplesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            writer.WriteSamples(buffer, 0, samplesRead);
            processedSamples += samplesRead;

            double progressValue = 0.3 + (0.6 * processedSamples / totalSamples);
            progress?.Report(new ExportProgress(ExportPhase.Writing, progressValue,
                $"Writing WAV: {progressValue * 100:F0}%"));
        }

        return true;
    }

    /// <summary>
    /// Exports audio to MP3 format using NAudio.Lame via reflection.
    /// </summary>
    private static bool ExportToMp3(
        NAudio.Wave.AudioFileReader reader,
        string outputPath,
        ExportPreset preset,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            // Try to use NAudio.Lame for MP3 encoding via reflection
            var lameType = Type.GetType("NAudio.Lame.LameMP3FileWriter, NAudio.Lame");

            if (lameType == null)
            {
                try
                {
                    var assembly = System.Reflection.Assembly.Load("NAudio.Lame");
                    lameType = assembly.GetType("NAudio.Lame.LameMP3FileWriter");
                }
                catch (FileNotFoundException)
                {
                    System.Diagnostics.Debug.WriteLine("NAudio.Lame not found. Install with: dotnet add package NAudio.Lame");
                    return false;
                }
            }

            if (lameType == null)
                return false;

            // Get constructor: LameMP3FileWriter(string, WaveFormat, int)
            var constructor = lameType.GetConstructor(new[] { typeof(string), typeof(WaveFormat), typeof(int) });
            if (constructor == null)
                return false;

            int bitRate = preset.BitRate ?? 320;
            using var writer = (IDisposable)constructor.Invoke(new object[] { outputPath, reader.WaveFormat, bitRate });

            var writeMethod = lameType.GetMethod("Write", new[] { typeof(byte[]), typeof(int), typeof(int) });
            if (writeMethod == null)
                return false;

            // Convert float to 16-bit PCM for MP3 encoding
            float[] floatBuffer = new float[4096];
            byte[] byteBuffer = new byte[floatBuffer.Length * 2]; // 16-bit = 2 bytes per sample
            int samplesRead;
            long totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);
            long processedSamples = 0;

            while ((samplesRead = reader.Read(floatBuffer, 0, floatBuffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Convert float samples to 16-bit PCM
                int byteCount = samplesRead * 2;
                for (int i = 0; i < samplesRead; i++)
                {
                    short sample = (short)(Math.Clamp(floatBuffer[i], -1.0f, 1.0f) * 32767f);
                    byteBuffer[i * 2] = (byte)(sample & 0xFF);
                    byteBuffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
                }

                writeMethod.Invoke(writer, new object[] { byteBuffer, 0, byteCount });
                processedSamples += samplesRead;

                double progressValue = 0.3 + (0.6 * processedSamples / totalSamples);
                progress?.Report(new ExportProgress(ExportPhase.Writing, progressValue,
                    $"Writing MP3: {progressValue * 100:F0}%"));
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MP3 export failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Exports audio to FLAC format using the encoder system.
    /// </summary>
    private static async Task<bool> ExportToFlacAsync(
        string inputPath,
        string outputPath,
        ExportPreset preset,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        var settings = EncoderSettings.FromExportPreset(preset);
        var encoder = EncoderFactory.CreateAndInitialize(settings);

        if (encoder == null)
        {
            System.Diagnostics.Debug.WriteLine("FLAC encoder not available. Install NAudio.Flac package.");
            return false;
        }

        try
        {
            var encoderProgress = new Progress<double>(p =>
            {
                double progressValue = 0.3 + (0.6 * p);
                progress?.Report(new ExportProgress(ExportPhase.Writing, progressValue,
                    $"Encoding FLAC: {progressValue * 100:F0}%"));
            });

            return await encoder.EncodeFileAsync(inputPath, outputPath, encoderProgress, cancellationToken);
        }
        finally
        {
            encoder.Dispose();
        }
    }

    /// <summary>
    /// Exports audio to OGG Vorbis format using the encoder system.
    /// </summary>
    private static async Task<bool> ExportToOggAsync(
        string inputPath,
        string outputPath,
        ExportPreset preset,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        var settings = EncoderSettings.FromExportPreset(preset);
        var encoder = EncoderFactory.CreateAndInitialize(settings);

        if (encoder == null)
        {
            System.Diagnostics.Debug.WriteLine("OGG Vorbis encoder not available. Install OggVorbisEncoder package.");
            return false;
        }

        try
        {
            var encoderProgress = new Progress<double>(p =>
            {
                double progressValue = 0.3 + (0.6 * p);
                progress?.Report(new ExportProgress(ExportPhase.Writing, progressValue,
                    $"Encoding OGG: {progressValue * 100:F0}%"));
            });

            return await encoder.EncodeFileAsync(inputPath, outputPath, encoderProgress, cancellationToken);
        }
        finally
        {
            encoder.Dispose();
        }
    }

    /// <summary>
    /// Exports audio to AIFF format using the built-in encoder.
    /// </summary>
    private static async Task<bool> ExportToAiffAsync(
        string inputPath,
        string outputPath,
        ExportPreset preset,
        IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        var settings = new EncoderSettings
        {
            Format = AudioFormat.Aiff,
            BitDepth = preset.BitDepth,
            SampleRate = preset.SampleRate,
            Channels = 2 // Stereo default
        };

        using var encoder = new AiffEncoder();

        if (!encoder.Initialize(settings))
        {
            System.Diagnostics.Debug.WriteLine("AIFF encoder initialization failed.");
            return false;
        }

        var encoderProgress = new Progress<double>(p =>
        {
            double progressValue = 0.3 + (0.6 * p);
            progress?.Report(new ExportProgress(ExportPhase.Writing, progressValue,
                $"Writing AIFF: {progressValue * 100:F0}%"));
        });

        return await encoder.EncodeFileAsync(inputPath, outputPath, encoderProgress, cancellationToken);
    }

    /// <summary>
    /// Main recording loop that reads samples from the source and writes to the file.
    /// </summary>
    private void RecordingLoop()
    {
        float[] buffer = new float[4096];
        Exception? recordingError = null;

        try
        {
            while (!_stopRequested)
            {
                // Check if paused
                lock (_recordingLock)
                {
                    if (_isPaused)
                    {
                        Monitor.Wait(_recordingLock, 10);
                        continue;
                    }
                }

                // Read samples from source
                int samplesRead = _source.Read(buffer, 0, buffer.Length);

                if (samplesRead > 0)
                {
                    lock (_recordingLock)
                    {
                        if (_waveRecorder != null && !_stopRequested && !_isPaused)
                        {
                            _waveRecorder.WriteSamples(buffer, 0, samplesRead);
                            _totalSamplesRecorded += samplesRead;

                            // Update peak level
                            float peak = _waveRecorder.PeakLevel;
                            if (peak > _currentPeakLevel)
                            {
                                _currentPeakLevel = peak;
                            }
                        }
                    }
                }
                else
                {
                    // No samples available, sleep briefly
                    Thread.Sleep(1);
                }
            }
        }
        catch (Exception ex)
        {
            recordingError = ex;
            System.Diagnostics.Debug.WriteLine($"Recording error: {ex.Message}");
        }
    }

    /// <summary>
    /// Progress timer callback.
    /// </summary>
    private void OnProgressTimer(object? state)
    {
        if (!_isRecording) return;

        lock (_recordingLock)
        {
            if (!_isRecording) return;

            float peakDb = _currentPeakLevel > 0
                ? 20f * (float)Math.Log10(_currentPeakLevel)
                : float.NegativeInfinity;

            long estimatedFileSize = _waveRecorder?.FileSize ?? 0;

            var args = new RecordingProgressEventArgs(
                RecordedDuration,
                _totalSamplesRecorded,
                peakDb,
                estimatedFileSize);

            // Reset peak for next interval
            _currentPeakLevel = 0;

            // Invoke on thread pool to avoid blocking
            ThreadPool.QueueUserWorkItem(_ => Progress?.Invoke(this, args));
        }
    }

    /// <summary>
    /// Ensures the output path has the correct file extension for the format.
    /// </summary>
    private string EnsureCorrectExtension(string path)
    {
        string expectedExtension = _format.GetFileExtension();
        string currentExtension = Path.GetExtension(path);

        if (!currentExtension.Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            return Path.ChangeExtension(path, expectedExtension);
        }

        return path;
    }

    /// <summary>
    /// Converts a WAV file to MP3 using NAudio.Lame.
    /// </summary>
    private bool ConvertToMp3(string wavPath, string mp3Path, int bitRate)
    {
        try
        {
            // Try to use NAudio.Lame for MP3 encoding via reflection
            var lameType = Type.GetType("NAudio.Lame.LameMP3FileWriter, NAudio.Lame");

            if (lameType == null)
            {
                try
                {
                    var assembly = System.Reflection.Assembly.Load("NAudio.Lame");
                    lameType = assembly.GetType("NAudio.Lame.LameMP3FileWriter");
                }
                catch (FileNotFoundException)
                {
                    System.Diagnostics.Debug.WriteLine("NAudio.Lame not found. Install with: dotnet add package NAudio.Lame");
                    return false;
                }
            }

            if (lameType == null)
            {
                return false;
            }

            using var reader = new WaveFileReader(wavPath);

            // Get constructor: LameMP3FileWriter(Stream, WaveFormat, int)
            var constructor = lameType.GetConstructor(new[] { typeof(Stream), typeof(WaveFormat), typeof(int) });
            if (constructor == null)
            {
                return false;
            }

            using var mp3Stream = File.Create(mp3Path);
            using var writer = (IDisposable)constructor.Invoke(new object[] { mp3Stream, reader.WaveFormat, bitRate });

            var writeMethod = lameType.GetMethod("Write", new[] { typeof(byte[]), typeof(int), typeof(int) });
            if (writeMethod == null)
            {
                return false;
            }

            byte[] buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                writeMethod.Invoke(writer, new object[] { buffer, 0, bytesRead });
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MP3 conversion failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Disposes of the recorder and releases all resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Stop recording if in progress
            if (_isRecording)
            {
                _stopRequested = true;
                _recordingThread?.Join(TimeSpan.FromSeconds(2));
            }

            _progressTimer?.Dispose();

            lock (_recordingLock)
            {
                _waveRecorder?.Dispose();
                _waveRecorder = null;
            }
        }

        _disposed = true;
    }

    /// <summary>
    /// Finalizer.
    /// </summary>
    ~AudioRecorder()
    {
        Dispose(false);
    }
}
