// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: WinForms host window for VST3 editors.

using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MusicEngine.Core;

namespace MusicEngine.Vst;

/// <summary>
/// WinForms host window for VST3 editors.
/// </summary>
public sealed class Vst3EditorWindow : Form
{
    private const int SwShow = 5;
    private const int SwRestore = 9;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiSmallIcon = 0x000000001;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, out ShFileInfo info, uint infoSize, uint flags);

    private readonly string _pluginPath;
    private readonly Panel _hostPanel;
    private IntPtr _hostHandle;
    private readonly bool _ownsHandle;
    private System.Windows.Forms.Timer? _processTimer;
    private float[]? _processBuffer;
    private int _processChannels;
    private GCHandle _processBufferHandle;
    private Size _editorSize = Size.Empty;
    private bool _suppressResize;

    /// <summary>
    /// Open a new VST3 editor window from a plugin path.
    /// </summary>
    /// <param name="pluginPath">Path to the VST3 plugin.</param>
    /// <param name="displayName">Display name for the window title.</param>
    public static void Open(string pluginPath, string displayName)
    {
        VstUiContext.Shared.BeginInvoke(() => OpenOnUiThread(pluginPath, displayName, IntPtr.Zero, ownsHandle: true));
    }

    /// <summary>
    /// Open a VST3 editor window for an existing native handle.
    /// </summary>
    /// <param name="hostHandle">Native host handle.</param>
    /// <param name="displayName">Display name for the window title.</param>
    /// <param name="pluginPath">Path to the VST3 plugin for icon lookup.</param>
    public static void OpenExisting(IntPtr hostHandle, string displayName, string pluginPath)
    {
        if (hostHandle == IntPtr.Zero) return;
        VstUiContext.Shared.BeginInvoke(() => OpenOnUiThread(pluginPath, displayName, hostHandle, ownsHandle: false));
    }

    private static void OpenOnUiThread(string pluginPath, string displayName, IntPtr existingHandle, bool ownsHandle)
    {
        var window = new Vst3EditorWindow(pluginPath, displayName, existingHandle, ownsHandle);
        window.Show();
    }

    private Vst3EditorWindow(string pluginPath, string displayName, IntPtr existingHandle, bool ownsHandle)
    {
        _pluginPath = pluginPath;
        _hostHandle = existingHandle;
        _ownsHandle = ownsHandle;
        Text = $"MusicEngine VST3 - {displayName}";
        Width = 900;
        Height = 600;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(24, 24, 24);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;

        _hostPanel = new Panel
        {
            Dock = DockStyle.None,
            Location = Point.Empty,
            BackColor = Color.Black,
            Size = new Size(1, 1)
        };
        _hostPanel.SizeChanged += (_, _) => OnHostPanelSizeChanged();
        Controls.Add(_hostPanel);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        TryEnableDarkTitleBar();
        TrySetPluginIcon();
        if (!Vst3Native.TryValidate(out var message))
        {
            LogError($"Native validation failed: {message}");
            MessageBox.Show(message, "MusicEngine", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        if (_ownsHandle)
        {
            _hostHandle = Vst3Native.Vst3Host_Create(_pluginPath);
            if (_hostHandle == IntPtr.Zero)
            {
                LogError("Vst3Host_Create returned null handle.");
                MessageBox.Show($"Failed to load VST3: {_pluginPath}", "MusicEngine", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            _processChannels = Math.Max(1, Vst3Native.Vst3Host_GetOutputChannels(_hostHandle));
            var blockSize = Math.Max(1, Settings.VstEditorBlockSize);
            Vst3Native.Vst3Host_SetupAudio(_hostHandle, Settings.SampleRate, blockSize);
        }

        bool opened = false;
        try
        {
            opened = Vst3Native.Vst3Host_OpenEditor(_hostHandle, _hostPanel.Handle);
        }
        catch (Exception ex)
        {
            LogError($"Vst3Host_OpenEditor threw: {ex}");
        }

        if (!opened)
        {
            MessageBox.Show("Failed to open VST3 editor.", "MusicEngine", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        SyncEditorSize();
        EnsureFront();

        if (_ownsHandle)
        {
            StartProcessingPump();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_hostPanel == null) return;
        TryResizeEditorToClient();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_hostHandle != IntPtr.Zero && _ownsHandle)
        {
            _processTimer?.Stop();
            _processTimer?.Dispose();
            _processTimer = null;
            if (_processBufferHandle.IsAllocated)
            {
                _processBufferHandle.Free();
            }
            Vst3Native.Vst3Host_Close(_hostHandle);
            _hostHandle = IntPtr.Zero;
        }
        base.OnFormClosed(e);
    }

    private void StartProcessingPump()
    {
        _processBuffer = new float[_processChannels * 512];
        _processBufferHandle = GCHandle.Alloc(_processBuffer, GCHandleType.Pinned);
        _processTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _processTimer.Tick += (_, _) =>
        {
            if (_hostHandle == IntPtr.Zero || _processBuffer == null) return;
            Vst3Native.Vst3Host_Process(_hostHandle, _processBufferHandle.AddrOfPinnedObject(), 512, _processChannels);
        };
        _processTimer.Start();
    }

    private void SyncEditorSize()
    {
        if (_hostHandle == IntPtr.Zero) return;
        if (!Vst3Native.Vst3Host_GetEditorSize(_hostHandle, out var width, out var height))
        {
            LogError("GetEditorSize failed.");
            return;
        }
        if (width <= 0 || height <= 0) return;

        _editorSize = new Size(width, height);
        _suppressResize = true;
        _hostPanel.Size = _editorSize;
        ClientSize = _editorSize;
        _suppressResize = false;
        CenterHostPanel();
    }

    private void TryResizeEditorToClient()
    {
        if (_hostPanel == null) return;
        if (_hostHandle == IntPtr.Zero || _suppressResize) return;
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

        var resized = Vst3Native.Vst3Host_ResizeEditor(_hostHandle, ClientSize.Width, ClientSize.Height);
        if (resized)
        {
            SyncEditorSize();
            return;
        }

        CenterHostPanel();
    }

    private void OnHostPanelSizeChanged()
    {
        if (_hostPanel == null) return;
        if (_suppressResize || _hostPanel.Width <= 0 || _hostPanel.Height <= 0) return;
        _editorSize = _hostPanel.Size;
        _suppressResize = true;
        ClientSize = _hostPanel.Size;
        _suppressResize = false;
        CenterHostPanel();
    }

    private void CenterHostPanel()
    {
        if (_hostPanel == null) return;
        if (_hostPanel.Width <= 0 || _hostPanel.Height <= 0) return;
        var x = Math.Max(0, (ClientSize.Width - _hostPanel.Width) / 2);
        var y = Math.Max(0, (ClientSize.Height - _hostPanel.Height) / 2);
        _hostPanel.Location = new Point(x, y);
    }

    private void EnsureFront()
    {
        if (Handle == IntPtr.Zero) return;
        ShowWindow(Handle, SwRestore);
        ShowWindow(Handle, SwShow);
        Activate();
        BringToFront();
        TopMost = true;
        TopMost = false;
        SetForegroundWindow(Handle);
        _hostPanel.Focus();
    }

    private void TryEnableDarkTitleBar()
    {
        if (Handle == IntPtr.Zero) return;
        var enable = 1;
        _ = DwmSetWindowAttribute(Handle, DwmwaUseImmersiveDarkMode, ref enable, sizeof(int));
    }

    private void TrySetPluginIcon()
    {
        if (Handle == IntPtr.Zero) return;
        if (string.IsNullOrWhiteSpace(_pluginPath)) return;

        var iconPath = ResolvePluginIconPath(_pluginPath);
        if (string.IsNullOrWhiteSpace(iconPath)) return;

        try
        {
            var extracted = Icon.ExtractAssociatedIcon(iconPath);
            if (extracted != null)
            {
                Icon = (Icon)extracted.Clone();
                extracted.Dispose();
                return;
            }
        }
        catch
        {
            // Fallback to shell if ExtractAssociatedIcon fails.
        }

        if (SHGetFileInfo(iconPath, 0, out var info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiSmallIcon) == IntPtr.Zero)
        {
            return;
        }

        if (info.hIcon == IntPtr.Zero) return;
        try
        {
            using var icon = Icon.FromHandle(info.hIcon);
            Icon = (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private static string? ResolvePluginIconPath(string pluginPath)
    {
        if (File.Exists(pluginPath)) return pluginPath;
        if (!Directory.Exists(pluginPath)) return null;

        var contents = Path.Combine(pluginPath, "Contents", "x86_64-win");
        if (Directory.Exists(contents))
        {
            var binaries = Directory.GetFiles(contents, "*.vst3", SearchOption.TopDirectoryOnly);
            if (binaries.Length > 0) return binaries[0];
            binaries = Directory.GetFiles(contents, "*.dll", SearchOption.TopDirectoryOnly);
            if (binaries.Length > 0) return binaries[0];
        }

        var fallback = Directory.GetFiles(pluginPath, "*.vst3", SearchOption.AllDirectories);
        if (fallback.Length > 0) return fallback[0];
        fallback = Directory.GetFiles(pluginPath, "*.dll", SearchOption.AllDirectories);
        if (fallback.Length > 0) return fallback[0];

        return null;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private static void LogError(string message)
    {
        Console.WriteLine($"[VST3] {message}");
    }
}
