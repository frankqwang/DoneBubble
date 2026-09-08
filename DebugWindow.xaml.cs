using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows;
using DoneBubble.Models;
using DoneBubble.Services;
namespace DoneBubble;
public partial class DebugWindow : Window
{
    private readonly Settings settings;
    private readonly ActivityContextCollector collector = new();
    private readonly LocalAiService ai = new();
    private readonly WindowCaptureService capture = new();
    public DebugWindow(Settings settings) { InitializeComponent(); this.settings = settings; }
    private async void Collect_Click(object sender, RoutedEventArgs e) { var context = await CaptureUnderlyingWindowAsync(); ShowContext(context); Status.Text = context == null ? "未能读取目标窗口" : "已采集，未发送"; }
    private void Topmost_Changed(object sender, RoutedEventArgs e) => Topmost = TopmostToggle.IsChecked == true;
    private async void AnalyzeImage_Click(object sender, RoutedEventArgs e)
    {
        var captured = await CaptureUnderlyingWindowAndImageAsync();
        ShowContext(captured.Context);
        if (captured.Image == null) { Status.Text = "未能截图目标窗口"; return; }
        ScreenshotView.Source = ToImage(captured.Image); Status.Text = $"已截图 {captured.Image.Length / 1024} KB，正在请求视觉模型…";
        try
        {
            var result = await ai.AnalyzeImageAsync(Convert.ToBase64String(captured.Image), captured.Context?.PromptText ?? "未读取到文字上下文", settings);
            PromptBox.Text = result.Prompt; RawBox.Text = result.RawResponse;
            ResultBox.Text = result.Candidate == null ? (result.Error ?? "没有候选") : $"摘要：{result.Candidate.Summary}\n建议分类：{result.Candidate.SuggestedCategory}\n置信度：{result.Candidate.Confidence:0.00}";
            Status.Text = result.Error == null ? "视觉分析完成" : "分析完成，但没有可确认候选：" + result.Error;
        }
        catch (Exception ex) { Status.Text = "视觉请求失败：" + ex.Message; }
    }
    private async void Analyze_Click(object sender, RoutedEventArgs e)
    {
        var context = await CaptureUnderlyingWindowAsync(); ShowContext(context);
        if (context == null) { Status.Text = "未能读取当前窗口"; return; }
        Status.Text = "正在请求 LM Studio…";
        try
        {
            var result = await ai.AnalyzeAsync(context, settings);
            PromptBox.Text = result.Prompt; RawBox.Text = result.RawResponse;
            ResultBox.Text = result.Candidate == null ? (result.Error ?? "没有候选") : $"摘要：{result.Candidate.Summary}\n建议分类：{result.Candidate.SuggestedCategory}\n置信度：{result.Candidate.Confidence:0.00}\n活动时长：{result.Candidate.DurationText}";
            Status.Text = result.Error == null ? "分析完成" : "分析完成，但没有可确认候选：" + result.Error;
        }
        catch (Exception ex) { Status.Text = "请求失败：" + ex.Message; }
    }
    private async void AnalyzeSeries_Click(object sender, RoutedEventArgs e)
    {
        Status.Text = "正在采集最近 30 秒的窗口轨迹…";
        var captured = await CaptureSeriesAsync();
        var context = captured.Context;
        ShowContext(context);
        if (captured.Images.Count > 0) ScreenshotView.Source = ToImage(captured.Images[0]);
        if (context == null) { Status.Text = "未能读取当前窗口"; return; }
        Status.Text = "正在请求 LM Studio / DeepSeek…";
        try
        {
            var result = captured.Images.Count > 0
                ? await ai.AnalyzeImagesAsync(captured.Images.Select(Convert.ToBase64String).ToList(), context.PromptText, settings)
                : await ai.AnalyzeAsync(context, settings);
            PromptBox.Text = result.Prompt; RawBox.Text = result.RawResponse;
            ResultBox.Text = result.Candidate == null ? (result.Error ?? "没有候选") : $"摘要：{result.Candidate.Summary}\n建议分类：{result.Candidate.SuggestedCategory}\n置信度：{result.Candidate.Confidence:0.00}\n活动时长：{result.Candidate.DurationText}";
            Status.Text = result.Error == null ? "时间窗口分析完成" : "分析完成，但没有可确认候选：" + result.Error;
        }
        catch (Exception ex) { Status.Text = "请求失败：" + ex.Message; }
    }
    private void ShowContext(ActivityContext? context) { ContextBox.Text = context?.PromptText ?? "（未采集到上下文）"; }
    private async Task<ActivityContext?> CaptureUnderlyingWindowAsync()
    {
        bool wasVisible = IsVisible;
        var bubble = Application.Current.MainWindow;
        bool bubbleWasVisible = bubble?.IsVisible == true;
        if (wasVisible) Hide();
        if (bubbleWasVisible) bubble!.Hide();
        await Task.Delay(220);
        var context = collector.Capture(TimeSpan.FromMinutes(1));
        if (bubbleWasVisible) bubble!.Show();
        if (wasVisible) Show();
        return context;
    }
    private async Task<(ActivityContext? Context, byte[]? Image)> CaptureUnderlyingWindowAndImageAsync()
    {
        bool wasVisible = IsVisible;
        var bubble = Application.Current.MainWindow;
        bool bubbleWasVisible = bubble?.IsVisible == true;
        if (wasVisible) Hide();
        if (bubbleWasVisible) bubble!.Hide();
        await Task.Delay(220);
        var context = collector.Capture(TimeSpan.FromMinutes(1));
        var image = capture.CaptureForeground();
        if (bubbleWasVisible) bubble!.Show();
        if (wasVisible) Show();
        return (context, image);
    }
    private async Task<(ActivityContext? Context, List<byte[]> Images)> CaptureSeriesAsync()
    {
        bool wasVisible = IsVisible;
        var bubble = Application.Current.MainWindow;
        bool bubbleWasVisible = bubble?.IsVisible == true;
        if (wasVisible) Hide();
        if (bubbleWasVisible) bubble!.Hide();
        await Task.Delay(220);
        var samples = new List<ActivityContext>();
        var images = new List<byte[]>();
        for (int i = 0; i < 3; i++)
        {
            var sample = collector.Capture(TimeSpan.FromSeconds(i * 15));
            if (sample != null) samples.Add(sample);
            var image = capture.CaptureForeground();
            if (image != null) images.Add(image);
            if (i < 2) await Task.Delay(TimeSpan.FromSeconds(15));
        }
        if (bubbleWasVisible) bubble!.Show();
        if (wasVisible) Show();
        var last = samples.LastOrDefault();
        if (last == null) return (null, images);
        return (last with
        {
            Duration = TimeSpan.FromSeconds(30),
            RecentObservations = string.Join("\n", samples.Select(s => $"{s.CapturedAt:HH:mm:ss}｜{s.Application}｜{s.WindowTitle}｜焦点：{s.FocusedControl ?? "未知"}"))
        }, images);
    }
    private static BitmapImage ToImage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
    }
    protected override void OnClosed(EventArgs e) { ai.Dispose(); base.OnClosed(e); }
}
