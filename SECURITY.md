# Security Policy

## What TimeNotice is

Local-only Windows tray app. No accounts, no cloud, no telemetry, no network listeners. Settings and logs live in `%AppData%/TimeNotice/`. No admin rights, no injection/hooks/drivers.

## Supported versions

| Version | Supported |
| ------- | --------- |
| V1 beta (`main`) | Yes |
| Older prereleases | No — please update to the latest release |

## Reporting a vulnerability

Please **do not** open a public issue for security bugs. Use GitHub's private reporting:

**Security tab → Report a vulnerability** (or contact the maintainer `@Skeeee343` privately).

Include: what you did, what you expected, app version, Windows version, and `debug.log` if relevant. Do not include passwords or unrelated personal data (`settings.json` contains only app preferences — still, trim anything you don't want to share).

You can expect an acknowledgment within a few days. As a solo project, fixes land on a best-effort basis; serious issues (e.g. anything escaping the low-privilege, local-only boundary) get priority.

## Known non-issues

- **SmartScreen warning on the release exe** — expected until signing/reputation (see `SCOPE.md` §2). Verify the download came from `github.com/Skeeee343/TimeNotice/releases`.
- **Antivirus flagging a fresh exe** — please report privately with the vendor + detection name before posting publicly.
