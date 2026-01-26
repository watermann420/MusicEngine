// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;

namespace MusicEngine.Core.Spatial;

/// <summary>
/// Ambisonic channel ordering convention.
/// </summary>
public enum AmbisonicChannelOrdering
{
    /// <summary>Ambisonic Channel Number (ACN) - standard ordering</summary>
    ACN,
    /// <summary>Furse-Malham ordering (legacy)</summary>
    FuMa
}

/// <summary>
/// Ambisonic normalization convention.
/// </summary>
public enum AmbisonicNormalization
{
    /// <summary>Semi-Normalized 3D (SN3D) - standard</summary>
    SN3D,
    /// <summary>Full 3D normalization (N3D)</summary>
    N3D,
    /// <summary>Furse-Malham normalization (legacy)</summary>
    FuMa
}

/// <summary>
/// Encodes mono audio sources to Ambisonic B-format.
/// Supports up to 3rd order Ambisonics with ACN channel ordering and SN3D normalization.
/// </summary>
public class AmbisonicEncoder
{
    private readonly int _order;
    private readonly int _channelCount;
    private readonly AmbisonicChannelOrdering _ordering;
    private readonly AmbisonicNormalization _normalization;

    // Spherical harmonic coefficients cache
    private readonly float[] _coefficients;

    /// <summary>
    /// Gets the ambisonic order (1, 2, or 3).
    /// </summary>
    public int Order => _order;

    /// <summary>
    /// Gets the number of output channels.
    /// Order 1: 4 channels (W, Y, Z, X)
    /// Order 2: 9 channels
    /// Order 3: 16 channels
    /// </summary>
    public int OutputChannelCount => _channelCount;

    /// <summary>
    /// Gets the channel ordering convention.
    /// </summary>
    public AmbisonicChannelOrdering ChannelOrdering => _ordering;

    /// <summary>
    /// Gets the normalization convention.
    /// </summary>
    public AmbisonicNormalization Normalization => _normalization;

    /// <summary>
    /// Creates a new ambisonic encoder.
    /// </summary>
    /// <param name="order">Ambisonic order (1, 2, or 3)</param>
    /// <param name="ordering">Channel ordering convention (default: ACN)</param>
    /// <param name="normalization">Normalization convention (default: SN3D)</param>
    public AmbisonicEncoder(
        int order = 1,
        AmbisonicChannelOrdering ordering = AmbisonicChannelOrdering.ACN,
        AmbisonicNormalization normalization = AmbisonicNormalization.SN3D)
    {
        if (order < 1 || order > 3)
            throw new ArgumentOutOfRangeException(nameof(order), "Ambisonic order must be 1, 2, or 3");

        _order = order;
        _ordering = ordering;
        _normalization = normalization;

        // Calculate channel count: (order + 1)^2
        _channelCount = (order + 1) * (order + 1);
        _coefficients = new float[_channelCount];
    }

    /// <summary>
    /// Encodes a mono sample to ambisonic B-format at the specified position.
    /// </summary>
    /// <param name="sample">The mono audio sample</param>
    /// <param name="azimuth">Azimuth angle in degrees (0 = front, 90 = right, -90 = left)</param>
    /// <param name="elevation">Elevation angle in degrees (0 = ear level, 90 = above, -90 = below)</param>
    /// <param name="outputBuffer">Buffer to receive the ambisonic channels</param>
    /// <param name="offset">Offset into the output buffer</param>
    public void Encode(float sample, float azimuth, float elevation, float[] outputBuffer, int offset)
    {
        // Calculate spherical harmonic coefficients for this direction
        CalculateCoefficients(azimuth, elevation);

        // Apply coefficients to the sample
        for (int ch = 0; ch < _channelCount; ch++)
        {
            outputBuffer[offset + ch] = sample * _coefficients[ch];
        }
    }

    /// <summary>
    /// Encodes a mono sample and accumulates into the output buffer.
    /// </summary>
    public void EncodeAccumulate(float sample, float azimuth, float elevation, float[] outputBuffer, int offset)
    {
        CalculateCoefficients(azimuth, elevation);

        for (int ch = 0; ch < _channelCount; ch++)
        {
            outputBuffer[offset + ch] += sample * _coefficients[ch];
        }
    }

