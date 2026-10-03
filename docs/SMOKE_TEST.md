# Smoke test checklist — Port Killer

Run before tagging a release. Check each box on a clean Windows 10/11 machine
when possible.

## Build

- [ ] `dotnet build PortKiller.sln -c Release -p:Platform=x64` succeeds
- [ ] `dotnet test tests/PortKiller.Tests/PortKiller.Tests.csproj -c Release -p:Platform=x64` passes
- [ ] `.\scripts\publish-portable.ps1` produces `artifacts\portable\PortKiller-<version>-win-x64.zip`
- [ ] `.\scripts\publish-msi.ps1` produces `artifacts\msi\PortKiller-<version>-win-x64.msi`
- [ ] MSI payload contains `Uninstaller.exe` and **never** `Desinstaller.exe`

## Portable ZIP

- [ ] Extract ZIP; root has `PortKiller.exe`, `README.md`, `LICENSE`, `runtime\`
- [ ] Double-click `PortKiller.exe` starts the UI
- [ ] List shows TCP LISTENING + UDP without freezing
- [ ] Search `3000` (or a known local port) isolates the row
- [ ] Search `LISTENING` still matches when UI language is French
- [ ] Details pane lists all ports for a multi-port process
- [ ] Copy PID / path works
- [ ] F5 refreshes; Delete opens confirmation; Escape clears search

## Termination

- [ ] Terminate a disposable user process (e.g. `python -m http.server 8765`) succeeds after confirm
- [ ] PID 0 / PID 4 / self: Terminate stays disabled or shows a clear protection message
- [ ] After kill, list refreshes and success banner appears
- [ ] Access Denied (protected process without admin) shows the localized error

## Elevation & language

- [ ] Non-admin banner is visible when not elevated
- [ ] Relaunch as administrator prompts UAC (cancel leaves the app running)
- [ ] Language combo: System / English / Français; changing language restarts the app
- [ ] French Windows (or override Français): toolbar and dialogs are French; states like ÉCOUTE appear

## MSI install / uninstall

- [ ] Install MSI (SmartScreen → More info → Run anyway if unsigned)
- [ ] Desktop + Start Menu shortcuts launch the app
- [ ] `C:\Program Files\Port Killer\Uninstaller.exe` exists (name exact)
- [ ] Uninstaller confirms, removes app files, shortcuts, and leaves no `Desinstaller.exe`
