namespace MusicEngine.Core.Events;

/// <summary>
/// Event arguments for channel-related events.
/// </summary>
public class ChannelEventArgs : EventArgs
{
    public int ChannelIndex { get; }
    public float Gain { get; }

    public ChannelEventArgs(int channelIndex, float gain = 1.0f)
    {
        ChannelIndex = channelIndex;
        Gain = gain;
    }
}

/// <summary>
/// Event arguments for plugin-related events.
/// </summary>
public class PluginEventArgs : EventArgs
{
    public IVstPlugin Plugin { get; }
    public string PluginName => Plugin.Name;
    public bool IsVst3 => Plugin.IsVst3;

    public PluginEventArgs(IVstPlugin plugin)
    {
        Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
    }
}

/// <summary>
/// Event arguments for MIDI routing changes.
/// </summary>
public class MidiRoutingEventArgs : EventArgs
{
    public int DeviceIndex { get; }
    public string? DeviceName { get; }
    public string? TargetName { get; }

    public MidiRoutingEventArgs(int deviceIndex, string? deviceName = null, string? targetName = null)
    {
        DeviceIndex = deviceIndex;
        DeviceName = deviceName;
        TargetName = targetName;
    }
}

/// <summary>
/// Event arguments for recording state changes (engine-level).
/// </summary>
public class EngineRecordingEventArgs : EventArgs
{
    public bool IsRecording { get; }
    public string? FilePath { get; }
    public TimeSpan Duration { get; }

    public EngineRecordingEventArgs(bool isRecording, string? filePath = null, TimeSpan duration = default)
    {
        IsRecording = isRecording;
        FilePath = filePath;
        Duration = duration;
    }
}

/// <summary>
/// Event arguments for initialization progress.
/// </summary>
public class InitializationProgressEventArgs : EventArgs
{
    public string Stage { get; }
    public int CurrentStep { get; }
    public int TotalSteps { get; }
    public double ProgressPercent => TotalSteps > 0 ? (CurrentStep * 100.0 / TotalSteps) : 0;

    public InitializationProgressEventArgs(string stage, int currentStep, int totalSteps)
    {
        Stage = stage;
        CurrentStep = currentStep;
        TotalSteps = totalSteps;
    }
}

/// <summary>
/// Progress info for async operations.
/// </summary>
public class InitializationProgress
{
    public string Stage { get; set; } = "";
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Progress info for VST scanning.
/// </summary>
public class VstScanProgressEventArgs : EventArgs
{
    public string CurrentPath { get; }
    public int ScannedCount { get; }
    public int TotalCount { get; }
    public string? CurrentPlugin { get; }

    public VstScanProgressEventArgs(string currentPath, int scannedCount, int totalCount, string? currentPlugin = null)
    {
        CurrentPath = currentPath;
        ScannedCount = scannedCount;
        TotalCount = totalCount;
        CurrentPlugin = currentPlugin;
    }
}
