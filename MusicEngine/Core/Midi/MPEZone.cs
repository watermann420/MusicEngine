// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MIDI handling component.

namespace MusicEngine.Core.Midi;

/// <summary>
/// Represents an MPE (MIDI Polyphonic Expression) zone configuration.
/// MPE zones allow per-note expression control using dedicated MIDI channels.
/// </summary>
public class MPEZone
{
    /// <summary>
    /// The master channel for the zone (1 for Lower Zone, 16 for Upper Zone).
    /// The master channel receives zone-wide messages like sustain pedal.
    /// </summary>
    public int MasterChannel { get; }

    /// <summary>
    /// The first member channel in this zone.
    /// Member channels carry per-note expression data.
    /// </summary>
    public int MemberChannelStart { get; }

    /// <summary>
    /// The number of member channels allocated to this zone (1-15).
    /// More channels allow more simultaneous notes with independent expression.
    /// </summary>
    public int MemberChannelCount { get; }

    /// <summary>
    /// The pitch bend range in semitones for member channels.
    /// MPE typically uses 48 semitones for wide pitch expression.
    /// </summary>
    public int PitchBendRange { get; set; } = 48;

    /// <summary>
    /// Creates a new MPE zone.
    /// </summary>
    /// <param name="masterChannel">The master channel (1 or 16)</param>
    /// <param name="memberChannelStart">The first member channel</param>
    /// <param name="memberChannelCount">Number of member channels (1-15)</param>
    public MPEZone(int masterChannel, int memberChannelStart, int memberChannelCount)
    {
        if (masterChannel != 1 && masterChannel != 16)
        {
            throw new ArgumentException("Master channel must be 1 (Lower Zone) or 16 (Upper Zone)", nameof(masterChannel));
        }

        if (memberChannelCount < 1 || memberChannelCount > 15)
        {
            throw new ArgumentException("Member channel count must be between 1 and 15", nameof(memberChannelCount));
        }

        MasterChannel = masterChannel;
        MemberChannelStart = memberChannelStart;
        MemberChannelCount = memberChannelCount;
    }

    /// <summary>
    /// Determines if a MIDI channel belongs to this MPE zone.
    /// </summary>
    /// <param name="channel">The MIDI channel (1-16)</param>
    /// <returns>True if the channel is the master or a member channel of this zone</returns>
    public bool IsChannelInZone(int channel)
    {
        if (channel == MasterChannel)
        {
            return true;
        }

        return channel >= MemberChannelStart && channel < MemberChannelStart + MemberChannelCount;
    }

    /// <summary>
    /// Checks if a channel is a member channel (not the master).
    /// </summary>
    /// <param name="channel">The MIDI channel (1-16)</param>
    /// <returns>True if the channel is a member channel</returns>
    public bool IsMemberChannel(int channel)
    {
        return channel >= MemberChannelStart && channel < MemberChannelStart + MemberChannelCount;
    }

    /// <summary>
    /// Gets the next available member channel for note allocation using round-robin.
    /// </summary>
    /// <param name="lastUsedChannel">The last channel that was used</param>
    /// <returns>The next member channel to use</returns>
    public int GetNextMemberChannel(int lastUsedChannel)
    {
        int next = lastUsedChannel + 1;
        if (next >= MemberChannelStart + MemberChannelCount)
        {
            next = MemberChannelStart;
        }
        return next;
    }
}
