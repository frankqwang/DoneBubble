using System;
namespace DoneBubble.Models;
public sealed record ActivityContext(string Application, int ProcessId, string WindowTitle, string WindowClass, string? ExecutablePath, string? FocusedControl, string? FocusedText, TimeSpan Duration, DateTime CapturedAt, string RecentObservations = "", string? ClipboardText = null, double IdleSeconds = 0)
{
    public string PromptText => $"采集时间：{CapturedAt:yyyy-MM-dd HH:mm:ss}\n应用：{Application}（PID {ProcessId}）\n窗口：{WindowTitle}\n窗口类名：{WindowClass}\n程序路径：{ExecutablePath ?? "未知"}\n焦点控件：{FocusedControl ?? "未读取到"}\n当前控件可见文本摘录：{FocusedText ?? "未读取到"}\n剪贴板文本摘要：{ClipboardText ?? "未读取到或不是文本"}\n有效活动时长：{Duration.TotalMinutes:0.#} 分钟\n最近输入空闲：{IdleSeconds:0.#} 秒\n窗口期间观察轨迹：{(string.IsNullOrWhiteSpace(RecentObservations) ? "只有一次采样，不能据此推断持续工作内容" : RecentObservations)}";
}
