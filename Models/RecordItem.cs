using System;
namespace DoneBubble.Models;
public sealed record RecordItem(long Id, string Content, DateTime CreatedAt, string? Category = null)
{
    // Legacy text-only records count as one point.
    public int Points => Category switch { "中" => 2, "重" => 3, _ => 1 };
    public string DisplayContent => Category == null ? $"{Content} · {Points} 分" : string.IsNullOrEmpty(Content) ? $"{Category} · {Points} 分" : $"{Category} · {Points} 分 · {Content}";
    public string Time => CreatedAt.ToString("HH:mm");
}
