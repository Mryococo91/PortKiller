param(
    [string]$PfxPath,
    [string]$CerPath,
    [string]$PasswordPath,
    [string]$ThumbprintPath
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$certDir = Join-Path $root "artifacts\certs"

if (-not $PfxPath) {
    $PfxPath = Join-Path $certDir "PortKiller.pfx"
}
if (-not $CerPath) {
    $CerPath = Join-Path $certDir "PortKiller.cer"
}
if (-not $PasswordPath) {
    $PasswordPath = Join-Path $certDir "password.txt"
}
if (-not $ThumbprintPath) {
    $ThumbprintPath = Join-Path $certDir "thumbprint.txt"
}

New-Item -ItemType Directory -Force -Path $certDir | Out-Null

function Save-SideloadMaterial {
    param($Certificate, $PlainPassword)

    $securePassword = ConvertTo-SecureString $PlainPassword -AsPlainText -Force
    Export-PfxCertificate -Cert $Certificate -FilePath $PfxPath -Password $securePassword | Out-Null
    Export-Certificate -Cert $Certificate -FilePath $CerPath | Out-Null
    Set-Content -Path $PasswordPath -Value $PlainPassword -NoNewline
    Set-Content -Path $ThumbprintPath -Value $Certificate.Thumbprint -NoNewline
}

if ((Test-Path $PfxPath) -and (Test-Path $PasswordPath)) {
    $passwordPlain = (Get-Content -Raw $PasswordPath).Trim()
    $securePassword = ConvertTo-SecureString $passwordPlain -AsPlainText -Force
    $imported = Import-PfxCertificate -FilePath $PfxPath -CertStoreLocation "Cert:\CurrentUser\My" -Password $securePassword
    Set-Content -Path $ThumbprintPath -Value $imported.Thumbprint -NoNewline
    if (-not (Test-Path $CerPath)) {
        Export-Certificate -Cert $imported -FilePath $CerPath | Out-Null
    }
    Write-Host "Sideload certificate imported into the user store ($($imported.Thumbprint))"
    exit 0
}

Write-Host "Creating a sideload certificate (CN=PortKiller)..."

$chars = [char[]]((48..57) + (65..90) + (97..122))
$passwordPlain = -join ((1..24) | ForEach-Object { $chars | Get-Random })

$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject "CN=PortKiller" `
    -KeyUsage DigitalSignature `
    -FriendlyName "Port Killer Sideload" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -HashAlgorithm SHA256 `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -KeyExportPolicy Exportable `
    -KeySpec Signature `
    -NotAfter (Get-Date).AddYears(5) `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

Save-SideloadMaterial -Certificate $cert -PlainPassword $passwordPlain

Write-Host "Certificate exported:"
Write-Host "  PFX : $PfxPath"
Write-Host "  CER : $CerPath"
Write-Host "  Thumbprint : $($cert.Thumbprint)"
exit 0
