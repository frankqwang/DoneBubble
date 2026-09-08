using System.Windows;
using System.Windows.Controls;
using DoneBubble.Models;
using DoneBubble.ViewModels;
namespace DoneBubble;
public partial class HistoryWindow : Window
{
    private readonly MainViewModel model;
    public HistoryWindow(MainViewModel model) { InitializeComponent(); this.model = model; DataContext = model; }
    private void Delete_Click(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is RecordItem item) model.Delete(item); }
    private void Refresh_Click(object sender, RoutedEventArgs e) => model.Refresh();
}
