//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Unit tests for the Arrangement class.

using FluentAssertions;
using MusicEngine.Core;
using Xunit;

namespace MusicEngine.Tests.Core;

public class ArrangementTests
{
    #region AudioClip Tests

    [Fact]
    public void AddAudioClip_ShouldAddClipToList()
    {
        // Arrange
        var arrangement = new Arrangement();
        var clip = new AudioClip("test.wav", 0, 4.0, 0);

        // Act
        var result = arrangement.AddAudioClip(clip);

        // Assert
        result.Should().BeTrue();
        arrangement.AudioClips.Should().HaveCount(1);
        arrangement.AudioClips[0].Should().BeSameAs(clip);
    }

    [Fact]
    public void AddAudioClip_WithParameters_ShouldCreateAndAddClip()
    {
        // Arrange
        var arrangement = new Arrangement();

        // Act
        var clip = arrangement.AddAudioClip("test.wav", 2.0, 8.0, 1);

        // Assert
        clip.Should().NotBeNull();
        clip.FilePath.Should().Be("test.wav");
        clip.StartPosition.Should().Be(2.0);
        clip.Length.Should().Be(8.0);
        clip.TrackIndex.Should().Be(1);
        arrangement.AudioClips.Should().HaveCount(1);
    }

    [Fact]
    public void AddAudioClip_DuplicateId_ShouldReturnFalse()
    {
        // Arrange
        var arrangement = new Arrangement();
        var clip = new AudioClip("test.wav", 0, 4.0);
        arrangement.AddAudioClip(clip);

        // Act
        var result = arrangement.AddAudioClip(clip);

        // Assert
        result.Should().BeFalse();
        arrangement.AudioClips.Should().HaveCount(1);
    }

    [Fact]
    public void AddAudioClip_ShouldFireAudioClipAddedEvent()
    {
        // Arrange
        var arrangement = new Arrangement();
        var clip = new AudioClip("test.wav", 0, 4.0);
        AudioClip? addedClip = null;
        arrangement.AudioClipAdded += (_, c) => addedClip = c;

        // Act
        arrangement.AddAudioClip(clip);

        // Assert
        addedClip.Should().BeSameAs(clip);
    }

    [Fact]
    public void RemoveAudioClip_ShouldRemoveClipFromList()
    {
        // Arrange
        var arrangement = new Arrangement();
        var clip = new AudioClip("test.wav", 0, 4.0);
        arrangement.AddAudioClip(clip);

        // Act
        var result = arrangement.RemoveAudioClip(clip);

        // Assert
        result.Should().BeTrue();
        arrangement.AudioClips.Should().BeEmpty();
    }

    [Fact]
    public void RemoveAudioClip_LockedClip_ShouldReturnFalse()
    {
        // Arrange
        var arrangement = new Arrangement();
        var clip = new AudioClip("test.wav", 0, 4.0) { IsLocked = true };
        arrangement.AddAudioClip(clip);

        // Act
        var result = arrangement.RemoveAudioClip(clip);

        // Assert
        result.Should().BeFalse();
        arrangement.AudioClips.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveAudioClip_ById_ShouldRemoveClip()
    {
        // Arrange
        var arrangement = new Arrangement();
        var clip = new AudioClip("test.wav", 0, 4.0);
        arrangement.AddAudioClip(clip);

        // Act
        var result = arrangement.RemoveAudioClip(clip.Id);

        // Assert
        result.Should().BeTrue();
        arrangement.AudioClips.Should().BeEmpty();
    }

    [Fact]
    public void RemoveAudioClip_ShouldFireAudioClipRemovedEvent()
    {
        // Arrange
        var arrangement = new Arrangement();
        var clip = new AudioClip("test.wav", 0, 4.0);
        arrangement.AddAudioClip(clip);
        AudioClip? removedClip = null;
        arrangement.AudioClipRemoved += (_, c) => removedClip = c;

        // Act
        arrangement.RemoveAudioClip(clip);

        // Assert
        removedClip.Should().BeSameAs(clip);
    }

    [Fact]
    public void AudioClipCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddAudioClip("a.wav", 0, 4.0);
        arrangement.AddAudioClip("b.wav", 4, 4.0);
        arrangement.AddAudioClip("c.wav", 8, 4.0);

        // Assert
        arrangement.AudioClipCount.Should().Be(3);
    }

