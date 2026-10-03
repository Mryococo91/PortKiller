# Project status — Port Killer

**Current phase:** 9 / 9 complete (MVP + localization)

**Last updated:** 2026-10-03

Release engineering pass applied: solution + Core library, unit tests, CI,
central versioning, localized protocol/state labels, UI service extraction,
smoke-test and signing docs, `Uninstaller.exe` only (never `Desinstaller.exe`).

Portable (ZIP) and installable (**MSI** via WiX, no paid certificate)
distribution. Commands are in `README.md`. Version source of truth:
`Directory.Build.props`.

The MSI places a desktop shortcut (all users) and `Uninstaller.exe`
at the Program Files root: confirmation, `msiexec`, then leftover folders,
shortcuts, and registry keys are cleaned up.

The portable ZIP root contains `PortKiller.exe` (icon), `README.md`,
`LICENSE`, and `runtime/` (engine + WinUI language folders).

The app UI follows the Windows display language (`en-US` fallback, `fr-FR`
included) and can be overridden in the toolbar. Source, comments, scripts,
and docs are in English.

## Summary

Port Killer is a local WinUI 3 utility that lists Windows TCP/UDP ports
(IPv4 + IPv6) through the IP Helper API, shows the associated process,
and lets you search, inspect, and terminate a process after confirmation.

## Phases

| Phase | Status |
| --- | --- |
| 1 Continuity, structure, models | Done |
| 2 IP Helper enumeration | Done |
| 3 Process enrichment + snapshot | Done |
| 4 List UI + manual refresh | Done |
| 5 Search | Done |
| 6 Terminate a process | Done |
| 7 Details pane | Done |
| 8 Auto-refresh, keyboard, permissions, polish | Done |
| 9 Localization (system language + EN/FR) | Done |

## Engineering (post-MVP)

| Item | Status |
| --- | --- |
| `PortKiller.sln` + `src/PortKiller.Core` | Done |
| Unit tests (`PortKiller.Tests`) | Done |
| GitHub Actions CI | Done |
| Versioning via `Directory.Build.props` | Done |
| Localized TCP/UDP state labels | Done |
| Dialog / clipboard / lifecycle services | Done |
| Smoke test checklist | `docs/SMOKE_TEST.md` |
| Signing / SmartScreen notes | `docs/SIGNING.md` |
| Uninstaller name | `Uninstaller.exe` only |

## Build

```bash
dotnet build PortKiller.sln -p:Platform=x64
dotnet build PortKiller.sln -c Release -p:Platform=x64
dotnet test tests/PortKiller.Tests/PortKiller.Tests.csproj -c Release
```

Distribution:

```powershell
.\scripts\publish-portable.ps1
.\scripts\publish-msi.ps1
```

## Decisions made along the way

- `PublishTrimmed` disabled: WinUI + P/Invoke + trimming is too fragile for this utility. See `docs/ARCHITECTURE.md`.
- `QueryFullProcessImageNameW` uses `char*` (`LibraryImport` does not marshal `Span<char>` without `DisableRuntimeMarshalling`, incompatible with WinUI).
- `x:Bind` does not use `UpdateSourceTrigger=PropertyChanged` (unsupported / unstable): search lives through `TextChanged`.
- XAML helpers (`EmptyVisibility`, etc.) are instance methods, not `static`.
- UI strings live in `Strings/<culture>/Resources.resw`. Changing language restarts the process.
- Testable domain logic lives in `PortKiller.Core` so unit tests do not load WinAppSDK.
- MSI helper binary is always `Uninstaller.exe` (English). Legacy `Desinstaller.exe` is rejected by the publish script.

## Next conversation

MVP + release hardening are in place. Before a public GitHub release: run
`docs/SMOKE_TEST.md`, publish ZIP/MSI, tag `v$(Version)` from
`Directory.Build.props`.
