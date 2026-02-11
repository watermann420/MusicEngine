// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Script host for test_script.cs.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using MusicEngine.Core;
using MusicEngine.Core.Modulation;
using MusicEngine.Effects.Audio;
using MusicEngine.Effects.Midi;
using MusicEngine.Effects.Vst;
using MusicEngine.Instruments;
using MusicEngine.Scripting.FluentApi;
using MusicEngine.Vst;
using MusicEngine.Timing;

namespace MusicEngine.Scripting;

/// <summary>
/// Script host for executing C# scripts against the engine.
/// </summary>
public sealed class ScriptHost
{
    private const string ScriptsFolderName = "Test Project";
    private const string LegacyScriptsFolderName = "Scripts";
    /// <summary>
    /// When true, VST instances are disposed when clearing script state.
    /// </summary>
    public bool DisposeVstOnClear { get; set; } = false;
    private VstAccess? _vstAccessCache;
    private readonly AudioEngine _engine;
    private readonly Sequencer _sequencer;
    private readonly Vst3Registry? _vstRegistry;
    private readonly HashSet<ISynth> _activeSynths = new();
    private ScriptGlobals? _globalsCache;
    private readonly ScriptOptions _options;
    private readonly object _compileLock = new();
    private readonly object _stateRewriteLock = new();
    private Script<object>? _compiledScript;
    private string? _compiledCode;
    private string? _compiledFilePath;
    private string? _lastExecutedCode;
    private readonly string? _scriptFilePath;
    private readonly Dictionary<string, string> _moduleCodeCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ScriptLibrary _library;
    private readonly HashSet<string> _masterScripts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _masterScriptOrder = new();
    private readonly Dictionary<string, string> _scriptAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VstBinding> _vstBindings = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string>? _modulesExecutedThisRun;
    private string? _currentScriptFilePath;
    private Assembly? _libraryAssembly;
    private bool _libraryAssemblyLoaded;

    /// <summary>
    /// Create a script host bound to an engine and sequencer.
    /// </summary>
    /// <param name="engine">Audio engine instance.</param>
    /// <param name="sequencer">Sequencer instance.</param>
    /// <param name="vstRegistry">Optional VST registry.</param>
    public ScriptHost(AudioEngine engine, Sequencer sequencer, Vst3Registry? vstRegistry = null,
        string? scriptFilePath = null)
    {
        _engine = engine;
        _sequencer = sequencer;
        _vstRegistry = vstRegistry;
        _scriptFilePath = scriptFilePath;
        _library = new ScriptLibrary(this);
        _options = ScriptOptions.Default
            .WithReferences(typeof(AudioEngine).Assembly, typeof(NAudio.Wave.ISampleProvider).Assembly)
            .WithImports("System", "MusicEngine.Core", "MusicEngine.Instruments", "MusicEngine.Instruments.Modules",
                "MusicEngine.Vst", "MusicEngine.Effects.Audio", "MusicEngine.Effects.Midi",
                "MusicEngine.Effects.Vst", "MusicEngine.Effects.Modulation", "MusicEngine.Core.Modulation",
                "MusicEngine.Timing",
                "System.Collections.Generic");
        if (TryLoadLibraryAssembly(out var libraryAssembly))
        {
            _options = _options.WithReferences(new[] { libraryAssembly });
        }
        if (!string.IsNullOrWhiteSpace(_scriptFilePath))
        {
            _options = _options.WithFilePath(_scriptFilePath);
        }
    }

    /// <summary>
    /// Execute script code without clearing state.
    /// </summary>
    public async Task ExecuteScriptAsync(string code)
    {
        await RunScriptAsync(code, skipIfUnchanged: false, clearState: false, filePath: _scriptFilePath,
            cacheKey: null);
    }

    /// <summary>
    /// Execute script code only if it has changed.
    /// </summary>
    public async Task<bool> ExecuteScriptIfChangedAsync(string code)
    {
        return await RunScriptAsync(code, skipIfUnchanged: true, clearState: false, filePath: _scriptFilePath,
            cacheKey: null);
    }

