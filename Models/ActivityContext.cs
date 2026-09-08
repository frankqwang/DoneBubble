using System;
namespace DoneBubble.Models;
public sealed record ActivityContext(string Application, string WindowTitle, string? ExecutablePath, string? FocusedText, TimeSpan Duration)
{
    public string PromptText => $"应用：{Application}\n窗口：{WindowTitle}\n程序路径：{ExecutablePath ?? "未知"}\n当前控件可见文本摘录：{FocusedText ?? "未读取到"}\n有效活动时长：{Duration.TotalMinutes:0.#} 分钟";
}
