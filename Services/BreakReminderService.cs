using System;
using System.Globalization;
namespace DoneBubble.Services;
public sealed class BreakReminderService
{
    private readonly SettingsService settings;
    public BreakReminderService(SettingsService settings) => this.settings = settings;
    public bool TryMarkReminder(int points, DateTime today)
    {
        string day = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        int milestone = points / 5 * 5;
        int previous = settings.Value.LastScoreReminderDate == day ? settings.Value.LastScoreReminderPoints : 0;
        if (milestone < 5 || milestone <= previous) return false;
        settings.Value.LastScoreReminderPoints = milestone;
        settings.Value.LastScoreReminderDate = day;
        // A settings failure must never break a successful record; memory still prevents repeats.
        try { settings.Save(); } catch { }
        return true;
    }
}
