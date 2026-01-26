// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Audio/MIDI routing component.

using NAudio.Wave;

namespace MusicEngine.Core.Routing;

/// <summary>
/// Surround sound panner using Vector Base Amplitude Panning (VBAP).
/// Takes a mono source and positions it in 3D space across surround speakers.
/// </summary>
public class SurroundPanner : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly SurroundFormat _format;
    private readonly float[] _channelGains;
    private readonly SurroundChannel[] _channels;
    private readonly float[] _monoBuffer;
    private readonly VbapCalculator _vbap;

    /// <summary>
    /// Output wave format (multichannel based on surround format).
    /// </summary>
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// Horizontal position in degrees (-180 to 180).
    /// 0 = front center, -90 = left, +90 = right, +/-180 = rear
    /// </summary>
    public float Azimuth { get; set; }

    /// <summary>
    /// Vertical position in degrees (-90 to 90).
    /// 0 = ear level, +90 = above
    /// </summary>
    public float Elevation { get; set; }

    /// <summary>
    /// Distance from listener (affects level attenuation).
    /// 1.0 = reference distance, higher = further away
    /// </summary>
    public float Distance { get; set; } = 1.0f;

    /// <summary>
    /// Spread control (0 = point source, 1 = full spread to all speakers).
    /// </summary>
    public float Spread { get; set; }

    /// <summary>
    /// Level sent to LFE channel (0 to 1).
    /// </summary>
    public float LFELevel { get; set; }

    /// <summary>
    /// Controls phantom center vs discrete center routing (0 to 1).
    /// 0 = all center content to L/R, 1 = all center content to C speaker
    /// </summary>
    public float CenterDivergence { get; set; } = 0.5f;

    /// <summary>
    /// Creates a new surround panner.
    /// </summary>
    /// <param name="monoSource">Mono audio source to pan</param>
    /// <param name="format">Target surround format</param>
    public SurroundPanner(ISampleProvider monoSource, SurroundFormat format)
    {
        if (monoSource.WaveFormat.Channels != 1)
        {
            throw new ArgumentException("Source must be mono (single channel)", nameof(monoSource));
        }

        _source = monoSource;
        _format = format;

        int channelCount = format.GetChannelCount();
        _channelGains = new float[channelCount];
        _channels = SurroundChannel.CreateChannelsForFormat(format);
        _monoBuffer = new float[monoSource.WaveFormat.SampleRate]; // 1 second buffer

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
            monoSource.WaveFormat.SampleRate,
            channelCount);

        _vbap = new VbapCalculator(_channels, format);
    }

    /// <summary>
    /// Sets the 3D position of the sound source.
    /// </summary>
    public void SetPosition(float azimuth, float elevation, float distance)
    {
        Azimuth = Math.Clamp(azimuth, -180f, 180f);
        Elevation = Math.Clamp(elevation, -90f, 90f);
        Distance = Math.Max(0.001f, distance);
    }

    /// <summary>
    /// Calculates gain coefficients for all channels based on the current position.
    /// </summary>
    public float[] CalculateGains(float azimuth, float elevation)
    {
        var gains = new float[_channelGains.Length];

        // Calculate base VBAP gains
        var vbapGains = _vbap.CalculateGains(azimuth, elevation);
        Array.Copy(vbapGains, gains, Math.Min(gains.Length, vbapGains.Length));

        // Apply spread: blend between point source and equal distribution
        if (Spread > 0)
        {
            float equalGain = 1.0f / MathF.Sqrt(gains.Length);
            for (int i = 0; i < gains.Length; i++)
            {
                if (!_channels[i].IsLFE) // Don't spread to LFE
                {
                    gains[i] = gains[i] * (1 - Spread) + equalGain * Spread;
                }
            }
        }

        // Apply center divergence
        ApplyCenterDivergence(gains, azimuth);

        // Apply LFE level (LFE gets a separate send)
        int lfeIndex = _format.GetLFEChannel();
        if (lfeIndex >= 0)
        {
            gains[lfeIndex] = LFELevel;
        }

        // Apply distance attenuation (inverse distance law)
        float distanceGain = 1.0f / Distance;
        for (int i = 0; i < gains.Length; i++)
        {
            gains[i] *= distanceGain;
        }

        // Apply per-channel trim levels
        for (int i = 0; i < gains.Length; i++)
        {
            gains[i] *= _channels[i].Level;
        }

        return gains;
    }

    /// <summary>
    /// Reads audio and pans it to surround channels.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int channelCount = _format.GetChannelCount();
        int sampleFrames = count / channelCount;

        // Ensure mono buffer is large enough
        if (sampleFrames > _monoBuffer.Length)
        {
            // Should not happen with reasonable buffer sizes
            sampleFrames = _monoBuffer.Length;
        }

        // Read mono samples
        int monoSamplesRead = _source.Read(_monoBuffer, 0, sampleFrames);

        if (monoSamplesRead == 0)
        {
            return 0;
        }

        // Calculate current gains
        var gains = CalculateGains(Azimuth, Elevation);

        // Pan mono samples to surround
        for (int i = 0; i < monoSamplesRead; i++)
        {
            float monoSample = _monoBuffer[i];
            int frameOffset = offset + (i * channelCount);

            for (int ch = 0; ch < channelCount; ch++)
            {
                buffer[frameOffset + ch] = monoSample * gains[ch];
            }
        }

        return monoSamplesRead * channelCount;
    }

    /// <summary>
    /// Applies center divergence to the calculated gains.
    /// </summary>
    private void ApplyCenterDivergence(float[] gains, float azimuth)
    {
        // Only apply if format has a center channel and we're near center
        if (_format == SurroundFormat.Stereo || _format == SurroundFormat.Quad)
        {
            return;
        }

        // Find indices for L, C, R channels
        int leftIndex = -1, centerIndex = -1, rightIndex = -1;
        for (int i = 0; i < _channels.Length; i++)
        {
            switch (_channels[i].Type)
            {
                case SurroundChannelType.Left:
                    leftIndex = i;
                    break;
                case SurroundChannelType.Center:
                    centerIndex = i;
                    break;
                case SurroundChannelType.Right:
                    rightIndex = i;
                    break;
            }
        }

        if (centerIndex < 0 || leftIndex < 0 || rightIndex < 0)
        {
            return;
        }

        // Calculate how much of the center channel to redistribute to L/R
        float centerGain = gains[centerIndex];
        float divergenceFactor = 1.0f - CenterDivergence;

        if (divergenceFactor > 0 && centerGain > 0)
        {
            // Move some center content to L and R
            float redistribution = centerGain * divergenceFactor * 0.707f; // -3dB to L and R
            gains[centerIndex] *= CenterDivergence;
            gains[leftIndex] += redistribution;
            gains[rightIndex] += redistribution;
        }
    }
}