    /// <summary>
    /// Clear state then execute script code.
    /// </summary>
    public async Task<bool> RefreshScriptAsync(string code, bool skipIfUnchanged = true)
    {
        ClearState();
        _modulesExecutedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await RunMasterScriptsAsync();
            return await RunScriptAsync(code, skipIfUnchanged: false, clearState: false, filePath: _scriptFilePath,
                cacheKey: null);
        }
        finally
        {
            _modulesExecutedThisRun = null;
        }
    }

    /// <summary>
    /// Clear state then execute the configured script file, if available.
    /// </summary>
    public async Task<bool> RefreshScriptFromFileAsync(bool skipIfUnchanged = true)
    {
        if (string.IsNullOrWhiteSpace(_scriptFilePath) || !File.Exists(_scriptFilePath))
        {
            return false;
        }

        var code = await File.ReadAllTextAsync(_scriptFilePath);
        return await RefreshScriptAsync(code, skipIfUnchanged);
    }

    public async Task<bool> RefreshMainScriptsAsync()
    {
        ClearState();
        _modulesExecutedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var mainScripts = FindMainScripts();
            if (mainScripts.Count == 0)
            {
                Console.WriteLine("No main file active.");
                return false;
            }

            foreach (var script in mainScripts)
            {
                await ExecuteModuleAsync(script, skipIfUnchanged: false);
            }
            return true;
        }
        finally
        {
            _modulesExecutedThisRun = null;
        }
    }


    internal async Task<bool> ExecuteModuleAsync(string scriptName, bool skipIfUnchanged = true)
    {
        if (string.IsNullOrWhiteSpace(scriptName))
        {
            Console.WriteLine("Script Error: module name is empty.");
            return false;
        }

        if (_modulesExecutedThisRun != null && _modulesExecutedThisRun.Contains(scriptName))
        {
            return false;
        }

        var path = ResolveScriptPath(scriptName);
        if (path == null)
        {
            Console.WriteLine($"Script Error: module not found: {scriptName}");
            return false;
        }

        var code = File.ReadAllText(path);
        var executed = await RunScriptAsync(code, skipIfUnchanged, clearState: false, filePath: path, cacheKey: path);
        if (_modulesExecutedThisRun != null && executed)
        {
            _modulesExecutedThisRun.Add(scriptName);
        }
        return executed;
    }

    private async Task<bool> RunScriptAsync(string code, bool skipIfUnchanged, bool clearState, string? filePath,
        string? cacheKey)
    {
        var rawCode = code;
        code = PreprocessCode(code);
        if (skipIfUnchanged)
        {
            if (cacheKey == null && string.Equals(_lastExecutedCode, code, StringComparison.Ordinal))
            {
                return false;
            }

            if (cacheKey != null && _moduleCodeCache.TryGetValue(cacheKey, out var cached) &&
                string.Equals(cached, code, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (clearState)
        {
            ClearState();
        }

        var globals = GetOrCreateGlobals();
        var previousFilePath = _currentScriptFilePath;
        var previousGlobalsPath = globals.ScriptFilePath;
        _currentScriptFilePath = filePath;
        globals.ScriptFilePath = filePath;
        var script = GetOrCompile(code, filePath);

        try
        {
            _globalsCache?.vst.BeginScriptRun();
            await script.RunAsync(globals);
            if (cacheKey == null)
            {
                _lastExecutedCode = code;
            }
            else
            {
                _moduleCodeCache[cacheKey] = code;
            }
            UpdateVstBindings(rawCode);
            _globalsCache?.vst.PruneUnusedStates();
            TryUpdateScriptStateSnapshots();
            return true;
        }
        catch (Exception ex)
        {
            if (ex is CompilationErrorException compilationError)
            {
                LogCompilationErrors(compilationError);
            }
            else
            {
                LogRuntimeError(ex);
            }
            return false;
        }
        finally
        {
            _currentScriptFilePath = previousFilePath;
            globals.ScriptFilePath = previousGlobalsPath;
        }
    }

    private ScriptGlobals GetOrCreateGlobals()
    {
        if (_globalsCache != null) return _globalsCache;

        var globals = new ScriptGlobals
        {
            Engine = _engine,
            Sequencer = _sequencer,
            Host = this,
            VstRegistry = _vstRegistry,
            ScriptFilePath = _scriptFilePath
        };
        globals.SetLibrary(_library);
        if (_vstAccessCache != null)
        {
            _vstAccessCache.UpdateGlobals(globals);
            globals.SetVstAccess(_vstAccessCache);
        }
        _globalsCache = globals;
        return globals;
    }

    private Script<object> GetOrCompile(string code, string? filePath)
    {
        lock (_compileLock)
        {
            if (_compiledScript != null && string.Equals(_compiledCode, code, StringComparison.Ordinal) &&
                string.Equals(_compiledFilePath, filePath, StringComparison.Ordinal))
            {
                return _compiledScript;
            }

            var options = _options;
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                options = options.WithFilePath(filePath);
            }
            _compiledScript = CSharpScript.Create(code, options, typeof(ScriptGlobals));
            _compiledCode = code;
            _compiledFilePath = filePath;
            return _compiledScript;
        }
    }

    internal string? CurrentScriptFilePath => _currentScriptFilePath;

    internal string? GetAliasForScript(string scriptName)
    {
        if (string.IsNullOrWhiteSpace(scriptName)) return null;
        return _scriptAliases.TryGetValue(scriptName, out var alias) ? alias : null;
    }

    internal bool TryResolveVstInstrument(string variableName, out IVstInstrument instrument)
    {
        instrument = null!;
        if (string.IsNullOrWhiteSpace(variableName)) return false;
        if (_globalsCache == null) return false;
        if (!_vstBindings.TryGetValue(variableName, out var binding)) return false;
        if (binding.IsEffect) return false;
        return _globalsCache.vst.TryGetInstrument(binding.PluginName, out instrument);
    }

    internal void RegisterScriptAlias(string alias, string scriptName)
    {
        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(scriptName)) return;
        _scriptAliases[scriptName] = alias;
    }

    internal void RegisterMasterScript(string scriptName)
    {
        if (string.IsNullOrWhiteSpace(scriptName)) return;
        if (_masterScripts.Add(scriptName))
        {
            _masterScriptOrder.Add(scriptName);
        }
    }

    private async Task RunMasterScriptsAsync()
    {
        if (_masterScriptOrder.Count == 0) return;
        var mainName = string.IsNullOrWhiteSpace(_scriptFilePath)
            ? null
            : Path.GetFileNameWithoutExtension(_scriptFilePath);
        foreach (var script in _masterScriptOrder)
        {
            if (!string.IsNullOrWhiteSpace(mainName) &&
                string.Equals(mainName, script, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            await ExecuteModuleAsync(script, skipIfUnchanged: false);
        }
    }

    private string? ResolveScriptsDirectory()
    {
        string? baseDir;
        if (!string.IsNullOrWhiteSpace(_scriptFilePath))
        {
            var scriptDir = Path.GetDirectoryName(_scriptFilePath);
            if (!string.IsNullOrWhiteSpace(scriptDir) &&
                (string.Equals(Path.GetFileName(scriptDir), ScriptsFolderName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Path.GetFileName(scriptDir), LegacyScriptsFolderName, StringComparison.OrdinalIgnoreCase)))
            {
                baseDir = Path.GetDirectoryName(scriptDir);
            }
            else
            {
                baseDir = scriptDir;
            }
        }
        else
        {
            baseDir = AppContext.BaseDirectory;
        }
        if (string.IsNullOrWhiteSpace(baseDir)) return null;

        var scriptsDir = Path.Combine(baseDir, ScriptsFolderName);
        if (Directory.Exists(scriptsDir)) return scriptsDir;

        var legacyDir = Path.Combine(baseDir, LegacyScriptsFolderName);
        if (Directory.Exists(legacyDir)) return legacyDir;

        return baseDir;
    }

    private List<string> FindMainScripts()
    {
        var scriptsDir = ResolveScriptsDirectory();
        if (string.IsNullOrWhiteSpace(scriptsDir) || !Directory.Exists(scriptsDir))
        {
            return new List<string>();
        }

        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = Directory.GetFiles(scriptsDir, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".csx", StringComparison.OrdinalIgnoreCase));
        foreach (var file in files)
        {
            var code = File.ReadAllText(file);
            foreach (Match match in MainFileRegex.Matches(code))
            {
                if (match.Groups.Count < 2) continue;
                var name = match.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(name) && match.Groups.Count > 2)
                {
                    name = match.Groups[2].Value.Trim();
                }
                if (!string.IsNullOrWhiteSpace(name))
                {
                    results.Add(name);
                }
            }

            if (MainFileBuilderRegex.IsMatch(code) || MainFileCallRegex.IsMatch(code))
            {
                results.Add(Path.GetFileNameWithoutExtension(file));
            }
        }

        return results.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
    }


    private static readonly Regex FileTwoArgsRegex =
        new(@"\bFile\s*\(\s*([A-Za-z_]\w*)\s*,\s*([A-Za-z_]\w*)\s*\)", RegexOptions.Compiled);
    private static readonly Regex FileOneArgRegex =
        new(@"\bFile\s*\(\s*([A-Za-z_]\w*)\s*\)", RegexOptions.Compiled);
    private static readonly Regex MainFileRegex =
        new(@"\bFile\s*\(\s*(?:Main|\""Main\"")\s*,\s*(?:\""([^\""]+)\""|([A-Za-z_]\w*))\s*\)",
            RegexOptions.Compiled);
    private static readonly Regex MainFileBuilderRegex =
        new(@"\bFile\s*\.Main\s*\(\s*\)\s*\.Name\s*\(\s*(?:\""([^\""]+)\""|([A-Za-z_]\w*))\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MainFileCallRegex =
        new(@"\bFile\s*\.Main\s*\(\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private string PreprocessCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return code;
        code = FileTwoArgsRegex.Replace(code, "File(\"$1\", \"$2\")");
        code = FileOneArgRegex.Replace(code, "File(\"$1\")");
        code = PreprocessFileNameCalls(code);
        code = PreprocessVstAliasCalls(code);
        code = PreprocessNoteNameCalls(code);
        code = PreprocessFriendlyNoteArgs(code);
        code = PreprocessFallbackCalls(code);
        code = PreprocessPatternFallbackCalls(code);
        code = PreprocessIncludeCalls(code);
        return code;
    }

    private string PreprocessFileNameCalls(string code)
    {
        var scriptsDir = ResolveScriptsDirectory();
        if (!string.IsNullOrWhiteSpace(scriptsDir) && Directory.Exists(scriptsDir))
        {
            foreach (var path in Directory.GetFiles(scriptsDir, "*.*", SearchOption.TopDirectoryOnly)
                         .Where(file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                                        file.EndsWith(".csx", StringComparison.OrdinalIgnoreCase)))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(name)) continue;
                code = Regex.Replace(code, $@"\bFile\.Main\s*\(\s*\.Name\(\s*{Regex.Escape(name)}\s*\)\s*\)",
                    $"File.Main().Name(\"{name}\")");
                code = Regex.Replace(code, $@"\bFile\s*\(\s*\.Name\(\s*{Regex.Escape(name)}\s*\)\s*\)",
                    $"File.Name(\"{name}\")");
                code = Regex.Replace(code, $@"\bFile\.Main\s*\(\s*\)\s*\.Name\(\s*{Regex.Escape(name)}\s*\)",
                    $"File.Main().Name(\"{name}\")");
                code = Regex.Replace(code, $@"\bFile\s*\.Name\(\s*{Regex.Escape(name)}\s*\)",
                    $"File.Name(\"{name}\")");
                code = Regex.Replace(code, $@"\bfile\s*\.Name\(\s*{Regex.Escape(name)}\s*\)",
                    $"file.Name(\"{name}\")");
                code = Regex.Replace(code, $@"\bfile\s*\.NameSpace\(\s*{Regex.Escape(name)}\s*\)",
                    $"file.NameSpace(\"{name}\")");
                code = Regex.Replace(code, $@"\bFile\s*\.NameSpace\(\s*{Regex.Escape(name)}\s*\)",
                    $"File.NameSpace(\"{name}\")");
            }
        }

        code = Regex.Replace(code, @"\bFile\s*\.Main\s*\(\s*\)\s*\.Name\(\s*([A-Za-z_]\w*)\s*\)",
            "File.Main().Name(\"$1\")");
        code = Regex.Replace(code, @"\bFile\s*\.Name\(\s*([A-Za-z_]\w*)\s*\)",
            "File.Name(\"$1\")");
        code = Regex.Replace(code, @"\bfile\s*\.Name\(\s*([A-Za-z_]\w*)\s*\)",
            "file.Name(\"$1\")");

        return code;
    }

    private static string PreprocessVstAliasCalls(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return code;

        const string callPattern =
            @"(?:[A-Za-z_]\w*\.)*(?:CreateVstEffect|VstEffect|VstFx|CreateVst|Vst)";
        var regex = new Regex(
            $@"\bvar\s+(?<var>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<call>{callPattern})\s*\(\s*""(?<name>[^""]+)""\s*\)",
            RegexOptions.IgnoreCase);

        return regex.Replace(code, @"var ${var} = ${call}(""${name}"", ""${var}"")");
    }

    private static string PreprocessNoteNameCalls(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return code;

        var regex = new Regex(@"\bNote(?:Ms)?\s*\(\s*(?<note>[A-Ga-g])(?<accidental>[bBsS]?)(?<octave>-?\d+)",
            RegexOptions.Compiled);

        return regex.Replace(code, match =>
        {
            var noteChar = char.ToUpperInvariant(match.Groups["note"].Value[0]);
            var accidental = match.Groups["accidental"].Value;
            var octaveText = match.Groups["octave"].Value;
            if (!int.TryParse(octaveText, out var octave))
            {
                return match.Value;
            }

            int baseNote = noteChar switch
            {
                'C' => 0,
                'D' => 2,
                'E' => 4,
                'F' => 5,
                'G' => 7,
                'A' => 9,
                'B' => 11,
                _ => 0
            };

            int offset = 0;
            if (!string.IsNullOrWhiteSpace(accidental))
            {
                var acc = char.ToUpperInvariant(accidental[0]);
                if (acc == 'B')
                {
                    offset = -1;
                }
                else if (acc == 'S')
                {
                    offset = 1;
                }
            }

            var midi = (octave + 1) * 12 + baseNote + offset;
            return match.Value.Replace(match.Groups["note"].Value + accidental + octaveText, midi.ToString());
        });
    }

    private static string PreprocessFriendlyNoteArgs(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return code;

        code = Regex.Replace(
            code,
            @"\bNoteMs\s*\((?<args>[^\)]*)\)",
            match =>
            {
                var args = match.Groups["args"].Value;
                args = ExpandNoteShorthand(args);
                args = Regex.Replace(
                    args,
                    @"(^|,)\s*time\s+(?<value>-?\d+(?:\.\d+)?)",
                    "$1 timeMs: ${value}",
                    RegexOptions.IgnoreCase);
                args = Regex.Replace(
                    args,
                    @"(^|,)\s*duration\s+(?<value>-?\d+(?:\.\d+)?)",
                    "$1 durationMs: ${value}",
                    RegexOptions.IgnoreCase);
                return $"NoteMs({args})";
            },
            RegexOptions.IgnoreCase);

        code = Regex.Replace(
            code,
            @"\bNote\s*\((?<args>[^\)]*)\)",
            match =>
            {
                var args = match.Groups["args"].Value;
                args = ExpandNoteShorthand(args);
                return $"Note({args})";
            },
            RegexOptions.IgnoreCase);

        code = Regex.Replace(
            code,
            @"(?<=\()\s*Note\s+(?<value>-?\d+(?:\.\d+)?)",
            "note: ${value}",
            RegexOptions.IgnoreCase);

        code = Regex.Replace(
            code,
            @"(?<=\(|,)\s*(?<name>note|beat|duration|speed|velocity|slideto|slidetime|slidetimems)\s+(?<value>-?\d+(?:\.\d+)?)",
            match =>
            {
                var name = match.Groups["name"].Value;
                var value = match.Groups["value"].Value;
                string mapped = name.ToLowerInvariant() switch
                {
                    "speed" => "velocity",
                    "slideto" => "slideTo",
                    "slidetime" => "slideTimeMs",
                    "slidetimems" => "slideTimeMs",
                    _ => name
                };
                return $"{mapped}: {value}";
            },
            RegexOptions.IgnoreCase);

        return code;
    }

    private static string PreprocessIncludeCalls(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return code;

        return Regex.Replace(
            code,
            @"\binclude\s+(?<name>[A-Za-z_]\w*)\s*;",
            "Use(\"${name}\", true).GetAwaiter().GetResult();",
            RegexOptions.IgnoreCase);
    }

    private static string PreprocessFallbackCalls(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return code;

        var sb = new System.Text.StringBuilder(code.Length);
        int index = 0;
        while (index < code.Length)
        {
            int callStart = FindToCall(code, index);
            if (callStart < 0)
            {
                sb.Append(code, index, code.Length - index);
                break;
            }

            sb.Append(code, index, callStart - index);

            int parenStart = FindToParen(code, callStart);
            if (parenStart < 0)
            {
                sb.Append(code, callStart, code.Length - callStart);
                break;
            }

            int parenEnd = FindMatchingParen(code, parenStart);
            if (parenEnd < 0)
            {
                sb.Append(code, callStart, code.Length - callStart);
                break;
            }

            sb.Append(code, callStart, parenStart - callStart + 1);

            string args = code.Substring(parenStart + 1, parenEnd - parenStart - 1);
            if (!TryBuildFallbackArgs(args, out var rewritten))
            {
                sb.Append(args);
            }
            else
            {
                sb.Append(rewritten);
            }

            sb.Append(')');
            index = parenEnd + 1;
        }

        return sb.ToString();
    }

    private static string PreprocessPatternFallbackCalls(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return code;
        return ProcessNamedFallbackCall(code, "CreatePattern");
    }

    private static string ProcessNamedFallbackCall(string code, string name)
    {
        var sb = new System.Text.StringBuilder(code.Length);
        int index = 0;
        while (index < code.Length)
        {
            int callStart = FindNamedCall(code, name, index);
            if (callStart < 0)
            {
                sb.Append(code, index, code.Length - index);
                break;
            }

            sb.Append(code, index, callStart - index);

            int parenStart = FindCallParen(code, callStart + name.Length);
            if (parenStart < 0)
            {
                sb.Append(code, callStart, code.Length - callStart);
                break;
            }

            int parenEnd = FindMatchingParen(code, parenStart);
            if (parenEnd < 0)
            {
                sb.Append(code, callStart, code.Length - callStart);
                break;
            }

            sb.Append(code, callStart, parenStart - callStart + 1);

            string args = code.Substring(parenStart + 1, parenEnd - parenStart - 1);
            if (!TryBuildFallbackArgs(args, out var rewritten))
            {
                sb.Append(args);
            }
            else
            {
                sb.Append(rewritten);
            }

            sb.Append(')');
            index = parenEnd + 1;
        }

        return sb.ToString();
    }

    private static int FindNamedCall(string code, string name, int start)
    {
        int index = start;
        while (index < code.Length)
        {
            int found = code.IndexOf(name, index, StringComparison.OrdinalIgnoreCase);
            if (found < 0) return -1;
            bool boundaryBefore = found == 0 || !(char.IsLetterOrDigit(code[found - 1]) || code[found - 1] == '_');
            int after = found + name.Length;
            bool boundaryAfter = after >= code.Length || !(char.IsLetterOrDigit(code[after]) || code[after] == '_');
            if (boundaryBefore && boundaryAfter)
            {
                return found;
            }
            index = found + name.Length;
        }
        return -1;
    }

    private static int FindToCall(string code, int start)
    {
        for (int i = start; i < code.Length - 2; i++)
        {
            if (code[i] != '.') continue;
            char t = code[i + 1];
            char o = code[i + 2];
            if (char.ToLowerInvariant(t) != 't' || char.ToLowerInvariant(o) != 'o') continue;

            int j = i + 3;
            while (j < code.Length && char.IsWhiteSpace(code[j]))
            {
                j++;
            }
            if (j < code.Length && code[j] == '(')
            {
                return i;
            }
        }
        return -1;
    }

    private static int FindToParen(string code, int callStart)
    {
        int i = callStart + 3;
        while (i < code.Length && char.IsWhiteSpace(code[i]))
        {
            i++;
        }
        return i < code.Length && code[i] == '(' ? i : -1;
    }

    private static int FindCallParen(string code, int start)
    {
        int i = start;
        while (i < code.Length && char.IsWhiteSpace(code[i]))
        {
            i++;
        }
        return i < code.Length && code[i] == '(' ? i : -1;
    }

    private static int FindMatchingParen(string code, int openIndex)
    {
        int depth = 0;
        for (int i = openIndex; i < code.Length; i++)
        {
            char c = code[i];
            if (c == '(') depth++;
            else if (c == ')')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static bool TryBuildFallbackArgs(string args, out string rewritten)
    {
        rewritten = args;
        if (string.IsNullOrWhiteSpace(args)) return false;

        var parts = SplitArgs(args);
        if (parts.Count == 0) return false;

        bool hasMarkers = false;
        var priority = new List<string>();
        var primaryList = new List<string>();
        var fallback = new List<string>();

        foreach (var raw in parts)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0) continue;

            char marker = trimmed[0];
            if (marker == '<' || marker == '>')
            {
                hasMarkers = true;
                var expr = trimmed.Substring(1).Trim();
                if (expr.Length == 0) continue;
                if (marker == '>')
                {
                    priority.Add(expr);
                }
                else
                {
                    fallback.Add(expr);
                }
            }
            else
            {
                primaryList.Add(trimmed);
            }
        }

        if (!hasMarkers) return false;

        var ordered = new List<string>();
        ordered.AddRange(priority);
        ordered.AddRange(primaryList);
        ordered.AddRange(fallback);

        if (ordered.Count <= 1) return false;

        var output = new System.Text.StringBuilder();
        output.Append(ordered[0]);
        for (int i = 1; i < ordered.Count; i++)
        {
            output.Append(", Fallback(");
            output.Append(ordered[i]);
            output.Append(')');
        }

        rewritten = output.ToString();
        return true;
    }

    private static List<string> SplitArgs(string args)
    {
        var parts = new List<string>();
        int depthParen = 0;
        int depthBracket = 0;
        int depthBrace = 0;
        int start = 0;
        for (int i = 0; i < args.Length; i++)
        {
            char c = args[i];
            switch (c)
            {
                case '(':
                    depthParen++;
                    break;
                case ')':
                    depthParen = Math.Max(0, depthParen - 1);
                    break;
                case '[':
                    depthBracket++;
                    break;
                case ']':
                    depthBracket = Math.Max(0, depthBracket - 1);
                    break;
                case '{':
                    depthBrace++;
                    break;
                case '}':
                    depthBrace = Math.Max(0, depthBrace - 1);
                    break;
                case ',':
                    if (depthParen == 0 && depthBracket == 0 && depthBrace == 0)
                    {
                        parts.Add(args.Substring(start, i - start));
                        start = i + 1;
                    }
                    break;
            }
        }

        if (start <= args.Length)
        {
            parts.Add(args.Substring(start));
        }
        return parts;
    }

    private static string ExpandNoteShorthand(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return args;

        args = Regex.Replace(
            args,
            @"(?<=^|,)\s*(?<token>Note|N)\s*(?<value>-?\d+(?:\.\d+)?)\b",
            " note ${value}",
            RegexOptions.IgnoreCase);

        args = Regex.Replace(
            args,
            @"(?<=^|,)\s*(?<token>Note|N)\s*(?<name>[A-Ga-g][bBsS]?-?\d+)\b",
            " note ${name}",
            RegexOptions.IgnoreCase);

        args = Regex.Replace(
            args,
            @"(?<=^|,)\s*(?<token>Note|N)(?<value>-?\d+(?:\.\d+)?)\b",
            " note ${value}",
            RegexOptions.IgnoreCase);

        args = Regex.Replace(
            args,
            @"(?<=^|,)\s*(?<token>Note|N)(?<name>[A-Ga-g][bBsS]?-?\d+)\b",
            " note ${name}",
            RegexOptions.IgnoreCase);

        args = Regex.Replace(
            args,
            @"(?<=^|,)\s*(?<name>vel|velocity|speed|len|length|dur|duration|start|beat|time)\s+(?<value>-?\d+(?:\.\d+)?)",
            match =>
            {
                var name = match.Groups["name"].Value;
                var value = match.Groups["value"].Value;
                string mapped = name.ToLowerInvariant() switch
                {
                    "vel" => "velocity",
                    "speed" => "velocity",
                    "len" => "duration",
                    "length" => "duration",
                    "dur" => "duration",
                    "start" => "beat",
                    _ => name
                };
                return $" {mapped} {value}";
            },
            RegexOptions.IgnoreCase);

        return args;
    }

    /// <summary>
    /// Clear script state, routing, and mappings.
    /// </summary>
    public void ClearState()
    {
        bool resumeOutput = _engine.TrySuspendOutput();
        _sequencer.ClearPatterns();
        _engine.ClearMappings();
        _engine.ClearMixer();
        _activeSynths.Clear();
        _moduleCodeCache.Clear();
        _vstBindings.Clear();
        _library.Clear();
        if (_globalsCache != null)
        {
            if (DisposeVstOnClear)
            {
                _globalsCache.vst.KeepInstances = false;
                _globalsCache.vst.Clear();
                _vstAccessCache = null;
            }
            else
            {
                _globalsCache.vst.KeepInstances = true;
                _vstAccessCache = _globalsCache.VstAccessInstance;
            }
            _globalsCache = null;
        }
        if (resumeOutput)
        {
            _engine.ResumeOutput();
        }
    }

    private void TryUpdateScriptStateSnapshots(bool force = false)
    {
        if (string.IsNullOrWhiteSpace(_scriptFilePath)) return;
        if (!File.Exists(_scriptFilePath)) return;
        if (_globalsCache == null) return;

        if (!Monitor.TryEnter(_stateRewriteLock))
        {
            return;
        }

        try
        {
            var code = File.ReadAllText(_scriptFilePath);
            if (string.IsNullOrWhiteSpace(code)) return;

            var bindings = ParseVstBindings(code);
            if (bindings.Count == 0) return;

            var updated = code;
            bool changed = false;

            foreach (var binding in bindings)
            {
                var state = GetStateBase64(binding);
                if (string.IsNullOrWhiteSpace(state)) continue;

                var replaced = ReplaceStateCall(updated, binding.Variable, state);
                if (!ReferenceEquals(replaced, updated))
                {
                    updated = replaced;
                    changed = true;
                }
            }

            if (!changed) return;
            if (!force && string.Equals(code, updated, StringComparison.Ordinal)) return;

            File.WriteAllText(_scriptFilePath, updated);
        }
        catch
        {
        }
        finally
        {
            Monitor.Exit(_stateRewriteLock);
        }
    }

    private string? GetStateBase64(VstBinding binding)
    {
        if (_globalsCache == null) return null;
        var vstAccess = _globalsCache.VstAccessInstance;
        if (vstAccess == null) return null;

        if (binding.IsEffect)
        {
            if (vstAccess.TryGetEffect(binding.PluginName, out var effect))
            {
                return effect.State();
            }
            return null;
        }

        if (vstAccess.TryGetInstrument(binding.PluginName, out var instrument))
        {
            return instrument.State();
        }

        return null;
    }

    private static string ReplaceStateCall(string code, string variable, string base64)
    {
        if (string.IsNullOrWhiteSpace(variable)) return code;
        if (string.IsNullOrWhiteSpace(base64)) return code;

        var pattern = $@"\b{Regex.Escape(variable)}\s*\.\s*State\s*\(\s*[^)]*\)";
        var replacement = $"{variable}.State(\"{base64}\")";

        var replaced = Regex.Replace(code, pattern, replacement, RegexOptions.IgnoreCase);
        return string.Equals(replaced, code, StringComparison.Ordinal) ? code : replaced;
    }

    private static List<VstBinding> ParseVstBindings(string code)
    {
        var bindings = new List<VstBinding>();
        if (string.IsNullOrWhiteSpace(code)) return bindings;

        var pattern =
            @"\bvar\s+(?<var>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<call>[A-Za-z0-9_\.]+)\s*\(\s*""(?<name>[^""]+)""\s*(?:,\s*[^)]*)?\)";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);
        var matches = regex.Matches(code);
        foreach (Match match in matches)
        {
            if (!match.Success) continue;
            var variable = match.Groups["var"].Value;
            var call = match.Groups["call"].Value;
            var name = match.Groups["name"].Value;
            if (string.IsNullOrWhiteSpace(variable) || string.IsNullOrWhiteSpace(call) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var isEffect = call.EndsWith("CreateVstEffect", StringComparison.OrdinalIgnoreCase) ||
                call.EndsWith("VstEffect", StringComparison.OrdinalIgnoreCase) ||
                call.EndsWith("VstFx", StringComparison.OrdinalIgnoreCase);
            if (!isEffect && !call.EndsWith("CreateVst", StringComparison.OrdinalIgnoreCase) &&
                !call.EndsWith("Vst", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bindings.Add(new VstBinding(variable, name, isEffect));
        }

        return bindings;
    }

    private readonly record struct VstBinding(string Variable, string PluginName, bool IsEffect);

    private void UpdateVstBindings(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        var bindings = ParseVstBindings(code);
        if (bindings.Count == 0)
        {
            _vstBindings.Clear();
            _globalsCache?.vst.UpdateDeclaredStateKeys(Array.Empty<string>());
            return;
        }

        _vstBindings.Clear();
        var declaredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in bindings)
        {
            _vstBindings[binding.Variable] = binding;
            if (!string.IsNullOrWhiteSpace(binding.Variable))
            {
                declaredKeys.Add(binding.Variable);
            }
        }

        _globalsCache?.vst.UpdateDeclaredStateKeys(declaredKeys);
    }

    private bool TryResolveVstBinding(string name, out VstBinding binding)
    {
        if (_vstBindings.TryGetValue(name, out binding))
        {
            return true;
        }

        binding = default;
        return false;
    }

    private static bool TryOpenVstBinding(VstBinding binding, VstAccess vstAccess)
    {
        if (binding.IsEffect)
        {
            if (vstAccess.TryGetEffect(binding.PluginName, out var effect))
            {
                effect.OpenEditor();
                return true;
            }
            return false;
        }

        if (vstAccess.TryGetInstrument(binding.PluginName, out var instrument))
        {
            instrument.OpenEditor();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Try to open a VST editor by name if already loaded.
    /// </summary>
    public bool TryOpenVstEditor(string name)
    {
        if (_globalsCache == null) return false;
        if (TryResolveVstBinding(name, out var binding) &&
            TryOpenVstBinding(binding, _globalsCache.vst))
        {
            return true;
        }

        return _globalsCache.vst.TryOpenEditor(name);
    }

    /// <summary>
    /// Reset VST state in the script globals.
    /// </summary>
    public void ResetVstState()
    {
        _globalsCache?.vst.ResetState();
    }

    /// <summary>
    /// Persist VST state for all cached instances.
    /// </summary>
    public void SaveVstState()
    {
        _globalsCache?.vst.SaveAllStates();
        TryUpdateScriptStateSnapshots(force: true);
    }

    /// <summary>
    /// Mute or unmute the transport output.
    /// </summary>
    public void SetTransportMuted(bool muted)
    {
        _engine.SetTransportMuted(muted);
    }

    /// <summary>
    /// Enable or disable MIDI input.
    /// </summary>
    public void SetMidiEnabled(bool enabled)
    {
        _engine.SetMidiEnabled(enabled, sendAllNotesOff: false);
    }

    /// <summary>
    /// Start the sequencer if not running.
    /// </summary>
    public void StartSequencer()
    {
        if (!_sequencer.IsRunning)
        {
            _sequencer.Start();
        }
    }

    /// <summary>
    /// Stop the sequencer if running.
    /// </summary>
    public void StopSequencer()
    {
        if (_sequencer.IsRunning)
        {
            _sequencer.Stop();
        }
    }

    internal void RegisterSynth(ISynth synth)
    {
        if (synth == null) return;
        _activeSynths.Add(synth);
    }

    /// <summary>
    /// Send all-notes-off to active non-VST synths.
    /// </summary>
    public void AllNotesOff()
    {
        foreach (var synth in _activeSynths)
        {
            if (synth is MusicEngine.Vst.Vst3Instrument)
            {
                continue;
            }
            synth.AllNotesOff();
        }
    }

    internal string? ResolveScriptPath(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (Path.IsPathRooted(name))
        {
            if (File.Exists(name)) return name;
            if (File.Exists($"{name}.cs")) return $"{name}.cs";
            if (File.Exists($"{name}.csx")) return $"{name}.csx";
            return null;
        }

        string? baseDir;
        if (!string.IsNullOrWhiteSpace(_scriptFilePath))
        {
            var scriptDir = Path.GetDirectoryName(_scriptFilePath);
            if (!string.IsNullOrWhiteSpace(scriptDir) &&
                (string.Equals(Path.GetFileName(scriptDir), ScriptsFolderName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Path.GetFileName(scriptDir), LegacyScriptsFolderName, StringComparison.OrdinalIgnoreCase)))
            {
                baseDir = Path.GetDirectoryName(scriptDir);
            }
            else
            {
                baseDir = scriptDir;
            }
        }
        else
        {
            baseDir = AppContext.BaseDirectory;
        }
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            return null;
        }

        var scriptsDir = Path.Combine(baseDir, ScriptsFolderName);
        var legacyScriptsDir = Path.Combine(baseDir, LegacyScriptsFolderName);
        var candidates = new[]
        {
            name,
            $"{name}.cs",
            $"{name}.csx"
        };

        foreach (var candidate in candidates)
        {
            var scriptPath = Path.Combine(scriptsDir, candidate);
            if (File.Exists(scriptPath))
            {
                return scriptPath;
            }
        }

        foreach (var candidate in candidates)
        {
            var scriptPath = Path.Combine(legacyScriptsDir, candidate);
            if (File.Exists(scriptPath))
            {
                return scriptPath;
            }
        }

        foreach (var candidate in candidates)
        {
            var scriptPath = Path.Combine(baseDir, candidate);
            if (File.Exists(scriptPath))
            {
                return scriptPath;
            }
        }

        return null;
    }

    private void LogCompilationErrors(CompilationErrorException error)
    {
        Console.WriteLine("Script Error: compilation failed.");
        foreach (var diagnostic in error.Diagnostics)
        {
            var lineSpan = diagnostic.Location.GetLineSpan();
            var line = lineSpan.StartLinePosition.Line + 1;
            var column = lineSpan.StartLinePosition.Character + 1;
            var path = string.IsNullOrWhiteSpace(lineSpan.Path) ? _scriptFilePath : lineSpan.Path;
            var location = string.IsNullOrWhiteSpace(path)
                ? $"line {line}, col {column}"
                : $"{path}:{line}:{column}";
            Console.WriteLine($"  {diagnostic.Severity} {diagnostic.Id}: {diagnostic.GetMessage()} ({location})");
        }
    }

    private void LogRuntimeError(Exception error)
    {
        Console.WriteLine($"Script Error: {error.GetType().Name}: {error.Message}");
        var location = TryFindStackLocation(error);
        if (!string.IsNullOrWhiteSpace(location))
        {
            Console.WriteLine($"  at {location}");
        }
    }

    private static string? TryFindStackLocation(Exception error)
    {
        if (string.IsNullOrWhiteSpace(error.StackTrace)) return null;
        var match = Regex.Match(error.StackTrace, @"in (.*):line (\d+)");
        if (!match.Success) return null;
        var path = match.Groups[1].Value.Trim();
        var line = match.Groups[2].Value.Trim();
        return string.IsNullOrWhiteSpace(path) ? null : $"{path}:{line}";
    }

    private bool TryLoadLibraryAssembly(out Assembly assembly)
    {
        assembly = _libraryAssembly ?? null!;
        if (_libraryAssemblyLoaded)
        {
            return _libraryAssembly != null;
        }

        _libraryAssemblyLoaded = true;
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, "MusicEngine.Library.dll");
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            _libraryAssembly = Assembly.LoadFrom(path);
            assembly = _libraryAssembly;
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal ISynth? TryCreateLibraryInstrument(string typeName)
    {
        if (!TryLoadLibraryAssembly(out var assembly)) return null;
        if (assembly == null) return null;
        var type = assembly.GetType(typeName, throwOnError: false, ignoreCase: true);
        if (type == null) return null;
        if (!typeof(ISynth).IsAssignableFrom(type)) return null;
        try
        {
            return Activator.CreateInstance(type) as ISynth;
        }
        catch (TargetInvocationException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            Console.WriteLine($"Script Error: Failed to create {typeName}: {inner}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Script Error: Failed to create {typeName}: {ex.Message}");
            return null;
        }
    }

    internal bool HasLibraryInstrument(string typeName)
    {
        if (!TryLoadLibraryAssembly(out var assembly)) return false;
        if (assembly == null) return false;
        var type = assembly.GetType(typeName, throwOnError: false, ignoreCase: true);
        return type != null && typeof(ISynth).IsAssignableFrom(type);
    }

    internal IReadOnlyList<string> GetLibraryInstrumentTypeNames()
    {
        if (!TryLoadLibraryAssembly(out var assembly) || assembly == null)
        {
            return Array.Empty<string>();
        }

        var results = new List<string>();
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract) continue;
            if (!typeof(ISynth).IsAssignableFrom(type)) continue;
            results.Add(type.FullName ?? type.Name);
        }
        results.Sort(StringComparer.OrdinalIgnoreCase);
        return results;
    }
}

