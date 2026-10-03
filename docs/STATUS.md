# Project status — Port Killer

**Current phase:** 9 / 9 complete (MVP + localization) + post-MVP product pass

**Last updated:** 2026-10-03

**Version:** 1.1.0 (`Directory.Build.props`)

## Summary

Port Killer lists Windows TCP/UDP ports through the IP Helper API, shows the
associated process, and lets you search, sort, export, inspect, and terminate
a process after confirmation. EN/FR UI. GitHub CI + tag release automation.

## Engineering

| Item | Status |
| --- | --- |
| MVP phases 1–9 | Done |
| `PortKiller.Core` + unit tests | Done (31 tests) |
| GitHub Actions CI | Done |
| Tag release workflow (multi-arch ZIP/MSI) | Done |
| Column sort + CSV export | Done |
| User preferences persistence | Done |
| Access Denied → relaunch as admin | Done |
| Smoke script `scripts/run-smoke-checks.ps1` | Done |
| Signing script + CI secrets hook | Done |
| README badges / downloads / screenshot | Done |

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

Release: push tag `v1.1.0` (must match `Directory.Build.props`).

## Next conversation

Ship `v1.1.0` after manual UI smoke (`docs/SMOKE_TEST.md`). Optional: buy
Authenticode cert and fill `SIGNING_PFX_*` secrets.
