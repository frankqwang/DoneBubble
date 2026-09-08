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
    public void Add(AiAnalysisResult result, long? recordId = null)
    {
        var items = Load(); var next = items.Count == 0 ? 1 : items[^1].Id + 1;
        string summary = result.Candidate?.Summary ?? "AI 未确认完成事项";
        string category = result.Candidate?.SuggestedCategory ?? "未确认";
        double confidence = result.Candidate?.Confidence ?? 0;
        items.Add(new AiSessionLog(next, recordId, DateTime.Now, summary, category, confidence, result.Prompt, result.RawResponse, result.Images));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(items, Options)); File.Move(path + ".tmp", path, true);
    }
    public AiSessionLog? FindByRecordId(long recordId)
    {
        foreach (var item in Load()) if (item.RecordId == recordId) return item;
        return null;
    }
}
