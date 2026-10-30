param([int]$WebPort = 8080, [string]$ApiUrl = 'http://localhost:5080', [switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not $SkipBuild) {
    Push-Location (Join-Path $RepoRoot 'client\elifoot_flutter')
    try {
        flutter build web --release --no-web-resources-cdn --pwa-strategy=none "--dart-define=API_BASE_URL=$ApiUrl"
        if ($LASTEXITCODE -ne 0) { throw 'Falha no build Flutter Web.' }
    } finally { Pop-Location }
}
node (Join-Path $PSScriptRoot 'serve-web.mjs') $WebPort
