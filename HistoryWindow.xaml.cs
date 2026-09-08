using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DoneBubble.Models;
using DoneBubble.Services;
using DoneBubble.ViewModels;

namespace DoneBubble;

public partial class HistoryWindow : Window, INotifyPropertyChanged
{
    private readonly MainViewModel model;
    private readonly AiSessionLogService aiLogs = new();
    private RecordItem? selectedRecord;
    private AiSessionLog? selectedLog;
    private readonly DispatcherTimer refreshTimer;
    public RecordItem? SelectedRecord { get => selectedRecord; private set { selectedRecord = value; Changed(); } }
    public AiSessionLog? SelectedLog { get => selectedLog; private set { selectedLog = value; Changed(); Changed(nameof(HasSelectedLog)); Changed(nameof(HasNoSelectedLog)); Changed(nameof(DisplayedFrames)); Changed(nameof(FrameHint)); } }
    public bool HasSelectedLog => SelectedLog != null;
    public bool HasNoSelectedLog => SelectedLog == null;
    // Keep the full frame archive in ai-sessions.json, but only decode a small
    // representative set in the WPF visual tree. This keeps scrolling and
    // window dragging responsive for long sessions.
    public IReadOnlyList<string> DisplayedFrames
    {
        get
        {
            var frames = SelectedLog?.Frames;
            if (frames == null || frames.Count <= 12) return frames ?? Array.Empty<string>();
            return Enumerable.Range(0, 12).Select(i => frames[(int)Math.Round(i * (frames.Count - 1d) / 11d)]).ToArray();
        }
    }
    public string FrameHint => SelectedLog?.Frames is { Count: > 12 } frames
        ? $"显示 12 张代表图，完整证据已保存在本地日志（共 {frames.Count} 张）"
        : SelectedLog?.Frames is { Count: > 0 } framesWithImages ? $"共 {framesWithImages.Count} 张" : "本次没有截图证据";

    public HistoryWindow(MainViewModel model)
    {
        InitializeComponent(); this.model = model; DataContext = model;
        refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        refreshTimer.Tick += (_, _) => RefreshIfChanged();
        Closed += (_, _) => refreshTimer.Stop();
        refreshTimer.Start();
    }
    private void Delete_Click(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is RecordItem item) model.Delete(item); }
    private void RefreshIfChanged()
    {
        long? selectedId = SelectedRecord?.Id;
        if (!model.RefreshIfChanged()) return;
        if (selectedId.HasValue) RecordList.SelectedItem = model.Records.FirstOrDefault(item => item.Id == selectedId.Value);
        SelectFirst();
    }
    private void Record_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedRecord = RecordList.SelectedItem as RecordItem;
        SelectedLog = SelectedRecord == null ? null : aiLogs.FindByRecordId(SelectedRecord.Id);
    }
    public void SelectFirst() { if (RecordList.Items.Count > 0 && RecordList.SelectedIndex < 0) RecordList.SelectedIndex = 0; }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Header_Drag(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) { try { DragMove(); } catch (InvalidOperationException) { } } }
    private void Window_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || IsInteractive(e.OriginalSource as DependencyObject)) return;
        try { DragMove(); } catch (InvalidOperationException) { }
    }
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Keep the record list readable while allowing the detail pane to grow with the window.
        if (LayoutGrid == null) return;
        LayoutGrid.ColumnDefinitions[0].Width = e.NewSize.Width < 760 ? new GridLength(280) : new GridLength(330);
    }
    private static bool IsInteractive(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Button || source is ListBoxItem || source is ScrollBar || source is TextBox) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }
    private void Window_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { Close(); e.Handled = true; } }
    private void Frame_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Image { Source: BitmapSource source }) return;
        var preview = new ImagePreviewWindow(source) { Owner = this };
        preview.ShowDialog();
        e.Handled = true;
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
