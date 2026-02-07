// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: VST3 scanner for Windows paths.

namespace MusicEngine.Vst;

/// <summary>
/// VST3 scanner for Windows search paths.
/// </summary>
public static class Vst3Scanner
{
    private const string Vst3PathsEnvVar = "MUSICENGINE_VST3_PATHS";

    private static readonly string[] DefaultPaths =
    {
        @"C:\Program Files\Common Files\VST3",
        @"C:\Program Files\VST3",
        @"C:\Program Files\Steinberg\VST3",
        @"C:\Program Files (x86)\Common Files\VST3"
    };

    /// <summary>
    /// Scan for VST3 plugins on disk.
    /// </summary>
    /// <returns>List of discovered VST3 plugins.</returns>
    public static List<Vst3PluginInfo> Scan()
    {
        var roots = GetSearchPaths();
        var results = new List<Vst3PluginInfo>();

        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var path in EnumerateVst3Paths(root))
            {
                results.Add(new Vst3PluginInfo
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    Path = path
                });
            }
        }

        return results
            .GroupBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static List<string> GetSearchPaths()
    {
        var env = Environment.GetEnvironmentVariable(Vst3PathsEnvVar);
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return DefaultPaths.ToList();
    }

    private static IEnumerable<string> EnumerateVst3Paths(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(current, "*.vst3", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (var entry in entries)
            {
                if (Directory.Exists(entry) || File.Exists(entry))
                {
                    yield return entry;
                }
            }

            IEnumerable<string> subdirs;
            try
            {
                subdirs = Directory.EnumerateDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var subdir in subdirs)
            {
                pending.Push(subdir);
            }
        }
    }
}
