# What does this change?

## Manual gate tested (SCOPE.md §10–11)

- [ ] `dotnet build src/TimeNotice/TimeNotice.csproj` — 0 warnings
- [ ] State which gate you ran and the result (e.g. borderless no-steal, Quiet suppress/resume):

## Checklist

- [ ] No hardcoded thresholds or exe names (all in settings.json)
- [ ] Settings persist after restart; no new deps (or justified below)
- [ ] Overlay still never steals focus / blocks clicks