/// <summary>
/// Globals exposed to scripts for building synths, patterns, and routing.
/// </summary>
public sealed class ScriptGlobals
{
    /// <summary>
    /// Audio engine instance.
    /// </summary>
    public AudioEngine Engine { get; set; } = null!;
    /// <summary>
    /// Sequencer instance.
    /// </summary>
    public Sequencer Sequencer { get; set; } = null!;
    /// <summary>
    /// Script host instance.
    /// </summary>
    public ScriptHost? Host { get; set; }
    /// <summary>
    /// VST registry if scanning is enabled.
    /// </summary>
    public Vst3Registry? VstRegistry { get; set; }
    /// <summary>
    /// Optional script file path for per-script storage.
    /// </summary>
    public string? ScriptFilePath { get; set; }
    /// <summary>
    /// Timing master from the sequencer.
    /// </summary>
    public TimingMaster Timing => Sequencer.Timing;

      private SimpleSynth? _synth;
      private ISynth? _lastInstrument;
      private GeneralMidiInstrument? _lastGeneralMidi;
      private SamplerInstrument? _lastSampler;
      private IVstInstrument? _lastVstInstrument;
      private Vst3Effect? _lastVstEffect;
      private EffectRack? _lastEffectRack;
      private MidiEffectRack? _lastMidiEffectRack;
      private AudioInput? _lastInput;
      private AudioDeck? _lastDeck;
      private AudioClip? _lastClip;
      private ScriptLibrary? _library;
      private ActivityController? _activity;
      private MasterBus? _masterBus;
      private LibraryAccess? _libraryAccess;

