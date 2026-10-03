param(
    [ValidateSet("win-x64", "win-x86", "win-arm64")]
    [string]$Runtime = "win-x64",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = & (Join-Path $PSScriptRoot "Get-PortKillerVersion.ps1") -RepoRoot $root
}

$project = Join-Path $root "PortKiller.csproj"
$launcherSrc = Join-Path $root "launcher"
$outDir = Join-Path $root "artifacts\portable\$Runtime"
$publishDir = Join-Path $outDir "_publish"
$runtimeDir = Join-Path $outDir "runtime"
$zipPath = Join-Path $root "artifacts\portable\PortKiller-$Version-$Runtime.zip"
$icon = Join-Path $root "Assets\AppIcon.ico"

$platform = switch ($Runtime) {
    "win-x64" { "x64" }
    "win-x86" { "x86" }
    "win-arm64" { "ARM64" }
}

$goArch = switch ($Runtime) {
    "win-x64" { "amd64" }
    "win-x86" { "386" }
    "win-arm64" { "arm64" }
}

if (-not (Get-Command go -ErrorAction SilentlyContinue)) {
    throw "Go is required for the portable launcher. Install it from https://go.dev/dl/"
}

if (Test-Path $outDir) {
    Remove-Item -Recurse -Force $outDir
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

Write-Host "1/3 Publishing the app ($Runtime)..."

dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:Platform=$platform `
    -p:Version=$Version `
    -p:PublishProfile=portable-$Runtime `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    -p:SelfContained=true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish (app) failed (exit code $LASTEXITCODE)."
}

Write-Host "2/3 Building the launcher..."

$syso = Join-Path $launcherSrc "rsrc_windows_$goArch.syso"
$manifest = Join-Path $launcherSrc "app.manifest"
$rsrcArgs = @("-arch", $goArch, "-ico", $icon, "-manifest", $manifest, "-o", $syso)
$rsrcCmd = Get-Command rsrc -ErrorAction SilentlyContinue
if ($rsrcCmd) {
    & $rsrcCmd.Source @rsrcArgs
} else {
    go run github.com/akavel/rsrc@v0.10.2 @rsrcArgs
}

if ($LASTEXITCODE -ne 0) {
    throw "Launcher resource generation failed (exit code $LASTEXITCODE)."
}

$rootExe = Join-Path $outDir "PortKiller.exe"
$prevGoos = $env:GOOS
$prevGoarch = $env:GOARCH
$prevCgo = $env:CGO_ENABLED
$env:GOOS = "windows"
$env:GOARCH = $goArch
$env:CGO_ENABLED = "0"
try {
    go build -C $launcherSrc -ldflags="-H windowsgui -s -w" -o $rootExe
    if ($LASTEXITCODE -ne 0) {
        throw "go build (launcher) failed (exit code $LASTEXITCODE)."
    }
}
finally {
    if ($null -eq $prevGoos) { Remove-Item Env:GOOS } else { $env:GOOS = $prevGoos }
    if ($null -eq $prevGoarch) { Remove-Item Env:GOARCH } else { $env:GOARCH = $prevGoarch }
    if ($null -eq $prevCgo) { Remove-Item Env:CGO_ENABLED } else { $env:CGO_ENABLED = $prevCgo }
}

if (-not (Test-Path $rootExe)) {
    throw "Launcher not found: $rootExe"
}

Write-Host "3/3 Arranging the portable root..."

Move-Item -Path $publishDir -Destination $runtimeDir

Copy-Item -Path (Join-Path $root "LICENSE") -Destination (Join-Path $outDir "LICENSE")
Copy-Item -Path (Join-Path $root "packaging\README.md") -Destination (Join-Path $outDir "README.md")

$setIcon = Join-Path $PSScriptRoot "Set-ExeIcon.ps1"
& $setIcon -ExePath (Join-Path $runtimeDir "PortKiller.exe") -IcoPath $icon

# Windows looks up WinUI .mui files in <dll-folder>\<culture>\.
# They therefore stay in runtime\fr-FR, runtime\ja-JP, and so on.

if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

Compress-Archive -Path (Join-Path $outDir "*") -DestinationPath $zipPath -Force

$rootItems = Get-ChildItem $outDir | ForEach-Object { $_.Name }
Write-Host ""
Write-Host "Portable package ready:"
Write-Host "  Folder : $outDir"
Write-Host "  ZIP    : $zipPath"
Write-Host "  Root   : $($rootItems -join ', ')"
Write-Host "  Launch : $(Join-Path $outDir 'PortKiller.exe')"
