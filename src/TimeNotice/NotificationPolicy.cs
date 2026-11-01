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
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    // Window title of the foreground window, for naming generic hosts
    // (javaw.exe serves any Java app — the title tells us which one).

    public static IntPtr ForegroundWindowHandle()
    {
        try { return GetForegroundWindow(); }
        catch { return IntPtr.Zero; }
    }

    public static string? ForegroundWindowText()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;
            var sb = new System.Text.StringBuilder(512);
            return GetWindowText(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : null;
        }
        catch { return null; }
    }

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
