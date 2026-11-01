# TimeNotice

Gentle time-awareness reminders for Windows. A small tray app that shows the current time over games and focused work without stealing focus or interrupting input.

External overlay only. No game injection, hooks, drivers, memory reading, or cloud. See `SCOPE.md` for the locked V1 scope.

## Status
Phase 1 — overlay proof. Scaffolded, not yet built on Windows. Track progress in `PROGRESS.md`.

## Build (Windows only, WPF does not compile on macOS)
1. Install .NET 8 SDK x64 (`dotnet --version` → 8.x) and Git.
2. Clone: `git clone https://github.com/Skeeee343/TimeNotice.git`
3. Build: `dotnet build src/TimeNotice/TimeNotice.csproj`
4. Run: `dotnet run --project src/TimeNotice/TimeNotice.csproj`
5. Tray icon → Test reminder. Settings live at `%AppData%/TimeNotice/settings.json`.

## Docs
- `SCOPE.md` — what V1 is and is not
- `PROGRESS.md` — cross-machine build log and phase gates
- `AGENTS.md` — how agents work in this repo
