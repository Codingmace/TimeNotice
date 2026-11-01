// Confirm prompt (Option 1 poll, SCOPE-clean).
// Polls Ctrl+Alt+Z/X with GetAsyncKeyState — observes keys without blocking
// them, no focus change, no hook. Modifier combo so normal typing never fires
// it. Only active while the sticky popup is visible.
// Fires automatically on a new heuristic session; also manual via tray test.
// ponytail: timeout default is caller-chosen — manual test defaults accept, auto defaults accept
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace TimeNotice;

public sealed class ConfirmPrompt : IDisposable
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_Z = 0x5A;
    private const int VK_X = 0x58;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12; // Alt; either side counts, combo keeps game typing safe
    private const int TimeoutSeconds = 15; // ponytail: fixed for test, settings field later if kept

    private readonly OverlayWindow _overlay;
    private readonly Settings _settings;
    private readonly string _exe;
    private readonly string _display;
    private readonly Action<string, string> _onDone;
    private readonly bool _timeoutDefaultAccept;
    private DispatcherTimer? _poll;
    private DateTime _deadlineUtc;
    private bool _done;
    private bool _prevZ, _prevX;
    private int _lastSecs = -1;

    public bool Active => !_done;

    public ConfirmPrompt(OverlayWindow overlay, Settings settings, string exe, string display, Action<string, string> onDone, bool timeoutDefaultAccept = true)
    {
        _overlay = overlay;
        _settings = settings;
        _exe = exe;
        _display = display;
        _onDone = onDone;
        _timeoutDefaultAccept = timeoutDefaultAccept;
    }

    public void Start()
    {
        _deadlineUtc = DateTime.UtcNow.AddSeconds(TimeoutSeconds);
        // Require a fresh press: keys already held at open don't count.
        _prevZ = IsDown(VK_Z);
        _prevX = IsDown(VK_X);
        _overlay.ShowSticky(PromptMain(), Footer(TimeoutSeconds));
        _lastSecs = TimeoutSeconds;
        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _poll.Tick += (_, _) => Tick();
        _poll.Start();
        Log($"src=confirm-test-shown exe={_exe} keys=Ctrl+Alt+Z/X timeout={TimeoutSeconds}s");
    }

    private static bool IsDown(int vk)
    {
        try { return (GetAsyncKeyState(vk) & 0x8000) != 0; }
        catch { return false; }
    }

    private string PromptMain() =>
        _display.Equals(_exe, StringComparison.OrdinalIgnoreCase)
            ? $"{_display} detected — track as game?"
            : $"{_display} ({_exe}) detected — track as game?"; // ponytail: raw exe shown so generic hosts (javaw.exe) are identifiable

    private string Footer(int secs) =>
        $"Ctrl+Alt+Z — Accept (auto in {secs}s)\nCtrl+Alt+X — Reject";

    private void Tick()
    {
        if (_done) return;
        var secs = Math.Max(0, (int)Math.Ceiling((_deadlineUtc - DateTime.UtcNow).TotalSeconds));
        if (secs != _lastSecs)
        {
            _lastSecs = secs;
            _overlay.UpdateSticky(PromptMain(), Footer(secs));
        }
        // Edge-trigger: fresh Ctrl+Alt+letter press only; held keys don't repeat-fire.
        var combo = IsDown(VK_CONTROL) && IsDown(VK_MENU);
        var downZ = IsDown(VK_Z);
        var downX = IsDown(VK_X);
        var hitZ = combo && downZ && !_prevZ;
        var hitX = combo && downX && !_prevX;
        _prevZ = downZ; _prevX = downX;
        if (hitZ) Resolve("accept-key-Z");
        else if (hitX) Resolve("reject-key-X");
        else if (secs <= 0) Resolve(_timeoutDefaultAccept ? "timeout-default-accept" : "timeout-default-dismiss");
    }

    private void Resolve(string choice)
    {
        if (_done) return;
        _done = true;
        try { _poll?.Stop(); } catch { }
        try { _overlay.HideNow(); } catch { }
        try
        {
            if (choice.StartsWith("accept-key", StringComparison.OrdinalIgnoreCase)
                || choice.Equals("timeout-default-accept", StringComparison.OrdinalIgnoreCase))
            {
                var entry = _exe;
                if (!string.IsNullOrWhiteSpace(_display) && !_display.Equals(DefaultDisplay(_exe), StringComparison.OrdinalIgnoreCase))
                    entry = $"{_exe} = {_display}"; // friendly name from window title, e.g. javaw.exe = Minecraft
                if (!_settings.GameExes.Any(l => string.Equals(GameExeOf(l), _exe, StringComparison.OrdinalIgnoreCase)))
                {
                    _settings.GameExes.Add(entry);
                    _settings.Save();
                }
            }
            else if (choice.StartsWith("reject", StringComparison.OrdinalIgnoreCase))
            {
                if (!_settings.IgnoreExes.Any(l => string.Equals(GameExeOf(l), _exe, StringComparison.OrdinalIgnoreCase)))
                {
                    _settings.IgnoreExes.Add(_exe);
                    _settings.Save();
                }
            }
            // dismiss: no settings change, re-prompts next trigger
        }
        catch { }
        Log($"src=confirm-result choice={choice} exe={_exe}");
        try { _onDone(choice, _exe); } catch { }
    }

    private static string GameExeOf(string line)
    {
        var t = (line ?? "").Trim();
        var eq = t.IndexOf('=');
        return eq < 0 ? t : t[..eq].Trim();
    }

    // Mirrors the Title-case fallback in App.GameDisplayName: anything richer
    // than this is a friendly name worth storing (javaw.exe -> Minecraft).
    private static string DefaultDisplay(string exe)
    {
        var bare = exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? exe[..^4] : exe;
        return bare.Length > 0 ? char.ToUpperInvariant(bare[0]) + bare[1..] : exe;
    }

    private static void Log(string msg)
    {
        Logger.Log(msg);
    }

    public void Dispose()
    {
        try { _poll?.Stop(); } catch { }
        _done = true;
    }
}
