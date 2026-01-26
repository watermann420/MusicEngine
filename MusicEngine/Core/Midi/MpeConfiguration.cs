// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MIDI handling component.

using System;


namespace MusicEngine.Core.Midi;


/// <summary>
/// MPE Zone type according to the MPE specification.
/// </summary>
public enum MpeZone
{
    /// <summary>
    /// Lower Zone: Master Channel 1, Member Channels 2-15.
    /// </summary>
    Lower,

    /// <summary>
    /// Upper Zone: Master Channel 16, Member Channels 15-2.
    /// </summary>
    Upper
}


/// <summary>
/// Configuration for an MPE zone.
/// MPE (MIDI Polyphonic Expression) allows per-note pitch bend, slide (CC74), and pressure.
/// </summary>
/// <remarks>
/// The MPE specification defines two possible zones:
/// - Lower Zone: Master on Channel 1, member channels 2 upward
/// - Upper Zone: Master on Channel 16, member channels 15 downward
///
/// Member channels carry per-note expression data:
/// - Pitch Bend: Per-note pitch (typically 48 semitones range)
/// - CC74 (Slide): Y-axis movement, typically mapped to brightness/timbre
/// - Channel Pressure: Z-axis pressure (aftertouch)
/// </remarks>
public class MpeConfiguration
{
    private int _memberChannelCount = 15;
    private int _pitchBendRange = 48;

    /// <summary>
    /// Gets or sets whether MPE mode is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the zone type (Lower or Upper).
    /// Lower Zone uses Channel 1 as master, Upper Zone uses Channel 16.
    /// </summary>
    public MpeZone Zone { get; set; } = MpeZone.Lower;

    /// <summary>
    /// Gets the master channel based on the zone type.
    /// Lower Zone: Channel 1 (index 0)
    /// Upper Zone: Channel 16 (index 15)
    /// </summary>
    public int MasterChannel => Zone == MpeZone.Lower ? 0 : 15;

    /// <summary>
    /// Gets or sets the number of member channels (1-15).
    /// For Lower Zone: channels 2 through (1 + MemberChannelCount)
    /// For Upper Zone: channels 15 through (16 - MemberChannelCount)
    /// </summary>
    public int MemberChannelCount
    {
        get => _memberChannelCount;
        set => _memberChannelCount = Math.Clamp(value, 1, 15);
    }

    /// <summary>
    /// Gets or sets the pitch bend range in semitones.
    /// MPE typically uses 48 semitones for wide pitch expression.
    /// Standard MIDI uses 2 semitones.
    /// </summary>
    public int PitchBendRange
    {
        get => _pitchBendRange;
        set => _pitchBendRange = Math.Clamp(value, 1, 96);
    }

    /// <summary>
    /// Gets or sets the default slide (CC74) value (0-127).
    /// This is the neutral position for the Y-axis.
    /// </summary>
    public int DefaultSlide { get; set; } = 64;

    /// <summary>
    /// Gets or sets the default pressure value (0-127).
    /// </summary>
    public int DefaultPressure { get; set; } = 0;

    /// <summary>
    /// Gets the first member channel (0-based index).
    /// </summary>
    public int FirstMemberChannel
    {
        get
        {
            if (Zone == MpeZone.Lower)
            {
                return 1; // Channel 2 (index 1)
            }
            else
            {
                return 15 - _memberChannelCount; // Starts from high channels going down
            }
        }
    }

    /// <summary>
    /// Gets the last member channel (0-based index).
    /// </summary>
    public int LastMemberChannel
    {
        get
        {
            if (Zone == MpeZone.Lower)
            {
                return Math.Min(15, _memberChannelCount); // Channel 2 + count - 1
            }
            else
            {
                return 14; // Channel 15 (index 14)
            }
        }
    }

    /// <summary>
    /// Determines if a channel is a member channel in this configuration.
    /// </summary>
    /// <param name="channel">The MIDI channel (0-15).</param>
    /// <returns>True if the channel is a member channel.</returns>
    public bool IsMemberChannel(int channel)
    {
        if (!Enabled) return false;

        if (Zone == MpeZone.Lower)
        {
            return channel >= 1 && channel <= _memberChannelCount;
        }
        else
        {
            return channel >= (15 - _memberChannelCount) && channel <= 14;
        }
    }

    /// <summary>
    /// Determines if a channel is the master channel in this configuration.
    /// </summary>
    /// <param name="channel">The MIDI channel (0-15).</param>
    /// <returns>True if the channel is the master channel.</returns>
    public bool IsMasterChannel(int channel)
    {
        return Enabled && channel == MasterChannel;
    }

    /// <summary>
    /// Gets an array of all member channel indices.
    /// </summary>
    /// <returns>Array of channel indices (0-15).</returns>
    public int[] GetMemberChannels()
    {
        var channels = new int[_memberChannelCount];

        if (Zone == MpeZone.Lower)
        {
            for (int i = 0; i < _memberChannelCount; i++)
            {
                channels[i] = i + 1;
            }
        }
        else
        {
            for (int i = 0; i < _memberChannelCount; i++)
            {
                channels[i] = 14 - i;
            }
        }

        return channels;
    }

    /// <summary>
    /// Creates a default MPE configuration for the Lower Zone.
    /// </summary>
    public static MpeConfiguration CreateLowerZone(int memberChannels = 15)
    {
        return new MpeConfiguration
        {
            Enabled = true,
            Zone = MpeZone.Lower,
            MemberChannelCount = memberChannels,
            PitchBendRange = 48
        };
    }

    /// <summary>
    /// Creates a default MPE configuration for the Upper Zone.
    /// </summary>
    public static MpeConfiguration CreateUpperZone(int memberChannels = 15)
    {
        return new MpeConfiguration
        {
            Enabled = true,
            Zone = MpeZone.Upper,
            MemberChannelCount = memberChannels,
            PitchBendRange = 48
        };
    }

    /// <summary>
    /// Creates a configuration for dual-zone MPE (both zones active).
    /// Note: This only creates the Lower Zone configuration.
    /// Use a second MpeConfiguration for the Upper Zone.
    /// </summary>
    /// <param name="lowerChannels">Number of channels for Lower Zone.</param>
    /// <param name="upperChannels">Number of channels for Upper Zone.</param>
    /// <returns>Tuple of (Lower Zone config, Upper Zone config).</returns>
    public static (MpeConfiguration Lower, MpeConfiguration Upper) CreateDualZone(
        int lowerChannels = 7, int upperChannels = 7)
    {
        // Ensure total doesn't exceed available channels (14 member channels max when both zones active)
        int total = lowerChannels + upperChannels;
        if (total > 14)
        {
            float ratio = 14f / total;
            lowerChannels = (int)(lowerChannels * ratio);
            upperChannels = 14 - lowerChannels;
        }

        var lower = new MpeConfiguration
        {
            Enabled = true,
            Zone = MpeZone.Lower,
            MemberChannelCount = lowerChannels,
            PitchBendRange = 48
        };

        var upper = new MpeConfiguration
        {
            Enabled = true,
            Zone = MpeZone.Upper,
            MemberChannelCount = upperChannels,
            PitchBendRange = 48
        };

        return (lower, upper);
    }
}
