// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio analysis component.

namespace MusicEngine.Core.Analysis;

/// <summary>
/// Represents a single alignment point mapping source time to target time.
/// </summary>
public class AlignmentPoint
{
    /// <summary>
    /// Gets or sets the time position in the source/reference audio in seconds.
    /// </summary>
    public double SourceTime { get; set; }

    /// <summary>
    /// Gets or sets the time position in the target audio in seconds.
    /// </summary>
    public double TargetTime { get; set; }

    /// <summary>
    /// Gets or sets the confidence of this alignment point (0.0 to 1.0).
    /// Higher values indicate more reliable alignment.
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Gets or sets the time stretch ratio at this point.
    /// Values greater than 1 indicate the target needs to be sped up,
    /// values less than 1 indicate it needs to be slowed down.
    /// </summary>
    public float LocalStretchRatio { get; set; } = 1.0f;

    /// <summary>
    /// Creates a new alignment point.
    /// </summary>
    public AlignmentPoint()
    {
    }

    /// <summary>
    /// Creates a new alignment point with specified values.
    /// </summary>
    /// <param name="sourceTime">Source/reference time in seconds</param>
    /// <param name="targetTime">Target time in seconds</param>
    /// <param name="confidence">Alignment confidence (0-1)</param>
    public AlignmentPoint(double sourceTime, double targetTime, float confidence = 1.0f)
    {
        SourceTime = sourceTime;
        TargetTime = targetTime;
        Confidence = confidence;
    }

    /// <summary>
    /// Gets the time offset (target - source) in seconds.
    /// Positive values indicate target is ahead, negative means behind.
    /// </summary>
    public double TimeOffset => TargetTime - SourceTime;

    /// <summary>
    /// Creates a copy of this alignment point.
    /// </summary>
    public AlignmentPoint Clone()
    {
        return new AlignmentPoint
        {
            SourceTime = SourceTime,
            TargetTime = TargetTime,
            Confidence = Confidence,
            LocalStretchRatio = LocalStretchRatio
        };
    }

    public override string ToString()
    {
        return $"AlignmentPoint: Source={SourceTime:F3}s -> Target={TargetTime:F3}s (Confidence={Confidence:P0})";
    }
}

/// <summary>
/// Contains the complete result of an audio alignment operation.
/// </summary>
public class AlignmentResult
{
    /// <summary>
    /// Gets the list of alignment points mapping source to target times.
    /// </summary>
    public List<AlignmentPoint> Points { get; } = new();

    /// <summary>
    /// Gets or sets the overall confidence of the alignment (0.0 to 1.0).
    /// </summary>
    public float OverallConfidence { get; set; }

    /// <summary>
    /// Gets or sets the warp path as an array of time mappings.
    /// Index represents the source sample, value represents target sample.
    /// </summary>
    public double[] WarpPath { get; set; } = Array.Empty<double>();

    /// <summary>
    /// Gets or sets the sample rate used for the alignment.
    /// </summary>
    public int SampleRate { get; set; }

    /// <summary>
    /// Gets or sets the duration of the reference audio in seconds.
    /// </summary>
    public double ReferenceDuration { get; set; }

    /// <summary>
    /// Gets or sets the duration of the target audio in seconds.
    /// </summary>
    public double TargetDuration { get; set; }

    /// <summary>
    /// Gets or sets the average time offset between reference and target.
    /// </summary>
    public double AverageOffset { get; set; }

    /// <summary>
    /// Gets or sets the maximum time deviation from the average.
    /// </summary>
    public double MaxDeviation { get; set; }

    /// <summary>
    /// Gets or sets the DTW cost (lower is better alignment).
    /// </summary>
    public float DTWCost { get; set; }

    /// <summary>
    /// Gets the number of alignment points.
    /// </summary>
    public int PointCount => Points.Count;

    /// <summary>
    /// Gets whether the alignment is considered successful.
    /// </summary>
    public bool IsSuccessful => OverallConfidence > 0.5f && Points.Count > 0;

    /// <summary>
    /// Gets the time mapping for a given source time using linear interpolation.
    /// </summary>
    /// <param name="sourceTime">Source time in seconds</param>
    /// <returns>Corresponding target time in seconds</returns>
    public double GetTargetTimeForSource(double sourceTime)
    {
        if (Points.Count == 0)
            return sourceTime;

        // Find surrounding alignment points
        int lowerIndex = -1;
        int upperIndex = -1;

        for (int i = 0; i < Points.Count; i++)
        {
            if (Points[i].SourceTime <= sourceTime)
                lowerIndex = i;
            if (Points[i].SourceTime >= sourceTime && upperIndex < 0)
                upperIndex = i;
        }

        // Handle edge cases
        if (lowerIndex < 0)
            return Points[0].TargetTime + (sourceTime - Points[0].SourceTime);
        if (upperIndex < 0 || upperIndex == lowerIndex)
            return Points[lowerIndex].TargetTime + (sourceTime - Points[lowerIndex].SourceTime);

        // Linear interpolation
        var lower = Points[lowerIndex];
        var upper = Points[upperIndex];

        double sourceDelta = upper.SourceTime - lower.SourceTime;
        if (sourceDelta < 1e-10)
            return lower.TargetTime;

        double t = (sourceTime - lower.SourceTime) / sourceDelta;
        return lower.TargetTime + t * (upper.TargetTime - lower.TargetTime);
    }

