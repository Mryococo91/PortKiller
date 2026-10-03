param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$propsPath = Join-Path $RepoRoot "Directory.Build.props"
if (-not (Test-Path $propsPath)) {
    throw "Directory.Build.props not found at $propsPath"
}

$content = Get-Content -LiteralPath $propsPath -Raw
if ($content -notmatch '<Version>\s*([^<]+?)\s*</Version>') {
    throw "Unable to read <Version> from Directory.Build.props"
}

return $Matches[1].Trim()