    /// <summary>
    /// Access instruments and helpers from the optional MusicEngine.Library assembly.
    /// </summary>
    public LibraryAccess LibraryTools => _libraryAccess ??= new LibraryAccess(this);

    /// <summary>
    /// Create and route a library instrument by full type name.
    /// </summary>
    public dynamic LibraryApi(string typeName)
        => LibraryTools.Instrument(typeName);

    /// <summary>
    /// Create and route a SimpleSynth instance.
    /// </summary>
    public SimpleSynth CreateSynth()
    {
        var synth = new SimpleSynth();
        Engine.AddSampleProvider(synth);
        Host?.RegisterSynth(synth);
        _synth = synth;
        _lastInstrument = synth;
        return synth;
    }

    /// <summary>
    /// Create and route a General MIDI instrument.
    /// </summary>
      public GeneralMidiInstrument CreateGeneralMidi()
      {
          var instrument = new GeneralMidiInstrument();
          Engine.AddSampleProvider(instrument);
          Host?.RegisterSynth(instrument);
          _lastGeneralMidi = instrument;
          _lastInstrument = instrument;
          return instrument;
      }

      /// <summary>
      /// Create and route a sampler instrument.
      /// </summary>
      public SamplerInstrument CreateSampler()
      {
          var sampler = new SamplerInstrument();
          Engine.AddSampleProvider(sampler);
          Host?.RegisterSynth(sampler);
          _lastSampler = sampler;
          _lastInstrument = sampler;
          return sampler;
      }