/// <summary>
/// Vector Base Amplitude Panning calculator.
/// Implements the VBAP algorithm for accurate 2D/3D sound positioning.
/// </summary>
internal class VbapCalculator
{
    private readonly SurroundChannel[] _channels;
    private readonly SurroundFormat _format;
    private readonly List<SpeakerTriplet> _triplets;
    private readonly List<SpeakerPair> _pairs;

    public VbapCalculator(SurroundChannel[] channels, SurroundFormat format)
    {
        _channels = channels;
        _format = format;

        // Build speaker pairs for 2D panning
        _pairs = BuildSpeakerPairs();

        // Build speaker triplets for 3D panning (with height channels)
        _triplets = format.HasHeightChannels() ? BuildSpeakerTriplets() : new List<SpeakerTriplet>();
    }

    public float[] CalculateGains(float azimuth, float elevation)
    {
        var gains = new float[_channels.Length];

        // Convert to unit vector
        float azimuthRad = azimuth * MathF.PI / 180f;
        float elevationRad = elevation * MathF.PI / 180f;
        float cosElevation = MathF.Cos(elevationRad);

        var sourceDir = (
            X: MathF.Sin(azimuthRad) * cosElevation,
            Y: MathF.Sin(elevationRad),
            Z: MathF.Cos(azimuthRad) * cosElevation
        );

        // Use 3D VBAP if we have height channels and non-zero elevation
        if (_format.HasHeightChannels() && MathF.Abs(elevation) > 1f)
        {
            Calculate3DVbap(sourceDir, gains);
        }
        else
        {
            Calculate2DVbap(sourceDir, gains);
        }

        // Normalize gains (power normalization)
        float sumSquared = 0f;
        for (int i = 0; i < gains.Length; i++)
        {
            sumSquared += gains[i] * gains[i];
        }

        if (sumSquared > 0.001f)
        {
            float normalizer = 1.0f / MathF.Sqrt(sumSquared);
            for (int i = 0; i < gains.Length; i++)
            {
                gains[i] *= normalizer;
            }
        }

        return gains;
    }

