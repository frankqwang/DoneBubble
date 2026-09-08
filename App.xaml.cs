using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using DoneBubble.Services;
using DoneBubble.ViewModels;
using Forms = System.Windows.Forms;
namespace DoneBubble;
public partial class App : Application
{
    private Mutex? instance;
    private Forms.NotifyIcon? tray;
    private Icon? trayIcon;
    private MainWindow? bubble;
    private HistoryWindow? history;
    private DebugWindow? debug;
    private readonly SettingsService settings = new();
    private readonly StartupService startup = new();
    private MainViewModel model = null!;
    private ActivityMonitor? activityMonitor;
    public bool IsExiting { get; private set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--self-test")
        {
            int code = SelfTest.Run(e.Args[1]); Shutdown(code); return;
        }
        instance = new Mutex(true, @"Local\DoneBubble.Desktop", out bool first);
        if (!first) { MessageBox.Show("DoneBubble 已在运行，请从系统托盘显示气泡。", "DoneBubble"); Shutdown(); return; }
        string? settingsError = settings.Load();
        model = new MainViewModel(new DatabaseService(), settings); model.Refresh();
        if (settingsError != null) model.Error = settingsError;
        bubble = new MainWindow(model, settings); MainWindow = bubble;
        bubble.HistoryRequested += ShowHistory;
        CreateTray();
        activityMonitor = new ActivityMonitor(settings, new LocalAiService());
        activityMonitor.CandidateFound += candidate => Dispatcher.BeginInvoke(new Action(() => model.ShowCandidate(candidate)));
        model.Rest.Finished += () => tray?.ShowBalloonTip(4000, "休息时间到",
            "休息倒计时结束了，按自己的节奏继续。", Forms.ToolTipIcon.Info);
        var reminder = new BreakReminderService(settings);
        model.RecordSaved += points =>
        {
            if (!reminder.TryMarkReminder(points, DateTime.Today)) return;
            model.Rest.Start();
            // Queue a non-modal tray notification after the input card has collapsed.
            Dispatcher.BeginInvoke(new Action(() =>
                tray?.ShowBalloonTip(6000, "该摸会儿鱼了 🐟",
                    $"事务负荷又满 5 分了，休息一下，摸会儿鱼吧。今天累计 {points} 分。", Forms.ToolTipIcon.Info)));
        };
        bubble.Show();
    }
    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示 / 隐藏", null, (_, _) => { if (bubble!.IsVisible) { bubble.Collapse(); bubble.Hide(); } else bubble.Reveal(); });
        menu.Items.Add("今日记录", null, (_, _) => ShowHistory());
        var auto = new Forms.ToolStripMenuItem("开机启动");
        try { auto.Checked = startup.IsEnabled(); settings.Value.AutoStart = auto.Checked; settings.Save(); }
        catch { model.Error = "无法读取或保存开机启动设置。"; }
        auto.Click += (_, _) =>
        {
            try
            {
                startup.SetEnabled(!auto.Checked); auto.Checked = startup.IsEnabled();
                settings.Value.AutoStart = auto.Checked; settings.Save();
            }
            catch (Exception ex) { MessageBox.Show("开机启动设置未能完成：" + ex.Message, "DoneBubble"); }
        };
        menu.Items.Add(auto); menu.Items.Add(new Forms.ToolStripSeparator());
        var ai = new Forms.ToolStripMenuItem("AI 活动识别（本地）") { Checked = settings.Value.AiAssistEnabled, CheckOnClick = true };
        ai.Click += (_, _) => { settings.Value.AiAssistEnabled = ai.Checked; try { settings.Save(); } catch { } };
        menu.Items.Add(ai);
        menu.Items.Add("AI 调试窗口", null, (_, _) => { debug ??= new DebugWindow(settings.Value); debug.Closed += (_, _) => debug = null; debug.Show(); debug.Activate(); });
        menu.Items.Add("退出", null, (_, _) => { IsExiting = true; bubble!.Stop(); history?.Close(); debug?.Close(); Shutdown(); });
        trayIcon = MakeIcon();
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "DoneBubble · 记录已经处理的事", ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, args) => { if (args.Button == Forms.MouseButtons.Left) bubble!.Reveal(); };
    }
    private void ShowHistory()
    {
        model.Refresh();
        if (history == null) { history = new HistoryWindow(model); history.Closed += (_, _) => history = null; }
        history.Show(); history.Activate();
    }
    private static Icon MakeIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using var fill = new SolidBrush(Color.FromArgb(37, 52, 65)); graphics.FillEllipse(fill, 1, 1, 30, 30);
            using var pen = new Pen(Color.FromArgb(183, 223, 202), 3) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            graphics.DrawLines(pen, new[] { new System.Drawing.Point(9, 16), new System.Drawing.Point(14, 21), new System.Drawing.Point(23, 11) });
        }
        var handle = bitmap.GetHicon();
        try { using var icon = Icon.FromHandle(handle); return (Icon)icon.Clone(); }
        finally { DestroyIcon(handle); }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
    protected override void OnExit(ExitEventArgs e)
    {
        IsExiting = true; bubble?.Stop();
        activityMonitor?.Dispose();
        if (tray != null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        trayIcon?.Dispose(); instance?.Dispose(); base.OnExit(e);
    }
}