      /// <summary>
      /// Create and route a speech (TTS) instrument.
      /// </summary>
    public dynamic CreateSpeech()
    {
        return LibraryTools.Instrument("MusicEngine.Instruments.SpeechInstrument");
    }

      /// <summary>
      /// Create and route a speech (TTS) instrument.
      /// </summary>
    public dynamic Speech() => CreateSpeech();

      /// <summary>
      /// Default General MIDI instrument (last created).
      /// </summary>
      public GeneralMidiInstrument piano => _lastGeneralMidi ??= CreateGeneralMidi();
      /// <summary>
      /// Default General MIDI instrument (last created).
      /// </summary>
      public GeneralMidiInstrument Piano => piano;
      /// <summary>
      /// Default synth (last created).
      /// </summary>
      public SimpleSynth synth => _synth ??= CreateSynth();
      /// <summary>
      /// Default synth (last created).
      /// </summary>
      public SimpleSynth Synth => synth;
      /// <summary>
      /// Default sampler (last created).
      /// </summary>
      public SamplerInstrument sampler => _lastSampler ??= CreateSampler();
      /// <summary>
      /// Default instrument (last created).
      /// </summary>
      public ISynth instrument => _lastInstrument ??= CreateSynth();
      /// <summary>
      /// Default instrument (last created).
      /// </summary>
      public ISynth Instrument => instrument;