    #endregion

    #region MidiClip Tests

    [Fact]
    public void AddMidiClip_ShouldAddClipToList()
    {
        // Arrange
        var arrangement = new Arrangement();
        var clip = new MidiClip(0, 4.0, 0);

        // Act
        var result = arrangement.AddMidiClip(clip);

        // Assert
        result.Should().BeTrue();
        arrangement.MidiClips.Should().HaveCount(1);
        arrangement.MidiClips[0].Should().BeSameAs(clip);
    }

    [Fact]
    public void AddMidiClip_WithParameters_ShouldCreateAndAddClip()
    {
        // Arrange
        var arrangement = new Arrangement();

        // Act
        var clip = arrangement.AddMidiClip(2.0, 8.0, 1);

        // Assert
        clip.Should().NotBeNull();
        clip.StartPosition.Should().Be(2.0);
        clip.Length.Should().Be(8.0);
        clip.TrackIndex.Should().Be(1);
        arrangement.MidiClips.Should().HaveCount(1);
    }

    [Fact]
    public void AddMidiClip_DuplicateId_ShouldReturnFalse()
    {
        // Arrange
        var arrangement = new Arrangement();
        var clip = new MidiClip(0, 4.0);
        arrangement.AddMidiClip(clip);

        // Act
        var result = arrangement.AddMidiClip(clip);

        // Assert
        result.Should().BeFalse();
        arrangement.MidiClips.Should().HaveCount(1);
    }

    [Fact]
    public void AddMidiClip_ShouldFireMidiClipAddedEvent()
    {
        // Arrange
        var arrangement = new Arrangement();
        var clip = new MidiClip(0, 4.0);
        MidiClip? addedClip = null;
        arrangement.MidiClipAdded += (_, c) => addedClip = c;

        // Act
        arrangement.AddMidiClip(clip);

        // Assert
        addedClip.Should().BeSameAs(clip);
    }

    [Fact]
    public void RemoveMidiClip_ShouldRemoveClipFromList()
    {
        // Arrange
        var arrangement = new Arrangement();
        var clip = new MidiClip(0, 4.0);
        arrangement.AddMidiClip(clip);

        // Act
        var result = arrangement.RemoveMidiClip(clip);

        // Assert
        result.Should().BeTrue();
        arrangement.MidiClips.Should().BeEmpty();
    }

    [Fact]
    public void RemoveMidiClip_LockedClip_ShouldReturnFalse()
    {
        // Arrange
        var arrangement = new Arrangement();
        var clip = new MidiClip(0, 4.0) { IsLocked = true };
        arrangement.AddMidiClip(clip);

        // Act
        var result = arrangement.RemoveMidiClip(clip);

        // Assert
        result.Should().BeFalse();
        arrangement.MidiClips.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveMidiClip_ById_ShouldRemoveClip()
    {
        // Arrange
        var arrangement = new Arrangement();
        var clip = new MidiClip(0, 4.0);
        arrangement.AddMidiClip(clip);

        // Act
        var result = arrangement.RemoveMidiClip(clip.Id);

        // Assert
        result.Should().BeTrue();
        arrangement.MidiClips.Should().BeEmpty();
    }

    [Fact]
    public void RemoveMidiClip_ShouldFireMidiClipRemovedEvent()
    {
        // Arrange
        var arrangement = new Arrangement();
        var clip = new MidiClip(0, 4.0);
        arrangement.AddMidiClip(clip);
        MidiClip? removedClip = null;
        arrangement.MidiClipRemoved += (_, c) => removedClip = c;

        // Act
        arrangement.RemoveMidiClip(clip);

        // Assert
        removedClip.Should().BeSameAs(clip);
    }

    [Fact]
    public void MidiClipCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddMidiClip(0, 4.0);
        arrangement.AddMidiClip(4, 4.0);
        arrangement.AddMidiClip(8, 4.0);

        // Assert
        arrangement.MidiClipCount.Should().Be(3);
    }

    #endregion

    #region Region Tests

    [Fact]
    public void AddRegion_ShouldAddRegionToList()
    {
        // Arrange
        var arrangement = new Arrangement();
        var region = new Region(0, 16, "Intro");

        // Act
        var result = arrangement.AddRegion(region);

        // Assert
        result.Should().BeTrue();
        arrangement.Regions.Should().HaveCount(1);
        arrangement.Regions[0].Should().BeSameAs(region);
    }

