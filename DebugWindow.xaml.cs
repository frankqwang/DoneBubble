using System;
using System.Threading.Tasks;
using System.Windows;
using DoneBubble.Models;
using DoneBubble.Services;
namespace DoneBubble;
public partial class DebugWindow : Window
{
    private readonly Settings settings;
    private readonly ActivityContextCollector collector = new();
    private readonly LocalAiService ai = new();
    public DebugWindow(Settings settings) { InitializeComponent(); this.settings = settings; }
    private void Collect_Click(object sender, RoutedEventArgs e) { var context = collector.Capture(TimeSpan.FromMinutes(1)); ShowContext(context); Status.Text = context == null ? "未能读取当前窗口" : "已采集，未发送"; }
    private async void Analyze_Click(object sender, RoutedEventArgs e)
    {
        var context = collector.Capture(TimeSpan.FromMinutes(1)); ShowContext(context);
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
    protected override void OnClosed(EventArgs e) { ai.Dispose(); base.OnClosed(e); }
}
