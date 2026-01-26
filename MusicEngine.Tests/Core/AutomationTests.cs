using FluentAssertions;
using MusicEngine.Core;
using MusicEngine.Core.Automation;
using Xunit;
using AutomationPoint = MusicEngine.Core.Automation.AutomationPoint;
using AutomationCurve = MusicEngine.Core.Automation.AutomationCurve;

namespace MusicEngine.Tests.Core;

public class AutomationTests
{
    #region AutomationPoint Tests

    [Fact]
    public void AutomationPoint_Constructor_Default_SetsDefaults()
    {
        var point = new AutomationPoint();

        point.Time.Should().Be(0);
        point.Value.Should().Be(0);
        point.CurveType.Should().Be(AutomationCurveType.Linear);
    }

    [Fact]
    public void AutomationPoint_Constructor_WithParameters_SetsValues()
    {
        var point = new AutomationPoint(2.5, 0.75f, AutomationCurveType.Bezier);

        point.Time.Should().Be(2.5);
        point.Value.Should().Be(0.75f);
        point.CurveType.Should().Be(AutomationCurveType.Bezier);
    }

    [Fact]
    public void AutomationPoint_Id_IsUniqueForEachInstance()
    {
        var point1 = new AutomationPoint();
        var point2 = new AutomationPoint();

        point1.Id.Should().NotBe(point2.Id);
    }

    [Fact]
    public void AutomationPoint_Clone_CreatesNewInstanceWithSameValues()
    {
        var original = new AutomationPoint(1.0, 0.5f, AutomationCurveType.SCurve)
        {
            Tension = 0.3f,
            Label = "Test Point"
        };

        var clone = original.Clone();

        clone.Time.Should().Be(original.Time);
        clone.Value.Should().Be(original.Value);
        clone.CurveType.Should().Be(original.CurveType);
        clone.Tension.Should().Be(original.Tension);
        clone.Label.Should().Be(original.Label);
        clone.Id.Should().NotBe(original.Id);
    }

    [Fact]
    public void AutomationPoint_CloneWithTimeOffset_AppliesOffset()
    {
        var original = new AutomationPoint(1.0, 0.5f);

        var clone = original.CloneWithTimeOffset(2.0);

        clone.Time.Should().Be(3.0);
    }

    [Fact]
    public void AutomationPoint_CompareTo_SortsByTime()
    {
        var point1 = new AutomationPoint(1.0, 0.5f);
        var point2 = new AutomationPoint(2.0, 0.5f);
        var point3 = new AutomationPoint(1.0, 0.5f);

        point1.CompareTo(point2).Should().BeLessThan(0);
        point2.CompareTo(point1).Should().BeGreaterThan(0);
        point1.CompareTo(point3).Should().Be(0);
    }

    [Fact]
    public void AutomationPoint_Equals_ComparesById()
    {
        var point1 = new AutomationPoint(1.0, 0.5f);
        var point2 = new AutomationPoint(1.0, 0.5f);
        var point3 = point1;

        point1.Equals(point2).Should().BeFalse();
        point1.Equals(point3).Should().BeTrue();
    }

    [Fact]
    public void AutomationPoint_Operators_WorkCorrectly()
    {
        var point1 = new AutomationPoint(1.0, 0.5f);
        var point2 = new AutomationPoint(2.0, 0.5f);

        (point1 < point2).Should().BeTrue();
        (point1 > point2).Should().BeFalse();
        (point1 <= point2).Should().BeTrue();
        (point2 >= point1).Should().BeTrue();
    }

    #endregion

    #region AutomationCurve Tests

    [Fact]
    public void AutomationCurve_Constructor_Default_CreatesEmptyCurve()
    {
        var curve = new AutomationCurve();

        curve.Count.Should().Be(0);
        curve.Duration.Should().Be(0);
    }

    [Fact]
    public void AutomationCurve_Constructor_WithPoints_AddsPoints()
    {
        var points = new[]
        {
            new AutomationPoint(0, 0),
            new AutomationPoint(1, 1)
        };

        var curve = new AutomationCurve(points);

        curve.Count.Should().Be(2);
    }

