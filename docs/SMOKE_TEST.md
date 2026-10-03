# Smoke test checklist — Port Killer

Run before tagging a release. Check each box on a clean Windows 10/11 machine
when possible.

## Automated subset

```powershell
.\scripts\run-smoke-checks.ps1          # build + tests (+ artifact checks if present)
.\scripts\run-smoke-checks.ps1 -Publish # also rebuild ZIP + MSI
```

## Build

- [ ] `dotnet build PortKiller.sln -c Release -p:Platform=x64` succeeds
- [ ] `dotnet test tests/PortKiller.Tests/PortKiller.Tests.csproj -c Release` passes
- [ ] `.\scripts\publish-portable.ps1` produces `artifacts\portable\PortKiller-<version>-win-x64.zip`
- [ ] `.\scripts\publish-msi.ps1` produces `artifacts\msi\PortKiller-<version>-win-x64.msi`
- [ ] MSI payload contains `Uninstaller.exe` and **never** `Desinstaller.exe`
- [ ] Optional: `win-x86` / `win-arm64` portable publish succeeds

## Portable ZIP

- [ ] Extract ZIP; root has `PortKiller.exe`, `README.md`, `LICENSE`, `runtime\`
- [ ] Double-click `PortKiller.exe` starts the UI
- [ ] List shows TCP LISTENING + UDP without freezing
- [ ] Search `3000` (or a known local port) isolates the row
- [ ] Search `LISTENING` still matches when UI language is French
- [ ] Click column headers sorts (port / PID / process / state)
- [ ] Export CSV (`Ctrl+E`) writes a file with the visible rows
- [ ] Export success banner title is **Export complete** / **Export terminé** (not “Process terminated”)
- [ ] CSV has UTF-8 BOM and columns `RemoteAddress` / `RemotePort`
- [ ] Details pane lists all ports for a multi-port process (remote endpoint when present)
- [ ] Copy PID / path works
- [ ] F5 refreshes; Delete opens confirmation; Escape clears search
- [ ] Tooltips mention shortcuts (F5, Ctrl+F, Ctrl+E, Delete)
- [ ] Auto-refresh + “All TCP” + window size + language survive an app restart (`settings.json`)
- [ ] Dismissing the elevation banner keeps it hidden after restart

## Termination

- [ ] Terminate a disposable user process (e.g. `python -m http.server 8765`) succeeds after confirm
- [ ] After kill, list refreshes and success banner title is **Process terminated**
- [ ] PID 0 / PID 4 / self / critical names (`csrss`, `lsass`, …): Terminate stays disabled
- [ ] Process without verifiable identity (no StartTime and no path): Terminate disabled / clear message
- [ ] Access Denied (protected process without admin) shows the localized error **and** a Relaunch as administrator action

## Elevation & language

- [ ] Non-admin banner is visible when not elevated (first launch)
- [ ] Relaunch as administrator prompts UAC (cancel leaves the app running)
- [ ] Language combo: System / English / Français; changing language restarts the app
- [ ] Changing language while elevated keeps the new instance elevated
- [ ] French Windows (or override Français): toolbar and dialogs are French; states like ÉCOUTE appear

## MSI install / uninstall

- [ ] Install MSI (SmartScreen → More info → Run anyway if unsigned)
- [ ] Desktop + Start Menu shortcuts launch the app
- [ ] `C:\Program Files\Port Killer\Uninstaller.exe` exists (name exact)
- [ ] Uninstaller confirms, removes app files, shortcuts, and leaves no `Desinstaller.exe`
- [ ] Uninstaller does not kill a portable `PortKiller.exe` running from another folder
