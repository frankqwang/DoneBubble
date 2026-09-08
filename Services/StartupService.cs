using System;
using Microsoft.Win32;
namespace DoneBubble.Services;
public sealed class StartupService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue("DoneBubble") is string;
    }
    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled)
        {
            string executable = Environment.ProcessPath ?? throw new InvalidOperationException("无法确定程序路径。");
            if (!executable.EndsWith("DoneBubble.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请从发布后的 DoneBubble.exe 设置开机启动。");
            key.SetValue("DoneBubble", "\"" + executable + "\"");
        }
        else key.DeleteValue("DoneBubble", false);
    }
}
