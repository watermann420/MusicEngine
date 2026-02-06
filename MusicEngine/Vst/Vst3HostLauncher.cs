// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Launches an external VST3 host to open plugin editors.

using System.Diagnostics;

namespace MusicEngine.Vst;

public static class Vst3HostLauncher
{
    private const string HostEnvVar = "MUSICENGINE_VST3_HOST";

    private static readonly string[] DefaultHostPaths =
    {
        @"C:\Program Files\VST3 SDK\bin\vst3_host.exe",
        @"C:\Program Files\VST3 SDK\bin\vst3host.exe",
        @"C:\Program Files\Steinberg\VST3 Plugin Test Host\vst3_host.exe",
        @"C:\Program Files\Steinberg\VST3 Plugin Test Host\vst3host.exe",
        @"C:\Program Files\Steinberg\VST3 Host\vst3_host.exe"
    };

    public static bool TryOpenPlugin(string pluginPath, out string message)
    {
        var hostPath = ResolveHostPath();
        if (string.IsNullOrWhiteSpace(hostPath))
        {
            message = "VST3 host not found. Set MUSICENGINE_VST3_HOST to your vst3_host.exe path.";
            return false;
        }

        if (!File.Exists(pluginPath))
        {
            message = $"Plugin not found: {pluginPath}";
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = hostPath,
            Arguments = $"\"{pluginPath}\"",
            UseShellExecute = false
        };

        try
        {
            Process.Start(startInfo);
            message = $"Opened VST3 host: {Path.GetFileName(hostPath)}";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Failed to launch VST3 host: {ex.Message}";
            return false;
        }
    }

    private static string? ResolveHostPath()
    {
        var env = Environment.GetEnvironmentVariable(HostEnvVar);
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
        {
            return env;
        }

        foreach (var path in DefaultHostPaths)
        {
            if (File.Exists(path)) return path;
        }

        return null;
    }
}
