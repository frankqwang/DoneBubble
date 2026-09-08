using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DoneBubble.Models;
using DoneBubble.Services;
namespace DoneBubble;
public partial class AiLogWindow : Window, INotifyPropertyChanged
{
    private static AiLogWindow? current;
    public ObservableCollection<AiSessionLog> Items { get; } = new();
    public bool IsEmpty => Items.Count == 0;
    private AiSessionLog? selected;
    public AiSessionLog? Selected { get => selected; set { selected = value; Changed(); } }
    public static void ShowLog(AiSessionLog log)
    {
        current ??= new AiLogWindow();
        current.SetLog(log);
        current.Show(); current.Activate();
    }
    public AiLogWindow() : this(null) { }
    public AiLogWindow(AiSessionLog? single) { InitializeComponent(); DataContext = this; if (single != null) Items.Add(single); else foreach (var item in new AiSessionLogService().Load().AsReadOnly()) Items.Add(item); if (Items.Count > 0) Selected = Items[^1]; }
    private void SetLog(AiSessionLog log) { Items.Clear(); Items.Add(log); Selected = log; }
    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
    private void Window_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { Hide(); e.Handled = true; } }
    private void Drag_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || IsInteractive(e.OriginalSource as DependencyObject)) return;
        try { DragMove(); } catch (InvalidOperationException) { }
    }
    private static bool IsInteractive(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Button || source is TextBox || source is ScrollBar || source is Expander) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
