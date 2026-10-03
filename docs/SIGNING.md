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

Document this in release notes and in the download page.

## Optional paid signing (later)

If you buy an Authenticode certificate (OV or EV):

1. Obtain a `.pfx` (or use a cloud HSM / Azure Trusted Signing).
2. Never commit the certificate or password to git.
3. Sign after publish, for example:

```powershell
# Example only — adjust thumbprint / file paths.
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
  /a .\artifacts\portable\win-x64\PortKiller.exe

signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
  /a .\artifacts\msi\PortKiller-1.0.0-win-x64.msi
```

4. Prefer CI secrets (`SIGNING_PFX_BASE64`, `SIGNING_PFX_PASSWORD`) over
   local copies.
5. Re-run `docs/SMOKE_TEST.md` on a machine that has never seen the app.

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
