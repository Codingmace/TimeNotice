# Contributing to TimeNotice

Thanks for stopping by. TimeNotice is a small solo-maintained Windows tray app (WPF, .NET 8). Small, focused PRs are welcome.

## Scope is locked

Read `SCOPE.md` before proposing features. V1 is informational reminders only. Out of scope and will be declined:

- DLL injection, memory reading, renderer hooks, kernel drivers, input automation
- Cloud sync, accounts, telemetry/upload, dashboards/history
- Lockouts, forced limits, MSIX/store packaging

## Build (Windows only — WPF does not compile on macOS)

1. Install .NET 8 SDK x64 (`dotnet --version` → 8.x).
2. `git clone https://github.com/Skeeee343/TimeNotice.git`
3. Stop any running copy first (the exe locks on rebuild): `Stop-Process -Name TimeNotice`
4. Build: `dotnet build src/TimeNotice/TimeNotice.csproj`
5. Run: `dotnet run --project src/TimeNotice/TimeNotice.csproj`

Logs: `%AppData%/TimeNotice/debug.log`. Settings: `%AppData%/TimeNotice/settings.json`.

## Ground rules

- Standard user, no admin/elevation. Manifest stays `asInvoker`.
- Overlay must never steal focus or block clicks (`ShowActivated=false`, `WS_EX_NOACTIVATE|TRANSPARENT`).
- Every overlay goes through the NotificationPolicy / Quiet Mode gate.
- Thresholds and exe names are configurable in JSON, never hardcoded.
- Stdlib / Win32 first, fewest files, minimal diffs.

## Bugs: send this, nothing else needed

From `%AppData%\TimeNotice\` (`Win+R` → paste `%AppData%\TimeNotice` → Enter):

1. What you were doing (game/app name, fullscreen or borderless, paused or not).
2. What you expected vs what you saw.
3. Attach `settings.json` + `debug.log` (plus `debug.log.old` if it exists).

The bug-report issue template asks for exactly this.

## PR checklist

- [ ] `dotnet build` passes with 0 warnings.
- [ ] Manual gate from `SCOPE.md` §10–11 tested (state which one).
- [ ] No hardcoded thresholds/exe names; settings persist after restart.
- [ ] No new dependencies unless justified (note why).

Questions: open an issue first before large changes.