    /// <summary>
    /// Calculates spherical harmonic coefficients for a given direction.
    /// Uses ACN channel ordering and SN3D normalization by default.
    /// </summary>
    private void CalculateCoefficients(float azimuth, float elevation)
    {
        // Convert to radians
        float phi = azimuth * MathF.PI / 180f;      // Azimuth
        float theta = elevation * MathF.PI / 180f;  // Elevation

        // Pre-calculate trigonometric values
        float cosTheta = MathF.Cos(theta);
        float sinTheta = MathF.Sin(theta);
        float cosPhi = MathF.Cos(phi);
        float sinPhi = MathF.Sin(phi);
        float cos2Phi = MathF.Cos(2f * phi);
        float sin2Phi = MathF.Sin(2f * phi);
        float cos3Phi = MathF.Cos(3f * phi);
        float sin3Phi = MathF.Sin(3f * phi);

        // === Order 0 (1 channel) ===
        // ACN 0: W (omnidirectional)
        _coefficients[0] = GetNormalization(0, 0);

        if (_order >= 1)
        {
            // === Order 1 (3 additional channels) ===
            // ACN 1: Y = sin(phi) * cos(theta) - pointing left
            // ACN 2: Z = sin(theta) - pointing up
            // ACN 3: X = cos(phi) * cos(theta) - pointing front

            _coefficients[1] = sinPhi * cosTheta * GetNormalization(1, -1);  // Y
            _coefficients[2] = sinTheta * GetNormalization(1, 0);             // Z
            _coefficients[3] = cosPhi * cosTheta * GetNormalization(1, 1);   // X
        }

        if (_order >= 2)
        {
            // === Order 2 (5 additional channels) ===
            float cosTheta2 = cosTheta * cosTheta;
            float sinTheta2 = sinTheta * sinTheta;

            // ACN 4: V = sqrt(3)/2 * sin(2*phi) * cos^2(theta)
            _coefficients[4] = MathF.Sqrt(3f) / 2f * sin2Phi * cosTheta2 * GetNormalization(2, -2);

            // ACN 5: T = sqrt(3)/2 * sin(phi) * sin(2*theta)
            _coefficients[5] = MathF.Sqrt(3f) / 2f * sinPhi * 2f * sinTheta * cosTheta * GetNormalization(2, -1);

            // ACN 6: R = 1/2 * (3*sin^2(theta) - 1)
            _coefficients[6] = 0.5f * (3f * sinTheta2 - 1f) * GetNormalization(2, 0);

            // ACN 7: S = sqrt(3)/2 * cos(phi) * sin(2*theta)
            _coefficients[7] = MathF.Sqrt(3f) / 2f * cosPhi * 2f * sinTheta * cosTheta * GetNormalization(2, 1);

            // ACN 8: U = sqrt(3)/2 * cos(2*phi) * cos^2(theta)
            _coefficients[8] = MathF.Sqrt(3f) / 2f * cos2Phi * cosTheta2 * GetNormalization(2, 2);
        }

        if (_order >= 3)
        {
            // === Order 3 (7 additional channels) ===
            float cosTheta2 = cosTheta * cosTheta;
            float cosTheta3 = cosTheta2 * cosTheta;
            float sinTheta2 = sinTheta * sinTheta;

            // ACN 9: Q = sqrt(5/8) * sin(3*phi) * cos^3(theta)
            _coefficients[9] = MathF.Sqrt(5f / 8f) * sin3Phi * cosTheta3 * GetNormalization(3, -3);

            // ACN 10: O = sqrt(15)/2 * sin(2*phi) * sin(theta) * cos^2(theta)
            _coefficients[10] = MathF.Sqrt(15f) / 2f * sin2Phi * sinTheta * cosTheta2 * GetNormalization(3, -2);

            // ACN 11: M = sqrt(3/8) * sin(phi) * cos(theta) * (5*sin^2(theta) - 1)
            _coefficients[11] = MathF.Sqrt(3f / 8f) * sinPhi * cosTheta * (5f * sinTheta2 - 1f) * GetNormalization(3, -1);

            // ACN 12: K = 1/2 * sin(theta) * (5*sin^2(theta) - 3)
            _coefficients[12] = 0.5f * sinTheta * (5f * sinTheta2 - 3f) * GetNormalization(3, 0);

            // ACN 13: L = sqrt(3/8) * cos(phi) * cos(theta) * (5*sin^2(theta) - 1)
            _coefficients[13] = MathF.Sqrt(3f / 8f) * cosPhi * cosTheta * (5f * sinTheta2 - 1f) * GetNormalization(3, 1);

            // ACN 14: N = sqrt(15)/2 * cos(2*phi) * sin(theta) * cos^2(theta)
            _coefficients[14] = MathF.Sqrt(15f) / 2f * cos2Phi * sinTheta * cosTheta2 * GetNormalization(3, 2);

            // ACN 15: P = sqrt(5/8) * cos(3*phi) * cos^3(theta)
            _coefficients[15] = MathF.Sqrt(5f / 8f) * cos3Phi * cosTheta3 * GetNormalization(3, 3);
        }

        // Apply channel ordering conversion if needed
        if (_ordering == AmbisonicChannelOrdering.FuMa)
        {
            ConvertToFuMaOrdering();
        }
    }

