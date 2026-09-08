using System;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Controls;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using DoneBubble.Services;
using DoneBubble.Models;
using DoneBubble.ViewModels;
namespace DoneBubble;
public partial class MainWindow : Window
{
    private readonly SettingsService settings;
    private readonly MainViewModel model;
    private bool expanded, dragging, composing;
    private Point down;
    private double anchorX, anchorY;
    private readonly DispatcherTimer timer;
    private readonly RestWindow restWindow;
    private DateTime date = DateTime.Today;
    public event Action? HistoryRequested;
    public event Action? AiLogsRequested;
    public event Action? AiDebugRequested;
    public event Action<AiAnalysisResult, long>? AiSummaryAccepted;
    public event Func<Task<AiAnalysisResult?>>? SessionSummaryRequested;
    public MainWindow(MainViewModel model, SettingsService settings)
    {
        this.model = model; this.settings = settings; InitializeComponent(); AiSummarizeToggle.IsChecked = true; DataContext = model;
        restWindow = new RestWindow(model.Rest, settings);
        Left = double.IsFinite(settings.Value.WindowX) ? settings.Value.WindowX : 80;
        Top = double.IsFinite(settings.Value.WindowY) ? settings.Value.WindowY : 160;
        Loaded += (_, _) => { Clamp(); Remember(); restWindow.Sync(IsVisible, Left + 82, Top); };
        Deactivated += (_, _) => { if (expanded) Collapse(); };
        // IME candidate confirmation must not also submit the record.
        TextCompositionManager.AddPreviewTextInputStartHandler(Input, (_, _) => composing = true);
        TextCompositionManager.AddPreviewTextInputHandler(Input, (_, _) => composing = false);
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => { model.Rest.Tick(); restWindow.Sync(IsVisible, Left + 82, Top); ResizeCard(); if (date != DateTime.Today) { date = DateTime.Today; model.Refresh(); } };
        timer.Start();
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowMessage);
    }
    private IntPtr WindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x007E || msg == 0x02E0) Dispatcher.BeginInvoke(() => { if (expanded) Collapse(); Clamp(); Remember(); });
        return IntPtr.Zero;
    }
    private void Bubble_Down(object sender, MouseButtonEventArgs e) { down = e.GetPosition(this); dragging = false; Bubble.CaptureMouse(); }
    private void Bubble_Move(object sender, MouseEventArgs e)
    {
        if (!Bubble.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed) return;
        var now = e.GetPosition(this);
        if (Math.Abs(now.X - down.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(now.Y - down.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        dragging = true; Bubble.ReleaseMouseCapture();
        try { DragMove(); } catch (InvalidOperationException) { }
        Clamp(); Remember();
    }
    private void Bubble_Up(object sender, MouseButtonEventArgs e)
    {
        bool captured = Bubble.IsMouseCaptured; Bubble.ReleaseMouseCapture();
        if (captured && !dragging) Expand();
    }
    public void Reveal() { Show(); Collapse(); Clamp(); Activate(); restWindow.Reveal(Left + 82, Top); }
    private void Expand()
    {
        model.Refresh(); anchorX = Left; anchorY = Top; expanded = true;
        Bubble.Visibility = Visibility.Collapsed; Card.Visibility = Visibility.Visible;
        Width = 350; Height = CardHeight; Clamp(); Activate();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { if (expanded) { LightButton.Focus(); Keyboard.Focus(LightButton); } }));
    }
    public void Collapse()
    {
        if (!expanded) return;
        expanded = false; composing = false; Card.Visibility = Visibility.Collapsed; Bubble.Visibility = Visibility.Visible;
        Width = Height = 76; Left = anchorX; Top = anchorY; Clamp();
    }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !composing) { Collapse(); e.Handled = true; }
        if (expanded && Input.IsKeyboardFocusWithin && e.Key == Key.Enter && !composing && e.ImeProcessedKey == Key.None)
        {
            e.Handled = true;
            if (model.Save()) Collapse(); else { Height = CardHeight; Clamp(); }
            return;
        }
        if (!expanded && (e.Key == Key.Enter || e.Key == Key.Space)) { Expand(); e.Handled = true; }
    }
    private async void Category_Click(object sender, RoutedEventArgs e)
    {
        if (!expanded || composing) return;
        AiAnalysisResult? aiResult = null;
        if (AiSummarizeToggle.IsChecked == true && SessionSummaryRequested != null)
        {
            model.Error = "正在生成总结…"; ResizeCard();
            aiResult = await SessionSummaryRequested();
            if (aiResult?.Candidate != null) model.Draft = aiResult.Candidate.Summary;
            model.Error = "";
            if (aiResult == null) { model.Error = "还没有采集到可总结的活动，请先勾选 AI 总结并工作几秒。"; ResizeCard(); return; }
        }
        if (model.Save((string)((Button)sender).Tag)) { if (aiResult != null) AiSummaryAccepted?.Invoke(aiResult, model.LastSavedId); Collapse(); }
        else { Height = CardHeight; Clamp(); }
    }
    private double CardHeight => (model.HasError ? 330 : 285) + (model.HasCandidate ? 102 : 0) + 24;
    private void ResizeCard()
    {
        if (expanded && Height != CardHeight) { Height = CardHeight; Clamp(); }
    }
    private void Collapse_Click(object sender, RoutedEventArgs e) => Collapse();
    private void CandidateAccept_Click(object sender, RoutedEventArgs e)
    {
        var candidate = model.Candidate;
        if (candidate == null) return;
        model.Draft = candidate.Summary; model.DismissCandidate();
        if (model.Save(candidate.SuggestedCategory) && candidate.Analysis != null) AiSummaryAccepted?.Invoke(candidate.Analysis, model.LastSavedId);
        ResizeCard();
    }
    private void CandidateDismiss_Click(object sender, RoutedEventArgs e) { model.DismissCandidate(); ResizeCard(); }
    private void History_Click(object sender, RoutedEventArgs e) { Collapse(); HistoryRequested?.Invoke(); }
    private void AiLogs_Click(object sender, RoutedEventArgs e) { Collapse(); AiLogsRequested?.Invoke(); }
    private void AiDebug_Click(object sender, RoutedEventArgs e) { Collapse(); AiDebugRequested?.Invoke(); }
    private void AiSummarizeToggle_Changed(object sender, RoutedEventArgs e)
    {
        settings.Value.AiAssistEnabled = AiSummarizeToggle.IsChecked == true;
        try { settings.Save(); } catch { }
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        // Closing the floating window never terminates the tray application.
        if (!((App)Application.Current).IsExiting) { e.Cancel = true; Collapse(); Hide(); }
        base.OnClosing(e);
    }
    public void Stop() { timer.Stop(); restWindow.DisposeWindow(); Collapse(); Remember(); }
    private void Remember()
    {
        settings.Value.WindowX = Left; settings.Value.WindowY = Top;
        try { settings.Save(); } catch { model.Error = "位置设置未能保存，请检查数据目录权限。"; }
    }
    // Screen work areas are physical pixels; WPF sizes are device-independent units.
    private void Clamp()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var screen = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        GetWindowRect(handle, out var rect);
        int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
        int x = Math.Clamp(rect.Left, screen.Left, Math.Max(screen.Left, screen.Right - width));
        int y = Math.Clamp(rect.Top, screen.Top, Math.Max(screen.Top, screen.Bottom - height));
        SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, 0x0015);
    }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
