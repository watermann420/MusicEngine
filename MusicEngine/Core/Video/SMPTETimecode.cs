// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;


namespace MusicEngine.Core.Video;


/// <summary>
/// SMPTE timecode frame rates.
/// </summary>
public enum SMPTEFrameRate
{
    /// <summary>
    /// 24 frames per second (film standard).
    /// </summary>
    FPS_24,

    /// <summary>
    /// 25 frames per second (PAL video standard).
    /// </summary>
    FPS_25,

    /// <summary>
    /// 29.97 frames per second with drop frame (NTSC broadcast).
    /// </summary>
    FPS_29_97_DF,

    /// <summary>
    /// 29.97 frames per second non-drop frame (NTSC video).
    /// </summary>
    FPS_29_97_NDF,

    /// <summary>
    /// 30 frames per second non-drop frame.
    /// </summary>
    FPS_30,

    /// <summary>
    /// 30 frames per second with drop frame.
    /// </summary>
    FPS_30_DF
}


/// <summary>
/// Represents an SMPTE timecode value used for video synchronization.
/// SMPTE timecode is the standard format for identifying specific frames in video.
/// </summary>
public struct SMPTETimecode : IEquatable<SMPTETimecode>, IComparable<SMPTETimecode>
{
    /// <summary>
    /// Gets or sets the hours component (0-23).
    /// </summary>
    public int Hours { get; set; }

    /// <summary>
    /// Gets or sets the minutes component (0-59).
    /// </summary>
    public int Minutes { get; set; }

    /// <summary>
    /// Gets or sets the seconds component (0-59).
    /// </summary>
    public int Seconds { get; set; }

    /// <summary>
    /// Gets or sets the frames component (0 to frame rate - 1).
    /// </summary>
    public int Frames { get; set; }

    /// <summary>
    /// Gets or sets the frame rate for this timecode.
    /// </summary>
    public SMPTEFrameRate FrameRate { get; set; }


    /// <summary>
    /// Creates a new SMPTE timecode.
    /// </summary>
    /// <param name="hours">Hours (0-23).</param>
    /// <param name="minutes">Minutes (0-59).</param>
    /// <param name="seconds">Seconds (0-59).</param>
    /// <param name="frames">Frames (0 to frame rate - 1).</param>
    /// <param name="frameRate">The frame rate.</param>
    public SMPTETimecode(int hours, int minutes, int seconds, int frames, SMPTEFrameRate frameRate)
    {
        Hours = Math.Clamp(hours, 0, 23);
        Minutes = Math.Clamp(minutes, 0, 59);
        Seconds = Math.Clamp(seconds, 0, 59);
        Frames = Math.Clamp(frames, 0, GetMaxFrames(frameRate) - 1);
        FrameRate = frameRate;
    }


    /// <summary>
    /// Gets the maximum number of frames for a given frame rate.
    /// </summary>
    private static int GetMaxFrames(SMPTEFrameRate frameRate)
    {
        return frameRate switch
        {
            SMPTEFrameRate.FPS_24 => 24,
            SMPTEFrameRate.FPS_25 => 25,
            SMPTEFrameRate.FPS_29_97_DF => 30,
            SMPTEFrameRate.FPS_29_97_NDF => 30,
            SMPTEFrameRate.FPS_30 => 30,
            SMPTEFrameRate.FPS_30_DF => 30,
            _ => 30
        };
    }


    /// <summary>
    /// Gets the actual frames per second value for the frame rate.
    /// </summary>
    private static double GetFPS(SMPTEFrameRate frameRate)
    {
        return frameRate switch
        {
            SMPTEFrameRate.FPS_24 => 24.0,
            SMPTEFrameRate.FPS_25 => 25.0,
            SMPTEFrameRate.FPS_29_97_DF => 30000.0 / 1001.0,
            SMPTEFrameRate.FPS_29_97_NDF => 30000.0 / 1001.0,
            SMPTEFrameRate.FPS_30 => 30.0,
            SMPTEFrameRate.FPS_30_DF => 30.0,
            _ => 30.0
        };
    }


    /// <summary>
    /// Determines if the frame rate uses drop frame.
    /// </summary>
    private static bool IsDropFrame(SMPTEFrameRate frameRate)
    {
        return frameRate == SMPTEFrameRate.FPS_29_97_DF || frameRate == SMPTEFrameRate.FPS_30_DF;
    }