    internal void SetLastInstrument(ISynth synth)
    {
        _lastInstrument = synth;
    }

      /// <summary>
      /// Create and route a live audio input (mic/line-in) by device index.
      /// </summary>
      public AudioInput CreateMic(int deviceIndex)
      {
          var input = Engine.CreateInput(deviceIndex);
          Engine.AddSampleProvider(input);
          _lastInput = input;
          return input;
      }

      /// <summary>
      /// Create and route a live audio input (mic/line-in) by device name.
      /// </summary>
      public AudioInput CreateMic(string deviceName)
      {
          var input = Engine.CreateInput(deviceName);
          Engine.AddSampleProvider(input);
          _lastInput = input;
          return input;
      }

      /// <summary>
      /// Create and route an audio deck.
      /// </summary>
      public AudioDeck CreateDeck(string name)
      {
          var deck = new AudioDeck(name);
          Engine.AddSampleProvider(deck);
          _lastDeck = deck;
          return deck;
      }

    /// <summary>
    /// Create a time master controller.
    /// </summary>
    public TimeMasterController CreateTimeMaster() => new TimeMasterController();

    /// <summary>
    /// Create a modular audio effect rack.
    /// </summary>
    public EffectRack CreateEffect()
    {
        var rack = new EffectRack();
        _lastEffectRack = rack;
        return rack;
    }

    /// <summary>
    /// Create a reverb preset effect.
    /// </summary>
    public SimpleReverbEffect ReverbPreset(string name, Action<SimpleReverbEffect>? configure = null)
        => Effect.ReverbPreset(name, configure);

    /// <summary>
    /// Create a delay preset effect.
    /// </summary>
    public SimpleDelayEffect DelayPreset(string name, Action<SimpleDelayEffect>? configure = null)
        => Effect.DelayPreset(name, configure);

    /// <summary>
    /// Create a tremolo preset effect.
    /// </summary>
    public TremoloEffect TremoloPreset(string name, Action<TremoloEffect>? configure = null)
        => Effect.TremoloPreset(name, configure);

    /// <summary>
    /// Create a bit crush preset effect.
    /// </summary>
    public BitCrusherEffect BitCrushPreset(string name, Action<BitCrusherEffect>? configure = null)
        => Effect.BitCrushPreset(name, configure);

    /// <summary>
    /// Create a noise preset effect.
    /// </summary>
    public NoiseEffect NoisePreset(string name, Action<NoiseEffect>? configure = null)
        => Effect.NoisePreset(name, configure);

