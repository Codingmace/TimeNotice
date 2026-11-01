# Build Progress — TimeNotice (cross-machine source of truth)

Update this file every session. One row per test run.

## Current
- Phase: 3 — Detection NEXT (launcher ignore list + unknown-exe handling; StartTime sessions already built 2026-11-01)
- Next action: await user go-ahead for P3 launcher-ignore + unknown-exe tray hint

## How to sync Mac <-> Windows
```bash
# Mac (primary edits)
git add -A && git commit -m "msg" && git push
# Windows (build/test)
git pull
dotnet build src/TimeNotice/TimeNotice.csproj
dotnet run --project src/TimeNotice/TimeNotice.csproj
```

## Phase gates (check when passed on Windows)
- [x] P1 Overlay: appears bottom-right, fades ~5s, no focus steal (type in Notepad while it shows), works over borderless window — passed 2026-11-01 per user manual test
- [x] P1 Quiet gate: add `notepad.exe` to QuietModeExes in `%AppData%/TimeNotice/settings.json`, focus Notepad → Test reminder shows nothing — passed 2026-11-01 (Notepad suppress=True, Desktop showed; debug.log confirms)
- [x] P2 Scheduler: recurring reminders + pause persists after restart — passed 2026-11-01 per user (pause/unpause confirmed; 1-min recurrence + settings persistence in debug.log across restarts)
- [ ] P3 Detection: known exe starts/stops session, launcher not mistaken
- [ ] P4 Heuristics: unknown sustained session found, 30s dip doesn't flap
- [ ] P5 Safety: flagged exe suppresses all popups, resumes after exit
- [ ] P6 Hardening: clean install, Defender scan, no invasive deps

## Session log
| Date (UTC) | Machine | Did | Result |
|---|---|---|---|
| 2026-10-21 | Mac | Scaffolded P1: tray + overlay + gate + settings; added SCOPE/AGENTS/PROGRESS | Not built — needs Windows `dotnet build` |
| 2026-10-21 | Mac | Static review: fixed Settings.Path shadow, ShutdownMode, overlay re-show; `git init` + first commit | Ready for Windows: clone/pull then build |
| 2026-10-21 | Mac | Ponytail audit (full tree): 4 findings, ~8 lines, 0 deps | Cuts logged below, none applied — approve before code/spec change |
| 2026-11-01 | Windows | Installed SDK 8.0.427, added nuget.org source, fixed App ambiguity + missing System.IO, `dotnet build` OK, exe launch-tested | Build passes, app stays running; pending manual P1 tray + Quiet gates |
| 2026-11-01 | Windows | Manual P1: Test reminder showed bottom-right + faded, typing in Notepad uninterrupted | PASS per user — overlay + no-steal confirmed |
| 2026-11-01 | Windows | Quiet gate via Test-in-3s + debug.log: Notepad suppress=True (no popup), Desktop suppress=False (popup) | PASS — P1 DONE; note: Desktop run detected fg=TimeNotice.exe, needs Ignore-self in Phase 3 |
| 2026-11-01 | Windows | P2 scheduler: auto timer every ReminderMinutes + tray Interval 15/30/60 + Next line, 0 warn build + launch OK | Built, pending gate: 1-min recurrence + pause-persist |
| 2026-11-01 | Windows | Settings window: left-click tray opens it (any X 1-480 min, secs, Next toggle, quiet list), right-click keeps menu | Built 0 warn + launch OK, pending user open/save test |
| 2026-11-01 | Windows | Two modes: General (enable + X min + 09:00-23:00 window) + Game (enable + Y min + game exe list), 30s poll, game priority | Built 0 warn + launch OK, pending user test |
| 2026-11-01 | Windows | Wording templates: MainTemplate/SubTemplate with {time} {next} + wrap box, tidied Spotify.exe casing | Built 0 warn + launch OK, pending user open/save test |
| 2026-11-01 | Windows | Overlap queue (single-slot, latest wins, 1.5s gap, queued log tags) + overlap warning in Settings + card-style Settings refresh | Built 0 warn + launch OK, pending user test |
| 2026-11-01 | Windows | Game time window (own Only-between + From/To) + auto collapse/expand of both time ranges on checkbox | Built 0 warn + launch OK, pending user open/save test |
| 2026-11-01 | Windows | Overlay anchor fix: position on rendered Actual size after Show (SizeToContent broke bottom-right) | Built 0 warn + launch OK, pending user confirm bottom-right |
| 2026-11-01 | Windows | Tray trim: removed Test-in-3s + Interval submenu (Settings window is the config UI now) | Built 0 warn + launch OK |
| 2026-11-01 | Windows | Appearance + split test: 4-corner Position, Theme Dark/Light/MatchHost/Custom + bg/text/sub colors + opacity slider, Game main/sub templates (blank=reuse), tray Test general/Test game with mode tag in debug.log | Built 0 warn + launch OK, pending user test |
| 2026-11-01 | Windows | Settings tabs: General / Game / Popup (wording + look) / Quiet, WarnText + Save always visible, taller Quiet box | Built 0 warn + launch OK, pending user test |
| 2026-11-01 | Windows | Pause checkmark: tray Pause item stays checked while paused, persists via settings | Built 0 warn + launch OK |
| 2026-11-01 | Windows | Process-based game clock (StartTime session, overdue fires, {game} {elapsed} {interval} tokens, exe = Name mapping) + detect popup checkbox/template, 5s poll | Built 0 warn + launch OK, pending user test |
| 2026-11-01 | Windows | Log diagnosis: 1-min refires were game pops (opencode.exe listed as game) in reused general wording; set distinct game wording, restarted live | Running, auto-game fired with new wording |
| 2026-11-01 | Windows | Detection two-line format: GameDetectSubTemplate (blank=none), short main + small sub like other pops, live config split | Built 0 warn, restarted live, detects firing |
| 2026-11-01 | Windows | DST-proof scheduler: interval/session math in UTC, time windows + display stay local; observed live clock correction 01:49→01:35 mid-session, self-healed | Built 0 warn, restarted live, firing |

## Ponytail debt (audit 2026-10-21, biggest first)
- [ ] `Settings.cs`: delete `ReminderMinutes + Position` until a Phase 2 reader exists
- [ ] `SCOPE.md` §8: trim `scale, monitor, perApp, startWithWindows` to shipped fields until needed
- [ ] Overlay: collapse `SubText`/`ShowSession` branch to one static test line until Phase 2 copy
- (empty-row cleanup done in this edit)

## Windows quick start
1. Install .NET 8 SDK x64, verify: `dotnet --version` → 8.x
2. `git pull` (or copy folder), then build/run commands above
3. Tray icon → Test reminder. Edit settings at `%AppData%/TimeNotice/settings.json`
4. Append a row to Session log above with pass/fail before next session.
