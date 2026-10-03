param(
    [ValidateSet("win-x64", "win-x86", "win-arm64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "PortKiller.csproj"
$outDir = Join-Path $root "artifacts\msix\$Runtime"

$platform = switch ($Runtime) {
    "win-x64" { "x64" }
    "win-x86" { "x86" }
    "win-arm64" { "ARM64" }
}

Write-Host "Publishing MSIX sideload package ($Runtime)..."

& (Join-Path $PSScriptRoot "ensure-sideload-cert.ps1")
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    throw "Unable to prepare the sideload certificate."
}

$pfxPath = Join-Path $root "artifacts\certs\PortKiller.pfx"
$cerPath = Join-Path $root "artifacts\certs\PortKiller.cer"
$passwordPath = Join-Path $root "artifacts\certs\password.txt"

if (-not (Test-Path $pfxPath) -or -not (Test-Path $passwordPath)) {
    throw "Sideload certificate not found. Run scripts\ensure-sideload-cert.ps1."
}

$thumbprintPath = Join-Path $root "artifacts\certs\thumbprint.txt"
if (-not (Test-Path $thumbprintPath)) {
    throw "Certificate thumbprint not found. Run scripts\ensure-sideload-cert.ps1."
}
$thumbprint = (Get-Content -Raw $thumbprintPath).Trim()

if (Test-Path $outDir) {
    Remove-Item -Recurse -Force $outDir
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:Platform=$platform `
    -p:WindowsAppSDKSelfContained=true `
    -p:SelfContained=true `
    -p:PublishTrimmed=false `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxBundle=Never `
    -p:UapAppxPackageBuildMode=SideloadOnly `
    -p:AppxPackageDir="$outDir\" `
    -p:AppxPackageSigningEnabled=true `
    -p:PackageCertificateThumbprint="$thumbprint" `

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish MSIX failed (exit code $LASTEXITCODE)."
}

$msix = Get-ChildItem -Path $outDir -Recurse -Filter *.msix | Select-Object -First 1
$cerCopy = Join-Path $outDir "PortKiller.cer"
if (Test-Path $cerPath) {
    Copy-Item $cerPath $cerCopy -Force
}

Write-Host ""
Write-Host "MSIX ready:"
if ($msix) {
    Write-Host "  Package : $($msix.FullName)"
} else {
    Write-Host "  Folder : $outDir"
}
Write-Host "  Public certificate : $cerCopy"
Write-Host ""
Write-Host "Install (Windows 11, sideload enabled):"
Write-Host "  Add-AppxPackage -Path `"$($msix.FullName)`""
