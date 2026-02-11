// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Script helper for folder-based sample access.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;

namespace MusicEngine.Scripting;

internal sealed class SampleFolder : DynamicObject
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".aiff", ".aif", ".mp3", ".flac", ".ogg", ".m4a", ".wma"
    };

    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _names = new();

    /// <summary>
    /// Scan a folder for sample files and expose them by name.
    /// </summary>
    public SampleFolder(string folder, string searchPattern, bool recursive, bool audioOnly)
    {
        Folder = folder ?? string.Empty;
        SearchPattern = string.IsNullOrWhiteSpace(searchPattern) ? "*.*" : searchPattern;
        Recursive = recursive;
        AudioOnly = audioOnly;

        if (!Directory.Exists(Folder)) return;

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var file in Directory.GetFiles(Folder, SearchPattern, option))
        {
            if (audioOnly && !IsAudioFile(file)) continue;
            var name = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!_paths.ContainsKey(name))
            {
                _paths[name] = file;
                _names.Add(name);
            }

            var alias = MakeAlias(name);
            if (!_aliases.ContainsKey(alias))
            {
                _aliases[alias] = file;
            }
        }
    }

    /// <summary>
    /// Source folder path.
    /// </summary>
    public string Folder { get; }
    /// <summary>
    /// Search pattern used for file discovery.
    /// </summary>
    public string SearchPattern { get; }
    /// <summary>
    /// True to scan subfolders.
    /// </summary>
    public bool Recursive { get; }
    /// <summary>
    /// True to include only audio file extensions.
    /// </summary>
    public bool AudioOnly { get; }

    /// <summary>
    /// All discovered sample names.
    /// </summary>
    public IReadOnlyList<string> Names => _names;

    /// <summary>
    /// Get a sample path by name or alias.
    /// </summary>
    public string Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        if (_paths.TryGetValue(name, out var path)) return path;
        if (_aliases.TryGetValue(name, out path)) return path;
        var alias = MakeAlias(name);
        return _aliases.TryGetValue(alias, out path) ? path : string.Empty;
    }

    /// <summary>
    /// Indexer for sample paths by name.
    /// </summary>
    public string this[string name] => Get(name);

    /// <summary>
    /// Dynamic member access for sample names.
    /// </summary>
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        var path = Get(binder.Name);
        if (!string.IsNullOrWhiteSpace(path))
        {
            result = path;
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// List all dynamic member names for tooling.
    /// </summary>
    public override IEnumerable<string> GetDynamicMemberNames()
        => _aliases.Keys.Concat(_paths.Keys);

    private static bool IsAudioFile(string path)
    {
        var ext = Path.GetExtension(path);
        return !string.IsNullOrWhiteSpace(ext) && AudioExtensions.Contains(ext);
    }

    private static string MakeAlias(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var buffer = new char[name.Length + 1];
        int pos = 0;
        if (!char.IsLetter(name[0]) && name[0] != '_')
        {
            buffer[pos++] = '_';
        }

        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                buffer[pos++] = ch;
            }
            else
            {
                buffer[pos++] = '_';
            }
        }

        return new string(buffer, 0, pos);
    }
}
