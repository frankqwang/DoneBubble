using System;
using System.ComponentModel;
using DoneBubble.Services;
namespace DoneBubble.ViewModels;
public sealed class RestViewModel : INotifyPropertyChanged
{
    private readonly SettingsService settings;
    private readonly Func<DateTimeOffset> now;
    public RestViewModel(SettingsService settings, Func<DateTimeOffset>? clock = null)
    {
        this.settings = settings;
        now = clock ?? (() => DateTimeOffset.UtcNow);
    }
    public TimeSpan Remaining => settings.Value.RestEndsAtUtc is { } end && end > now() ? end - now() : TimeSpan.Zero;
    public bool IsResting => Remaining > TimeSpan.Zero;
    public string RemainingText
    {
        get
        {
            int seconds = (int)Math.Ceiling(Remaining.TotalSeconds);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }
    }
    public event Action? Finished;
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Start()
    {
        // Further reminders during an active break do not reset the user's timer.
        if (IsResting) return;
        settings.Value.RestEndsAtUtc = now().AddMinutes(10);
        Persist(); Changed();
    }
    public void Adjust(int minutes)
    {
        if (!IsResting) return;
        var end = settings.Value.RestEndsAtUtc!.Value.AddMinutes(minutes);
        settings.Value.RestEndsAtUtc = end <= now() ? null : end;
        Persist(); Changed();
    }
    public void Tick()
    {
        if (settings.Value.RestEndsAtUtc is null) return;
        if (!IsResting)
        {
            settings.Value.RestEndsAtUtc = null;
            Persist(); Changed(); Finished?.Invoke();
        }
        else Changed();
    }
    private void Persist() { try { settings.Save(); } catch { /* Timer continues in memory if settings cannot be saved. */ } }
    private void Changed()
    {
        PropertyChanged?.Invoke(this, new(nameof(IsResting)));
        PropertyChanged?.Invoke(this, new(nameof(RemainingText)));
    }
}
