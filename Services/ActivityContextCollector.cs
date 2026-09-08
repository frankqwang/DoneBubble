using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using DoneBubble.Models;
using CapturedActivityContext = DoneBubble.Models.ActivityContext;
namespace DoneBubble.Services;
public sealed class ActivityContextCollector
{
    private static readonly Regex Secret = new(@"(?i)(password|passwd|token|secret|api[_ -]?key|authorization)\s*[:=]\s*[^\s]+", RegexOptions.Compiled);
    private static readonly Regex Email = new(@"\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);
    private static readonly Regex LongSecret = new(@"\b[A-Za-z0-9_\-]{32,}\b", RegexOptions.Compiled);
    public CapturedActivityContext? Capture(TimeSpan duration)
    {
        IntPtr handle = NativeMethods.GetForegroundWindow();
        if (handle == IntPtr.Zero) return null;
        NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            string? text = ReadFocusedText();
            return new CapturedActivityContext(process.ProcessName, NativeMethods.WindowTitle(handle), TryPath(process), Redact(text), duration);
        }
        catch { return null; }
    }
    private static string? TryPath(Process process) { try { return process.MainModule?.FileName; } catch { return null; } }
    private static string? ReadFocusedText()
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            if (element == null || element.Current.IsPassword) return null;
            string text = "";
            if (element.TryGetCurrentPattern(TextPattern.Pattern, out object pattern)) text = ((TextPattern)pattern).DocumentRange.GetText(900);
            else if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object value)) text = ((ValuePattern)value).Current.Value;
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        catch { return null; }
    }
    private static string? Redact(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string result = Secret.Replace(text, "$1=[已隐藏]");
        result = Email.Replace(result, "[邮箱已隐藏]");
        result = LongSecret.Replace(result, "[长字符串已隐藏]");
        return result.Length <= 900 ? result : result[..900];
    }
    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        public static string WindowTitle(IntPtr handle) { var text = new StringBuilder(512); GetWindowText(handle, text, text.Capacity); return text.ToString().Trim(); }
    }
}
