# Port Killer

Windows utility (WinUI 3) to see **which process is using which port**
and terminate it without `netstat` / `taskkill`.

Windows only. Free, open source (MIT). No account, no cloud, no telemetry.

The UI follows the Windows display language (English and French shipped).
Source code, comments, scripts, and documentation are in English.

## Build prerequisites

- Windows 10 1809+ / Windows 11
- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Go](https://go.dev/dl/) — only to produce the portable ZIP (root launcher)

## Build for development

```powershell
dotnet build PortKiller.sln -p:Platform=x64
dotnet build PortKiller.sln -c Release -p:Platform=x64
dotnet test tests/PortKiller.Tests/PortKiller.Tests.csproj -c Release
```

Open `PortKiller.sln` in Visual Studio. Profiles:

- `PortKiller (Unpackaged)` — exe without installation
- `PortKiller (Package)` — debug MSIX

Version number lives in `Directory.Build.props` (`<Version>`). Publish scripts
read it automatically for ZIP/MSI file names.

## Distribute (GitHub / website)

Two files are enough. **No paid certificate is required.**

| File | Use |
| --- | --- |
| `PortKiller-<version>-win-x64.zip` | Portable build: extract, run `PortKiller.exe` |
| `PortKiller-<version>-win-x64.msi` | Installs to Program Files, desktop + Start Menu shortcuts, `Uninstaller.exe` at the root |

Windows may show SmartScreen ("unknown publisher") because the MSI is
unsigned. That is expected and free: **More info → Run anyway**.
After enough downloads the warning fades. A code-signing certificate is
**not required**. Details: `docs/SIGNING.md`. Manual release checklist:
`docs/SMOKE_TEST.md`.

### 1. Portable (ZIP)

```powershell
.\scripts\publish-portable.ps1
```

Output:

- `artifacts\portable\win-x64\PortKiller.exe` (root launcher, with icon)
- `artifacts\portable\win-x64\README.md` and `LICENSE`
- `artifacts\portable\win-x64\runtime\` (.NET / WinUI engine + language folders)
- `artifacts\portable\PortKiller-<version>-win-x64.zip`

After extraction the root stays readable: exe, license, readme, `runtime` folder.

Other architectures: `-Runtime win-x86` or `-Runtime win-arm64`.

### 2. Installable (MSI)

The script publishes the portable build first, then builds the `.msi` with
[WiX](https://wixtoolset.org/) (free tooling).

```powershell
.\scripts\publish-msi.ps1
```

Output: `artifacts\msi\PortKiller-1.0.0-win-x64.msi`

After install (`C:\Program Files\Port Killer`):

- `PortKiller.exe` — launch the app
- `Uninstaller.exe` — uninstall and remove files, shortcuts, and registry traces
- **Port Killer** desktop shortcut (all users)

```powershell
.\scripts\publish-msi.ps1 -Runtime win-x86 -Version 1.0.0
```

Then install:

```powershell
msiexec /i .\artifacts\msi\PortKiller-1.0.0-win-x64.msi
```

or double-click the file.

### Equivalent portable command

```powershell
dotnet publish .\PortKiller.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:Platform=x64 `
  -p:PublishProfile=portable-win-x64 `
  -p:WindowsPackageType=None `
  -p:WindowsAppSDKSelfContained=true `
  -p:SelfContained=true `
  -p:PublishSingleFile=false `
  -p:PublishTrimmed=false `
  -o .\artifacts\portable\win-x64
```

## MSIX (optional, not recommended for GitHub)

MSIX sideload requires installing a certificate and enabling developer /
sideload mode. Poor fit for a public download.

```powershell
.\scripts\publish-msix.ps1
```

## Localization

The UI uses WinUI `.resw` resources and follows the Windows display language.
English (`en-US`) is the default fallback. French (`fr-FR`) is included.
Users can override the language in the app; the choice is stored under
`%LocalAppData%\PortKiller\language.txt`.

To add a language:

1. Copy `Strings/en-US/Resources.resw` to `Strings/<culture>/Resources.resw`.
2. Translate the values.
3. Register the culture in `Helpers/LocalizationService.cs`.

## Repository layout

See `docs/ARCHITECTURE.md`, `docs/PLAN.md`, `docs/STATUS.md`,
`docs/SMOKE_TEST.md`, and `docs/SIGNING.md`.

```text
PortKiller.sln
src/PortKiller.Core/     Shared models + filter + termination
tests/PortKiller.Tests/  Unit tests (no WinUI)
.github/workflows/ci.yml Build + test on Windows
```
