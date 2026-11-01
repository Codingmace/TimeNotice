using System.Windows;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace TimeNotice;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstance;
    private WinForms.NotifyIcon? _tray;
    private System.Drawing.Icon? _trayIcon;
    private OverlayWindow? _overlay;
    private Settings _settings = new();
    private DispatcherTimer? _scheduler;
    private SettingsWindow? _settingsWindow;
    private ConfirmPrompt? _confirm;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Single instance: second launch exits silently (tray already running).
        try { _singleInstance = new Mutex(true, @"Local\TimeNotice-single", out var owned); if (!owned) { Shutdown(); return; } }
        catch { /* fail-open: single user, second copy just runs */ }
        DispatcherUnhandledException += (_, args) =>
        {
            try { Logger.Log($"src=crash ui ex={args.Exception.GetType().Name} msg={args.Exception.Message}"); } catch { }
            args.Handled = true; // stay alive: tray app must not vanish on one bad tick
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try { Logger.Log($"src=crash bg ex={args.ExceptionObject?.GetType().Name}"); } catch { }
        };
        _settings = Settings.Load();
        ApplyStartup();
        _overlay = new OverlayWindow(_settings);
        _overlay.Hidden += OnOverlayHidden;

        // ponytail: shared AppIcon helper; a .ico file replaces this if the Explorer exe icon needs branding too
        _trayIcon = AppIcon.CreateTrayIcon();
        _tray = new WinForms.NotifyIcon
        {
            Icon = _trayIcon,
            Visible = true,
            Text = "TimeNotice"
        };
        _tray.ContextMenuStrip = new WinForms.ContextMenuStrip();
        _tray.ContextMenuStrip.Items.Add("Test general reminder", null, (_, _) => ShowTest("manual-general", "general"));
        _tray.ContextMenuStrip.Items.Add("Test game reminder", null, (_, _) => ShowTest("manual-game", "game"));
        _tray.ContextMenuStrip.Items.Add("Test confirm (Z/X)", null, (_, _) => TestConfirm());
        _tray.ContextMenuStrip.Items.Add("Add focused app to Games", null, (_, _) => ClassifyFocused("games"));
        _tray.ContextMenuStrip.Items.Add("Add focused app to Quiet", null, (_, _) => ClassifyFocused("quiet"));
        _tray.ContextMenuStrip.Items.Add("Add focused app to Ignored", null, (_, _) => ClassifyFocused("ignored"));
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
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerMode; // SCOPE §4: sleep/resume closes sessions
        StartScheduler();
    }

    // Phase 6: Registry Run entry (HKCU, no elevation). Called on startup
    // and after every Settings save (SettingsWindow passes ApplyStartup).
    public void ApplyStartup()
    {
        try
        {
            using var run = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (run is null) return;
            if (_settings.StartWithWindows)
            {
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(exe))
                    run.SetValue("TimeNotice", $"\"{exe}\"");
            }
            else
            {
                try { run.DeleteValue("TimeNotice", throwOnMissingValue: false); } catch { }
            }
        }
        catch { }
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            if (_settingsWindow.IsVisible) _settingsWindow.Activate();
            else _settingsWindow.Show();
            return;
        }
        _settingsWindow = new SettingsWindow(_settings, () => { ApplyStartup(); StartScheduler(); });
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
    private string? _lastActiveGameExe; // latest tick's session exe, feeds wording when focus is elsewhere
    private DateTime _gameSessionStart;
    private readonly HashSet<string> _detectSeen = new(StringComparer.OrdinalIgnoreCase); // ponytail: cleared when process exits, relaunch re-notifies
    private readonly HashSet<string> _unknownSeen = new(StringComparer.OrdinalIgnoreCase); // ponytail: one silent log line per exe per run

    // Phase 4 heuristic session (SCOPE §3-§4): IDLE -> ACTIVE -> SUSPECT -> INACTIVE.
    private HeurState _heurState = HeurState.Idle;
    private string? _heurExe;
    private DateTime _heurStartUtc;
    private DateTime _heurSuspectSinceUtc;
    private DateTime _heurCandSinceUtc;
    private string? _heurCandExe;
    private Heuristics.GpuSampler? _gpu;
    private bool _heurInitLogged;

    // Phase 3: self + ignore list never count as gameplay. Ignore wins
    // over a Games entry for the same exe (SCOPE §5: launchers = Ignore).
    private bool IsIgnored(string? exe)
    {
        if (string.IsNullOrWhiteSpace(exe)) return false;
        if (exe.Equals("TimeNotice.exe", StringComparison.OrdinalIgnoreCase)) return true; // never score self
        foreach (var line in _settings.IgnoreExes)
            if (SameExe(ParseGameEntry(line).Exe, exe)) return true;
        return GameCatalog.IsIgnore(exe);
    }

    // Effective game list: user mapping first, then catalog Normal entries not
    // overridden into Ignored. Unknown exes are NOT added — silent log only.
    private List<(string Exe, string? Name)> EffectiveGames()
    {
        var list = _settings.GameExes
            .Select(ParseGameEntry)
            .Where(g => g.Exe.Length > 0)
            .ToList();
        var have = new HashSet<string>(list.Select(g => ExeKey(g.Exe)), StringComparer.OrdinalIgnoreCase);
        foreach (var c in GameCatalog.Entries)
        {
            if (!c.Policy.Equals("Normal", StringComparison.OrdinalIgnoreCase)) continue;
            if (have.Contains(ExeKey(c.Exe)) || IsIgnored(c.Exe)) continue;
            list.Add((c.Exe, c.Display));
            have.Add(ExeKey(c.Exe));
        }
        return list;
    }

    private void NoteUnknown(string? fg)
    {
        if (string.IsNullOrWhiteSpace(fg)) return;
        if (IsIgnored(fg)) return;
        if (_settings.QuietModeExes.Any(q => SameExe(q, fg))) return;
        foreach (var line in _settings.GameExes)
            if (SameExe(ParseGameEntry(line).Exe, fg)) return;
        if (GameCatalog.Find(fg) is not null) return;
        if (!_unknownSeen.Add(fg)) return; // silent after first sighting per run
        Logger.Log($"src=unknown-exe fg={fg} hint=tray");
        if (_tray is not null)
        {
            var tip = $"TimeNotice - unknown: {fg} (right-click > Settings to classify)";
            _tray.Text = tip.Length <= 63 ? tip : tip[..63]; // WinForms cap is 64 chars
        }
    }

    private void ClassifyFocused(string target)
    {
        var fg = NotificationPolicy.ForegroundExe();
        if (string.IsNullOrWhiteSpace(fg)) return;
        if (target == "games")
        {
            if (!_settings.GameExes.Any(l => SameExe(ParseGameEntry(l).Exe, fg)))
                _settings.GameExes.Add(fg);
        }
        else if (target == "quiet")
        {
            if (!_settings.QuietModeExes.Any(q => SameExe(q, fg)))
                _settings.QuietModeExes.Add(fg);
        }
        else
        {
            if (!_settings.IgnoreExes.Any(l => SameExe(ParseGameEntry(l).Exe, fg)))
                _settings.IgnoreExes.Add(fg);
        }
        _settings.Save();
        Logger.Log($"src=classify fg={fg} target={target}");
    }

    private void CheckAuto()
    {
        var nowUtc = DateTime.UtcNow; // ponytail: interval math in UTC, DST-proof
        var nowLocal = DateTime.Now; // ponytail: time windows are local wall-clock by user intent
        var fg = NotificationPolicy.ForegroundExe();
        if (_settings.PauseAll) return;
        if (NotificationPolicy.ShouldSuppress(fg, _settings)) return; // Quiet wins over both modes, retry next tick
        NoteUnknown(fg); // Phase 3: silent log + tray hint, never a popup
        if (IsIgnored(fg)) fg = null; // ignored + self never hold focus for sessions
        var games = EffectiveGames();
        games.RemoveAll(g => IsIgnored(g.Exe)); // ignore wins over a Games entry for the same exe
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
                    if (SameExe(g.Exe, fg)) { focused = g.Exe; break; }
                }
            }
            string? active = focused;
            DateTime activeStart = default;
            if (active is not null && !running.TryGetValue(active, out activeStart)) active = null;
            if (active is null && _settings.CountWhenRunning)
            {
                foreach (var (exe, start) in running.OrderBy(kv => kv.Value))
                { active = exe; activeStart = start; break; } // earliest-running listed game, focus not needed
            }
            _lastActiveGameExe = active; // feeds ShowTest wording when focus is elsewhere
            if (active is not null)
            {
                if (!SameExe(_gameSessionExe, active) || _gameSessionStart != activeStart)
                {
                    _gameSessionExe = active; // new session: overdue fires on next due tick
                    _gameSessionStart = activeStart;
                    _lastGame = activeStart;
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
        UpdateHeuristic(fg, nowUtc); // Phase 4: unknown sustained sessions; never overrides a known-game session
    }

    private bool IsKnownGame(string? exe)
    {
        if (string.IsNullOrWhiteSpace(exe)) return false;
        foreach (var g in EffectiveGames())
            if (SameExe(g.Exe, exe)) return true;
        return false;
    }

    private void HeurReset(string why)
    {
        if (_heurState != HeurState.Idle || _heurExe is not null || _heurCandExe is not null)
            Logger.Log($"src=heur-state to=idle why={why} exe={_heurExe ?? _heurCandExe ?? "<none>"}");
        _heurState = HeurState.Idle;
        _heurExe = null;
        _heurCandExe = null;
        if (_gameSessionExe is not null && !IsKnownGame(_gameSessionExe))
            _gameSessionExe = null; // drop heuristic bridge, keep known-game sessions
    }

    private void UpdateHeuristic(string? fg, DateTime nowUtc)
    {
        if (!_settings.HeuristicEnabled || !_settings.GameEnabled)
        {
            if (_heurState != HeurState.Idle || _heurExe is not null) HeurReset("disabled");
            return;
        }
        _gpu ??= new Heuristics.GpuSampler();
        var candidate = !string.IsNullOrWhiteSpace(fg) && !IsIgnored(fg) && !IsKnownGame(fg)
            && !_settings.QuietModeExes.Any(q => SameExe(q, fg))
            && GameCatalog.Find(fg) is null;
        var idleSec = Heuristics.IdleSeconds();
        var fs = candidate && Heuristics.IsForegroundFullscreen();
        var gpu = candidate ? _gpu.SampleSum(Heuristics.ForegroundPid()) : null;
        if (!_heurInitLogged)
        {
            _heurInitLogged = true;
            Logger.Log($"src=heur-init gpuAvail={_gpu.Available} idle={idleSec}s fs={fs}");
        }
        var idleLost = idleSec > Math.Max(5, _settings.HeuristicSuspectSeconds); // AFK past threshold
        var signal = candidate && !idleLost && (fs || (gpu ?? 0) >= Math.Max(1, _settings.HeuristicGpuPercent));

        switch (_heurState)
        {
            case HeurState.Idle:
                if (signal && SameExe(_heurCandExe, fg))
                {
                    if ((nowUtc - _heurCandSinceUtc).TotalSeconds >= _settings.HeuristicStartSeconds)
                    {
                        _heurState = HeurState.Active; // session found: sustained unknown activity
                        _heurExe = fg;
                        _heurStartUtc = _heurCandSinceUtc;

                        _heurCandExe = null;
                        Logger.Log($"src=heur-state to=active exe={fg} idle={idleSec}s fs={fs} gpu={(gpu is null ? "n/a" : $"{gpu:F0}%")}");
                        if (_tray is not null)
                        {
                            var tip = $"TimeNotice - session: {GameDisplayName(fg!)}";
                            _tray.Text = tip.Length <= 63 ? tip : tip[..63];
                        }
                        MaybeAutoConfirm(fg); // new heuristic session: ask once, Z=Games X=Ignored C=skip
                    }
                }
                else if (signal)
                {
                    if (!SameExe(_heurCandExe, fg))
                        Logger.Log($"src=heur-cand exe={fg} gpu={(gpu is null ? "n/a" : $"{gpu:F0}%")} idle={idleSec}s fs={fs}");
                    _heurCandExe = fg; // first sustained tick; dips restart the clock (conservative)
                    _heurCandSinceUtc = nowUtc;
                }
                else _heurCandExe = null;
                break;
            case HeurState.Active:
                if (signal && SameExe(_heurExe, fg))
                {

                    if (_gameSessionExe is null || SameExe(_gameSessionExe, _heurExe))
                    {
                        _gameSessionExe = _heurExe; // bridge: game wording + elapsed reuse the heuristic clock
                        _gameSessionStart = _heurStartUtc;
                    }
                    if (GameInWindow(DateTime.Now) && (nowUtc - _heurStartUtc) >= TimeSpan.FromMinutes(Math.Max(1, _settings.GameMinutes))
                        && (nowUtc - _lastGame) >= TimeSpan.FromMinutes(Math.Max(1, _settings.GameMinutes)))
                    {
                        _lastGame = nowUtc;
                        ShowTest("auto-game", "game");
                    }
                }
                else
                {
                    _heurState = HeurState.Suspect; // alt-tab / menu dip / short AFK: grace, no flap
                    _heurSuspectSinceUtc = nowUtc;
                    Logger.Log($"src=heur-state to=suspect exe={_heurExe} idle={idleSec}s");
                }
                break;
            case HeurState.Suspect:
                if (signal && SameExe(_heurExe, fg))
                {
                    _heurState = HeurState.Active; // recovered inside grace: session continues, clock kept

                    Logger.Log($"src=heur-state to=active exe={_heurExe} why=recovered");
                }
                else if ((nowUtc - _heurSuspectSinceUtc).TotalMinutes >= Math.Max(1, _settings.HeuristicInactiveMinutes))
                {
                    var done = _heurExe;
                    _heurState = HeurState.Inactive;
                    Logger.Log($"src=heur-state to=inactive exe={done} why=timeout");
                    HeurReset("closed");
                }
                break;
            case HeurState.Inactive:
                HeurReset("closed");
                break;
        }
    }

    private static (string Exe, string? Name) ParseGameEntry(string line)
    {
        var text = (line ?? "").Trim();
        var eq = text.IndexOf('=');
        if (eq < 0) return (text, null);
        return (text[..eq].Trim(), text[(eq + 1)..].Trim());
    }

    // "spotify" == "spotify.exe": strip suffix once so boxes can mix forms.
    private static bool SameExe(string? a, string? b)
    {
        return string.Equals(ExeKey(a), ExeKey(b), StringComparison.OrdinalIgnoreCase);
    }

    private static string ExeKey(string? s)
    {
        var t = ParseGameEntry(s ?? "").Exe.Trim();
        if (t.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) t = t[..^4];
        return t;
    }

    private string GameDisplayName(string exe)
    {
        foreach (var line in _settings.GameExes)
        {
            var (e, name) = ParseGameEntry(line);
            if (SameExe(e, exe) && !string.IsNullOrWhiteSpace(name))
                return name!;
        }
        var hit = GameCatalog.Find(exe); // catalog display when user gave a bare exe
        if (hit is not null) return hit.Display;
        var bare = exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? exe[..^4] : exe;
        return bare.Length > 0 ? char.ToUpperInvariant(bare[0]) + bare[1..] : exe; // ponytail: auto Title-case, override with "exe = Name"
    }

    private readonly Dictionary<string, DateTime> _startCache = new(StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, DateTime> GetRunningGames(List<(string Exe, string? Name)> games)
    {
        var found = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        foreach (var (exe, _) in games)
        {
            var procName = exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? exe[..^4] : exe;
            if (procName.Length == 0) continue;
            try
            {
                var any = false;
                foreach (var p in System.Diagnostics.Process.GetProcessesByName(procName))
                {
                    any = true;
                    try
                    {
                        DateTime start;
                        try { start = p.StartTime.ToUniversalTime(); } // ponytail: UTC so DST shifts never move the session clock
                        catch { start = _startCache.TryGetValue(exe, out var old) ? old : DateTime.UtcNow; } // elevated: reuse first-seen start, never reset to now each tick
                        _startCache[exe] = start;
                        if (!found.TryGetValue(exe, out var cur) || start < cur)
                            found[exe] = start;
                    }
                    finally { p.Dispose(); }
                }
                if (!any) _startCache.Remove(exe); // exited: next launch is a new session
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
        Logger.Log($"src=auto-detect mode=game game={display} suppress={suppress} pause={_settings.PauseAll}");
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
            Logger.Log($"src={(replaced ? "queued-replaced" : "queued")} orig={source}");
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
            Logger.Log($"src=queued-shown orig={source}");
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

    // Auto confirm: first tick of a new heuristic session asks once.
    // Z writes Games, X writes Ignored, timeout (15s) accepts like Z.
    // Generic hosts (javaw.exe) are named from the window title when known.
    private static readonly HashSet<string> GenericHostExes = new(StringComparer.OrdinalIgnoreCase) { "javaw.exe" };

    private string AutoDisplayName(string? exe)
    {
        if (string.IsNullOrWhiteSpace(exe)) return "";
        foreach (var line in _settings.GameExes)
        {
            var (e, name) = ParseGameEntry(line);
            if (SameExe(e, exe) && !string.IsNullOrWhiteSpace(name))
                return GameDisplayName(exe); // user mapping wins, never overridden
        }
        if (GameCatalog.Find(exe) is not null) return GameDisplayName(exe);
        if (GenericHostExes.Contains(exe))
        {
            var title = NotificationPolicy.ForegroundWindowText();
            if (title is not null && title.Contains("minecraft", StringComparison.OrdinalIgnoreCase))
                return "Minecraft";
        }
        return GameDisplayName(exe);
    }
    private void MaybeAutoConfirm(string? exe)
    {
        if (_overlay is null) return;
        if (_confirm is { Active: true }) return; // one prompt at a time
        if (string.IsNullOrWhiteSpace(exe)) return;
        if (IsIgnored(exe)) return;
        if (IsKnownGame(exe)) return;
        if (_settings.QuietModeExes.Any(q => SameExe(q, exe))) return;
        if (GameCatalog.Find(exe) is not null) return;
        foreach (var line in _settings.IgnoreExes)
            if (SameExe(ParseGameEntry(line).Exe, exe)) return;
        ClearQueue(); // confirm owns the overlay; stale queue must not overwrite its result
        _confirm = new ConfirmPrompt(_overlay, _settings, exe, AutoDisplayName(exe), OnConfirmDone, timeoutDefaultAccept: true);
        _confirm.Start();
    }

    private void TestConfirm()
    {
        if (_overlay is null) return;
        if (_confirm is { Active: true }) return; // one prompt at a time
        var fg = NotificationPolicy.ForegroundExe();
        if (string.IsNullOrWhiteSpace(fg) || fg.Equals("TimeNotice.exe", StringComparison.OrdinalIgnoreCase))
            fg = "minecraft.exe"; // demo name when invoked from tray/desktop
        var display = AutoDisplayName(fg);
        ClearQueue();
        _confirm = new ConfirmPrompt(_overlay, _settings, fg, display, OnConfirmDone);
        _confirm.Start();
    }

    private void ClearQueue()
    {
        _queuedMain = null;
        _queuedSub = null;
        try { _drainTimer?.Stop(); } catch { }
    }

    private void OnConfirmDone(string choice, string exe)
    {
        try { _confirm?.Dispose(); } catch { }
        _confirm = null;
        try { _drainTimer?.Stop(); } catch { } // stale drain must not overwrite the receipt
        ShowOrQueue($"confirm: {choice} ({exe})", "choice saved — see debug.log", "confirm-done");
    }

    private void ShowTest(string source = "manual", string? modeOverride = null)
    {
        if (_overlay is null) return;
        var fg = NotificationPolicy.ForegroundExe();
        var suppress = NotificationPolicy.ShouldSuppress(fg, _settings);
        var gameActive = false;
        if (!string.IsNullOrWhiteSpace(fg) && !IsIgnored(fg))
        {
            foreach (var g in EffectiveGames())
            {
                if (SameExe(g.Exe, fg)) { gameActive = true; break; }
            }
            if (!gameActive && _heurState == HeurState.Active && SameExe(_heurExe, fg))
                gameActive = true; // Phase 4 heuristic session counts as game-active
            if (!gameActive && _settings.CountWhenRunning && !string.IsNullOrWhiteSpace(_lastActiveGameExe))
                gameActive = true; // running-counts: session on a background game
        }
        var isGame = modeOverride?.Equals("game", StringComparison.OrdinalIgnoreCase) == true
            || (modeOverride?.Equals("general", StringComparison.OrdinalIgnoreCase) != true && gameActive && _settings.GameEnabled);
        Logger.Log($"src={source} mode={(isGame ? "game" : "general")} fg={fg ?? "<null>"} suppress={suppress} pause={_settings.PauseAll} quiet=[{string.Join(",", _settings.QuietModeExes)}]");
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
                if (SameExe(e, fg)) return GameDisplayName(e);
            }
            if (isGame && _heurState == HeurState.Active && SameExe(_heurExe, fg))
                return GameDisplayName(fg); // heuristic session: Title-case, never another game's name
            if (isGame && _settings.CountWhenRunning && !string.IsNullOrWhiteSpace(_lastActiveGameExe))
                return GameDisplayName(_lastActiveGameExe); // running-counts: name the session game, not the focused app
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
        try { Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerMode; } catch { }
        try { _gpu?.Dispose(); } catch { }
        if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
        try { _trayIcon?.Dispose(); } catch { }
        try { _singleInstance?.ReleaseMutex(); _singleInstance?.Dispose(); } catch { }
        _singleInstance = null;
        base.OnExit(e);
    }

    private void OnPowerMode(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        try
        {
            Dispatcher.BeginInvoke(() =>
            {
                Logger.Log($"src=power mode={e.Mode} sessions-closed");
                _gameSessionExe = null; // SCOPE §4: sleep/resume closes the session
                HeurReset("power");
            });
        }
        catch { }
    }
}
