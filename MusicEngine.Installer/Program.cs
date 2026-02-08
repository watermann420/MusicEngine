using System;
using System.IO;
using System.Linq;

namespace MusicEngine.Installer;

internal static class Program
{
    private const string PayloadFolderName = "payload";

    public static int Main(string[] args)
    {
        Console.WriteLine("MusicEngine Installer");
        Console.WriteLine();

        var installerDir = AppContext.BaseDirectory;
        var payloadDir = Path.Combine(installerDir, PayloadFolderName);
        if (!Directory.Exists(payloadDir))
        {
            Console.WriteLine($"Payload not found: {payloadDir}");
            Console.WriteLine("Place the MusicEngine build output inside a 'payload' folder next to this installer.");
            return 1;
        }

        var defaultInstall = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicEngine");
        var installDir = GetArgValue(args, "--install-dir") ?? PromptInstallDir(defaultInstall);
        if (string.IsNullOrWhiteSpace(installDir))
        {
            installDir = defaultInstall;
        }

        installDir = Path.GetFullPath(Environment.ExpandEnvironmentVariables(installDir));
        if (!EnsureInstallDirectory(installDir))
        {
            Console.WriteLine("Install cancelled.");
            return 2;
        }

        try
        {
            CopyDirectory(payloadDir, installDir);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Install failed: {ex.Message}");
            return 3;
        }

        var exePath = FindMainExecutable(installDir);
        if (exePath == null)
        {
            Console.WriteLine("Warning: MusicEngine.exe not found in install directory, shortcut not created.");
            return 0;
        }

        var skipShortcut = HasArg(args, "--no-shortcut");
        if (!skipShortcut)
        {
            TryCreateShortcut(exePath, installDir);
        }

        Console.WriteLine("Install complete.");
        return 0;
    }

    private static string PromptInstallDir(string defaultInstall)
    {
        Console.WriteLine("Install directory (Enter to use default):");
        Console.WriteLine(defaultInstall);
        Console.Write("> ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultInstall : input.Trim();
    }

    private static bool EnsureInstallDirectory(string installDir)
    {
        if (!Directory.Exists(installDir))
        {
            Directory.CreateDirectory(installDir);
            return true;
        }

        if (!Directory.EnumerateFileSystemEntries(installDir).Any())
        {
            return true;
        }

        Console.WriteLine($"Directory not empty: {installDir}");
        Console.Write("Overwrite existing files? (y/N): ");
        var input = Console.ReadLine();
        return string.Equals(input, "y", StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, dir);
            Directory.CreateDirectory(Path.Combine(targetDir, relative));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var target = Path.Combine(targetDir, relative);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string? FindMainExecutable(string installDir)
    {
        var direct = Path.Combine(installDir, "MusicEngine.exe");
        if (File.Exists(direct))
        {
            return direct;
        }

        return Directory.GetFiles(installDir, "MusicEngine.exe", SearchOption.AllDirectories).FirstOrDefault();
    }

    private static void TryCreateShortcut(string exePath, string workingDir)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktop))
        {
            Console.WriteLine("Warning: Desktop folder not found, skipping shortcut.");
            return;
        }

        var shortcutPath = Path.Combine(desktop, "MusicEngine.lnk");
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                Console.WriteLine("Warning: WScript.Shell not available, skipping shortcut.");
                return;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = exePath;
            shortcut.WorkingDirectory = workingDir;
            shortcut.Description = "MusicEngine Console";
            shortcut.Save();
            Console.WriteLine($"Shortcut created: {shortcutPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: shortcut failed: {ex.Message}");
        }
    }

    private static bool HasArg(string[] args, string name)
    {
        return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
