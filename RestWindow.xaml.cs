using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DoneBubble.Services;
using DoneBubble.ViewModels;
namespace DoneBubble;
public partial class RestWindow : Window
{
    private readonly RestViewModel rest;
    private readonly SettingsService settings;
    private bool disposed, suppressed, positioned;
    public RestWindow(RestViewModel rest, SettingsService settings)
    {
        InitializeComponent(); this.rest = rest; this.settings = settings; DataContext = rest;
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(Message);
    }
    public void Sync(bool bubbleVisible, double x, double y)
    {
        if (disposed) return;
        if (!rest.IsResting) { Hide(); suppressed = false; return; }
        if (!bubbleVisible || suppressed) { Hide(); return; }
        if (!positioned)
        {
            Left = settings.Value.RestWindowX is double savedX && double.IsFinite(savedX) ? savedX : x;
            Top = settings.Value.RestWindowY is double savedY && double.IsFinite(savedY) ? savedY : y;
            positioned = true;
        }
        if (!IsVisible) { Show(); Clamp(); }
    }
    public void Reveal(double x, double y) { suppressed = false; Sync(true, x, y); }
    private void Adjust(object sender, RoutedEventArgs e)
    {
        rest.Adjust(int.Parse((string)((Button)sender).Tag));
        if (!rest.IsResting) Hide();
    }
    private void Drag(object sender, MouseButtonEventArgs e)
    {
        try { DragMove(); } catch (InvalidOperationException) { }
        Clamp();
        settings.Value.RestWindowX = Left; settings.Value.RestWindowY = Top;
        try { settings.Save(); } catch { }
    }
    private IntPtr Message(IntPtr hwnd, int message, IntPtr w, IntPtr l, ref bool handled)
    {
        if (message == 0x007E || message == 0x02E0) Dispatcher.BeginInvoke(new Action(Clamp));
        return IntPtr.Zero;
    }
    private void Clamp()
    {
        if (disposed) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var area = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        GetWindowRect(handle, out var rect);
        SetWindowPos(handle, IntPtr.Zero,
            Math.Clamp(rect.Left, area.Left, Math.Max(area.Left, area.Right - (rect.Right - rect.Left))),
            Math.Clamp(rect.Top, area.Top, Math.Max(area.Top, area.Bottom - (rect.Bottom - rect.Top))), 0, 0, 0x0015);
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!disposed) { e.Cancel = true; suppressed = true; Hide(); }
        base.OnClosing(e);
    }
    public void DisposeWindow() { if (disposed) return; disposed = true; Close(); }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
