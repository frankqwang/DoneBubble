using System;
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
        Status.Text = $"已截图 {captured.Image.Length / 1024} KB，正在请求视觉模型…";
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
    private void ShowContext(ActivityContext? context) { ContextBox.Text = context?.PromptText ?? "（未采集到上下文）"; }
    private async Task<ActivityContext?> CaptureUnderlyingWindowAsync()
    {
        bool wasVisible = IsVisible;
        if (wasVisible) Hide();
        await Task.Delay(220);
        var context = collector.Capture(TimeSpan.FromMinutes(1));
        if (wasVisible) Show();
        return context;
    }
    private async Task<(ActivityContext? Context, byte[]? Image)> CaptureUnderlyingWindowAndImageAsync()
    {
        bool wasVisible = IsVisible;
        if (wasVisible) Hide();
        await Task.Delay(220);
        var context = collector.Capture(TimeSpan.FromMinutes(1));
        var image = capture.CaptureForeground();
        if (wasVisible) Show();
        return (context, image);
    }
    protected override void OnClosed(EventArgs e) { ai.Dispose(); base.OnClosed(e); }
}
