// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Core engine component.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;


namespace MusicEngine.Core;


/// <summary>
/// Provides high-resolution timing using System.Diagnostics.Stopwatch for sub-millisecond accuracy.
/// Designed for professional music production requiring sample-accurate timing.
/// </summary>
public class HighResolutionTimer : IDisposable
{
    // Platform-specific high-resolution timing support
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uMilliseconds);

    private readonly Stopwatch _stopwatch;
    private readonly object _lock = new();
    private Thread? _timerThread;
    private volatile bool _running;
    private volatile bool _disposed;

    // Timing configuration
    private double _tickIntervalMicroseconds = 500.0; // Default: 0.5ms (500 microseconds)
    private double _targetTicksPerSecond = 2000.0; // Default: 2000 ticks per second
    private bool _useSpinWait;
    private int _spinWaitIterations = 10;

    // Jitter compensation
    private readonly CircularBuffer<double> _jitterBuffer;
    private double _averageJitter;
    private double _jitterCompensation;
    private bool _jitterCompensationEnabled = true;

    // Statistics
    private long _totalTicks;
    private long _lateTickCount;
    private double _maxJitter;
    private double _minJitter = double.MaxValue;
    private double _lastTickTime;

    // Platform timing enhancement
    private bool _highResolutionMode;
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Event fired on each timer tick with precise timing information.
    /// </summary>
    public event EventHandler<TimerTickEventArgs>? Tick;

    /// <summary>
    /// Event fired when a tick is late beyond the acceptable threshold.
    /// </summary>
    public event EventHandler<JitterEventArgs>? JitterDetected;

    /// <summary>
    /// Gets the stopwatch frequency for precise time calculations.
    /// </summary>
    public static long Frequency => Stopwatch.Frequency;

    /// <summary>
    /// Gets whether the system supports high-resolution timing.
    /// </summary>
    public static bool IsHighResolution => Stopwatch.IsHighResolution;

    /// <summary>
    /// Gets the resolution of the timer in nanoseconds.
    /// </summary>
    public static double ResolutionNanoseconds => 1_000_000_000.0 / Frequency;

    /// <summary>
    /// Gets or sets the tick interval in microseconds.
    /// </summary>
    public double TickIntervalMicroseconds
    {
        get => _tickIntervalMicroseconds;
        set
        {
            if (value < 100) // Minimum 100 microseconds (0.1ms)
                throw new ArgumentOutOfRangeException(nameof(value), "Tick interval must be at least 100 microseconds.");
            _tickIntervalMicroseconds = value;
            _targetTicksPerSecond = 1_000_000.0 / value;
        }
    }

    /// <summary>
    /// Gets or sets the target ticks per second.
    /// </summary>
    public double TargetTicksPerSecond
    {
        get => _targetTicksPerSecond;
        set
        {
            if (value < 10 || value > 100000)
                throw new ArgumentOutOfRangeException(nameof(value), "Target ticks per second must be between 10 and 100000.");
            _targetTicksPerSecond = value;
            _tickIntervalMicroseconds = 1_000_000.0 / value;
        }
    }

    /// <summary>
    /// Gets or sets whether to use SpinWait for highest precision (CPU intensive).
    /// </summary>
    public bool UseSpinWait
    {
        get => _useSpinWait;
        set => _useSpinWait = value;
    }

    /// <summary>
    /// Gets or sets the number of SpinWait iterations when UseSpinWait is enabled.
    /// </summary>
    public int SpinWaitIterations
    {
        get => _spinWaitIterations;
        set => _spinWaitIterations = Math.Max(1, Math.Min(100, value));
    }

    /// <summary>
    /// Gets or sets whether jitter compensation is enabled.
    /// </summary>
    public bool JitterCompensationEnabled
    {
        get => _jitterCompensationEnabled;
        set => _jitterCompensationEnabled = value;
    }

    /// <summary>
    /// Gets the current jitter compensation value in microseconds.
    /// </summary>
    public double CurrentJitterCompensation => _jitterCompensation;

    /// <summary>
    /// Gets the average jitter over the sample window.
    /// </summary>
    public double AverageJitter => _averageJitter;

    /// <summary>
    /// Gets the maximum observed jitter.
    /// </summary>
    public double MaxJitter => _maxJitter;

    /// <summary>
    /// Gets whether the timer is currently running.
    /// </summary>
    public bool IsRunning => _running;

    /// <summary>
    /// Gets the total number of ticks since the timer started.
    /// </summary>
    public long TotalTicks => _totalTicks;

    /// <summary>
    /// Gets the count of late ticks (ticks with jitter above threshold).
    /// </summary>
    public long LateTickCount => _lateTickCount;

    /// <summary>
    /// Gets the current elapsed time in seconds with high precision.
    /// </summary>
    public double ElapsedSeconds => _stopwatch.Elapsed.TotalSeconds;

    /// <summary>
    /// Gets the current elapsed time in microseconds with high precision.
    /// </summary>
    public double ElapsedMicroseconds => _stopwatch.Elapsed.TotalMilliseconds * 1000.0;

    /// <summary>
    /// Gets the current elapsed ticks from the high-resolution timer.
    /// </summary>
    public long ElapsedTicks => _stopwatch.ElapsedTicks;

    /// <summary>
    /// Creates a new high-resolution timer with default settings.
    /// </summary>
    public HighResolutionTimer()
    {
        _stopwatch = new Stopwatch();
        _jitterBuffer = new CircularBuffer<double>(64); // Track last 64 ticks for jitter averaging

        if (!Stopwatch.IsHighResolution)
        {
            Console.WriteLine("Warning: System does not support high-resolution timing. Precision may be reduced.");
        }
    }

    /// <summary>
    /// Creates a new high-resolution timer with specified tick interval.
    /// </summary>
    /// <param name="tickIntervalMicroseconds">Interval between ticks in microseconds.</param>
    public HighResolutionTimer(double tickIntervalMicroseconds) : this()
    {
        TickIntervalMicroseconds = tickIntervalMicroseconds;
    }

    /// <summary>
    /// Creates a new high-resolution timer with specified ticks per second.
    /// </summary>
    /// <param name="ticksPerSecond">Number of ticks per second.</param>
    /// <param name="useTicksPerSecond">Dummy parameter to differentiate constructor.</param>
    public HighResolutionTimer(double ticksPerSecond, bool useTicksPerSecond) : this()
    {
        TargetTicksPerSecond = ticksPerSecond;
    }

    /// <summary>
    /// Starts the high-resolution timer.
    /// </summary>
    public void Start()
    {
        if (_running) return;

        lock (_lock)
        {
            if (_running) return;

            // Enable high-resolution timing mode on Windows
            if (IsWindows)
            {
                try
                {
                    TimeBeginPeriod(1); // Request 1ms timer resolution
                    _highResolutionMode = true;
                }
                catch
                {
                    _highResolutionMode = false;
                }
            }

            // Reset statistics
            _totalTicks = 0;
            _lateTickCount = 0;
            _maxJitter = 0;
            _minJitter = double.MaxValue;
            _averageJitter = 0;
            _jitterCompensation = 0;
            _jitterBuffer.Clear();

            _running = true;
            _stopwatch.Restart();
            _lastTickTime = 0;

            _timerThread = new Thread(TimerLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = "HighResolutionTimer"
            };
            _timerThread.Start();
        }
    }

    /// <summary>
    /// Stops the high-resolution timer.
    /// </summary>
    public void Stop()
    {
        if (!_running) return;

        lock (_lock)
        {
            _running = false;
        }

        _timerThread?.Join(1000); // Wait up to 1 second for thread to finish
        _stopwatch.Stop();

        // Restore Windows timer resolution
        if (IsWindows && _highResolutionMode)
        {
            try
            {
                TimeEndPeriod(1);
                _highResolutionMode = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to restore Windows timer resolution: {ex.Message}");
                // Continue execution - timer cleanup is non-critical during disposal
            }
        }
    }

    /// <summary>
    /// Resets the timer statistics.
    /// </summary>
    public void ResetStatistics()
    {
        lock (_lock)
        {
            _totalTicks = 0;
            _lateTickCount = 0;
            _maxJitter = 0;
            _minJitter = double.MaxValue;
            _jitterBuffer.Clear();
        }
    }

    /// <summary>
    /// Converts seconds to ticks using the stopwatch frequency.
    /// </summary>
    public static long SecondsToTicks(double seconds)
    {
        return (long)(seconds * Frequency);
    }

    /// <summary>
    /// Converts ticks to seconds using the stopwatch frequency.
    /// </summary>
    public static double TicksToSeconds(long ticks)
    {
        return (double)ticks / Frequency;
    }

    /// <summary>
    /// Converts microseconds to ticks.
    /// </summary>
    public static long MicrosecondsToTicks(double microseconds)
    {
        return (long)(microseconds * Frequency / 1_000_000.0);
    }

    /// <summary>
    /// Converts ticks to microseconds.
    /// </summary>
    public static double TicksToMicroseconds(long ticks)
    {
        return ticks * 1_000_000.0 / Frequency;
    }

    private void TimerLoop()
    {
        double tickIntervalSeconds = _tickIntervalMicroseconds / 1_000_000.0;
        double nextTickTime = 0;
        double sleepThreshold = tickIntervalSeconds * 0.5; // Sleep if more than 50% of interval remains

        while (_running)
        {
            double currentTime = _stopwatch.Elapsed.TotalSeconds;
            double targetTime = nextTickTime;

            // Apply jitter compensation
            if (_jitterCompensationEnabled && _averageJitter > 0)
            {
                targetTime -= _jitterCompensation / 1_000_000.0;
            }

            // Wait for next tick time
            double timeUntilTick = targetTime - currentTime;

            if (timeUntilTick > 0)
            {
                if (timeUntilTick > sleepThreshold && !_useSpinWait)
                {
                    // Sleep for most of the remaining time
                    int sleepMs = (int)((timeUntilTick - sleepThreshold) * 1000);
                    if (sleepMs > 0)
                    {
                        Thread.Sleep(sleepMs);
                    }
                }

                // Spin-wait for the remaining time for precision
                while (_stopwatch.Elapsed.TotalSeconds < targetTime && _running)
                {
                    if (_useSpinWait)
                    {
                        Thread.SpinWait(_spinWaitIterations);
                    }
                    else
                    {
                        Thread.Yield();
                    }
                }
            }

            if (!_running) break;

            // Calculate actual tick time and jitter
            currentTime = _stopwatch.Elapsed.TotalSeconds;
            double jitter = (currentTime - nextTickTime) * 1_000_000.0; // Jitter in microseconds
            double deltaTime = currentTime - _lastTickTime;

            // Update statistics
            _totalTicks++;
            _jitterBuffer.Push(jitter);

            if (Math.Abs(jitter) > Math.Abs(_maxJitter))
            {
                _maxJitter = jitter;
            }
            if (Math.Abs(jitter) < Math.Abs(_minJitter))
            {
                _minJitter = jitter;
            }

            // Calculate average jitter for compensation
            _averageJitter = _jitterBuffer.Average();

            // Update jitter compensation (smooth adjustment)
            if (_jitterCompensationEnabled)
            {
                _jitterCompensation = _jitterCompensation * 0.9 + _averageJitter * 0.1;
            }

            // Detect significant jitter
            double jitterThreshold = _tickIntervalMicroseconds * 0.1; // 10% of tick interval
            if (Math.Abs(jitter) > jitterThreshold)
            {
                _lateTickCount++;
                JitterDetected?.Invoke(this, new JitterEventArgs(jitter, _averageJitter, _totalTicks));
            }

            // Fire tick event
            Tick?.Invoke(this, new TimerTickEventArgs(
                currentTime,
                deltaTime,
                _totalTicks,
                jitter,
                _averageJitter
            ));

            _lastTickTime = currentTime;
            nextTickTime += tickIntervalSeconds;

            // Prevent drift accumulation by adjusting if we fall behind
            if (currentTime > nextTickTime + tickIntervalSeconds * 2)
            {
                nextTickTime = currentTime + tickIntervalSeconds;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        GC.SuppressFinalize(this);
    }

    ~HighResolutionTimer()
    {
        Dispose();
    }
}

/// <summary>
/// Event arguments for timer tick events.
/// </summary>
public class TimerTickEventArgs : EventArgs
{
    /// <summary>Current time in seconds since timer start.</summary>
    public double CurrentTime { get; }

    /// <summary>Time since last tick in seconds.</summary>
    public double DeltaTime { get; }

    /// <summary>Total ticks since timer start.</summary>
    public long TickNumber { get; }

    /// <summary>Jitter of this tick in microseconds (positive = late, negative = early).</summary>
    public double Jitter { get; }

    /// <summary>Average jitter over recent ticks in microseconds.</summary>
    public double AverageJitter { get; }

    public TimerTickEventArgs(double currentTime, double deltaTime, long tickNumber, double jitter, double averageJitter)
    {
        CurrentTime = currentTime;
        DeltaTime = deltaTime;
        TickNumber = tickNumber;
        Jitter = jitter;
        AverageJitter = averageJitter;
    }
}

/// <summary>
/// Event arguments for jitter detection events.
/// </summary>
public class JitterEventArgs : EventArgs
{
    /// <summary>The jitter amount in microseconds.</summary>
    public double Jitter { get; }

    /// <summary>The current average jitter.</summary>
    public double AverageJitter { get; }

    /// <summary>The tick number when jitter was detected.</summary>
    public long TickNumber { get; }

    public JitterEventArgs(double jitter, double averageJitter, long tickNumber)
    {
        Jitter = jitter;
        AverageJitter = averageJitter;
        TickNumber = tickNumber;
    }
}

/// <summary>
/// Simple circular buffer for jitter averaging.
/// </summary>
internal class CircularBuffer<T> where T : struct
{
    private readonly T[] _buffer;
    private int _head;
    private int _count;

    public CircularBuffer(int capacity)
    {
        _buffer = new T[capacity];
    }

    public void Push(T value)
    {
        _buffer[_head] = value;
        _head = (_head + 1) % _buffer.Length;
        if (_count < _buffer.Length)
        {
            _count++;
        }
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
    }

    public double Average()
    {
        if (_count == 0) return 0;

        double sum = 0;
        for (int i = 0; i < _count; i++)
        {
            sum += Convert.ToDouble(_buffer[i]);
        }
        return sum / _count;
    }

    public int Count => _count;
}
