// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// Description: Shared STA context for VST UI actions.

#if WINDOWS
using System;
using System.Threading;
using System.Windows.Forms;
#endif

namespace MusicEngine.Vst;

#if WINDOWS
internal sealed class VstUiContext
{
    private static readonly Lazy<VstUiContext> LazyInstance = new(() => new VstUiContext());
    public static VstUiContext Shared => LazyInstance.Value;

    private readonly ManualResetEventSlim _ready = new(false);
    private Control? _invoker;

    private VstUiContext()
    {
        var thread = new Thread(() =>
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            _invoker = new Control();
            _invoker.CreateControl();
            _ready.Set();

            Application.Run(new ApplicationContext());
        })
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        _ready.Wait();
    }

    public T Invoke<T>(Func<T> action)
    {
        if (_invoker == null) throw new InvalidOperationException("VST UI context not ready.");
        if (!_invoker.InvokeRequired)
        {
            return action();
        }

        var result = default(T);
        Exception? exception = null;
        _invoker.Invoke(new MethodInvoker(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        }));

        if (exception != null) throw exception;
        return result!;
    }

    public void BeginInvoke(Action action)
    {
        if (_invoker == null) throw new InvalidOperationException("VST UI context not ready.");
        if (!_invoker.InvokeRequired)
        {
            action();
            return;
        }

        _invoker.BeginInvoke(new MethodInvoker(action));
    }
}
#endif
