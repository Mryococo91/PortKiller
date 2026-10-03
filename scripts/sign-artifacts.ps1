param(
    [Parameter(Mandatory = $true)]
    [string[]]$Paths,
    [string]$Description = "Port Killer",
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"

# Optional CI / local secrets:
#   SIGNING_PFX_PATH       path to .pfx
#   SIGNING_PFX_BASE64     base64-encoded .pfx (written to a temp file)
#   SIGNING_PFX_PASSWORD   certificate password

$pfxPath = $env:SIGNING_PFX_PATH
$tempPfx = $null

try {
    if ([string]::IsNullOrWhiteSpace($pfxPath) -and -not [string]::IsNullOrWhiteSpace($env:SIGNING_PFX_BASE64)) {
        $tempPfx = Join-Path ([System.IO.Path]::GetTempPath()) ("PortKiller-sign-" + [guid]::NewGuid().ToString("N") + ".pfx")
        [IO.File]::WriteAllBytes($tempPfx, [Convert]::FromBase64String($env:SIGNING_PFX_BASE64))
        $pfxPath = $tempPfx
    }

    if ([string]::IsNullOrWhiteSpace($pfxPath)) {
        throw "No certificate configured. Set SIGNING_PFX_PATH or SIGNING_PFX_BASE64 (+ SIGNING_PFX_PASSWORD)."
    }

    $signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if (-not $signtool) {
        $candidates = @(
            "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe",
            "${env:ProgramFiles}\Windows Kits\10\bin\*\x64\signtool.exe"
        ) | ForEach-Object { Get-Item $_ -ErrorAction SilentlyContinue } | Sort-Object FullName -Descending
        if (-not $candidates) {
            throw "signtool.exe not found. Install the Windows SDK signing tools."
        }
        $signtoolPath = $candidates[0].FullName
    }
    else {
        $signtoolPath = $signtool.Source
    }

    foreach ($path in $Paths) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "File not found: $path"
        }

        $args = @(
            "sign",
            "/fd", "SHA256",
            "/td", "SHA256",
            "/tr", $TimestampUrl,
            "/d", $Description,
            "/f", $pfxPath
        )
        if (-not [string]::IsNullOrWhiteSpace($env:SIGNING_PFX_PASSWORD)) {
            $args += @("/p", $env:SIGNING_PFX_PASSWORD)
        }
        $args += $path

        Write-Host "Signing $path"
        & $signtoolPath @args
        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed for $path (exit $LASTEXITCODE)"
        }
    }

    Write-Host "Signing complete."
}
finally {
    if ($tempPfx -and (Test-Path -LiteralPath $tempPfx)) {
        Remove-Item -LiteralPath $tempPfx -Force -ErrorAction SilentlyContinue
    }
}
