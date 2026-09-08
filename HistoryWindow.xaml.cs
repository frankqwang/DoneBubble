using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DoneBubble.Models;
using DoneBubble.ViewModels;
namespace DoneBubble;
public partial class HistoryWindow : Window
{
    private readonly MainViewModel model;
    public event Action? AiLogsRequested;
    public HistoryWindow(MainViewModel model) { InitializeComponent(); this.model = model; DataContext = model; }
    private void Delete_Click(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is RecordItem item) model.Delete(item); }
    private void Refresh_Click(object sender, RoutedEventArgs e) => model.Refresh();
    private void AiLogs_Click(object sender, RoutedEventArgs e) => AiLogsRequested?.Invoke();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Header_Drag(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) { try { DragMove(); } catch (InvalidOperationException) { } } }
    private void Window_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || IsInteractive(e.OriginalSource as DependencyObject)) return;
        try { DragMove(); } catch (InvalidOperationException) { }
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
}
