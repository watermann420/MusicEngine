// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: MusicEngine component.

using System;

namespace MusicEngine.Core.Spatial;

/// <summary>
/// Room size presets for reverb estimation.
/// </summary>
public enum RoomSize
{
    /// <summary>Small room (bathroom, closet)</summary>
    Small,
    /// <summary>Medium room (bedroom, office)</summary>
    Medium,
    /// <summary>Large room (living room, conference room)</summary>
    Large,
    /// <summary>Hall (auditorium, concert hall)</summary>
    Hall,
    /// <summary>Outdoor environment (minimal reverb)</summary>
    Outdoor,
    /// <summary>Custom room parameters</summary>
    Custom
}

/// <summary>
/// Represents the listener (virtual microphone/ears) in 3D space.
/// All spatial audio is rendered relative to the listener's position and orientation.
/// </summary>
public class SpatialListener
{
    // Position
    private float _positionX;
    private float _positionY;
    private float _positionZ;

    // Orientation
    private float _yaw;   // Rotation around vertical axis (look left/right)
    private float _pitch; // Rotation around lateral axis (look up/down)
    private float _roll;  // Rotation around forward axis (head tilt)

    // HRTF settings
    private bool _hrtfEnabled = false;
    private float _hrtfStrength = 1f;

    // Distance settings
    private float _referenceDistance = 1f;
    private float _maxDistance = 1000f;
    private float _speedOfSound = 343f; // m/s at sea level, 20C

    // Room estimation
    private RoomSize _roomSize = RoomSize.Medium;
    private float _roomWidth = 5f;
    private float _roomDepth = 5f;
    private float _roomHeight = 2.5f;
    private float _roomAbsorption = 0.5f;

    /// <summary>
    /// Creates a new spatial listener at the origin.
    /// </summary>
    public SpatialListener()
    {
        // Default position is at origin facing forward (positive Y)
    }

    #region Position Properties

    /// <summary>
    /// X position (right is positive).
    /// </summary>
    public float PositionX
    {
        get => _positionX;
        set => _positionX = value;
    }

    /// <summary>
    /// Y position (front is positive).
    /// </summary>
    public float PositionY
    {
        get => _positionY;
        set => _positionY = value;
    }

    /// <summary>
    /// Z position (up is positive).
    /// </summary>
    public float PositionZ
    {
        get => _positionZ;
        set => _positionZ = value;
    }

    /// <summary>
    /// Sets the 3D position in one call.
    /// </summary>
    public void SetPosition(float x, float y, float z)
    {
        _positionX = x;
        _positionY = y;
        _positionZ = z;
    }

    /// <summary>
    /// Gets the position as a tuple.
    /// </summary>
    public (float X, float Y, float Z) Position => (_positionX, _positionY, _positionZ);

    #endregion

    #region Orientation Properties

    /// <summary>
    /// Yaw angle in degrees (rotation around vertical axis).
    /// 0 = facing forward (positive Y), 90 = facing right (positive X).
    /// </summary>
    public float Yaw
    {
        get => _yaw;
        set => _yaw = NormalizeAngle(value);
    }

    /// <summary>
    /// Pitch angle in degrees (rotation around lateral axis).
    /// 0 = level, positive = looking up.
    /// </summary>
    public float Pitch
    {
        get => _pitch;
        set => _pitch = Math.Clamp(value, -90f, 90f);
    }

    /// <summary>
    /// Roll angle in degrees (rotation around forward axis).
    /// 0 = level, positive = tilting right.
    /// </summary>
    public float Roll
    {
        get => _roll;
        set => _roll = NormalizeAngle(value);
    }

    /// <summary>
    /// Sets the orientation in one call.
    /// </summary>
    public void SetOrientation(float yaw, float pitch, float roll)
    {
        _yaw = NormalizeAngle(yaw);
        _pitch = Math.Clamp(pitch, -90f, 90f);
        _roll = NormalizeAngle(roll);
    }

    /// <summary>
    /// Gets the forward direction as a unit vector.
    /// </summary>
    public (float X, float Y, float Z) ForwardDirection
    {
        get
        {
            float yawRad = _yaw * MathF.PI / 180f;
            float pitchRad = _pitch * MathF.PI / 180f;

            float x = MathF.Sin(yawRad) * MathF.Cos(pitchRad);
            float y = MathF.Cos(yawRad) * MathF.Cos(pitchRad);
            float z = MathF.Sin(pitchRad);

            return (x, y, z);
        }
    }