    [Fact]
    public void AddRegion_WithParameters_ShouldCreateAndAddRegion()
    {
        // Arrange
        var arrangement = new Arrangement();

        // Act
        var region = arrangement.AddRegion(0, 32, "Verse", RegionType.Section);

        // Assert
        region.Should().NotBeNull();
        region.StartPosition.Should().Be(0);
        region.EndPosition.Should().Be(32);
        region.Name.Should().Be("Verse");
        region.Type.Should().Be(RegionType.Section);
        arrangement.Regions.Should().HaveCount(1);
    }

    [Fact]
    public void AddRegion_DuplicateId_ShouldReturnFalse()
    {
        // Arrange
        var arrangement = new Arrangement();
        var region = new Region(0, 16);
        arrangement.AddRegion(region);

        // Act
        var result = arrangement.AddRegion(region);

        // Assert
        result.Should().BeFalse();
        arrangement.Regions.Should().HaveCount(1);
    }

    [Fact]
    public void AddRegion_ShouldFireRegionAddedEvent()
    {
        // Arrange
        var arrangement = new Arrangement();
        var region = new Region(0, 16);
        Region? addedRegion = null;
        arrangement.RegionAdded += (_, r) => addedRegion = r;

        // Act
        arrangement.AddRegion(region);

        // Assert
        addedRegion.Should().BeSameAs(region);
    }

    [Fact]
    public void RemoveRegion_ShouldRemoveRegionFromList()
    {
        // Arrange
        var arrangement = new Arrangement();
        var region = new Region(0, 16);
        arrangement.AddRegion(region);

        // Act
        var result = arrangement.RemoveRegion(region);

        // Assert
        result.Should().BeTrue();
        arrangement.Regions.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRegion_LockedRegion_ShouldReturnFalse()
    {
        // Arrange
        var arrangement = new Arrangement();
        var region = new Region(0, 16) { IsLocked = true };
        arrangement.AddRegion(region);

        // Act
        var result = arrangement.RemoveRegion(region);

        // Assert
        result.Should().BeFalse();
        arrangement.Regions.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveRegion_ById_ShouldRemoveRegion()
    {
        // Arrange
        var arrangement = new Arrangement();
        var region = new Region(0, 16);
        arrangement.AddRegion(region);

        // Act
        var result = arrangement.RemoveRegion(region.Id);

        // Assert
        result.Should().BeTrue();
        arrangement.Regions.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRegion_ShouldFireRegionRemovedEvent()
    {
        // Arrange
        var arrangement = new Arrangement();
        var region = new Region(0, 16);
        arrangement.AddRegion(region);
        Region? removedRegion = null;
        arrangement.RegionRemoved += (_, r) => removedRegion = r;

        // Act
        arrangement.RemoveRegion(region);

        // Assert
        removedRegion.Should().BeSameAs(region);
    }

    [Fact]
    public void RegionCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddRegion(0, 16, "A");
        arrangement.AddRegion(16, 32, "B");
        arrangement.AddRegion(32, 48, "C");

        // Assert
        arrangement.RegionCount.Should().Be(3);
    }

    #endregion

    #region GetClipsAt Tests

    [Fact]
    public void GetAudioClipsAt_ShouldReturnClipsContainingPosition()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddAudioClip("a.wav", 0, 8.0, 0);  // 0-8
        arrangement.AddAudioClip("b.wav", 4, 8.0, 1);  // 4-12
        arrangement.AddAudioClip("c.wav", 16, 4.0, 0); // 16-20

        // Act
        var clipsAt6 = arrangement.GetAudioClipsAt(6.0);

        // Assert
        clipsAt6.Should().HaveCount(2);
        clipsAt6.Should().Contain(c => c.FilePath == "a.wav");
        clipsAt6.Should().Contain(c => c.FilePath == "b.wav");
    }

    [Fact]
    public void GetAudioClipsAt_NoClipsAtPosition_ShouldReturnEmpty()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddAudioClip("a.wav", 0, 4.0);
        arrangement.AddAudioClip("b.wav", 8, 4.0);

        // Act
        var clipsAt6 = arrangement.GetAudioClipsAt(6.0);

        // Assert
        clipsAt6.Should().BeEmpty();
    }