    /// <summary>
    /// Gets the normalization factor for a given spherical harmonic.
    /// </summary>
    private float GetNormalization(int l, int m)
    {
        return _normalization switch
        {
            AmbisonicNormalization.SN3D => GetSN3DNormalization(l, m),
            AmbisonicNormalization.N3D => GetN3DNormalization(l, m),
            AmbisonicNormalization.FuMa => GetFuMaNormalization(l, m),
            _ => 1f
        };
    }

    /// <summary>
    /// SN3D (Semi-Normalized 3D) normalization factor.
    /// </summary>
    private float GetSN3DNormalization(int l, int m)
    {
        // SN3D normalization: sqrt((2 - delta_m0) * (l-|m|)! / (l+|m|)!)
        // For practical purposes, return 1.0 as the spherical harmonics
        // are already computed with SN3D-compatible scaling
        return 1f;
    }

    /// <summary>
    /// N3D (Full 3D) normalization factor.
    /// </summary>
    private float GetN3DNormalization(int l, int m)
    {
        // N3D = SN3D * sqrt(2*l + 1)
        return MathF.Sqrt(2f * l + 1f);
    }

    /// <summary>
    /// FuMa (Furse-Malham) normalization factor.
    /// </summary>
    private float GetFuMaNormalization(int l, int m)
    {
        // FuMa has specific normalization for each channel
        // W is scaled by 1/sqrt(2), others have various factors
        if (l == 0) return 1f / MathF.Sqrt(2f);
        return 1f;
    }

    /// <summary>
    /// Converts ACN channel ordering to FuMa ordering.
    /// </summary>
    private void ConvertToFuMaOrdering()
    {
        // FuMa ordering for first order: W, X, Y, Z
        // ACN ordering: W, Y, Z, X
        if (_order >= 1)
        {
            // Swap Y (ACN 1) and X (ACN 3) to get FuMa order
            float temp = _coefficients[1];
            _coefficients[1] = _coefficients[3]; // X
            _coefficients[3] = temp;             // Y moves to position 3

            // Actually FuMa is W, X, Y, Z so we need: ACN[0], ACN[3], ACN[1], ACN[2]
            // But we've already filled by ACN, so reorder:
            float y = _coefficients[1];
            float z = _coefficients[2];
            float x = _coefficients[3];
            _coefficients[1] = x; // X
            _coefficients[2] = y; // Y
            _coefficients[3] = z; // Z
        }

        // Higher orders would need additional reordering
        // (not commonly used with FuMa)
    }

    /// <summary>
    /// Gets the channel name for a given ACN index.
    /// </summary>
    public static string GetChannelName(int acnIndex)
    {
        return acnIndex switch
        {
            // Order 0
            0 => "W",
            // Order 1
            1 => "Y",
            2 => "Z",
            3 => "X",
            // Order 2
            4 => "V",
            5 => "T",
            6 => "R",
            7 => "S",
            8 => "U",
            // Order 3
            9 => "Q",
            10 => "O",
            11 => "M",
            12 => "K",
            13 => "L",
            14 => "N",
            15 => "P",
            _ => $"CH{acnIndex}"
        };
    }

    /// <summary>
    /// Gets the ACN index for a given order and degree.
    /// </summary>
    /// <param name="l">Order (0, 1, 2, 3)</param>
    /// <param name="m">Degree (-l to +l)</param>
    public static int GetAcnIndex(int l, int m)
    {
        // ACN = l^2 + l + m
        return l * l + l + m;
    }

    /// <summary>
    /// Gets the order and degree for a given ACN index.
    /// </summary>
    public static (int Order, int Degree) GetOrderDegree(int acnIndex)
    {
        int l = (int)MathF.Floor(MathF.Sqrt(acnIndex));
        int m = acnIndex - l * l - l;
        return (l, m);
    }
}