    /// <summary>
    /// Gets the up direction as a unit vector (accounting for roll).
    /// </summary>
    public (float X, float Y, float Z) UpDirection
    {
        get
        {
            float yawRad = _yaw * MathF.PI / 180f;
            float pitchRad = _pitch * MathF.PI / 180f;
            float rollRad = _roll * MathF.PI / 180f;

            // Start with world up
            float ux = 0f;
            float uy = 0f;
            float uz = 1f;

            // Apply pitch rotation
            float tempY = uy * MathF.Cos(pitchRad) - uz * MathF.Sin(pitchRad);
            float tempZ = uy * MathF.Sin(pitchRad) + uz * MathF.Cos(pitchRad);
            uy = tempY;
            uz = tempZ;

            // Apply yaw rotation
            float tempX = ux * MathF.Cos(yawRad) + uy * MathF.Sin(yawRad);
            tempY = -ux * MathF.Sin(yawRad) + uy * MathF.Cos(yawRad);
            ux = tempX;
            uy = tempY;

            // Apply roll rotation around forward axis
            var (fx, fy, fz) = ForwardDirection;
            float cosRoll = MathF.Cos(rollRad);
            float sinRoll = MathF.Sin(rollRad);

            // Rodrigues rotation formula
            float dot = ux * fx + uy * fy + uz * fz;
            float crossX = fy * uz - fz * uy;
            float crossY = fz * ux - fx * uz;
            float crossZ = fx * uy - fy * ux;

            float finalX = ux * cosRoll + crossX * sinRoll + fx * dot * (1 - cosRoll);
            float finalY = uy * cosRoll + crossY * sinRoll + fy * dot * (1 - cosRoll);
            float finalZ = uz * cosRoll + crossZ * sinRoll + fz * dot * (1 - cosRoll);

            return (finalX, finalY, finalZ);
        }
    }

    /// <summary>
    /// Gets the right direction as a unit vector.
    /// </summary>
    public (float X, float Y, float Z) RightDirection
    {
        get
        {
            var (fx, fy, fz) = ForwardDirection;
            var (ux, uy, uz) = UpDirection;

            // Cross product: right = forward x up
            float rx = fy * uz - fz * uy;
            float ry = fz * ux - fx * uz;
            float rz = fx * uy - fy * ux;

            // Normalize
            float len = MathF.Sqrt(rx * rx + ry * ry + rz * rz);
            if (len > 0.0001f)
            {
                rx /= len;
                ry /= len;
                rz /= len;
            }

            return (rx, ry, rz);
        }
    }

    #endregion

    #region HRTF Properties

    /// <summary>
    /// Whether HRTF (Head-Related Transfer Function) processing is enabled.
    /// When enabled, stereo output will include binaural cues for 3D perception with headphones.
    /// </summary>
    public bool HrtfEnabled
    {
        get => _hrtfEnabled;
        set => _hrtfEnabled = value;
    }

    /// <summary>
    /// HRTF effect strength (0 = disabled, 1 = full effect).
    /// </summary>
    public float HrtfStrength
    {
        get => _hrtfStrength;
        set => _hrtfStrength = Math.Clamp(value, 0f, 1f);
    }

    #endregion

    #region Distance Properties

    /// <summary>
    /// Reference distance for distance attenuation (default = 1.0).
    /// Sources at this distance play at full volume.
    /// </summary>
    public float ReferenceDistance
    {
        get => _referenceDistance;
        set => _referenceDistance = Math.Max(0.001f, value);
    }

    /// <summary>
    /// Maximum audible distance. Sources beyond this are silent.
    /// </summary>
    public float MaxDistance
    {
        get => _maxDistance;
        set => _maxDistance = Math.Max(_referenceDistance, value);
    }

    /// <summary>
    /// Speed of sound for delay and doppler calculations (default = 343 m/s).
    /// </summary>
    public float SpeedOfSound
    {
        get => _speedOfSound;
        set => _speedOfSound = Math.Max(1f, value);
    }

    #endregion

    #region Room Properties

    /// <summary>
    /// Room size preset for reverb estimation.
    /// </summary>
    public RoomSize RoomSize
    {
        get => _roomSize;
        set
        {
            _roomSize = value;
            ApplyRoomPreset(value);
        }
    }

    /// <summary>
    /// Custom room width in units (used when RoomSize = Custom).
    /// </summary>
    public float RoomWidth
    {
        get => _roomWidth;
        set => _roomWidth = Math.Max(0.5f, value);
    }

    /// <summary>
    /// Custom room depth in units (used when RoomSize = Custom).
    /// </summary>
    public float RoomDepth
    {
        get => _roomDepth;
        set => _roomDepth = Math.Max(0.5f, value);
    }

