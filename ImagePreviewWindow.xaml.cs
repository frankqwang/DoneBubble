using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DoneBubble;

public partial class ImagePreviewWindow : Window
{
    public ImagePreviewWindow(ImageSource source)
    {
        InitializeComponent();
        PreviewImage.Source = source;
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { Close(); e.Handled = true; } }
    private void Header_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) { try { DragMove(); } catch (InvalidOperationException) { } }
    }
}
