// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: WinForms host window for VST3 editors.

using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MusicEngine.Core;

namespace MusicEngine.Vst;

public sealed class Vst3EditorWindow : Form
{
    private readonly string _pluginPath;
    private readonly Panel _hostPanel;
    private IntPtr _hostHandle;
    private readonly bool _ownsHandle;
    private System.Windows.Forms.Timer? _processTimer;
    private float[]? _processBuffer;
    private int _processChannels;
    private GCHandle _processBufferHandle;

    public static void Open(string pluginPath, string displayName)
    {
        VstUiContext.Shared.BeginInvoke(() => OpenOnUiThread(pluginPath, displayName, IntPtr.Zero, ownsHandle: true));
    }

    public static void OpenExisting(IntPtr hostHandle, string displayName)
    {
        if (hostHandle == IntPtr.Zero) return;
        VstUiContext.Shared.BeginInvoke(() => OpenOnUiThread(string.Empty, displayName, hostHandle, ownsHandle: false));
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

        _hostPanel = new Panel
        {
            Dock = DockStyle.Fill
        };
        Controls.Add(_hostPanel);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (!Vst3Native.TryValidate(out var message))
        {
            MessageBox.Show(message, "MusicEngine", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        if (_ownsHandle)
        {
            _hostHandle = Vst3Native.Vst3Host_Create(_pluginPath);
            if (_hostHandle == IntPtr.Zero)
            {
                MessageBox.Show($"Failed to load VST3: {_pluginPath}", "MusicEngine", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            _processChannels = Math.Max(1, Vst3Native.Vst3Host_GetOutputChannels(_hostHandle));
            Vst3Native.Vst3Host_SetupAudio(_hostHandle, Settings.SampleRate, 512);
        }

        if (!Vst3Native.Vst3Host_OpenEditor(_hostHandle, _hostPanel.Handle))
        {
            MessageBox.Show("Failed to open VST3 editor.", "MusicEngine", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        if (_ownsHandle)
        {
            StartProcessingPump();
        }
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
}