    private void Calculate2DVbap((float X, float Y, float Z) sourceDir, float[] gains)
    {
        // Project source to horizontal plane
        float sourceAzimuth = MathF.Atan2(sourceDir.X, sourceDir.Z);

        // Find the speaker pair that contains this direction
        SpeakerPair? bestPair = null;
        float bestGain1 = 0, bestGain2 = 0;

        foreach (var pair in _pairs)
        {
            // Get speaker directions (on horizontal plane)
            var (s1x, _, s1z) = _channels[pair.Index1].ToUnitVector();
            var (s2x, _, s2z) = _channels[pair.Index2].ToUnitVector();

            float speaker1Azimuth = MathF.Atan2(s1x, s1z);
            float speaker2Azimuth = MathF.Atan2(s2x, s2z);

            // Check if source is between these speakers
            if (IsAngleBetween(sourceAzimuth, speaker1Azimuth, speaker2Azimuth))
            {
                // Calculate gains using inverse matrix
                var result = SolveVbap2D(sourceAzimuth, speaker1Azimuth, speaker2Azimuth);

                if (result.Valid && result.G1 >= 0 && result.G2 >= 0)
                {
                    bestPair = pair;
                    bestGain1 = result.G1;
                    bestGain2 = result.G2;
                    break;
                }
            }
        }

        if (bestPair is SpeakerPair foundPair)
        {
            gains[foundPair.Index1] = bestGain1;
            gains[foundPair.Index2] = bestGain2;
        }
        else
        {
            // Fallback: find nearest speaker
            float minAngle = float.MaxValue;
            int nearestIndex = 0;

            for (int i = 0; i < _channels.Length; i++)
            {
                if (_channels[i].IsLFE) continue;

                var (sx, _, sz) = _channels[i].ToUnitVector();
                float speakerAzimuth = MathF.Atan2(sx, sz);
                float angleDiff = MathF.Abs(NormalizeAngle(sourceAzimuth - speakerAzimuth));

                if (angleDiff < minAngle)
                {
                    minAngle = angleDiff;
                    nearestIndex = i;
                }
            }

            gains[nearestIndex] = 1.0f;
        }
    }

    private void Calculate3DVbap((float X, float Y, float Z) sourceDir, float[] gains)
    {
        // Find the best speaker triplet
        SpeakerTriplet? bestTriplet = null;
        float bestG1 = 0, bestG2 = 0, bestG3 = 0;

        foreach (var triplet in _triplets)
        {
            var s1 = _channels[triplet.Index1].ToUnitVector();
            var s2 = _channels[triplet.Index2].ToUnitVector();
            var s3 = _channels[triplet.Index3].ToUnitVector();

            var result = SolveVbap3D(sourceDir, s1, s2, s3);

            if (result.Valid && result.G1 >= 0 && result.G2 >= 0 && result.G3 >= 0)
            {
                bestTriplet = triplet;
                bestG1 = result.G1;
                bestG2 = result.G2;
                bestG3 = result.G3;
                break;
            }
        }

        if (bestTriplet is SpeakerTriplet foundTriplet)
        {
            gains[foundTriplet.Index1] = bestG1;
            gains[foundTriplet.Index2] = bestG2;
            gains[foundTriplet.Index3] = bestG3;
        }
        else
        {
            // Fall back to 2D VBAP
            Calculate2DVbap(sourceDir, gains);
        }
    }