    [Fact]
    public void GetMidiClipsAt_ShouldReturnClipsContainingPosition()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddMidiClip(0, 8.0, 0);  // 0-8
        arrangement.AddMidiClip(4, 8.0, 1);  // 4-12
        arrangement.AddMidiClip(16, 4.0, 0); // 16-20

        // Act
        var clipsAt6 = arrangement.GetMidiClipsAt(6.0);

        // Assert
        clipsAt6.Should().HaveCount(2);
    }

    [Fact]
    public void GetRegionsAt_ShouldReturnRegionsContainingPosition()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddRegion(0, 16, "Intro");
        arrangement.AddRegion(8, 24, "Verse");
        arrangement.AddRegion(32, 48, "Chorus");

        // Act
        var regionsAt10 = arrangement.GetRegionsAt(10.0);

        // Assert
        regionsAt10.Should().HaveCount(2);
        regionsAt10.Should().Contain(r => r.Name == "Intro");
        regionsAt10.Should().Contain(r => r.Name == "Verse");
    }

    #endregion

    #region GetClipsInRange Tests

    [Fact]
    public void GetAudioClipsInRange_ShouldReturnClipsOverlappingRange()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddAudioClip("a.wav", 0, 4.0);   // 0-4
        arrangement.AddAudioClip("b.wav", 3, 4.0);   // 3-7
        arrangement.AddAudioClip("c.wav", 8, 4.0);   // 8-12
        arrangement.AddAudioClip("d.wav", 20, 4.0);  // 20-24

        // Act
        var clipsInRange = arrangement.GetAudioClipsInRange(2.0, 10.0);

        // Assert
        clipsInRange.Should().HaveCount(3);
        clipsInRange.Should().Contain(c => c.FilePath == "a.wav");
        clipsInRange.Should().Contain(c => c.FilePath == "b.wav");
        clipsInRange.Should().Contain(c => c.FilePath == "c.wav");
    }

    [Fact]
    public void GetMidiClipsInRange_ShouldReturnClipsOverlappingRange()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddMidiClip(0, 4.0);   // 0-4
        arrangement.AddMidiClip(3, 4.0);   // 3-7
        arrangement.AddMidiClip(8, 4.0);   // 8-12

        // Act
        var clipsInRange = arrangement.GetMidiClipsInRange(2.0, 6.0);

        // Assert
        clipsInRange.Should().HaveCount(2);
    }

    [Fact]
    public void GetRegionsInRange_ShouldReturnRegionsOverlappingRange()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddRegion(0, 16, "A");
        arrangement.AddRegion(8, 24, "B");
        arrangement.AddRegion(32, 48, "C");

        // Act
        var regionsInRange = arrangement.GetRegionsInRange(4.0, 20.0);

        // Assert
        regionsInRange.Should().HaveCount(2);
        regionsInRange.Should().Contain(r => r.Name == "A");
        regionsInRange.Should().Contain(r => r.Name == "B");
    }

    #endregion

    #region Loop Region Tests

    [Fact]
    public void SetLoopRegion_ShouldCreateLoopRegion()
    {
        // Arrange
        var arrangement = new Arrangement();

        // Act
        var loopRegion = arrangement.SetLoopRegion(8.0, 24.0);

        // Assert
        loopRegion.Should().NotBeNull();
        loopRegion.StartPosition.Should().Be(8.0);
        loopRegion.EndPosition.Should().Be(24.0);
        loopRegion.Type.Should().Be(RegionType.Loop);
    }

    [Fact]
    public void SetLoopRegion_ShouldReplaceExistingLoopRegion()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.SetLoopRegion(0, 16);
        arrangement.Regions.Should().HaveCount(1);

        // Act
        arrangement.SetLoopRegion(8, 32);

        // Assert
        arrangement.Regions.Where(r => r.Type == RegionType.Loop).Should().HaveCount(1);
        var loop = arrangement.GetLoopRegion();
        loop!.StartPosition.Should().Be(8);
        loop.EndPosition.Should().Be(32);
    }

    [Fact]
    public void GetLoopRegion_ShouldReturnActiveLoopRegion()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.SetLoopRegion(4.0, 20.0);

        // Act
        var loopRegion = arrangement.GetLoopRegion();

        // Assert
        loopRegion.Should().NotBeNull();
        loopRegion!.StartPosition.Should().Be(4.0);
        loopRegion.EndPosition.Should().Be(20.0);
    }

    [Fact]
    public void GetLoopRegion_NoLoopSet_ShouldReturnNull()
    {
        // Arrange
        var arrangement = new Arrangement();

        // Act
        var loopRegion = arrangement.GetLoopRegion();

        // Assert
        loopRegion.Should().BeNull();
    }

    [Fact]
    public void GetLoopRegion_InactiveLoop_ShouldReturnNull()
    {
        // Arrange
        var arrangement = new Arrangement();
        var loopRegion = arrangement.SetLoopRegion(0, 16);
        loopRegion.IsActive = false;

        // Act
        var result = arrangement.GetLoopRegion();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region TotalLengthWithClips Tests

    [Fact]
    public void TotalLengthWithClips_EmptyArrangement_ShouldBeZero()
    {
        // Arrange
        var arrangement = new Arrangement();

        // Assert
        arrangement.TotalLengthWithClips.Should().Be(0);
    }

    [Fact]
    public void TotalLengthWithClips_ShouldReturnMaxEndPosition()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddAudioClip("a.wav", 0, 16.0);  // ends at 16
        arrangement.AddMidiClip(8, 32.0);             // ends at 40
        arrangement.AddSection(0, 24);                // ends at 24

        // Assert
        arrangement.TotalLengthWithClips.Should().Be(40);
    }

    [Fact]
    public void TotalLengthWithClips_WithOnlyAudioClips_ShouldCalculateCorrectly()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddAudioClip("a.wav", 0, 8.0);   // ends at 8
        arrangement.AddAudioClip("b.wav", 4, 20.0);  // ends at 24
        arrangement.AddAudioClip("c.wav", 16, 4.0);  // ends at 20

        // Assert
        arrangement.TotalLengthWithClips.Should().Be(24);
    }

    [Fact]
    public void TotalLengthWithClips_WithOnlyMidiClips_ShouldCalculateCorrectly()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddMidiClip(0, 16.0);
        arrangement.AddMidiClip(8, 24.0);  // ends at 32

        // Assert
        arrangement.TotalLengthWithClips.Should().Be(32);
    }

    [Fact]
    public void TotalLengthWithClips_WithSections_ShouldIncludeSectionLength()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddSection(0, 32, "Full Song");
        arrangement.AddMidiClip(0, 16.0);

        // Assert
        arrangement.TotalLengthWithClips.Should().Be(32);
    }

    [Fact]
    public void TotalLengthWithClips_WithSectionRepeats_ShouldIncludeEffectiveLength()
    {
        // Arrange
        var arrangement = new Arrangement();
        var section = arrangement.AddSection(0, 16, "Repeating");
        section.RepeatCount = 4;  // Effective end = 64

        // Assert
        arrangement.TotalLengthWithClips.Should().Be(64);
    }

    #endregion

    #region Section Tests

    [Fact]
    public void AddSection_ShouldAddSectionToArrangement()
    {
        // Arrange
        var arrangement = new Arrangement();

        // Act
        var section = arrangement.AddSection(0, 16, "Intro");

        // Assert
        section.Should().NotBeNull();
        arrangement.Sections.Should().HaveCount(1);
        arrangement.Sections[0].Name.Should().Be("Intro");
    }

    [Fact]
    public void AddSection_WithType_ShouldSetCorrectProperties()
    {
        // Arrange
        var arrangement = new Arrangement();

        // Act
        var section = arrangement.AddSection(0, 32, SectionType.Chorus);

        // Assert
        section.Type.Should().Be(SectionType.Chorus);
        section.Name.Should().Be("Chorus");
    }

    [Fact]
    public void RemoveSection_ShouldRemoveSectionFromArrangement()
    {
        // Arrange
        var arrangement = new Arrangement();
        var section = arrangement.AddSection(0, 16, "ToRemove");

        // Act
        var result = arrangement.RemoveSection(section);

        // Assert
        result.Should().BeTrue();
        arrangement.Sections.Should().BeEmpty();
    }

    [Fact]
    public void RemoveSection_LockedSection_ShouldReturnFalse()
    {
        // Arrange
        var arrangement = new Arrangement();
        var section = arrangement.AddSection(0, 16);
        section.IsLocked = true;

        // Act
        var result = arrangement.RemoveSection(section);

        // Assert
        result.Should().BeFalse();
        arrangement.Sections.Should().HaveCount(1);
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void ClearClips_ShouldRemoveAllUnlockedClips()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddAudioClip("a.wav", 0, 4.0);
        arrangement.AddAudioClip("b.wav", 4, 4.0);
        arrangement.AddMidiClip(0, 8.0);
        arrangement.AddMidiClip(8, 8.0);

        // Act
        var removed = arrangement.ClearClips();

        // Assert
        removed.Should().Be(4);
        arrangement.AudioClips.Should().BeEmpty();
        arrangement.MidiClips.Should().BeEmpty();
    }

    [Fact]
    public void ClearClips_ShouldNotRemoveLockedClips()
    {
        // Arrange
        var arrangement = new Arrangement();
        var unlockedClip = arrangement.AddAudioClip("unlocked.wav", 0, 4.0);
        var lockedClip = arrangement.AddAudioClip("locked.wav", 4, 4.0);
        lockedClip.IsLocked = true;

        // Act
        var removed = arrangement.ClearClips();

        // Assert
        removed.Should().Be(1);
        arrangement.AudioClips.Should().HaveCount(1);
        arrangement.AudioClips[0].Should().BeSameAs(lockedClip);
    }

    [Fact]
    public void ClearClips_IncludeLockedTrue_ShouldRemoveAllClips()
    {
        // Arrange
        var arrangement = new Arrangement();
        var lockedClip = arrangement.AddAudioClip("locked.wav", 0, 4.0);
        lockedClip.IsLocked = true;
        arrangement.AddMidiClip(0, 4.0);

        // Act
        var removed = arrangement.ClearClips(includeLockedClips: true);

        // Assert
        removed.Should().Be(2);
        arrangement.AudioClips.Should().BeEmpty();
        arrangement.MidiClips.Should().BeEmpty();
    }

    [Fact]
    public void ClearRegions_ShouldRemoveAllUnlockedRegions()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddRegion(0, 16, "A");
        arrangement.AddRegion(16, 32, "B");
        arrangement.AddRegion(32, 48, "C");

        // Act
        var removed = arrangement.ClearRegions();

        // Assert
        removed.Should().Be(3);
        arrangement.Regions.Should().BeEmpty();
    }

    [Fact]
    public void ClearRegions_ShouldNotRemoveLockedRegions()
    {
        // Arrange
        var arrangement = new Arrangement();
        var lockedRegion = arrangement.AddRegion(0, 16, "Locked");
        lockedRegion.IsLocked = true;
        arrangement.AddRegion(16, 32, "Unlocked");

        // Act
        var removed = arrangement.ClearRegions();

        // Assert
        removed.Should().Be(1);
        arrangement.Regions.Should().HaveCount(1);
        arrangement.Regions[0].Name.Should().Be("Locked");
    }

    #endregion

    #region Clips On Track Tests

    [Fact]
    public void GetAudioClipsOnTrack_ShouldReturnClipsOnSpecificTrack()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddAudioClip("a.wav", 0, 4.0, 0);
        arrangement.AddAudioClip("b.wav", 4, 4.0, 1);
        arrangement.AddAudioClip("c.wav", 8, 4.0, 0);
        arrangement.AddAudioClip("d.wav", 12, 4.0, 2);

        // Act
        var track0Clips = arrangement.GetAudioClipsOnTrack(0);
        var track1Clips = arrangement.GetAudioClipsOnTrack(1);

        // Assert
        track0Clips.Should().HaveCount(2);
        track1Clips.Should().HaveCount(1);
    }

    [Fact]
    public void GetMidiClipsOnTrack_ShouldReturnClipsOnSpecificTrack()
    {
        // Arrange
        var arrangement = new Arrangement();
        arrangement.AddMidiClip(0, 4.0, 0);
        arrangement.AddMidiClip(4, 4.0, 1);
        arrangement.AddMidiClip(8, 4.0, 0);

        // Act
        var track0Clips = arrangement.GetMidiClipsOnTrack(0);

        // Assert
        track0Clips.Should().HaveCount(2);
    }

    #endregion
}
