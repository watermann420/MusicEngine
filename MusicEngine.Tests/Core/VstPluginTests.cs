//Engine License (MEL) - Honor-Based Commercial Support
// copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Created by Watermann420
// Description: Unit tests for IVstPlugin implementations.

using FluentAssertions;
using MusicEngine.Core;
using MusicEngine.Tests.Helpers;
using MusicEngine.Tests.Mocks;
using NAudio.Wave;
using Xunit;

namespace MusicEngine.Tests.Core;

/// <summary>
/// Unit tests for IVstPlugin interface implementations covering VST2 and VST3 plugins.
/// </summary>
public class VstPluginTests
{
    #region IVstPlugin Interface Tests

    [Fact]
    public void MockVstPlugin_ImplementsIVstPlugin()
    {
        var plugin = new MockVstPlugin();

        plugin.Should().BeAssignableTo<IVstPlugin>();
    }

    [Fact]
    public void MockVstPlugin_IsVst3_ReturnsFalse()
    {
        var plugin = new MockVstPlugin();

        plugin.IsVst3.Should().BeFalse();
    }

    [Fact]
    public void MockVst3Plugin_IsVst3_ReturnsTrue()
    {
        var plugin = new MockVst3Plugin();

        plugin.IsVst3.Should().BeTrue();
    }

    [Fact]
    public void MockVstPlugin_Name_CanBeSet()
    {
        var plugin = new MockVstPlugin { Name = "CustomName" };

        plugin.Name.Should().Be("CustomName");
    }

    [Fact]
    public void MockVstPlugin_WaveFormat_ReturnsCorrectFormat()
    {
        var plugin = new MockVstPlugin(sampleRate: 48000, channels: 2);

        plugin.WaveFormat.SampleRate.Should().Be(48000);
        plugin.WaveFormat.Channels.Should().Be(2);
    }

    #endregion

    #region ProcessBlock Tests

    [Fact]
    public void VstPlugin_Read_ReturnsRequestedSamples()
    {
        var plugin = new MockVstPlugin();
        var buffer = new float[1024];

        int read = plugin.Read(buffer, 0, buffer.Length);

        read.Should().Be(buffer.Length);
    }

    [Fact]
    public void VstPlugin_Read_WhenBypassed_OutputsSilence()
    {
        var plugin = new MockVstPlugin { IsBypassed = true };
        plugin.NoteOn(60, 100);
        var buffer = new float[1024];

        plugin.Read(buffer, 0, buffer.Length);

        VstTestHelper.IsSilent(buffer).Should().BeTrue();
    }

    [Fact]
    public void VstPlugin_Read_WithActiveNotes_ProducesAudio()
    {
        var plugin = new MockVstPlugin { IsInstrument = true };
        plugin.NoteOn(60, 100);
        var buffer = new float[1024];

        plugin.Read(buffer, 0, buffer.Length);

        VstTestHelper.HasAudioContent(buffer).Should().BeTrue();
    }

    [Fact]
    public void VstPlugin_Read_Effect_PassesThroughInput()
    {
        var plugin = MockVstPlugin.CreateEffect();
        var inputBuffer = VstTestHelper.CreateSineWaveBuffer(440f, 512);
        var inputProvider = new MockSampleProvider(inputBuffer);
        plugin.InputProvider = inputProvider;

        var outputBuffer = new float[1024];
        plugin.Read(outputBuffer, 0, outputBuffer.Length);

        VstTestHelper.HasAudioContent(outputBuffer).Should().BeTrue();
    }

    [Fact]
    public void VstPlugin_MasterVolume_AffectsOutput()
    {
        var plugin = new MockVstPlugin { IsInstrument = true, MasterVolume = 0.5f };
        plugin.NoteOn(60, 100);

        var buffer1 = new float[512];
        plugin.Read(buffer1, 0, buffer1.Length);
        var peak1 = VstTestHelper.GetPeakAmplitude(buffer1);

        plugin.Reset();
        plugin.MasterVolume = 1.0f;
        plugin.NoteOn(60, 100);
        var buffer2 = new float[512];
        plugin.Read(buffer2, 0, buffer2.Length);
        var peak2 = VstTestHelper.GetPeakAmplitude(buffer2);

        // Higher volume should produce higher peak (approximately 2x)
        (peak2 > peak1).Should().BeTrue();
    }

    #endregion

    #region Parameter Tests

