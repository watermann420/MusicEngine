// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;
using System.IO;


namespace MusicEngine.Core.Video;


/// <summary>
/// Represents a video track that can be synchronized with the audio timeline.
/// The video track stores metadata about a video file and provides timecode conversion.
/// </summary>
/// <remarks>
/// Note: This class provides metadata and timecode functionality. Actual video
/// playback and rendering should be handled by a video player component in the UI layer.
/// </remarks>
public class VideoTrack
{
    private string _filePath = string.Empty;
    private double _currentPosition;


    /// <summary>
    /// Gets the unique identifier for this video track.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the file path of the loaded video.
    /// </summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Gets the duration of the video in seconds.
    /// </summary>
    public double Duration { get; private set; }

    /// <summary>
    /// Gets the video width in pixels.
    /// </summary>
    public int Width { get; private set; }

    /// <summary>
    /// Gets the video height in pixels.
    /// </summary>
    public int Height { get; private set; }

    /// <summary>
    /// Gets the video frame rate.
    /// </summary>
    public double FrameRate { get; private set; }

    /// <summary>
    /// Gets or sets the SMPTE frame rate used for timecode display.
    /// </summary>
    public SMPTEFrameRate SMPTERate { get; set; } = SMPTEFrameRate.FPS_25;

    /// <summary>
    /// Gets or sets the offset from the timeline start in seconds.
    /// Positive values place the video later in the timeline.
    /// </summary>
    public double Offset { get; set; }

    /// <summary>
    /// Gets or sets whether the video track is muted (audio from video is muted).
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// Gets or sets the name of this video track.
    /// </summary>
    public string Name { get; set; } = "Video";

    /// <summary>
    /// Gets or sets whether this video track is visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Gets or sets the opacity of this video track (0.0 to 1.0).
    /// </summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>
    /// Gets whether a video is currently loaded.
    /// </summary>
    public bool IsLoaded => !string.IsNullOrEmpty(_filePath) && Duration > 0;

    /// <summary>
    /// Gets or sets the current playback position in seconds.
    /// </summary>
    public double CurrentPosition
    {
        get => _currentPosition;
        set
        {
            if (Math.Abs(_currentPosition - value) > 0.0001)
            {
                _currentPosition = Math.Clamp(value, 0, Duration);
                PositionChanged?.Invoke(this, _currentPosition);
            }
        }
    }


    /// <summary>
    /// Event raised when the playback position changes.
    /// </summary>
    public event EventHandler<double>? PositionChanged;

    /// <summary>
    /// Event raised when a video is loaded.
    /// </summary>
    public event EventHandler<string>? VideoLoaded;

    /// <summary>
    /// Event raised when the video is unloaded.
    /// </summary>
    public event EventHandler? VideoUnloaded;


    /// <summary>
    /// Creates a new video track with a unique ID.
    /// </summary>
    public VideoTrack()
    {
        Id = Guid.NewGuid();
    }


    /// <summary>
    /// Creates a new video track with a specific ID.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    public VideoTrack(Guid id)
    {
        Id = id;
    }


    /// <summary>
    /// Loads a video file. This sets up metadata for the video.
    /// </summary>
    /// <param name="filePath">The path to the video file.</param>
    /// <remarks>
    /// This method validates the file exists and sets default metadata.
    /// In a full implementation, you would use a video library (like FFmpeg)
    /// to extract actual metadata from the video file.
    /// </remarks>
    public void LoadVideo(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Video file not found.", filePath);

        _filePath = filePath;
        Name = Path.GetFileNameWithoutExtension(filePath);

        // Default metadata - in a real implementation, these would be read from the video file
        // using a library like FFmpeg via FFMediaToolkit, MediaFoundation, or similar
        LoadVideoMetadata(filePath);

        // Auto-detect SMPTE rate from frame rate
        SMPTERate = DetectSMPTERate(FrameRate);

        _currentPosition = 0;

        VideoLoaded?.Invoke(this, filePath);
    }


    /// <summary>
    /// Loads video metadata from file.
    /// </summary>
    /// <remarks>
    /// This is a placeholder implementation. In production, use FFMediaToolkit,
    /// MediaFoundation, or similar to read actual video metadata.
    /// </remarks>
    private void LoadVideoMetadata(string filePath)
    {
        // Default values - real implementation would read from video file
        // Using common defaults for testing
        Width = 1920;
        Height = 1080;
        FrameRate = 25.0;
        Duration = 0; // Would be read from video

        // Try to detect frame rate from file extension/name patterns
        string fileName = Path.GetFileName(filePath).ToLowerInvariant();

        if (fileName.Contains("24fps") || fileName.Contains("24p"))
        {
            FrameRate = 24.0;
        }
        else if (fileName.Contains("25fps") || fileName.Contains("25p") || fileName.Contains("pal"))
        {
            FrameRate = 25.0;
        }
        else if (fileName.Contains("29.97") || fileName.Contains("2997") || fileName.Contains("ntsc"))
        {
            FrameRate = 29.97;
        }
        else if (fileName.Contains("30fps") || fileName.Contains("30p"))
        {
            FrameRate = 30.0;
        }
        else if (fileName.Contains("60fps") || fileName.Contains("60p"))
        {
            FrameRate = 60.0;
        }

        // Try to detect resolution from filename
        if (fileName.Contains("4k") || fileName.Contains("2160"))
        {
            Width = 3840;
            Height = 2160;
        }
        else if (fileName.Contains("1080"))
        {
            Width = 1920;
            Height = 1080;
        }
        else if (fileName.Contains("720"))
        {
            Width = 1280;
            Height = 720;
        }
        else if (fileName.Contains("480"))
        {
            Width = 854;
            Height = 480;
        }
    }