    [Fact]
    public void AutomationCurve_AddPoint_IncreasesCount()
    {
        var curve = new AutomationCurve();

        curve.AddPoint(1.0, 0.5f);

        curve.Count.Should().Be(1);
    }

    [Fact]
    public void AutomationCurve_AddPoint_ReturnsCreatedPoint()
    {
        var curve = new AutomationCurve();

        var point = curve.AddPoint(1.0, 0.5f, AutomationCurveType.Bezier);

        point.Time.Should().Be(1.0);
        point.Value.Should().Be(0.5f);
        point.CurveType.Should().Be(AutomationCurveType.Bezier);
    }

    [Fact]
    public void AutomationCurve_AddPoint_FiresCurveChangedEvent()
    {
        var curve = new AutomationCurve();
        bool eventFired = false;
        curve.CurveChanged += (s, e) => eventFired = true;

        curve.AddPoint(1.0, 0.5f);

        eventFired.Should().BeTrue();
    }

    [Fact]
    public void AutomationCurve_RemovePoint_DecreasesCount()
    {
        var curve = new AutomationCurve();
        var point = curve.AddPoint(1.0, 0.5f);

        var result = curve.RemovePoint(point);

        result.Should().BeTrue();
        curve.Count.Should().Be(0);
    }

    [Fact]
    public void AutomationCurve_RemovePointAtTime_RemovesCorrectPoint()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(1.0, 0.5f);
        curve.AddPoint(2.0, 0.8f);

        var result = curve.RemovePointAtTime(1.0);

