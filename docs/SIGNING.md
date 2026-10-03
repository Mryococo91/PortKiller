# Code signing & SmartScreen

Port Killer ships **unsigned** by default so distribution stays free
(GitHub Releases, personal site). That is intentional.

## What users see

Windows SmartScreen may show **Windows protected your PC** / unknown publisher
for the MSI or the portable EXE.

Expected free path:

1. **More info**
2. **Run anyway**

After enough downloads from a stable reputation source, the warning usually
fades. No paid certificate is required for open-source distribution.

## Local signing script

`scripts\sign-artifacts.ps1` wraps `signtool` and reads:

| Variable | Purpose |
| --- | --- |
| `SIGNING_PFX_PATH` | Path to a `.pfx` on disk |
| `SIGNING_PFX_BASE64` | Base64-encoded `.pfx` (CI-friendly) |
| `SIGNING_PFX_PASSWORD` | Certificate password |

```powershell
$env:SIGNING_PFX_PATH = "C:\certs\portkiller.pfx"
$env:SIGNING_PFX_PASSWORD = "***"

# Publish layout without zipping, sign binaries, then zip / build MSI.
.\scripts\publish-portable.ps1 -Runtime win-x64 -SkipZip
.\scripts\sign-artifacts.ps1 -Paths @(
  ".\artifacts\portable\win-x64\PortKiller.exe",
  ".\artifacts\portable\win-x64\runtime\PortKiller.exe"
)
Compress-Archive -Path ".\artifacts\portable\win-x64\*" `
  -DestinationPath ".\artifacts\portable\PortKiller-1.2.0-win-x64.zip" -Force

.\scripts\publish-msi.ps1 -Runtime win-x64 -UninstallerOnly
.\scripts\sign-artifacts.ps1 -Paths @(".\artifacts\portable\win-x64\Uninstaller.exe")
.\scripts\publish-msi.ps1 -Runtime win-x64 -MsiOnly
.\scripts\sign-artifacts.ps1 -Paths @(".\artifacts\msi\PortKiller-1.2.0-win-x64.msi")
```

Never commit the certificate or password to git.

## GitHub Actions secrets

For the [Release workflow](../.github/workflows/release.yml):

1. Encode the PFX: `[Convert]::ToBase64String([IO.File]::ReadAllBytes('cert.pfx'))`
2. Add repository secrets:
   - `SIGNING_PFX_BASE64`
   - `SIGNING_PFX_PASSWORD`
3. Push a `v*` tag. The release job signs **before** packaging:
   - `PortKiller.exe` (launcher) and `runtime\PortKiller.exe` before the ZIP
   - `Uninstaller.exe` before the MSI bind
   - the `.msi` after it is built
   When secrets are absent, signing is skipped and unsigned artifacts are still published.

## Manual signtool

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
  /f cert.pfx /p "***" .\artifacts\portable\win-x64\PortKiller.exe
```

## MSIX sideload certificates

`scripts\publish-msix.ps1` / `ensure-sideload-cert.ps1` create a **local
development** certificate. That path is for developers only and is not a
substitute for Authenticode on public MSI/ZIP downloads.

## Policy for this repository

| Artifact | Default | Notes |
| --- | --- | --- |
| Portable ZIP | Unsigned | SmartScreen may appear |
| MSI (WiX) | Unsigned | SmartScreen may appear |
| MSIX | Dev cert only | Optional; not recommended for public GitHub |

When signing is added, keep the unsigned build instructions working for
contributors who do not have a certificate.
