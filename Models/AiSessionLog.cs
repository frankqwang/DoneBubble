using System;
namespace DoneBubble.Models;
using System.Collections.Generic;
public sealed record AiSessionLog(long Id, long? RecordId, DateTime CreatedAt, string Summary, string Category, double Confidence, string Prompt, string RawResponse, IReadOnlyList<string>? Frames = null)
{
    public string Heading => $"{CreatedAt:MM-dd HH:mm} · {Category} · {Summary}";
}
