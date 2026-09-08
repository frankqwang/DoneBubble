using System;
namespace DoneBubble.Models;
public sealed record ActivityContext(string Application, int ProcessId, string WindowTitle, string WindowClass, string? ExecutablePath, string? FocusedControl, string? FocusedText, TimeSpan Duration, DateTime CapturedAt)
{
    public string PromptText => $"采集时间：{CapturedAt:yyyy-MM-dd HH:mm:ss}\n应用：{Application}（PID {ProcessId}）\n窗口：{WindowTitle}\n窗口类名：{WindowClass}\n程序路径：{ExecutablePath ?? "未知"}\n焦点控件：{FocusedControl ?? "未读取到"}\n当前控件可见文本摘录：{FocusedText ?? "未读取到"}\n有效活动时长：{Duration.TotalMinutes:0.#} 分钟";
}
