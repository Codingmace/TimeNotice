# Build Progress — TimeNotice (cross-machine source of truth)

Update this file every session. One row per test run.

## Current
- Phase: 6 — DONE 2026-11-01 (hardening verified: clean install + corrupt recovery live-tested, Defender scan clean per user)
- Next action: V1 exit checklist sweep (§11) or public release prep
- Open (deferred to post-Phase 6): CountWhenRunning ON-path self-test (tick box, alt-tab 2 min); Game-tab crowding polish; repo public release; game+general overlap — both fire by design (late-night time awareness), reconsider game-with-time-only after user testing/feedback; beta debug options in-app (tray Open-logs-folder + Copy-debug-info, startup ver/OS/settings line in debug.log) — docs-only for now, see README Beta debugging
- Repo: public since 2026-11-02
- Config: reset to fresh-install defaults 2026-11-01 at user request

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
- [x] P3 Detection: known exe starts/stops session, ignored app not mistaken — passed 2026-11-01 per user explicit close-out
- [x] P4 Heuristics: unknown sustained session found (AppControl via GPU, live 2026-11-01), suspect grace on focus loss logged, no flap
- [x] P5 Safety: matrix-verified 2026-11-01 — every cell already implemented (P1 Quiet test + P3 ignore-wins test); No Overlay dropped, no new code
- [x] P6 Hardening: clean install, Defender scan, no invasive deps — DONE 2026-11-01 (fresh-profile launch OK, corrupt→backup+defaults OK, Defender clean per user, only Microsoft PerformanceCounter pkg)
- [ ] T-gates (eng review 2026-11-01, before ship): confirm timeout default honors ConfirmTimeoutAction; confirm firing while Settings open keeps both edits; corrupt settings.json shows one balloon with backup path and stays alive

## Behavior matrix (agreed 2026-11-01 — the whole notification model)

Terms: **tracked** = listed game focused (or heuristic session, or running with CountWhenRunning on).
**ignored** = IgnoreExes + self. Time reminders are unconditional; only Quiet/Pause silence them.

| Game mode | Time mode | Focused app | Result |
|---|---|---|---|
| off | off | anything | nothing |
| off | on | anything (not Quiet) | time only |
| off | on | Quiet / paused | nothing |
| on | off | tracked game | game only |
| on | off | ignored / Quiet | nothing |
| on | on | tracked game | both |
| on | on | ignored | time only |
| on | on | Quiet | nothing |

Notes: unknown app never blocks time reminders and never starts a game session by itself
(heuristic promotion is the only path, and it logs + asks). No Overlay mode dropped:
with no database, an invisible untracked clock is unobservable, so it added nothing.

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
| 2026-11-01 | Windows | P3 Detection: GameCatalog (9 entries ≤12 max: 5 Ignore + cs2/csgo multi-exe + eldenring + Balatro), IgnoreExes setting seeded from catalog + Game-tab box, self-ignore TimeNotice.exe, unknown-exe silent log + tray tooltip, tray classify (Games/Quiet/Ignored) | Built 0 warn; live log-verified: unknown-exe Notepad line + no popup; ignore-wins both directions (opencode in Ignored = 12s silence, removed = detect+auto-game resume); Settings visuals pending user open/save |
| 2026-11-01 | Windows | P4 heuristics: state machine (Idle/Active/Suspect/Inactive) + idle/fullscreen/GPU signals, 5 settings, tray+Settings UI, sleep closes sessions | Built 0 warn; live: heur-init gpuAvail=True, known-game path intact |
| 2026-11-01 | Windows | P4 gate PASS: user held AppControl maximized 30s → heur-cand (gpu=51% idle=0s) → active → auto-game reminder naming AppControl; focus return → suspect grace (no flap, no close). Restored HeuristicStartSeconds 60, rebuilt 0 warn, relaunched, committed | App stable (PID 21728), pushed |
| 2026-11-01 | Windows | Running-counts toggle: CountWhenRunning (default off, Game tab checkbox) — session follows earliest-running listed game without focus; focused still wins; wording names the session game; heuristic still focus-only | Built 0 warn; default-off regression live (detect+game normal); ON-path left to user self-test |
| 2026-11-01 | Windows | Confirm prompt (Z/X/C poll test): ConfirmPrompt via GetAsyncKeyState, sticky overlay, 15s default-accept; tray Test confirm | Built 0 warn, relaunched |
| 2026-11-01 | Windows | Confirm combos + auto path: Ctrl+Alt+Z/X(/C), MaybeAutoConfirm on heur Idle→Active (Z/X only, timeout writes nothing), timeout-dismiss bug fixed | Built 0 warn; live: javaw accept → Games → auto-detect fired |
| 2026-11-01 | Windows | GPU default 5→10, wiped javaw for retest, HeuristicStart 30s this run; live: cand 54% → active in 30s → auto Ctrl+Alt+Z/X prompt → timeout-dismiss, Games still empty | Loop verified, next session re-prompts |
| 2026-11-01 | Windows | Auto-naming: ForegroundWindowText + GenericHostExes (javaw) + title-has-minecraft → prompt shows Minecraft, Accept stores "javaw.exe = Minecraft" | Built 0 warn, relaunched; pending live game session |
| 2026-11-01 | Windows | Confirm fixes: auto timeout now accepts (was dismiss — footer promised Accept), footer dropped "type freely" line | Built 0 warn, relaunched; Games still empty, next session re-prompts |
| 2026-11-01 | Windows | Confirm cleanup: stacked Accept/Reject lines, removed Dismiss keybind entirely (tray test now Z/X) | Built 0 warn, relaunched |
| 2026-11-01 | Windows | P6 hardening: Logger (ensure-dir + 512KB rotate), single-instance mutex, crash handlers, corrupt-backup + atomic save, asInvoker manifest, Start-with-Windows checkbox + HKCU Run | Build 0 warn 0 err; Release publish OK to publish/; pending user gates: clean-install + Defender scan |
| 2026-11-01 | Windows | P6 gates: wiped %AppData%/TimeNotice → fresh launch OK (PID 8900, dir + debug.log recreated, silent unknown-exe); garbage settings.json → corrupt backup + defaults, app alive (PID 12552); Defender clean per user; restored pre-P6 files, relaunched | P6 DONE |
| 2026-11-02 | Windows | Heuristic dip-grace (SuspectSeconds hold, cand-drop log) + sampler refresh-on-new-pid + null-on-no-match; Save stays open (merge baseline re-snapshot); Settings Topmost-pulse to front; build 0 warn 0 err | Pending user pull + Dota retest on gaming PC |

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
