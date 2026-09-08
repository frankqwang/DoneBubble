using System;
using System.IO;
using System.Text.Json;
namespace DoneBubble.Services;
public sealed class Settings
{
    public DateTimeOffset? RestEndsAtUtc { get; set; }
    public double? RestWindowX { get; set; }
    public double? RestWindowY { get; set; }
    public double WindowX { get; set; } = 80;
    public double WindowY { get; set; } = 160;
    public int LastScoreReminderPoints { get; set; }
    public string? LastScoreReminderDate { get; set; }
    public int LastBreakReminderCount { get; set; }
    public string? LastBreakReminderDate { get; set; }
    public bool AutoStart { get; set; }
    public bool AiAssistEnabled { get; set; }
    public string AiEndpoint { get; set; } = "http://127.0.0.1:1234/v1/chat/completions";
    public string AiModel { get; set; } = "qwen3.5-4b";
    // Stored only in the per-user settings file; never commit this value to source control.
    public string AiApiKey { get; set; } = "";
}
public sealed class SettingsService
{
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DoneBubble");
    private readonly string path;
    public SettingsService(string? directory = null) => path = Path.Combine(directory ?? DataDirectory, "settings.json");
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public Settings Value { get; private set; } = new();
    public string? Load()
    {
        try { if (File.Exists(path)) Value = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), Options) ?? new(); return null; }
        catch (Exception ex) { return "设置读取失败，使用默认位置。" + ex.Message; }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(Value, Options));
        File.Move(path + ".tmp", path, true);
    }
}