    /// <summary>
    /// Gets the source time for a given target time using linear interpolation.
    /// </summary>
    /// <param name="targetTime">Target time in seconds</param>
    /// <returns>Corresponding source time in seconds</returns>
    public double GetSourceTimeForTarget(double targetTime)
    {
        if (Points.Count == 0)
            return targetTime;

        // Find surrounding alignment points
        int lowerIndex = -1;
        int upperIndex = -1;

        for (int i = 0; i < Points.Count; i++)
        {
            if (Points[i].TargetTime <= targetTime)
                lowerIndex = i;
            if (Points[i].TargetTime >= targetTime && upperIndex < 0)
                upperIndex = i;
        }

        // Handle edge cases
        if (lowerIndex < 0)
            return Points[0].SourceTime + (targetTime - Points[0].TargetTime);
        if (upperIndex < 0 || upperIndex == lowerIndex)
            return Points[lowerIndex].SourceTime + (targetTime - Points[lowerIndex].TargetTime);

        // Linear interpolation
        var lower = Points[lowerIndex];
        var upper = Points[upperIndex];

        double targetDelta = upper.TargetTime - lower.TargetTime;
        if (targetDelta < 1e-10)
            return lower.SourceTime;

        double t = (targetTime - lower.TargetTime) / targetDelta;
        return lower.SourceTime + t * (upper.SourceTime - lower.SourceTime);
    }

    /// <summary>
    /// Gets the local stretch ratio at a given source time.
    /// </summary>
    /// <param name="sourceTime">Source time in seconds</param>
    /// <returns>Local stretch ratio (1.0 = no change)</returns>
    public float GetStretchRatioAtTime(double sourceTime)
    {
        if (Points.Count < 2)
            return 1.0f;

        // Find closest point
        int closestIndex = 0;
        double minDist = double.MaxValue;

        for (int i = 0; i < Points.Count; i++)
        {
            double dist = Math.Abs(Points[i].SourceTime - sourceTime);
            if (dist < minDist)
            {
                minDist = dist;
                closestIndex = i;
            }
        }

        return Points[closestIndex].LocalStretchRatio;
    }

    /// <summary>
    /// Calculates statistics from the alignment points.
    /// </summary>
    public void CalculateStatistics()
    {
        if (Points.Count == 0)
        {
            AverageOffset = 0;
            MaxDeviation = 0;
            return;
        }

        // Calculate average offset
        double sumOffset = 0;
        foreach (var point in Points)
        {
            sumOffset += point.TimeOffset;
        }
        AverageOffset = sumOffset / Points.Count;

        // Calculate max deviation
        MaxDeviation = 0;
        foreach (var point in Points)
        {
            double deviation = Math.Abs(point.TimeOffset - AverageOffset);
            if (deviation > MaxDeviation)
                MaxDeviation = deviation;
        }

        // Calculate local stretch ratios
        for (int i = 1; i < Points.Count; i++)
        {
            double sourceDelta = Points[i].SourceTime - Points[i - 1].SourceTime;
            double targetDelta = Points[i].TargetTime - Points[i - 1].TargetTime;

            if (sourceDelta > 1e-10)
            {
                Points[i].LocalStretchRatio = (float)(targetDelta / sourceDelta);
            }
        }

        if (Points.Count > 0)
        {
            Points[0].LocalStretchRatio = Points.Count > 1 ? Points[1].LocalStretchRatio : 1.0f;
        }
    }

    /// <summary>
    /// Adds an alignment point to the result.
    /// </summary>
    public void AddPoint(double sourceTime, double targetTime, float confidence = 1.0f)
    {
        Points.Add(new AlignmentPoint(sourceTime, targetTime, confidence));
    }

    /// <summary>
    /// Simplifies the alignment by removing redundant points.
    /// </summary>
    /// <param name="tolerance">Maximum deviation allowed for point removal in seconds</param>
    public void Simplify(double tolerance = 0.001)
    {
        if (Points.Count < 3)
            return;

        var simplified = new List<AlignmentPoint> { Points[0] };

        for (int i = 1; i < Points.Count - 1; i++)
        {
            var prev = simplified[^1];
            var curr = Points[i];
            var next = Points[i + 1];

            // Check if current point can be removed (lies on line between prev and next)
            double expectedTarget = prev.TargetTime +
                (curr.SourceTime - prev.SourceTime) / (next.SourceTime - prev.SourceTime) *
                (next.TargetTime - prev.TargetTime);

            if (Math.Abs(curr.TargetTime - expectedTarget) > tolerance)
            {
                simplified.Add(curr);
            }
        }

        simplified.Add(Points[^1]);

        Points.Clear();
        Points.AddRange(simplified);
    }

    /// <summary>
    /// Creates a deep copy of this alignment result.
    /// </summary>
    public AlignmentResult Clone()
    {
        var clone = new AlignmentResult
        {
            OverallConfidence = OverallConfidence,
            SampleRate = SampleRate,
            ReferenceDuration = ReferenceDuration,
            TargetDuration = TargetDuration,
            AverageOffset = AverageOffset,
            MaxDeviation = MaxDeviation,
            DTWCost = DTWCost
        };

        foreach (var point in Points)
        {
            clone.Points.Add(point.Clone());
        }

        if (WarpPath.Length > 0)
        {
            clone.WarpPath = new double[WarpPath.Length];
            Array.Copy(WarpPath, clone.WarpPath, WarpPath.Length);
        }

        return clone;
    }

    /// <summary>
    /// Gets a summary string of the alignment result.
    /// </summary>
    public override string ToString()
    {
        return $"AlignmentResult: {Points.Count} points, Confidence={OverallConfidence:P0}, " +
               $"AvgOffset={AverageOffset * 1000:F1}ms, MaxDev={MaxDeviation * 1000:F1}ms";
    }
}
