using System.Windows;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace TimeNotice;

public partial class App : System.Windows.Application
{
    private WinForms.NotifyIcon? _tray;
    private OverlayWindow? _overlay;
    private Settings _settings = new();
    private DispatcherTimer? _scheduler;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _settings = Settings.Load();
        _overlay = new OverlayWindow(_settings);
        _overlay.Hidden += OnOverlayHidden;

        // ponytail: SystemIcons placeholder, custom .ico when branding matters
        _tray = new WinForms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Visible = true,
            Text = "TimeNotice"
        };
        _tray.ContextMenuStrip = new WinForms.ContextMenuStrip();
        _tray.ContextMenuStrip.Items.Add("Test general reminder", null, (_, _) => ShowTest("manual-general", "general"));
        _tray.ContextMenuStrip.Items.Add("Test game reminder", null, (_, _) => ShowTest("manual-game", "game"));
        var pauseItem = new WinForms.ToolStripMenuItem("Pause") { Checked = _settings.PauseAll, CheckOnClick = true };
        pauseItem.CheckedChanged += (_, _) =>
        {
            _settings.PauseAll = pauseItem.Checked;
            _settings.Save();
        };
        _tray.ContextMenuStrip.Items.Add(pauseItem);
        _tray.ContextMenuStrip.Items.Add("Settings…", null, (_, _) => OpenSettings());
        _tray.ContextMenuStrip.Items.Add("Exit", null, (_, _) => Shutdown());
        _tray.MouseClick += (_, args) =>
        {
            if (args.Button == WinForms.MouseButtons.Left) OpenSettings();
        };
        StartScheduler();
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            if (_settingsWindow.IsVisible) _settingsWindow.Activate();
            else _settingsWindow.Show();
            return;
        }
        _settingsWindow = new SettingsWindow(_settings, StartScheduler);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void StartScheduler()
    {
        _scheduler?.Stop();
        if (_lastGeneral == default) _lastGeneral = DateTime.UtcNow; // ponytail: UTC math so DST shifts never move intervals; display stays local
        _scheduler = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) }; // ponytail: 5s poll so game-detect is prompt; per-mode intervals compared inside
        _scheduler.Tick += (_, _) => CheckAuto();
        _scheduler.Start();
    }

    private DateTime _lastGeneral;
    private DateTime _lastGame;
    private string? _gameSessionExe;
    private DateTime _gameSessionStart;
    private readonly HashSet<string> _detectSeen = new(StringComparer.OrdinalIgnoreCase); // ponytail: cleared when process exits, relaunch re-notifies

    private void CheckAuto()
    {
        var nowUtc = DateTime.UtcNow; // ponytail: interval math in UTC, DST-proof
        var nowLocal = DateTime.Now; // ponytail: time windows are local wall-clock by user intent
        var fg = NotificationPolicy.ForegroundExe();
        if (_settings.PauseAll) return;
        if (NotificationPolicy.ShouldSuppress(fg, _settings)) return; // Quiet wins over both modes, retry next tick
        var games = _settings.GameExes
            .Select(ParseGameEntry)
            .Where(g => g.Exe.Length > 0)
            .ToList();
        // detection: any listed game process running (focused or not)
        if (_settings.GameEnabled && games.Count > 0)
        {
            var running = GetRunningGames(games);
            _detectSeen.RemoveWhere(seen => !running.ContainsKey(seen));
            foreach (var (exe, _) in running.OrderBy(kv => kv.Value))
            {
                if (_detectSeen.Add(exe) && _settings.GameNotifyOnDetect)
                    ShowDetect(exe);
            }
            // session clock follows the focused game's process start, not app launch
            string? focused = null;
            if (!string.IsNullOrWhiteSpace(fg))
            {
                foreach (var g in games)
                {
                    if (string.Equals(g.Exe, fg, StringComparison.OrdinalIgnoreCase)) { focused = g.Exe; break; }
                }
            }
            if (focused is not null && running.TryGetValue(focused, out var sessionStart))
            {
                if (!string.Equals(_gameSessionExe, focused, StringComparison.OrdinalIgnoreCase) || _gameSessionStart != sessionStart)
                {
                    _gameSessionExe = focused; // new session: overdue fires on next due tick
                    _gameSessionStart = sessionStart;
                    _lastGame = sessionStart;
                }
                if (GameInWindow(nowLocal) && (nowUtc - _gameSessionStart) >= TimeSpan.FromMinutes(Math.Max(1, _settings.GameMinutes))
                    && (nowUtc - _lastGame) >= TimeSpan.FromMinutes(Math.Max(1, _settings.GameMinutes)))
                {
                    _lastGame = nowUtc;
                    ShowTest("auto-game", "game");
                }
            }
            else
            {
                _gameSessionExe = null; // no focused game: keep detect memory, drop session
            }
        }
        if (_settings.GeneralEnabled && InWindow(nowLocal) && (nowUtc - _lastGeneral) >= TimeSpan.FromMinutes(Math.Max(1, _settings.GeneralMinutes)))
        {
            _lastGeneral = nowUtc;
            ShowTest("auto-general", "general");
        }
    }

    private static (string Exe, string? Name) ParseGameEntry(string line)
    {
        var text = (line ?? "").Trim();
        var eq = text.IndexOf('=');
        if (eq < 0) return (text, null);
        return (text[..eq].Trim(), text[(eq + 1)..].Trim());
    }

    private string GameDisplayName(string exe)
    {
        foreach (var line in _settings.GameExes)
        {
            var (e, name) = ParseGameEntry(line);
            if (string.Equals(e, exe, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(name))
                return name!;
        }
        var bare = exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? exe[..^4] : exe;
        return bare.Length > 0 ? char.ToUpperInvariant(bare[0]) + bare[1..] : exe; // ponytail: auto Title-case, override with "exe = Name"
    }

    private static Dictionary<string, DateTime> GetRunningGames(List<(string Exe, string? Name)> games)
    {
        var found = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        foreach (var (exe, _) in games)
        {
            var procName = exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? exe[..^4] : exe;
            if (procName.Length == 0) continue;
            try
            {
                foreach (var p in System.Diagnostics.Process.GetProcessesByName(procName))
                {
                    try
                    {
                        DateTime start;
                        try { start = p.StartTime.ToUniversalTime(); } // ponytail: UTC so DST shifts never move the session clock
                        catch { start = DateTime.UtcNow; } // fail-open: clock starts now
                        if (!found.TryGetValue(exe, out var cur) || start < cur)
                            found[exe] = start;
                    }
                    finally { p.Dispose(); }
                }
            }
            catch { } // ponytail: silent unknown-exe, tray hint only
        }
        return found;
    }

    private static string FormatElapsed(TimeSpan span)
    {
        var mins = Math.Max(0, (int)Math.Floor(span.TotalMinutes));
        if (mins < 60) return $"{mins} min";
        return $"{mins / 60} h {mins % 60} min";
    }

    private void ShowDetect(string exe)
    {
        if (_overlay is null) return;
        var fg = NotificationPolicy.ForegroundExe();
        var suppress = NotificationPolicy.ShouldSuppress(fg, _settings);
        var display = GameDisplayName(exe);
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(Settings.Dir, "debug.log"), $"{DateTime.Now:HH:mm:ss} src=auto-detect mode=game game={display} suppress={suppress} pause={_settings.PauseAll}{System.Environment.NewLine}"); } catch { }
        if (suppress) return; // Quiet Mode / paused: silent
        var mins = Math.Max(1, _settings.GameMinutes).ToString();
        var main = (_settings.GameDetectTemplate ?? "")
            .Replace("{game}", display)
            .Replace("{interval}", mins)
            .Replace("{time}", DateTime.Now.ToString("h:mm tt"));
        if (string.IsNullOrWhiteSpace(main)) main = $"{display} detected";
        var sub = "";
        if (_settings.ShowSession)
        {
            sub = (_settings.GameDetectSubTemplate ?? "")
                .Replace("{game}", display)
                .Replace("{interval}", mins)
                .Replace("{time}", DateTime.Now.ToString("h:mm tt"));
        }
        ShowOrQueue(main, sub, "auto-detect");
    }

    private const double QueueGapSeconds = 1.5; // ponytail: single-slot queue, latest replaces waiting
    private string? _queuedMain;
    private string? _queuedSub;
    private string _queuedSource = "";
    private DispatcherTimer? _drainTimer;

    private void ShowOrQueue(string main, string sub, string source)
    {
        if (_overlay is null) return;
        if (_overlay.IsShowing)
        {
            var replaced = _queuedMain is not null;
            _queuedMain = main;
            _queuedSub = sub;
            _queuedSource = source;
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(Settings.Dir, "debug.log"), $"{DateTime.Now:HH:mm:ss} src={(replaced ? "queued-replaced" : "queued")} orig={source}{System.Environment.NewLine}"); } catch { }
            return;
        }
        _overlay.ShowReminder(main, sub);
    }

    private void OnOverlayHidden()
    {
        if (_queuedMain is null) return;
        var main = _queuedMain;
        var sub = _queuedSub ?? "";
        var source = _queuedSource;
        _queuedMain = null;
        _queuedSub = null;
        _drainTimer?.Stop();
        _drainTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(QueueGapSeconds) };
        _drainTimer.Tick += (_, _) =>
        {
            _drainTimer?.Stop();
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(Settings.Dir, "debug.log"), $"{DateTime.Now:HH:mm:ss} src=queued-shown orig={source}{System.Environment.NewLine}"); } catch { }
            _overlay?.ShowReminder(main, sub);
        };
        _drainTimer.Start();
    }

    private bool InWindow(DateTime now)
    {
        return InWindow(now, _settings.WindowOnly, _settings.WindowStart, _settings.WindowEnd);
    }

    private bool GameInWindow(DateTime now)
    {
        return InWindow(now, _settings.GameWindowOnly, _settings.GameWindowStart, _settings.GameWindowEnd);
    }

    private static bool InWindow(DateTime now, bool only, string startText, string endText)
    {
        if (!only) return true;
        if (!TimeSpan.TryParse(startText, out var start)) return true; // fail-open on bad input
        if (!TimeSpan.TryParse(endText, out var end)) return true;
        var t = now.TimeOfDay;
        if (start <= end) return t >= start && t < end;
        return t >= start || t < end; // overnight, e.g. 22:00-07:00
    }

    private void ShowTest(string source = "manual", string? modeOverride = null)
    {
        if (_overlay is null) return;
        var fg = NotificationPolicy.ForegroundExe();
        var suppress = NotificationPolicy.ShouldSuppress(fg, _settings);
        var gameActive = false;
        if (!string.IsNullOrWhiteSpace(fg))
        {
            foreach (var line in _settings.GameExes)
            {
                if (string.Equals(ParseGameEntry(line).Exe, fg, StringComparison.OrdinalIgnoreCase)) { gameActive = true; break; }
            }
        }
        var isGame = modeOverride?.Equals("game", StringComparison.OrdinalIgnoreCase) == true
            || (modeOverride?.Equals("general", StringComparison.OrdinalIgnoreCase) != true && gameActive && _settings.GameEnabled);
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(Settings.Dir, "debug.log"), $"{DateTime.Now:HH:mm:ss} src={source} mode={(isGame ? "game" : "general")} fg={fg ?? "<null>"} suppress={suppress} pause={_settings.PauseAll} quiet=[{string.Join(",", _settings.QuietModeExes)}]{System.Environment.NewLine}"); } catch { }
        if (suppress) return; // Quiet Mode / paused: silent
        var mins = isGame ? Math.Max(1, _settings.GameMinutes) : Math.Max(1, _settings.GeneralMinutes);
        var nowLocal = DateTime.Now;
        var nowStr = nowLocal.ToString("h:mm tt");
        var nextStr = nowLocal.AddMinutes(mins).ToString("h:mm tt");
        var gameDisplay = ResolveGameDisplay(fg, isGame);
        var elapsed = isGame && _gameSessionExe is not null ? FormatElapsed(DateTime.UtcNow - _gameSessionStart) : (isGame ? "0 min" : "");
        var mainTpl = (isGame && !string.IsNullOrWhiteSpace(_settings.GameMainTemplate)) ? _settings.GameMainTemplate : _settings.MainTemplate;
        var subTpl = (isGame && !string.IsNullOrWhiteSpace(_settings.GameSubTemplate)) ? _settings.GameSubTemplate : _settings.SubTemplate;
        var main = (mainTpl ?? "").Replace("{time}", nowStr).Replace("{next}", nextStr).Replace("{game}", gameDisplay).Replace("{elapsed}", elapsed).Replace("{interval}", mins.ToString());
        if (string.IsNullOrWhiteSpace(main)) main = nowStr;
        var sub = "";
        if (_settings.ShowSession)
        {
            sub = (subTpl ?? "").Replace("{time}", nowStr).Replace("{next}", nextStr).Replace("{game}", gameDisplay).Replace("{elapsed}", elapsed).Replace("{interval}", mins.ToString());
        }
        ShowOrQueue(main, sub, source);
    }

    private string ResolveGameDisplay(string? fg, bool isGame)
    {
        if (!string.IsNullOrWhiteSpace(fg))
        {
            foreach (var line in _settings.GameExes)
            {
                var (e, _) = ParseGameEntry(line);
                if (string.Equals(e, fg, StringComparison.OrdinalIgnoreCase)) return GameDisplayName(e);
            }
        }
        if (isGame)
        {
            foreach (var line in _settings.GameExes)
            {
                var (e, _) = ParseGameEntry(line);
                if (e.Length > 0) return GameDisplayName(e);
            }
        }
        return "";
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
        base.OnExit(e);
    }
}
