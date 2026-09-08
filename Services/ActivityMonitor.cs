using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DoneBubble.Models;
using CapturedActivityContext = DoneBubble.Models.ActivityContext;
namespace DoneBubble.Services;
public sealed class ActivityMonitor : IDisposable
{
    private readonly SettingsService settings;
    private readonly LocalAiService ai;
    private readonly Timer timer;
    private readonly ActivityContextCollector collector = new();
    private string application = "", title = "";
    private CapturedActivityContext? latestContext;
    private DateTime started, lastCandidate = DateTime.MinValue;
    private bool busy;
    private readonly List<string> observations = new();
    public event Action<ActivityCandidate>? CandidateFound;
    public ActivityMonitor(SettingsService settings, LocalAiService ai) { this.settings = settings; this.ai = ai; timer = new(Tick, null, 5000, Timeout.Infinite); }
    private async void Tick(object? state)
    {
        try
        {
            if (busy || !settings.Value.AiAssistEnabled || GetIdleSeconds() > 90) { ResetIfIdle(); return; }
            string nextApplication = GetForegroundApplication(); string nextTitle = GetForegroundTitle();
            if (string.IsNullOrWhiteSpace(nextApplication) || nextApplication.Equals("DoneBubble", StringComparison.OrdinalIgnoreCase)) return;
            if (!nextApplication.Equals(application, StringComparison.OrdinalIgnoreCase) || !SameTopic(title, nextTitle))
            {
                await ConsiderAsync(); application = nextApplication; title = nextTitle; started = DateTime.Now; latestContext = null; observations.Clear();
            }
            else if (started == default)
            {
                started = DateTime.Now;
                latestContext = collector.Capture(TimeSpan.Zero) ?? latestContext;
                AddObservation(latestContext);
            }
            else
            {
                latestContext = collector.Capture(DateTime.Now - started) ?? latestContext;
                AddObservation(latestContext);
            }
        }
        finally
        {
            ScheduleNext();
        }
    }
    private void ScheduleNext()
    {
        double minutes = started == default ? 0 : (DateTime.Now - started).TotalMinutes;
        int seconds = minutes switch { <= 1 => 5, <= 5 => 10, <= 15 => 20, <= 30 => 30, _ => 60 };
        try { timer.Change(TimeSpan.FromSeconds(seconds), Timeout.InfiniteTimeSpan); } catch (ObjectDisposedException) { }
    }
    public async Task<AiAnalysisResult?> FlushSessionAsync()
    {
        if (busy || started == default || latestContext == null) return null;
        var context = latestContext with { RecentObservations = string.Join("\n", observations) };
        ResetSession();
        busy = true;
        try
        {
            var result = await ai.AnalyzeAsync(context, settings.Value).ConfigureAwait(false);
            if (result.Candidate != null) lastCandidate = DateTime.Now;
            return result;
        }
        catch { return null; }
        finally { busy = false; ScheduleNext(); }
    }
    private async Task ConsiderAsync()
    {
        if (started == default || (DateTime.Now - started).TotalMinutes < 10 || lastCandidate.Date == DateTime.Today && lastCandidate >= started) return;
        busy = true;
        try { var context = latestContext; if (context != null) context = context with { RecentObservations = string.Join("\n", observations) }; var candidate = context == null ? null : await ai.JudgeAsync(context, settings.Value); if (candidate != null) { lastCandidate = DateTime.Now; CandidateFound?.Invoke(candidate); } }
        catch { /* AI is optional; an unavailable local server is silent. */ }
        finally { busy = false; }
    }
    private void ResetIfIdle() { if (GetIdleSeconds() > 90) ResetSession(); }
    private void ResetSession() { application = title = ""; started = default; latestContext = null; observations.Clear(); }
    private void AddObservation(CapturedActivityContext? context)
    {
        if (context == null) return;
        string sample = $"{context.CapturedAt:HH:mm:ss}｜{context.Application}｜{context.WindowTitle}｜焦点：{context.FocusedControl ?? "未知"}";
        if (!observations.Contains(sample)) { observations.Add(sample); if (observations.Count > 8) observations.RemoveAt(0); }
    }
    private static bool SameTopic(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase) || (a.Length > 0 && b.Contains(a, StringComparison.OrdinalIgnoreCase));
    private static string GetForegroundApplication() { IntPtr handle = GetForegroundWindow(); GetWindowThreadProcessId(handle, out uint id); try { using var process = Process.GetProcessById((int)id); return process.ProcessName; } catch { return ""; } }
    private static string GetForegroundTitle() { var buffer = new StringBuilder(512); GetWindowText(GetForegroundWindow(), buffer, buffer.Capacity); return buffer.ToString().Trim(); }
    private static double GetIdleSeconds() { var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() }; return GetLastInputInfo(ref info) ? (Environment.TickCount64 - info.dwTime) / 1000d : 0; }
    public void Dispose() { timer.Dispose(); ai.Dispose(); }
    [StructLayout(LayoutKind.Sequential)] private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
}
