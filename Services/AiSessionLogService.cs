using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DoneBubble.Models;
namespace DoneBubble.Services;
public sealed class AiSessionLogService
{
    private readonly string path = Path.Combine(SettingsService.DataDirectory, "ai-sessions.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public List<AiSessionLog> Load()
    {
        try { if (!File.Exists(path)) return new(); return JsonSerializer.Deserialize<List<AiSessionLog>>(File.ReadAllText(path), Options) ?? new(); }
        catch { return new(); }
    }
    public void Add(AiAnalysisResult result)
    {
        if (result.Candidate == null) return;
        var items = Load(); var next = items.Count == 0 ? 1 : items[^1].Id + 1;
        items.Add(new AiSessionLog(next, DateTime.Now, result.Candidate.Summary, result.Candidate.SuggestedCategory, result.Candidate.Confidence, result.Prompt, result.RawResponse));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(items, Options)); File.Move(path + ".tmp", path, true);
    }
}
