# Project status — Port Killer

**Current phase:** 9 / 9 complete (MVP + localization) + post-MVP product pass

**Last updated:** 2026-10-03

**Version:** 1.2.0 (`Directory.Build.props`)

## Summary

Port Killer lists Windows TCP/UDP ports through the IP Helper API, shows the
associated process, and lets you search, sort, export, inspect, and terminate
a process after confirmation. EN/FR UI. GitHub CI + tag release automation.

## Engineering

| Item | Status |
| --- | --- |
| MVP phases 1–9 | Done |
| `PortKiller.Core` + unit tests | Done |
| GitHub Actions CI | Done |
| Tag release workflow (multi-arch ZIP/MSI) | Done |
| Column sort + CSV export | Done |
| User preferences persistence | Done |
| Access Denied → relaunch as admin | Done |
| Smoke script `scripts/run-smoke-checks.ps1` | Done |
| Signing script + CI secrets hook | Done (sign before ZIP/MSI) |
| README badges / downloads / screenshot | Done |
| P0: kill fail-closed + critical names | Done |
| P0: refresh/terminate serialization | Done |
| P0: uninstaller prefetch scoped to PORTKILLER* | Done |
| P1: EndpointKey remote + CSV/details | Done |
| P1: Success titles + feedback clearing | Done |
| P1: prefs Save try/catch; star local column | Done |
| P1: language restart keeps elevation | Done |
| P1: release tag/manifest version checks | Done |
| P1: uninstaller kills only under installDir | Done |
| P1: VisibleEntries keyed sync | Done |
| P2: Localize AsyncLocal + ConfigureDefault | Done |
| P2: unified settings.json (language, window, banner) | Done |
| P2: CSV BOM + formula guard; native buffer bounds | Done |
| P2: a11y tooltips; toolbar scroll; Terminate Command | Done |
| P2: FR TCP states + launcher vouvoiement | Done |
| P2: CI smoke-publish x64 job | Done |

## Build

```bash
dotnet build PortKiller.sln -p:Platform=x64
dotnet test tests/PortKiller.Tests/PortKiller.Tests.csproj -c Release
.\scripts\run-smoke-checks.ps1
```

Distribution:

```powershell
.\scripts\publish-portable.ps1
.\scripts\publish-msi.ps1
```

Release: push tag `v1.2.0` (must match `Directory.Build.props`).

## Next conversation

Manual UI smoke (`docs/SMOKE_TEST.md`) on the published artifacts.
Optional: Authenticode cert + `SIGNING_PFX_*`.
