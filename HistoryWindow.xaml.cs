using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
    public RecordItem? SelectedRecord { get => selectedRecord; private set { selectedRecord = value; Changed(); } }
    public AiSessionLog? SelectedLog { get => selectedLog; private set { selectedLog = value; Changed(); Changed(nameof(HasSelectedLog)); Changed(nameof(HasNoSelectedLog)); } }
    public bool HasSelectedLog => SelectedLog != null;
    public bool HasNoSelectedLog => SelectedLog == null;

    public HistoryWindow(MainViewModel model) { InitializeComponent(); this.model = model; DataContext = model; }
    private void Delete_Click(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is RecordItem item) model.Delete(item); }
    private void Refresh_Click(object sender, RoutedEventArgs e) { model.Refresh(); SelectFirst(); }
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
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