        result.Should().BeTrue();
        curve.Count.Should().Be(1);
    }

    [Fact]
    public void AutomationCurve_RemovePointsInRange_RemovesMultiplePoints()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(0.5, 0.5f);
        curve.AddPoint(1.0, 0.5f);
        curve.AddPoint(1.5, 0.5f);
        curve.AddPoint(2.0, 0.5f);

        var removed = curve.RemovePointsInRange(0.8, 1.8);

        removed.Should().Be(2);
        curve.Count.Should().Be(2);
    }

    [Fact]
    public void AutomationCurve_Clear_RemovesAllPoints()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(1.0, 0.5f);
        curve.AddPoint(2.0, 0.8f);

        curve.Clear();

        curve.Count.Should().Be(0);
    }

    [Fact]
    public void AutomationCurve_MinTime_ReturnsSmallestTime()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(3.0, 0.5f);
        curve.AddPoint(1.0, 0.5f);
        curve.AddPoint(2.0, 0.5f);

        curve.MinTime.Should().Be(1.0);
    }

    [Fact]
    public void AutomationCurve_MaxTime_ReturnsLargestTime()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(1.0, 0.5f);
        curve.AddPoint(3.0, 0.5f);
        curve.AddPoint(2.0, 0.5f);

        curve.MaxTime.Should().Be(3.0);
    }

    [Fact]
    public void AutomationCurve_Duration_ReturnsCorrectDuration()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(1.0, 0.5f);
        curve.AddPoint(4.0, 0.5f);

        curve.Duration.Should().Be(3.0);
    }

    [Fact]
    public void AutomationCurve_Points_ReturnsSortedList()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(3.0, 0.5f);
        curve.AddPoint(1.0, 0.5f);
        curve.AddPoint(2.0, 0.5f);

        var points = curve.Points;

        points[0].Time.Should().Be(1.0);
        points[1].Time.Should().Be(2.0);
        points[2].Time.Should().Be(3.0);
    }

    #endregion

    #region AutomationCurve Interpolation Tests

    [Fact]
    public void AutomationCurve_GetValueAtTime_EmptyCurve_ReturnsZero()
    {
        var curve = new AutomationCurve();

        var value = curve.GetValueAtTime(1.0);

        value.Should().Be(0f);
    }

    [Fact]
    public void AutomationCurve_GetValueAtTime_BeforeFirstPoint_ReturnsFirstValue()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(1.0, 0.5f);
        curve.AddPoint(2.0, 1.0f);

        var value = curve.GetValueAtTime(0.5);

        value.Should().Be(0.5f);
    }

    [Fact]
    public void AutomationCurve_GetValueAtTime_AfterLastPoint_ReturnsLastValue()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(1.0, 0.5f);
        curve.AddPoint(2.0, 1.0f);

        var value = curve.GetValueAtTime(3.0);

        value.Should().Be(1.0f);
    }

    [Fact]
    public void AutomationCurve_GetValueAtTime_Linear_InterpolatesCorrectly()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(0.0, 0.0f, AutomationCurveType.Linear);
        curve.AddPoint(2.0, 1.0f, AutomationCurveType.Linear);

        var value = curve.GetValueAtTime(1.0);

        value.Should().BeApproximately(0.5f, 0.001f);
    }

    [Fact]
    public void AutomationCurve_GetValueAtTime_Step_ReturnsStartValue()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(0.0, 0.0f, AutomationCurveType.Step);
        curve.AddPoint(2.0, 1.0f, AutomationCurveType.Step);

        var value = curve.GetValueAtTime(1.0);

        value.Should().Be(0.0f);
    }

    [Fact]
    public void AutomationCurve_GetValueAtTime_SCurve_InterpolatesSmooth()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(0.0, 0.0f, AutomationCurveType.SCurve);
        curve.AddPoint(1.0, 1.0f, AutomationCurveType.SCurve);

        var valueAtMidpoint = curve.GetValueAtTime(0.5);

        valueAtMidpoint.Should().BeApproximately(0.5f, 0.001f);
    }

    [Fact]
    public void AutomationCurve_GetValueAtTime_Exponential_InterpolatesCorrectly()
    {
        var curve = new AutomationCurve();
        var point = curve.AddPoint(0.0, 0.0f, AutomationCurveType.Exponential);
        point.Tension = 0.5f;
        curve.AddPoint(1.0, 1.0f, AutomationCurveType.Exponential);

        var value = curve.GetValueAtTime(0.5);

        value.Should().BeGreaterThan(0f);
        value.Should().BeLessThan(0.5f);
    }

    [Fact]
    public void AutomationCurve_GetPointAtOrBefore_ReturnsCorrectPoint()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(0.0, 0.0f);
        curve.AddPoint(1.0, 0.5f);
        curve.AddPoint(2.0, 1.0f);

        var point = curve.GetPointAtOrBefore(1.5);

        point.Should().NotBeNull();
        point!.Time.Should().Be(1.0);
    }

    [Fact]
    public void AutomationCurve_GetClosestPoint_ReturnsNearestPoint()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(0.0, 0.0f);
        curve.AddPoint(1.0, 0.5f);
        curve.AddPoint(2.0, 1.0f);

        var point = curve.GetClosestPoint(0.8);

        point.Should().NotBeNull();
        point!.Time.Should().Be(1.0);
    }

    [Fact]
    public void AutomationCurve_GetPointsInRange_ReturnsCorrectPoints()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(0.0, 0.0f);
        curve.AddPoint(1.0, 0.5f);
        curve.AddPoint(2.0, 0.8f);
        curve.AddPoint(3.0, 1.0f);

        var points = curve.GetPointsInRange(0.5, 2.5);

        points.Should().HaveCount(2);
        points[0].Time.Should().Be(1.0);
        points[1].Time.Should().Be(2.0);
    }

    #endregion

    #region AutomationCurve Transformation Tests

    [Fact]
    public void AutomationCurve_ShiftTime_ShiftsAllPoints()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(1.0, 0.5f);
        curve.AddPoint(2.0, 0.8f);

        curve.ShiftTime(1.0);

        curve.Points[0].Time.Should().Be(2.0);
        curve.Points[1].Time.Should().Be(3.0);
    }

    [Fact]
    public void AutomationCurve_ScaleTime_ScalesAllTimes()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(1.0, 0.5f);
        curve.AddPoint(2.0, 0.8f);

        curve.ScaleTime(2.0);

        curve.Points[0].Time.Should().Be(2.0);
        curve.Points[1].Time.Should().Be(4.0);
    }

    [Fact]
    public void AutomationCurve_ScaleValues_ScalesAllValues()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(1.0, 0.5f);
        curve.AddPoint(2.0, 0.8f);

        curve.ScaleValues(2.0f);

        curve.Points[0].Value.Should().Be(1.0f);
        curve.Points[1].Value.Should().Be(1.6f);
    }

    [Fact]
    public void AutomationCurve_ClampValues_ClampsAllValues()
    {
        var curve = new AutomationCurve();
        curve.AddPoint(1.0, 0.2f);
        curve.AddPoint(2.0, 0.8f);

        curve.ClampValues(0.3f, 0.7f);

        curve.Points[0].Value.Should().Be(0.3f);
        curve.Points[1].Value.Should().Be(0.7f);
    }

    [Fact]
    public void AutomationCurve_Clone_CreatesIndependentCopy()
    {
        var original = new AutomationCurve();
        original.AddPoint(1.0, 0.5f);
        original.AddPoint(2.0, 0.8f);

        var clone = original.Clone();
        clone.AddPoint(3.0, 1.0f);

        original.Count.Should().Be(2);
        clone.Count.Should().Be(3);
    }

    #endregion

    #region TempoMap Tests

    [Fact]
    public void TempoMap_Constructor_Default_SetsDefaultBpm()
    {
        var map = new TempoMap();

        map.DefaultBpm.Should().Be(120.0);
        map.Count.Should().Be(0);
    }

    [Fact]
    public void TempoMap_Constructor_WithBpm_SetsCustomBpm()
    {
        var map = new TempoMap(140.0);

        map.DefaultBpm.Should().Be(140.0);
    }

    [Fact]
    public void TempoMap_DefaultBpm_ClampsToBounds()
    {
        var map = new TempoMap();

        map.DefaultBpm = 0.5;
        map.DefaultBpm.Should().Be(1.0);

        map.DefaultBpm = 1000;
        map.DefaultBpm.Should().Be(999.0);
    }

    [Fact]
    public void TempoMap_AddTempoChange_AddsToMap()
    {
        var map = new TempoMap();

        var change = map.AddTempoChange(4.0, 140.0);

        change.PositionBeats.Should().Be(4.0);
        change.Bpm.Should().Be(140.0);
        map.Count.Should().Be(1);
    }

    [Fact]
    public void TempoMap_AddTempoChange_ReplacesExistingAtSamePosition()
    {
        var map = new TempoMap();

        map.AddTempoChange(4.0, 120.0);
        map.AddTempoChange(4.0, 140.0);

        map.Count.Should().Be(1);
        map.TempoChanges[0].Bpm.Should().Be(140.0);
    }

    [Fact]
    public void TempoMap_AddTempoRamp_CreatesTwoChanges()
    {
        var map = new TempoMap();

        map.AddTempoRamp(0.0, 4.0, 120.0, 140.0);

        map.Count.Should().Be(2);
        map.TempoChanges[0].IsRamp.Should().BeTrue();
        map.TempoChanges[1].IsRamp.Should().BeFalse();
    }

    [Fact]
    public void TempoMap_RemoveTempoChange_RemovesFromMap()
    {
        var map = new TempoMap();
        map.AddTempoChange(4.0, 140.0);

        var result = map.RemoveTempoChange(4.0);

        result.Should().BeTrue();
        map.Count.Should().Be(0);
    }

    [Fact]
    public void TempoMap_RemoveTempoChangesInRange_RemovesMultiple()
    {
        var map = new TempoMap();
        map.AddTempoChange(1.0, 110.0);
        map.AddTempoChange(2.0, 120.0);
        map.AddTempoChange(3.0, 130.0);
        map.AddTempoChange(4.0, 140.0);

        var removed = map.RemoveTempoChangesInRange(1.5, 3.5);

        removed.Should().Be(2);
        map.Count.Should().Be(2);
    }

    [Fact]
    public void TempoMap_Clear_RemovesAllChanges()
    {
        var map = new TempoMap();
        map.AddTempoChange(4.0, 140.0);
        map.AddTempoChange(8.0, 160.0);

        map.Clear();

        map.Count.Should().Be(0);
    }

    [Fact]
    public void TempoMap_GetTempoAt_NoChanges_ReturnsDefault()
    {
        var map = new TempoMap(120.0);

        var tempo = map.GetTempoAt(4.0);

        tempo.Should().Be(120.0);
    }

    [Fact]
    public void TempoMap_GetTempoAt_ReturnsCorrectTempo()
    {
        var map = new TempoMap(120.0);
        map.AddTempoChange(4.0, 140.0);

        map.GetTempoAt(2.0).Should().Be(120.0);
        map.GetTempoAt(4.0).Should().Be(140.0);
        map.GetTempoAt(6.0).Should().Be(140.0);
    }

    [Fact]
    public void TempoMap_GetTempoAt_WithRamp_InterpolatesTempo()
    {
        var map = new TempoMap(120.0);
        map.AddTempoRamp(0.0, 4.0, 120.0, 140.0);

        var tempoAtMidpoint = map.GetTempoAt(2.0);

        tempoAtMidpoint.Should().BeApproximately(130.0, 1.0);
    }

    [Fact]
    public void TempoMap_GetTempoChangeAt_ReturnsNullBeforeFirstChange()
    {
        var map = new TempoMap();
        map.AddTempoChange(4.0, 140.0);

        var change = map.GetTempoChangeAt(2.0);

        change.Should().BeNull();
    }

    [Fact]
    public void TempoMap_GetNextTempoChange_ReturnsNextChange()
    {
        var map = new TempoMap();
        map.AddTempoChange(4.0, 140.0);
        map.AddTempoChange(8.0, 160.0);

        var next = map.GetNextTempoChange(5.0);

        next.Should().NotBeNull();
        next!.PositionBeats.Should().Be(8.0);
    }

    [Fact]
    public void TempoMap_BeatsToSeconds_NoChanges_ConvertsCorrectly()
    {
        var map = new TempoMap(120.0);

        var seconds = map.BeatsToSeconds(4.0);

        seconds.Should().BeApproximately(2.0, 0.001);
    }

    [Fact]
    public void TempoMap_SecondsToBeats_NoChanges_ConvertsCorrectly()
    {
        var map = new TempoMap(120.0);

        var beats = map.SecondsToBeats(2.0);

        beats.Should().BeApproximately(4.0, 0.001);
    }

    [Fact]
    public void TempoMap_BeatsToSeconds_WithTempoChange_ConvertsCorrectly()
    {
        var map = new TempoMap(120.0);
        map.AddTempoChange(4.0, 240.0);

        var secondsAt4Beats = map.BeatsToSeconds(4.0);
        secondsAt4Beats.Should().BeApproximately(2.0, 0.001);

        var secondsAt8Beats = map.BeatsToSeconds(8.0);
        secondsAt8Beats.Should().BeApproximately(3.0, 0.001);
    }

    [Fact]
    public void TempoMap_FromArray_CreatesCorrectMap()
    {
        var bpmValues = new double[] { 120.0, 130.0, 140.0 };

        var map = TempoMap.FromArray(bpmValues, 4.0);

        map.Count.Should().Be(3);
        map.GetTempoAt(0.0).Should().Be(120.0);
        map.GetTempoAt(4.0).Should().Be(130.0);
        map.GetTempoAt(8.0).Should().Be(140.0);
    }

    [Fact]
    public void TempoMap_FromArray_WithRamps_CreatesRampingMap()
    {
        var bpmValues = new double[] { 120.0, 140.0 };

        var map = TempoMap.FromArray(bpmValues, 4.0, true);

        map.TempoChanges[0].IsRamp.Should().BeTrue();
    }

    #endregion

    #region TimeSignature Tests

    [Fact]
    public void TimeSignature_Constructor_SetsValues()
    {
        var ts = new TimeSignature(4, 4);

        ts.Numerator.Should().Be(4);
        ts.Denominator.Should().Be(4);
    }

    [Fact]
    public void TimeSignature_Constructor_ThrowsOnInvalidNumerator()
    {
        var action1 = () => new TimeSignature(0, 4);
        var action2 = () => new TimeSignature(33, 4);

        action1.Should().Throw<ArgumentOutOfRangeException>();
        action2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TimeSignature_Constructor_ThrowsOnInvalidDenominator()
    {
        var action1 = () => new TimeSignature(4, 3);
        var action2 = () => new TimeSignature(4, 128);

        action1.Should().Throw<ArgumentOutOfRangeException>();
        action2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TimeSignature_Predefined_Common_IsCorrect()
    {
        var ts = TimeSignature.Common;

        ts.Numerator.Should().Be(4);
        ts.Denominator.Should().Be(4);
    }

    [Fact]
    public void TimeSignature_Predefined_Waltz_IsCorrect()
    {
        var ts = TimeSignature.Waltz;

        ts.Numerator.Should().Be(3);
        ts.Denominator.Should().Be(4);
    }

    [Fact]
    public void TimeSignature_BeatsPerBar_ReturnsNumerator()
    {
        var ts = new TimeSignature(6, 8);

        ts.BeatsPerBar.Should().Be(6);
    }

    [Fact]
    public void TimeSignature_BeatValue_ReturnsCorrectFraction()
    {
        var ts44 = new TimeSignature(4, 4);
        var ts68 = new TimeSignature(6, 8);

        ts44.BeatValue.Should().Be(0.25);
        ts68.BeatValue.Should().Be(0.125);
    }

    [Fact]
    public void TimeSignature_BarLengthInQuarterNotes_CalculatesCorrectly()
    {
        var ts44 = new TimeSignature(4, 4);
        var ts34 = new TimeSignature(3, 4);
        var ts68 = new TimeSignature(6, 8);

        ts44.BarLengthInQuarterNotes.Should().Be(4.0);
        ts34.BarLengthInQuarterNotes.Should().Be(3.0);
        ts68.BarLengthInQuarterNotes.Should().Be(3.0);
    }

    [Fact]
    public void TimeSignature_IsCompound_IdentifiesCompoundTime()
    {
        var ts44 = new TimeSignature(4, 4);
        var ts68 = new TimeSignature(6, 8);
        var ts98 = new TimeSignature(9, 8);
        var ts128 = new TimeSignature(12, 8);

        ts44.IsCompound.Should().BeFalse();
        ts68.IsCompound.Should().BeTrue();
        ts98.IsCompound.Should().BeTrue();
        ts128.IsCompound.Should().BeTrue();
    }

    [Fact]
    public void TimeSignature_IsIrregular_IdentifiesIrregularTime()
    {
        var ts44 = new TimeSignature(4, 4);
        var ts54 = new TimeSignature(5, 4);
        var ts78 = new TimeSignature(7, 8);

        ts44.IsIrregular.Should().BeFalse();
        ts54.IsIrregular.Should().BeTrue();
        ts78.IsIrregular.Should().BeTrue();
    }

    [Fact]
    public void TimeSignature_StrongBeatsPerBar_ReturnsCorrectCount()
    {
        var ts44 = new TimeSignature(4, 4);
        var ts68 = new TimeSignature(6, 8);
        var ts128 = new TimeSignature(12, 8);

        ts44.StrongBeatsPerBar.Should().Be(4);
        ts68.StrongBeatsPerBar.Should().Be(2);
        ts128.StrongBeatsPerBar.Should().Be(4);
    }

    [Fact]
    public void TimeSignature_QuarterNotesToBarBeat_ConvertsCorrectly()
    {
        var ts = new TimeSignature(4, 4);

        var (bar, beat) = ts.QuarterNotesToBarBeat(10.5);

        bar.Should().Be(2);
        beat.Should().BeApproximately(2.5, 0.001);
    }

    [Fact]
    public void TimeSignature_BarBeatToQuarterNotes_ConvertsCorrectly()
    {
        var ts = new TimeSignature(4, 4);

        var quarterNotes = ts.BarBeatToQuarterNotes(2, 2.5);

        quarterNotes.Should().BeApproximately(10.5, 0.001);
    }

    [Fact]
    public void TimeSignature_GetAccentPattern_Returns44Pattern()
    {
        var ts = new TimeSignature(4, 4);

        var accents = ts.GetAccentPattern();

        accents.Should().HaveCount(4);
        accents[0].Should().Be(1.0);
        accents[1].Should().Be(0.25);
        accents[2].Should().Be(0.5);
        accents[3].Should().Be(0.25);
    }

    [Fact]
    public void TimeSignature_GetGroupingPattern_ReturnsCorrectGroups()
    {
        var ts54 = new TimeSignature(5, 4);
        var ts78 = new TimeSignature(7, 8);
        var ts68 = new TimeSignature(6, 8);

        ts54.GetGroupingPattern().Should().ContainInOrder(3, 2);
        ts78.GetGroupingPattern().Should().ContainInOrder(2, 2, 3);
        ts68.GetGroupingPattern().Should().ContainInOrder(3, 3);
    }

    [Fact]
    public void TimeSignature_Parse_ParsesValidString()
    {
        var ts = TimeSignature.Parse("6/8");

        ts.Numerator.Should().Be(6);
        ts.Denominator.Should().Be(8);
    }

    [Fact]
    public void TimeSignature_Parse_ThrowsOnInvalidFormat()
    {
        var action = () => TimeSignature.Parse("invalid");

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void TimeSignature_TryParse_ReturnsTrueOnSuccess()
    {
        var result = TimeSignature.TryParse("4/4", out var ts);

        result.Should().BeTrue();
        ts.Numerator.Should().Be(4);
        ts.Denominator.Should().Be(4);
    }

    [Fact]
    public void TimeSignature_TryParse_ReturnsFalseOnFailure()
    {
        var result = TimeSignature.TryParse("invalid", out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TimeSignature_Equals_ComparesCorrectly()
    {
        var ts1 = new TimeSignature(4, 4);
        var ts2 = new TimeSignature(4, 4);
        var ts3 = new TimeSignature(3, 4);

        ts1.Equals(ts2).Should().BeTrue();
        ts1.Equals(ts3).Should().BeFalse();
        (ts1 == ts2).Should().BeTrue();
        (ts1 != ts3).Should().BeTrue();
    }

    [Fact]
    public void TimeSignature_ToString_ReturnsFormattedString()
    {
        var ts = new TimeSignature(6, 8);

        ts.ToString().Should().Be("6/8");
    }

    [Fact]
    public void TimeSignature_Deconstruct_ReturnsComponents()
    {
        var ts = new TimeSignature(4, 4);

        var (numerator, denominator) = ts;

        numerator.Should().Be(4);
        denominator.Should().Be(4);
    }

    #endregion

    #region TempoChange Tests

    [Fact]
    public void TempoChange_Constructor_SetsValues()
    {
        var change = new TempoChange(4.0, 140.0);

        change.PositionBeats.Should().Be(4.0);
        change.Bpm.Should().Be(140.0);
        change.IsRamp.Should().BeFalse();
        change.RampCurve.Should().Be(0.0);
    }

    [Fact]
    public void TempoChange_Constructor_WithRamp_SetsRampValues()
    {
        var change = new TempoChange(4.0, 140.0, true, 0.5);

        change.IsRamp.Should().BeTrue();
        change.RampCurve.Should().Be(0.5);
    }

    [Fact]
    public void TempoChange_Constructor_ClampsBpm()
    {
        var changeLow = new TempoChange(0.0, 0.5);
        var changeHigh = new TempoChange(0.0, 1000.0);

        changeLow.Bpm.Should().Be(1.0);
        changeHigh.Bpm.Should().Be(999.0);
    }

    [Fact]
    public void TempoChange_Constructor_ClampsRampCurve()
    {
        var changeLow = new TempoChange(0.0, 120.0, true, -2.0);
        var changeHigh = new TempoChange(0.0, 120.0, true, 2.0);

        changeLow.RampCurve.Should().Be(-1.0);
        changeHigh.RampCurve.Should().Be(1.0);
    }

    [Fact]
    public void TempoChange_Constructor_ThrowsOnNegativePosition()
    {
        var action = () => new TempoChange(-1.0, 120.0);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TempoChange_Constructor_ThrowsOnZeroBpm()
    {
        var action = () => new TempoChange(0.0, 0.0);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TempoChange_ToString_ReturnsDescriptiveString()
    {
        var change = new TempoChange(4.0, 140.0);

        var str = change.ToString();

        str.Should().Contain("140");
        str.Should().Contain("4");
    }

    #endregion
}
