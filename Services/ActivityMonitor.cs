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
    private readonly WindowCaptureService windowCapture = new();
    private readonly List<byte[]> frames = new();
    private byte[]? lastKeptFrame;
    private DateTime lastFrameKeptAt;
    private string application = "", title = "";
    private CapturedActivityContext? latestContext;
    private DateTime started, lastCandidate = DateTime.MinValue;
    private bool busy, paused;
    private Task<AiAnalysisResult?>? pendingAnalysisTask;
    private readonly List<string> observations = new();
    public event Action<ActivityCandidate>? CandidateFound;
    public ActivityMonitor(SettingsService settings, LocalAiService ai) { this.settings = settings; this.ai = ai; timer = new(Tick, null, 5000, Timeout.Infinite); }
    private async void Tick(object? state)
    {
        try
        {
            if (busy || !settings.Value.AiAssistEnabled) return;
            if (GetIdleSeconds() > 90) { MarkIdle(); return; }
            string nextApplication = GetForegroundApplication(); string nextTitle = GetForegroundTitle();
            if (string.IsNullOrWhiteSpace(nextApplication) || nextApplication.Equals("DoneBubble", StringComparison.OrdinalIgnoreCase)) return;
            if (paused) { ResetSession(); paused = false; }
            if (!nextApplication.Equals(application, StringComparison.OrdinalIgnoreCase) || !SameTopic(title, nextTitle))
            {
                await ConsiderAsync(); application = nextApplication; title = nextTitle; started = DateTime.Now; latestContext = null; observations.Clear();
            }
            else if (started == default)
            {
                started = DateTime.Now;
                latestContext = collector.Capture(TimeSpan.Zero) ?? latestContext;
                AddObservation(latestContext);
                CaptureFrame();
            }
            else
            {
                latestContext = collector.Capture(DateTime.Now - started) ?? latestContext;
                AddObservation(latestContext);
                CaptureFrame();
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
        if (pendingAnalysisTask != null) return await pendingAnalysisTask.ConfigureAwait(false);
        if (busy) return new AiAnalysisResult("", "", null, "AI 正在处理另一段活动");
        if (started == default || latestContext == null)
            return new AiAnalysisResult("", "", null, "没有可总结的活动 session");
        return await AnalyzeCurrentSessionAsync().ConfigureAwait(false);
    }
    private async Task<AiAnalysisResult?> AnalyzeCurrentSessionAsync()
    {
        if (started == default || latestContext == null) return new AiAnalysisResult("", "", null, "没有可总结的活动 session");
        var context = latestContext with { RecentObservations = string.Join("\n", observations) };
        // Reset the live session before awaiting the model, but keep its evidence for
        // this request and for the AI log. ResetSession clears the live frame buffer.
        var sessionFrames = frames.ConvertAll(frame => frame);
        var analysisFrames = SelectRepresentativeFrames(sessionFrames, 3).ConvertAll(frame => windowCapture.OptimizeForModel(frame));
        ResetSession();
        busy = true;
        try
        {
            var result = analysisFrames.Count > 0
                ? await ai.AnalyzeImagesAsync(analysisFrames.ConvertAll(frame => Convert.ToBase64String(frame)), context.PromptText, settings.Value).ConfigureAwait(false)
                : await ai.AnalyzeAsync(context, settings.Value, default, true).ConfigureAwait(false);
            if (sessionFrames.Count > 0) result = result with { Images = sessionFrames.ConvertAll(Convert.ToBase64String) };
            if (result.Candidate != null) lastCandidate = DateTime.Now;
            return result;
        }
        catch (Exception ex)
        {
            // Keep a diagnostic AI log even when the model times out or rejects the payload.
            return new AiAnalysisResult(context.PromptText, "", null, "AI 请求失败：" + ex.Message, sessionFrames.ConvertAll(Convert.ToBase64String));
        }
        finally { busy = false; ScheduleNext(); }
    }
    private async Task ConsiderAsync()
    {
        // Three minutes is long enough to filter out accidental window switches while
        // still catching short replies, reviews, and focused troubleshooting sessions.
        if (started == default || (DateTime.Now - started).TotalMinutes < 3 || lastCandidate.Date == DateTime.Today && lastCandidate >= started) return;
        busy = true;
        try { var context = latestContext; if (context != null) context = context with { RecentObservations = string.Join("\n", observations) }; var result = context == null ? null : await ai.AnalyzeAsync(context, settings.Value); var candidate = result?.Candidate; if (candidate != null) { lastCandidate = DateTime.Now; CandidateFound?.Invoke(candidate with { Analysis = result }); } }
        catch { /* AI is optional; an unavailable local server is silent. */ }
        finally { busy = false; }
    }
    private void MarkIdle()
    {
        if (started == default || paused || pendingAnalysisTask != null) return;
        paused = true;
        pendingAnalysisTask = AnalyzeCurrentSessionAsync();
        _ = CompleteIdleAnalysisAsync(pendingAnalysisTask);
    }
    private async Task CompleteIdleAnalysisAsync(Task<AiAnalysisResult?> task)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            if (result?.Candidate != null)
            {
                lastCandidate = DateTime.Now;
                CandidateFound?.Invoke(result.Candidate with { Analysis = result });
            }
        }
        catch { /* The manual record path will show a diagnostic if it is used. */ }
        finally { pendingAnalysisTask = null; }
    }
    private void ResetSession() { application = title = ""; started = default; latestContext = null; observations.Clear(); frames.Clear(); lastKeptFrame = null; lastFrameKeptAt = default; paused = false; }
    private void CaptureFrame()
    {
        try
        {
            var image = windowCapture.CaptureScreen();
            if (image == null) return;
            image = windowCapture.OptimizeForArchive(image);
            // A timer tick is not necessarily a meaningful change. Keep every
            // distinct view, but suppress near-identical frames so a quiet
            // session does not create hundreds of megabytes of evidence.
            if (lastKeptFrame != null && DateTime.Now - lastFrameKeptAt < TimeSpan.FromMinutes(2) && !IsMeaningfullyDifferent(lastKeptFrame, image)) return;
            frames.Add(image);
            lastKeptFrame = image;
            lastFrameKeptAt = DateTime.Now;
        }
        catch { }
    }
    private static bool IsMeaningfullyDifferent(byte[] previous, byte[] current)
    {
        try
        {
            using var a = new System.Drawing.Bitmap(new System.IO.MemoryStream(previous));
            using var b = new System.Drawing.Bitmap(new System.IO.MemoryStream(current));
            const int width = 32, height = 18;
            double difference = 0;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                var pa = a.GetPixel(x * a.Width / width, y * a.Height / height);
                var pb = b.GetPixel(x * b.Width / width, y * b.Height / height);
                difference += (Math.Abs(pa.R - pb.R) + Math.Abs(pa.G - pb.G) + Math.Abs(pa.B - pb.B)) / (255d * 3d);
            }
            return difference / (width * height) > 0.018;
        }
        catch { return true; }
    }
    private static List<byte[]> SelectRepresentativeFrames(List<byte[]> all, int max)
    {
        if (all.Count <= max) return all;
        var selected = new List<byte[]>(max);
        for (int i = 0; i < max; i++)
        {
            int index = (int)Math.Round(i * (all.Count - 1d) / (max - 1));
            selected.Add(all[index]);
        }
        return selected;
    }
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
