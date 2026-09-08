using System;
namespace DoneBubble.Models;
public sealed record AiSessionLog(long Id, long? RecordId, DateTime CreatedAt, string Summary, string Category, double Confidence, string Prompt, string RawResponse)
{
    public string Heading => $"{CreatedAt:MM-dd HH:mm} · {Category} · {Summary}";
}
