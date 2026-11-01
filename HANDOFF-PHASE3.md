# Handoff — Phase 3 (Detection)

Date: 2026-11-01. Previous session did P1–P2 plus approved extras. App is live on the test PC.

## Status

- PASS: P1 overlay + no-focus-steal, P1 Quiet gate, P2 scheduler (recurring + pause persist, user-confirmed).
- NEXT: P3 Detection per `SCOPE.md` §4–§5 + §10: foreground + catalog. Gate: known games start/stop, ignored app not mistaken.
- HEAD: `387f4ae` + this handoff commit. Remote: `https://github.com/Skeeee343/TimeNotice` (`main`, tracking set, pushed OK).

## What's already built (don't rebuild)

- Two-mode scheduler (`src/TimeNotice/App.xaml.cs`): General (enable + X min + own window) + Game (enable + Y min + own window + exe list). 5s poll.
- Process-based game clock: session start = focused game's `Process.StartTime` → UTC; overdue sessions fire on next tick; relaunch resets. `{game}` `{elapsed}` `{interval}` tokens in game wording.
- `exe = Name` mapping in Games box (`spotify.exe = Spotify`); auto Title-case fallback; matcher parses the exe part (a `=` line never breaks matching).
- Detection popup: checkbox + main/sub templates, fires ≤5s after a listed game **process starts** (running, not focused); re-notifies on relaunch; gated by Quiet/Pause; logs `src=auto-detect`.
- Game reminders require the game to be **focused** (detection does not). This is intentional; user may ask for a "running counts" toggle later.
- Overlap queue: single-slot, latest wins, 1.5s gap (`queued` / `queued-replaced` / `queued-shown` in log).
- Appearance: 4-corner Position, Theme Dark/Light/MatchHost (registry `AppsUseLightTheme`)/Custom + 3 hex colors + opacity slider.
- Split tray tests (general/game); Pause item has checkmark; Settings window has General/Game/Popup/Quiet tabs.
- Scheduler math in UTC (DST-proof); time windows + display stay local. `debug.log` timestamps stay local.
- `settings.json` migration guards in `Settings.Load` (blank → defaults, clamp opacity, validate position/theme).

## P3 remaining (from SCOPE, in suggested order)

1. Ignore list: `steam.exe` etc. must never count as gameplay (currently any listed exe counts; unlisted exes are invisible).
2. Small verified game catalog (12 max): `{exe, display, policy}` + multi-exe titles listed explicitly.
3. Unknown-exe handling: silent `debug.log` + tray hint, never an in-game popup.
4. Later phases (do NOT start unasked): P4 heuristics (GPU PDH + idle + fullscreen state machine), P5 per-game policy, P6 hardening/Defender.

## Test-PC operating notes

- Build: `dotnet build src/TimeNotice/TimeNotice.csproj` (`--no-restore` works once restored). Must `Stop-Process -Name TimeNotice` before rebuild (exe locks).
- Launch: `Start-Process <.../TimeNotice.exe>`; app is currently LEFT RUNNING on the test PC — check `Get-Process -Name TimeNotice` before starting another.
- Config: `%AppData%/TimeNotice/settings.json` + `debug.log` (Win+R `%AppData%\TimeNotice`). Live config currently has `opencode.exe = OpenCode` as a game (user's test entry, kept per explicit choice) + `spotify.exe = Spotify`, game 1 min / general 30 min.
- `debug.log` sources: `manual-general`, `manual-game`, `auto-general`, `auto-game`, `auto-detect`, `queued*`, each with `mode=` + `fg=` + `suppress=`.
- Agent may generate/remove `settings.json` and manage the TimeNotice process only. No injection/hooks/drivers/cloud — forbidden by scope.

## Gotchas hit this session

- XAML: attribute text starting with `{...}` needs a `{}` escape prefix.
- `SettingsWindow.xaml.cs` needs `System.Windows.Controls.ComboBox` fully qualified (WinForms ambiguity via implicit usings).
- PowerShell here has no `head`; use `Select-Object -First N`. Earlier `Add-Type` foreground-window snippet failed on quoting — working version saved at `C:\Users\Jourden\AppData\Local\Temp\opencode\fgcheck.ps1` (outside repo, temp only).
- `ConvertFrom-Json` objects lack new props from older files — use `Add-Member -Force` when migrating live config.
- `git config user.name/email` is repo-local (`Jourden / yoyo3457@gmail.com`); no global identity on this PC.

## Open questions for the user

- Game reminders: keep focused-only, or add a "fire while merely running" toggle?
- Keep `opencode.exe` as a permanent game entry or remove it now testing is done?
- Confirm the GitHub repo is Private (couldn't verify visibility from the shell).
