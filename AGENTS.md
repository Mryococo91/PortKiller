# Port Killer — Agent guide

This repository is **Port Killer**, a Windows WinUI 3 utility that lists
occupied network ports and can terminate the associated process.

This is **not** Ordinato, not an ERP, and not a web project.

## Resume command

When the user writes `Lance la phase X`:

1. Read `docs/STATUS.md` (actual state) and `docs/PLAN.md` (phase definitions).
2. Audit the code. The code is the source of truth, not the plan.
3. If phase X-1 is not actually done, stop and list the gaps.
4. Implement **only** phase X.
5. Do not rewrite working code without a reason.
6. Build with `dotnet build`.
7. Update `docs/STATUS.md`.

## Frozen decisions

See `docs/ARCHITECTURE.md`. In short:

- WinUI 3 + Windows App SDK, Windows only
- IP Helper API (`GetExtendedTcpTable` / `GetExtendedUdpTable`), never `netstat` / `taskkill`
- Light MVVM via `CommunityToolkit.Mvvm` (no Prism, no DI container)
- P/Invoke `LibraryImport` in `Native/`
- No UAC elevation at startup
- UI follows the Windows display language (`en-US` default, `fr-FR` included)
- Source, comments, scripts, and docs are in English
- Default view: TCP LISTENING + UDP

## Build

```bash
dotnet build PortKiller.sln -p:Platform=x64
dotnet test tests/PortKiller.Tests/PortKiller.Tests.csproj -c Release
```

Distribution:

```powershell
.\scripts\publish-portable.ps1
.\scripts\publish-msi.ps1
```

Version: edit `<Version>` in `Directory.Build.props`.
MSI helper binary must be named `Uninstaller.exe` only (never `Desinstaller.exe`).
Smoke: `.\scripts\run-smoke-checks.ps1` (add `-Publish` before a release).
Tag `v*` triggers `.github/workflows/release.yml`.

See `README.md` for detailed commands.
