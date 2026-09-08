using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using DoneBubble.Models;
using DoneBubble.Services;
using System.Windows.Documents;
namespace DoneBubble;
public partial class AiLogWindow : Window, INotifyPropertyChanged
{
    private static AiLogWindow? current;
    public ObservableCollection<AiSessionLog> Items { get; } = new();
    public bool IsEmpty => Items.Count == 0;
    private AiSessionLog? selected;
    public AiSessionLog? Selected { get => selected; set { selected = value; Changed(); RenderRawOutput(); } }
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
    private void RenderRawOutput()
    {
        if (RawOutputBox == null) return;
        string text = selected?.RawResponse ?? "";
        try
        {
            using var document = JsonDocument.Parse(text);
            text = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch { }

        var flow = new FlowDocument { PagePadding = new Thickness(0) };
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        var pattern = new Regex("(\\\"(?:\\\\.|[^\\\"\\\\])*\\\")(\\s*:)?|\\b(?:true|false|null)\\b|-?\\b\\d+(?:\\.\\d+)?\\b", RegexOptions.Compiled);
        int cursor = 0;
        foreach (Match match in pattern.Matches(text))
        {
            if (match.Index > cursor) paragraph.Inlines.Add(new Run(text[cursor..match.Index]) { Foreground = new SolidColorBrush(Color.FromRgb(220, 229, 238)) });
            bool isString = match.Value.StartsWith("\\\"");
            bool isKey = isString && match.Groups[2].Success;
            var color = isKey ? Color.FromRgb(126, 203, 214) : isString ? Color.FromRgb(196, 220, 174) : match.Value is "true" or "false" or "null" ? Color.FromRgb(220, 180, 120) : Color.FromRgb(220, 200, 150);
            paragraph.Inlines.Add(new Run(match.Value) { Foreground = new SolidColorBrush(color) });
            cursor = match.Index + match.Length;
        }
        if (cursor < text.Length) paragraph.Inlines.Add(new Run(text[cursor..]) { Foreground = new SolidColorBrush(Color.FromRgb(220, 229, 238)) });
        flow.Blocks.Add(paragraph);
        RawOutputBox.Document = flow;
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