    /// <summary>
    /// Converts this timecode to seconds.
    /// </summary>
    /// <returns>The time in seconds.</returns>
    public double ToSeconds()
    {
        double fps = GetFPS(FrameRate);

        if (IsDropFrame(FrameRate))
        {
            // Drop frame calculation
            // In drop frame, frames 0 and 1 are dropped at the start of each minute,
            // except for minutes 0, 10, 20, 30, 40, and 50

            int totalMinutes = Hours * 60 + Minutes;
            int dropFrames = 2; // Number of frames dropped per minute (except every 10th minute)

            // Calculate total dropped frames
            int droppedFrames = dropFrames * (totalMinutes - totalMinutes / 10);

            // Calculate total frame count
            int totalFrames = Hours * 3600 * 30 +
                             Minutes * 60 * 30 +
                             Seconds * 30 +
                             Frames -
                             droppedFrames;

            return totalFrames / fps;
        }
        else
        {
            // Non-drop frame calculation
            double totalFrames = Hours * 3600.0 * fps +
                                Minutes * 60.0 * fps +
                                Seconds * fps +
                                Frames;

            return totalFrames / fps;
        }
    }


    /// <summary>
    /// Converts this timecode to total frame count.
    /// </summary>
    /// <returns>The total number of frames.</returns>
    public long ToFrames()
    {
        int maxFrames = GetMaxFrames(FrameRate);

        if (IsDropFrame(FrameRate))
        {
            int totalMinutes = Hours * 60 + Minutes;
            int dropFrames = 2;
            int droppedFrames = dropFrames * (totalMinutes - totalMinutes / 10);

            return (long)(Hours * 3600 * maxFrames +
                         Minutes * 60 * maxFrames +
                         Seconds * maxFrames +
                         Frames -
                         droppedFrames);
        }
        else
        {
            return (long)(Hours * 3600 * maxFrames +
                         Minutes * 60 * maxFrames +
                         Seconds * maxFrames +
                         Frames);
        }
    }


    /// <summary>
    /// Creates a timecode from a time in seconds.
    /// </summary>
    /// <param name="seconds">The time in seconds.</param>
    /// <param name="frameRate">The target frame rate.</param>
    /// <returns>The corresponding SMPTE timecode.</returns>
    public static SMPTETimecode FromSeconds(double seconds, SMPTEFrameRate frameRate)
    {
        if (seconds < 0)
            seconds = 0;

        double fps = GetFPS(frameRate);
        int maxFrames = GetMaxFrames(frameRate);

        if (IsDropFrame(frameRate))
        {
            // Convert seconds to frames, then to drop frame timecode
            long totalFrames = (long)Math.Round(seconds * fps);

            // Drop frame calculation
            int dropFramesPerMinute = 2;
            int framesPerMinute = maxFrames * 60 - dropFramesPerMinute;
            int framesPer10Minutes = framesPerMinute * 10 + dropFramesPerMinute;

            int tenMinuteBlocks = (int)(totalFrames / framesPer10Minutes);
            int remainingFrames = (int)(totalFrames % framesPer10Minutes);

            int additionalMinutes;
            if (remainingFrames < dropFramesPerMinute)
            {
                remainingFrames += dropFramesPerMinute;
                additionalMinutes = 0;
            }
            else
            {
                additionalMinutes = (remainingFrames - dropFramesPerMinute) / framesPerMinute + 1;
                remainingFrames = (remainingFrames - dropFramesPerMinute) % framesPerMinute + dropFramesPerMinute;
            }

            int totalMinutes = tenMinuteBlocks * 10 + additionalMinutes;

            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            int secs = remainingFrames / maxFrames;
            int frames = remainingFrames % maxFrames;

            return new SMPTETimecode(hours, minutes, secs, frames, frameRate);
        }
        else
        {
            // Non-drop frame calculation
            long totalFrames = (long)Math.Round(seconds * fps);

            int hours = (int)(totalFrames / (maxFrames * 3600));
            totalFrames %= maxFrames * 3600;

            int minutes = (int)(totalFrames / (maxFrames * 60));
            totalFrames %= maxFrames * 60;

            int secs = (int)(totalFrames / maxFrames);
            int frames = (int)(totalFrames % maxFrames);

            return new SMPTETimecode(hours, minutes, secs, frames, frameRate);
        }
    }


    /// <summary>
    /// Creates a timecode from a total frame count.
    /// </summary>
    /// <param name="totalFrames">The total frame count.</param>
    /// <param name="frameRate">The frame rate.</param>
    /// <returns>The corresponding SMPTE timecode.</returns>
    public static SMPTETimecode FromFrames(long totalFrames, SMPTEFrameRate frameRate)
    {
        double fps = GetFPS(frameRate);
        return FromSeconds(totalFrames / fps, frameRate);
    }