    [Fact]
    public void GetParameterCount_ReturnsCorrectCount()
    {
        var plugin = new MockVstPlugin();

        plugin.GetParameterCount().Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetParameterValue_ReturnsValueInRange()
    {
        var plugin = new MockVstPlugin();

        var value = plugin.GetParameterValue(0);

        value.Should().BeInRange(0f, 1f);
    }

    [Fact]
    public void SetParameterValue_ClampsToValidRange()
    {
        var plugin = new MockVstPlugin();

        plugin.SetParameterValue(0, 1.5f);
        plugin.GetParameterValue(0).Should().Be(1.0f);

        plugin.SetParameterValue(0, -0.5f);
        plugin.GetParameterValue(0).Should().Be(0.0f);
    }

    [Fact]
    public void GetParameterName_ReturnsName()
    {
        var plugin = new MockVstPlugin();

        var name = plugin.GetParameterName(0);

        name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetParameterDisplay_ReturnsFormattedValue()
    {
        var plugin = new MockVstPlugin();
        plugin.SetParameterValue(0, 0.75f);

        var display = plugin.GetParameterDisplay(0);

        display.Should().Contain("0.75");
    }

    [Fact]
    public void SetParameter_ByName_SetsValue()
    {
        var plugin = new MockVstPlugin();
        plugin.AddParameter(5, "Cutoff", 0.5f);

        plugin.SetParameter("Cutoff", 0.8f);

        plugin.GetParameterValue(5).Should().Be(0.8f);
    }

    [Fact]
    public void GetParameterInfo_ValidIndex_ReturnsInfo()
    {
        var plugin = new MockVstPlugin();

        var info = plugin.GetParameterInfo(0);

        info.Should().NotBeNull();
        info!.Index.Should().Be(0);
        info.IsAutomatable.Should().BeTrue();
    }

    [Fact]
    public void GetParameterInfo_InvalidIndex_ReturnsNull()
    {
        var plugin = new MockVstPlugin();

        var info = plugin.GetParameterInfo(999);

        info.Should().BeNull();
    }

    [Fact]
    public void GetAllParameterInfo_ReturnsAllParameters()
    {
        var plugin = new MockVstPlugin();

        var allInfo = plugin.GetAllParameterInfo();

        allInfo.Should().HaveCount(plugin.GetParameterCount());
    }

    [Fact]
    public void CanParameterBeAutomated_ValidIndex_ReturnsTrue()
    {
        var plugin = new MockVstPlugin();

        var canAutomate = plugin.CanParameterBeAutomated(0);

        canAutomate.Should().BeTrue();
    }

    [Fact]
    public void CanParameterBeAutomated_InvalidIndex_ReturnsFalse()
    {
        var plugin = new MockVstPlugin();

        var canAutomate = plugin.CanParameterBeAutomated(999);

        canAutomate.Should().BeFalse();
    }

    #endregion

    #region VST3 Specific Tests

    [Fact]
    public void Vst3Plugin_GetUnits_ReturnsUnits()
    {
        var plugin = new MockVst3Plugin();

        var units = plugin.GetUnits();

        units.Should().NotBeEmpty();
    }

    [Fact]
    public void Vst3Plugin_GetParametersInUnit_ReturnsParameterIndices()
    {
        var plugin = new MockVst3Plugin();

        var parameters = plugin.GetParametersInUnit(0);

        parameters.Should().NotBeEmpty();
    }

    [Fact]
    public void Vst3Plugin_SendNoteExpression_RecordsExpression()
    {
        var plugin = MockVst3Plugin.CreateInstrumentWithNoteExpression();

        plugin.SendNoteExpression(1, Vst3NoteExpressionType.Volume, 0.8);

        plugin.RecordedNoteExpressions.Should().Contain(ne =>
            ne.NoteId == 1 && ne.Type == Vst3NoteExpressionType.Volume && ne.Value == 0.8);
    }

    [Fact]
    public void Vst3Plugin_SupportsNoteExpression_ReturnsCorrectValue()
    {
        var pluginWithNE = MockVst3Plugin.CreateInstrumentWithNoteExpression();
        var pluginWithoutNE = new MockVst3Plugin { SupportsNoteExpression = false };

        pluginWithNE.SupportsNoteExpression.Should().BeTrue();
        pluginWithoutNE.SupportsNoteExpression.Should().BeFalse();
    }

    [Fact]
    public void Vst3Plugin_SupportsSidechain_ReturnsCorrectValue()
    {
        var plugin = MockVst3Plugin.CreateEffectWithSidechain();

        plugin.SupportsSidechain.Should().BeTrue();
        plugin.SidechainBusIndex.Should().Be(1);
    }

    [Fact]
    public void Vst3Plugin_GetBusCount_ReturnsCorrectCount()
    {
        var plugin = new MockVst3Plugin();

        var inputCount = plugin.GetBusCount(Vst3MediaType.Audio, Vst3BusDirection.Input);
        var outputCount = plugin.GetBusCount(Vst3MediaType.Audio, Vst3BusDirection.Output);

        inputCount.Should().BeGreaterThan(0);
        outputCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Vst3Plugin_GetBusInfo_ReturnsInfo()
    {
        var plugin = new MockVst3Plugin();

        var busInfo = plugin.GetBusInfo(Vst3MediaType.Audio, Vst3BusDirection.Output, 0);

        busInfo.ChannelCount.Should().Be(2);
        busInfo.BusType.Should().Be(Vst3BusType.Main);
    }

    [Fact]
    public void Vst3Plugin_SetBusActive_SetsActiveState()
    {
        var plugin = MockVst3Plugin.CreateEffectWithSidechain();

        var result = plugin.SetBusActive(Vst3MediaType.Audio, Vst3BusDirection.Input, 1, true);

        result.Should().BeTrue();
    }

    [Fact]
    public void Vst3Plugin_LatencySamples_CanBeSet()
    {
        var plugin = new MockVst3Plugin { LatencySamples = 256 };

        plugin.LatencySamples.Should().Be(256);
    }

    #endregion

    #region Preset Management Tests

    [Fact]
    public void SavePreset_ReturnsTrue()
    {
        var plugin = new MockVstPlugin();
        var tempPath = Path.Combine(Path.GetTempPath(), "test_preset.fxp");

        var result = plugin.SavePreset(tempPath);

        result.Should().BeTrue();
        plugin.LastSavedPresetPath.Should().Be(tempPath);
    }

    [Fact]
    public void LoadPreset_ValidPath_ReturnsTrue()
    {
        var plugin = new MockVstPlugin();

        var result = plugin.LoadPreset("valid_preset.fxp");

        result.Should().BeTrue();
    }

    [Fact]
    public void LoadPreset_InvalidPath_ReturnsFalse()
    {
        var plugin = new MockVstPlugin();

        var result = plugin.LoadPreset("nonexistent_invalid.fxp");

        result.Should().BeFalse();
    }

    [Fact]
    public void GetPresetNames_ReturnsPresets()
    {
        var plugin = new MockVstPlugin();

        var presets = plugin.GetPresetNames();

        presets.Should().NotBeEmpty();
    }

    [Fact]
    public void SetPreset_ValidIndex_ChangesPreset()
    {
        var plugin = new MockVstPlugin();
        var presets = plugin.GetPresetNames();

        plugin.SetPreset(1);

        plugin.CurrentPresetIndex.Should().Be(1);
        plugin.CurrentPresetName.Should().Be(presets[1]);
    }

    [Fact]
    public void SetPreset_InvalidIndex_DoesNotChange()
    {
        var plugin = new MockVstPlugin();
        var originalIndex = plugin.CurrentPresetIndex;

        plugin.SetPreset(999);

        plugin.CurrentPresetIndex.Should().Be(originalIndex);
    }

    [Fact]
    public void Vst3Plugin_LoadPreset_VstpresetFormat_ReturnsTrue()
    {
        var plugin = new MockVst3Plugin();

        var result = plugin.LoadPreset("preset.vstpreset");

        result.Should().BeTrue();
    }

    [Fact]
    public void Vst3Plugin_SavePreset_VstpresetFormat_ReturnsTrue()
    {
        var plugin = new MockVst3Plugin();

        var result = plugin.SavePreset("output.vstpreset");

        result.Should().BeTrue();
    }

    #endregion

    #region MIDI Handling Tests

    [Fact]
    public void NoteOn_AddsActiveNote()
    {
        var plugin = new MockVstPlugin();

        plugin.NoteOn(60, 100);

        plugin.ActiveNotes.Should().Contain(n => n.Note == 60 && n.Velocity == 100);
        plugin.NoteOnCount.Should().Be(1);
    }

    [Fact]
    public void NoteOff_RemovesActiveNote()
    {
        var plugin = new MockVstPlugin();
        plugin.NoteOn(60, 100);

        plugin.NoteOff(60);

        plugin.ActiveNotes.Should().NotContain(n => n.Note == 60);
        plugin.NoteOffCount.Should().Be(1);
    }

    [Fact]
    public void AllNotesOff_ClearsAllNotes()
    {
        var plugin = new MockVstPlugin();
        plugin.NoteOn(60, 100);
        plugin.NoteOn(64, 100);
        plugin.NoteOn(67, 100);

        plugin.AllNotesOff();

        plugin.ActiveNotes.Should().BeEmpty();
        plugin.AllNotesOffCount.Should().Be(1);
    }

    [Fact]
    public void SendControlChange_RecordsMessage()
    {
        var plugin = new MockVstPlugin();

        plugin.SendControlChange(0, 1, 64);

        plugin.RecordedControlChanges.Should().Contain(cc =>
            cc.Channel == 0 && cc.Controller == 1 && cc.Value == 64);
    }

    [Fact]
    public void SendPitchBend_RecordsMessage()
    {
        var plugin = new MockVstPlugin();

        plugin.SendPitchBend(0, 8192);

        plugin.RecordedPitchBends.Should().Contain(pb =>
            pb.Channel == 0 && pb.Value == 8192);
    }

    [Fact]
    public void SendProgramChange_RecordsMessage()
    {
        var plugin = new MockVstPlugin();

        plugin.SendProgramChange(0, 5);

        plugin.RecordedProgramChanges.Should().Contain(pc =>
            pc.Channel == 0 && pc.Program == 5);
    }

    #endregion

    #region Activation Tests

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var plugin = new MockVstPlugin();

        plugin.Activate();

        plugin.IsActive.Should().BeTrue();
        plugin.ActivateCount.Should().Be(1);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var plugin = new MockVstPlugin();
        plugin.Activate();

        plugin.Deactivate();

        plugin.IsActive.Should().BeFalse();
        plugin.DeactivateCount.Should().Be(1);
    }

    [Fact]
    public void SetSampleRate_UpdatesSampleRate()
    {
        var plugin = new MockVstPlugin();

        plugin.SetSampleRate(48000);

        plugin.SampleRate.Should().Be(48000);
    }

    [Fact]
    public void SetBlockSize_UpdatesBlockSize()
    {
        var plugin = new MockVstPlugin();

        plugin.SetBlockSize(1024);

        plugin.BlockSize.Should().Be(1024);
    }

    #endregion

    #region Editor Tests

    [Fact]
    public void OpenEditor_ReturnsHandle()
    {
        var plugin = new MockVstPlugin { HasEditor = true };

        var handle = plugin.OpenEditor(new IntPtr(100));

        handle.Should().NotBe(IntPtr.Zero);
        plugin.EditorIsOpen.Should().BeTrue();
    }

    [Fact]
    public void CloseEditor_ClosesEditor()
    {
        var plugin = new MockVstPlugin { HasEditor = true };
        plugin.OpenEditor(new IntPtr(100));

        plugin.CloseEditor();

        plugin.EditorIsOpen.Should().BeFalse();
    }

    [Fact]
    public void GetEditorSize_ReturnsSize()
    {
        var plugin = new MockVstPlugin { HasEditor = true };

        var result = plugin.GetEditorSize(out int width, out int height);

        result.Should().BeTrue();
        width.Should().BeGreaterThan(0);
        height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetEditorSize_NoEditor_ReturnsFalse()
    {
        var plugin = new MockVstPlugin { HasEditor = false };

        var result = plugin.GetEditorSize(out int width, out int height);

        result.Should().BeFalse();
    }

    #endregion

    #region Bypass Tests

    [Fact]
    public void IsBypassed_DefaultFalse()
    {
        var plugin = new MockVstPlugin();

        plugin.IsBypassed.Should().BeFalse();
    }

    [Fact]
    public void IsBypassed_WhenChanged_RaisesEvent()
    {
        var plugin = new MockVstPlugin();
        bool eventRaised = false;
        bool newValue = false;
        plugin.BypassChanged += (s, e) => { eventRaised = true; newValue = e; };

        plugin.IsBypassed = true;

        eventRaised.Should().BeTrue();
        newValue.Should().BeTrue();
    }

    [Fact]
    public void IsBypassed_SameValue_DoesNotRaiseEvent()
    {
        var plugin = new MockVstPlugin { IsBypassed = false };
        int eventCount = 0;
        plugin.BypassChanged += (s, e) => eventCount++;

        plugin.IsBypassed = false;

        eventCount.Should().Be(0);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var plugin = new MockVstPlugin();

        var action = () =>
        {
            plugin.Dispose();
            plugin.Dispose();
        };

        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_ClearsActiveNotes()
    {
        var plugin = new MockVstPlugin();
        plugin.NoteOn(60, 100);

        plugin.Dispose();

        plugin.ActiveNotes.Should().BeEmpty();
    }

    #endregion

    #region MasterVolume Tests

    [Fact]
    public void MasterVolume_DefaultIsOne()
    {
        var plugin = new MockVstPlugin();

        plugin.MasterVolume.Should().Be(1.0f);
    }

    [Fact]
    public void MasterVolume_ClampsToRange()
    {
        var plugin = new MockVstPlugin();

        plugin.MasterVolume = 3.0f;
        plugin.MasterVolume.Should().Be(2.0f);

        plugin.MasterVolume = -1.0f;
        plugin.MasterVolume.Should().Be(0.0f);
    }

    #endregion
}
