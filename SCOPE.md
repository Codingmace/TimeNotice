# Time Awareness for Windows — V1 Scope (locked)

Source: original plan doc (removed for privacy) + review decisions 2026-10-21.
Owner model: user = PM, agent = all technical code/docs.

## 1. Objective
Lightweight Windows tray app that reduces time blindness during gaming/focused use. Informational only: no lockouts, no focus steal, no game integration.

## 2. Targets (locked)
- Primary: Windows 11. Free Win10 support via `net8.0-windows10.0.17763` (.NET 8 LTS, WPF, C# 12).
- Standard user only, manifest `asInvoker`. No elevation, no UAC path.
- Packaging Phase 1: self-contained single exe. MSIX + paid signing deferred (SmartScreen warning expected until reputation/cert).
- Budgets: idle <60MB RAM, <1% CPU. Local only: `%AppData%/TimeNotice/` for settings/logs. No cloud/telemetry.

## 3. V1 in scope
- Tray app: pause/resume, settings, classify unknown exe, Quiet Mode choice.
- Scheduled reminders: configurable interval (default 30 min), wall-clock time, optional session/next-reminder line, on/off outside games.
- Hybrid detection: user mapping > known catalog > heuristic (foreground + GPU + fullscreen + idle).
- Session timer with `IDLE -> ACTIVE -> SUSPECT_INACTIVE -> INACTIVE`.
- Overlay: WPF Topmost, `ShowActivated=false`, `WS_EX_NOACTIVATE|TRANSPARENT`, click-through, corner/opacity in JSON, auto-fade (default 10s).
- Per-game policy: Normal / Ignore / Quiet Mode. Centralized gate runs before any overlay. Quiet Mode suppresses all popups while detected. (No Overlay dropped: with no database, an invisible untracked clock is unobservable, so it added nothing.)
- Confirm prompt: unknown sustained game asks once via Ctrl+Alt+Z (track) / Ctrl+Alt+X (ignore), sticky, no focus steal; timeout applies the configured action (default Accept, 30s).
- Settings in JSON. SQLite only if history added later (deferred).

## 4. Detection — concrete APIs only
- Foreground: `GetWindowThreadProcessId`. Single 5s poll drives foreground + idle + GPU + fullscreen together.
- Idle: `GetLastInputInfo` seconds since input.
- Fullscreen/borderless: `GetWindowRect` vs monitor rect (±2px), contextual only. Exclusive-fullscreen no-show is accepted, no renderer hooks.
- GPU active: PDH `GPU Engine` counters per-process. No NVIDIA SDK.
- Thresholds (configurable, test-driven): signal loss moves Active→Suspect at once; `HeuristicSuspectSeconds` (default 30s) holds candidate dips + known-game alt-tab grace; `HeuristicInactiveMinutes` (default 5) closes to INACTIVE. Sleep/resume closes session. Alt-tab = foreground loss.

## 5. Catalog (ship 12 verified max)
Schema: `{exe, display, policy}`. Example rule: launchers (`steam.exe`, etc.) = Ignore, never gameplay.
- Ship small verified list only. Exact exes verified at build time. 9 Entries (5 Ignore launchers + 4 Normal games) stay under the 12 max; 6 ExtraIgnored (Discord/Zoom/Teams/Slack/OBS) are a separate runtime safety net, never gameplay.
- Multi-exe titles (DX11/DX12) listed explicitly.
- User can add/correct/ignore/override locally from tray. Unknown exes: silent log + tray hint, never in-game popup.

## 6. Overlay copy
`9:30 PM | Session: 1h 30m | Next: 10:00 PM` — time prominent, session/next optional. Exclusive-fullscreen no-show is accepted, no renderer hooks.

## 7. Safety / anti-cheat boundary
Allowed: user-mode Win32, perf telemetry, topmost overlay, idle status.
Forbidden: DLL injection, memory read/write, renderer hooks, kernel drivers, file mods, input automation/bypass. Per-title caution policies stay configurable; no guarantee vs future anti-cheat.

## 8. Config model (`settings.json`)
interval, displayDuration, showTime, showSession, generalReminders, position, opacity, perApp {classification, policy}, pauseAll, startWithWindows (Registry Run).

## 9. Explicitly deferred
Dashboards/history, cloud/sync/upload, forced limits/lockouts, memory inspection/injection, full game DB, advanced categorization, MSIX/store, paid cert.

## 10. Phases + gates
1. Overlay proof — tray + manual reminder + fade. Gate: no focus steal over desktop/borderless.
2. Scheduler — recurring reminders + persist. Gate: intervals + pause survive restart.
3. Process detection — foreground + catalog. Gate: known games start/stop, no launcher false positives.
4. Heuristics — GPU/idle/fullscreen + state machine. Gate: unknown sustained session found, 30s drops don't flap.
5. Safety gate — per-game policy + Quiet Mode. Gate: flagged exe suppresses all, resumes after exit.
6. Hardening — startup, logging, config recovery, Defender scan. Gate: clean install, no invasive deps.

## 11. Test checklist (V1 exit)
Borderless no-steal, exclusive-fullscreen documented, known/unknown classification, menu GPU dip, alt-tab, idle close, multi-monitor/DPI, Quiet Mode suppress/resume, Defender scan clean, protected-title smoke (no hooks).

## 12. First deliverable
Tray WPF app + on-demand fading overlay + Quiet Mode exe suppress. Validates top 2 UX risks before detector/catalog.