    private static (bool Valid, float G1, float G2) SolveVbap2D(float sourceAngle, float speaker1Angle, float speaker2Angle)
    {
        // Solve for gains g1, g2 such that:
        // g1 * s1 + g2 * s2 = source direction

        float s1x = MathF.Sin(speaker1Angle);
        float s1z = MathF.Cos(speaker1Angle);
        float s2x = MathF.Sin(speaker2Angle);
        float s2z = MathF.Cos(speaker2Angle);
        float px = MathF.Sin(sourceAngle);
        float pz = MathF.Cos(sourceAngle);

        // 2x2 matrix inverse
        float det = s1x * s2z - s1z * s2x;

        if (MathF.Abs(det) < 0.0001f)
        {
            return (false, 0, 0);
        }

        float invDet = 1.0f / det;
        float g1 = (s2z * px - s2x * pz) * invDet;
        float g2 = (-s1z * px + s1x * pz) * invDet;

        return (true, g1, g2);
    }

    private static (bool Valid, float G1, float G2, float G3) SolveVbap3D(
        (float X, float Y, float Z) source,
        (float X, float Y, float Z) s1,
        (float X, float Y, float Z) s2,
        (float X, float Y, float Z) s3)
    {
        // Solve 3x3 system for gains g1, g2, g3
        // [s1x s2x s3x] [g1]   [px]
        // [s1y s2y s3y] [g2] = [py]
        // [s1z s2z s3z] [g3]   [pz]

        // Calculate determinant
        float det = s1.X * (s2.Y * s3.Z - s2.Z * s3.Y)
                  - s2.X * (s1.Y * s3.Z - s1.Z * s3.Y)
                  + s3.X * (s1.Y * s2.Z - s1.Z * s2.Y);

        if (MathF.Abs(det) < 0.0001f)
        {
            return (false, 0, 0, 0);
        }

        float invDet = 1.0f / det;

        // Calculate cofactors and solve (Cramer's rule)
        float g1 = ((s2.Y * s3.Z - s2.Z * s3.Y) * source.X
                  + (s2.Z * s3.X - s2.X * s3.Z) * source.Y
                  + (s2.X * s3.Y - s2.Y * s3.X) * source.Z) * invDet;

        float g2 = ((s1.Z * s3.Y - s1.Y * s3.Z) * source.X
                  + (s1.X * s3.Z - s1.Z * s3.X) * source.Y
                  + (s1.Y * s3.X - s1.X * s3.Y) * source.Z) * invDet;

        float g3 = ((s1.Y * s2.Z - s1.Z * s2.Y) * source.X
                  + (s1.Z * s2.X - s1.X * s2.Z) * source.Y
                  + (s1.X * s2.Y - s1.Y * s2.X) * source.Z) * invDet;

        return (true, g1, g2, g3);
    }

    private List<SpeakerPair> BuildSpeakerPairs()
    {
        var pairs = new List<SpeakerPair>();

        // Get non-LFE, non-height speakers sorted by azimuth
        var speakers = new List<(int Index, float Azimuth)>();

        for (int i = 0; i < _channels.Length; i++)
        {
            if (_channels[i].IsLFE) continue;
            if (_channels[i].Elevation > 30f) continue; // Skip height channels

            var (x, _, z) = _channels[i].ToUnitVector();
            float azimuth = MathF.Atan2(x, z);
            speakers.Add((i, azimuth));
        }

        speakers.Sort((a, b) => a.Azimuth.CompareTo(b.Azimuth));

        // Create pairs from adjacent speakers
        for (int i = 0; i < speakers.Count; i++)
        {
            int nextIndex = (i + 1) % speakers.Count;
            pairs.Add(new SpeakerPair(speakers[i].Index, speakers[nextIndex].Index));
        }

        return pairs;
    }