    /// <summary>
    /// Custom room height in units (used when RoomSize = Custom).
    /// </summary>
    public float RoomHeight
    {
        get => _roomHeight;
        set => _roomHeight = Math.Max(0.5f, value);
    }

    /// <summary>
    /// Room absorption coefficient (0 = reflective, 1 = absorptive).
    /// </summary>
    public float RoomAbsorption
    {
        get => _roomAbsorption;
        set => _roomAbsorption = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Calculates the estimated reverb time (T60) based on room parameters.
    /// </summary>
    public float EstimatedReverbTime
    {
        get
        {
            // Sabine's equation: T60 = 0.161 * V / A
            // V = volume, A = total absorption
            float volume = _roomWidth * _roomDepth * _roomHeight;
            float surfaceArea = 2f * (_roomWidth * _roomDepth + _roomWidth * _roomHeight + _roomDepth * _roomHeight);
            float totalAbsorption = surfaceArea * _roomAbsorption;

            if (totalAbsorption < 0.001f) totalAbsorption = 0.001f;

            return 0.161f * volume / totalAbsorption;
        }
    }

    #endregion

    /// <summary>
    /// Applies preset room dimensions.
    /// </summary>
    private void ApplyRoomPreset(RoomSize size)
    {
        switch (size)
        {
            case RoomSize.Small:
                _roomWidth = 2f;
                _roomDepth = 2f;
                _roomHeight = 2.2f;
                _roomAbsorption = 0.4f;
                break;

            case RoomSize.Medium:
                _roomWidth = 5f;
                _roomDepth = 5f;
                _roomHeight = 2.5f;
                _roomAbsorption = 0.5f;
                break;

            case RoomSize.Large:
                _roomWidth = 10f;
                _roomDepth = 10f;
                _roomHeight = 3f;
                _roomAbsorption = 0.4f;
                break;

            case RoomSize.Hall:
                _roomWidth = 25f;
                _roomDepth = 40f;
                _roomHeight = 12f;
                _roomAbsorption = 0.3f;
                break;

            case RoomSize.Outdoor:
                _roomWidth = 100f;
                _roomDepth = 100f;
                _roomHeight = 50f;
                _roomAbsorption = 0.95f;
                break;

            case RoomSize.Custom:
                // Keep current values
                break;
        }
    }

    /// <summary>
    /// Calculates the distance from the listener to a point in 3D space.
    /// </summary>
    public float DistanceTo(float x, float y, float z)
    {
        float dx = x - _positionX;
        float dy = y - _positionY;
        float dz = z - _positionZ;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// Calculates the angle (azimuth) from the listener to a point.
    /// Returns the angle in degrees (0 = front, 90 = right, -90 = left, 180 = back).
    /// </summary>
    public float AzimuthTo(float x, float y, float z)
    {
        // Get relative position
        float dx = x - _positionX;
        float dy = y - _positionY;

        // Apply listener yaw rotation
        float yawRad = _yaw * MathF.PI / 180f;
        float rotX = dx * MathF.Cos(yawRad) - dy * MathF.Sin(yawRad);
        float rotY = dx * MathF.Sin(yawRad) + dy * MathF.Cos(yawRad);

        return MathF.Atan2(rotX, rotY) * 180f / MathF.PI;
    }

    /// <summary>
    /// Calculates the elevation angle from the listener to a point.
    /// Returns the angle in degrees (0 = ear level, 90 = above, -90 = below).
    /// </summary>
    public float ElevationTo(float x, float y, float z)
    {
        float dx = x - _positionX;
        float dy = y - _positionY;
        float dz = z - _positionZ;

        float horizontalDist = MathF.Sqrt(dx * dx + dy * dy);
        return MathF.Atan2(dz, horizontalDist) * 180f / MathF.PI;
    }

    /// <summary>
    /// Calculates the delay in seconds for sound to travel from a source to the listener.
    /// </summary>
    public float PropagationDelay(float sourceX, float sourceY, float sourceZ)
    {
        float distance = DistanceTo(sourceX, sourceY, sourceZ);
        return distance / _speedOfSound;
    }

    /// <summary>
    /// Normalizes an angle to the range [-180, 180).
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        while (angle >= 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    /// <summary>
    /// Resets the listener to the default position and orientation.
    /// </summary>
    public void Reset()
    {
        _positionX = 0f;
        _positionY = 0f;
        _positionZ = 0f;
        _yaw = 0f;
        _pitch = 0f;
        _roll = 0f;
    }
}