    /// <summary>
    /// Create a drive preset effect.
    /// </summary>
    public DriveEffect DrivePreset(string name, Action<DriveEffect>? configure = null)
        => Effect.DrivePreset(name, configure);

    /// <summary>
    /// Create a filter preset effect.
    /// </summary>
    public SimpleFilterEffect FilterPreset(string name, Action<SimpleFilterEffect>? configure = null)
        => Effect.FilterPreset(name, configure);

    /// <summary>
    /// Create a modular MIDI effect rack.
    /// </summary>
    public MidiEffectRack CreateMidiEffect()
    {
        var rack = new MidiEffectRack();
        _lastMidiEffectRack = rack;
        return rack;
    }

    /// <summary>
    /// Create a pattern using the last created synth.
    /// </summary>
    public Pattern CreatePattern() => CreatePattern(_synth ??= CreateSynth());

    /// <summary>
    /// Create a pattern targeting a specific synth.
    /// </summary>
    public Pattern CreatePattern(ISynth synth)
    {
        var pattern = new Pattern(synth);
        pattern.Sequencer = Sequencer;
        Engine.RegisterPatternForEditor(pattern);
        return pattern;
    }

    /// <summary>
    /// Create a pattern targeting multiple synths.
    /// </summary>
    public Pattern CreatePattern(ISynth synth, params ISynth[] moreSynths)
    {
        var pattern = new Pattern(synth, moreSynths);
        pattern.Sequencer = Sequencer;
        Engine.RegisterPatternForEditor(pattern);
        return pattern;
    }

    /// <summary>
    /// Create a pattern with priority/fallback targets.
    /// </summary>
    public Pattern CreatePattern(ISynth primary, FallbackTarget fallback, params FallbackTarget[] fallbacks)
    {
        var pattern = new Pattern(primary, includePrimary: false);
        var list = new List<ISynth> { primary };
        if (fallback?.Synth != null) list.Add(fallback.Synth);
        if (fallbacks != null)
        {
            foreach (var target in fallbacks)
            {
                if (target?.Synth != null)
                {
                    list.Add(target.Synth);
                }
            }
        }
        pattern.AddPriorityGroup(list.ToArray());
        pattern.Sequencer = Sequencer;
        Engine.RegisterPatternForEditor(pattern);
        return pattern;
    }

    /// <summary>
    /// Load a folder of samples for easy access by name.
    /// </summary>
    public dynamic GetSamples(string folder, string searchPattern = "*.*", bool recursive = true, bool audioOnly = true)
    {
        var resolved = ResolvePath(folder);
        var samples = new SampleFolder(resolved, searchPattern, recursive, audioOnly);
        return new CaseInsensitiveProxy(samples);
    }

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        if (Path.IsPathRooted(path)) return path;

        var baseDir = !string.IsNullOrWhiteSpace(ScriptFilePath)
            ? Path.GetDirectoryName(ScriptFilePath)
            : AppContext.BaseDirectory;