    private List<SpeakerTriplet> BuildSpeakerTriplets()
    {
        var triplets = new List<SpeakerTriplet>();

        // For Atmos 7.1.4, create triplets connecting bed speakers to height speakers
        // This is a simplified version - full implementation would use Delaunay triangulation

        // Get channel indices by type
        var channelMap = new Dictionary<SurroundChannelType, int>();
        for (int i = 0; i < _channels.Length; i++)
        {
            channelMap[_channels[i].Type] = i;
        }

        // Front triplets (connecting front bed to front height)
        if (channelMap.TryGetValue(SurroundChannelType.Left, out int l) &&
            channelMap.TryGetValue(SurroundChannelType.Center, out int c) &&
            channelMap.TryGetValue(SurroundChannelType.Right, out int r) &&
            channelMap.TryGetValue(SurroundChannelType.TopFrontLeft, out int tfl) &&
            channelMap.TryGetValue(SurroundChannelType.TopFrontRight, out int tfr))
        {
            triplets.Add(new SpeakerTriplet(l, c, tfl));
            triplets.Add(new SpeakerTriplet(c, r, tfr));
            triplets.Add(new SpeakerTriplet(c, tfl, tfr));
            triplets.Add(new SpeakerTriplet(l, tfl, c));
            triplets.Add(new SpeakerTriplet(r, c, tfr));
        }

        // Rear triplets (connecting rear bed to rear height)
        if (channelMap.TryGetValue(SurroundChannelType.LeftRearSurround, out int lsr) &&
            channelMap.TryGetValue(SurroundChannelType.RightRearSurround, out int rsr) &&
            channelMap.TryGetValue(SurroundChannelType.TopRearLeft, out int trl) &&
            channelMap.TryGetValue(SurroundChannelType.TopRearRight, out int trr))
        {
            triplets.Add(new SpeakerTriplet(lsr, rsr, trl));
            triplets.Add(new SpeakerTriplet(lsr, rsr, trr));
            triplets.Add(new SpeakerTriplet(trl, trr, lsr));
            triplets.Add(new SpeakerTriplet(trl, trr, rsr));
        }

        // Side triplets (connecting side speakers to height)
        if (channelMap.TryGetValue(SurroundChannelType.LeftSideSurround, out int lss) &&
            channelMap.TryGetValue(SurroundChannelType.RightSideSurround, out int rss))
        {
            if (channelMap.TryGetValue(SurroundChannelType.Left, out int leftIdx) &&
                channelMap.TryGetValue(SurroundChannelType.LeftRearSurround, out int lsrIdx) &&
                channelMap.TryGetValue(SurroundChannelType.TopFrontLeft, out tfl) &&
                channelMap.TryGetValue(SurroundChannelType.TopRearLeft, out trl))
            {
                triplets.Add(new SpeakerTriplet(leftIdx, lss, tfl));
                triplets.Add(new SpeakerTriplet(lss, lsrIdx, trl));
                triplets.Add(new SpeakerTriplet(lss, tfl, trl));
            }

            if (channelMap.TryGetValue(SurroundChannelType.Right, out int rightIdx) &&
                channelMap.TryGetValue(SurroundChannelType.RightRearSurround, out int rsrIdx) &&
                channelMap.TryGetValue(SurroundChannelType.TopFrontRight, out tfr) &&
                channelMap.TryGetValue(SurroundChannelType.TopRearRight, out trr))
            {
                triplets.Add(new SpeakerTriplet(rightIdx, rss, tfr));
                triplets.Add(new SpeakerTriplet(rss, rsrIdx, trr));
                triplets.Add(new SpeakerTriplet(rss, tfr, trr));
            }
        }

        // Top plane triplets
        if (channelMap.TryGetValue(SurroundChannelType.TopFrontLeft, out tfl) &&
            channelMap.TryGetValue(SurroundChannelType.TopFrontRight, out tfr) &&
            channelMap.TryGetValue(SurroundChannelType.TopRearLeft, out trl) &&
            channelMap.TryGetValue(SurroundChannelType.TopRearRight, out trr))
        {
            triplets.Add(new SpeakerTriplet(tfl, tfr, trl));
            triplets.Add(new SpeakerTriplet(tfr, trr, trl));
        }

        return triplets;
    }

    private static bool IsAngleBetween(float angle, float start, float end)
    {
        angle = NormalizeAngle(angle);
        start = NormalizeAngle(start);
        end = NormalizeAngle(end);

        // Handle wrap-around at -PI/PI
        if (start <= end)
        {
            return angle >= start && angle <= end;
        }
        else
        {
            return angle >= start || angle <= end;
        }
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI) angle -= 2f * MathF.PI;
        while (angle < -MathF.PI) angle += 2f * MathF.PI;
        return angle;
    }

    private readonly record struct SpeakerPair(int Index1, int Index2);
    private readonly record struct SpeakerTriplet(int Index1, int Index2, int Index3);
}
