using System;
namespace DoneBubble.Models;
public sealed record ActivityCandidate(string Summary, string SuggestedCategory, double Confidence, TimeSpan Duration)
{
    public string DurationText => Duration.TotalMinutes >= 60 ? $"{Duration.TotalHours:0.#} 小时" : $"{Math.Max(1, (int)Math.Round(Duration.TotalMinutes))} 分钟";
    public string ConfidenceText => Confidence >= .75 ? "较有把握" : Confidence >= .5 ? "可以确认" : "不太确定";
}