        return string.IsNullOrWhiteSpace(baseDir) ? path : Path.Combine(baseDir, path);
    }

    /// <summary>
    /// Create a note binding helper for direct note triggering.
    /// </summary>
    public NoteBuilder Note(int note) => new NoteBuilder(this, note);

    /// <summary>
    /// Create a note binding helper for direct note triggering.
    /// </summary>
    public NoteBuilder note(int note) => Note(note);

    /// <summary>
    /// Create a note binding helper for direct note triggering.
    /// </summary>
    public NoteBuilder NOTE(int note) => Note(note);

      /// <summary>
      /// Create a new VST3 instrument by name.
      /// </summary>
      public IVstInstrument CreateVst(string name, string? alias = null)
      {
          var instrument = alias == null ? vst.Create(name) : vst.Create(name, alias);
          _lastVstInstrument = instrument;
          _lastInstrument = instrument;
          return instrument;
      }
      /// <summary>
      /// Create a new VST3 instrument by name.
      /// </summary>
      public IVstInstrument Vst(string name, string? alias = null) => CreateVst(name, alias);
      /// <summary>
      /// Default VST instrument (last created).
      /// </summary>
      public IVstInstrument vsti => Require(_lastVstInstrument, "CreateVst(\"Name\")");
      /// <summary>
      /// Default VST instrument (last created).
      /// </summary>
      public IVstInstrument Vsti => vsti;
      /// <summary>
      /// Create and route a live audio input (mic/line-in) by device index.
      /// </summary>
      public AudioInput Mic(int deviceIndex) => CreateMic(deviceIndex);
      /// <summary>
      /// Create and route a live audio input (mic/line-in) by device name.
      /// </summary>
      public AudioInput Mic(string deviceName) => CreateMic(deviceName);
      /// <summary>
      /// Create and route a live audio input (mic/line-in) by device index.
      /// </summary>
      public AudioInput CreateInput(int deviceIndex) => CreateMic(deviceIndex);
      /// <summary>
      /// Create and route a live audio input (mic/line-in) by device name.
      /// </summary>
      public AudioInput CreateInput(string deviceName) => CreateMic(deviceName);
      /// <summary>
      /// Create and route a live audio input (mic/line-in) by device index.
      /// </summary>
      public AudioInput Input(int deviceIndex) => CreateMic(deviceIndex);
      /// <summary>
      /// Create and route a live audio input (mic/line-in) by device name.
      /// </summary>
      public AudioInput Input(string deviceName) => CreateMic(deviceName);
      /// <summary>
      /// Default audio input (last created).
      /// </summary>
      public AudioInput mic => Require(_lastInput, "CreateMic(index) / CreateInput(index)");
    /// <summary>
    /// Create and route a sampler instrument.
    /// </summary>
    public SamplerInstrument Sampler() => CreateSampler();
    /// <summary>
    /// Create and route an audio deck.
    /// </summary>
      public AudioDeck Deck(string name) => CreateDeck(name);
      /// <summary>
      /// Default audio deck (last created).
      /// </summary>
      public AudioDeck deck => Require(_lastDeck, "CreateDeck(\"Name\")");
    /// <summary>
    /// Create a time master controller.
    /// </summary>
    public TimeMasterController TimeMaster() => CreateTimeMaster();
      /// <summary>
      /// Create a new VST3 effect by name.
      /// </summary>
      public Vst3Effect CreateVstEffect(string name, string? alias = null)
      {
          var effect = alias == null ? vst.CreateEffect(name) : vst.CreateEffect(name, alias);
          _lastVstEffect = effect;
          return effect;
      }
      /// <summary>
      /// Default VST effect (last created).
      /// </summary>
      public Vst3Effect vstfx => Require(_lastVstEffect, "CreateVstEffect(\"Name\")");
      /// <summary>
      /// Default VST effect (last created).
      /// </summary>
      public Vst3Effect VstFx => vstfx;
      /// <summary>
      /// Load an audio clip from disk.
      /// </summary>
      public AudioClip CreateAudioClip(string path)
      {
          var clip = new AudioClip(path);
          _lastClip = clip;
          return clip;
      }
      /// <summary>
      /// Default audio clip (last created).
      /// </summary>
      public AudioClip clip => Require(_lastClip, "CreateAudioClip(\"Path\")");
      /// <summary>
      /// Default effect rack (last created).
      /// </summary>
      public EffectRack effect => Require(_lastEffectRack, "CreateEffect()");
      /// <summary>
      /// Default MIDI effect rack (last created).
      /// </summary>
      public MidiEffectRack midiefx => Require(_lastMidiEffectRack, "CreateMidiEffect()");

    /// <summary>
    /// Shared script library (dynamic).
    /// </summary>
    public dynamic File => _library ??= new ScriptLibrary(Host ?? throw new InvalidOperationException("Host missing."));

    /// <summary>
    /// Alias for File (dynamic).
    /// </summary>
    public dynamic Include => File;
    /// <summary>
    /// Alias for File (dynamic).
    /// </summary>
    public dynamic include => Include;
    /// <summary>
    /// Alias for File (dynamic).
    /// </summary>
    public dynamic INCLUDE => Include;

    /// <summary>
    /// Shared script library (typed access).
    /// </summary>
    public ScriptLibrary Library => _library ??= new ScriptLibrary(Host ?? throw new InvalidOperationException("Host missing."));

    /// <summary>
    /// Master bus marker for routing.
    /// </summary>
    public MasterBus Master => _masterBus ??= new MasterBus();
    /// <summary>
    /// Master bus marker for routing.
    /// </summary>
    public MasterBus master => Master;
    /// <summary>
    /// Master bus marker for routing.
    /// </summary>
    public MasterBus MASTER => Master;

    /// <summary>
    /// Global activity controller.
    /// </summary>
    public ActivityController Activity => _activity ??= new ActivityController(this);

    /// <summary>
    /// Global activity controller.
    /// </summary>
    public ActivityController activity => Activity;

    /// <summary>
    /// Audio renderer keyword for ASIO output.
    /// </summary>
    public string asio => "asio";
    /// <summary>
    /// Audio renderer keyword for ASIO output.
    /// </summary>
    public string Asio => asio;
    /// <summary>
    /// Audio renderer keyword for WaveOut/MME output.
    /// </summary>
    public string waveout => "waveout";
    /// <summary>
    /// Audio renderer keyword for WaveOut/MME output.
    /// </summary>
    public string WaveOut => waveout;
    /// <summary>
    /// Audio renderer keyword for WaveOut/MME output.
    /// </summary>
    public string mme => "waveout";
    /// <summary>
    /// Audio renderer keyword for WaveOut/MME output.
    /// </summary>
    public string MME => waveout;

    /// <summary>
    /// Create a fallback target for priority MIDI routing.
    /// </summary>
    public FallbackTarget Fallback(ISynth synth) => new FallbackTarget(synth);

    /// <summary>
    /// Load and run a module script by name.
    /// </summary>
    public Task<bool> Use(string name)
    {
        if (Host == null) return Task.FromResult(false);
        return Host.ExecuteModuleAsync(name);
    }

    /// <summary>
    /// Load and run a module script by name with optional force reload.
    /// </summary>
    public Task<bool> Use(string name, bool force)
    {
        if (Host == null) return Task.FromResult(false);
        return Host.ExecuteModuleAsync(name, skipIfUnchanged: !force);
    }

    /// <summary>
    /// Fluent audio control API (case-insensitive proxy).
    /// </summary>
    public dynamic audio => _audioProxy ??= new CaseInsensitiveProxy(_audioControl ??= new AudioControl(this));
    /// <summary>
    /// Fluent audio control API (case-insensitive proxy).
    /// </summary>
    public dynamic Audio => audio;
    /// <summary>
    /// Fluent audio control API (case-insensitive proxy).
    /// </summary>
    public dynamic AUDIO => audio;
    /// <summary>
    /// Fluent MIDI control API.
    /// </summary>
    public MidiControl midi => _midiControl ??= new MidiControl(this);
    /// <summary>
    /// Fluent MIDI control API.
    /// </summary>
    public MidiControl Midi => midi;
    /// <summary>
    /// Fluent MIDI control API.
    /// </summary>
    public MidiControl MIDI => midi;
    /// <summary>
    /// Dynamic VST access API.
    /// </summary>
    public dynamic vst => _vstAccess ??= new VstAccess(this);
    /// <summary>
    /// Case-insensitive root for scripting APIs.
    /// </summary>
    public dynamic Music => _musicProxy ??= new CaseInsensitiveProxy(this);
    /// <summary>
    /// Case-insensitive root for scripting APIs.
    /// </summary>
    public dynamic music => Music;
    /// <summary>
    /// Case-insensitive root for scripting APIs.
    /// </summary>
    public dynamic MUSIC => Music;
    /// <summary>
    /// Random source helper for scripts.
    /// </summary>
    public RandomSource Random { get; } = new RandomSource();

    /// <summary>
    /// Shared MIDI mapping helper for scripts.
    /// </summary>
    public MidiMap MidiMap => _midiMap ??= new MidiMap();
    /// <summary>
    /// Shared MIDI mapping helper for scripts.
    /// </summary>
      public MidiMap Map => MidiMap;

    /// <summary>
    /// Bind a normalized MIDI value (0..1) to a property/field.
    /// </summary>
    public Action<float> Bind(object target, string member, float min = 0f, float max = 1f)
        => PropertyBinder.Create(target, member, min, max);

    /// <summary>
    /// Bind a normalized MIDI value (0..1) to a property/field with a custom mapper.
    /// </summary>
    public Action<float> Bind(object target, string member, Func<float, float> map)
        => PropertyBinder.Create(target, member, map);

    /// <summary>
    /// Bind a normalized MIDI value (0..1) to a method call on rising edge.
    /// </summary>
    public Action<float> BindTrigger(object target, string method)
        => ActionBinder.Trigger(target, method);

    /// <summary>
    /// Bind a normalized MIDI value (0..1) to a method with a single parameter.
    /// </summary>
    public Action<float> BindCall(object target, string method, float min = 0f, float max = 1f)
        => ActionBinder.Call(target, method, min, max);

    /// <summary>
    /// Bind a normalized MIDI value (0..1) to a method with a custom mapper.
    /// </summary>
    public Action<float> BindCall(object target, string method, Func<float, float> map)
        => ActionBinder.Call(target, method, map);

    /// <summary>
    /// Toggle a boolean property/field on rising edge.
    /// </summary>
    public Action<float> BindToggle(object target, string member)
        => ActionBinder.Toggle(target, member);

    /// <summary>
    /// Switch a boolean property/field based on the current value.
    /// </summary>
    public Action<float> BindSwitch(object target, string member)
        => ActionBinder.Switch(target, member);

    /// <summary>
    /// Toggle a boolean value (getter/setter) on rising edge.
    /// </summary>
    public Action<float> BindToggle(Func<bool> getter, Action<bool> setter)
        => ActionBinder.Toggle(getter, setter);

    /// <summary>
    /// Switch a boolean value (getter/setter) based on the current value.
    /// </summary>
    public Action<float> BindSwitch(Func<bool> getter, Action<bool> setter)
        => ActionBinder.Switch(getter, setter);

    /// <summary>
    /// Create a modulated variable from any writable property/field.
    /// </summary>
    public ModVar Var(object target, string member, float? initial = null)
        => Mod.Var(target, member, initial);

    /// <summary>
    /// Alias for Var.
    /// </summary>
    public ModVar Param(object target, string member, float? initial = null)
        => Var(target, member, initial);

    private VstAccess? _vstAccess;
    private MidiMap? _midiMap;
    private AudioControl? _audioControl;
    private MidiControl? _midiControl;
    private CaseInsensitiveProxy? _audioProxy;
    private CaseInsensitiveProxy? _musicProxy;

    internal VstAccess? VstAccessInstance => _vstAccess;

    internal void SetLibrary(ScriptLibrary library)
    {
        _library = library;
    }

    internal void SetVstAccess(VstAccess access)
    {
        _vstAccess = access;
    }

    internal void RouteMidi(int deviceIndex, ISynth synth) => Engine.RouteMidiInput(deviceIndex, synth);

    internal void RouteMidi(int deviceIndex, int channel, ISynth synth)
        => Engine.RouteMidiInput(deviceIndex, channel, synth);

    internal void SetMidiDeviceEnabled(int deviceIndex, bool enabled, bool sendAllNotesOff)
        => Engine.SetMidiDeviceEnabled(deviceIndex, enabled, sendAllNotesOff);

    internal void SetMidiChannelEnabled(int deviceIndex, int channel, bool enabled, bool sendAllNotesOff)
        => Engine.SetMidiChannelEnabled(deviceIndex, channel, enabled, sendAllNotesOff);

    internal void SetMidiRouteEnabled(int deviceIndex, int channel, ISynth synth, bool enabled, bool sendAllNotesOff)
        => Engine.SetMidiRouteEnabled(deviceIndex, channel, synth, enabled, sendAllNotesOff);

    internal void MapControlAction(int deviceIndex, int controlId, Action<float> action)
        => Engine.MapControlAction(deviceIndex, controlId, action);

    internal void MapControlAction(int deviceIndex, int channel, int controlId, Action<float> action)
        => Engine.MapControlAction(deviceIndex, channel, controlId, action);

    private static T Require<T>(T? value, string hint) where T : class
    {
        if (value != null) return value;
        throw new InvalidOperationException($"No instance created yet. Call {hint} first.");
    }
}
