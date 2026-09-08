using System;
using System.Linq;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DoneBubble.Models;
using DoneBubble.Services;
namespace DoneBubble.ViewModels;
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService database;
    private string draft = "", error = "";
    private bool available;
    public MainViewModel(DatabaseService database, SettingsService? settings = null)
    {
        this.database = database;
        Rest = new RestViewModel(settings ?? new SettingsService());
    }
    public RestViewModel Rest { get; }
    private ActivityCandidate? candidate;
    public ActivityCandidate? Candidate { get => candidate; private set { candidate = value; Changed(); Changed(nameof(HasCandidate)); Changed(nameof(CandidateDetails)); } }
    public bool HasCandidate => Candidate != null;
    public string CandidateDetails => Candidate == null ? "" : $"建议：{Candidate.SuggestedCategory} · {Candidate.DurationText} · {Candidate.ConfidenceText}";
    public ObservableCollection<RecordItem> Records { get; } = new();
    public string Draft { get => draft; set { draft = value; Changed(); } }
    public string Error { get => error; set { error = value; Changed(); Changed(nameof(HasError)); } }
    public bool HasError => Error.Length > 0;
    public Brush BubbleBrush
    {
        get
        {
            var brush = new SolidColorBrush(available ? LoadColorService.ForPoints(TotalPoints) : Color.FromRgb(157, 164, 172));
            brush.Freeze();
            return brush;
        }
    }
    public string BubbleHint => available ? $"今天 {Records.Count} 件 · {TotalPoints} 分 · 每 5 分休息一下" : "记录暂时不可用，点击重试";
    public string CountLabel => available ? Records.Count.ToString() : "—";
    public int TotalPoints => Records.Sum(item => item.Points);
    public string Heading => available ? $"今天 {Records.Count} 件 · {TotalPoints} 分" : "今天的记录暂时不可用";
    public bool IsEmpty => available && Records.Count == 0;
    public void Refresh()
    {
        try
        {
            var items = database.GetToday();
            ApplyRecords(items);
            available = true; Error = "";
        }
        catch (Exception) { available = false; Error = "无法读取本地记录，请检查磁盘空间或文件权限后重试。"; }
        NotifyCounts();
    }
    public bool RefreshIfChanged()
    {
        try
        {
            var items = database.GetToday();
            if (Records.SequenceEqual(items)) return false;
            ApplyRecords(items); available = true; Error = ""; NotifyCounts(); return true;
        }
        catch { return false; }
    }
    private void ApplyRecords(System.Collections.Generic.IReadOnlyList<RecordItem> items)
    {
        Records.Clear(); foreach (var item in items) Records.Add(item);
    }
    public event Action<int>? RecordSaved;
    public long LastSavedId { get; private set; }
    public void ShowCandidate(ActivityCandidate value) => Candidate = value;
    public void DismissCandidate() => Candidate = null;
    public bool AcceptCandidate() { if (Candidate == null) return false; Draft = Candidate.Summary; var category = Candidate.SuggestedCategory; Candidate = null; return Save(category); }
    public bool Save(string? category = null)
    {
        if (category == null && string.IsNullOrWhiteSpace(Draft)) return false;
        try { LastSavedId = database.Add(Draft, category: category); }
        catch (Exception) { Error = "保存失败，输入已保留。请检查磁盘空间或权限后重试。"; return false; }
        Draft = ""; Refresh();
        if (available) RecordSaved?.Invoke(TotalPoints);
        return true;
    }
    public void Delete(RecordItem item)
    {
        try { database.Delete(item.Id); Refresh(); }
        catch (Exception) { Error = "删除失败，请稍后重试。"; }
    }
    public void UpdateContent(long id, string content)
    {
        try { database.UpdateContent(id, content); Refresh(); }
        catch (Exception) { /* The original record remains valid if an AI backfill fails. */ }
    }
    private void NotifyCounts() { Changed(nameof(BubbleBrush)); Changed(nameof(BubbleHint)); Changed(nameof(TotalPoints)); Changed(nameof(CountLabel)); Changed(nameof(Heading)); Changed(nameof(IsEmpty)); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