    /// <summary>
    /// Detects the appropriate SMPTE frame rate from the video frame rate.
    /// </summary>
    private static SMPTEFrameRate DetectSMPTERate(double frameRate)
    {
        // Round to nearest common rate
        if (Math.Abs(frameRate - 24.0) < 0.1)
            return SMPTEFrameRate.FPS_24;

        if (Math.Abs(frameRate - 25.0) < 0.1)
            return SMPTEFrameRate.FPS_25;

        if (Math.Abs(frameRate - 29.97) < 0.1)
            return SMPTEFrameRate.FPS_29_97_DF; // Default to drop frame for NTSC

        if (Math.Abs(frameRate - 30.0) < 0.1)
            return SMPTEFrameRate.FPS_30;

        // For higher frame rates, use 30fps timecode
        return SMPTEFrameRate.FPS_30;
    }


    /// <summary>
    /// Unloads the current video.
    /// </summary>
    public void UnloadVideo()
    {
        _filePath = string.Empty;
        Duration = 0;
        Width = 0;
        Height = 0;
        FrameRate = 0;
        _currentPosition = 0;

        VideoUnloaded?.Invoke(this, EventArgs.Empty);
    }


    /// <summary>
    /// Sets the video metadata manually (for testing or when using external video libraries).
    /// </summary>
    /// <param name="width">Video width in pixels.</param>
    /// <param name="height">Video height in pixels.</param>
    /// <param name="frameRate">Video frame rate.</param>
    /// <param name="duration">Video duration in seconds.</param>
    public void SetMetadata(int width, int height, double frameRate, double duration)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        FrameRate = Math.Max(1.0, frameRate);
        Duration = Math.Max(0, duration);
        SMPTERate = DetectSMPTERate(frameRate);
    }


    /// <summary>
    /// Gets the SMPTE timecode at the specified position.
    /// </summary>
    /// <param name="position">Position in seconds on the timeline.</param>
    /// <returns>The SMPTE timecode at that position.</returns>
    public SMPTETimecode GetTimecodeAt(double position)
    {
        // Adjust for video offset
        double videoPosition = position - Offset;

        // Clamp to video bounds
        videoPosition = Math.Clamp(videoPosition, 0, Duration);

        return SMPTETimecode.FromSeconds(videoPosition, SMPTERate);
    }


    /// <summary>
    /// Gets the timeline position from a SMPTE timecode.
    /// </summary>
    /// <param name="timecode">The SMPTE timecode.</param>
    /// <returns>The position in seconds on the timeline.</returns>
    public double GetPositionFromTimecode(SMPTETimecode timecode)
    {
        // Convert timecode to seconds and add offset
        return timecode.ToSeconds() + Offset;
    }


    /// <summary>
    /// Gets the video frame number at the specified position.
    /// </summary>
    /// <param name="position">Position in seconds on the timeline.</param>
    /// <returns>The frame number (0-based).</returns>
    public long GetFrameAt(double position)
    {
        double videoPosition = position - Offset;
        videoPosition = Math.Clamp(videoPosition, 0, Duration);

        return (long)(videoPosition * FrameRate);
    }


    /// <summary>
    /// Gets the timeline position for a specific frame number.
    /// </summary>
    /// <param name="frame">The frame number (0-based).</param>
    /// <returns>The position in seconds on the timeline.</returns>
    public double GetPositionFromFrame(long frame)
    {
        if (FrameRate <= 0)
            return Offset;

        return (frame / FrameRate) + Offset;
    }


    /// <summary>
    /// Seeks to a specific timecode.
    /// </summary>
    /// <param name="timecode">The target timecode.</param>
    public void SeekToTimecode(SMPTETimecode timecode)
    {
        CurrentPosition = timecode.ToSeconds();
    }


    /// <summary>
    /// Seeks to a specific frame.
    /// </summary>
    /// <param name="frame">The target frame number.</param>
    public void SeekToFrame(long frame)
    {
        if (FrameRate > 0)
        {
            CurrentPosition = frame / FrameRate;
        }
    }


    /// <summary>
    /// Steps forward by one frame.
    /// </summary>
    public void StepForward()
    {
        if (FrameRate > 0)
        {
            CurrentPosition = Math.Min(Duration, _currentPosition + 1.0 / FrameRate);
        }
    }


    /// <summary>
    /// Steps backward by one frame.
    /// </summary>
    public void StepBackward()
    {
        if (FrameRate > 0)
        {
            CurrentPosition = Math.Max(0, _currentPosition - 1.0 / FrameRate);
        }
    }


    /// <summary>
    /// Gets the current timecode.
    /// </summary>
    public SMPTETimecode CurrentTimecode => GetTimecodeAt(_currentPosition + Offset);


    /// <summary>
    /// Gets the aspect ratio of the video (width / height).
    /// </summary>
    public double AspectRatio => Height > 0 ? (double)Width / Height : 16.0 / 9.0;


    /// <summary>
    /// Gets the total frame count.
    /// </summary>
    public long TotalFrames => FrameRate > 0 ? (long)(Duration * FrameRate) : 0;


    /// <inheritdoc/>
    public override string ToString()
    {
        if (!IsLoaded)
            return $"{Name} (no video loaded)";

        return $"{Name} ({Width}x{Height}, {FrameRate:F2}fps, {SMPTETimecode.FromSeconds(Duration, SMPTERate)})";
    }
}
