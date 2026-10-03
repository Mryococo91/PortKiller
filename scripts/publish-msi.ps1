param(
    [ValidateSet("win-x64", "win-x86", "win-arm64")]
    [string]$Runtime = "win-x64",
    [string]$Version = "",
    # Build Uninstaller.exe into an existing portable folder; skip publish + MSI.
    [switch]$UninstallerOnly,
    # Build the MSI from an already-prepared portable folder (with Uninstaller.exe).
    [switch]$MsiOnly
)

if ($UninstallerOnly -and $MsiOnly) {
    throw "Use only one of -UninstallerOnly or -MsiOnly."
}

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = & (Join-Path $PSScriptRoot "Get-PortKillerVersion.ps1") -RepoRoot $root
}

function Build-GoWindowsExe {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDir,
        [Parameter(Mandatory = $true)][string]$OutputExe,
        [Parameter(Mandatory = $true)][string]$Runtime,
        [Parameter(Mandatory = $true)][string]$Icon
    )

    if (-not (Get-Command go -ErrorAction SilentlyContinue)) {
        throw "Go is required for the MSI uninstaller. Install it from https://go.dev/dl/"
    }

    $goArch = switch ($Runtime) {
        "win-x64" { "amd64" }
        "win-x86" { "386" }
        "win-arm64" { "arm64" }
    }

    $syso = Join-Path $SourceDir "rsrc_windows_$goArch.syso"
    $manifest = Join-Path $SourceDir "app.manifest"
    $rsrcArgs = @("-arch", $goArch, "-ico", $Icon, "-manifest", $manifest, "-o", $syso)
    $rsrcCmd = Get-Command rsrc -ErrorAction SilentlyContinue
    if ($rsrcCmd) {
        & $rsrcCmd.Source @rsrcArgs
    } else {
        go run github.com/akavel/rsrc@v0.10.2 @rsrcArgs
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Uninstaller resource generation failed."
    }

    $prevGoos = $env:GOOS
    $prevGoarch = $env:GOARCH
    $prevCgo = $env:CGO_ENABLED
    $env:GOOS = "windows"
    $env:GOARCH = $goArch
    $env:CGO_ENABLED = "0"
    try {
        go build -C $SourceDir -ldflags="-H windowsgui -s -w" -o $OutputExe
        if ($LASTEXITCODE -ne 0) {
            throw "go build (uninstaller) failed (exit code $LASTEXITCODE)."
        }
    }
    finally {
        if ($null -eq $prevGoos) { Remove-Item Env:GOOS } else { $env:GOOS = $prevGoos }
        if ($null -eq $prevGoarch) { Remove-Item Env:GOARCH } else { $env:GOARCH = $prevGoarch }
        if ($null -eq $prevCgo) { Remove-Item Env:CGO_ENABLED } else { $env:CGO_ENABLED = $prevCgo }
    }

    if (-not (Test-Path $OutputExe)) {
        throw "Uninstaller not found: $OutputExe"
    }
}

$installerProject = Join-Path $root "installer\PortKiller.Installer.wixproj"
$portableDir = Join-Path $root "artifacts\portable\$Runtime"
$msiDir = Join-Path $root "artifacts\msi"

$installerPlatform = switch ($Runtime) {
    "win-x64" { "x64" }
    "win-x86" { "x86" }
    "win-arm64" { "arm64" }
}

$exe = Join-Path $portableDir "PortKiller.exe"
$uninstallerSrc = Join-Path $root "uninstaller"
$uninstallerExe = Join-Path $portableDir "Uninstaller.exe"
$icon = Join-Path $root "Assets\AppIcon.ico"
$zipPath = Join-Path $root "artifacts\portable\PortKiller-$Version-$Runtime.zip"

$publishDir = $portableDir
if (-not $publishDir.EndsWith("\") -and -not $publishDir.EndsWith("/")) {
    $publishDir += "\"
}

if (-not $MsiOnly) {
    if (-not $UninstallerOnly) {
        Write-Host "1/4 Publishing portable package ($Runtime, v$Version)..."
        & (Join-Path $PSScriptRoot "publish-portable.ps1") -Runtime $Runtime -Version $Version -SkipZip
        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
            throw "Portable publish failed."
        }

        if (-not (Test-Path $exe)) {
            throw "PortKiller.exe not found in $portableDir. Did the portable publish succeed?"
        }

        # ZIP before Uninstaller.exe so the portable archive stays launcher+runtime only.
        if (Test-Path $zipPath) {
            Remove-Item -Force $zipPath
        }
        Compress-Archive -Path (Join-Path $portableDir "*") -DestinationPath $zipPath -Force
        Write-Host "Portable ZIP : $zipPath"
    }
    elseif (-not (Test-Path $exe)) {
        throw "PortKiller.exe not found in $portableDir. Publish the portable layout first."
    }

    # Legacy French name must never ship.
    Remove-Item -LiteralPath (Join-Path $portableDir "Desinstaller.exe") -Force -ErrorAction SilentlyContinue

    Write-Host "Building Uninstaller.exe..."
    Build-GoWindowsExe -SourceDir $uninstallerSrc -OutputExe $uninstallerExe -Runtime $Runtime -Icon $icon
    & (Join-Path $PSScriptRoot "Set-ExeIcon.ps1") -ExePath $uninstallerExe -IcoPath $icon

    if (-not (Test-Path -LiteralPath $uninstallerExe)) {
        throw "Expected Uninstaller.exe at $uninstallerExe"
    }

    if (Test-Path -LiteralPath (Join-Path $portableDir "Desinstaller.exe")) {
        throw "Desinstaller.exe must not be produced. Use Uninstaller.exe only."
    }

    if ($UninstallerOnly) {
        Write-Host "Uninstaller ready: $uninstallerExe"
        return
    }
}
else {
    if (-not (Test-Path $exe)) {
        throw "PortKiller.exe not found in $portableDir. Publish the portable layout first."
    }
    if (-not (Test-Path -LiteralPath $uninstallerExe)) {
        throw "Uninstaller.exe not found in $portableDir. Run with -UninstallerOnly first."
    }
}

Write-Host "Creating the MSI ($installerPlatform)..."
dotnet build $installerProject `
    -c Release `
    -p:InstallerPlatform=$installerPlatform `
    -p:ProductVersion=$Version `
    -p:PublishDir=$publishDir

if ($LASTEXITCODE -ne 0) {
    throw "MSI creation failed (exit code $LASTEXITCODE)."
}

New-Item -ItemType Directory -Force -Path $msiDir | Out-Null
$built = Get-ChildItem -Path (Join-Path $root "artifacts\msi\$installerPlatform") -Filter *.msi |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $built) {
    throw "No .msi file was produced."
}

$finalName = "PortKiller-$Version-$Runtime.msi"
$finalPath = Join-Path $msiDir $finalName
Copy-Item $built.FullName $finalPath -Force

Write-Host ""
Write-Host "MSI ready:"
Write-Host "  $finalPath"
Write-Host ""
Write-Host "The MSI installs a desktop shortcut and Uninstaller.exe at the Program Files root."
Write-Host "The MSI is unsigned (free). Windows may show SmartScreen: More info → Run anyway."
