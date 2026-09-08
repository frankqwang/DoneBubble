using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DoneBubble.Services;
using DoneBubble.ViewModels;
namespace DoneBubble;
// Explicit diagnostic mode uses an isolated database, never the user's records.
internal static class SelfTest
{
    public static int Run(string resultPath)
    {
        string directory = Path.Combine(Path.GetTempPath(), "DoneBubble-test-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(directory);
            using (var legacy = new SqliteConnection("Data Source=" + Path.Combine(directory, "test.db")))
            {
                legacy.Open();
                using var command = legacy.CreateCommand();
                command.CommandText = "CREATE TABLE Records (Id INTEGER PRIMARY KEY AUTOINCREMENT, Content TEXT NOT NULL, CreatedAt TEXT NOT NULL); INSERT INTO Records(Content, CreatedAt) VALUES ('旧记录', '2020-01-01 00:00:00')";
                command.ExecuteNonQuery();
            }
            var db = new DatabaseService(Path.Combine(directory, "test.db"));
            DateTime day = DateTime.Today;
            Check(db.GetToday().Count == 0, "database initialization");
            db.Add("昨天", day.AddTicks(-1)); db.Add("明天", day.AddDays(1));
            db.Add(" 修复客服超时问题 ", day); db.Add("Review PR ' ; --", day.AddHours(12));
            var today = db.GetToday();
            Check(today.Count == 2, "local date boundaries");
            Check(today[0].Content == "Review PR ' ; --" && today[1].Content == "修复客服超时问题", "ordering, trimming and SQL parameters");
            db.Delete(today[0].Id); Check(db.GetToday().Count == 1, "single deletion");
            var model = new MainViewModel(db, new SettingsService(directory)); model.Refresh();
            model.Draft = "  "; Check(!model.Save() && model.CountLabel == "1", "ignore blank input");
            model.Draft = "回复重要邮件"; Check(model.Save() && model.Draft == "" && model.CountLabel == "2", "save and count");
            // A directory used as the database file must fail without losing the draft.
            var failed = new MainViewModel(new DatabaseService(directory));
            failed.Refresh(); failed.Draft = "保留这句话";
            Check(!failed.Save() && failed.Draft == "保留这句话" && failed.HasError && failed.CountLabel == "—", "storage failure recovery");
            var settings = new SettingsService(directory);
            settings.Value.WindowX = 120; settings.Value.WindowY = 180; settings.Value.AutoStart = true; settings.Save();
            var restored = new SettingsService(directory); restored.Load();
            Check(restored.Value.WindowX == 120 && restored.Value.AutoStart, "settings persistence");
            var low = LoadColorService.ForPoints(1);
            var peak = LoadColorService.ForPoints(5);
            var next = LoadColorService.ForPoints(6);
            var high = LoadColorService.ForPoints(31);
            Check(low.G > low.R && peak.R > peak.G, "green to red within cycle");
            Check(next.G > peak.G && next != low && high.G < next.G, "cycle reset and cumulative load");
            Check(LoadColorService.ForPoints(0) == low && LoadColorService.ForPoints(10).R > LoadColorService.ForPoints(10).G, "zero and exact five-point boundaries");
            DateTimeOffset clock = new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);
            var restSettings = new SettingsService(Path.Combine(directory, "rest"));
            var rest = new RestViewModel(restSettings, () => clock);
            int finished = 0; rest.Finished += () => finished++;
            rest.Start(); Check(rest.RemainingText == "15:00", "rest starts at fifteen minutes");
            rest.Adjust(5); Check(rest.RemainingText == "20:00", "add five minutes");
            rest.Adjust(-5); Check(rest.RemainingText == "15:00", "subtract five minutes");
            clock = clock.AddMinutes(2); rest.Tick();
            Check(rest.RemainingText == "13:00", "elapsed wall clock");
            rest.Start(); Check(rest.RemainingText == "13:00", "active rest is not reset");
            var loadedRest = new SettingsService(Path.Combine(directory, "rest")); loadedRest.Load();
            Check(new RestViewModel(loadedRest, () => clock).RemainingText == "13:00", "rest restored after restart");
            clock = clock.AddMinutes(20); rest.Tick(); rest.Tick();
            Check(!rest.IsResting && finished == 1 && restSettings.Value.RestEndsAtUtc == null, "sleep expiry notifies once");
            rest.Start(); rest.Adjust(-5); rest.Adjust(-5); rest.Adjust(-5);
            Check(!rest.IsResting && rest.RemainingText == "00:00", "rest clamps at zero");
            var reminder = new BreakReminderService(settings);
            Check(!reminder.TryMarkReminder(4, day), "no reminder before five points");
            Check(reminder.TryMarkReminder(5, day), "reminder at five points");
            Check(!reminder.TryMarkReminder(7, day), "carry over remainder without repeat");
            var reminderSettings = new SettingsService(directory); reminderSettings.Load();
            Check(!new BreakReminderService(reminderSettings).TryMarkReminder(7, day), "reminder survives restart");
            Check(reminder.TryMarkReminder(11, day), "crossing ten triggers reminder");
            Check(!reminder.TryMarkReminder(9, day) && !reminder.TryMarkReminder(10, day), "deletion does not repeat milestone");
            Check(reminder.TryMarkReminder(15, day), "reminder at fifteen");
            Check(reminder.TryMarkReminder(7, day.AddDays(1)), "next day crossing five");
            int savedCount = 0; model.RecordSaved += count => savedCount = count;
            var window = new MainWindow(model, settings); window.Show(); window.Reveal();
            Snapshot(window, resultPath + ".bubble.png");
            typeof(MainWindow).GetMethod("Expand", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            Snapshot(window, resultPath + ".input.png");
            ((Button)window.FindName("LightButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(savedCount == 3 && model.CountLabel == "3" && window.Width == 76 && db.GetToday().Any(x => x.Category == "轻" && x.Content == ""), "empty-note category click");
            model.Save("中"); model.Draft = "事故排查"; model.Save("重");
            Check(db.GetToday().Any(x => x.Category == "中" && x.Content == "") && db.GetToday().Any(x => x.DisplayContent == "重 · 3 分 · 事故排查"), "all categories and optional note");
            Check(model.TotalPoints == 8 && savedCount == 8, "weighted total and save event");
            Check(db.GetToday(new DateTime(2020, 1, 1)).Single().Content == "旧记录", "legacy records preserved");
            typeof(MainWindow).GetMethod("Expand", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            ((TextBox)window.FindName("Input")).Focus();
            model.Draft = "Enter 自动保存验证";
            window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, Key.Enter) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Check(model.Draft == "" && model.CountLabel == "6" && window.Width == 76, "Enter saves and collapses");
            typeof(MainWindow).GetMethod("Expand", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            ((Button)window.FindName("CollapseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(window.IsVisible && window.Width == 76 && ((Border)window.FindName("Bubble")).Visibility == Visibility.Visible, "card close keeps bubble visible");
            model.Rest.Start();
            var restWindow = new RestWindow(model.Rest, settings); restWindow.Sync(true, 210, 180);
            typeof(MainWindow).GetMethod("Expand", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            ((Button)restWindow.FindName("RestPlusButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(model.Rest.Remaining.TotalMinutes > 19, "rest plus button");
            ((Button)restWindow.FindName("RestMinusButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(model.Rest.Remaining.TotalMinutes <= 15 && model.Rest.Remaining.TotalMinutes > 14, "rest minus button");
            Snapshot(restWindow, resultPath + ".rest.png");
            Check(restWindow.IsVisible && restWindow.ShowActivated == false, "independent non-activating rest window");
            restWindow.DisposeWindow();
            window.Collapse(); Snapshot(window, resultPath + ".rest-bubble.png");
            window.Close(); Check(!window.IsVisible, "close hides without exiting"); window.Stop();
            var history = new HistoryWindow(model); history.Show(); Snapshot(history, resultPath + ".history.png"); history.Close();
            File.WriteAllText(resultPath, "PASS: rest timer start/adjust/restart/expiry, rest buttons, load palette cycles and cumulative load, break reminder threshold, weighted scoring, every five points, milestone deduplication, restart persistence, next-day reset, save notification, legacy migration, category click without text, all categories, optional note, initialization, local date boundaries, ordering, parameterization, deletion, blank input, save/count, failure recovery, settings persistence, WPF windows loaded, Enter saves and collapses, card close keeps bubble visible, window close hides.");
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(resultPath, "FAIL: " + ex); return 1; }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); try { Directory.Delete(directory, true); } catch { } }
    }
    private static void Snapshot(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); }
}
