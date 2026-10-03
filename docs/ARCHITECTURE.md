# Architecture — Port Killer

Local Windows utility. No backend, no database, no telemetry, no external
service.

## Stack

- C# / .NET (`net10.0-windows10.0.26100.0`)
- WinUI 3
- Windows App SDK 2.5.x
- Single MSIX project (`runFullTrust`) + unpackaged profile
- `CommunityToolkit.Mvvm` only as MVVM helper

## Structure

```text
PortKiller.sln
src/PortKiller.Core/   Models, filter, display keys, termination (no WinUI).
Native/                P/Invoke (iphlpapi, kernel32). No business rules.
Services/              Port reading, process enrichment, snapshot, UI adapters.
ViewModels/            UI state, filtering, commands.
Views/                 MainPage WinUI 3.
Helpers/               Elevation, localization (WinUI ResourceLoader).
Strings/               WinUI resources (`en-US`, `fr-FR`).
App.xaml               Simple service composition.
MainWindow             Single window + Frame.
tests/PortKiller.Tests xUnit coverage for Core.
launcher/              Go launcher: AppIcon.ico, starts runtime\PortKiller.exe.
uninstaller/           Go uninstaller (MSI only): msiexec + leftover cleanup → Uninstaller.exe.
Directory.Build.props  Single Version for assemblies / ZIP / MSI names.
```

## Portable layout

```text
PortKiller.exe     Go launcher (AppIcon.ico)
README.md          End-user instructions
LICENSE            MIT license
runtime/           Self-contained WinUI publish
  PortKiller.exe   Real application (same icon)
  *.dll            .NET + Windows App SDK
  en-US/, fr-FR/   App resources + WinUI MUI (next to the DLLs, Windows requirement)
```

Short files (README, license, future settings) stay at the root.
Only the large engine goes in `runtime/`.

Culture folders cannot live in a separate `locales/` directory:
Windows loads `Microsoft.ui.xaml.dll.mui` from `<dll-folder>\<culture>\`.
Port Killer UI strings use `Strings/<culture>/Resources.resw` in source;
on publish they join the same MUI contract under `runtime/`.

## Localization

- Default / fallback: `en-US` (`DefaultLanguage` in the project file)
- Additional UI language: `fr-FR`
- Startup: follow `ApplicationLanguages` / Windows display language
- Optional override: combo box in the toolbar, persisted in
  `%LocalAppData%\PortKiller\language.txt`
- Changing language restarts the process so `x:Uid` resources reload
- Code, comments, scripts, installer markup, and docs stay in English

## Data flow

```text
iphlpapi GetExtendedTcpTable / GetExtendedUdpTable
        ↓
TcpUdpTableReader → PortEndpoint (port, proto, address, state, PID)
        ↓
ProcessInfoService → ProcessIdentity (name, path, StartTime)
        ↓
PortSnapshotService → PortEntry[]
        ↓
MainViewModel (business filter + search)
        ↓
WinUI 3 ListView + details pane
        ↓
ContentDialog (confirmation)
        ↓
ProcessTerminationService (identity check → Process.Kill)
```

## Decisions

| Topic | Choice | Why |
| --- | --- | --- |
| Port source | IP Helper `OWNER_PID` IPv4+IPv6 | Only stable API that yields the PID without a shell |
| Executable path | `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` + `QueryFullProcessImageNameW` | `Process.MainModule` often fails without admin |
| Kill | `Process.Kill()` after `{PID, StartTime, ProcessName}` | Avoids killing a reused PID |
| Default view | TCP LISTENING + UDP | ESTABLISHED connections are too noisy |
| Auto-refresh | Off by default, 5 s when enabled | Stay light in the background |
| Elevation | Never at launch; optional button | Main use (local dev) does not need admin |
| P/Invoke | Hand-written `LibraryImport` | Small surface area, no CsWin32/Vanara |
| MVVM | `ObservableObject` + `RelayCommand` / `AsyncRelayCommand` | `[ObservableProperty]` generators are less AOT/WinRT-friendly |
| Packaging | Self-contained ZIP + unsigned WiX MSI. Optional MSIX. | Free / GitHub / website, no paid certificate |
| Portable root | `PortKiller.exe` (launcher) + `runtime/` | The exe stays visible; DLLs, PRI, and `xx-XX` folders stay next to the real binary (Windows MUI contract) |
| MSI | WiX per-machine | Desktop shortcut (all users), Start Menu, `Uninstaller.exe` only at the install root (never `Desinstaller.exe`). The uninstaller runs `msiexec` then removes leftover folders, shortcuts, and registry keys. |
| Localization | `.resw` + system language, English fallback | Open-source default; French kept as a first-class UI language |
| Versioning | `Directory.Build.props` `<Version>` | Shared by `dotnet` assemblies and publish scripts (`PortKiller-<version>-win-x64.zip` / `.msi`) |
| Tests | `PortKiller.Core` + xUnit | Avoids WinAppSDK COM init in CI |
| Signing | Unsigned by default | See `docs/SIGNING.md`; SmartScreen → More info → Run anyway |

## Protected processes

Never offer to kill:

- PID 0 (Idle)
- PID 4 (System)
- The Port Killer process itself

## Permissions

The app runs at Medium IL (`asInvoker` / packaged `runFullTrust`).
Without admin, user sockets are visible; some SYSTEM services hide the
path and refuse `Kill` (Access Denied). Show a clear message, never a
silent UAC prompt.
