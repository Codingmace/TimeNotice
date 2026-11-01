using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace TimeNotice;

public static class NotificationPolicy
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    public static string? ForegroundExe()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;
            GetWindowThreadProcessId(hwnd, out var pid);
            using var p = Process.GetProcessById((int)pid);
            var file = p.MainModule?.FileName;
            if (!string.IsNullOrEmpty(file)) return Path.GetFileName(file);
            return p.ProcessName + ".exe";
        }
        catch { return null; /* ponytail: silent unknown-exe, tray hint only */ }
    }

    public static bool ShouldSuppress(string? exe, Settings s)
    {
        if (s.PauseAll) return true;
        if (string.IsNullOrWhiteSpace(exe)) return false; // fail-open for Phase 1
        return s.QuietModeExes.Any(q => string.Equals(q, exe, StringComparison.OrdinalIgnoreCase));
    }
}