    /// <summary>
    /// Parses a timecode string in HH:MM:SS:FF or HH:MM:SS;FF format.
    /// </summary>
    /// <param name="timecode">The timecode string.</param>
    /// <param name="frameRate">The frame rate.</param>
    /// <returns>The parsed SMPTE timecode.</returns>
    public static SMPTETimecode Parse(string timecode, SMPTEFrameRate frameRate)
    {
        if (string.IsNullOrWhiteSpace(timecode))
            throw new ArgumentException("Timecode string cannot be null or empty.", nameof(timecode));

        // Support both : and ; as frame separator
        // ; typically indicates drop frame
        char[] separators = { ':', ';' };
        string[] parts = timecode.Split(separators);

        if (parts.Length != 4)
            throw new FormatException($"Invalid timecode format: {timecode}. Expected HH:MM:SS:FF or HH:MM:SS;FF");

        if (!int.TryParse(parts[0], out int hours) ||
            !int.TryParse(parts[1], out int minutes) ||
            !int.TryParse(parts[2], out int seconds) ||
            !int.TryParse(parts[3], out int frames))
        {
            throw new FormatException($"Invalid timecode values: {timecode}");
        }

        return new SMPTETimecode(hours, minutes, seconds, frames, frameRate);
    }


    /// <summary>
    /// Tries to parse a timecode string.
    /// </summary>
    /// <param name="timecode">The timecode string.</param>
    /// <param name="frameRate">The frame rate.</param>
    /// <param name="result">The parsed timecode.</param>
    /// <returns>True if parsing succeeded.</returns>
    public static bool TryParse(string timecode, SMPTEFrameRate frameRate, out SMPTETimecode result)
    {
        result = default;

        try
        {
            result = Parse(timecode, frameRate);
            return true;
        }
        catch
        {
            return false;
        }
    }


    /// <summary>
    /// Returns the timecode as a string in HH:MM:SS:FF format (or HH:MM:SS;FF for drop frame).
    /// </summary>
    public override string ToString()
    {
        char separator = IsDropFrame(FrameRate) ? ';' : ':';
        return $"{Hours:D2}:{Minutes:D2}:{Seconds:D2}{separator}{Frames:D2}";
    }


    /// <summary>
    /// Returns the timecode as a string with the specified separator.
    /// </summary>
    /// <param name="separator">The separator character.</param>
    public string ToString(char separator)
    {
        return $"{Hours:D2}{separator}{Minutes:D2}{separator}{Seconds:D2}{separator}{Frames:D2}";
    }


    /// <inheritdoc/>
    public bool Equals(SMPTETimecode other)
    {
        return Hours == other.Hours &&
               Minutes == other.Minutes &&
               Seconds == other.Seconds &&
               Frames == other.Frames &&
               FrameRate == other.FrameRate;
    }


    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is SMPTETimecode other && Equals(other);
    }


    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Hours, Minutes, Seconds, Frames, FrameRate);
    }


    /// <inheritdoc/>
    public int CompareTo(SMPTETimecode other)
    {
        // Compare by converting to frames for accuracy
        return ToFrames().CompareTo(other.ToFrames());
    }


    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(SMPTETimecode left, SMPTETimecode right)
    {
        return left.Equals(right);
    }


    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(SMPTETimecode left, SMPTETimecode right)
    {
        return !left.Equals(right);
    }


    /// <summary>
    /// Less than operator.
    /// </summary>
    public static bool operator <(SMPTETimecode left, SMPTETimecode right)
    {
        return left.CompareTo(right) < 0;
    }


    /// <summary>
    /// Greater than operator.
    /// </summary>
    public static bool operator >(SMPTETimecode left, SMPTETimecode right)
    {
        return left.CompareTo(right) > 0;
    }


    /// <summary>
    /// Less than or equal operator.
    /// </summary>
    public static bool operator <=(SMPTETimecode left, SMPTETimecode right)
    {
        return left.CompareTo(right) <= 0;
    }


    /// <summary>
    /// Greater than or equal operator.
    /// </summary>
    public static bool operator >=(SMPTETimecode left, SMPTETimecode right)
    {
        return left.CompareTo(right) >= 0;
    }


    /// <summary>
    /// Adds a number of frames to a timecode.
    /// </summary>
    public static SMPTETimecode operator +(SMPTETimecode tc, int frames)
    {
        return FromFrames(tc.ToFrames() + frames, tc.FrameRate);
    }


    /// <summary>
    /// Subtracts a number of frames from a timecode.
    /// </summary>
    public static SMPTETimecode operator -(SMPTETimecode tc, int frames)
    {
        return FromFrames(Math.Max(0, tc.ToFrames() - frames), tc.FrameRate);
    }
}
