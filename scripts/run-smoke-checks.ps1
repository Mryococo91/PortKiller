param(
    [ValidateSet("win-x64", "win-x86", "win-arm64")]
    [string]$Runtime = "win-x64",
    [switch]$Publish
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$version = & (Join-Path $PSScriptRoot "Get-PortKillerVersion.ps1") -RepoRoot $root
$failed = @()

function Step([string]$Name, [scriptblock]$Action) {
    Write-Host ""
    Write-Host "==> $Name"
    try {
        & $Action
        Write-Host "OK  $Name"
    }
    catch {
        Write-Host "FAIL $Name : $($_.Exception.Message)"
        $script:failed += $Name
    }
}

Step "dotnet build" {
    dotnet build PortKiller.sln -c Release -p:Platform=x64 -p:PublishReadyToRun=false
    if ($LASTEXITCODE -ne 0) { throw "build failed ($LASTEXITCODE)" }
}

Step "dotnet test" {
    dotnet test tests/PortKiller.Tests/PortKiller.Tests.csproj -c Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw "tests failed ($LASTEXITCODE)" }
}

if ($Publish) {
    Step "publish portable" {
        & (Join-Path $PSScriptRoot "publish-portable.ps1") -Runtime $Runtime -Version $version
    }

    Step "publish msi" {
        & (Join-Path $PSScriptRoot "publish-msi.ps1") -Runtime $Runtime -Version $version
    }
}

$zip = Join-Path $root "artifacts\portable\PortKiller-$version-$Runtime.zip"
$msi = Join-Path $root "artifacts\msi\PortKiller-$version-$Runtime.msi"
$portableDir = Join-Path $root "artifacts\portable\$Runtime"

if (Test-Path $portableDir) {
    Step "Uninstaller.exe only" {
        if (-not (Test-Path (Join-Path $portableDir "Uninstaller.exe"))) {
            # Portable ZIP alone has no uninstaller; only required after MSI publish.
            if (Test-Path $msi) {
                throw "Uninstaller.exe missing after MSI publish"
            }
            Write-Host "  (skipped: MSI not present)"
            return
        }
        if (Test-Path (Join-Path $portableDir "Desinstaller.exe")) {
            throw "Desinstaller.exe must not exist"
        }
    }

    Step "portable root layout" {
        foreach ($name in @("PortKiller.exe", "README.md", "LICENSE", "runtime")) {
            if (-not (Test-Path (Join-Path $portableDir $name))) {
                throw "Missing $name in portable root"
            }
        }
    }
}

if (Test-Path $zip) {
    Step "ZIP present" { Write-Host "  $zip" }
}

if (Test-Path $msi) {
    Step "MSI present" { Write-Host "  $msi" }
}

Write-Host ""
if ($failed.Count -gt 0) {
    Write-Host "Smoke checks FAILED: $($failed -join ', ')"
    exit 1
}

Write-Host "Smoke checks passed (automated subset)."
Write-Host "Still run the manual UI checklist in docs/SMOKE_TEST.md on a clean machine."
exit 0
